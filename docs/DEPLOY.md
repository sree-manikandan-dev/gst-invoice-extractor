# Deploying to one cheap server

The app runs as three containers (Postgres, API, web) on a single machine with Docker Compose.
An AWS EC2 `t3.micro`/`t4g.small` or a Lightsail instance is enough. Costs vary; set an
[AWS budget alert](https://docs.aws.amazon.com/cost-management/latest/userguide/budgets-create.html)
before you launch anything, and stop the instance when you are not demoing.

## 1. Create the server (one time)

1. Launch an Ubuntu 24.04 instance. Open inbound ports 22 (your IP only) and 80.
2. SSH in and install Docker:
   ```bash
   curl -fsSL https://get.docker.com | sh
   sudo usermod -aG docker $USER   # log out and back in after this
   mkdir -p ~/app
   ```
3. Create `~/app/.env`:
   ```bash
   API_IMAGE=ghcr.io/<your-github-username>/gst-invoice-extractor/api:latest
   WEB_IMAGE=ghcr.io/<your-github-username>/gst-invoice-extractor/web:latest
   EXTRACTION_PROVIDER=Claude
   ANTHROPIC_API_KEY=sk-ant-...
   POSTGRES_PASSWORD=<a long random password>
   WEB_PORT=80
   ```
4. Images on GitHub Container Registry are private by default. Either make both packages public
   (GitHub > your profile > Packages > package settings), or run
   `docker login ghcr.io -u <username>` on the server with a token that has `read:packages`.

## 2. Let GitHub Actions deploy

Add these repository secrets (Settings > Secrets and variables > Actions):

| Secret | Value |
| --- | --- |
| `DEPLOY_HOST` | The server's public IP or DNS name |
| `DEPLOY_USER` | `ubuntu` (or your SSH user) |
| `DEPLOY_SSH_KEY` | The private key that can SSH into the server |
| `ANTHROPIC_API_KEY` | Only needed for the manual eval workflow |

Merging to `main` builds and pushes images (CI workflow). Then run **Actions > Deploy > Run workflow**.
It copies `docker-compose.yml`, pulls the new images and restarts the containers.

## 3. Before you share the link

- There is no login. Anyone with the URL can upload and see invoices, so only upload sample
  or masked bills, and say "demo" in the README.
- Add HTTPS if you keep it running: put Caddy in front, or use a Lightsail load balancer.
