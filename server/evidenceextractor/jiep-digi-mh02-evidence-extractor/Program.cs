using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

internal class Program
{
	private static string ExtractedFolder;

	private static string SuccessFolder;

	private static string FailedExtractFolder;

	private static string FailedInsertFolder;

	private static string? UdpServerIp;

	private static int UdpServerPort;

	private static string? Distrik;

	private static string ConnectionString;

	private static string ReceivedFolder;

	private static SqlConnection dbConnection;

	private static string portScript;

	private static string StoredProcedureName;

	private static tbl_t_control alertEntry;

	private static async Task Main(string[] args)
	{
		IConfigurationRoot configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", optional: false, reloadOnChange: true).Build();
		Distrik = configuration.GetValue<string>("AppSettings:Distrik");
		ConnectionString = configuration.GetValue<string>("AppSettings:ConnectionString");
		portScript = configuration.GetValue<string>("AppSettings:PortScript");
		StoredProcedureName = configuration.GetValue<string>("AppSettings:StoredProcedureName") ?? "cusp_insert_t_control_from_device";
		ReceivedFolder = configuration.GetValue<string>("AppSettings:ReceivedFolder") + "\\" + portScript;
		ExtractedFolder = configuration.GetValue<string>("AppSettings:ExtractedFolder");
		SuccessFolder = configuration.GetValue<string>("AppSettings:SuccessFolder");
		FailedExtractFolder = configuration.GetValue<string>("AppSettings:FailedExtractFolder") ?? "";
		FailedInsertFolder = configuration.GetValue<string>("AppSettings:FailedInsertFolder") ?? "";
		UdpServerIp = configuration.GetValue<string>("UdpSettings:UdpServerIp");
		UdpServerPort = configuration.GetValue<int>("UdpSettings:UdpServerPort");
		if (!Directory.Exists(SuccessFolder))
		{
			Directory.CreateDirectory(SuccessFolder);
		}
		if (!Directory.Exists(FailedExtractFolder))
		{
			Directory.CreateDirectory(FailedExtractFolder);
		}
		if (!Directory.Exists(ReceivedFolder))
		{
			Directory.CreateDirectory(ReceivedFolder);
		}
		if (!Directory.Exists(ExtractedFolder))
		{
			Directory.CreateDirectory(ExtractedFolder);
		}
		if (!Directory.Exists(FailedInsertFolder))
		{
			Directory.CreateDirectory(FailedInsertFolder);
		}
		await RunProcessingLoopAsync();
	}

	private static async Task RunProcessingLoopAsync()
	{
		string serverAddress = UdpServerIp;
		int serverPort = UdpServerPort;
		var status = new
		{
			servicename = "Extractor_" + portScript
		};
		string jsonStatus = JsonConvert.SerializeObject(status);

		// Start UDP status sender in background
		var udpTask = Task.Run(async () => await SendUdpStatusContinuously(serverAddress, serverPort, jsonStatus));

		// Main processing loop
		while (true)
		{
			try
			{
				Console.WriteLine($"{DateTime.Now} Process Running...");
				await ProcessFilesAsync();
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error during processing: " + ex.Message);
			}
			await Task.Delay(5000);
		}
	}

