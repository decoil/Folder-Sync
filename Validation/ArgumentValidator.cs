using System;
using System.IO;
using FolderSync.Models;

namespace FolderSync.Validation
{
    public static class ArgumentValidator
    {
        public static ValidationResult Validate(string[] args)
        {
            if (args == null || args.Length != 4)
            {
                return new ValidationResult(false, "Invalid number of arguments");
            }

            var sourcePath = args[0];
            var replicaPath = args[1];
            var intervalStr = args[2];
            var logFilePath = args[3];

            // Validate source directory
            if (!Directory.Exists(sourcePath))
            {
                return new ValidationResult(false, $"Source directory does not exist: {sourcePath}");
            }

            // Validate replica path (create if doesn't exist)
            try
            {
                Directory.CreateDirectory(replicaPath);
            }
            catch (Exception ex)
            {
                return new ValidationResult(false, $"Cannot create replica directory: {ex.Message}");
            }

            // Validate interval
            if (!int.TryParse(intervalStr, out int intervalSeconds) || intervalSeconds <= 0)
            {
                return new ValidationResult(false, "Interval must be a positive integer");
            }

            // Validate log file path
            try
            {
                var logDir = Path.GetDirectoryName(logFilePath);
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
            }
            catch (Exception ex)
            {
                return new ValidationResult(false, $"Invalid log file path: {ex.Message}");
            }

            return new ValidationResult(
                true, 
                Configuration: new SyncConfiguration(sourcePath, replicaPath, intervalSeconds, logFilePath)
            );
        }
    }
}