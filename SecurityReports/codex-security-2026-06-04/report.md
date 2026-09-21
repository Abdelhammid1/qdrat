# Security Review: QdratNew

## Scope

- Target: `D:\web\site2025\QdratNew`
- Date: 2026-06-04
- Mode: report-only production security review.
- Reviewed code areas: ASP.NET Core startup/configuration, Identity/authentication, Partner area, Student area, selected Admin controllers, middleware, file upload/static file handling, raw SQL usage, Razor raw HTML usage, and NuGet vulnerability metadata.
- Excluded from source review: `bin`, `obj`, `node_modules`, generated build outputs, and full dynamic penetration testing against a live production instance.
- Limitation: the Codex Security exhaustive workflow recommends explicit subagent authorization for full repository-wide coverage. This report was produced by a parent-agent static review plus targeted local commands. It is sufficient for a production go/no-go decision, but not a substitute for a full multi-agent exhaustive audit.

### Scan Summary

| Field | Value |
|---|---|
| Production decision | **NO-GO until P0/P1 issues are fixed and secrets are rotated** |
| Reportable findings | 9 |
| Severity mix | 2 Critical, 4 High, 3 Medium |
| Highest business risk | Unauthorized partner data access and hardcoded production database credentials |
| Validation mode | Static code tracing plus NuGet advisory check |
| Dependency check | `dotnet list QdratNew.csproj package --vulnerable --include-transitive` |

## Threat Model

QdratNew is an educational ASP.NET Core MVC system with Admin, Partner, Instructor, Student, Public, analytics, homework, exam, notification, AI, and reporting workflows.

Primary assets:

- Student PII: names, national IDs, attendance, exam results, homework results, progress, course/batch enrollment.
- Partner tenant data: partner students, batches, subscriptions, exams, homework drafts, reports.
- Admin control plane: user management, permissions, impersonation, question bank, analytics, background jobs.
- Production SQL Server data and credentials.
- Exam and homework integrity.

Trust boundaries:

- Anonymous internet user to public MVC routes.
- Authenticated student to own student data.
- Partner user to only their assigned partner tenant.
- Instructor to only assigned batches/courses/students.
- Admin roles and custom permissions to privileged management functions.
- Background jobs/Hangfire to privileged operational actions.
- Uploaded files to static web origin.

Security invariants:

- No partner, student, instructor, or admin data should be accessible without authentication and correct object ownership.
- Tenant selection must be bound to the authenticated user, not only to a session value.
- Production secrets must not be stored in repository configuration.
- Admin and background job surfaces must require explicit authorization.
- Student identity must come from the authenticated user, never from attacker-supplied IDs.
- State-changing POST actions must use anti-forgery unless protected by a deliberate alternate CSRF design.

## Findings

| # | Title | Severity | Confidence |
|---|---|---|---|
| 1 | Partner tenant can be selected without authentication | Critical | High |
| 2 | Production Azure SQL credentials are committed in config | Critical | High |
| 3 | Hangfire dashboard is mapped without explicit authorization | High | Medium |
| 4 | Student study-session workflows are unauthenticated and object IDs are trusted | High | High |
| 5 | Student AI recommendations expose arbitrary student performance by `studentId` | High | High |
| 6 | Generic unauthenticated PDF rendering endpoint accepts arbitrary view names | High | Medium |
| 7 | Impersonation cookie is not marked Secure in production code | Medium | High |
| 8 | Identity password and session policy are weak for production | Medium | High |
| 9 | Vulnerable transitive NuGet packages are present | Medium | High |

### Confidence Scale

| Label | Meaning |
|---|---|
| High | Direct source, configuration, or local tool evidence supports the finding. |
| Medium | Source evidence supports a plausible issue, but runtime behavior or deployment exposure still needs proof. |
| Low | Weak or incomplete evidence; not used for final reportable findings here. |

### [1] Partner tenant can be selected without authentication

