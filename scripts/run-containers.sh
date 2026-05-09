#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"

if [ ! -f "$ROOT/.env" ]; then
    echo "ERROR: .env file not found. Run: cp .env.example .env  and fill in your values."
    exit 1
fi

# Auto-detect container engine: podman-compose > docker compose > docker-compose
if command -v podman-compose &>/dev/null; then
    ENGINE="podman-compose"
elif docker compose version &>/dev/null 2>&1; then
    ENGINE="docker compose"
elif command -v docker-compose &>/dev/null; then
    ENGINE="docker-compose"
else
    echo "ERROR: No container engine found."
    echo "Install one of: docker, podman + podman-compose"
    exit 1
fi

echo "Using: $ENGINE"
echo ""
echo "Building and starting containers..."
echo "  OneAccess   → http://localhost:5050"
echo "  SquarUI     → http://localhost:5001"
echo "  LettersAdmin→ http://localhost:5002"
echo ""

cd "$ROOT"
$ENGINE up --build -d

echo ""
echo "All containers running."
echo "Run: scripts/stop-containers.sh  to stop"
echo ""
$ENGINE ps
