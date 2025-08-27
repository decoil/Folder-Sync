# FolderSync

A production-ready .NET folder synchronization service that maintains an exact replica of a source directory with comprehensive logging, hash-based integrity verification, and optimized performance.

## Quick Start

```bash
# Build the application
dotnet build --configuration Release

# Run synchronization
./FolderSync "/path/to/source" "/path/to/replica" 300 "/path/to/sync.log"
```

## Installation

### Prerequisites
- .NET 6.0 SDK or later
- Operating System: Windows, macOS, or Linux

### Build from Source
```bash
git clone https://github.com/decoil/Folder-Sync.git
cd Folder-Sync
dotnet restore
dotnet build --configuration Release
```

## Usage

```
FolderSync <source> <replica> <interval_seconds> <log_file>
```

### Arguments
- `source`: Source directory path (must exist)
- `replica`: Replica directory path (created if missing)
- `interval_seconds`: Synchronization interval in seconds (positive integer)
- `log_file`: Log file path (directory created if missing)

### Examples

```bash
# Sync every 5 minutes with detailed logging
./FolderSync "/home/user/documents" "/backup/documents" 300 "/var/log/foldersync.log"

# Development sync every 30 seconds
./FolderSync "./src" "./backup" 30 "./sync.log"
```

## Features

### Core Functionality
- **One-way synchronization**: Maintains replica as exact copy of source
- **Continuous monitoring**: Configurable interval-based synchronization
- **Hash verification**: MD5 content hashing for integrity validation
- **Directory management**: Full support for files, subdirectories, and empty directories
- **Atomic operations**: Individual file operations with failure isolation

### Performance Optimizations
- **Selective hashing**: Content hash computed only when file metadata changes
- **State caching**: Metadata persistence between sync cycles
- **Async I/O**: Non-blocking file operations with 80KB buffer size
- **Efficient scanning**: Skip unchanged files based on size/timestamp comparison

### Reliability
- **Graceful error handling**: Service continues despite individual file failures
- **Signal handling**: Clean shutdown on CTRL+C
- **Comprehensive logging**: Dual output to console and file with structured format
- **Timestamp preservation**: Maintains original file creation and modification times

## Architecture

### Project Structure
```
FolderSync/
├── Program.cs                  # Entry point and service orchestration
├── Models/
│   ├── FileMetadata.cs        # Immutable file metadata records
│   ├── SyncConfiguration.cs   # Configuration data structure
│   ├── SyncOperation.cs       # Operation discriminated union
│   └── ValidationResult.cs    # Validation result container
├── Core/
│   ├── FileSynchronizer.cs    # Main synchronization coordinator
│   ├── DirectoryScanner.cs    # Recursive directory traversal
│   ├── SyncPlanner.cs         # Diff algorithm implementation
│   └── FileOperations.cs      # File system operation executor
├── Infrastructure/
│   ├── ILogger.cs             # Logging abstraction
│   └── CompositeLogger.cs     # Console and file logging implementation
└── Validation/
    └── ArgumentValidator.cs    # Command-line argument validation
```

### Synchronization Algorithm

1. **Directory Scanning**
   - Recursive traversal of source and replica directories
   - Collection of file metadata (path, size, timestamps)
   - Selective hash computation for modified files only

2. **Change Detection**
   - Comparison of source and replica metadata
   - Identification of create, update, and delete operations
   - Empty directory handling

3. **Operation Planning**
   - Generation of minimal operation set
   - Prioritization of operations for consistency

4. **Execution**
   - Atomic file operations with individual error handling
   - Progress logging and error reporting
   - State persistence for next iteration

### Data Structures

#### FileMetadata Record
```csharp
public record FileMetadata(
    string RelativePath,
    long Size,
    DateTime LastModified,
    string? ContentHash = null
);
```

#### Sync Operations
- `Create(string, FileMetadata)` - New file creation
- `Update(string, FileMetadata, FileMetadata)` - File modification
- `Delete(string)` - File removal
- `CreateDirectory(string)` - Directory creation
- `DeleteDirectory(string)` - Directory removal

## Configuration

### Command Line Validation
- Source directory existence verification
- Replica directory creation (if missing)
- Positive integer interval validation
- Log file path accessibility check

### Runtime Behavior
- **Hash Computation**: Triggered only when file size or modification time changes
- **Error Recovery**: Individual operation failures logged but don't halt service
- **Empty Directory Handling**: Preserved in replica with trailing slash notation
- **Cleanup**: Orphaned empty directories automatically removed

## Logging

### Log Format
```
[YYYY-MM-DD HH:mm:ss.fff] [LEVEL] Message
```

### Log Levels
- `INFO`: Normal operations and status updates
- `WARN`: Non-critical issues (access denied, missing files)
- `ERROR`: Operation failures and exceptions

### Sample Output
```
[2024-01-15 10:30:45.123] [INFO] === Folder Synchronization Service Started ===
[2024-01-15 10:30:45.124] [INFO] Source: /home/user/documents
[2024-01-15 10:30:45.125] [INFO] Replica: /backup/documents
[2024-01-15 10:30:47.331] [INFO] --- Synchronization started at 2024-01-15 10:30:47 ---
[2024-01-15 10:30:47.445] [INFO] Executing 3 operations...
[2024-01-15 10:30:47.446] [INFO] [CREATE] documents/report.pdf
[2024-01-15 10:30:47.512] [INFO] [UPDATE] documents/readme.txt
[2024-01-15 10:30:47.513] [INFO] [DELETE] documents/old_file.tmp
[2024-01-15 10:30:47.514] [INFO] Operations completed: 3 successful, 0 failed
```

## Performance Characteristics

### Computational Complexity
- **Memory Usage**: O(n) where n = total file count
- **Hash Computation**: O(m) where m = modified files only
- **Directory Traversal**: O(n) with early termination optimizations

### I/O Optimization
- Asynchronous file operations prevent blocking
- 80KB buffer size for optimal throughput
- Minimal redundant filesystem access through state caching

## Error Handling

### Recoverable Errors
- Individual file access failures
- Temporary I/O exceptions
- Permission denied errors

### Fatal Errors  
- Invalid command-line arguments
- Source directory inaccessibility
- Log file creation failures

### Exit Codes
- `0`: Successful execution
- `1`: Fatal error or invalid arguments

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Setup
```bash
git clone https://github.com/decoil/Folder-Sync.git
cd Folder-Sync
dotnet restore
dotnet build
dotnet test  # Run unit tests
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Technical Notes

- **Thread Safety**: Service uses `SemaphoreSlim` for log file synchronization
- **Platform Compatibility**: Cross-platform path handling with `Path.GetRelativePath()`
- **Resource Management**: Proper disposal of `MD5` instances and file streams
- **Signal Handling**: `Console.CancelKeyPress` for graceful shutdown
- **Async Patterns**: Consistent use of `async/await` throughout the codebase
