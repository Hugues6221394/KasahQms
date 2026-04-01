# KASAH QMS — Enterprise Quality Management Platform

KASAH QMS is a modern Quality Management System product by **CODAFRIQA**, designed for organizations that need strong compliance, operational transparency, and measurable process quality.

It helps businesses standardize quality operations, reduce process failures, improve accountability, and maintain clear communication between teams, managers, auditors, and executives.

---

## 1) What this platform does (non-technical overview)

KASAH QMS centralizes quality operations into one secure platform where your teams can:

- control documents and approvals,
- manage CAPA and audit workflows,
- track tasks and responsibilities,
- monitor training and competency records,
- receive real-time updates on every critical action,
- maintain a complete audit trail for transparency and accountability.

The goal is simple: **fewer surprises, faster decisions, and stronger compliance readiness**.

---

## 2) Core business value for clients

### A. Better compliance and governance
- Formal approval workflows for documents and quality actions.
- Traceable decisions (who approved/rejected, when, and why).
- Audit-ready history of important operations.

### B. End-to-end transparency
- In-app notifications, badge indicators, and email alerts keep stakeholders informed.
- Managers and approvers see pending actions early.
- Teams can track status changes in real time.

### C. Operational efficiency
- Reduced follow-up delays due to clear ownership and reminders.
- Structured workflows for documents, tasks, training, and CAPA.
- Faster turnaround through centralized collaboration.

### D. Security and control
- Role- and permission-based access.
- Tenant-aware data separation for multi-client usage.
- Authentication safeguards including 2FA flow support.

### E. Scalable for multiple industries and client preferences
- Designed for businesses that need customization by department, approval paths, and reporting practices.
- Suitable for local and global operations with configurable workflows.

---

## 3) Full feature overview

## 3.1 Document Management
- Create, submit, review, approve, reject, resubmit, and archive controlled documents.
- Clear visibility of **Draft / Submitted / In Review / Approved / Rejected** states.
- Latest decision display with reason/remarks for transparent communication.
- Resubmissions return to approvers with badges/notifications for continuity.

## 3.2 Approval Workflows
- Central approvals page for pending actions (tasks, documents, and training-related approvals).
- Badge counters indicate outstanding actions.
- Real-time refresh behavior ensures decisions are reflected quickly.

## 3.3 CAPA (Corrective and Preventive Actions)
- Capture issues and non-conformities.
- Assign actions, owners, timelines, and follow-up controls.
- Track CAPA lifecycle from identification to closure.

## 3.4 Audit Management
- Plan and track internal or external audits.
- Record findings and related action items.
- Connect audit outcomes with tasks and CAPA workflows.

## 3.5 Task Management
- Create and assign tasks to responsible users.
- Monitor progress and due status.
- Support approval-oriented task flow for managerial oversight.

## 3.6 Training & Competency Hub
- Schedule, execute, complete, and expire training records.
- Trainer assessments, trainee responses, and role-based visibility controls.
- Rating and feedback flows with proper ownership permissions.
- Training archive lifecycle:
  - archive completed trainings,
  - restore archived trainings,
  - permanently delete archived records (authorized roles).
- Search and filter support for scaling training records.

## 3.7 Communication Layer (Transparency by design)
- Real-time notifications with SignalR.
- In-app badge counters on navigation items.
- Notification center and deep-link routing to affected records.
- Email notifications for key events (e.g., approval decisions and significant workflow updates).

## 3.8 Real-time Chat & Collaboration
- Direct and team communication channels.
- Faster context-sharing for quality actions and approvals.

## 3.9 Role-Based Access & Delegation
- Fine-grained permission model.
- Role hierarchy support (staff, managers, executive-level, admin-level patterns).
- Delegation capabilities for continuity during absences or handovers.

## 3.10 Multi-Tenant Architecture
- Tenant-aware data isolation.
- Built to support multiple business clients on one platform foundation.
- Customizable deployments per client requirements.

## 3.11 Audit Trail & Accountability
- Tracks key user actions and workflow events.
- Supports governance, incident analysis, and compliance evidence.

