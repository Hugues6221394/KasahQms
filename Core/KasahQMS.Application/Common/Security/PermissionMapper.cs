using System.Collections.Generic;
using KasahQMS.Domain.Enums;

namespace KasahQMS.Application.Common.Security;

/// <summary>
/// Maps Domain Permission enum values to Application-layer permission strings.
/// Used so role-based permissions (stored as enum) match handler authorization checks (Documents.View, etc.).
/// </summary>
public static class PermissionMapper
{
    /// <summary>
    /// Returns the application permission strings for a given domain permission.
    /// </summary>
    public static IReadOnlyList<string> ToApplicationPermissions(Permission permission)
    {
        if (permission == Permission.None) return Array.Empty<string>();

        var list = new List<string>();

        if ((permission & Permission.DocumentRead) != 0)
            list.Add(Permissions.Documents.View);
        if ((permission & Permission.DocumentCreate) != 0)
        {
            list.Add(Permissions.Documents.Create);
            list.Add(Permissions.Documents.Submit); // Creator submits draft
        }
        if ((permission & Permission.DocumentEdit) != 0)
            list.Add(Permissions.Documents.Edit);
        if ((permission & Permission.DocumentDelete) != 0)
            list.Add(Permissions.Documents.Delete);
        if ((permission & Permission.DocumentApprove) != 0)
        {
            list.Add(Permissions.Documents.Approve);
            list.Add(Permissions.Documents.Reject);
        }
        if ((permission & Permission.DocumentArchive) != 0)
            list.Add(Permissions.Documents.Archive);

        if ((permission & Permission.AuditRead) != 0)
            list.Add(Permissions.Audits.View);
        if ((permission & Permission.AuditCreate) != 0)
            list.Add(Permissions.Audits.Create);
        if ((permission & Permission.AuditEdit) != 0)
            list.Add(Permissions.Audits.Edit);
        if ((permission & Permission.AuditDelete) != 0)
            list.Add(Permissions.Audits.Delete);

        if ((permission & Permission.CapaRead) != 0)
            list.Add(Permissions.Capa.View);
        if ((permission & Permission.CapaCreate) != 0)
            list.Add(Permissions.Capa.Create);
        if ((permission & Permission.CapaEdit) != 0)
            list.Add(Permissions.Capa.Edit);
        if ((permission & Permission.CapaDelete) != 0)
            list.Add(Permissions.Capa.Delete);
        if ((permission & Permission.CapaVerify) != 0)
            list.Add(Permissions.Capa.Verify);

        if ((permission & Permission.TaskRead) != 0)
            list.Add(Permissions.Tasks.View);
        if ((permission & Permission.TaskCreate) != 0)
        {
            list.Add(Permissions.Tasks.Create);
            list.Add(Permissions.Tasks.Complete); // Assignees complete tasks
        }
        if ((permission & Permission.TaskEdit) != 0)
            list.Add(Permissions.Tasks.Edit);
        if ((permission & Permission.TaskDelete) != 0)
            list.Add(Permissions.Tasks.Delete);
        if ((permission & Permission.TaskAssign) != 0)
            list.Add(Permissions.Tasks.Assign);

        if ((permission & Permission.UserRead) != 0)
            list.Add(Permissions.Users.View);
        if ((permission & Permission.UserCreate) != 0)
            list.Add(Permissions.Users.Create);
        if ((permission & Permission.UserEdit) != 0)
            list.Add(Permissions.Users.Edit);
        if ((permission & Permission.UserDelete) != 0)
            list.Add(Permissions.Users.Delete);

        if ((permission & Permission.SystemSettings) != 0)
            list.Add(Permissions.System.ManageSettings);
        if ((permission & Permission.ViewAuditLogs) != 0)
        {
            list.Add(Permissions.AuditLogs.View);
            list.Add(Permissions.AuditLogs.Export);
        }
        if ((permission & Permission.ManageRoles) != 0)
        {
            list.Add(Permissions.Users.ManageRoles);
            list.Add(Permissions.Roles.ManagePermissions);
        }

        return list;
    }

    /// <summary>
    /// ViewAll permissions to inject for hierarchy roles (TMD, Deputy, Department Manager).
    /// Call only when user has the corresponding Read permission.
    /// </summary>
    public static IReadOnlyList<string> ViewAllForHierarchyRoles(bool hasDocumentRead, bool hasTaskRead, bool hasAuditRead, bool hasCapaRead, bool hasUserRead)
    {
        var list = new List<string>();
        if (hasDocumentRead) list.Add(Permissions.Documents.ViewAll);
        if (hasTaskRead) list.Add(Permissions.Tasks.ViewAll);
        if (hasAuditRead) list.Add(Permissions.Audits.ViewAll);
        if (hasCapaRead) list.Add(Permissions.Capa.ViewAll);
        if (hasUserRead) list.Add(Permissions.Users.ViewAll);
        return list;
    }
}
