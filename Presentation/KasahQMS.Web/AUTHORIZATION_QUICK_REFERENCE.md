# Quick Reference: Authorization Usage

## For Controllers

### Blocking Auditors from Sensitive Operations

```csharp
[HttpPost]
public async Task<IActionResult> CreateDocument([FromBody] CreateDocumentDto dto)
{
    var userId = _currentUserService.UserId;
    if (userId == null) return Unauthorized();

    // Check if user can perform action
    var canCreate = await _authorizationService.CanCreateDocumentAsync(userId.Value);
    if (!canCreate)
    {
        await _auditLoggingService.LogActionAsync("ACTION_DENIED", "Document", null, 
            "Attempted unauthorized action", false);
        return Forbid(); // 403 Forbidden
    }

    // Proceed with operation...
    await _auditLoggingService.LogDocumentCreatedAsync(docId, dto.Title);
    return Ok();
}
```

### Enforcing Document State

```csharp
[HttpPost("{id:guid}/approve")]
public async Task<IActionResult> ApproveDocument(Guid id, [FromBody] ApproveDocumentDto dto)
{
    var userId = _currentUserService.UserId;
    if (userId == null) return Unauthorized();

    // Validate state
    var currentState = await _documentStateService.GetCurrentStateAsync(id);
    if (currentState != DocumentStatus.Submitted)
        return BadRequest(new { error = "Document must be Submitted" });

    // Check permission
    var canApprove = await _authorizationService.CanApproveDocumentAsync(userId.Value, id);
    if (!canApprove) return Forbid();

    // Execute with state transition
    var cmd = new ApproveDocumentCommand(id, dto.Comments);
    var result = await _mediator.Send(cmd);
    
    if (result.IsSuccess)
    {
        await _documentStateService.TransitionStateAsync(id, DocumentStatus.Approved, userId);
        await _auditLoggingService.LogDocumentApprovedAsync(id, dto.Comments);
    }

    return result.IsSuccess ? Ok() : BadRequest(result.ErrorMessage);
}
```

## For Razor Pages

### Check Permissions on Page Load

```csharp
public async Task OnGetAsync()
{
    var currentUser = await GetCurrentUserAsync();
    if (currentUser == null)
        return RedirectToPage("/Account/Login");

    // Check if auditor
    var isAuditor = await IsAuditorAsync();
    if (isAuditor)
    {
        ErrorMessage = "Auditors cannot perform this action.";
        return; // Show error on page
    }

    // Continue with page setup...
    await LoadData();
}
```

### Check Manager Permission

```csharp
public async Task<IActionResult> OnPostAsync()
{
    var currentUser = await GetCurrentUserAsync();
    
    // Only managers can create tasks
    var isManager = currentUser?.Roles?.Any(r => r.Name is "TMD" 
        or "Deputy Country Manager" or "Department Manager" or "Manager" 
        or "System Admin" or "Admin") == true;
    
    if (!isManager)
    {
        ModelState.AddModelError("", "Only managers can create tasks.");
        return Page();
    }

    // Create task...
}
```

### Check Hierarchical Access

```csharp
public async Task<IActionResult> OnGetAsync(Guid targetUserId)
{
    var userId = _currentUserService.UserId;
    
    // Can user view this subordinate?
    var canView = await CanViewSubordinateAsync(targetUserId);
    if (!canView)
        return Forbid();

    // Load subordinate data...
}
```

## Common Patterns

### Pattern 1: Simple Permission Check

```csharp
// Is user allowed to create?
if (await _authorizationService.CanCreateDocumentAsync(userId))
{
    // Allow action
}
else
{
    return Forbid(); // 403
}
```

### Pattern 2: State + Permission Check

```csharp
// Can user perform action in current state?
var state = await _documentStateService.GetCurrentStateAsync(documentId);
var allowed = state == DocumentStatus.Draft && 
              await _authorizationService.CanEditDocumentAsync(userId, documentId);

if (!allowed) return Forbid();
```

### Pattern 3: Audit Trail

