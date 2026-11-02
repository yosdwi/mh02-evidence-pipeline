using System;

internal class tbl_t_control
{
	public string IncidentId { get; set; }

	public string Pid { get; set; }

	public string District { get; set; }

	public string UnitId { get; set; }

	public string Org { get; set; }

	public DBNull DeviceIp { get; set; }

	public string DeviceNumber { get; set; }

	public string VehicleStatus { get; set; }

	public string Status { get; set; }

	public double Speed { get; set; }

	public double MaxSpeed { get; set; }

	public int ViolationFrequency { get; set; }

	public DateTime ViolationTime { get; set; }

	public string ViolationCode { get; set; }

	public DateTime ShiftDate { get; set; }

	public int TotalAlarms { get; set; }

	public string SegmentRoad { get; set; }

	public string Confirmation { get; set; }

	public int Shifts { get; set; }

	public string GpsLat { get; set; }

	public string GpsLong { get; set; }

	public DateTime CreatedDate { get; set; }

	public string CreatedBy { get; set; }

	// Timing fields
	public DateTime? MergeStart { get; set; }

	public DateTime? MergeEnd { get; set; }

	public DateTime? CompressStart { get; set; }

	public DateTime? CompressEnd { get; set; }

	public DateTime? SentStart { get; set; }

	public DateTime? SentEnd { get; set; }
}