	private static async Task SendUdpStatusContinuously(string serverAddress, int serverPort, string jsonStatus)
	{
		using UdpClient udpClient = new UdpClient();
		byte[] statusData = Encoding.UTF8.GetBytes(jsonStatus);

		while (true)
		{
			try
			{
				await udpClient.SendAsync(statusData, statusData.Length, serverAddress, serverPort);
				Console.WriteLine($"Status sent to server {serverAddress} on {serverPort}");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"UDP send error: {ex.Message}");
			}
			await Task.Delay(3000); // Send every 3 seconds
		}
	}

	private static async Task ProcessFilesAsync()
	{
		_ = 1;
		try
		{
			IEnumerable<Alert> receivedAlerts = GetReceivedAlerts();
			foreach (Alert alert in receivedAlerts)
			{
				string zipFilePath = alert.FullPathZip;
				if (File.Exists(zipFilePath))
				{
					string extractionFolder = Path.Combine(ExtractedFolder, alert.DeviceID, alert.IncidentId);
					if (await ExtractZipFileAsync(zipFilePath, extractionFolder))
					{
						await ProcessExtractedFilesAsync(alert, extractionFolder);
					}
					else
					{
						MoveFile(zipFilePath, FailedExtractFolder);
					}
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error during file processing: " + ex.Message);
		}
	}

	private static IEnumerable<Alert> GetReceivedAlerts()
	{
		List<Alert> alerts = new List<Alert>();
		string[] files = Directory.GetFiles(ReceivedFolder, "*.zip");
		foreach (string item in files)
		{
			string filename = Path.GetFileNameWithoutExtension(item);
			string[] parts = filename.Split('_');

			// Parse filename with timing: CABIN_20241220_123456_DEVICE001_1703052896123_1703052901456
			// Or old format: CABIN_20241220_123456_DEVICE001
			if (parts.Length >= 3)
			{
				DateTime dateModifiedZipWIB = File.GetCreationTime(item).AddHours(-1.0);
				string deviceId;
				string incidentId;
				long? sentStart = null;
				long? sentEnd = null;

				if (parts.Length >= 6)
				{
					// New format with timing: device_id is at index -3, sent_start at -2, sent_end at -1
					deviceId = parts[parts.Length - 3];
					sentStart = long.TryParse(parts[parts.Length - 2], out long ss) ? ss : (long?)null;
					sentEnd = long.TryParse(parts[parts.Length - 1], out long se) ? se : (long?)null;
					incidentId = string.Join("_", parts.Take(parts.Length - 3));
				}
				else
				{
					// Old format without timing
					deviceId = parts.Last();
					incidentId = string.Join("_", parts.Take(parts.Length - 1));
				}

				string s = string.Join("", parts.Skip(1).Take(parts.Length - (sentStart.HasValue ? 4 : 2)));
				DateTime.TryParseExact(s, "yyyyMMddHHmmss", null, DateTimeStyles.None, out var timeEvidence);
				string i_textPath = Path.Combine(ExtractedFolder, deviceId, incidentId, incidentId + ".txt");
				string i_imagePath = Path.Combine(ExtractedFolder, deviceId, incidentId, incidentId + ".jpg");
				string i_videoPath = Path.Combine(ExtractedFolder, deviceId, incidentId, incidentId + ".mp4");

				alerts.Add(new Alert
				{
					Filename = filename,
					IncidentId = incidentId,
					DeviceID = deviceId,
					DateModified = dateModifiedZipWIB,
					TimeEvidence = timeEvidence,
					TextPath = i_textPath,
					ImagePath = i_imagePath,
					VideoPath = i_videoPath,
					FullPathZip = item,
					SentStart = sentStart,
					SentEnd = sentEnd
				});

				Console.WriteLine($"Parsed alert: {incidentId}, Device: {deviceId}, SentStart: {sentStart}, SentEnd: {sentEnd}");
			}
		}
		return alerts;
	}

	private static async Task<bool> ExtractZipFileAsync(string zipFilePath, string extractTo)
	{
		int retryCount = 0;
		while (retryCount < 3)
		{
			try
			{
				// Validate ZIP file before extraction
				using (var archive = ZipFile.OpenRead(zipFilePath))
				{
					// Check if archive is readable and has entries
					if (archive.Entries.Count == 0)
					{
						Console.WriteLine($"ZIP file {zipFilePath} is empty");
						return false;
					}
				}

				if (Directory.Exists(extractTo))
				{
					Directory.Delete(extractTo, recursive: true);
				}

				Directory.CreateDirectory(extractTo);

				// Use overwrite mode to handle any conflicts
				ZipFile.ExtractToDirectory(zipFilePath, extractTo, overwriteFiles: true);
				Console.WriteLine("File extracted successfully to " + extractTo);
				return true;
			}
			catch (InvalidDataException ex)
			{
				Console.WriteLine($"ZIP file {zipFilePath} is corrupted: {ex.Message}");
				return false;
			}
			catch (UnauthorizedAccessException ex)
			{
				Console.WriteLine($"Access denied extracting {zipFilePath}: {ex.Message}");
				retryCount++;
			}
			catch (IOException ex)
			{
				Console.WriteLine($"IO error extracting {zipFilePath} (attempt {retryCount + 1}/3): {ex.Message}");
				retryCount++;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error extracting {zipFilePath} (attempt {retryCount + 1}/3): {ex.Message}");
				retryCount++;
			}

			if (retryCount < 3)
			{
				await Task.Delay(1000 * retryCount); // Progressive delay
			}
		}
		return false;
	}

	private static void MoveFile(string sourcePath, string destinationFolder)
	{
		try
		{
			string destPath = Path.Combine(destinationFolder, Path.GetFileName(sourcePath));
			Directory.CreateDirectory(destinationFolder);
			if (File.Exists(destPath))
			{
				File.Delete(destPath);
			}
			File.Move(sourcePath, destPath);
			Console.WriteLine("File moved to " + destPath);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error moving file {sourcePath} to {destinationFolder}: {ex.Message}");
		}
	}

	private static async Task ProcessExtractedFilesAsync(Alert alert, string extractionFolder)
	{
		_ = 1;
		try
		{
			string textFilePath = Path.Combine(extractionFolder, alert.IncidentId + ".txt");
			string jsonFilePath = Path.Combine(extractionFolder, alert.IncidentId + ".json");
			if (!File.Exists(jsonFilePath))
			{
				if (!File.Exists(textFilePath))
				{
					Console.WriteLine("Text file not found for " + textFilePath);
					return;
				}
				(double Speed, double maxSpeed, string[] ValidRow) speedFromLogs = GetSpeedFromLogs(textFilePath);
				double speed = speedFromLogs.Speed;
				double maxSpeed = speedFromLogs.maxSpeed;
				string[] validRow = speedFromLogs.ValidRow;
				alertEntry = GenerateAlertEntryFromTxt(alert, speed, maxSpeed, validRow);
				Console.WriteLine("[TXT] - Processing generate row alert from " + textFilePath);
			}
			else
			{
				try
				{
					DeviationJson? deviation = JsonConvert.DeserializeObject<DeviationJson>(File.ReadAllText(jsonFilePath));
					string unitIdFromTxt = null;
					if (File.Exists(textFilePath))
					{
						string[] lines = File.ReadAllLines(textFilePath);
						if (lines.Length != 0)
						{
							string[] columns = lines[0].Split(',');
							if (columns.Length > 7)
							{
								unitIdFromTxt = columns[6].Replace("'", "").Replace(" ", "");
							}
						}
					}
					Console.WriteLine(unitIdFromTxt);
					alertEntry = GenerateAlertEntryFromJson(deviation, alert, unitIdFromTxt);
					Console.WriteLine("[JSON] - Processing generate row alert from " + jsonFilePath);
				}
				catch (Exception ex)
				{
					Console.WriteLine("An error occurred while processing the JSON: " + ex.Message);
				}
			}
			bool insertResult = await InsertControlFromDeviceAsync(alertEntry, alert);
			if (!insertResult)
			{
				Console.WriteLine("InsertControlFromDeviceAsync failed for " + alert.Filename);
				MoveFile(Path.Combine(ReceivedFolder, alert.Filename + ".zip"), FailedInsertFolder);
				Console.WriteLine("File Move " + alert.Filename + " to " + FailedInsertFolder);
			}
			else
			{
				Console.WriteLine("All operations succeeded for " + alert.Filename);
				MoveFile(Path.Combine(alert.FullPathZip), SuccessFolder);
			}
		}
		catch (Exception ex2)
		{
			Console.WriteLine("Error processing extracted files for " + alert.Filename + ": " + ex2.Message);
			Console.WriteLine("Moving file to failed extract due to processing error: " + alert.Filename);
			MoveFile(Path.Combine(ReceivedFolder, alert.Filename + ".zip"), FailedExtractFolder);
			Console.WriteLine("File moved to " + FailedExtractFolder);
		}
	}

	private static (double Speed, double maxSpeed, string[] ValidRow) GetSpeedFromLogs(string txtPath)
	{
		double speed = 0.0;
		string[] validRow = null;
		double maxSpeed = 0.0;
		try
		{
			foreach (string item in File.ReadLines(txtPath))
			{
				string[] fields = item.Trim().Split(',');
				if (fields.Length < 8)
				{
					continue;
				}
				if (fields[3].Trim() == "ON")
				{
					if (double.TryParse(fields[13].Trim().Trim('\''), out var currentMaxSpeed))
					{
						maxSpeed = currentMaxSpeed;
					}
					if (double.TryParse(fields[7].Trim().Trim('\''), out var currentSpeed))
					{
						validRow = fields;
						if (currentSpeed > speed)
						{
							speed = currentSpeed;
							validRow = fields;
						}
					}
				}
				else if (fields[3].Trim() == "POST")
				{
					break;
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("Process Read file Logs Error: " + ex.Message);
		}
		return (Speed: speed, maxSpeed: maxSpeed, ValidRow: validRow);
	}

	private static tbl_t_control GenerateAlertEntryFromTxt(Alert alert, double speed, double maxSpeed, string[] validRow)
	{
		string i_segment = validRow[10].Replace("'", "").Replace(" ", "");
		string i_segmentRoad = validRow[11].Replace("'", "").Replace(" ", "") + " " + i_segment;
		DateTime convDatetime = alert.TimeEvidence;
		int hour = convDatetime.Hour;
		int minute = convDatetime.Minute;
		int i_shift = (((hour >= 6 && hour < 18) || (hour == 17 && minute <= 59)) ? 1 : 2);
		DateTime pelanggaranShiftDate = ((i_shift != 2 || hour >= 6) ? convDatetime.Date : convDatetime.Date.AddDays(-1.0));
		return new tbl_t_control
		{
			IncidentId = (validRow[1] ?? "").Replace("'", "").Replace(" ", ""),
			Pid = (validRow[5] + "-" + validRow[1]).Replace("'", "").Replace(" ", ""),
			District = Distrik,
			UnitId = validRow[6].Replace("'", "").Replace(" ", ""),
			Org = "PAMA",
			DeviceIp = DBNull.Value,
			DeviceNumber = alert.DeviceID,
			VehicleStatus = "-",
			Status = "DATALOG OFFLINE",
			Speed = speed,
			MaxSpeed = maxSpeed,
			ViolationFrequency = 0,
			ViolationTime = alert.TimeEvidence,
			ViolationCode = validRow[0].Replace("'", "").Replace(" ", ""),
			ShiftDate = pelanggaranShiftDate.Date,
			TotalAlarms = 0,
			SegmentRoad = (i_segmentRoad ?? ""),
			Confirmation = "open",
			Shifts = i_shift,
			GpsLat = validRow[8].Replace("'", "").Replace(" ", ""),
			GpsLong = validRow[9].Replace("'", "").Replace(" ", ""),
			CreatedDate = alert.DateModified,
			CreatedBy = "Job Evidence Device"
		};
	}

	private static DateTime? ConvertEpochToDateTime(long? epochMs)
	{
		if (!epochMs.HasValue)
			return null;

		return DateTimeOffset.FromUnixTimeMilliseconds(epochMs.Value).DateTime;
	}


	private static tbl_t_control GenerateAlertEntryFromJson(DeviationJson deviation, Alert alert, string unitId)
	{
		string i_segment = deviation.Segment;
		string i_segmentRoad = deviation.Road + " " + i_segment;
		DateTime convDatetime = alert.TimeEvidence;
		int hour = convDatetime.Hour;
		int minute = convDatetime.Minute;
		int i_shift = (((hour >= 6 && hour < 18) || (hour == 17 && minute <= 59)) ? 1 : 2);
		DateTime pelanggaranShiftDate = ((i_shift != 2 || hour >= 6) ? convDatetime.Date : convDatetime.Date.AddDays(-1.0));
		return new tbl_t_control
		{
			IncidentId = deviation.DeviationId,
			Pid = deviation.DeviceId + "-" + deviation.DeviationId,
			District = Distrik,
			UnitId = (deviation.UnitNo ?? unitId),
			Org = "PAMA",
			DeviceIp = DBNull.Value,
			DeviceNumber = deviation.DeviceId,
			VehicleStatus = "-",
			Status = "DATALOG OFFLINE",
			Speed = double.Parse(deviation.Speed),
			MaxSpeed = double.Parse(deviation.MaxRoadSpeed),
			ViolationFrequency = 0,
			ViolationTime = convDatetime,
			ViolationCode = deviation.DeviationCode,
			ShiftDate = pelanggaranShiftDate.Date,
			TotalAlarms = 0,
			SegmentRoad = (i_segmentRoad ?? ""),
			Confirmation = "open",
			Shifts = i_shift,
			GpsLat = deviation.Lat,
			GpsLong = deviation.Lon,
			CreatedDate = alert.DateModified,
			CreatedBy = "Job Evidence Device",
			// Convert epoch milliseconds to DateTime
			MergeStart = ConvertEpochToDateTime(deviation.MergeStart),
			MergeEnd = ConvertEpochToDateTime(deviation.MergeEnd),
			CompressStart = ConvertEpochToDateTime(deviation.CompressStart),
			CompressEnd = ConvertEpochToDateTime(deviation.CompressEnd),
			SentStart = ConvertEpochToDateTime(alert.SentStart),
			SentEnd = ConvertEpochToDateTime(alert.SentEnd)
		};
	}


	private static async Task<bool> InsertControlFromDeviceAsync(tbl_t_control tbl_t_controls, Alert alert)
	{
		int retryCount = 0;
		while (retryCount < 3)
		{
			try
			{
				string query = StoredProcedureName;
				using SqlConnection connection = new SqlConnection(ConnectionString);
				using SqlCommand cmd = new SqlCommand(query, connection);
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandTimeout = 30000;
				cmd.Parameters.AddWithValue("@pid", tbl_t_controls.Pid);
				cmd.Parameters.AddWithValue("@distrik", tbl_t_controls.District);
				cmd.Parameters.AddWithValue("@id_unit", tbl_t_controls.UnitId);
				cmd.Parameters.AddWithValue("@org", tbl_t_controls.Org);
				cmd.Parameters.AddWithValue("@device_ip", tbl_t_controls.DeviceIp);
				cmd.Parameters.AddWithValue("@device_number", tbl_t_controls.DeviceNumber);
				cmd.Parameters.AddWithValue("@veh_status", tbl_t_controls.VehicleStatus);
				cmd.Parameters.AddWithValue("@status", tbl_t_controls.Status);
				cmd.Parameters.AddWithValue("@speed", tbl_t_controls.Speed);
				cmd.Parameters.AddWithValue("@speed_max", tbl_t_controls.MaxSpeed);
				cmd.Parameters.AddWithValue("@pelanggaran_freq_num", tbl_t_controls.ViolationFrequency);
				cmd.Parameters.AddWithValue("@pelanggaran_waktu", tbl_t_controls.ViolationTime);
				cmd.Parameters.AddWithValue("@pelanggaran_code", tbl_t_controls.ViolationCode);
				cmd.Parameters.AddWithValue("@pelanggaran_shift_date", tbl_t_controls.ShiftDate);
				cmd.Parameters.AddWithValue("@total_alarm", tbl_t_controls.TotalAlarms);
				cmd.Parameters.AddWithValue("@segment_road", tbl_t_controls.SegmentRoad);
				cmd.Parameters.AddWithValue("@confirmation", tbl_t_controls.Confirmation);
				cmd.Parameters.AddWithValue("@shifts", tbl_t_controls.Shifts);
				cmd.Parameters.AddWithValue("@gpslat", tbl_t_controls.GpsLat);
				cmd.Parameters.AddWithValue("@gpslong", tbl_t_controls.GpsLong);
				cmd.Parameters.AddWithValue("@created_date", tbl_t_controls.CreatedDate);
				cmd.Parameters.AddWithValue("@created_by", tbl_t_controls.CreatedBy);
				// Add timing parameters
				// cmd.Parameters.AddWithValue("@merge_start", tbl_t_controls.MergeStart ?? (object)DBNull.Value);
				// cmd.Parameters.AddWithValue("@merge_end", tbl_t_controls.MergeEnd ?? (object)DBNull.Value);
				// cmd.Parameters.AddWithValue("@compress_start", tbl_t_controls.CompressStart ?? (object)DBNull.Value);
				// cmd.Parameters.AddWithValue("@compress_end", tbl_t_controls.CompressEnd ?? (object)DBNull.Value);
				// cmd.Parameters.AddWithValue("@sent_start", tbl_t_controls.SentStart ?? (object)DBNull.Value);
				// cmd.Parameters.AddWithValue("@sent_end", tbl_t_controls.SentEnd ?? (object)DBNull.Value);
				await connection.OpenAsync();
				await cmd.ExecuteNonQueryAsync();
				Console.WriteLine("InsertControlFromDeviceAsync executed successfully.");
				return true;
			}
			catch (SqlException ex)
			{
				if (ex.Message.Contains("PRIMARY KEY") || ex.Message.Contains("duplicate key") || ex.Message.Contains("UNIQUE constraint"))
				{
					Console.WriteLine("Duplicate Key Detected for PID: " + tbl_t_controls.Pid);

					// Check if video_path_server is null - if null, update it
					if (await CheckIfVideoPathServerIsNull(tbl_t_controls.Pid))
					{
						Console.WriteLine("video_path_server is null, updating for PID: " + tbl_t_controls.Pid);
						bool updateResult = await UpdateEvidenceControlAsync(tbl_t_controls, alert);
						if (updateResult)
						{
							Console.WriteLine("Successfully updated video_path_server for existing PID: " + tbl_t_controls.Pid);
							return true;
						}
						else
						{
							Console.WriteLine("Failed to update video_path_server for PID: " + tbl_t_controls.Pid);
							return false;
						}
					}
					else
					{
						Console.WriteLine("video_path_server already exists for PID: " + tbl_t_controls.Pid + ", skipping update");
						return true;
					}
				}
				Console.WriteLine("SQL Error: " + ex.Message + ", " + tbl_t_controls.Pid);
				retryCount++;
			}
			catch (Exception ex2)
			{
				Console.WriteLine("Error inserting control from device: " + ex2.Message);
				retryCount++;
			}
			if (retryCount == 3)
			{
				Console.WriteLine("Maximum retry reached. Resetting SQL Server connection...");
				ResetSqlServerConnection();
				return false;
			}
			await Task.Delay(1000 * retryCount);
		}
		return false;
	}

	private static async Task<bool> CheckIfVideoPathServerIsNull(string pid)
	{
		string query = "SELECT video_path_server FROM tbl_t_control WHERE pid = @pid";
		using SqlConnection connection = new SqlConnection(ConnectionString);
		using SqlCommand cmd = new SqlCommand(query, connection);
		cmd.Parameters.AddWithValue("@pid", pid);
		await connection.OpenAsync();
		object result = await cmd.ExecuteScalarAsync();
		return result == DBNull.Value || result == null;
	}

	private static void ResetSqlServerConnection()
	{
		try
		{
			using SqlConnection connection = new SqlConnection(ConnectionString);
			connection.Open();
			connection.Close();
			Console.WriteLine("SQL Server connection reset successfully.");
		}
		catch (Exception ex)
		{
			Console.WriteLine("Failed to reset SQL Server connection: " + ex.Message);
		}
	}

	private static async Task<bool> UpdateEvidenceControlAsync(tbl_t_control tbl_t_controls, Alert alert)
	{
		int retryCount = 0;
		while (retryCount < 3)
		{
			try
			{
				string query = "cusp_update_evidence_t_control_from_device";
				using SqlConnection connection = new SqlConnection(ConnectionString);
				using SqlCommand cmd = new SqlCommand(query, connection);
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandTimeout = 30000;
				cmd.Parameters.AddWithValue("@incident_id", tbl_t_controls.IncidentId);
				cmd.Parameters.AddWithValue("@device_id", tbl_t_controls.DeviceNumber);
				cmd.Parameters.AddWithValue("@distrik", tbl_t_controls.District);
				cmd.Parameters.AddWithValue("@sync_date", alert.DateModified);
				await connection.OpenAsync();
				await cmd.ExecuteNonQueryAsync();
				Console.WriteLine("UpdateEvidenceControlAsync executed successfully.");
				return true;
			}
			catch (Exception ex)
			{
				retryCount++;
				Console.WriteLine($"Error updating evidence control (Attempt {retryCount}/{3}): {ex.Message}");
				if (retryCount == 3)
				{
					Console.WriteLine("Maximum retry reached. Resetting SQL Server connection...");
					ResetSqlServerConnection();
					return false;
				}
				await Task.Delay(1000 * retryCount);
			}
		}
		return false;
	}
}
