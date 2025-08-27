using System;

namespace FolderSync.Models
{
    public record FileMetadata(
        string RelativePath,
        long Size,
        DateTime LastModified,
        string? ContentHash = null
    )
    {
        public bool RequiresHashComputation(FileMetadata? other)
        {
            if (other == null) return true;
            
            // Only compute hash if size or modification time changed
            return Size != other.Size || LastModified != other.LastModified;
        }
    }
}