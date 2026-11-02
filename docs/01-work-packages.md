# Work packages

1. **EVD-00 Baseline:** preserve legacy code paths, reproducible test samples, config templates, capture metrics.
2. **EVD-01 Recorder:** use FFmpeg once per incident, keep PRE/ON/POST order; benchmark CPU/latency/size/fps/overlap.
3. **EVD-02 File finalizer:** strict `RECORDING → FINALIZING → VERIFIED → READY`; ffprobe/full decode validation, SHA-256, atomic rename, no early readiness.
4. **EVD-03 Device delivery:** durable SQLite outbox, direct S3 transfer to MinIO, verify bytes/checksum, offline retry; compare Python SDK vs rclone with device/network benchmarks.
5. **EVD-04 Server ingestion:** reuse existing stored-procedure mapping; MinIO manifest-last event; RabbitMQ ack only on successful SQL write, DLQ and replay; reconciliation for lost events.

Branch pattern: `feature/evd-01-ffmpeg-recorder`, `feature/evd-02-finalizer`, `feature/evd-03-device-uploader`, `feature/evd-04-server-ingestion`.

Acceptance is measured end-to-end (trigger to SQL committed) and stage-by-stage. Do not implement API Push BC/BEDMS in this sprint.