| Field | Value |
|---|---|
| Severity | critical |
| Confidence | high |
| Category | Authorization bypass / cross-tenant access |
| CWE | CWE-862 Missing Authorization, CWE-639 IDOR |
| Affected lines | `Areas\Partner\Controllers\PartnerContextController.cs:45`, `Areas\Partner\Controllers\PartnerContextController.cs:51`, `Areas\Partner\Controllers\PartnerBaseController.cs:18`, `Areas\Partner\Controllers\PartnerBaseController.cs:79`, `Areas\Partner\Controllers\StudentsController.cs:42`, `Areas\Partner\Controllers\StudentsController.cs:46` |

#### Summary

`PartnerContextController.Set(int partnerId)` accepts a partner ID, only checks that the partner exists, then stores it in `HttpContext.Session` as `ActivePartnerId`. The controller has no `[Authorize]`, no role policy, and no anti-forgery token. `PartnerBaseController` then trusts this session value for partner scoping. Several Partner controllers have no `[Authorize]` and inherit this base controller.

#### Validation

- [x] Attacker-controlled input: `partnerId` in POST to PartnerContext Set.
- [x] Broken control: no authentication/authorization binding between current user and partner.
- [x] Sink: partner-scoped queries use `ActivePartnerId`.
- [x] Impact: partner student/batch/homework/exam surfaces become reachable for the selected tenant.
- [ ] Runtime HTTP proof was not executed against a live app/database.

Evidence:

- `PartnerContextController.cs:45`: `public IActionResult Set(int partnerId)`
- `PartnerContextController.cs:47`: checks only `_context.Partners.Any(p => p.Id == partnerId)`
- `PartnerContextController.cs:51`: `HttpContext.Session.SetInt32("ActivePartnerId", partnerId)`
- `PartnerBaseController.cs:18-19`: reads `ActivePartnerId` only from session
- `StudentsController.cs:42-46`: returns students where `Branch.PartnerId == ActivePartnerId`

#### Dataflow

Request `POST /Partner/PartnerContext/Set?partnerId=N` -> `Set(int partnerId)` -> session `ActivePartnerId=N` -> unauthenticated Partner controller inheriting `PartnerBaseController` -> database query filtered by attacker-selected partner ID -> response with partner data or state-changing workflow.

#### Reachability

An unauthenticated or unauthorized user who can reach the Partner routes can set a session partner ID if they know or guess a valid integer ID. Partner IDs are usually enumerable. This crosses tenant boundaries and can expose PII and operational data.

#### Severity

Critical because the path is near-unauthenticated, low-friction, cross-tenant, and affects partner/student data. If deployment has an external gateway that blocks all `/Partner/*` routes before ASP.NET Core, severity could lower; no repository evidence showed such a gateway.

#### Remediation

- Add `[Authorize(Policy = "PartnerOnly")]` to `PartnerBaseController` or a global Partner-area convention.
- In `Set`, require authenticated user and verify `_context.UserPartners.Any(up => up.UserId == userId && up.PartnerId == partnerId)`.
- Add `[ValidateAntiForgeryToken]` to `Set`.
- Add integration tests proving a user cannot select another partner or access Partner controllers anonymously.

### [2] Production Azure SQL credentials are committed in config

| Field | Value |
|---|---|
| Severity | critical |
| Confidence | high |
| Category | Hardcoded production secret |
| CWE | CWE-798 Use of Hard-coded Credentials |
| Affected lines | `appsettings.Production.json:3` |

#### Summary

`appsettings.Production.json` contains the production Azure SQL Server host, database name, username, and plaintext password. This is an immediate credential exposure risk in source control, local machines, backups, build artifacts, and any copied release package.

#### Validation

- [x] Secret-like value is present in repository config.
- [x] It is labeled production.
- [x] It targets Azure SQL over TCP.
- [x] It includes a password.
- [ ] Validity against the live database was not tested, appropriately.

#### Dataflow

Repository/config access -> plaintext database credential -> direct database login attempt or reuse in deployed app -> production data compromise.