```csharp
// Log before action
await _auditLoggingService.LogDocumentCreatedAsync(docId, title);

// Or log failure
await _auditLoggingService.LogActionAsync("FAILED_ACTION", "Document", docId, 
    "Reason for failure", false);
```

### Pattern 4: Hierarchical Check

```csharp
var subordinates = await _hierarchyService.GetSubordinateUserIdsAsync(userId);
if (!subordinates.Contains(targetUserId))
    return Forbid(); // User not in hierarchy
```

## Authorization Check Matrix

| Scenario | Check Method | Expected Result |
|----------|--------------|-----------------|
| Can create document? | `CanCreateDocumentAsync()` | Blocked for Auditor |
| Can edit document? | `CanEditDocumentAsync()` | Only Draft status |
| Can submit document? | `CanSubmitDocumentAsync()` | Draft → Submitted |
| Can approve document? | `CanApproveDocumentAsync()` | Manager+ only, Submitted status |
| Can reject document? | `CanRejectDocumentAsync()` | Manager+ only, Submitted status |
| Can delete document? | `CanDeleteDocumentAsync()` | Admin only, not Published |
| Can create task? | `CanCreateTaskAsync()` | Manager only |
| Can assign task? | `CanAssignTaskAsync()` | Manager authority required |
| Is user auditor? | `IsAuditorAsync()` | Read-only access |
| Is user admin? | `IsAdminAsync()` | Full access |
| Can view subordinate? | `CanViewSubordinateAsync()` | Hierarchy check |

## State Machine

```
     Draft (Editable)
       ↓ ↑
    Submit Reject
       ↓   ↑
    Submitted
       ↓
    Approve
       ↓
   Published (Read-only)
```

### Valid State Transitions

```
Draft          → Submitted, Archived, [stays Draft]
Submitted      → Approved, Rejected, Draft
Approved       → Published, Archived
Rejected       → Draft, Archived
Published      → Archived
Archived       → [no transitions]
```

## Logging Events

```csharp
// Document actions
await _auditLoggingService.LogDocumentCreatedAsync(docId, title);
await _auditLoggingService.LogDocumentSubmittedAsync(docId);
await _auditLoggingService.LogDocumentApprovedAsync(docId, comments);
await _auditLoggingService.LogDocumentRejectedAsync(docId, reason);
await _auditLoggingService.LogDocumentEditedAsync(docId);

// Task actions
await _auditLoggingService.LogTaskCreatedAsync(taskId, title, assignedToId);
await _auditLoggingService.LogTaskCompletedAsync(taskId);
await _auditLoggingService.LogTaskRejectedAsync(taskId, reason);

// User actions
await _auditLoggingService.LogUserLoginAsync(userId);
await _auditLoggingService.LogFailedLoginAsync(username);

// Generic action
await _auditLoggingService.LogActionAsync(
    action: "ACTION_TYPE",
    entity: "EntityType",
    entityId: id,
    details: "Additional context",
    success: true
);
```

## Error Responses

### 401 Unauthorized
```csharp
return Unauthorized(new { error = "User not authenticated" });
```
User must log in

### 403 Forbidden
```csharp
return Forbid(); // or new StatusCodeResult(StatusCodes.Status403Forbidden)
```
User lacks permission for action

### 400 Bad Request
```csharp
return BadRequest(new { error = "Document must be in Draft state" });
```
Action violates business logic (wrong state, missing data, etc.)

## Service Injection

All services must be registered in `Program.cs`:

```csharp
builder.Services.AddScoped<IHierarchyService, HierarchyService>();
builder.Services.AddScoped<IAuthorizationService, AuthorizationService>();
builder.Services.AddScoped<IDocumentStateService, DocumentStateService>();
builder.Services.AddScoped<IAuditLoggingService, AuditLoggingService>();
builder.Services.AddScoped<IWorkflowRoutingService, WorkflowRoutingService>();
```

Then inject into controller/page:

```csharp
public MyController(
    IAuthorizationService authService,
    IDocumentStateService stateService,
    IAuditLoggingService auditService)
{
    // ...
}
```

