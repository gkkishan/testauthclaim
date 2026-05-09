#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PIDFILE="$ROOT/.local-pids"

if [ ! -f "$PIDFILE" ]; then
    echo "No running apps found (.local-pids not found)."
    exit 0
fi

echo "Stopping apps..."
while read -r pid; do
    if kill -0 "$pid" 2>/dev/null; then
        kill "$pid"
        echo "  Stopped PID $pid"
    fi
done < "$PIDFILE"

rm -f "$PIDFILE"
echo "All apps stopped."
