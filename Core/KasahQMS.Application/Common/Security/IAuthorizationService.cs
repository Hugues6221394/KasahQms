using KasahQMS.Domain.Common;

namespace KasahQMS.Application.Common.Security;

/// <summary>
/// Authorization service for checking user permissions and access rights.
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Checks if the current user has the specified permission.
    /// </summary>
    Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if the current user has any of the specified permissions.
    /// </summary>
    Task<bool> HasAnyPermissionAsync(IEnumerable<string> permissions, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if the current user has all of the specified permissions.
    /// </summary>
    Task<bool> HasAllPermissionsAsync(IEnumerable<string> permissions, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if the current user is in the specified role.
    /// </summary>
    Task<bool> IsInRoleAsync(string role, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if the current user can access the specified resource.
    /// </summary>
    Task<bool> CanAccessResourceAsync(string resourceType, Guid resourceId, string action, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if the current user can view another user's data (hierarchy check).
    /// </summary>
    Task<bool> CanViewUserDataAsync(Guid targetUserId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if the current user can view a subordinate's data.
    /// </summary>
    Task<bool> CanViewSubordinateDataAsync(Guid subordinateId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if the current user can delegate a permission to a subordinate.
    /// </summary>
    Task<bool> CanDelegatePermissionAsync(Guid subordinateId, string permission, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all permissions for the current user.
    /// </summary>
    Task<IEnumerable<string>> GetUserPermissionsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Authorizes or throws exception.
    /// </summary>
    Task AuthorizeAsync(string permission, CancellationToken cancellationToken = default);
}

/// <summary>
/// Static class containing all system permission constants.
/// </summary>
public static class Permissions
{
    public static class Documents
    {
        public const string View = "Documents.View";
        public const string Create = "Documents.Create";
        public const string Edit = "Documents.Edit";
        public const string Delete = "Documents.Delete";
        public const string Submit = "Documents.Submit";
        public const string Approve = "Documents.Approve";
        public const string Reject = "Documents.Reject";
        public const string Archive = "Documents.Archive";
        public const string ViewAll = "Documents.ViewAll";
        public const string ManageVersions = "Documents.ManageVersions";
    }
    
    public static class Tasks
    {
        public const string View = "Tasks.View";
        public const string Create = "Tasks.Create";
        public const string Edit = "Tasks.Edit";
        public const string Delete = "Tasks.Delete";
        public const string Assign = "Tasks.Assign";
        public const string Complete = "Tasks.Complete";
        public const string ViewAll = "Tasks.ViewAll";
    }
    
    public static class Audits
    {
        public const string View = "Audits.View";
        public const string Create = "Audits.Create";
        public const string Edit = "Audits.Edit";
        public const string Delete = "Audits.Delete";
        public const string Conduct = "Audits.Conduct";
        public const string AddFindings = "Audits.AddFindings";
        public const string CloseAudit = "Audits.CloseAudit";
        public const string ViewAll = "Audits.ViewAll";
    }
    
    public static class Capa
    {
        public const string View = "Capa.View";
        public const string Create = "Capa.Create";
        public const string Edit = "Capa.Edit";
        public const string Delete = "Capa.Delete";
        public const string Assign = "Capa.Assign";
        public const string Verify = "Capa.Verify";
        public const string Close = "Capa.Close";
        public const string ViewAll = "Capa.ViewAll";
    }
    
    public static class Users
    {
        public const string View = "Users.View";
        public const string Create = "Users.Create";
        public const string Edit = "Users.Edit";
        public const string Delete = "Users.Delete";
        public const string ManageRoles = "Users.ManageRoles";
        public const string ViewAll = "Users.ViewAll";
        public const string Unlock = "Users.Unlock";
        public const string ResetPassword = "Users.ResetPassword";
    }
    
    public static class Roles
    {
        public const string View = "Roles.View";
        public const string Create = "Roles.Create";
        public const string Edit = "Roles.Edit";
        public const string Delete = "Roles.Delete";
        public const string ManagePermissions = "Roles.ManagePermissions";
    }
    
    public static class Organization
    {
        public const string View = "Organization.View";
        public const string Create = "Organization.Create";
        public const string Edit = "Organization.Edit";
        public const string Delete = "Organization.Delete";
        public const string ManageHierarchy = "Organization.ManageHierarchy";
    }
    
    public static class Reports
    {
        public const string View = "Reports.View";
        public const string Create = "Reports.Create";
        public const string Export = "Reports.Export";
        public const string ViewAll = "Reports.ViewAll";
    }
    
    public static class AuditLogs
    {
        public const string View = "AuditLogs.View";
        public const string Export = "AuditLogs.Export";
    }
    
    public static class System
    {
        public const string ManageSettings = "System.ManageSettings";
        public const string ManageTenants = "System.ManageTenants";
        public const string ViewSystemHealth = "System.ViewSystemHealth";
    }
}

/// <summary>
/// Default role definitions with their permissions.
/// </summary>
public static class DefaultRoles
{
    public const string SystemAdmin = "SystemAdmin";
    public const string TenantAdmin = "TenantAdmin";
    public const string TopManagingDirector = "TopManagingDirector";
    public const string DeputyDirector = "DeputyDirector";
    public const string DepartmentManager = "DepartmentManager";
    public const string JuniorStaff = "JuniorStaff";
    public const string Auditor = "Auditor";
    public const string ReadOnly = "ReadOnly";
    
    /// <summary>
    /// Gets the default permissions for a role.
    /// </summary>
    public static IEnumerable<string> GetDefaultPermissions(string role)
    {
        return role switch
        {
            SystemAdmin => GetSystemAdminPermissions(),
            TenantAdmin => GetTenantAdminPermissions(),
            TopManagingDirector => GetTMDPermissions(),
            DeputyDirector => GetDeputyPermissions(),
            DepartmentManager => GetManagerPermissions(),
            JuniorStaff => GetJuniorStaffPermissions(),
            Auditor => GetAuditorPermissions(),
            ReadOnly => GetReadOnlyPermissions(),
            _ => Enumerable.Empty<string>()
        };
    }
    
    private static IEnumerable<string> GetSystemAdminPermissions()
    {
        // System admins have all permissions
        return new[]
        {
            Permissions.System.ManageSettings,
            Permissions.System.ManageTenants,
            Permissions.System.ViewSystemHealth,
            Permissions.Users.ViewAll,
            Permissions.Users.Create,
            Permissions.Users.Edit,
            Permissions.Users.Delete,
            Permissions.Users.ManageRoles,
            Permissions.Users.Unlock,
            Permissions.Users.ResetPassword,
            Permissions.Roles.View,
            Permissions.Roles.Create,
            Permissions.Roles.Edit,
            Permissions.Roles.Delete,
            Permissions.Roles.ManagePermissions,
            Permissions.Organization.View,
            Permissions.Organization.Create,
            Permissions.Organization.Edit,
            Permissions.Organization.Delete,
            Permissions.Organization.ManageHierarchy,
            Permissions.AuditLogs.View,
            Permissions.AuditLogs.Export,
        };
    }
    
    private static IEnumerable<string> GetTenantAdminPermissions()
    {
        return new[]
        {
            Permissions.Users.ViewAll,
            Permissions.Users.Create,
            Permissions.Users.Edit,
            Permissions.Users.ManageRoles,
            Permissions.Users.Unlock,
            Permissions.Users.ResetPassword,
            Permissions.Roles.View,
            Permissions.Roles.Create,
            Permissions.Roles.Edit,
            Permissions.Roles.ManagePermissions,
            Permissions.Organization.View,
            Permissions.Organization.Create,
            Permissions.Organization.Edit,
            Permissions.Organization.ManageHierarchy,
            Permissions.Documents.ViewAll,
            Permissions.Tasks.ViewAll,
            Permissions.Audits.ViewAll,
            Permissions.Capa.ViewAll,
            Permissions.Reports.ViewAll,
            Permissions.AuditLogs.View,
            Permissions.AuditLogs.Export,
        };
    }
    
    private static IEnumerable<string> GetTMDPermissions()
    {
        return new[]
        {
            // Full document access
            Permissions.Documents.View,
            Permissions.Documents.Create,
            Permissions.Documents.Edit,
            Permissions.Documents.Submit,
            Permissions.Documents.Approve,
            Permissions.Documents.Reject,
            Permissions.Documents.Archive,
            Permissions.Documents.ViewAll,
            // Full task access
            Permissions.Tasks.View,
            Permissions.Tasks.Create,
            Permissions.Tasks.Edit,
            Permissions.Tasks.Assign,
            Permissions.Tasks.Complete,
            Permissions.Tasks.ViewAll,
            // Full audit access
            Permissions.Audits.View,
            Permissions.Audits.Create,
            Permissions.Audits.CloseAudit,
            Permissions.Audits.ViewAll,
            // Full CAPA access
            Permissions.Capa.View,
            Permissions.Capa.Create,
            Permissions.Capa.Verify,
            Permissions.Capa.Close,
            Permissions.Capa.ViewAll,
            // User management (limited)
            Permissions.Users.View,
            Permissions.Users.ViewAll,
            // Reports
            Permissions.Reports.View,
            Permissions.Reports.Create,
            Permissions.Reports.Export,
            Permissions.Reports.ViewAll,
            // Organization
            Permissions.Organization.View,
            // Audit logs
            Permissions.AuditLogs.View,
        };
    }
    
    private static IEnumerable<string> GetDeputyPermissions()
    {
        return new[]
        {
            Permissions.Documents.View,
            Permissions.Documents.Create,
            Permissions.Documents.Edit,
            Permissions.Documents.Submit,
            Permissions.Documents.Approve,
            Permissions.Documents.Reject,
            Permissions.Documents.ViewAll,
            Permissions.Tasks.View,
            Permissions.Tasks.Create,
            Permissions.Tasks.Edit,
            Permissions.Tasks.Assign,
            Permissions.Tasks.Complete,
            Permissions.Tasks.ViewAll,
            Permissions.Audits.View,
            Permissions.Audits.ViewAll,
            Permissions.Capa.View,
            Permissions.Capa.Create,
            Permissions.Capa.ViewAll,
            Permissions.Users.View,
            Permissions.Reports.View,
            Permissions.Reports.Create,
            Permissions.Reports.Export,
            Permissions.Organization.View,
        };
    }
    
    private static IEnumerable<string> GetManagerPermissions()
    {
        return new[]
        {
            Permissions.Documents.View,
            Permissions.Documents.Create,
            Permissions.Documents.Edit,
            Permissions.Documents.Submit,
            Permissions.Documents.Approve,
            Permissions.Documents.Reject,
            Permissions.Tasks.View,
            Permissions.Tasks.Create,
            Permissions.Tasks.Edit,
            Permissions.Tasks.Assign,
            Permissions.Tasks.Complete,
            Permissions.Audits.View,
            Permissions.Capa.View,
            Permissions.Capa.Create,
            Permissions.Capa.Assign,
            Permissions.Users.View,
            Permissions.Reports.View,
            Permissions.Reports.Create,
            Permissions.Organization.View,
        };
    }
    
    private static IEnumerable<string> GetJuniorStaffPermissions()
    {
        return new[]
        {
            Permissions.Documents.View,
            Permissions.Documents.Create,
            Permissions.Documents.Edit,
            Permissions.Documents.Submit,
            Permissions.Tasks.View,
            Permissions.Tasks.Complete,
            Permissions.Capa.View,
        };
    }
    
    private static IEnumerable<string> GetAuditorPermissions()
    {
        return new[]
        {
            Permissions.Documents.View,
            Permissions.Documents.ViewAll,
            Permissions.Audits.View,
            Permissions.Audits.Create,
            Permissions.Audits.Edit,
            Permissions.Audits.Conduct,
            Permissions.Audits.AddFindings,
            Permissions.Audits.CloseAudit,
            Permissions.Audits.ViewAll,
            Permissions.Capa.View,
            Permissions.Capa.ViewAll,
            Permissions.Reports.View,
            Permissions.Reports.Export,
            Permissions.AuditLogs.View,
            Permissions.AuditLogs.Export,
        };
    }
    
    private static IEnumerable<string> GetReadOnlyPermissions()
    {
        return new[]
        {
            Permissions.Documents.View,
            Permissions.Tasks.View,
            Permissions.Audits.View,
            Permissions.Capa.View,
            Permissions.Reports.View,
        };
    }
}

