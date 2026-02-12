using KasahQMS.Domain.Entities.AuditLog;
using KasahQMS.Domain.Entities.Audits;
using KasahQMS.Domain.Entities.Capa;
using KasahQMS.Domain.Entities.Chat;
using KasahQMS.Domain.Entities.Configuration;
using KasahQMS.Domain.Entities.Documents;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Domain.Entities.Notifications;
using KasahQMS.Domain.Entities.Security;
using KasahQMS.Domain.Entities.Stock;
using KasahQMS.Domain.Entities.Tasks;
using KasahQMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace KasahQMS.Infrastructure.Persistence.Data;

/// <summary>
/// Application database context.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

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
