# KasahQMS Authorization System - Visual Overview

## System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    User Request                                 │
└──────────────────────────────────┬──────────────────────────────┘
                                   │
                    ┌──────────────▼────────────┐
                    │  Authentication Middleware │
                    │  (Cookies / JWT)          │
                    └──────────────┬────────────┘
                                   │
                    ┌──────────────▼──────────────────┐
                    │  Extract User Identity           │
                    │  (CurrentUserService)            │
                    └──────────────┬───────────────────┘
                                   │
        ┌──────────────────────────┴──────────────────────────┐
        │                                                     │
    ┌───▼──────────────────────┐                  ┌──────────▼─────────────┐
    │  Authorization Filter    │                  │  Authorization Service │
    │  (AuthorizeDocument...)  │──────────────────│  (Check Permissions)   │
    └───┬──────────────────────┘                  └──────────┬─────────────┘
        │                                                    │
        │ ┌────────────────────────────────────────────┐    │
        ├─┤  Get User Roles                           │    │
        │ │  Check Role Permissions                   │    │
        │ │  Verify User is not Auditor               │    │
        │ └────────────────────────────────────────────┘    │
        │                                                    │
    ┌───▼──────────────────────┐         ┌─────────────────▼─────┐
    │   Hierarchy Service      │         │  Document State       │
    │   - Get Subordinates     │         │  Service              │
    │   - Check Manager Chain  │         │  - Validate Transition│
    │   - Detect Circles       │         │  - Check Edit Rights  │
    └───┬──────────────────────┘         └─────────────┬─────────┘
        │                                              │
        └──────────────────┬───────────────────────────┘
                           │
                    ┌──────▼──────┐
                    │  AUTHORIZE? │
                    └──────┬──────┘
                           │
            ┌──────────────┴──────────────┐
            │                             │
        ┌───▼────┐                    ┌───▼────┐
        │   YES  │                    │   NO   │
        │ ✅     │                    │ ❌     │
        └───┬────┘                    └───┬────┘
            │                             │
     ┌──────▼──────┐            ┌────────▼─────────┐
     │  Execute    │            │ Return Error      │
     │  Action     │            │ - 403 Forbidden   │
     │             │            │ - 400 Bad Request │
     └──────┬──────┘            └───────────────────┘
            │
    ┌───────▼─────────┐
    │ Audit Logger    │
    │ - Log Action    │
    │ - Log Success   │
    │ - Log IP/Time   │
    └───────┬─────────┘
            │
    ┌───────▼──────────────┐
    │  Response to Client  │
    └─────────────────────┘
```

## Role-Based Access Control Matrix

```
┌──────────────────┬──────────┬──────────┬────────┬─────────┬────────────┬───────────┐
│      Role        │ Auditor  │  Staff   │Manager │Dept Mgr │Deputy Mgr  │   TMD     │
├──────────────────┼──────────┼──────────┼────────┼─────────┼────────────┼───────────┤
│ Create Document  │    ❌    │    ✅    │   ✅   │   ✅    │     ✅     │    ✅     │
│ Edit Document    │    ❌    │   ✅*    │   ✅   │   ✅    │     ✅     │    ✅     │
│ Submit Document  │    ❌    │   ✅*    │   ✅   │   ✅    │     ✅     │    ✅     │
│ Approve Document │    ❌    │    ❌    │   ✅   │   ✅    │     ✅     │    ✅     │
│ Reject Document  │    ❌    │    ❌    │   ✅   │   ✅    │     ✅     │    ✅     │
│ Delete Document  │    ❌    │    ❌    │   ❌   │   ❌    │     ❌     │    ✅     │
│ Create Task      │    ❌    │    ❌    │   ✅   │   ✅    │     ✅     │    ✅     │
│ Assign Task      │    ❌    │    ❌    │  ✅**  │  ✅**   │     ✅     │    ✅     │
│ View All Data    │    ✅ RO │    ❌    │  ✅**  │  ✅**   │     ✅     │    ✅     │
│ Export Data      │    ✅    │    ❌    │   ❌   │   ❌    │     ❌     │    ❌     │
└──────────────────┴──────────┴──────────┴────────┴─────────┴────────────┴───────────┘

