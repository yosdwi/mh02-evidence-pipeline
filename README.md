# MH02 Evidence Pipeline

Baseline sumber untuk refactor bertahap evidence device → network → server on-premise → SQL Server.

**Status:** source snapshot, bukan deployable release. Kode existing belum diubah untuk FFmpeg / S3.

## Komponen

| Lokasi | Komponen | Status |
|---|---|---|
| `device/smartd/script/mh02model/apps/mh02/` | DeviationCounter, Cabin/Front Recorders, processor dependencies | Existing snapshot |
| `device/smartd/script/mh02evidenceuploader/` | EvidencePackager, EvidenceUploader | Existing snapshot |
| `server/evidencereceiver/jiep-digi-mh02-evidence-receiver/` | ASP.NET Core receiver, `.csproj/.sln` | Existing snapshot |
| `server/evidenceextractor/jiep-digi-mh02-evidence-extractor/` | .NET extractor + SQL stored procedure call, `.csproj` | Existing snapshot |
| `experiments/evidence-uploader-go/` | Go/rclone uploader alternative | **Eksperimen, bukan active pipeline** |
| `docs/` | Baseline, gap, work packages, diagram | Documentation |

### First implementation sequence

- `EVD-01` FFmpeg single-pass recorder (Cabin/Front) tanpa merusak trigger/metadata.
- `EVD-02` finalize, media validation, checksum, atomic READY.
- `EVD-03` persistent device outbox + S3 uploader, benchmark vs rclone.
- `EVD-04` MinIO/RabbitMQ ingestion, idempotent SQL commit + end-to-end ACK.

### Security + local runtime

Konfigurasi dengan endpoint, DSN, credential, IP kamera tidak diikutkan mentah-mentah. File `*.example.json` / `*.example.conf` adalah template. Buat salinan lokal ke nama asli sebelum menjalankan service; **jangan commit salinan tersebut**.

Original ZIP included nested `.git` folders, compiled binaries, device media files, and live configurations; these are intentionally excluded. `init.md` baseline interpreted in `docs/00-source-inventory.md`.

### Server build status

The receiver and extractor project definitions are included, **not proven buildable**. Extractor `.csproj` retains legacy `HintPath` references to `../Extractor_9000/*.dll` absent from the ZIP. Resolve these after capturing a baseline; do not claim green build until tested with .NET 8 SDK and real config.

### Git repository

The archive is a local Git repository with baseline commit; remote `origin` not configured. To publish, create a **private** `yosdwi/mh02-evidence-pipeline` repository, then push `main`. See `docs/03-publish.md`.
