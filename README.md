# FolderSync

A robust one-way folder synchronization tool for .NET, inspired by the [Unison File Synchronizer](https://web.mit.edu/6.033/2012/wwwdocs/papers/unisonimpl.pdf) architecture. FolderSync maintains an exact replica of a source folder, efficiently detecting and propagating changes while maintaining data integrity.

## Features

- **One-way synchronization** from source to replica folder
- **Content-based change detection** using MD5 hashing
- **Efficient incremental updates** - only changed files are synchronized
- **Automatic directory management** - creates and removes directories as needed
- **Comprehensive logging** to both console and file
- **Periodic synchronization** with configurable intervals
- **Graceful shutdown** with Ctrl+C support
- **Archive-based state tracking** for reliable change detection

## Installation

### Prerequisites
- .NET 6.0 or later
- Windows, Linux, or macOS

### Building from Source
```bash
git clone https://github.com/yourusername/FolderSync.git
cd FolderSync
dotnet build -c Release
```

## Usage

```bash
dotnet run -- <source> <replica> <interval_seconds> <log_file>
```

### Parameters
- `source`: Path to the source folder to synchronize from
- `replica`: Path to the replica folder to synchronize to
- `interval_seconds`: How often to perform synchronization (in seconds)
- `log_file`: Path to the log file for operation history

### Example
```bash
dotnet run -- /path/to/source /path/to/replica 60 sync.log
```

This will synchronize `/path/to/source` to `/path/to/replica` every 60 seconds, logging all operations to `sync.log`.

## Architecture

FolderSync's architecture is inspired by the Unison file synchronizer, implementing a clean separation of concerns through distinct components that handle different aspects of the synchronization process.

### Component Overview

```
┌─────────────────────────────────────────────────┐
│              FileSynchronizer                   │
│         (Main Orchestration Layer)              │
└─────────────┬───────────────────────────────────┘
              │
              ├──────────────┬──────────────┬──────────────┐
              ▼              ▼              ▼              ▼
┌──────────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│  UpdateDetector  │ │  Reconciler  │ │TransportAgent│ │ArchiveManager│
│                  │ │              │ │              │ │              │
│  Scans folders   │ │  Computes    │ │  Executes    │ │  Persists    │
│  & computes      │ │  sync plan   │ │  file ops    │ │  archive     │
│  content hashes  │ │  (diff)      │ │              │ │  state       │
└──────────────────┘ └──────────────┘ └──────────────┘ └──────────────┘
```

### Core Components

#### 1. **Update Detector**
- Recursively scans directories to build a complete file inventory
- Computes MD5 content hashes for each file
- Creates an immutable `Archive` snapshot representing the current state
- Similar to Unison's archive building phase

#### 2. **Reconciler**
- Compares source and replica archives to determine required operations
- Generates an immutable sync plan with three operation types:
  - `Create`: File exists in source but not in replica
  - `Update`: File exists in both but content differs
  - `Delete`: File exists in replica but not in source
- Implements Unison's reconciliation algorithm for one-way sync

#### 3. **Transport Agent**
- Executes the sync plan by performing actual file operations
- Handles file copying with proper error handling
- Preserves file modification times
- Automatically manages directory creation and cleanup
- Corresponds to Unison's transport layer

#### 4. **Archive Manager**
- Persists archive state to `.sync_archive.json` in the replica folder
- Enables future optimizations for detecting changes since last sync
- Provides crash recovery capabilities

### Data Structures

The system uses immutable data structures throughout for thread safety and reliability:

- **FileMetadata**: Immutable record containing file path, size, modification time, and content hash
- **Archive**: Immutable snapshot of a directory's state at a point in time
- **SyncOperation**: Discriminated union representing the three types of sync operations

### Synchronization Flow

1. **Scanning Phase**: UpdateDetector scans both source and replica directories
2. **Reconciliation Phase**: Reconciler compares archives and generates sync plan
3. **Propagation Phase**: TransportAgent executes the sync plan
4. **Persistence Phase**: ArchiveManager saves the new state

This architecture ensures:
- **Reliability**: Each phase is isolated and can be retried independently
- **Efficiency**: Only changed files are processed
- **Consistency**: Operations are applied atomically
- **Observability**: All operations are logged for audit trails

## Features in Detail

### Change Detection
Files are considered changed when their MD5 hash differs, ensuring content-based comparison rather than relying solely on timestamps or file sizes.

### Logging
The `CompositeLogger` provides dual logging to:
- Console for real-time monitoring
- Log file for persistent audit trail

Each operation is timestamped and categorized (CREATE, UPDATE, DELETE).

### Error Handling
- Graceful handling of missing directories
- Automatic creation of replica directory if it doesn't exist
- Comprehensive exception logging
- Thread-safe file operations

## License

MIT