Legend:
✅ = Allowed
❌ = Blocked
RO = Read-Only
*  = Own department only
** = Subordinates only
```

## Document Workflow State Machine

```
                    ┌─────────────────────────┐
                    │         DRAFT           │
                    │ (Editable)              │
                    │ • Creator can edit      │
                    │ • Admin can edit        │
                    │ • Auditor cannot edit   │
                    └────────────┬────────────┘
                                 │
                   ┌─────────────┴──────────────┐
                   │                            │
              SUBMIT                       ARCHIVE
                   │                            │
                   ▼                            ▼
            ┌─────────────────┐        ┌──────────────┐
            │    SUBMITTED    │        │   ARCHIVED   │
            │ (Under Review)  │        │ (Read-only)  │
            │ • Manager views │        └──────────────┘
            │ • Cannot edit   │
            └────────┬────────┘
                     │
        ┌────────────┴────────────┐
        │                         │
      APPROVE                  REJECT
        │                         │
        ▼                         ▼
    ┌─────────┐          ┌──────────────────┐
    │APPROVED │          │      DRAFT       │
    │         │          │ (Back to Draft)  │
    └────┬────┘          │ • Reason logged  │
         │               └──────────────────┘
       PUBLISH
         │
         ▼
    ┌──────────────┐
    │  PUBLISHED   │
    │ (Read-only)  │
    │ • No editing │
    │ • Final copy │
    └──────────────┘
```

## Authorization Decision Tree

```
                    ┌─ User Request ─┐
                    │   Operation    │
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │ Is User         │
                    │ Authenticated?  │
                    └────┬──────────┬─┘
                       NO│          │YES
                        ▼          │
                    ┌─────────┐    │
                    │ 401     │    │
                    │UNAUTH  │    │
                    └─────────┘    │
                                   ▼
                        ┌──────────────────┐
                        │ Is User an       │
                        │ Auditor?         │
                        └────┬──────────┬──┘
                           YES│         │NO
                            ▼ │        │
                      ┌────────┐      │
                      │ Check  │      │
                      │READ-   │      │
                      │ONLY    │      │
                      │PERMITTED│      │
                      └────┬───┘      │
                       NO │ │ YES     │
                          │ └─┐      │
                          │   │      ▼
                          │   │   ┌──────────────────┐
                          │   │   │ Check Role for   │
                          │   │   │ Operation        │
                          │   │   └──┬───────────┬──┘
                          │   │    NO│           │YES
                          │   │     ▼           │
                          │   │  ┌──────┐      │
                          │   │  │ 403  │      │
                          │   │  │FORBID│      │
                          │   │  └──────┘      │
                          │   │               ▼
                          │   │            ┌────────────────┐
                          │   │            │ Check Document │
                          │   │            │ State          │
                          │   │            └──┬──────────┬──┘
                          │   │             NO│          │YES
                          │   │              ▼           │
                          │   │           ┌───────┐     │
                          │   │           │ 400   │     │
                          │   │           │BAD REQ│     │
                          │   │           └───────┘     │
                          │   │                        ▼
                          │   │                    ┌─────────┐
                          │   │                    │ EXECUTE │
                          │   │                    │ ACTION  │
                          │   │                    └────┬────┘
                          │   │                         │
                          │   │                    ┌────▼────┐
                          │   │                    │ LOG     │
                          │   │                    │ AUDIT   │
                          │   │                    └────┬────┘
                          │   │                         │
                          └───┴─────────────────────────┼──►RESPONSE
```

## Service Dependency Injection

```
┌──────────────────────────────────────────────────────────────┐
│                     Program.cs                              │
│                  (Service Registration)                     │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  // Core Framework Services                                │
│  • AddApplicationLayer()                                   │
│  • AddPersistenceServices()                                │
│  • AddInfrastructureServices()                             │
│  • AddHttpContextAccessor()                                │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐│
│  │ NEW Authorization & Security Services                ││
│  ├────────────────────────────────────────────────────────┤│
│  │                                                        ││
│  │  📦 IHierarchyService                                ││
│  │     └─→ HierarchyService                             ││
│  │         • Manager-subordinate relationships          ││
│  │         • Recursive hierarchy traversal              ││
│  │         • Circular reference detection               ││
│  │                                                        ││
│  │  📦 IAuthorizationService                            ││
│  │     └─→ AuthorizationService                         ││
│  │         • Permission checking                        ││
│  │         • Role validation                            ││
│  │         • Hierarchy-based access control             ││
│  │                                                        ││
│  │  📦 IDocumentStateService                            ││
│  │     └─→ DocumentStateService                         ││
│  │         • State machine enforcement                  ││
│  │         • Workflow validation                        ││
│  │         • State transition rules                     ││
│  │                                                        ││
│  │  📦 IAuditLoggingService                             ││
│  │     └─→ AuditLoggingService                          ││
│  │         • Action logging                             ││
│  │         • Audit trail creation                       ││
│  │         • Compliance reporting                       ││
│  │                                                        ││
│  │  📦 IWorkflowRoutingService                          ││
│  │     └─→ WorkflowRoutingService                       ││
│  │         • Document routing                           ││
│  │         • Approval workflows                         ││
│  │         • Task auto-generation                       ││
│  │                                                        ││
│  └────────────────────────────────────────────────────────┘│
│                                                              │
└──────────────────────────────────────────────────────────────┘
                           │
                           │ Injected Into
                           ▼
        ┌──────────────────────────────┐
        │   Controllers & Pages        │
        ├──────────────────────────────┤
        │ • DocumentsController        │
        │ • DocumentsCreatePage        │
        │ • TasksCreatePage            │
        │ • AuthorizedPageModel        │
        │ • Other endpoints            │
        └──────────────────────────────┘
