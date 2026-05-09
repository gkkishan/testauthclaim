# OneAccess SSO Gateway

Centralized SSO gateway that authenticates users via Okta and distributes AES-encrypted JWT tokens to downstream apps.

```
SquarUI (:5001) ──────┐
                      ├──→ OneAccess Gateway (:5050) ──→ Okta IDP
LettersAdmin (:5002) ─┘
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Docker or Podman (only if running in containers)

## Quick Start

### 1. Clone and setup

```bash
git clone https://github.com/gkkishan/testauthclaim.git
cd testauthclaim
./scripts/setup.sh
```

### 2. Configure credentials

Edit the `.env` file (created by setup.sh) with your Okta credentials:

```
OKTA_DOMAIN=https://integrator-5446987.okta.com
OKTA_CLIENT_ID=<your-client-id>
OKTA_CLIENT_SECRET=<your-client-secret>
AES_KEY=4Y3RM6H9
AES_IV=1S6JKH24
```

### 3. Run

**Option A — Run locally (recommended for development):**

```bash
./scripts/run-local.sh      # starts all 3 apps
./scripts/stop-local.sh     # stops all 3 apps (or just Ctrl+C)
```

**Option B — Run in containers (Docker or Podman):**

```bash
./scripts/run-containers.sh     # auto-detects docker/podman
./scripts/stop-containers.sh    # stops and removes containers
```

### 4. Open in browser

| App | URL | Required Okta Group |
|-----|-----|---------------------|
| SquarUI | http://localhost:5001 | `squar-group` |
| LettersAdmin | http://localhost:5002 | `lettersadmin-group` |
| OneAccess Gateway | http://localhost:5050 | — (landing page) |

Start from SquarUI or LettersAdmin. You'll be redirected through OneAccess → Okta → back to the app automatically.

## Okta Setup (one-time)

1. **Create OIDC app** — Applications → Create App Integration → OIDC → Web Application
   - Sign-in redirect URI: `http://localhost:5050/auth/callback/okta`
   - Copy Client ID and Client Secret into `.env`

2. **Add groups claim** — Security → API → Authorization Servers → default → Claims → Add Claim
   - Name: `groups`, Include in: ID Token (Always), Value type: Groups, Filter: regex `.*`

3. **Create groups** — Directory → Groups → create `squar-group` and `lettersadmin-group`

4. **Assign users** — Add test users to groups, then assign them to the OneAccess app

## Scripts Reference

| Script | What it does |
|--------|-------------|
| `scripts/setup.sh` | Checks .NET SDK, creates `.env`, restores packages, builds solution |
| `scripts/run-local.sh` | Builds and runs all 3 apps with `dotnet run`. Ctrl+C stops all. |
| `scripts/stop-local.sh` | Stops apps started by `run-local.sh` |
| `scripts/run-containers.sh` | Auto-detects Docker/Podman, builds images, starts containers |
| `scripts/stop-containers.sh` | Stops and removes containers |

## How It Works

1. User visits SquarUI (`:5001`) or LettersAdmin (`:5002`)
2. Middleware detects no auth cookie → redirects to OneAccess `/auth/login?app=squar`
3. OneAccess generates PKCE challenge → redirects to Okta `/authorize`
4. User logs in at Okta → Okta redirects back to OneAccess `/auth/callback/okta`
5. OneAccess exchanges code for tokens, checks user's groups against app's `AllowedGroups`
6. If authorized: wraps Okta JWT in AES-encrypted envelope → redirects to app with `?squarToken={encrypted}`
7. App decrypts token, validates JWT against Okta JWKS, creates auth cookie
8. User sees the welcome page

See [docs/architecture.mdx](docs/architecture.mdx) for detailed sequence diagrams and security analysis.

## Project Structure

```
├── Shared/              # AES encryption + token model (shared library)
├── OneAccess/           # SSO Gateway (:5050)
├── SquarUI/             # Downstream app (:5001)
├── LettersAdmin/        # Downstream app (:5002)
├── scripts/             # Run/stop/setup scripts
├── docs/                # Architecture docs with sequence diagrams
├── docker-compose.yml   # Container orchestration
├── .env.example         # Credential template
└── solution.sln         # .NET solution file
```
