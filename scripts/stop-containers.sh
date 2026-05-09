#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"

# Auto-detect container engine
if command -v podman-compose &>/dev/null; then
    ENGINE="podman-compose"
elif docker compose version &>/dev/null 2>&1; then
    ENGINE="docker compose"
elif command -v docker-compose &>/dev/null; then
    ENGINE="docker-compose"
else
    echo "ERROR: No container engine found."
    exit 1
fi

echo "Using: $ENGINE"
cd "$ROOT"
$ENGINE down
echo "All containers stopped."
