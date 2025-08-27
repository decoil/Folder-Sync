using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using FolderSync.Infrastructure;
using FolderSync.Models;

namespace FolderSync.Core
{
    public class DirectoryScanner
    {
        private readonly ILogger _logger;
        private readonly MD5 _md5 = MD5.Create();

        public DirectoryScanner(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<Dictionary<string, FileMetadata>> ScanDirectoryAsync(
            string basePath, 
            Dictionary<string, FileMetadata>? previousState = null)
        {
            var files = new Dictionary<string, FileMetadata>();
            
            if (!Directory.Exists(basePath))
            {
                await _logger.LogWarningAsync($"Directory does not exist: {basePath}");
                return files;
            }

            try
            {
                var baseDir = new DirectoryInfo(basePath);
                await ScanRecursiveAsync(baseDir, baseDir, files, previousState);
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Error scanning directory {basePath}: {ex.Message}");
            }
            
            return files;
        }

        private async Task ScanRecursiveAsync(
            DirectoryInfo baseDir,
            DirectoryInfo currentDir,
            Dictionary<string, FileMetadata> files,
            Dictionary<string, FileMetadata>? previousState)
        {
            try
            {
                // Scan files
                foreach (var file in currentDir.GetFiles())
                {
                    try
                    {
                        var relativePath = Path.GetRelativePath(baseDir.FullName, file.FullName);
                        var metadata = new FileMetadata(
                            relativePath,
                            file.Length,
                            file.LastWriteTimeUtc
                        );

                        // Only compute hash if needed (file changed)
                        FileMetadata? previousFile = null;
                        previousState?.TryGetValue(relativePath, out previousFile);
                        
                        if (metadata.RequiresHashComputation(previousFile))
                        {
                            var hash = await ComputeHashAsync(file.FullName);
                            metadata = metadata with { ContentHash = hash };
                        }
                        else if (previousFile != null)
                        {
                            metadata = metadata with { ContentHash = previousFile.ContentHash };
                        }

                        files[relativePath] = metadata;
                    }
                    catch (Exception ex)
                    {
                        await _logger.LogErrorAsync($"Error processing file {file.FullName}: {ex.Message}");
                    }
                }

                // Scan subdirectories
                foreach (var subDir in currentDir.GetDirectories())
                {
                    var relativePath = Path.GetRelativePath(baseDir.FullName, subDir.FullName);
                    if (!Directory.EnumerateFileSystemEntries(subDir.FullName).Any())
                    {
                        files[$"{relativePath}/"] = new FileMetadata(
                            $"{relativePath}/",
                            0,
                            subDir.LastWriteTimeUtc
                        );
                    }
                    
                    await ScanRecursiveAsync(baseDir, subDir, files, previousState);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                await _logger.LogWarningAsync($"Access denied to {currentDir.FullName}: {ex.Message}");
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Error scanning directory {currentDir.FullName}: {ex.Message}");
            }
        }

        private async Task<string> ComputeHashAsync(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                var hashBytes = await Task.Run(() => _md5.ComputeHash(stream));
                return BitConverter.ToString(hashBytes).Replace("-", "");
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Error computing hash for {filePath}: {ex.Message}");
                return Guid.NewGuid().ToString(); // Fallback to ensure sync happens
            }
        }

        public void Dispose()
        {
            _md5?.Dispose();
        }
    }
}