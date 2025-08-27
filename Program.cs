using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FolderSync.Core;
using FolderSync.Infrastructure;
using FolderSync.Validation;

namespace FolderSync
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            try
            {
                // Validate command line arguments
                var validation = ArgumentValidator.Validate(args);
                if (!validation.IsValid)
                {
                    Console.WriteLine($"Error: {validation.ErrorMessage}");
                    Console.WriteLine("Usage: FolderSync <source> <replica> <interval_seconds> <log_file>");
                    return 1;
                }

                var config = validation.Configuration!;
                
                // Initialize logger
                var logger = new CompositeLogger(config.LogFilePath);
                
                // Log startup information
                await logger.LogAsync("Starting folder synchronization...");
                await logger.LogAsync($"Source: {config.SourcePath}");
                await logger.LogAsync($"Replica: {config.ReplicaPath}");
                await logger.LogAsync($"Interval: {config.IntervalSeconds} seconds");
                await logger.LogAsync($"Log file: {config.LogFilePath}");

                // Create synchronizer
                var synchronizer = new FileSynchronizer(config, logger);

                // Setup cancellation
                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    logger.LogAsync("Shutdown signal received...").Wait();
                    cts.Cancel();
                };

                // Main synchronization loop
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await synchronizer.SynchronizeAsync();
                    }
                    catch (Exception ex)
                    {
                        await logger.LogErrorAsync($"Synchronization error: {ex.Message}");
                        // Continue running despite errors
                    }

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(config.IntervalSeconds), cts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }

                await logger.LogAsync("Stopping folder synchronization...");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error: {ex.Message}");
                return 1;
            }
        }
    }
}