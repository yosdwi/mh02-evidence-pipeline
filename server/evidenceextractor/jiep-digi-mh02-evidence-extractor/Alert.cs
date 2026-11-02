using System;

internal class Alert
{
	public string Filename { get; set; }

	public string IncidentId { get; set; }

	public string DeviceID { get; set; }

	public DateTime DateModified { get; set; }

	public DateTime TimeEvidence { get; set; }

	public string TextPath { get; set; }

	public string ImagePath { get; set; }

	public string VideoPath { get; set; }

	public string FullPathZip { get; set; }

	// Timing fields extracted from filename
	public long? SentStart { get; set; }

	public long? SentEnd { get; set; }
}
