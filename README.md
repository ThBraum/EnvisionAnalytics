# EnvisionAnalytics

This repository contains a lightweight analytics dashboard skeleton built with ASP.NET.

Requirements
- Docker and Docker Compose installed.

Running the app

```bash
# from the project root (where `docker-compose.yml` lives)
docker compose up --build
```

The web application will be available at `http://localhost:8000` and PostgreSQL will be reachable on `localhost:5432`.

Initial admin user (seeded)
- Username: `admin`
- Email: `admin@envision.local`
- Password: `P@ssw0rd!`

SMTP (sending password reset emails)
- The application can send password-reset and notification emails using SMTP. Do NOT commit credentials to source control.
- A step-by-step guide for configuring SMTP (including recommended providers and app-password instructions) is included in the file `.env.example` at the project root — please follow that guide.
- For local testing you can set SMTP variables in a `.env` file or export them in your shell. Example variables the app reads:

```env
Smtp__Host=smtp.gmail.com
Smtp__Port=587
Smtp__EnableSsl=true
Smtp__User=you@example.com
Smtp__Pass=your-app-password
Smtp__From=you@example.com
Smtp__DisplayName=Envision Analytics Mailer
```

- When running with Docker, prefer to put sensitive overrides in `docker-compose.override.yml` or a local `.env` file (do not commit these files).

Real-time events (SignalR)
- A helper script is available at `tools/create_orders.sh` to create sample orders and generate events. Provide a `PRODUCT_ID` environment variable and run:

```bash
export PRODUCT_ID="<GUID_FROM_DB>"
./tools/create_orders.sh 50
```

Pre-commit checks
- The repository includes a `precommit` service in `docker-compose.yml` that runs `pre-commit` and `dotnet-format`.
- To enable local git hooks run:

```bash
# from the project root
bash ./scripts/install-git-hooks.sh
```

- To run the checks manually without the git hook:

```bash
pre-commit run --all-files
```

Notes
- This project uses development-friendly defaults; the seeded credentials and DB passwords are for development only. Do not use them in production.
- For production email, consider using a dedicated provider (SendGrid, Mailgun, Amazon SES) and a secrets manager for credentials.