#### Reachability

Anyone with repository access, build artifact access, deployment package access, or leaked config can obtain the credential. Because the configured DB is production, this must be treated as exposed.

#### Severity

Critical because it can lead directly to production database compromise and mass PII exposure. Severity only lowers after the credential is rotated and verified invalid.

#### Remediation

- Rotate the Azure SQL password immediately.
- Remove secrets from `appsettings.Production.json`.
- Use Azure App Service configuration, Key Vault, managed identity, or environment variables.
- Search git history and deployment artifacts for the exposed value.
- Add secret scanning in CI and block future commits.

### [3] Hangfire dashboard is mapped without explicit authorization

| Field | Value |
|---|---|
| Severity | high |
| Confidence | medium |
| Category | Missing authorization on operational dashboard |
| CWE | CWE-862 Missing Authorization |
| Affected lines | `Program.cs:914` |

#### Summary

`app.UseHangfireDashboard();` is registered without visible `DashboardOptions` authorization filters or route restriction. Hangfire dashboards can expose job names, arguments, failures, operational metadata, and sometimes job-trigger controls depending on configuration.

#### Validation

- [x] Dashboard is mapped in production middleware.
- [x] No explicit authorization filter is shown at the registration site.
- [x] App has recurring privileged jobs.
- [ ] Runtime access behavior was not tested; Hangfire may apply local-only restrictions by default depending on version and hosting.

#### Dataflow

HTTP request to Hangfire dashboard route -> dashboard middleware -> job metadata/control surface.

#### Reachability

If the dashboard route is externally reachable, an attacker may view operational data or interact with job controls. The route is mapped after authentication/authorization middleware, but middleware itself needs its own dashboard authorization filter.

#### Severity

High with medium confidence because the code clearly lacks explicit production authorization, while exact runtime exposure depends on Hangfire defaults and hosting. Confirming external access would raise confidence.

#### Remediation

- Map dashboard at a deliberate route such as `/admin/jobs`.
- Add a Hangfire authorization filter requiring SuperAdmin/Owner/Developer.
- Disable dashboard in production unless explicitly enabled.
- Add smoke test: anonymous request to dashboard returns 401/403.

### [4] Student study-session workflows are unauthenticated and object IDs are trusted

| Field | Value |
|---|---|
| Severity | high |
| Confidence | high |
| Category | IDOR / missing authorization |
| CWE | CWE-862 Missing Authorization, CWE-639 IDOR |
| Affected lines | `Areas\Students\Controllers\StudySessionsController.cs:12`, `Areas\Students\Controllers\StudySessionsController.cs:50`, `Areas\Students\Controllers\StudySessionsController.cs:89`, `Areas\Students\Controllers\StudySessionsController.cs:132`, `Areas\Students\Controllers\StudySessionsController.cs:148`, `Areas\Students\Controllers\SessionRatingsController.cs:22`, `Areas\Students\Controllers\SessionRatingsController.cs:73` |

#### Summary

`StudySessionsController` has no `[Authorize]` and directly loads sessions by ID for details/edit/update flows. It also uses a hardcoded `studentId = 3` in list/dashboard paths. `SessionRatingsController` has no `[Authorize]`, reads any session by `sessionId`, and trusts `model.StudentId` when creating ratings.

#### Validation

- [x] No controller-level authorization attribute is present.
- [x] Object IDs are accepted directly.
- [x] `FindAsync(id)` and `FirstOrDefaultAsync(s => s.Id == sessionId)` do not enforce ownership.
- [x] POST update and rating creation mutate data.
- [ ] Runtime route proof was not executed.

#### Dataflow

Request with `id` or `sessionId` -> controller loads reservation by ID without ownership -> returns details or updates reservation/rating.

#### Reachability

Any user who can reach the route can attempt numeric IDs. Even if some actions later fail because of model validation, the code shows missing ownership checks and unauthenticated entrypoints.

#### Severity

