#!/usr/bin/env bash
set -euo pipefail

# Instala os hooks Git para usar o diretório .githooks neste repositório
# Executar: `bash ./scripts/install-git-hooks.sh`

repo_root=$(cd "$(dirname "$0")/.." && pwd)
cd "$repo_root"

git config core.hooksPath .githooks
chmod +x .githooks/pre-commit || true

echo "Git hooks instalados. core.hooksPath=$(git config core.hooksPath)"
