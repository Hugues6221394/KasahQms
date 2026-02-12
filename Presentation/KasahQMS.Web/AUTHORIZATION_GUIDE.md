# KasahQMS Authorization & Access Control - Implementation Guide

## Overview

This document describes the comprehensive authorization and access control framework implemented in the KasahQMS system to enforce role-based access control (RBAC) with hierarchical permission management.

## System Architecture

### Authorization Flow

```
User Request
    ↓
[Authenticate] (Program.cs middleware)
    ↓
[Extract Claims] (CurrentUserService)
    ↓
[Check Authorization] (AuthorizationService / Filters)
    ↓
[Enforce State Machine] (DocumentStateService)
    ↓
[Execute Action]
    ↓
[Log Action] (AuditLoggingService)
```

## Core Components

### 1. Authorization Service (`IAuthorizationService`)

**Purpose**: Central permission engine for all authorization checks

**Key Methods**:
- `CanCreateDocumentAsync(userId)` - Document creation permission
- `CanEditDocumentAsync(userId, documentId)` - Edit permission (Draft only)
- `CanSubmitDocumentAsync(userId, documentId)` - Submit for approval
- `CanApproveDocumentAsync(userId, documentId)` - Approval permission (manager+)
- `CanViewDocumentAsync(userId, documentId)` - View permission (hierarchical)
- `CanCreateTaskAsync(userId)` - Task creation (manager only)

**Usage Pattern**:
```csharp
var canApprove = await _authorizationService.CanApproveDocumentAsync(userId, documentId);
if (!canApprove)
    return Forbid(); // Return 403
```

### 2. Hierarchy Service (`IHierarchyService`)

**Purpose**: Manager-subordinate relationship management

**Key Methods**:
- `GetSubordinateUserIdsAsync(managerId)` - Get all direct and indirect reports
- `IsSubordinateAsync(managerId, targetUserId)` - Check subordinate relationship
- `GetManagerChainAsync(userId)` - Get upward chain of command

**Features**:
- Recursive subordinate traversal
- Circular reference detection
- In-memory caching for performance

**Usage Pattern**:
```csharp
var subordinates = await _hierarchyService.GetSubordinateUserIdsAsync(managerId);
bool canView = subordinates.Contains(targetUserId);
```

### 3. Document State Machine (`IDocumentStateService`)

**Purpose**: Enforce document lifecycle workflow

**States**:
```
Draft → Submitted → Approved → Published
  ↑                    ↓
  └──────── Rejected ←─┘
```

**Key Methods**:
- `ValidateStateTransitionAsync(documentId, targetState)` - Check valid transition
- `TransitionStateAsync(documentId, targetState)` - Execute transition
- `IsEditableAsync(documentId)` - Check if editable (Draft only)

**Valid Transitions**:
- Draft → Submitted, Rejected, Archived
- Submitted → Approved, Rejected, Draft
- Approved → Published, Archived
- Rejected → Draft, Archived
- Published → Archived (read-only)

### 4. Audit Logging Service (`IAuditLoggingService`)

**Purpose**: Create immutable audit trail

**Logged Events**:
- `DOCUMENT_CREATED` - Document created
- `DOCUMENT_SUBMITTED` - Submitted for approval
- `DOCUMENT_APPROVED` - Approved by reviewer
- `DOCUMENT_REJECTED` - Rejected with reason
- `TASK_CREATED` - Task assigned
- `USER_LOGIN` - User authentication
- `LOGIN_FAILED` - Failed login attempt

**Data Captured**:
- User ID (who)
- Action type (what)
- Entity ID (which)
- Timestamp (when)
- IP address (where)
- Success/failure status

### 5. Authorization Filters

**AuthorizeDocumentOperation** Filter:
```csharp
[AuthorizeDocumentOperation("Create")]
[AuthorizeDocumentOperation("Edit")]
[AuthorizeDocumentOperation("Approve")]
[AuthorizeDocumentOperation("Reject")]
[AuthorizeDocumentOperation("Delete")]
```

**AuthorizeTaskOperation** Filter:
```csharp
[AuthorizeTaskOperation("Create")]
[AuthorizeTaskOperation("Assign")]
```

**ValidateDocumentState** Filter:
```csharp
[ValidateDocumentState("Draft")]    // Must be in Draft
[ValidateDocumentState("Submitted")] // Must be Submitted
```