High because it exposes or modifies student session reservations and ratings, which are student-specific records. If these routes are unused or blocked by routing in production, severity could lower; repository routes indicate normal MVC area routing is active.

#### Remediation

- Inherit from `StudentBaseController` or add `[Authorize(Roles = "Student")]`.
- Resolve `StudentId` from the authenticated user for every action.
- Add `WHERE reservation.StudentID == currentStudentId` to every details/edit/delete/rating query.
- Do not accept `StudentId` from posted view models for student-owned actions.

### [5] Student AI recommendations expose arbitrary student performance by `studentId`

| Field | Value |
|---|---|
| Severity | high |
| Confidence | high |
| Category | IDOR / sensitive data exposure |
| CWE | CWE-639 Authorization Bypass Through User-Controlled Key |
| Affected lines | `Areas\Students\Controllers\AIRecommendationController.cs:10`, `Areas\Students\Controllers\AIRecommendationController.cs:19`, `Areas\Students\Controllers\AIRecommendationController.cs:21` |

#### Summary

`AIRecommendationController.Index(int studentId)` has no `[Authorize]` and queries `StudentPerformances` using attacker-controlled `studentId`. This can expose academic performance and recommendations for arbitrary students.

#### Validation

- [x] No authorization attribute is present.
- [x] `studentId` comes from route/query binding.
- [x] Query uses that ID directly.
- [x] Data is sensitive student performance data.

#### Dataflow

Request `studentId` -> `StudentPerformances.Where(sp => sp.StudentID == studentId)` -> recommendation view.

#### Reachability

An external user can enumerate student IDs if the route is exposed. Even an authenticated student should not be able to request another student's performance.

#### Severity

High because it is a straightforward IDOR over sensitive student performance records. Severity would lower only if route is unreachable in production or protected by external middleware not visible in the repo.

#### Remediation

- Add `[Authorize(Roles = "Student")]`.
- Remove `studentId` parameter for student self-service; resolve from `UserManager`.
- For admin use, create a separate admin route protected by admin permission.
- Add tests for anonymous and cross-student access.

### [6] Generic unauthenticated PDF rendering endpoint accepts arbitrary view names

| Field | Value |
|---|---|
| Severity | high |
| Confidence | medium |
| Category | Missing authorization / unsafe generic rendering |
| CWE | CWE-862 Missing Authorization |
| Affected lines | `Controllers\PrintController.cs:7`, `Controllers\PrintController.cs:11`, `Controllers\PrintController.cs:15` |

#### Summary

`PrintController.ViewAsPdf` is unauthenticated and accepts `viewName`, `area`, `controller`, and `id` as user input, then passes them to Rotativa `ViewAsPdf`. This is a risky generic rendering primitive that may bypass intended report authorization or render internal views in unexpected contexts.

#### Validation

- [x] Controller has no `[Authorize]`.
- [x] View and route values are attacker-controlled.
- [x] PDF/reporting is a sensitive workflow in this product.
- [ ] Exact view rendering behavior and data availability require runtime proof.

#### Dataflow

Request parameters -> route values and view name -> Rotativa rendering -> PDF response.

#### Reachability

If exposed, any user can request the endpoint. Impact depends on which views can be rendered without required model data and whether protected actions are invoked by Rotativa.

#### Severity

High with medium confidence because generic server-side rendering endpoints frequently become authorization bypasses for reports. Runtime testing could raise or lower severity.

#### Remediation

- Remove generic PDF route.
- Replace with explicit report actions, each protected by the same authorization as the underlying report.
- Validate report IDs against current user's scope.
- Add a deny-by-default allowlist if generic rendering must remain.

### [7] Impersonation cookie is not marked Secure in production code

| Field | Value |
|---|---|
| Severity | medium |
| Confidence | high |
| Category | Cookie security misconfiguration |
| CWE | CWE-614 Sensitive Cookie in HTTPS Session Without Secure Attribute |
| Affected lines | `Services\ImpersonationService.cs:33`, `Services\ImpersonationService.cs:35`, `Services\ImpersonationService.cs:36`, `Services\ImpersonationService.cs:38` |