## 3.12 Dashboard, UX, and Navigation Enhancements
- Status cards, badges, and action summaries for quick understanding.
- Back-navigation support and clear page-level action controls.
- Designed for both daily operators and executive reviewers.

---

## 4) Typical end-to-end workflow (example)

1. A user submits a controlled document.
2. Approver receives in-app notification + badge indication.
3. Approver reviews and either:
   - approves (document progresses), or
   - rejects with reason (owner sees clear reason and can revise).
4. Owner resubmits after corrections.
5. Approver queue and badges update again for the new cycle.
6. All key actions remain visible through notifications/history for transparency.

The same visibility principle applies across training, tasks, and related quality processes.

---

## 5) Security and privacy posture

- Authentication with secure session handling and 2FA flow.
- Role/permission checks across features and pages.
- Rate-limiting and secure middleware protections.
- Tenant-level data segregation for multi-client safety.
- Email and notification-based transparency without exposing unauthorized data.

> For production use, ensure strong secrets, secure SMTP settings, HTTPS, and hardened infrastructure policies.

---

## 6) Customization model for client deployments

KASAH QMS is built to be tailored for each business client, including:

- approval path structure,
- role and permission strategy,
- organization hierarchy,
- training and assessment expectations,
- notification preferences,
- branding and UI preferences,
- compliance reporting requirements.

This allows CODAFRIQA and Kasah Technologies to deliver a reusable core platform while adapting workflows per client.

---

## 7) Technology stack (for technical stakeholders)

- .NET 8.0 / ASP.NET Core (Razor Pages + API)
- PostgreSQL 16 with EF Core 8
- SignalR for real-time features
- Tailwind CSS
- JWT + Cookie hybrid authentication
- Docker-ready deployment model

---

## 8) Quick start (Docker)

```bash
# 1. Clone the repo
git clone <repo-url> && cd src

# 2. Create environment file
cp .env.example .env
# Edit .env with actual values (DB password, JWT secret, SMTP settings)

# 3. Generate strong JWT secret (minimum 32 chars)
openssl rand -base64 48

# 4. Start services
docker compose up -d

# 5. Open application
open http://localhost:8080
```

The app automatically:
- creates PostgreSQL database,
- runs EF Core migrations,
- seeds default tenant/roles/org units/sample users.

### Default seeded login

| Role | Email | Password |
|------|-------|----------|
| System Admin | sysadmin@kasah.com | P@ssw0rd! |
| TMD | tmd@kasah.com | P@ssw0rd! |

> Change seeded passwords immediately after first login.

---

## 9) Development setup

```bash
# Prerequisites: .NET 8 SDK, PostgreSQL

# 1. Create local database
createdb kasah_qms

# 2. Configure appsettings.Development.json
#    (connection string + environment secrets)

# 3. Run web app
cd Presentation/KasahQMS.Web
dotnet run
```

Application URL (default): `http://localhost:5002`

---

## 10) Project structure

```
src/
├── Core/
│   ├── KasahQMS.Domain                     # Domain entities, enums, business rules
│   └── KasahQMS.Application                # Use cases, CQRS handlers, validation
├── Infrastructure/
│   ├── KasahQMS.Infrastructure             # Auth, email, caching, storage services
│   └── KasahQMS.Infrastructure.Persistence # EF Core, repositories, migrations
├── Presentation/
│   ├── KasahQMS.Web                        # Razor Pages UI, hubs, controllers
│   └── KasahQMS.Api                        # API endpoints
├── Dockerfile
├── docker-compose.yml
└── KasahQMS.sln
```

---

## 11) Deployment summary (VPS example)

```bash
# 1. SSH to server
ssh user@your-server

# 2. Install Docker
curl -fsSL https://get.docker.com | sh

# 3. Clone and configure
git clone <repo-url> && cd src
cp .env.example .env
nano .env

# 4. Launch
docker compose up -d
```

Optional hardening:
- reverse proxy with Nginx,
- TLS/SSL with Let's Encrypt,
- centralized logs and monitoring.

Health endpoint:

```http
GET /health
```

---

## 12) Ownership and licensing

**Product ownership:** CODAFRIQA and Kasah Technologies  
**License:** Proprietary software (all rights reserved by owners and authorized business terms)
