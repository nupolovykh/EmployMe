#!/usr/bin/env bash
set -euo pipefail

cd /workspace

if [ ! -f .env ] && [ -f .env.example ]; then
    cp .env.example .env
    echo "Created .env from .env.example — fill in the required values."
fi

dotnet tool update --global dotnet-ef

if [ -d src/Web ]; then
    (cd src/Web && npm install)
fi

echo "EmployMe dev container ready."
