#!/usr/bin/env bash
set -euo pipefail

cd /workspace

# The bind-mounted workspace keeps the host's file ownership, which usually
# isn't the container's vscode (uid 1000) — claim it once so vscode can write here.
if [ "$(stat -c %U .)" != "vscode" ]; then
    sudo chown -R vscode:vscode .
fi

# Named volumes mount root-owned on first creation regardless of what the
# image had at that path before — fix both up so vscode (the extension's
# user) can actually write session state and shell history into them.
if [ ! -w /home/vscode/.claude ]; then
    sudo chown -R vscode:vscode /home/vscode/.claude
fi
if [ ! -w /commandhistory ]; then
    sudo chown -R vscode:vscode /commandhistory
fi

if [ ! -f .env ] && [ -f .env.example ]; then
    cp .env.example .env
    echo "Created .env from .env.example — fill in the required values."
fi

dotnet tool update --global dotnet-ef

if [ -d src/Web ]; then
    (cd src/Web && npm install)
fi

echo "EmployMe dev container ready."
