#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PIDFILE="$ROOT/.local-pids"

# Load .env
if [ ! -f "$ROOT/.env" ]; then
    echo "ERROR: .env file not found. Run: cp .env.example .env  and fill in your values."
    exit 1
fi
set -a
source "$ROOT/.env"
set +a

# Export as dotnet config-style env vars
export Okta__Domain="$OKTA_DOMAIN"
export Okta__ClientId="$OKTA_CLIENT_ID"
export Okta__ClientSecret="$OKTA_CLIENT_SECRET"
export Encryption__AesKey="$AES_KEY"
export Encryption__AesIV="$AES_IV"

# Kill any previous run
if [ -f "$PIDFILE" ]; then
    echo "Stopping previous run..."
    while read -r pid; do
        kill "$pid" 2>/dev/null || true
    done < "$PIDFILE"
    rm -f "$PIDFILE"
fi

echo "Building solution..."
dotnet build "$ROOT/solution.sln" -c Debug --nologo -q

echo ""
echo "Starting apps..."
echo "  OneAccess   → http://localhost:5050"
echo "  SquarUI     → http://localhost:5001"
echo "  LettersAdmin→ http://localhost:5002"
echo ""

ASPNETCORE_ENVIRONMENT=Development dotnet run --project "$ROOT/OneAccess/OneAccess.csproj" --no-build --urls "http://localhost:5050" &
echo $! >> "$PIDFILE"

ASPNETCORE_ENVIRONMENT=Development dotnet run --project "$ROOT/SquarUI/SquarUI.csproj" --no-build --urls "http://localhost:5001" &
echo $! >> "$PIDFILE"

ASPNETCORE_ENVIRONMENT=Development dotnet run --project "$ROOT/LettersAdmin/LettersAdmin.csproj" --no-build --urls "http://localhost:5002" &
echo $! >> "$PIDFILE"

echo "All apps started. PIDs saved to .local-pids"
echo "Run: scripts/stop-local.sh  to stop all apps"
echo ""
echo "Press Ctrl+C to stop..."
wait
