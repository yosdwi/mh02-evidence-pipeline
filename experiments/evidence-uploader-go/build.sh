#!/bin/bash

echo "Building Evidence Uploader for Windows..."

# Set Go environment for Windows cross-compilation
export GOOS=windows
export GOARCH=amd64
export CGO_ENABLED=0

# Build the application
echo "Cross-compiling to Windows x64..."
go build -ldflags="-s -w" -o evidence-uploader.exe .

if [ $? -eq 0 ]; then
    echo "✅ Build successful! Binary: evidence-uploader.exe"
    echo "File info:"
    ls -lh evidence-uploader.exe
    echo ""
    echo "Windows executable ready for deployment!"
    echo "Size: $(du -h evidence-uploader.exe | cut -f1)"
else
    echo "❌ Build failed!"
    exit 1
fi

echo ""
echo "To run on Windows:"
echo "1. Copy evidence-uploader.exe to Windows machine"
echo "2. Ensure rclone.exe is available in PATH or at D:\\smartd\\script\\rclone\\rclone.exe"
echo "3. Place rclone.conf in same directory"
echo "4. Run: evidence-uploader.exe"