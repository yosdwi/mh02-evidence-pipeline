using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text;

[ApiController]
[Route("/files")]
public class FileUploadController : ControllerBase
{
    private readonly string _tempDir;
    private readonly string _finalDir;
    private readonly string _serverIP;
    private readonly int _udpPort;
    private readonly string? _serviceName;
    private readonly ILogger<FileUploadController> _logger;
    private readonly IPEndPoint _serverAddress;

    public FileUploadController(IConfiguration configuration, ILogger<FileUploadController> logger)
    {
        _logger = logger;
        
        _tempDir = configuration["FileSettings:TempDir"];
        _finalDir = configuration["FileSettings:FinalDir"];
        _serverIP = configuration.GetValue<string>("ServerSettings:ServerIP");
        _udpPort = configuration.GetValue<int>("ServerSettings:UDPPort");
        _serviceName = configuration.GetValue<string>("ServiceName");

        IPAddress pserverIP = IPAddress.Parse(_serverIP);

        _serverAddress = new IPEndPoint(pserverIP, _udpPort);

        EnsureDirectoriesExist();

    }

    //private void SendStatus(int port, string status, string serviceName)
    //{
    //    using (var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
    //    {
    //        var payload = JsonSerializer.Serialize(new { status, service = serviceName });
    //        var data = Encoding.UTF8.GetBytes(payload);
    //        sock.SendTo(data, _serverAddress);

    //        _logger.LogInformation($"Status sent: {payload}");
    //    }
    //}

    //private void SendStatus(CancellationToken cancellationToken)
    public async Task SendStatus(CancellationToken cancellationToken)
    {
        var cancellationTokenSource = new CancellationTokenSource();

        // Memulai pengiriman status
        var sendStatusTask = SendStatus(cancellationTokenSource.Token);

        // Menunggu beberapa waktu sebelum membatalkan tugas (misalnya, 30 detik)
        await Task.Delay(TimeSpan.FromSeconds(30));
        cancellationTokenSource.Cancel();

        await sendStatusTask; // Menunggu tugas selesai dengan aman

        // try
        // {
        //     while (!cancellationToken.IsCancellationRequested)
        //     {
        //         var status = "OK";
        //         using (var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
        //         {
        //             var payload = JsonSerializer.Serialize(new { status, service = _serviceName });
        //             var data = Encoding.UTF8.GetBytes(payload);
        //             sock.SendTo(data, _serverAddress);

        //             _logger.LogInformation($"Status sent: {payload}");
        //         }

        //         await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        //     }
        // }
        // catch (TaskCanceledException)
        // {
        //     _logger.LogInformation("SendStatus task was canceled.");
        // }
        // catch (Exception ex)
        // {
        //     Console.WriteLine($"[ERROR] : {ex}");
        // }
    }


    private void EnsureDirectoriesExist()
    {
      
        try
        {
            if (!Directory.Exists(_tempDir))
            {
                Directory.CreateDirectory(_tempDir);
                _logger.LogInformation("Temporary directory created: {TempDir}", _tempDir);
            }

            if (!Directory.Exists(_finalDir))
            {
                Directory.CreateDirectory(_finalDir);
                _logger.LogInformation("Final directory created: {FinalDir}", _finalDir);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize directories.");
            throw;
        }
    }

    [HttpPost]
    public async Task<IActionResult> UploadFile([FromQuery] string deviceid, [FromQuery] string? sent_start, IFormFile file)
    {

        if (file == null || string.IsNullOrEmpty(deviceid))
        {
            _logger.LogWarning("Upload failed: Missing parameter.");
            return Content("FAILED", "text/plain");
        }

        // Generate sent_end timestamp when file is received (epoch format)
        var sent_end = ((DateTimeOffset)DateTime.Now).ToUnixTimeMilliseconds().ToString();

        // Create filename with timing info: originalname_deviceid_sentstart_sentend.zip
        var originalName = Path.GetFileNameWithoutExtension(file.FileName);
        var fileName = string.IsNullOrEmpty(sent_start)
            ? $"{originalName}_{deviceid}.zip"
            : $"{originalName}_{deviceid}_{sent_start}_{sent_end}.zip";

        var tempFilePath = Path.Combine(_tempDir, fileName);
        var finalFilePath = Path.Combine(_finalDir, fileName);

        
        try
        {
            // Remove existing file in temp if exists
            if (System.IO.File.Exists(tempFilePath))
            {
                System.IO.File.Delete(tempFilePath);
                _logger.LogInformation("Existing temp file deleted: {TempFilePath}", tempFilePath);
            }

            // Save file to temp directory
            await using (var stream = new FileStream(tempFilePath, FileMode.CreateNew))
            {
                await file.CopyToAsync(stream);
                _logger.LogInformation("File saved to temporary directory: {TempFilePath}", tempFilePath);
            }

            // Validate ZIP file
            if (!ValidateZipFile(tempFilePath))
            {
                System.IO.File.Delete(tempFilePath);
                _logger.LogWarning("Invalid ZIP file deleted: {TempFilePath}", tempFilePath);
                return Content("CORRUPT", "text/plain");
            }

            // Move file to final directory
            if (System.IO.File.Exists(finalFilePath))
            {
                System.IO.File.Delete(finalFilePath);
                _logger.LogInformation("Existing final file replaced: {FinalFilePath}", finalFilePath);
            }

            System.IO.File.Move(tempFilePath, finalFilePath);
            _logger.LogInformation("File moved to final directory: {FinalFilePath}", finalFilePath);
            _logger.LogInformation("Timing info - sent_start: {SentStart}, sent_end: {SentEnd}", sent_start ?? "N/A", sent_end);

            //SendStatus(_udpPort, "OK", _serviceName);

            return Content("OK", "text/plain");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error for device ID: {DeviceID}", deviceid);
            return Content("INTERNALSERVERERROR", "text/plain");
        }
    }

    private bool ValidateZipFile(string filePath)
    {
        try
        {
            using var zip = ZipFile.OpenRead(filePath);
            if (zip.Entries.Count == 0)
            {
                _logger.LogWarning("ZIP validation failed: File is empty - {FilePath}", filePath);
                return false; 
            }
            _logger.LogInformation("ZIP validation passed: {FilePath}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ZIP validation failed: {FilePath}", filePath);
            return false;
        }
    }
}
