# Scope contract

Device → network → on-prem server → SQL Server. Primary metric: incident trigger to evidence committed and accessible; second metric: evidence missing/corrupt rate.

In scope: capture/finalize MP4, packaging replacement, resilient uploads, MinIO ingestion, RabbitMQ processing, SQL SP compatibility, observability.

Out of scope: rewriting alert detection, Node-RED/MariaDB/Socket.IO delivery, BEDMS downstream API push.

All staged changes must retain compatibility with existing incident ID / metadata, with feature flags / rollback where possible.
