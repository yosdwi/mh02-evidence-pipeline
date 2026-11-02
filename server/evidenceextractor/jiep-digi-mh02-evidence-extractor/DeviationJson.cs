using Newtonsoft.Json;

public class DeviationJson
{
	public string DeviationId { get; set; }

	public string DeviationStart { get; set; }

	public string DeviationEnd { get; set; }

	public string Speed { get; set; }

	public string Segment { get; set; }

	public string Road { get; set; }

	public string Lat { get; set; }

	public string Lon { get; set; }

	public string MaxRoadSpeed { get; set; }

	public string DeviceId { get; set; }

	public double Timestamp { get; set; }

	public string DeviationCode { get; set; }

	public string UnitNo { get; set; }

	// Timing fields (epoch milliseconds)
	[JsonProperty("merge_start")]
	public long? MergeStart { get; set; }

	[JsonProperty("merge_end")]
	public long? MergeEnd { get; set; }

	[JsonProperty("compress_start")]
	public long? CompressStart { get; set; }

	[JsonProperty("compress_end")]
	public long? CompressEnd { get; set; }
}