## Role-Based Access Control (RBAC)

### Role Definitions

| Role | Create Doc | Edit Doc | Submit Doc | Approve Doc | Create Task | View All |
|------|-----------|----------|-----------|-------------|------------|----------|
| TMD | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Deputy Country Mgr | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Department Manager | ✅ | ✅ | ✅ | ✅ | ✅ | ✅* |
| Manager | ✅ | ✅ | ✅ | ✅ | ✅ | ✅* |
| Staff | ✅ | ✅* | ✅* | ❌ | ❌ | ❌ |
| Auditor | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ RO |

*Limited to own department/subordinates
RO = Read-Only

### Permission Rules

#### Document Permissions

**Create Document**:
- ❌ Auditors cannot create
- ✅ All other authenticated users can create

**Edit Document**:
- ❌ Auditors cannot edit
- ✅ Only Draft status documents are editable
- ✅ Managers/Admins can edit any Draft

**Submit Document**:
- ❌ Auditors cannot submit
- ✅ Document must be in Draft state
- ✅ Only non-auditors can submit

**Approve Document**:
- ❌ Auditors cannot approve
- ✅ Document must be Submitted
- ✅ Only Manager+ roles can approve
- ✅ Admins can always approve

**Reject Document**:
- ❌ Auditors cannot reject
- ✅ Document must be Submitted
- ✅ Rejection reason is mandatory
- ✅ Only Manager+ roles can reject

**Delete Document**:
- ❌ Auditors cannot delete
- ❌ Published documents cannot be deleted
- ✅ Only Admin/TMD can delete

**View Document**:
- ✅ Auditors can view all (read-only)
- ✅ Admins can view all
- ✅ Users can view permitted documents

#### Task Permissions

**Create Task**:
- ❌ Auditors cannot create tasks
- ✅ Only Manager+ roles can create
- ✅ TMD, Deputy, Dept Manager, Manager, Admin

**Assign Task**:
- ✅ Must have task creation permission
- ✅ Department Managers limited to department
- ✅ Admins/TMD can assign to anyone

**View Task**:
- ✅ Auditors can view all (read-only)
- ✅ Admins can view all
- ✅ Staff can view assigned tasks

## Implementation Examples

### Example 1: Prevent Auditor from Creating Document

**Controller Code**:
```csharp
[HttpPost]
[Authorize]
public async Task<ActionResult> CreateDocument([FromBody] CreateDocumentDto dto)
{
    var userId = _currentUserService.UserId;
    
    // Check permission
    var canCreate = await _authorizationService.CanCreateDocumentAsync(userId.Value);
    if (!canCreate)
    {
        await _auditLoggingService.LogActionAsync("DOCUMENT_CREATE_DENIED", "Document", null, 
            "Auditor attempted unauthorized document creation", false);
        return Forbid(); // 403
    }
    
    // Create document...
    await _auditLoggingService.LogDocumentCreatedAsync(docId, dto.Title);
    return Ok();
}
```

**Result**: Auditor gets 403 Forbidden, action is logged

### Example 2: Prevent Staff from Creating Task

**Page Code**:
```csharp
public async Task<IActionResult> OnPostAsync()
{
    var currentUser = await GetCurrentUserAsync();
    
    // Authorization check
    var isManager = currentUser.Roles?.Any(r => r.Name is "TMD" 
        or "Deputy Country Manager" 
        or "Department Manager" 
        or "Manager" 
        or "System Admin" 
        or "Admin") == true;
    
    if (!isManager)
    {
        ModelState.AddModelError("", "Only managers can create tasks.");
        await LoadLookupsAsync();
        return Page(); // Show error message
    }
    
    // Create task...
}
```

**Result**: Staff sees error message, action blocked

### Example 3: Enforce Document State

**Controller Code**:
```csharp
[HttpPost("{id:guid}/approve")]
[Authorize]
public async Task<IActionResult> ApproveDocument(Guid id, [FromBody] ApproveDocumentDto dto)
{
    // Check state
    var currentState = await _documentStateService.GetCurrentStateAsync(id);
    if (currentState != DocumentStatus.Submitted)
        return BadRequest(new { error = "Document must be Submitted to approve" });
    
    // Check permission
    var canApprove = await _authorizationService.CanApproveDocumentAsync(userId.Value, id);
    if (!canApprove)
        return Forbid();
    
    // Approve...
    await _documentStateService.TransitionStateAsync(id, DocumentStatus.Approved, userId);
    await _auditLoggingService.LogDocumentApprovedAsync(id, dto.Comments);
    
    return Ok();
}
```

