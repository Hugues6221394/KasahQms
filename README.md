# KASAH QMS — Quality Management System

A comprehensive Quality Management System built with .NET 8.0 Clean Architecture for regulated industries.

## Features

- **Document Management** — Create, review, approve, and archive quality documents with versioning
- **CAPA** — Corrective and Preventive Action tracking with strict lifecycle management
- **Audit Management** — Plan, execute, and track internal/external audits with findings
- **Task Management** — Assign and track work items linked to documents, CAPAs, and audits
- **Stock Management** — Inventory tracking with locations, movements, and reservations
- **Real-time Chat** — Department, cross-department, and direct messaging via SignalR
- **Notifications** — Real-time push notifications and in-app notification center
- **Role-based Access** — Granular permissions with delegation support
- **Multi-tenancy** — Tenant-isolated data with global query filters
- **Audit Trail** — Full audit logging of all user actions

## Tech Stack

- .NET 8.0 / ASP.NET Core (Razor Pages + API)
- PostgreSQL 16 with EF Core 8
- SignalR for real-time features
- Tailwind CSS (CDN)
- JWT + Cookie hybrid authentication
- Docker deployment

## Quick Start (Docker)

```bash
# 1. Clone the repo
git clone <repo-url> && cd src

# 2. Create your environment file
cp .env.example .env
# Edit .env with your actual values (DB password, JWT secret, SMTP settings)

# 3. Generate a secure JWT secret (minimum 32 chars)
openssl rand -base64 48

# 4. Start everything
docker compose up -d

# 5. Access the app
open http://localhost:8080
```

The app will automatically:
- Create the PostgreSQL database
- Run all EF Core migrations
- Seed default tenant, roles, org units, and sample users

## Default Login (after seeding)

| Role | Email | Password |
|------|-------|----------|
| System Admin | sysadmin@kasah.com | P@ssw0rd! |
| TMD | tmd@kasah.com | P@ssw0rd! |

> **Change the default passwords immediately after first login.**

## Development Setup

```bash
# Prerequisites: .NET 8 SDK, PostgreSQL

# 1. Start PostgreSQL and create the database
createdb kasah_qms

# 2. Update connection string in appsettings.Development.json

# 3. Run the app
cd Presentation/KasahQMS.Web
dotnet run

# App runs at http://localhost:5002
```

## Project Structure

```
src/
├── Core/
│   ├── KasahQMS.Domain          # Entities, enums, value objects
│   └── KasahQMS.Application     # Use cases, CQRS handlers, validators
├── Infrastructure/
│   ├── KasahQMS.Infrastructure           # Auth, email, caching, file storage
│   └── KasahQMS.Infrastructure.Persistence  # EF Core, repositories, migrations
├── Presentation/
│   ├── KasahQMS.Web             # Razor Pages, SignalR hubs, controllers
│   └── KasahQMS.Api             # Minimal API endpoints
├── Dockerfile
├── docker-compose.yml
└── KasahQMS.sln
```

## Deployment to VPS

```bash
# 1. SSH into your VPS
ssh user@your-server

# 2. Install Docker
curl -fsSL https://get.docker.com | sh

# 3. Clone repo and configure
git clone <repo-url> && cd src
cp .env.example .env
nano .env  # Set real passwords and secrets

# 4. Deploy
docker compose up -d

# 5. (Optional) Set up reverse proxy with Nginx + SSL
# See docs for Nginx configuration with Let's Encrypt
```

## Health Check

```
GET /health
```

## License

Proprietary — KASAH Technologies Ltd.
