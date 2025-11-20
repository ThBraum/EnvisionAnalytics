# EnvisionAnalytics (skeleton)

Estrutura inicial do dashboard analítico (esqueleto).

Requisitos: Docker & Docker Compose instalados.

Para rodar:

```bash
# na raiz do projeto (onde está docker-compose.yml)
docker-compose up --build
```

A aplicação web será exposta em `http://localhost:8000` e o Postgres em `localhost:5432`.

Usuário admin inicial: `admin@envision.local` / `P@ssw0rd!` (seeded)

SMTP / Password reset
- The app supports sending password-reset emails using SMTP. **Do NOT check credentials into source control.**
- Provide SMTP settings via environment variables or config files.
- For local development, you can set these variables in a `.env` or `.env.local` file at the project root:

```env
Smtp__Host=smtp.gmail.com
Smtp__Port=587
Smtp__EnableSsl=true
Smtp__User=you@example.com
Smtp__Pass=supersecret
Smtp__From=you@example.com
```

- When running with Docker, you can set these in `docker-compose.override.yml` (keep it local and out of VCS) or export them in your shell before starting.


	Smtp__Host=smtp.gmail.com
	Smtp__Port=587
	Smtp__EnableSsl=true
	Smtp__User=you@example.com
	Smtp__Pass=supersecret
	Smtp__From=you@example.com

- When running with Docker you can set these in `docker-compose.override.yml` (keep it local and out of VCS) or export locally before starting.

Testing real-time events
- A simple helper script is provided at `tools/create_orders.sh`. Provide a `PRODUCT_ID` env var and run:

```bash
export PRODUCT_ID="<GUID_FROM_DB>"
./tools/create_orders.sh 50
```

This will POST sample orders to the running app and you'll see SignalR events in the dashboard.

Pre-commit (rodar automaticamente ao commitar)
- Existe um serviço `precommit` em `docker-compose.yml` que instala `pre-commit` e `dotnet-format`.
- Para rodar os checks localmente via container, instale o hook Git do repositório:

```bash
# a partir da raiz do repo
bash ./scripts/install-git-hooks.sh
# agora os commits executarão o script .githooks/pre-commit
```

O hook invoca `docker compose run --rm precommit pre-commit run --hook-stage commit`.
Se preferir rodar manualmente sem o hook, execute:

```bash
docker compose run --rm precommit pre-commit run --all-files
```
