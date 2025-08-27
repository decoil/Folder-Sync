namespace FolderSync.Models
{
    public abstract record SyncOperation
    {
        public record Create(string RelativePath, FileMetadata Metadata) : SyncOperation;
        public record Update(string RelativePath, FileMetadata OldMetadata, FileMetadata NewMetadata) : SyncOperation;
        public record Delete(string RelativePath) : SyncOperation;
        public record CreateDirectory(string RelativePath) : SyncOperation;
        public record DeleteDirectory(string RelativePath) : SyncOperation;
    }
}