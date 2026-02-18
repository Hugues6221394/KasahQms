namespace KasahQMS.Domain.Enums;

/// <summary>
/// System permissions for access control.
/// </summary>
[Flags]
public enum Permission
{
    None = 0,
    
    // Document permissions
    DocumentRead = 1,
    DocumentCreate = 2,
    DocumentEdit = 4,
    DocumentDelete = 8,
    DocumentApprove = 16,
    DocumentArchive = 32,
    
    // Audit permissions
    AuditRead = 64,
    AuditCreate = 128,
    AuditEdit = 256,
    AuditDelete = 512,
    
    // CAPA permissions
    CapaRead = 1024,
    CapaCreate = 2048,
    CapaEdit = 4096,
    CapaDelete = 8192,
    CapaVerify = 16384,
    
    // Task permissions
    TaskRead = 32768,
    TaskCreate = 65536,
    TaskEdit = 131072,
    TaskDelete = 262144,
    TaskAssign = 524288,
    
    // User permissions
    UserRead = 1048576,
    UserCreate = 2097152,
    UserEdit = 4194304,
    UserDelete = 8388608,
    
    // Admin permissions
    SystemSettings = 16777216,
    ViewAuditLogs = 33554432,
    ManageRoles = 67108864,

    // Stock permissions
    StockRead = 134217728,
    StockManage = 268435456,

    // Analytics
    AnalyticsRead = 536870912
}

