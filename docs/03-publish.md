# Publish private GitHub repo

**Preferred name:** `yosdwi/mh02-evidence-pipeline` (private).

Local Git is already initialized; no remote is connected.

After extracting project and installing/configuring GitHub CLI, run:

```bash
gh auth login
gh repo create yosdwi/mh02-evidence-pipeline --private --source=. --remote=origin --push
```

Alternatively create an empty private repository on GitHub, then:

```bash
git remote add origin git@github.com:yosdwi/mh02-evidence-pipeline.git
git push -u origin main
```

Do not upload original ZIPs or `.env`/`rclone.conf`/live device configs. Secret scanning and rotating previously exposed live credentials are separate required checks.
