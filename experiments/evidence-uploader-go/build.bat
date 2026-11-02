@echo off
echo Building Evidence Uploader for Windows...

REM Set Go environment for Windows build
set GOOS=windows
set GOARCH=amd64
set CGO_ENABLED=0

REM Build the application
go build -ldflags="-s -w" -o evidence-uploader.exe .

if %ERRORLEVEL% == 0 (
    echo Build successful! Binary: evidence-uploader.exe
    echo File size:
    dir evidence-uploader.exe | findstr evidence-uploader.exe
) else (
    echo Build failed!
    exit /b 1
)

pause