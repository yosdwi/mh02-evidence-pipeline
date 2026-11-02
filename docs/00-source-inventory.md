# Existing source inventory

Sources:
- `improvment-evidence-leadtime.zip` — device recorder, evidence packager/uploader, server receiver + extractor.
- `mh02evidenceuploader-leadtime.zip` — experimental Go/rclone uploader (kept isolated).
- Reference integration repo (not imported): `yosdwi/jiep-digi-mh02-evidence-datalog-brcbmaha602` — MinIO / RabbitMQ / .NET worker.

Evidence path: DeviationCounter → DeviationRecorder (OpenCV MP4/TXT/JSON/JPG) → EvidencePackager (wait incident >120 s; FFmpeg re-encode + ZIP) → EvidenceUploader (scan every 60 s; HTTP POST ZIP; timeout 90 s) → EvidenceReceiver (ACK on ZIP saved) → EvidenceExtractor (scan every 5 s) → SQL Server stored procedure.

Known limitations: alert and evidence use independent paths; receiver success is not SQL commit; media integrity not fully validated; current fixed wait and re-encode add latency; failure must be traced per incident.

Scope excludes alert transport (`Node-RED`, Socket.IO, MongoDB→SQL dispatcher) and BEDMS push. Those components are upstream/downstream dependencies, not part of this sprint. No functionality was modified in this baseline.
