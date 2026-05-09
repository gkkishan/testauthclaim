#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"

echo "=== OneAccess Dev Setup ==="
echo ""

# Check .NET SDK
if ! command -v dotnet &>/dev/null; then
    echo "ERROR: .NET SDK not found."
    echo "Install .NET 10: https://dotnet.microsoft.com/download/dotnet/10.0"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo "  .NET SDK: $DOTNET_VERSION"

# Check container engine (optional)
if command -v podman &>/dev/null; then
    echo "  Container: podman $(podman --version | awk '{print $3}')"
    if ! command -v podman-compose &>/dev/null; then
        echo "  WARNING: podman-compose not found. Install: pip install podman-compose"
    fi
elif command -v docker &>/dev/null; then
    echo "  Container: $(docker --version)"
else
    echo "  Container: none found (optional — you can run locally with dotnet)"
fi

echo ""

# Create .env if missing
if [ ! -f "$ROOT/.env" ]; then
    cp "$ROOT/.env.example" "$ROOT/.env"
    echo "  Created .env from .env.example"
    echo "  >>> EDIT .env with your Okta Client ID and Client Secret <<<"
else
    echo "  .env already exists"
fi

echo ""

# Restore packages
echo "Restoring NuGet packages..."
dotnet restore "$ROOT/solution.sln" --nologo -q

echo ""
echo "Building solution..."
dotnet build "$ROOT/solution.sln" -c Debug --nologo -q

echo ""
echo "=== Setup complete ==="
echo ""
echo "Next steps:"
echo "  1. Edit .env with your Okta credentials"
echo "  2. Run locally:      ./scripts/run-local.sh"
echo "  3. Run in containers: ./scripts/run-containers.sh"
echo ""
echo "Ports:"
echo "  OneAccess    → http://localhost:5050"
echo "  SquarUI      → http://localhost:5001"
echo "  LettersAdmin → http://localhost:5002"
