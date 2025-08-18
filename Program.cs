using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FolderSync
{
   
    public record FileMetadata(
        string RelativePath,
        long Size,
        DateTime LastModified,
        string ContentHash
    );

    public record Archive(
        ImmutableDictionary<string, FileMetadata> Files,
        DateTime LastSyncTime
    );

    public abstract record SyncOperation
    {
        public record Create(string RelativePath, FileMetadata Metadata) : SyncOperation;
        public record Update(string RelativePath, FileMetadata OldMetadata, FileMetadata NewMetadata) : SyncOperation;
        public record Delete(string RelativePath) : SyncOperation;
    }

    // Update Detector - 
    public class UpdateDetector
    {
        private readonly MD5 _md5 = MD5.Create();

        public async Task<Archive> ScanDirectoryAsync(string basePath)
        {
            var files = new Dictionary<string, FileMetadata>();
            
            if (!Directory.Exists(basePath))
                return new Archive(files.ToImmutableDictionary(), DateTime.UtcNow);

            var baseDir = new DirectoryInfo(basePath);
            await ScanRecursiveAsync(baseDir, baseDir, files);
            
            return new Archive(files.ToImmutableDictionary(), DateTime.UtcNow);
        }

        private async Task ScanRecursiveAsync(
            DirectoryInfo baseDir, 
            DirectoryInfo currentDir, 
            Dictionary<string, FileMetadata> files)
        {
            foreach (var file in currentDir.GetFiles())
            {
                var relativePath = Path.GetRelativePath(baseDir.FullName, file.FullName);
                var hash = await ComputeHashAsync(file.FullName);
                
                files[relativePath] = new FileMetadata(
                    relativePath,
                    file.Length,
                    file.LastWriteTimeUtc,
                    hash
                );
            }

            foreach (var subDir in currentDir.GetDirectories())
            {
                await ScanRecursiveAsync(baseDir, subDir, files);
            }
        }

        private async Task<string> ComputeHashAsync(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            var hashBytes = await Task.Run(() => _md5.ComputeHash(stream));
            return BitConverter.ToString(hashBytes).Replace("-", "");
        }
    }

    // Reconciler - determines what operations are needed
    public class Reconciler
    {
        public ImmutableList<SyncOperation> ComputeSyncPlan(
            Archive sourceArchive,
            Archive replicaArchive)
        {
            var operations = new List<SyncOperation>();
            
            // Files to create or update
            foreach (var (path, sourceFile) in sourceArchive.Files)
            {
                if (!replicaArchive.Files.TryGetValue(path, out var replicaFile))
                {
                    operations.Add(new SyncOperation.Create(path, sourceFile));
                }
                else if (sourceFile.ContentHash != replicaFile.ContentHash)
                {
                    operations.Add(new SyncOperation.Update(path, replicaFile, sourceFile));
                }
            }
            
            // Files to delete
            foreach (var (path, _) in replicaArchive.Files)
            {
                if (!sourceArchive.Files.ContainsKey(path))
                {
                    operations.Add(new SyncOperation.Delete(path));
                }
            }
            
            return operations.ToImmutableList();
        }
    }

    // Transport Agent - performs actual file operations
    public class TransportAgent
    {
        private readonly ILogger _logger;

        public TransportAgent(ILogger logger)
        {
            _logger = logger;
        }

        public async Task ExecuteSyncPlanAsync(
            string sourcePath,
            string replicaPath,
            ImmutableList<SyncOperation> operations)
        {
            foreach (var operation in operations)
            {
                await ExecuteOperationAsync(sourcePath, replicaPath, operation);
            }
        }

        private async Task ExecuteOperationAsync(
            string sourcePath,
            string replicaPath,
            SyncOperation operation)
        {
            switch (operation)
            {
                case SyncOperation.Create create:
                    await CreateFileAsync(sourcePath, replicaPath, create.RelativePath);
                    await _logger.LogAsync($"[CREATE] {create.RelativePath}");
                    break;
                    
                case SyncOperation.Update update:
                    await UpdateFileAsync(sourcePath, replicaPath, update.RelativePath);
                    await _logger.LogAsync($"[UPDATE] {update.RelativePath}");
                    break;
                    
                case SyncOperation.Delete delete:
                    DeleteFile(replicaPath, delete.RelativePath);
                    await _logger.LogAsync($"[DELETE] {delete.RelativePath}");
                    break;
            }
        }

        private async Task CreateFileAsync(string sourcePath, string replicaPath, string relativePath)
        {
            var sourceFile = Path.Combine(sourcePath, relativePath);
            var replicaFile = Path.Combine(replicaPath, relativePath);
            
            Directory.CreateDirectory(Path.GetDirectoryName(replicaFile)!);
            await CopyFileAsync(sourceFile, replicaFile);
        }

        private async Task UpdateFileAsync(string sourcePath, string replicaPath, string relativePath)
        {
            var sourceFile = Path.Combine(sourcePath, relativePath);
            var replicaFile = Path.Combine(replicaPath, relativePath);
            
            await CopyFileAsync(sourceFile, replicaFile);
        }

        private void DeleteFile(string replicaPath, string relativePath)
        {
            var replicaFile = Path.Combine(replicaPath, relativePath);
            if (File.Exists(replicaFile))
            {
                File.Delete(replicaFile);
                
                // Clean up empty directories
                var dir = Path.GetDirectoryName(replicaFile);
                while (!string.IsNullOrEmpty(dir) && 
                       Directory.Exists(dir) && 
                       !Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir);
                    dir = Path.GetDirectoryName(dir);
                }
            }
        }

        private async Task CopyFileAsync(string source, string destination)
        {
            using var sourceStream = File.OpenRead(source);
            using var destStream = File.Create(destination);
            await sourceStream.CopyToAsync(destStream);
            
            File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(source));
        }
    }

    // Logger interface and implementation
    public interface ILogger
    {
        Task LogAsync(string message);
    }

    public class CompositeLogger : ILogger
    {
        private readonly string _logFilePath;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public CompositeLogger(string logFilePath)
        {
            _logFilePath = logFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(logFilePath) ?? ".");
        }

        public async Task LogAsync(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logMessage = $"[{timestamp}] {message}";
            
            Console.WriteLine(logMessage);
            
            await _semaphore.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(_logFilePath, logMessage + Environment.NewLine);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }

    // Archive Manager - handles persistence of archive state
    public class ArchiveManager
    {
        private readonly string _archivePath;

        public ArchiveManager(string replicaPath)
        {
            _archivePath = Path.Combine(replicaPath, ".sync_archive.json");
        }

        public async Task<Archive?> LoadArchiveAsync()
        {
            if (!File.Exists(_archivePath))
                return null;

            try
            {
                var json = await File.ReadAllTextAsync(_archivePath);
                return JsonSerializer.Deserialize<Archive>(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveArchiveAsync(Archive archive)
        {
            var json = JsonSerializer.Serialize(archive, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            await File.WriteAllTextAsync(_archivePath, json);
        }
    }

    // Main Synchronizer - orchestrates the whole process
    public class FileSynchronizer
    {
        private readonly string _sourcePath;
        private readonly string _replicaPath;
        private readonly UpdateDetector _detector;
        private readonly Reconciler _reconciler;
        private readonly TransportAgent _transport;
        private readonly ArchiveManager _archiveManager;
        private readonly ILogger _logger;

        public FileSynchronizer(string sourcePath, string replicaPath, ILogger logger)
        {
            _sourcePath = sourcePath;
            _replicaPath = replicaPath;
            _logger = logger;
            _detector = new UpdateDetector();
            _reconciler = new Reconciler();
            _transport = new TransportAgent(logger);
            _archiveManager = new ArchiveManager(replicaPath);
        }

        public async Task SynchronizeAsync()
        {
            await _logger.LogAsync("Starting synchronization...");
            
            try
            {
                // Scan source directory
                var sourceArchive = await _detector.ScanDirectoryAsync(_sourcePath);
                
                // Scan replica directory
                var replicaArchive = await _detector.ScanDirectoryAsync(_replicaPath);
                
                // Compute sync plan
                var syncPlan = _reconciler.ComputeSyncPlan(sourceArchive, replicaArchive);
                
                if (syncPlan.IsEmpty)
                {
                    await _logger.LogAsync("No changes detected.");
                }
                else
                {
                    await _logger.LogAsync($"Executing {syncPlan.Count} operations...");
                    
                    // Execute sync plan
                    await _transport.ExecuteSyncPlanAsync(_sourcePath, _replicaPath, syncPlan);
                    
                    // Save updated archive
                    await _archiveManager.SaveArchiveAsync(sourceArchive);
                }
                
                await _logger.LogAsync("Synchronization completed successfully.");
            }
            catch (Exception ex)
            {
                await _logger.LogAsync($"Error during synchronization: {ex.Message}");
                throw;
            }
        }
    }

    // Program entry point
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length != 4)
            {
                Console.WriteLine("Usage: FolderSync <source> <replica> <interval_seconds> <log_file>");
                Environment.Exit(1);
            }

            var sourcePath = args[0];
            var replicaPath = args[1];
            var intervalSeconds = int.Parse(args[2]);
            var logFilePath = args[3];

            if (!Directory.Exists(sourcePath))
            {
                Console.WriteLine($"Source directory does not exist: {sourcePath}");
                Environment.Exit(1);
            }

            Directory.CreateDirectory(replicaPath);

            var logger = new CompositeLogger(logFilePath);
            var synchronizer = new FileSynchronizer(sourcePath, replicaPath, logger);

            await logger.LogAsync($"Starting folder synchronization service");
            await logger.LogAsync($"Source: {sourcePath}");
            await logger.LogAsync($"Replica: {replicaPath}");
            await logger.LogAsync($"Interval: {intervalSeconds} seconds");

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            while (!cts.Token.IsCancellationRequested)
            {
                await synchronizer.SynchronizeAsync();
                
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cts.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            await logger.LogAsync("Synchronization service stopped.");
        }
    }
}