#### Summary

The impersonation state cookie is protected and HttpOnly, but `Secure = false` is hardcoded with a comment saying true in production. This cookie affects which identity `ImpersonationMiddleware` installs into `HttpContext.User`.

#### Validation

- [x] Cookie stores impersonation state.
- [x] Data is protected with ASP.NET Data Protection.
- [x] `HttpOnly` is set.
- [x] `Secure` is explicitly false.

#### Dataflow

Privileged admin starts impersonation -> encrypted cookie issued without Secure flag -> browser may send over HTTP if reachable -> middleware replaces principal from cookie.

#### Reachability

If any HTTP endpoint or proxy downgrade path exists, the cookie can be transmitted without HTTPS-only protection. HSTS/HTTPS redirection reduce but do not remove the need for the Secure flag.

#### Severity

Medium because the cookie is encrypted and requires privileged creation, but the security flag is wrong for a sensitive identity-switching feature.

#### Remediation

- Set `Secure = true` or `CookieSecurePolicy.Always`.
- Consider `SameSite=Strict` for impersonation if workflows allow.
- Add expiration and audit metadata.
- Add automated check for sensitive cookies.

### [8] Identity password and session policy are weak for production

| Field | Value |
|---|---|
| Severity | medium |
| Confidence | high |
| Category | Authentication hardening |
| CWE | CWE-521 Weak Password Requirements |
| Affected lines | `Program.cs:146`, `Program.cs:148`, `Program.cs:149`, `Program.cs:150`, `Program.cs:151`, `Program.cs:152`, `Program.cs:636` |

#### Summary

Identity configuration disables digit, lowercase, uppercase, and non-alphanumeric password requirements and allows length 6. Confirmed account is not required. Session idle timeout is 6 hours. This increases account compromise risk for a production educational system with PII and admin dashboards.

#### Validation

- [x] Weak password settings are configured in startup.
- [x] Session timeout is long.
- [x] 2FA pages exist, but enforcement for privileged roles was not found in startup.

#### Dataflow

Weak credentials -> successful login/brute force/credential stuffing -> access to student, partner, or admin data depending on role.

#### Reachability

Internet-exposed login pages are common in this app. `Login.cshtml.cs` also uses `lockoutOnFailure: false` based on local search output, which should be reviewed.

#### Severity

Medium because it is a hardening weakness, not a direct bypass. It becomes high if admin accounts use weak passwords or no MFA.

#### Remediation

- Require stronger passwords, lockout on failure, and confirmed email/account where appropriate.
- Enforce MFA for `SuperAdmin`, `Owner`, `Developer`, and partner admin roles.
- Shorten sensitive-session lifetime or use sliding expiration carefully.
- Add login monitoring and alerting.

### [9] Vulnerable transitive NuGet packages are present

| Field | Value |
|---|---|
| Severity | medium |
| Confidence | high |
| Category | Vulnerable dependency |
| CWE | CWE-1104 Use of Unmaintained Third Party Components |
| Affected lines | `QdratNew.csproj` package graph |

#### Summary

NuGet vulnerability audit reported vulnerable transitive packages. High severity advisories include `Microsoft.Build 17.8.3`, `Npgsql 8.0.0`, `System.Data.SqlClient 4.4.0`, and `System.IO.Packaging 6.0.0`. The project uses packages such as EPPlus/ClosedXML/OpenXML/PDF tooling and database clients, so dependency review matters.

#### Validation

- [x] Command succeeded against NuGet: `dotnet list QdratNew.csproj package --vulnerable --include-transitive`.
- [x] Multiple high/moderate advisories were returned.
- [ ] Reachability of each CVE through project workflows was not individually proven.

#### Dataflow

Application feature imports/exports documents or uses database/build-related libraries -> vulnerable package code may be reachable -> impact depends on advisory class and input path.

