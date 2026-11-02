# Evidence Uploader Go

High-performance Go rewrite of the Python evidence uploader with improved reliability and performance.

## Features

- **Zero Race Conditions**: Proper timeout handling and process management
- **Better Performance**: Native Go concurrency vs Python subprocess
- **Robust Error Handling**: Exponential backoff, circuit breaker pattern
- **Graceful Shutdown**: Clean stop with signal handling
- **Enhanced Monitoring**: Detailed stats and structured logging

## Quick Start

### Build
```bash
# Windows
build.bat

# Or manually
go build -o evidence-uploader.exe .
```

### Run
```bash
# Direct run
./evidence-uploader.exe

# Or with custom rclone config path
./evidence-uploader.exe
```

## Configuration

The application uses the same configuration sources as the Python version:
- Device ID from: `D:\smartd\script\mh02model\files\configs\smartd.json`
- Rclone config: `./rclone.conf`
- Status output: `D:\pama\ftp\evidence_status_uploader.json`
- Stop flag: `D:\smartd\script\mh02evidenceuploader\evidenceuploadstop.id`

## Deployment

### Replace Python Version
1. Stop existing Python service
2. Build Go version: `build.bat`
3. Start: `evidence-uploader.exe`
4. Monitor status JSON for verification

### Run Both (Testing)
The Go version writes status with `"uploader_type": "RCLONE-GO"` to distinguish from Python version.

## Architecture

```
evidence-uploader/
├── config/     # Configuration loading & validation
├── evidence/   # Directory scanning & evidence validation
├── sync/       # Rclone operations & retry logic
├── status/     # JSON status reporting
└── main.go     # Entry point & orchestration
```

## Improvements Over Python

1. **Timeout Handling**: No more subprocess vs rclone timeout race conditions
2. **Resource Usage**: ~10MB memory vs ~50MB Python
3. **Error Recovery**: Smart retry with exponential backoff
4. **Process Management**: Clean shutdown, no zombies
5. **Performance**: Native concurrency, no GIL limitations

## Monitoring

Status JSON includes additional Go-specific metrics:
- `total_syncs`: Total sync operations attempted
- `successful_syncs`: Number of successful syncs
- `failed_syncs`: Number of failed syncs
- `last_sync_time`: Timestamp of last sync
- `last_error`: Last error message (if any)