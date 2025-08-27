namespace FolderSync.Models
{
    public record SyncConfiguration(
        string SourcePath,
        string ReplicaPath,
        int IntervalSeconds,
        string LogFilePath
    );
}

