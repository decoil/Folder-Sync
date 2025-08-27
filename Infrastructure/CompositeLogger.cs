using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FolderSync.Infrastructure
{
    public class CompositeLogger : ILogger
    {
        private readonly string _logFilePath;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public CompositeLogger(string logFilePath)
        {
            _logFilePath = logFilePath;
            
            try
            {
                var directory = Path.GetDirectoryName(logFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Could not create log directory: {ex.Message}");
            }
        }

        public Task LogAsync(string message) => WriteLogAsync("INFO", message);
        public Task LogErrorAsync(string message) => WriteLogAsync("ERROR", message);
        public Task LogWarningAsync(string message) => WriteLogAsync("WARN", message);

        private async Task WriteLogAsync(string level, string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logMessage = $"[{timestamp}] [{level}] {message}";
            
            // Always write to console
            Console.WriteLine(logMessage);
            
            // Try to write to file
            await _semaphore.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(_logFilePath, logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to write to log file: {ex.Message}");
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}