**Result**: 
- If state is not Submitted: 400 Bad Request
- If user cannot approve: 403 Forbidden
- If approved: State transitions and action logged

### Example 4: Hierarchical Access

**Manager viewing subordinate document**:
```csharp
public async Task<IActionResult> OnGetAsync(Guid documentId)
{
    var userId = _currentUserService.UserId;
    
    // Check hierarchical access
    var subordinates = await _hierarchyService.GetSubordinateUserIdsAsync(userId);
    var document = await _dbContext.Documents.FindAsync(documentId);
    
    if (!subordinates.Contains(document.CreatedBy))
        return Forbid(); // Cannot view - not subordinate
    
    // Can view subordinate's document...
    return Page();
}
```

## Testing the Authorization System

### Test Case 1: Auditor Block Document Creation

```csharp
[Test]
public async Task AuditorCannotCreateDocument()
{
    // Arrange
    var auditorUser = CreateTestUser(role: "Auditor");
    
    // Act
    var canCreate = await _authorizationService.CanCreateDocumentAsync(auditorUser.Id);
    
    // Assert
    Assert.IsFalse(canCreate);
}
```

### Test Case 2: Manager Can Approve Submitted Document

```csharp
[Test]
public async Task ManagerCanApproveSubmittedDocument()
{
    // Arrange
    var manager = CreateTestUser(role: "Manager");
    var doc = CreateTestDocument(status: DocumentStatus.Submitted);
    
    // Act
    var canApprove = await _authorizationService.CanApproveDocumentAsync(manager.Id, doc.Id);
    
    // Assert
    Assert.IsTrue(canApprove);
}
```

### Test Case 3: Staff Cannot Approve Document

```csharp
[Test]
public async Task StaffCannotApproveDocument()
{
    // Arrange
    var staff = CreateTestUser(role: "Staff");
    var doc = CreateTestDocument(status: DocumentStatus.Submitted);
    
    // Act
    var canApprove = await _authorizationService.CanApproveDocumentAsync(staff.Id, doc.Id);
    
    // Assert
    Assert.IsFalse(canApprove);
}
```

## Deployment Considerations

### 1. Performance

**Caching**:
- Hierarchy service uses in-memory cache
- Consider Redis for distributed scenarios
- Cache TTL: 5-15 minutes recommended

**N+1 Query Prevention**:
- Use `.Include()` for role loading
- Batch authorization checks where possible

### 2. Audit Logging

**Storage**:
- Currently logs to application logs
- Consider persistence to database
- Implement log retention policies (e.g., 2 years)

**Privacy**:
- Ensure IP addresses are anonymized if required
- Comply with GDPR/data protection regulations

### 3. Monitoring

**Alert on**:
- Multiple failed authorization attempts
- Auditor attempting privileged operations
- Unusual hierarchical access patterns

### 4. Documentation

- Keep role matrix updated
- Document any custom authorization rules
- Maintain permission matrix for compliance audits

## Troubleshooting

### Issue: User cannot perform expected action

**Diagnosis**:
1. Check user roles in database
2. Verify user's org unit and manager assignment
3. Check document status
4. Review audit logs for denial reason

**Fix**:
```sql
-- Check user roles
SELECT u.*, r.Name FROM Users u
JOIN UserRoles ur ON u.Id = ur.UserId
JOIN Roles r ON ur.RoleId = r.Id
WHERE u.Id = '{userId}';

-- Check hierarchy
SELECT Id, ManagerId, OrganizationUnitId 
FROM Users WHERE Id = '{userId}';
```

### Issue: Auditor can still create documents

**Diagnosis**:
1. Verify Auditor role name matches exactly ("Auditor")
2. Check that authorization service is registered
3. Verify controller uses authorization service

**Fix**:
- Clear application cache if using caching
- Restart application to reload roles

## References

- PRD: Document Lifecycle, Role Definitions, Approval Workflow
- Architecture: Clean Architecture with CQRS (MediatR)
- Framework: ASP.NET Core 8.0 with Entity Framework Core
- Patterns: Policy-based authorization, filter attributes

