using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FolderSync.Infrastructure;
using FolderSync.Models;

namespace FolderSync.Core
{
    public class FileOperations
    {
        private readonly ILogger _logger;

        public FileOperations(ILogger logger)
        {
            _logger = logger;
        }

        public async Task ExecuteOperationAsync(
            string sourcePath,
            string replicaPath,
            SyncOperation operation)
        {
            try
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

                    case SyncOperation.CreateDirectory createDir:
                        CreateDirectory(replicaPath, createDir.RelativePath);
                        await _logger.LogAsync($"[CREATE DIR] {createDir.RelativePath}");
                        break;

                    case SyncOperation.DeleteDirectory deleteDir:
                        DeleteDirectory(replicaPath, deleteDir.RelativePath);
                        await _logger.LogAsync($"[DELETE DIR] {deleteDir.RelativePath}");
                        break;
                }
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Failed to execute {operation.GetType().Name} for {GetOperationPath(operation)}: {ex.Message}");
                throw;
            }
        }

        private string GetOperationPath(SyncOperation operation)
        {
            return operation switch
            {
                SyncOperation.Create create => create.RelativePath,
                SyncOperation.Update update => update.RelativePath,
                SyncOperation.Delete delete => delete.RelativePath,
                SyncOperation.CreateDirectory createDir => createDir.RelativePath,
                SyncOperation.DeleteDirectory deleteDir => deleteDir.RelativePath,
                _ => "unknown"
            };
        }

        private async Task CreateFileAsync(string sourcePath, string replicaPath, string relativePath)
        {
            var sourceFile = Path.Combine(sourcePath, relativePath);
            var replicaFile = Path.Combine(replicaPath, relativePath);
            
            var directory = Path.GetDirectoryName(replicaFile);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
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
                CleanEmptyDirectories(Path.GetDirectoryName(replicaFile), replicaPath);
            }
        }

        private void CreateDirectory(string replicaPath, string relativePath)
        {
            var fullPath = Path.Combine(replicaPath, relativePath);
            Directory.CreateDirectory(fullPath);
        }

        private void DeleteDirectory(string replicaPath, string relativePath)
        {
            var fullPath = Path.Combine(replicaPath, relativePath);
            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
            }
        }

        private void CleanEmptyDirectories(string? directory, string basePath)
        {
            while (!string.IsNullOrEmpty(directory) && 
                   directory != basePath &&
                   Directory.Exists(directory) && 
                   !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                try
                {
                    Directory.Delete(directory);
                    directory = Path.GetDirectoryName(directory);
                }
                catch
                {
                    break; // Stop if we can't delete
                }
            }
        }

        private async Task CopyFileAsync(string source, string destination)
        {
            const int bufferSize = 81920; // 80KB buffer
            
            using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
            using var destStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true);
            
            await sourceStream.CopyToAsync(destStream);
            
            // Preserve timestamps
            File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(source));
            File.SetCreationTimeUtc(destination, File.GetCreationTimeUtc(source));
        }
    }
}