```

## Request Flow - Document Creation Example

```
User (TMD)              Frontend                API                Backend Services
    │                      │                    │                        │
    ├──"Create Document"──►│                    │                        │
    │                      │                    │                        │
    │                      ├──POST /api/documents├────────────────────►│
    │                      │                    │                   │
    │                      │                    │   [Authenticate]   │
    │                      │                    │◄──────────────────┤
    │                      │                    │   UserId found ✅  │
    │                      │                    │                   │
    │                      │                    │   [Authorize]     │
    │                      │                    │   TMD ? ✅ Can    │
    │                      │                    │◄──────────────────┤
    │                      │                    │                   │
    │                      │                    │   [Create Cmd]    │
    │                      │                    │◄──────────────────┤
    │                      │                    │   Insert Document │
    │                      │                    │                   │
    │                      │                    │   [Audit Log]     │
    │                      │                    │◄──────────────────┤
    │                      │                    │   DOCUMENT_CREATED│
    │                      │                    │                   │
    │                      │  ✅ 201 Created   │                   │
    │                      │◄──────────────────┤                    │
    │  ✅ Document ID     │                    │                   │
    │◄─────────────────────┤                    │                   │
    │                      │                    │                   │

User (Auditor)          Frontend                API                Backend Services
    │                      │                    │                        │
    ├──"Create Document"──►│                    │                        │
    │                      │                    │                        │
    │                      ├──POST /api/documents├────────────────────►│
    │                      │                    │                   │
    │                      │                    │   [Authenticate]   │
    │                      │                    │   UserId found ✅  │
    │                      │                    │◄──────────────────┤
    │                      │                    │                   │
    │                      │                    │   [Authorize]     │
    │                      │                    │   Auditor? ❌     │
    │                      │                    │◄──────────────────┤
    │                      │ ❌ 403 Forbidden  │                   │
    │                      │◄──────────────────┤                   │
    │  ❌ Auditors cannot  │                    │                   │
    │  create documents    │                    │                   │
    │◄─────────────────────┤                    │                   │
```

## Error Flow - Attempted Unauthorized Action

```
Request
    │
    ├─ Authentication Check
    │  └─ Valid? → Continue
    │     Invalid? → 401 Unauthorized
    │
    ├─ Authorization Check
    │  └─ Allowed? → Continue
    │     Blocked? → 403 Forbidden
    │        ├─ Log: DENIED_ACTION
    │        ├─ Include: User ID, Action, Reason
    │        └─ Return: Error response
    │
    ├─ State Validation
    │  └─ Valid? → Continue
    │     Invalid? → 400 Bad Request
    │        ├─ Log: INVALID_STATE
    │        ├─ Include: Current state, Required state
    │        └─ Return: Error response
    │
    └─ Execute Action
       └─ Success? → 200 OK + Log: SUCCESS_ACTION
          Failure? → 400 Bad Request + Log: FAILED_ACTION
```

## File Organization

```
KasahQMS.Web/
├── Services/
│   ├── AuthorizationService.cs          ✨ NEW
│   ├── HierarchyService.cs              ✨ NEW
│   ├── DocumentStateService.cs          ✨ NEW
│   ├── AuditLoggingService.cs           ✨ NEW
│   ├── WorkflowRoutingService.cs        ✨ NEW
│   ├── CurrentUserService.cs
│   └── DashboardRoutingService.cs
├── Filters/
│   └── AuthorizationFilters.cs          ✨ NEW
├── Pages/
│   ├── AuthorizedPageModel.cs           ✨ NEW
│   ├── Documents/
│   │   └── Create.cshtml.cs             ✅ UPDATED
│   └── Tasks/
│       └── Create.cshtml.cs             ✅ UPDATED
├── Controllers/
│   ├── DocumentsController.cs           ✅ UPDATED
│   └── ...
├── Program.cs                            ✅ UPDATED
│
└── Documentation/
    ├── AUTHORIZATION_FIXES_SUMMARY.md   ✨ NEW
    ├── AUTHORIZATION_GUIDE.md           ✨ NEW
    ├── AUTHORIZATION_QUICK_REFERENCE.md ✨ NEW
    └── IMPLEMENTATION_CHECKLIST.md      ✨ NEW
```

## Key Metrics

```
Authorization Checks:        5 major services
Permission Rules:            20+ authorization rules
Supported Roles:             6+ role types
Audit Events:                10+ event types
State Transitions:           12+ valid transitions
Documentation Pages:         4 comprehensive guides
```

