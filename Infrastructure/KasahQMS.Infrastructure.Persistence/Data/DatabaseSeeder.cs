using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Entities.AuditLog;
using KasahQMS.Domain.Entities.Audits;
using KasahQMS.Domain.Entities.Capa;
using KasahQMS.Domain.Entities.Configuration;
using KasahQMS.Domain.Entities.Documents;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Domain.Entities.Tasks;
using KasahQMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Infrastructure.Persistence.Data;

public class DatabaseSeeder
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        ApplicationDbContext dbContext,
        IPasswordHasher passwordHasher,
        ILogger<DatabaseSeeder> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        if (await _dbContext.Tenants.AnyAsync())
        {
            await PatchManagerRolePermissionsAsync();
            return;
        }

        _logger.LogInformation("Seeding KasahQMS database with sample data.");

        var tenant = Tenant.Create("Kasah Rwanda", "RW", "Primary tenant for Kasah QMS");

        var orgExecutive = OrganizationUnit.Create(tenant.Id, "Executive", "EXEC", "Country leadership unit");
        var orgOperations = OrganizationUnit.Create(tenant.Id, "Operations", "OPS", "Operations and delivery");
        var orgLegal = OrganizationUnit.Create(tenant.Id, "Legal / HR / Regulatory", "LEGAL", "Legal and regulatory compliance");
        var orgTechnical = OrganizationUnit.Create(tenant.Id, "Technical & Service", "TECH", "Technical operations");
        var orgTender = OrganizationUnit.Create(tenant.Id, "Key Accounts & Tender", "TENDER", "Key accounts and tender management");
        var orgFinance = OrganizationUnit.Create(tenant.Id, "Finance & Logistics", "FIN", "Finance and logistics");

        var seedPassword = "P@ssw0rd!";
        var hashedPassword = _passwordHasher.Hash(seedPassword);

        var systemAdminRole = Role.Create(tenant.Id, "System Admin", "Platform administration", AllPermissions(), Guid.Empty);
        // TMD has ALL permissions - they are the boss, no approval needed
        var tmdRole = Role.Create(tenant.Id, "TMD", "Top Managing Director", new[]
        {
            Permission.DocumentRead, Permission.DocumentCreate, Permission.DocumentEdit, Permission.DocumentDelete,
            Permission.DocumentApprove, Permission.DocumentArchive,
            Permission.AuditRead, Permission.AuditCreate, Permission.AuditEdit, Permission.AuditDelete,
            Permission.CapaRead, Permission.CapaCreate, Permission.CapaEdit, Permission.CapaDelete, Permission.CapaVerify,
            Permission.TaskRead, Permission.TaskCreate, Permission.TaskAssign, Permission.TaskEdit, Permission.TaskDelete,
            Permission.UserRead, Permission.UserCreate, Permission.UserEdit,
            Permission.ViewAuditLogs, Permission.ManageRoles
        }, Guid.Empty);
        // Deputy has most permissions but may need TMD approval for some actions
        var deputyRole = Role.Create(tenant.Id, "Deputy Country Manager", "Operations leadership", new[]
        {
            Permission.DocumentRead, Permission.DocumentCreate, Permission.DocumentEdit, Permission.DocumentApprove, Permission.DocumentArchive,
            Permission.AuditRead, Permission.AuditCreate, Permission.AuditEdit,
            Permission.CapaRead, Permission.CapaCreate, Permission.CapaEdit, Permission.CapaVerify,
            Permission.TaskRead, Permission.TaskCreate, Permission.TaskAssign, Permission.TaskEdit, Permission.TaskDelete,
            Permission.UserRead, Permission.ViewAuditLogs
        }, Guid.Empty);
        // Department managers can create documents and CAPAs within their department
        var departmentManagerRole = Role.Create(tenant.Id, "Department Manager", "Departmental oversight", new[]
        {
            Permission.DocumentRead, Permission.DocumentCreate, Permission.DocumentEdit, Permission.DocumentApprove,
            Permission.TaskRead, Permission.TaskCreate, Permission.TaskAssign, Permission.TaskEdit, Permission.TaskDelete,
            Permission.CapaRead, Permission.CapaCreate, Permission.CapaEdit,
            Permission.AuditRead
        }, Guid.Empty);
        var auditorRole = Role.Create(tenant.Id, "Auditor", "Read-only audit access", new[]
        {
            Permission.DocumentRead, Permission.AuditRead, Permission.ViewAuditLogs
        }, Guid.Empty);
        var staffRole = Role.Create(tenant.Id, "Staff", "Operational contributor", new[]
        {
            Permission.DocumentRead, Permission.DocumentCreate, Permission.TaskRead, Permission.TaskCreate
        }, Guid.Empty);

        var systemAdmin = User.Create(tenant.Id, "sysadmin@kasah.com", "System", "Admin", hashedPassword, Guid.Empty);
        systemAdmin.JobTitle = "Platform Administrator";
        systemAdmin.RequirePasswordChange = false;
        systemAdmin.AssignToOrganizationUnit(orgExecutive.Id);

        var tmd = User.Create(tenant.Id, "tmd@kasah.com", "Grace", "Mukamana", hashedPassword, Guid.Empty);
        tmd.JobTitle = "Top Managing Director";
        tmd.RequirePasswordChange = false;
        tmd.AssignToOrganizationUnit(orgExecutive.Id);

        var deputy = User.Create(tenant.Id, "deputy@kasah.com", "Patrick", "Nshuti", hashedPassword, Guid.Empty);
        deputy.JobTitle = "Deputy Country Manager";
        deputy.RequirePasswordChange = false;
        deputy.AssignToOrganizationUnit(orgOperations.Id);
        deputy.SetManager(tmd.Id);

        var legalManager = User.Create(tenant.Id, "legal.manager@kasah.com", "Aline", "Uwimana", hashedPassword, Guid.Empty);
        legalManager.JobTitle = "Legal / HR / Regulatory Manager";
        legalManager.RequirePasswordChange = false;
        legalManager.AssignToOrganizationUnit(orgLegal.Id);
        legalManager.SetManager(deputy.Id);

        var techManager = User.Create(tenant.Id, "tech.manager@kasah.com", "Eric", "Habimana", hashedPassword, Guid.Empty);
        techManager.JobTitle = "Technical & Service Manager";
        techManager.RequirePasswordChange = false;
        techManager.AssignToOrganizationUnit(orgTechnical.Id);
        techManager.SetManager(deputy.Id);

        var tenderLead = User.Create(tenant.Id, "tender.lead@kasah.com", "Diane", "Mukeshimana", hashedPassword, Guid.Empty);
        tenderLead.JobTitle = "Key Accounts & Tender Lead";
        tenderLead.RequirePasswordChange = false;
        tenderLead.AssignToOrganizationUnit(orgTender.Id);
        tenderLead.SetManager(deputy.Id);

        var financeManager = User.Create(tenant.Id, "finance.manager@kasah.com", "Samuel", "Kamanzi", hashedPassword, Guid.Empty);
        financeManager.JobTitle = "Finance, Accounting & Logistics Manager";
        financeManager.RequirePasswordChange = false;
        financeManager.AssignToOrganizationUnit(orgFinance.Id);
        financeManager.SetManager(deputy.Id);

        var staffLegal = User.Create(tenant.Id, "staff.legal@kasah.com", "Claudine", "Uwase", hashedPassword, Guid.Empty);
        staffLegal.JobTitle = "Junior Staff";
        staffLegal.RequirePasswordChange = false;
        staffLegal.AssignToOrganizationUnit(orgLegal.Id);
        staffLegal.SetManager(legalManager.Id);

        var staffTech = User.Create(tenant.Id, "staff.tech@kasah.com", "Jean", "Ndayisaba", hashedPassword, Guid.Empty);
        staffTech.JobTitle = "Junior Staff";
        staffTech.RequirePasswordChange = false;
        staffTech.AssignToOrganizationUnit(orgTechnical.Id);
        staffTech.SetManager(techManager.Id);

        var auditor = User.Create(tenant.Id, "auditor@kasah.com", "Aisha", "Kabera", hashedPassword, Guid.Empty);
        auditor.JobTitle = "Internal Auditor";
        auditor.RequirePasswordChange = false;
        auditor.AssignToOrganizationUnit(orgExecutive.Id);
        auditor.SetManager(tmd.Id);

        systemAdmin.Roles = new List<Role> { systemAdminRole };
        tmd.Roles = new List<Role> { tmdRole };
        deputy.Roles = new List<Role> { deputyRole };
        legalManager.Roles = new List<Role> { departmentManagerRole };
        techManager.Roles = new List<Role> { departmentManagerRole };
        tenderLead.Roles = new List<Role> { departmentManagerRole };
        financeManager.Roles = new List<Role> { departmentManagerRole };
        staffLegal.Roles = new List<Role> { staffRole };
        staffTech.Roles = new List<Role> { staffRole };
        auditor.Roles = new List<Role> { auditorRole };

        var documentTypePolicy = new DocumentType
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = "Policy",
            Description = "Company policy documents"
        };
        var documentTypeProcedure = new DocumentType
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = "Procedure",
            Description = "Operating procedures"
        };
        var documentCategoryQuality = new DocumentCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = "Quality Management",
            Description = "QMS documentation"
        };
        var documentCategoryTender = new DocumentCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = "Tender Management",
            Description = "Tender documentation"
        };

        // Configure workflow for Procedure document type (Tender Requisitions)
        // Order: Finance Manager -> Deputy -> TMD
        var procedureApprovers = new List<DocumentTypeApprover>
        {
            new DocumentTypeApprover
            {
                DocumentTypeId = documentTypeProcedure.Id,
                ApproverId = financeManager.Id,
                ApprovalOrder = 1,
                IsRequired = true
            },
            new DocumentTypeApprover
            {
                DocumentTypeId = documentTypeProcedure.Id,
                ApproverId = deputy.Id,
                ApprovalOrder = 2,
                IsRequired = true
            },
            new DocumentTypeApprover
            {
                DocumentTypeId = documentTypeProcedure.Id,
                ApproverId = tmd.Id,
                ApprovalOrder = 3,
                IsRequired = true
            }
        };

        var docTender = Document.Create(tenant.Id, "Tender Requisition", "DOC-2026-001", tenderLead.Id,
            "Tender requisition template", documentTypeProcedure.Id, documentCategoryTender.Id);
        docTender.Status = DocumentStatus.Submitted;
        docTender.CurrentApproverId = financeManager.Id;

        var docQualityManual = Document.Create(tenant.Id, "Quality Manual", "DOC-2026-002", tmd.Id,
            "Master quality manual", documentTypePolicy.Id, documentCategoryQuality.Id);
        docQualityManual.Status = DocumentStatus.Approved;
        docQualityManual.ApprovedById = tmd.Id;
        docQualityManual.ApprovedAt = DateTime.UtcNow.AddDays(-5);

        var taskReviewTender = QmsTask.Create(tenant.Id, "Review tender requisition", "TASK-2026-001", financeManager.Id,
            "Review budget and compliance for tender requisition", TaskPriority.High, DateTime.UtcNow.AddDays(3));
        taskReviewTender.Assign(financeManager.Id);
        taskReviewTender.LinkToDocument(docTender.Id);

        var taskVerifyCapa = QmsTask.Create(tenant.Id, "Verify CAPA effectiveness", "TASK-2026-002", deputy.Id,
            "Verify CAPA-2026-004 effectiveness", TaskPriority.Medium, DateTime.UtcNow.AddDays(7));
        taskVerifyCapa.Assign(deputy.Id);

        var audit = Audit.Create(tenant.Id, "ISO 9001 Internal Audit", "AUD-2026-001", AuditType.Internal,
            DateTime.UtcNow.AddDays(7), DateTime.UtcNow.AddDays(10), auditor.Id);
        audit.SetLeadAuditor(auditor.Id);
        audit.SetScope("Enterprise quality management process");

        var capa = Capa.Create(tenant.Id, "CAPA on supplier compliance", "CAPA-2026-004",
            CapaType.Corrective, CapaPriority.High, deputy.Id);
        capa.AssignOwner(techManager.Id);
        capa.SetTargetCompletionDate(DateTime.UtcNow.AddDays(21));

        var policies = new List<AccessPolicy>
        {
            AccessPolicy.Create(tenant.Id, "Hierarchy visibility", "Hierarchy", "User.ManagerId", "InHierarchy", "SelfAndSubordinates", tmd.Id, tmdRole.Id,
                "TMD can view all subordinate activity"),
            AccessPolicy.Create(tenant.Id, "Department scope", "OrganizationUnit", "User.OrganizationUnitId", "Equals", "AssignedUnit", deputy.Id, deputyRole.Id,
                "Deputy can view operations unit data"),
            AccessPolicy.Create(tenant.Id, "Manager approval", "Workflow", "Document.Status", "In", "Submitted,InReview", deputy.Id, departmentManagerRole.Id,
                "Managers can approve within their scope"),
            AccessPolicy.Create(tenant.Id, "Auditor read-only", "Security", "Permission", "Equals", "ReadOnly", auditor.Id, auditorRole.Id,
                "Auditors operate in read-only mode")
        };

        var settings = new List<SystemSetting>
        {
            SystemSetting.Create(tenant.Id, "Security.RequireMfa", "true", systemAdmin.Id, "Require MFA for privileged users"),
            SystemSetting.Create(tenant.Id, "Security.StrongPasswords", "true", systemAdmin.Id, "Enforce strong password policy"),
            SystemSetting.Create(tenant.Id, "Security.LockoutThreshold", "5", systemAdmin.Id, "Failed login threshold"),
            SystemSetting.Create(tenant.Id, "Notifications.CapaEscalationDays", "7", systemAdmin.Id, "CAPA escalation threshold"),
            SystemSetting.Create(tenant.Id, "Notifications.AuditReminderCadence", "Weekly", systemAdmin.Id, "Audit reminder cadence"),
            SystemSetting.Create(tenant.Id, "Retention.AuditLogYears", "7", systemAdmin.Id, "Audit log retention"),
            SystemSetting.Create(tenant.Id, "Retention.DocumentArchiveMonths", "24", systemAdmin.Id, "Document archive threshold"),
            SystemSetting.Create(tenant.Id, "Backups.Frequency", "Daily", systemAdmin.Id, "Backup cadence"),
            SystemSetting.Create(tenant.Id, "Seed.PasswordHint", seedPassword, systemAdmin.Id, "Seeded demo password")
        };

        var auditLogs = new List<AuditLogEntry>
        {
            AuditLogEntry.Create("DOCUMENT_SUBMITTED", "Document", docTender.Id, "Tender requisition submitted", tenderLead.Id, tenant.Id),
            AuditLogEntry.Create("DOCUMENT_APPROVED", "Document", docQualityManual.Id, "Quality manual approved", tmd.Id, tenant.Id),
            AuditLogEntry.Create("TASK_ASSIGNED", "Task", taskReviewTender.Id, "Tender review assigned", financeManager.Id, tenant.Id),
            AuditLogEntry.CreateAuthenticationLog(systemAdmin.Id, "LOGIN_SUCCESS", "System admin login")
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.OrganizationUnits.AddRange(orgExecutive, orgOperations, orgLegal, orgTechnical, orgTender, orgFinance);
        _dbContext.Roles.AddRange(systemAdminRole, tmdRole, deputyRole, departmentManagerRole, auditorRole, staffRole);
        _dbContext.Users.AddRange(systemAdmin, tmd, deputy, legalManager, techManager, tenderLead, financeManager, staffLegal, staffTech, auditor);
        _dbContext.DocumentTypes.AddRange(documentTypePolicy, documentTypeProcedure);
        _dbContext.DocumentCategories.AddRange(documentCategoryQuality, documentCategoryTender);
        _dbContext.Set<DocumentTypeApprover>().AddRange(procedureApprovers);
        _dbContext.Documents.AddRange(docTender, docQualityManual);
        _dbContext.QmsTasks.AddRange(taskReviewTender, taskVerifyCapa);
        _dbContext.Audits.Add(audit);
        _dbContext.Capas.Add(capa);
        _dbContext.AccessPolicies.AddRange(policies);
        _dbContext.SystemSettings.AddRange(settings);
        _dbContext.AuditLogEntries.AddRange(auditLogs);

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Database seeding completed.");
    }

    /// <summary>
    /// Patches TMD, Deputy, Department Manager roles to ensure they have all required permissions.
    /// TMD gets ALL permissions (they are the boss).
    /// </summary>
    private async Task PatchManagerRolePermissionsAsync()
    {
        var roles = await _dbContext.Roles
            .Where(r => r.Name == "TMD" || r.Name == "Deputy Country Manager" || r.Name == "Department Manager")
            .ToListAsync();
        var modified = false;
        foreach (var r in roles)
        {
            Permission[] newPerms;
            if (r.Name == "TMD")
            {
                // TMD has ALL permissions - they are the boss, no approval needed
                newPerms = new[]
                {
                    Permission.DocumentRead, Permission.DocumentCreate, Permission.DocumentEdit, Permission.DocumentDelete,
                    Permission.DocumentApprove, Permission.DocumentArchive,
                    Permission.AuditRead, Permission.AuditCreate, Permission.AuditEdit, Permission.AuditDelete,
                    Permission.CapaRead, Permission.CapaCreate, Permission.CapaEdit, Permission.CapaDelete, Permission.CapaVerify,
                    Permission.TaskRead, Permission.TaskCreate, Permission.TaskAssign, Permission.TaskEdit, Permission.TaskDelete,
                    Permission.UserRead, Permission.UserCreate, Permission.UserEdit,
                    Permission.ViewAuditLogs, Permission.ManageRoles
                };
            }
            else if (r.Name == "Deputy Country Manager")
            {
                newPerms = new[]
                {
                    Permission.DocumentRead, Permission.DocumentCreate, Permission.DocumentEdit, Permission.DocumentApprove, Permission.DocumentArchive,
                    Permission.AuditRead, Permission.AuditCreate, Permission.AuditEdit,
                    Permission.CapaRead, Permission.CapaCreate, Permission.CapaEdit, Permission.CapaVerify,
                    Permission.TaskRead, Permission.TaskCreate, Permission.TaskAssign, Permission.TaskEdit, Permission.TaskDelete,
                    Permission.UserRead, Permission.ViewAuditLogs
                };
            }
            else if (r.Name == "Department Manager")
            {
                newPerms = new[]
                {
                    Permission.DocumentRead, Permission.DocumentCreate, Permission.DocumentEdit, Permission.DocumentApprove,
                    Permission.TaskRead, Permission.TaskCreate, Permission.TaskAssign, Permission.TaskEdit, Permission.TaskDelete,
                    Permission.CapaRead, Permission.CapaCreate, Permission.CapaEdit,
                    Permission.AuditRead
                };
            }
            else
                continue;

            // Check if permissions need updating
            var currentPerms = r.Permissions?.ToHashSet() ?? new HashSet<Permission>();
            var needsUpdate = newPerms.Any(p => !currentPerms.Contains(p));
            if (!needsUpdate) continue;

            r.Permissions = newPerms;
            modified = true;
            _logger.LogInformation("Patched role {RoleName} with full CRUD permissions.", r.Name);
        }

        if (modified)
            await _dbContext.SaveChangesAsync();
    }

    private static Permission[] AllPermissions()
    {
        return Enum.GetValues<Permission>();
    }
}

