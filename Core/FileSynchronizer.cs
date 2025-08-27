using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FolderSync.Infrastructure;
using FolderSync.Models;

namespace FolderSync.Core
{
    public class FileSynchronizer
    {
        private readonly SyncConfiguration _config;
        private readonly ILogger _logger;
        private readonly DirectoryScanner _scanner;
        private readonly SyncPlanner _planner;
        private readonly FileOperations _fileOps;
        private Dictionary<string, FileMetadata>? _lastSourceState;
        private Dictionary<string, FileMetadata>? _lastReplicaState;

        public FileSynchronizer(SyncConfiguration config, ILogger logger)
        {
            _config = config;
            _logger = logger;
            _scanner = new DirectoryScanner(logger);
            _planner = new SyncPlanner();
            _fileOps = new FileOperations(logger);
        }

        public async Task SynchronizeAsync()
        {
            await _logger.LogAsync($"--- Synchronization started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---");
            
            try
            {
                // Scan directories (with optimization for hash computation)
                var sourceFiles = await _scanner.ScanDirectoryAsync(_config.SourcePath, _lastSourceState);
                var replicaFiles = await _scanner.ScanDirectoryAsync(_config.ReplicaPath, _lastReplicaState);
                
                // Create sync plan
                var operations = _planner.CreateSyncPlan(sourceFiles, replicaFiles);
                
                if (!operations.Any())
                {
                    await _logger.LogAsync("No changes detected");
                }
                else
                {
                    await _logger.LogAsync($"Executing {operations.Count} operations...");
                    
                    var successCount = 0;
                    var failureCount = 0;
                    
                    foreach (var operation in operations)
                    {
                        try
                        {
                            await _fileOps.ExecuteOperationAsync(
                                _config.SourcePath, 
                                _config.ReplicaPath, 
                                operation);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            await _logger.LogErrorAsync($"Operation failed: {ex.Message}");
                            failureCount++;
                        }
                    }
                    
                    await _logger.LogAsync($"Operations completed: {successCount} successful, {failureCount} failed");
                }
                
                // Store state for next iteration (optimization)
                _lastSourceState = sourceFiles;
                _lastReplicaState = sourceFiles; // After sync, replica should match source
                
                await _logger.LogAsync("Synchronization completed successfully");
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Synchronization failed: {ex.Message}");
                throw;
            }
        }
    }
}