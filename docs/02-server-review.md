# Receiver/extractor scaffold audit

- `server/evidencereceiver/jiep-digi-mh02-evidence-receiver/Receiver.csproj` — ASP.NET Core .NET 8; Program.cs and `FileUploadController.cs` present.
- `server/evidenceextractor/jiep-digi-mh02-evidence-extractor/EvidenceExtractor.csproj` — .NET 8 console executable; Program.cs performs polling, ZIP extraction, stored-procedure operations.
- Both server modules have legacy nested `.git` directories in the original ZIP; removed from monorepo snapshot.
- Receiver retains per-port/persistence behavior: upload accepted means stored, not SQL inserted.
- Extractor project references DLLs via `..\Extractor_9000\*.dll` absent from ZIP; package references are also present. Must assess during build triage.
- No `.NET 8 SDK` exists in current creation environment, so build has **not** been validated.
- Current receiver/extractor are not the new MinIO/RabbitMQ worker. New worker shall be added behind MinIO object-ready manifest, using SP contract and SQL idempotency.
- Reference worker repo has reported risks: ACK even on business failure, per-object race, publisher confirms/reconciliation gaps. It is NOT blindly merged.
- Device's original per-camera config and server appsettings were excluded as live config; sanitized examples are provided.

Critical: no extraction status ACK confirms SQL transaction to device. Implementation EVD-04 needs application-level SQL_COMMITTED acknowledgement and durable SQL/idempotency tracking. For new MinIO/Rabbit worker use structured per-incident completion semantics rather than per-object trigger.
