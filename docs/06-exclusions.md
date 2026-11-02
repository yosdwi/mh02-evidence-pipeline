# Excluded inputs

Excluded on purpose: nested `.git`, Python bytecode, `.dll`, `.exe`, device MP4/JPG samples, credentials/config `.conf` and machine-specific runtime data. Alert transport and BEDMS integration were excluded by scope. Source checksums of copied files are in `05-source-checksums.csv`. Original ZIPs were not placed in this repository.

Additional security exclusions: experimental demo `setup-bucket.sh` and its Docker Compose bundle contain embedded credentials/public-bucket policies; deliberately excluded. Hardcoded MinIO credentials in experimental Go config replaced with environment variable reads (recorded in source checksums as sanitized changes). RTSP sample diagnostic script omitted. Do not rely on experiment for production security configuration.