#### Reachability

Document and Excel workflows exist in the project. Database client packages are also present. Exact vulnerable call paths require follow-up per advisory.

#### Severity

Medium for the project-level report because package presence is confirmed but exploitability per advisory is not. Individual advisories may become High if reachable from uploaded Excel/doc/PDF or server-side processing.

#### Remediation

- Run `dotnet list package --vulnerable --include-transitive` in CI.
- Upgrade direct packages that pull vulnerable transitive dependencies.
- Specifically review Excel/import/document/PDF workflows after updates.
- Add regression tests for file import paths.

## Reviewed Surfaces

| Surface | Risk Area | Outcome | Notes |
|---|---|---|---|
| ASP.NET Core startup | Middleware/auth/session/Hangfire | Reported | HSTS/HTTPS present; Hangfire dashboard lacks explicit auth. |
| Production config | Secrets | Reported | Production SQL credential is committed. |
| Partner area | Cross-tenant authorization | Reported | Session-only tenant selection and missing auth on multiple controllers. |
| Student study sessions | IDOR/auth | Reported | Missing auth and ownership checks. |
| Student performance AI | IDOR | Reported | Attacker-controlled `studentId`. |
| Admin permissions | Custom authorization | No issue found in sampled handler | `AdminPermissionAttribute` extends `AuthorizeAttribute`; handler checks profile permissions. |
| Raw SQL in sampled admin users flow | SQL injection | Rejected | Sampled raw SQL uses `SqlParameter`; no injection finding promoted. |
| File upload/static uploads | Upload abuse | Needs follow-up | Extension checks exist in sampled profile/post uploads, but content-type/magic/size and static serving need hardening. |
| Razor `Html.Raw` | Stored XSS | Needs follow-up | Many raw render sites exist for questions/static pages; content trust/sanitization must be audited separately. |
| NuGet packages | Known vulnerabilities | Reported | Vulnerable transitive packages confirmed. |

## Production Decision

Current status: **NO-GO for production as-is.**

Minimum release gate before production:

1. Fix Partner authorization and tenant binding.
2. Rotate and remove production database credentials from repository/config.
3. Protect or disable Hangfire dashboard.
4. Fix unauthenticated/IDOR student study-session and AI recommendation endpoints.
5. Lock down generic PDF rendering.
6. Re-run security smoke tests for anonymous, cross-student, cross-partner, and admin-only routes.

## Recommended 72-Hour Action Plan

Day 0 emergency:

- Rotate Azure SQL credential and move connection string to secure configuration.
- Temporarily block `/Partner/*`, `/hangfire`, `/Print/ViewAsPdf`, and vulnerable Student study-session/AI routes at gateway/app level if immediate code deployment is not ready.

Day 1 fixes:

- Add Partner-area authorization centrally.
- Bind partner selection to authenticated `UserPartners`.
- Add anti-forgery to PartnerContext Set.
- Protect Hangfire dashboard.

Day 2 fixes:

- Convert student-only controllers to inherit `StudentBaseController` or add `[Authorize(Roles = "Student")]`.
- Remove user-controlled `studentId` and posted `StudentId` from student self-service flows.
- Replace generic PDF endpoint with explicit authorized report routes.

Day 3 verification:

- Add integration tests for anonymous access, role access, partner isolation, student ownership, CSRF, and dashboard protection.
- Re-run `dotnet list package --vulnerable --include-transitive`.
- Perform manual smoke test on staging.

## Open Questions And Follow Up

- Are `/Partner/*`, `/hangfire`, and `/Print/ViewAsPdf` exposed publicly in Azure App Service, or restricted by App Gateway/WAF?
- Is the committed SQL password currently valid in production?
- Are question/static-page HTML fields sanitized before storage?
- Which package brings `System.Data.SqlClient 4.4.0`, `System.IO.Packaging 6.0.0`, and `Npgsql 8.0.0` transitively?
- Should partner admins and internal admins require MFA before production?

