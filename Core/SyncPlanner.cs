using System.Collections.Generic;
using System.Linq;
using FolderSync.Models;

namespace FolderSync.Core
{
    public class SyncPlanner
    {
        public List<SyncOperation> CreateSyncPlan(
            Dictionary<string, FileMetadata> source,
            Dictionary<string, FileMetadata> replica)
        {
            var operations = new List<SyncOperation>();
            
            // Files/directories to create or update
            foreach (var (path, sourceFile) in source)
            {
                if (path.EndsWith("/"))
                {
                    // Empty directory
                    if (!replica.ContainsKey(path))
                    {
                        operations.Add(new SyncOperation.CreateDirectory(path.TrimEnd('/')));
                    }
                }
                else if (!replica.TryGetValue(path, out var replicaFile))
                {
                    operations.Add(new SyncOperation.Create(path, sourceFile));
                }
                else if (NeedsUpdate(sourceFile, replicaFile))
                {
                    operations.Add(new SyncOperation.Update(path, replicaFile, sourceFile));
                }
            }
            
            // Files/directories to delete
            foreach (var (path, _) in replica)
            {
                if (!source.ContainsKey(path))
                {
                    if (path.EndsWith("/"))
                    {
                        operations.Add(new SyncOperation.DeleteDirectory(path.TrimEnd('/')));
                    }
                    else
                    {
                        operations.Add(new SyncOperation.Delete(path));
                    }
                }
            }
            
            return operations;
        }

        private bool NeedsUpdate(FileMetadata source, FileMetadata replica)
        {
            // If we have hashes, use them
            if (!string.IsNullOrEmpty(source.ContentHash) && !string.IsNullOrEmpty(replica.ContentHash))
            {
                return source.ContentHash != replica.ContentHash;
            }
            
            // Otherwise fall back to size/date comparison
            return source.Size != replica.Size || source.LastModified != replica.LastModified;
        }
    }
}