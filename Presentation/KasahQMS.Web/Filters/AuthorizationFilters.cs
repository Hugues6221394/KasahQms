using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Enums;
using KasahQMS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace KasahQMS.Web.Filters;

/// <summary>
/// Attribute to authorize document operations (create, edit, approve, reject, delete).
/// Can be used on controller actions and page handlers.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class AuthorizeDocumentOperationAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _operation; // "Create", "Edit", "Approve", "Reject", "Delete", "View"

    public AuthorizeDocumentOperationAttribute(string operation)
    {
        _operation = operation;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var authService = context.HttpContext.RequestServices.GetService<IAuthorizationService>();
        var currentUserService = context.HttpContext.RequestServices.GetService<ICurrentUserService>();
        var logger = context.HttpContext.RequestServices.GetService<ILogger<AuthorizeDocumentOperationAttribute>>();

        if (authService == null || currentUserService == null)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            return;
        }

        var userId = currentUserService.UserId;
        if (userId == null)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status401Unauthorized);
            return;
        }

        // Get document ID from route or query parameters
        var documentId = context.RouteData.Values["id"] as string ??
                        context.HttpContext.Request.Query["id"].ToString();

        if (!Guid.TryParse(documentId, out var docId))
        {
            logger?.LogWarning("Invalid document ID format: {DocumentId}", documentId);
            context.Result = new StatusCodeResult(StatusCodes.Status400BadRequest);
            return;
        }

        var isAuthorized = _operation switch
        {
            "Create" => await authService.CanCreateDocumentAsync(userId.Value),
            "Edit" => await authService.CanEditDocumentAsync(userId.Value, docId),
            "Approve" => await authService.CanApproveDocumentAsync(userId.Value, docId),
            "Reject" => await authService.CanRejectDocumentAsync(userId.Value, docId),
            "Delete" => await authService.CanDeleteDocumentAsync(userId.Value, docId),
            "View" => await authService.CanViewDocumentAsync(userId.Value, docId),
            _ => false
        };

        if (!isAuthorized)
        {
            logger?.LogWarning(
                "User {UserId} unauthorized to {Operation} document {DocumentId}",
                userId, _operation, docId);
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

        await next();
    }
}

/// <summary>
/// Attribute to authorize task operations (create, assign, complete).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class AuthorizeTaskOperationAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _operation; // "Create", "Assign", "Complete", "View"

    public AuthorizeTaskOperationAttribute(string operation)
    {
        _operation = operation;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var authService = context.HttpContext.RequestServices.GetService<IAuthorizationService>();
        var currentUserService = context.HttpContext.RequestServices.GetService<ICurrentUserService>();
        var logger = context.HttpContext.RequestServices.GetService<ILogger<AuthorizeTaskOperationAttribute>>();

        if (authService == null || currentUserService == null)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            return;
        }

        var userId = currentUserService.UserId;
        if (userId == null)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status401Unauthorized);
            return;
        }

        bool isAuthorized = _operation switch
        {
            "Create" => await authService.CanCreateTaskAsync(userId.Value),
            "View" => await authService.CanViewTaskAsync(userId.Value, GetTaskIdFromContext(context)),
            _ => false
        };

        if (!isAuthorized)
        {
            logger?.LogWarning("User {UserId} unauthorized to {Operation} task", userId, _operation);
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

        await next();
    }

    private static Guid GetTaskIdFromContext(ActionExecutingContext context)
    {
        var taskId = context.RouteData.Values["id"] as string ??
                    context.HttpContext.Request.Query["id"].ToString();
        return Guid.TryParse(taskId, out var id) ? id : Guid.Empty;
    }
}

/// <summary>
/// Attribute to validate document state for operations.
/// Ensures document is in correct state (e.g., Draft for edit, Submitted for approve).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ValidateDocumentStateAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _requiredState; // "Draft", "Submitted", "Published"

    public ValidateDocumentStateAttribute(string requiredState)
    {
        _requiredState = requiredState;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var stateService = context.HttpContext.RequestServices.GetService<IDocumentStateService>();
        var logger = context.HttpContext.RequestServices.GetService<ILogger<ValidateDocumentStateAttribute>>();

        if (stateService == null)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            return;
        }

        var documentId = context.RouteData.Values["id"] as string ??
                        context.HttpContext.Request.Query["id"].ToString();

        if (!Guid.TryParse(documentId, out var docId))
        {
            logger?.LogWarning("Invalid document ID: {DocumentId}", documentId);
            context.Result = new StatusCodeResult(StatusCodes.Status400BadRequest);
            return;
        }

        var currentState = await stateService.GetCurrentStateAsync(docId);
        if (currentState == null)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status404NotFound);
            return;
        }

        var isValidState = _requiredState switch
        {
            "Draft" => currentState == DocumentStatus.Draft,
            "Submitted" => currentState == DocumentStatus.Submitted,
            "Approved" => currentState == DocumentStatus.Approved,
            _ => false
        };

        if (!isValidState)
        {
            logger?.LogWarning(
                "Document {DocumentId} is in {CurrentState}, but {RequiredState} is required",
                docId, currentState, _requiredState);
            context.Result = new BadRequestObjectResult(
                new { error = $"Document must be in {_requiredState} state for this operation" });
            return;
        }

        await next();
    }
}

/// <summary>
/// Attribute to require hierarchical access (user can view subordinate).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class RequireHierarchicalAccessAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var hierarchyService = context.HttpContext.RequestServices.GetService<IHierarchyService>();
        var currentUserService = context.HttpContext.RequestServices.GetService<ICurrentUserService>();
        var logger = context.HttpContext.RequestServices.GetService<ILogger<RequireHierarchicalAccessAttribute>>();

        if (hierarchyService == null || currentUserService == null)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            return;
        }

        var userId = currentUserService.UserId;
        if (userId == null)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status401Unauthorized);
            return;
        }

        // Get target user ID from route
        var targetUserId = context.RouteData.Values["targetUserId"] as string ??
                          context.HttpContext.Request.Query["targetUserId"].ToString();

        if (!Guid.TryParse(targetUserId, out var targetId))
        {
            logger?.LogWarning("Invalid target user ID: {TargetUserId}", targetUserId);
            context.Result = new StatusCodeResult(StatusCodes.Status400BadRequest);
            return;
        }

        var isAuthorized = await hierarchyService.IsSubordinateAsync(userId.Value, targetId) || userId == targetId;

        if (!isAuthorized)
        {
            logger?.LogWarning(
                "User {UserId} does not have hierarchical access to {TargetUserId}",
                userId, targetId);
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

        await next();
    }
}
