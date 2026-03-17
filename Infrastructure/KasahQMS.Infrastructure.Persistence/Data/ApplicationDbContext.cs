#pragma warning disable CS8602 // EF Core navigation properties in query filters are translated to SQL JOINs
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.AuditLog;
using KasahQMS.Domain.Entities.Audits;
using KasahQMS.Domain.Entities.Capa;
using KasahQMS.Domain.Entities.Chat;
using KasahQMS.Domain.Entities.Configuration;
using KasahQMS.Domain.Entities.Documents;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Domain.Entities.Notifications;
using KasahQMS.Domain.Entities.Privacy;
using KasahQMS.Domain.Entities.Risk;
using KasahQMS.Domain.Entities.Security;
using KasahQMS.Domain.Entities.Stock;
using KasahQMS.Domain.Entities.Supplier;
using KasahQMS.Domain.Entities.Tasks;
using KasahQMS.Domain.Entities.Training;
using KasahQMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Linq.Expressions;

namespace KasahQMS.Infrastructure.Persistence.Data;

/// <summary>
/// Application database context with tenant isolation via global query filters.
/// </summary>
public class ApplicationDbContext : DbContext
{
    private readonly ICurrentUserService? _currentUserService;
    private Guid? _tenantId;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService currentUserService) : base(options)
    {
        _currentUserService = currentUserService;
        _tenantId = currentUserService.TenantId;
    }

    /// <summary>
    /// Set the tenant filter explicitly (for seeding or background jobs).
    /// </summary>
    public void SetTenantId(Guid tenantId) => _tenantId = tenantId;

    /// <summary>
    /// Disable tenant filter (for cross-tenant admin operations).
    /// </summary>
    public void DisableTenantFilter() => _tenantId = null;

    private Guid? CurrentTenantId => _tenantId ?? _currentUserService?.TenantId;

    // Identity
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<OrganizationUnit> OrganizationUnits => Set<OrganizationUnit>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<AccessPolicy> AccessPolicies => Set<AccessPolicy>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    // Documents
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<DocumentApproval> DocumentApprovals => Set<DocumentApproval>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<DocumentCategory> DocumentCategories => Set<DocumentCategory>();
    public DbSet<DocumentTypeApprover> DocumentTypeApprovers => Set<DocumentTypeApprover>();
    public DbSet<DocumentAccessLog> DocumentAccessLogs => Set<DocumentAccessLog>();
    public DbSet<DocumentAttachment> DocumentAttachments => Set<DocumentAttachment>();

    // Audits
    public DbSet<Audit> Audits => Set<Audit>();
    public DbSet<AuditFinding> AuditFindings => Set<AuditFinding>();
    public DbSet<AuditTeamMember> AuditTeamMembers => Set<AuditTeamMember>();
    public DbSet<AuditChecklistItem> AuditChecklistItems => Set<AuditChecklistItem>();
    public DbSet<AuditEvidence> AuditEvidence => Set<AuditEvidence>();
    public DbSet<AuditFindingResponse> AuditFindingResponses => Set<AuditFindingResponse>();

    // CAPA
    public DbSet<Capa> Capas => Set<Capa>();
    public DbSet<CapaAction> CapaActions => Set<CapaAction>();

    // Tasks
    public DbSet<QmsTask> QmsTasks => Set<QmsTask>();
    public DbSet<TaskAttachment> TaskAttachments => Set<TaskAttachment>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<TaskActivity> TaskActivities => Set<TaskActivity>();

    // Notifications
    public DbSet<Notification> Notifications => Set<Notification>();

    // Audit Logs
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<UserLoginActivity> UserLoginActivities => Set<UserLoginActivity>();
    
    // Permission Delegations
    public DbSet<UserPermissionDelegation> UserPermissionDelegations => Set<UserPermissionDelegation>();

    // Chat
    public DbSet<ChatThread> ChatThreads => Set<ChatThread>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatThreadParticipant> ChatThreadParticipants => Set<ChatThreadParticipant>();

    // Stock Management
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<StockLocation> StockLocations => Set<StockLocation>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();

    // Security
    public DbSet<UserTwoFactorAuth> UserTwoFactorAuths => Set<UserTwoFactorAuth>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<PasswordPolicy> PasswordPolicies => Set<PasswordPolicy>();

    // Privacy
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<DataExportRequest> DataExportRequests => Set<DataExportRequest>();
    public DbSet<DataRetentionPolicy> DataRetentionPolicies => Set<DataRetentionPolicy>();

    // Training
    public DbSet<TrainingRecord> TrainingRecords => Set<TrainingRecord>();
    public DbSet<CompetencyAssessment> CompetencyAssessments => Set<CompetencyAssessment>();

    // Risk
    public DbSet<RiskAssessment> RiskAssessments => Set<RiskAssessment>();
    public DbSet<RiskRegisterEntry> RiskRegisterEntries => Set<RiskRegisterEntry>();

    // Supplier
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierAudit> SupplierAudits => Set<SupplierAudit>();

    // News
    public DbSet<KasahQMS.Domain.Entities.News.NewsArticle> NewsArticles => Set<KasahQMS.Domain.Entities.News.NewsArticle>();
    public DbSet<KasahQMS.Domain.Entities.News.NewsRead> NewsReads => Set<KasahQMS.Domain.Entities.News.NewsRead>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // ===========================================
        // Global Query Filters for Tenant Isolation
        // ===========================================
        // These filters ensure that queries automatically scope to the current tenant.
        // Soft delete capability is built into AuditableEntity (IsDeleted, DeletedAt, DeletedById)
        // but filters will be added incrementally as needed for compliance workflows.
        // Use IgnoreQueryFilters() when cross-tenant access is explicitly needed.

        modelBuilder.Entity<User>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Document>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Audit>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Capa>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<QmsTask>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AuditLogEntry>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<OrganizationUnit>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Role>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<SystemSetting>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<UserPermissionDelegation>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<ChatThread>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<UserLoginActivity>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<StockItem>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<StockLocation>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<StockMovement>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<StockReservation>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<DocumentType>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<DocumentCategory>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);

        // Security query filters
        modelBuilder.Entity<UserSession>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<PasswordPolicy>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);

        // Privacy query filters
        modelBuilder.Entity<ConsentRecord>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<DataExportRequest>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<DataRetentionPolicy>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);

        // Training query filters
        modelBuilder.Entity<TrainingRecord>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<CompetencyAssessment>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);

        // Risk query filters
        modelBuilder.Entity<RiskAssessment>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);

        // Supplier query filters
        modelBuilder.Entity<Supplier>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);

        // ===========================================
        // Child Entity Query Filters (matching parent tenant filters)
        // ===========================================
        // These filter child entities by their parent entity's TenantId to ensure
        // tenant isolation at multiple levels
        modelBuilder.Entity<RefreshToken>()
            .HasQueryFilter(e => CurrentTenantId == null || e.User.TenantId == CurrentTenantId);

        modelBuilder.Entity<UserRole>()
            .HasQueryFilter(e => CurrentTenantId == null || e.User.TenantId == CurrentTenantId);

        modelBuilder.Entity<DocumentVersion>()
            .HasQueryFilter(e => CurrentTenantId == null || e.Document.TenantId == CurrentTenantId);

        modelBuilder.Entity<DocumentApproval>()
            .HasQueryFilter(e => CurrentTenantId == null || e.Document.TenantId == CurrentTenantId);

        modelBuilder.Entity<DocumentAttachment>()
            .HasQueryFilter(e => CurrentTenantId == null || e.Document.TenantId == CurrentTenantId);

        modelBuilder.Entity<DocumentTypeApprover>()
            .HasQueryFilter(e => CurrentTenantId == null || e.DocumentType.TenantId == CurrentTenantId);

        modelBuilder.Entity<DocumentAccessLog>()
            .HasQueryFilter(e => CurrentTenantId == null || e.Document.TenantId == CurrentTenantId);

        modelBuilder.Entity<AuditChecklistItem>()
            .HasQueryFilter(e => CurrentTenantId == null || e.Audit.TenantId == CurrentTenantId);

        modelBuilder.Entity<AuditFinding>()
            .HasQueryFilter(e => CurrentTenantId == null || e.Audit.TenantId == CurrentTenantId);

        modelBuilder.Entity<CapaAction>()
            .HasQueryFilter(e => CurrentTenantId == null || e.Capa.TenantId == CurrentTenantId);

        modelBuilder.Entity<Notification>()
            .HasQueryFilter(e => CurrentTenantId == null || e.User.TenantId == CurrentTenantId);

        // Configure Role permissions as JSON string
        modelBuilder.Entity<Role>()
            .Property(r => r.Permissions)
            .HasConversion(
                v => v != null ? string.Join(',', v.Select(p => ((int)p).ToString())) : "",
                v => string.IsNullOrEmpty(v) 
                    ? Array.Empty<Permission>() 
                    : v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => (Permission)int.Parse(s))
                        .ToArray())
            .Metadata.SetValueComparer(new ValueComparer<Permission[]>(
                (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                c => c != null ? c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())) : 0,
                c => c != null ? c.ToArray() : Array.Empty<Permission>()));

        // Configure Task tags as JSON
        modelBuilder.Entity<QmsTask>()
            .Property(t => t.Tags)
            .HasConversion(
                v => v != null ? string.Join(',', v) : "",
                v => string.IsNullOrEmpty(v) 
                    ? new List<string>() 
                    : v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList())
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                c => c != null ? c.Aggregate(0, (a, v) => HashCode.Combine(a, v != null ? v.GetHashCode() : 0)) : 0,
                c => c != null ? new List<string>(c) : new List<string>()));

        // Configure many-to-many relationship between User and Role using UserRole junction table
        modelBuilder.Entity<User>()
            .HasMany(u => u.Roles)
            .WithMany(r => r.Users)
            .UsingEntity<UserRole>(
                j => j
                    .HasOne(ur => ur.Role)
                    .WithMany(r => r.UserRoles)
                    .HasForeignKey(ur => ur.RoleId),
                j => j
                    .HasOne(ur => ur.User)
                    .WithMany(u => u.UserRoles)
                    .HasForeignKey(ur => ur.UserId),
                j =>
                {
                    j.Property(ur => ur.AssignedAt).HasDefaultValueSql("NOW()");
                    j.HasKey(ur => new { ur.UserId, ur.RoleId });
                });

        // Create indexes for performance
        modelBuilder.Entity<Document>().HasIndex(d => new { d.TenantId, d.DocumentNumber }).IsUnique();
        modelBuilder.Entity<Audit>().HasIndex(a => new { a.TenantId, a.AuditNumber }).IsUnique();
        modelBuilder.Entity<Capa>().HasIndex(c => new { c.TenantId, c.CapaNumber }).IsUnique();
        modelBuilder.Entity<QmsTask>().HasIndex(t => new { t.TenantId, t.TaskNumber }).IsUnique();
        modelBuilder.Entity<AuditLogEntry>().HasIndex(a => new { a.TenantId, a.Timestamp });
        modelBuilder.Entity<Notification>().HasIndex(n => n.UserId);
        modelBuilder.Entity<Notification>().HasIndex(n => new { n.UserId, n.IsRead });
        modelBuilder.Entity<UserLoginActivity>().HasIndex(a => new { a.TenantId, a.Timestamp });
        modelBuilder.Entity<UserLoginActivity>().HasIndex(a => a.UserId);
        modelBuilder.Entity<ChatMessage>().HasIndex(m => m.ThreadId);
        modelBuilder.Entity<ChatMessage>().HasIndex(m => m.CreatedAt);
        modelBuilder.Entity<ChatThreadParticipant>().HasIndex(p => new { p.ThreadId, p.UserId }).IsUnique();
        
        // Stock Management indexes
        modelBuilder.Entity<StockItem>().HasIndex(s => new { s.TenantId, s.SKU }).IsUnique();
        modelBuilder.Entity<StockLocation>().HasIndex(l => new { l.TenantId, l.Code }).IsUnique();
        modelBuilder.Entity<StockMovement>().HasIndex(m => new { m.TenantId, m.MovementNumber }).IsUnique();
        modelBuilder.Entity<StockMovement>().HasIndex(m => new { m.StockItemId, m.Status });
        modelBuilder.Entity<StockMovement>().HasIndex(m => m.CreatedAt);
        modelBuilder.Entity<StockReservation>().HasIndex(r => new { r.TenantId, r.ReservationNumber }).IsUnique();
        modelBuilder.Entity<StockReservation>().HasIndex(r => new { r.StockItemId, r.Status });
        modelBuilder.Entity<StockReservation>().HasIndex(r => r.TenderId);
        
        // Security indexes
        modelBuilder.Entity<UserTwoFactorAuth>().HasIndex(t => t.UserId).IsUnique();
        modelBuilder.Entity<UserSession>().HasIndex(s => s.UserId);
        modelBuilder.Entity<UserSession>().HasIndex(s => s.Token);

        // Privacy indexes
        modelBuilder.Entity<ConsentRecord>().HasIndex(c => new { c.UserId, c.ConsentType });
        modelBuilder.Entity<DataExportRequest>().HasIndex(d => d.UserId);

        // Training indexes
        modelBuilder.Entity<TrainingRecord>().HasIndex(t => t.UserId);
        modelBuilder.Entity<TrainingRecord>().HasIndex(t => t.Status);
        modelBuilder.Entity<CompetencyAssessment>().HasIndex(c => c.UserId);

        // Risk indexes
        modelBuilder.Entity<RiskAssessment>().HasIndex(r => new { r.TenantId, r.RiskNumber }).IsUnique();
        modelBuilder.Entity<RiskAssessment>().HasIndex(r => r.OwnerId);
        modelBuilder.Entity<RiskAssessment>().HasIndex(r => r.Status);
        modelBuilder.Entity<RiskRegisterEntry>().HasIndex(e => e.RiskAssessmentId);

        // Supplier indexes
        modelBuilder.Entity<Supplier>().HasIndex(s => new { s.TenantId, s.Code }).IsUnique();
        modelBuilder.Entity<Supplier>().HasIndex(s => s.QualificationStatus);
        modelBuilder.Entity<SupplierAudit>().HasIndex(a => a.SupplierId);
        
        // Stock Movement relationships
        modelBuilder.Entity<StockMovement>()
            .HasOne(m => m.StockItem)
            .WithMany(s => s.Movements)
            .HasForeignKey(m => m.StockItemId)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<StockMovement>()
            .HasOne(m => m.FromLocation)
            .WithMany(l => l.OutgoingMovements)
            .HasForeignKey(m => m.FromLocationId)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<StockMovement>()
            .HasOne(m => m.ToLocation)
            .WithMany(l => l.IncomingMovements)
            .HasForeignKey(m => m.ToLocationId)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<StockMovement>()
            .HasOne(m => m.Reservation)
            .WithMany(r => r.Movements)
            .HasForeignKey(m => m.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);
            
        // Stock Reservation relationships
        modelBuilder.Entity<StockReservation>()
            .HasOne(r => r.StockItem)
            .WithMany(s => s.Reservations)
            .HasForeignKey(r => r.StockItemId)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<StockReservation>()
            .HasOne(r => r.Location)
            .WithMany()
            .HasForeignKey(r => r.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
