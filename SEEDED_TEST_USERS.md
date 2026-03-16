# Seeded Test Users & Credentials

These users are created by `DatabaseSeeder` in:
`Infrastructure/KasahQMS.Infrastructure.Persistence/Data/DatabaseSeeder.cs`

## Default Password (for all seeded users)

- **Password:** `P@ssw0rd!`

## Seeded Accounts

| Role | Full Name | Email | Job Title | Organization Unit |
|---|---|---|---|---|
| System Admin | System Admin | `sysadmin@kasah.com` | Platform Administrator | Executive |
| TMD | Grace Mukamana | `tmd@kasah.com` | Top Managing Director | Executive |
| Deputy Country Manager | Patrick Nshuti | `deputy@kasah.com` | Deputy Country Manager | Operations |
| Department Manager | Aline Uwimana | `legal.manager@kasah.com` | Legal / HR / Regulatory Manager | Legal / HR / Regulatory |
| Department Manager | Eric Habimana | `tech.manager@kasah.com` | Technical & Service Manager | Technical & Service |
| Department Manager | Diane Mukeshimana | `tender.lead@kasah.com` | Key Accounts & Tender Lead | Key Accounts & Tender |
| Department Manager | Samuel Kamanzi | `finance.manager@kasah.com` | Finance, Accounting & Logistics Manager | Finance & Logistics |
| Staff | Claudine Uwase | `staff.legal@kasah.com` | Junior Staff | Legal / HR / Regulatory |
| Staff | Jean Ndayisaba | `staff.tech@kasah.com` | Junior Staff | Technical & Service |
| Auditor | Aisha Kabera | `auditor@kasah.com` | Internal Auditor | Executive |

## Notes

- These accounts are only seeded when the database has **no tenant records**.
- If seeding has already happened before, users may differ if modified manually.
- Change passwords before production use.
