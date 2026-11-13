# Long run — 2026-11-11

Standing approval for the queue T1, H1, H2, W1, W2, F8, C1, S1–S9. Status per batch below.

## App map update — DONE (commit 2cef2bf)
READ-ONLY check (app_list): all apps exist with the exact names and types.

| App | Type | Revision at check |
|---|---|---|
| Salenix_Sales | WebApplication | 4 |
| Salenix_Finance | WebApplication | 3 |
| Salenix_Comms | WebApplication | 3 |
| Salenix_Agent | WebApplication | 3 |
| Salenix | WebApplication | 3 |
| Salenix_Admin | WebApplication | 2 |
| Salenix_Integration | LowCodeLibrary | 3 |
| Salenix_UIKit | LowCodeLibrary | 3 |

CLAUDE.md: decision 8 (approvals in Foundation, approve/reject in the domain app), new app map (Foundation 18, Sales 22, Finance 11, Comms 8, Agent 11), publish order, one-way references, references already added, decision 20 (Theme v2).

## T1 Salenix_Theme v2 — DONE (rev 3, commit 82b6eae)
Done before the long run started (see the chat report). Nothing changed in this run.

## H1 Foundation Safe helpers — DONE, no change needed (rev 20 stays)
READ-ONLY review (Mentor, exact node text) of Foundation_SafeTodayIn and Foundation_SafeConvertUtcToZone:
- Handler AllExceptions (Abort Transaction = False, Log Error = False) → If `Index(AllExceptions.ExceptionMessage, "TIMEZONE_UNKNOWN:") = 0` → True: LogMessage + call with "Asia/Kolkata", UsedZone = "Asia/Kolkata" → End; False: Raise FoundationError with `AllExceptions.ExceptionMessage` (original message, unchanged).
- Salenix_Crypto raises `TIMEZONE_UNKNOWN: '<zone>' is not an IANA time zone.` (crypto/Salenix_Crypto/TimeZones.cs:27), so the prefix check matches.
- TodayFor / NowFor: ValidationError re-raised unchanged; AllExceptions → LogMessage + FoundationError "FOUNDATION_ERROR: Could not resolve the workspace date/time."
This is exactly the approved H1 design, so nothing was changed or published.
Mentor notes (no action): other errors arrive as FoundationError (type changes, message kept) — as designed; TimezoneFor falls back to Asia/Kolkata without a log when the settings row or Timezone is empty (source default behaviour).
**Test (H3 DevTools):** TodayFor/NowFor for a workspace with Timezone "Asia/Kolkata" → today/now +5:30; set Timezone "Mars/Base" in Development data → result Asia/Kolkata and one log line "Unknown timezone 'Mars/Base'; fallback Asia/Kolkata. TIMEZONE_UNKNOWN: ..." (this also proves ODC passes the external-library message through unchanged).

## H2 Foundation invitations — PUBLISHED rev 21 (env_app verified, deployed 2026-11-11 07:58 UTC)

### Plan
Source: source/src/lib/team/invitations.ts, source/src/app/dashboard/team/actions.ts:39-190, source/src/lib/team/schema.ts, source/src/lib/auth/tokens.ts, source/src/lib/membership.ts; BRD FR-TEAM-01..04, FR-AUTH-03, §3.3.
Library calls: Salenix_Crypto NewToken(32) (43 chars base64url) and Sha256Hex(Token) (lower-case hex). Entity OrgInvitation exists (F1).
New Setting: `AppBaseUrl` (Text, not secret, default "") — the user sets the value; AcceptLink = Settings.AppBaseUrl + "/accept-invite?token=" + Token.

| Action (service, Public = Yes) | Inputs | Outputs | Rules |
|---|---|---|---|
| Invitation_Create | Email, RoleCode | InvitationId, AcceptLink, ExpiresAt | Context (ResolveContext); CheckPermission inviteMembers (owner, admin, manager). Email = ToLower(Trim(Email)); empty or not valid → "Enter a valid email address"; Length > 255 → "Email is too long". RoleCode ∈ admin, manager, member (Trim+ToLower) else "Pick a role". Manager may invite member only → "Managers can only invite members." (approved change). Active membership of a UserProfile with that email in the tenant → "This person is already a member of this workspace." (decision c: suspended members can be re-invited). Delete older pending invites (AcceptedAt empty) for the same tenant + email (decision b). Token = NewToken(32); TokenHash = Sha256Hex(Token); ExpiresAt = now + 7 days; InvitedByUserId = GetUserId(). Raw token never stored. |
| Invitation_ListPending | – | List of InvitationItem (Id, Email, RoleCode, InvitedByName, ExpiresAt, CreatedAt) | Context; CheckPermission inviteMembers; AcceptedAt empty and ExpiresAt > now (local variable, not CurrDateTime() in the filter); oldest first (FR-TEAM-01). |
| Invitation_Revoke | InvitationId | – | Context; CheckPermission revokeInvitations (owner, admin); only a pending row of the active tenant; not found → "Invalid invitation id."; hard delete (FR-TEAM-03). |
| Invitation_GetByToken | Token | IsValid, WorkspaceName, MaskedEmail, RoleCode, ExpiresAt | No membership needed (accept page runs before the user is a member); requires a logged-in user. Hash the token; row with AcceptedAt empty and ExpiresAt > now; invalid → IsValid = False (no exception). MaskedEmail = first letter + "****@" + domain. |
| Invitation_Accept | Token | TenantId, WorkspaceName | EnsureMyProfile logic first. Find by TokenHash (aggregate), then GetOrgInvitationForUpdate(Id) and re-check AcceptedAt empty and ExpiresAt > now after the lock → else "This invitation is invalid or has expired." Caller's UserProfile.Email ≠ invite email → "This invitation was sent to a different email address." Membership (tenant, user): exists → Status = "active", Role = invite role unless current role = owner (owner is never downgraded, BRD FR-TEAM-04; source downgrades); else create active membership. AcceptedAt = now. UserProfile.ActiveTenantId = invite tenant (opens the invited workspace, FR-AUTH-03). Grant ODC role SalenixUser if ODC allows it (decision d), otherwise stop and report. |
Errors: user errors raise ValidationError with "VALIDATION_ERROR: " + message; two handlers (ValidationError re-raised unchanged; AllExceptions → LogMessage + FoundationError "FOUNDATION_ERROR: Could not ...").
Not built (OPEN): invite email (Salenix_Integration), activity invited / invitation_revoked (Salenix_Sales ActivityLog — Foundation cannot reference Sales), realtime member.invited, onboarding job.

### Result H2
Created (Salenix_Foundation rev 21): Setting AppBaseUrl (Text, not secret, default ""); structure InvitationItem (Public = Yes — required because a public service action returns it); service actions Invitation_Create, Invitation_ListPending, Invitation_Revoke, Invitation_GetByToken, Invitation_Accept. Nothing existing changed. Node text checked against the plan for Create and Accept (full text in the Mentor report); ListPending, Revoke, GetByToken checked from the summary.
Deviations and handling:
- ODC does not allow a service action to call another service action → Invitation_Accept cannot call Foundation_EnsureMyProfile. Chosen: inline copy of the EnsureMyProfile logic inside Invitation_Accept (Comment "Keep both in sync"); EnsureMyProfile untouched. Later cleanup: extract a private server action Foundation_EnsureProfileCore and call it from both (needs a change to the tested EnsureMyProfile — your call).
- Invitation_Accept adds two guards: User row missing / profile missing after create → "MEMBERSHIP_MISSING: Your profile could not be found."
- Invitation_Revoke filter uses LongIntegerToIdentifier(InvitationId).
- Mentor claimed the MaskedEmail expression drops the last domain character; that is wrong (Substr takes a length: "anna@x.com" → "a****@x.com"); expression kept.
- GrantSalenixUserRole exists in ODC → Accept grants SalenixUser when CheckSalenixUserRole is False (decision d satisfied, no manual step).
- **OrgInvitation has no explicit indexes** (Mentor sees none: no TokenHash unique, no Email, no (TenantId, Email)). F1 spec asked for them. Please check in Studio; if missing, add TokenHash unique + (TenantId, Email) by hand (or I add them in a later entity batch).
- Context index (context_actions) still showed rev 20 right after the publish; verification is env_app revision 21 + the Mentor node text.
Tests for you (DevTools H3 — needs Dev_* wrappers for the 5 actions, DevTools reference refresh first):
1. Set Setting AppBaseUrl in the ODC Portal (Salenix_Foundation → Configurations), e.g. https://vtpl-dev.outsystems.app/Salenix.
2. As owner: Invitation_Create("NewUser@Example.com ", "member") → InvitationId > 0, ExpiresAt ≈ now + 7 days, AcceptLink ends with "/accept-invite?token=" + 43 chars. OrgInvitation row: Email "newuser@example.com", TokenHash 64 hex chars (not the token).
3. Same email again → old pending row deleted, one new row (decision b).
4. Invitation_Create(email of an active member, "member") → "This person is already a member of this workspace." ; role "owner" → "Pick a role"; email "abc" → "Enter a valid email address".
5. As manager: role "admin" → "Managers can only invite members."; role "member" → works. As member: → "Permission denied: requires "inviteMembers" (current role: member)".
6. Invitation_ListPending → oldest first, InvitedByName filled.
7. Invitation_GetByToken(token) → IsValid True, WorkspaceName, MaskedEmail "n****@example.com", RoleCode member. Wrong token → IsValid False.
8. Log in as newuser@example.com → Invitation_Accept(token) → TenantId of the workspace; Membership active role member; UserProfile.ActiveTenantId = that tenant; user has role SalenixUser; second Accept with the same token → "This invitation is invalid or has expired."
9. Accept while logged in as a different email → "This invitation was sent to a different email address."
10. Owner invites an existing owner's email of another workspace user who is owner here? (owner-not-downgraded): make user X owner, suspend nothing, invite X as member (X is active → blocked by step 4); to test the owner rule set X's Membership.Status = "suspended" in data, invite as member, accept → Status active, Role stays owner.
11. As admin: Invitation_Revoke(pending id) → row deleted; same id again → "Invalid invitation id."; as manager → "Permission denied: requires "revokeInvitations" ...".

## W1/W2 source findings (input for the plan)
The source has **no approval tables** (no approval_rules / approval_requests / approval_actions). Approval in the code is columns on two tables:
- Won deal: `deals.approval_status` enum none/pending/approved/rejected (default none), `approved_by` text, `approved_at` timestamptz (source/src/db/schema/deals.ts:76-80, drizzle/0042). On reject approved_by/at hold the rejecter.
  - Enter Won (source/src/lib/deals/approval.ts:41-81): actor with approveDeals → approved (auto); else pending. Leaving Won (or any full save of a non-Won deal) → none. Won → Won untouched. Create at Won is refused (deals/actions.ts:359-361).
  - Approve (deals/actions.ts:1638-1716): permission approveDeals; requires stage won AND status pending, else "This deal is not awaiting approval."; sets approved + by/at; activity deal_approved; realtime deal.updated.
  - Reject (deals/actions.ts:1723-1820): same guard; reason OPTIONAL (trim, ≤ 500, empty → null); stage → negotiation, closed_at null, status rejected, by/at = rejecter; stage-change reason approval_rejected ("Reopened by manager rejection"); activity deal_rejected; realtime deal.stage_changed won→negotiation.
  - Bulk approve/reject (≤ 200 ids, skips non-pending; error "No deals selected.").
  - Lists: awaiting (won+pending, closed_at desc, 200), recently approved (50), recently rejected (50) (deals/queries.ts:590-672).
- Credit terms: `deal_credit_terms` (collections.ts:48-73, drizzle/0069): agreed_credit_days 0–730, set_by, approved_by, approved_at (both null or both set), note; unique per deal. Over standard (> OrganizationSettings.StandardCreditDays) needs approval; actor with approveCreditTerms self-approves; days change clears approval; approve has no guard; **no reject** in the source.
- Approver roles: approveDeals and approveCreditTerms = owner, admin, manager (rbac.ts:55, 97). No rules, no levels, no seeds in the code.
BRD vs code: BRD "reason ≤ 500" reads as required, code optional; BRD create-at-Won approval vs code refuses create at Won; BRD "rejected clears on next stage move" vs code clears on any non-Won save; bulk message "Select at least one deal" vs code "No deals selected."

## W1 Foundation approval entities — PUBLISHED rev 22 (env_app verified, 2026-11-11 08:00 UTC)

### Plan (new design: no source tables, see findings above)
Design follows decision 8: a generic single-level engine in Foundation that only records requests and decisions; Sales/Finance keep their own state columns (Deal.ApprovalStatus/ApprovedBy/ApprovedAt as in the source; DealCreditTerm ApprovedBy/ApprovedAt) and apply decisions themselves. SubjectId is a Long Integer because Foundation cannot reference Sales/Finance entities (references go one way).
| Entity | Attributes | Indexes |
|---|---|---|
| ApprovalRule | Id; TenantId (M, Delete); SubjectType Text 50 M (deal_won, credit_terms); Name Text 250 M; ApproverRoles Text 250 M default "owner,admin,manager"; Level Integer M default 1; IsActive Boolean M default True; CreatedAt, UpdatedAt DT M; LegacyUuid Text 36 | (TenantId, SubjectType) unique; LegacyUuid |
| ApprovalRequest | Id; TenantId (M, Delete); RuleId ApprovalRule Identifier (Delete rule Ignore); SubjectType Text 50 M; SubjectId Long Integer M; SubjectLabel Text 250 (e.g. deal name, for lists); Status Text 50 M default "pending" (pending, approved, rejected, cancelled); AutoApproved Boolean M default False; SubmittedByUserId User Identifier (Ignore); SubmittedAt DT M; DecidedByUserId User Identifier (Ignore); DecidedAt DT; DecisionReason Text 500; CreatedAt, UpdatedAt DT M; LegacyUuid | (TenantId, Status); (TenantId, SubjectType, SubjectId); LegacyUuid |
| ApprovalAction (append-only) | Id; TenantId (M, Delete); RequestId ApprovalRequest Identifier (M, Delete); Action Text 50 M (submitted, auto_approved, approved, rejected, cancelled); ActorUserId User Identifier (Ignore); Reason Text 500; CreatedAt DT M | (TenantId, RequestId); RequestId |
All Public = No (actions expose structures). No entity actions used outside Foundation.

### Result W1
Mentor report matches the plan exactly (every attribute, default, delete rule, index). Index names were shortened by the platform (e.g. IX_ApprovalRule_TenantId_Sub); ODC also adds automatic non-unique indexes on TenantId and ApprovalAction.RequestId. SubmittedByUserId / DecidedByUserId not mandatory (as planned). No deviations.
Test: none needed for entities alone (W2 tests cover them). Studio check: open Foundation rev 22 → the 3 entities, (TenantId, SubjectType) unique on ApprovalRule.

## W2 Foundation approval logic — PUBLISHED rev 23 (env_app verified, 2026-11-11 08:07 UTC)

### Plan
ODC limit found in H2: a service action cannot call another service action → shared logic goes into private server actions.
Subject types: deal_won (permission approveDeals), credit_terms (permission approveCreditTerms); both owner, admin, manager (rbac.ts:55, 97). Submit permission: manageDeals (source: setting terms and moving a deal need manageDeals).
| Element | Kind | Rules |
|---|---|---|
| Approval_EnsureRule(TenantId, SubjectType) → RuleId, IsActive, ApproverRoles | private server action | Create-only seed: if no ApprovalRule (TenantId, SubjectType) → create Name "Won deal approval" / "Credit terms approval", ApproverRoles "owner,admin,manager", Level 1, IsActive True. Called lazily by Submit (no change to Workspace_Provision). |
| Approval_CanDecide(RoleCode, SubjectType, ApproverRoles) → CanDecide | private server action | Role in ApproverRoles (comma list, Trim+ToLower) AND Foundation_CheckPermission(role, approveDeals / approveCreditTerms). |
| Approval_Submit(SubjectType, SubjectId, SubjectLabel) → Success, OutcomeCode, Message, RequestId, Status, AutoApproved | service, public | Context; manageDeals; SubjectType must be deal_won or credit_terms → "Unknown approval subject."; rule inactive → OutcomeCode not_required, Status approved, no row; existing pending request for the same subject → returns it, OutcomeCode already_pending (idempotent); submitter can decide → request approved, AutoApproved True, DecidedBy = submitter, actions submitted + auto_approved, OutcomeCode auto_approved (source approval.ts:41-54); else pending + action submitted, OutcomeCode pending. |
| Approval_Decide(RequestId, Decision, Reason) → Success, OutcomeCode, Message, SubjectType, SubjectId, Status | service, public | Context; request of the tenant else "Invalid approval request."; Decision approve/reject (Trim+ToLower) else "Approve or reject?" (source askmira/actions.ts:424); CanDecide else permission message; lock (GetApprovalRequestForUpdate) and re-check; same decision already applied → Success True, OutcomeCode already_decided, no change (idempotent); other non-pending state → "This request is no longer awaiting approval." (source "This deal is not awaiting approval."); reject needs Reason (queue rule; **source: optional** — deviation recorded), Reason ≤ 500; writes DecidedBy/At, DecisionReason, action row. The domain app applies the decision afterwards (Sales: Won → negotiation with reason approval_rejected). |
| Approval_Cancel(SubjectType, SubjectId) → CancelledCount | service, public | Added (not in the queue): Sales must clear a pending approval when a deal leaves Won (source approval.ts:67-81). Context; manageDeals; pending requests of the subject → cancelled + action cancelled. |
| Approval_ListPending(SubjectType) → Items (ApprovalItem List) | service, public | Context; caller must be able to decide the subject type (empty SubjectType = all types the caller may decide); Status pending; SubmittedAt desc; max 200 (source limit). |
| Approval_Get(RequestId) → Item (ApprovalItem), Actions (ApprovalActionItem List) | service, public | Context; request of the tenant; caller is the submitter or can decide; else "Invalid approval request." |
Structures (Public): ApprovalItem (Id, SubjectType, SubjectId, SubjectLabel, Status, AutoApproved, SubmittedByUserId, SubmittedByName, SubmittedAt, DecidedByUserId, DecidedByName, DecidedAt, DecisionReason); ApprovalActionItem (Action, ActorUserId, ActorName, Reason, CreatedAt).

### Result W2
Created (rev 23): structures ApprovalItem, ApprovalActionItem (Public); private server actions Approval_PermissionFor, Approval_EnsureRule, Approval_CanDecide; service actions Approval_Submit, Approval_Cancel, Approval_Decide, Approval_ListPending, Approval_Get. Nothing existing changed. Full node text of Submit, CanDecide and Decide checked against the plan; ListPending filters checked.
Deviations: private-action handler re-raises VALIDATION_ERROR:/FOUNDATION_ERROR: messages unchanged, other messages get the "FOUNDATION_ERROR: " prefix; extra locals (Target) and separately named helper nodes; ListPending treats SubjectType only as a filter after validation. Seed is lazy (Approval_EnsureRule creates the two rules per workspace on first use) — no change to Workspace_Provision; rules for a workspace appear after its first Submit/ListPending/Decide.
Decisions recorded: reject reason REQUIRED (queue rule; source optional); Approval_Cancel added for "deal leaves Won".
Tests for you (DevTools; needs Dev_* wrappers): 
1. As member (cannot approve): Approval_Submit("deal_won", 101, "Test deal") → OutcomeCode pending, Status pending, RequestId > 0; ApprovalRule rows deal_won + credit_terms created for the workspace (if ListPending ran first) or deal_won at least; ApprovalAction "submitted".
2. Submit again same subject → already_pending, same RequestId.
3. As manager: Approval_ListPending("") → contains 101; Approval_Decide(id, "reject", "") → "Enter a reason for the rejection."; Decide(id, "maybe", "") → "Approve or reject?"; Decide(id, "approve", "") → OutcomeCode approved; again → already_decided; Decide(id, "reject", "x") → "This request is no longer awaiting approval."
4. As manager: Approval_Submit("credit_terms", 55, "Terms") → auto_approved, AutoApproved True, two actions (submitted, auto_approved).
5. As member: Approval_Decide(any) → "Permission denied: requires "approveDeals" (current role: member)"; Approval_Get(own request) works; Approval_Get(someone else's) → "Invalid approval request."
6. Submit pending then Approval_Cancel("deal_won", 101) → CancelledCount 1; Approval_Get shows actions submitted, cancelled.
7. Approval_Submit("invoice", 1, "") → "Unknown approval subject."; viewer → "Permission denied: requires "manageDeals" ...".

## F8 Foundation entities RealtimeEvent, WorkflowRun, ChartView, DashboardWidget — PUBLISHED rev 24 (env_app verified, 2026-11-11 08:09 UTC)

### Plan
Source: notes/source-schema-sales.md (workflow-runs.ts 0006, chart-views.ts 0022, dashboard-widgets.ts 0023); RealtimeEvent has no source table (BRD-App-Logic l.977). jsonb → Text 8000 (Metadata 4000 like Notification). All Public = No, Optimized, Id AutoNumber, TenantId mandatory Delete, user ids User Identifier (Ignore), LegacyUuid Text 36 (non-unique index) except RealtimeEvent.
| Entity | Attributes | Indexes |
|---|---|---|
| RealtimeEvent | TenantId; Type Text 100 M; EntityType Text 50; EntityId Long Integer; ActorUserId User Identifier; Payload Text 4000; CreatedAt DT M | (TenantId, CreatedAt) |
| WorkflowRun | TenantId; WorkflowId Text 250 M; EventName Text 250; ExternalRunId Text 250 (source inngest_run_id); Status Text 50 M default "running" (running, completed, failed, skipped); StartedAt DT M; CompletedAt DT; Error Text 2000; Metadata Text 4000; LegacyUuid | (TenantId, StartedAt); ExternalRunId |
| ChartView | TenantId; UserId User Identifier M; ChartKey Text 100 M; Title Text 250; RoleScope Text 50 M default "user" (user, team, tenant); Config Text 8000 M; IsDefault Boolean M default False; CreatedAt, UpdatedAt DT M; LegacyUuid | (TenantId, UserId, ChartKey) — source partial unique "one default per user+chart" cannot be an index in ODC → enforced by the save action later |
| DashboardWidget | TenantId; UserId User Identifier M; WidgetType Text 100 M; ChartType, Metric, Dimension Text 100; Title Text 250; LayoutX, LayoutY Integer M default 0; LayoutW, LayoutH Integer M default 4; ConfigJson Text 8000 M; CreatedAt, UpdatedAt DT M; LegacyUuid | (TenantId, UserId) |

### Result F8
Mentor report matches the plan exactly (attributes, defaults, delete rules, indexes). Platform adds AutoIndex_TenantId on each. No actions, no deviations. Not in the source: RealtimeEvent (design from BRD). Source partial unique on ChartView default is not possible as an index (ODC) → to be enforced in the future ChartView save action.

## C1 Salenix_Sales template cleanup — PUBLISHED rev 5 (env_app verified, 2026-11-11 08:12 UTC)
REPORT ONLY first (Mentor inventory), then removal of TEMPLATE elements only.
Removed: screens Login, RecoverPasswordRequest, RecoverPasswordReset, ChangePassword, UserProfile, InvalidPermissions; UI flows Common, Layouts, Emails (MainFlow kept, empty); blocks Layout, LayoutSideMenu, LayoutTopMenu, LayoutBlank, Menu, MenuIcon, ApplicationTitle, UserInfo; client actions DoLogin, DoLogout, SendResetPasswordEmail, SendChangeEmail, UpdateUser; server actions SendResetPasswordEmail, SendChangeEmail, UpdateUser; folders Authentication, UserActions; client variables LastURL, UserName, UserPhotoURL; images Logo, User; resource favicon.png; emails ResetPassword, ChangeEmail; EmailTheme; external site RedirectToURL; references OutSystemsCharts, OutSystemsMaps.
Changed: theme Salenix_Sales Default Layout Layout → none, Default Menu Menu → none, Login background (Maps marker) → none. Style sheet untouched.
Kept: role Salenix_Sales, theme Salenix_Sales (+ Inter font resources), references (System), OutSystemsUI, Salenix_Foundation, Salenix_Crypto.
References present (Mentor): Salenix_Foundation → entities Tenant, Membership; structures NotificationItem, WorkspaceItem; role SalenixUser; service actions Notification_ListMine/MarkAllRead/MarkRead/Create, Member_ChangeRole/Remove, Workspace_Create/CreateFirst/ListMine/SetActive, Foundation_GetMyContext/EnsureMyProfile/GetFlags/HasPermission/NowFor/TodayFor, DevOnly_AddMember. Salenix_Crypto → all 13 actions + JwtVerifyResult.
Open items: (1) warning "Global Exception Handler Common\OnException does not exist" — Mentor could not clear the app property; set it to none (or a new handler) in Studio. (2) 18 offline warnings for the Inter font URLs in the Salenix_Sales theme (pre-existing; Sales has no UI, the theme can be left as is). (3) **NEEDS REFERENCE: Salenix_Sales → Salenix_Foundation: Approval_Submit, Approval_Cancel, Approval_Decide, Approval_Get, Approval_ListPending, ApprovalItem, ApprovalActionItem, Invitation_* (not needed by Sales), CustomFieldDefinition (if you want a real FK for CustomFieldValue; Foundation must make it Public first)** — reference refresh in Studio (decision: references are by hand).
Test: open https://vtpl-dev.outsystems.app/Salenix_Sales → no screen (expected; domain app has no UI).

## S1 Sales entities Company, Contact, MergeAudit, CustomFieldValue — PUBLISHED rev 6 (env_app verified, 2026-11-11 08:15 UTC)

### Plan
Source: notes/source-schema-sales.md; BRD-Database Company/Contact/MergeAudit (lengths from the BRD). All Public = No, Optimized, Id AutoNumber, TenantId (Tenant Identifier, M, Delete), user refs User Identifier (Ignore), LegacyUuid Text 36 (non-unique index; BRD says unique — never unique, our rule).
Decisions: no CustomFields Text on Company/Contact — custom values live in CustomFieldValue (app map; BRD §5.6). MergedIntoId = self Identifier with delete rule Ignore (source FK ON DELETE SET NULL; BRD Long Integer without FK). Company gets the 8 rollup attributes from VIEW company_rollups (0065) — maintained by Sales write actions (decision 10).
| Entity | Attributes | Indexes |
|---|---|---|
| Company | TenantId; Name Text 250 M; Domain Text 250; Industry Text 250; OwnerId; CreatedAt, UpdatedAt DT M; UpdatedBy; DeletedAt DT; DeletedBy; NormalizedName Text 250; MergedIntoId Company Identifier (Ignore); DedupOverride Bool M False; DealCount Int M 0; OpenDealCount Int M 0; WonDealCount Int M 0; OpenPipelineValueMinor Long M 0; WonLifetimeValueMinor Long M 0; TopStage Text 50; ContactCount Int M 0; LastActivityAt DT; LegacyUuid | (TenantId, Name); (TenantId, Domain); (TenantId, OwnerId); (TenantId, DeletedAt); (TenantId, MergedIntoId); (TenantId, NormalizedName); LegacyUuid |
| Contact | TenantId; CompanyId Company Identifier (Ignore); FirstName, LastName Text 250; Email Email 250; Phone Text 250; Title Text 250; OwnerId; CreatedAt, UpdatedAt M; UpdatedBy; DeletedAt; DeletedBy; NormalizedName Text 250; MergedIntoId Contact Identifier (Ignore); DedupOverride Bool M False; LegacyUuid | (TenantId, Email); (TenantId, LastName); (TenantId, CompanyId); (TenantId, OwnerId); (TenantId, DeletedAt); (TenantId, MergedIntoId); (TenantId, NormalizedName); LegacyUuid |
| MergeAudit | TenantId; EntityType Text 50 M; WinnerId Long M; LoserId Long M; LoserSnapshot Text 8000 M; RepointedCounts Text 2000 M default "{}"; MergedBy; CreatedAt M; LegacyUuid | (TenantId, CreatedAt); (TenantId, WinnerId); LegacyUuid |
| CustomFieldValue | TenantId; EntityType Text 50 M (deal, company, contact); EntityId Long M; DefinitionId Long M (CustomFieldDefinition lives in Foundation and is not referenced → plain Long, no FK); ValueText Text 2000; ValueNumber Decimal 28,8; ValueDate Date; ValueBoolean Boolean; CreatedAt, UpdatedAt M | (TenantId, EntityType, EntityId, DefinitionId) unique; (TenantId, DefinitionId) |

### Result S1
All four entities created as planned, with two platform-forced deviations:
- **TenantId delete rule = Ignore, not Delete**: ODC rejects Delete on a reference to an entity of another app ("not supported on this version"). Applies to every Sales/Finance/Comms/Agent entity. Consequence: deleting a Tenant does not cascade to domain apps → a workspace-delete flow must remove Sales rows in code (future Workspace_Delete: Foundation raises an event / Sales cleanup action). **Decision for you.**
- Contact.Email length 256 (Email type is fixed).
Indexes as planned (platform shortens names; adds AutoIndex_TenantId). CustomFieldValue unique (TenantId, EntityType, EntityId, DefinitionId) present.
Mentor also added the reference to Salenix_Foundation.Tenant (was already referenced; no change in references list).

## S2 Sales entities Deal, DealContact, Offering, DealOffering, DealValueSplit, DealOrderProof — PUBLISHED rev 7 (env_app verified, 2026-11-11 08:18 UTC)

### Plan
Source: notes/source-schema-sales.md (deals.ts, 0045, 0055/0057, 0066, 0071). Same conventions as S1. Codes are Text (lower case) — no static entities. Money *Minor Long Integer. Cross-app ids without FK: DealOrderProof.DocumentId (FileAttachment in Foundation, not referenced).
| Entity | Attributes | Indexes |
|---|---|---|
| Deal | TenantId; CompanyId Company Id (Ignore); ContactId Contact Id (Ignore); Name Text 250 M; Description Text 2000; CompanyName, ContactName Text 250; ContactEmail Text 250; Stage Text 50 M "lead"; DealType Text 50; AmountMinor Long M 0; UnitPriceMinor Long; Quantity Integer; OfferPriceMinor Long; MarginBps Integer; ApprovalStatus Text 50 M "none"; ApprovedBy User; ApprovedAt DT; LostReason Text 500; HoldReason Text 50; HoldRevisitOn Date; HoldNote Text 2000; NextStep Text 500; Competitor Text 250; Source Text 100; Currency Text 10 M "INR"; ExpectedCloseDate Date; OriginalCloseDate Date; PushCount Int M 0; LastPushedAt DT; TotalSlipDays Int M 0; RollbackCount Int M 0; QuotaWeight Decimal 3,2 M 1; LastStageChangedAt DT; ClosedAt DT; OwnerId User; Probability Integer; HealthScore Integer; HealthBand Text 50; ForecastCategory Text 50; ForecastCategoryOverridden Bool M False; CreatedAt, UpdatedAt M; UpdatedBy; DeletedAt; DeletedBy; LegacyUuid | (TenantId, Stage); (TenantId, OwnerId); (TenantId, CompanyId); (TenantId, ContactId); (TenantId, ExpectedCloseDate); (TenantId, DeletedAt); (TenantId, ForecastCategory); (TenantId, HoldRevisitOn); (TenantId, ApprovalStatus); LegacyUuid |
| DealContact | TenantId; DealId Deal Id M (Delete); ContactId Contact Id M (Delete); Role Text 50 M "other"; IsPrimary Bool M False; CreatedAt M; LegacyUuid | (DealId, ContactId) unique; (TenantId, DealId); (TenantId, ContactId) |
| Offering | TenantId; Name Text 250 M; Category Text 50 M; Active Bool M True; SortOrder Int M 0; CreatedAt, UpdatedAt M; LegacyUuid | (TenantId, Name) unique (case-insensitive check in the save action); (TenantId, Category, SortOrder); LegacyUuid |
| DealOffering | TenantId; DealId M (Delete); OfferingId Offering Id M (Protect); IsPrimary Bool M False; CreatedAt M; LegacyUuid | (DealId, OfferingId) unique; (TenantId, DealId); (TenantId, OfferingId) |
| DealValueSplit | TenantId; DealId M (Delete); ElementIndex Int M 1; Component Text 50 M "other"; ComponentLabel Text 250 default "Contract value"; BillingType Text 50 M "recurring"; YearIndex Int M; PeriodStart Date M; PeriodEnd Date M; AmountMinor Long M 0; CreatedAt, UpdatedAt M; LegacyUuid | (DealId, ElementIndex, YearIndex) unique; (TenantId, DealId) |
| DealOrderProof (append-only) | TenantId; DealId M (Delete); DocumentId Long M; ProofType Text 50 M "purchase_order"; ReferenceNo Text 250 M; IssuedOn Date; BuyerName Text 250; Currency Text 10 M; SubtotalMinor Long M; TaxMinor Long M 0; TotalMinor Long M; AiRead Text 8000; SubmittedBy User Id M; CreatedAt M; LegacyUuid | (TenantId, DealId); (DealId, ReferenceNo) (source unique on lower(trim) → enforced by the add action) |
Source CHECKs (probability 0–100, close date for proposal/negotiation/won, split year/element ≥ 1, period_end > period_start, other needs label, proof amounts) → enforced in Sales actions (ODC has no CHECK).

### Result S2
All six entities match the plan (attributes, defaults, delete rules, unique indexes). TenantId = Ignore (platform, see S1). Platform adds AutoIndex on DealId/ContactId/OfferingId. No deviations.

## S3 Sales entities DealStageHistory, DealDateChange, DealAuditLog, StageGate, DealScore, DealRiskScore, ForecastSnapshot — PUBLISHED rev 8 (env_app verified, 2026-11-11 08:20 UTC)

### Plan
Same conventions (TenantId Ignore — platform). DealId = Deal Identifier mandatory, Delete. jsonb → Text 4000.
| Entity | Attributes | Indexes |
|---|---|---|
| DealStageHistory | TenantId; DealId; Stage Text 50 M; EnteredAt DT M; ExitedAt DT; LegacyUuid | (DealId, EnteredAt); (TenantId, Stage) — "one open row per deal" enforced by Deal_* actions |
| DealDateChange | TenantId; DealId; OldCloseDate Date; NewCloseDate Date; ChangedBy User; ChangedAt DT M; Reason Text 500; IsPush Bool M False; LegacyUuid | (TenantId, DealId, ChangedAt) |
| DealAuditLog (append-only) | TenantId; DealId; ChangedBy User; ChangedAt DT M; FieldName Text 50 M; OldValue Text 250; NewValue Text 250; ChangeType Text 50 M; ReasonCode Text 50; ReasonNote Text 1000; LegacyUuid | (TenantId, DealId, ChangedAt) |
| StageGate | TenantId; Stage Text 50 M; RuleType Text 50 M; RuleConfig Text 4000 M default "{}"; Severity Text 20 M default "block"; IsActive Bool M True; CreatedBy User; CreatedAt, UpdatedAt M; LegacyUuid | (TenantId, Stage, RuleType) unique; (TenantId, Stage, IsActive) |
| DealScore | TenantId; DealId; Score Integer M; Band Text 50 M; Reasons Text 4000 M default "[]"; ComputedAt DT M; LegacyUuid | (DealId, ComputedAt); (TenantId, DealId) |
| DealRiskScore | TenantId; DealId; Score Integer M; Band Text 50 M; Factors Text 4000 M default "[]"; Signals Text 4000 M default "{}"; Explanation Text 2000; ExplanationModel Text 100; ExplainedAt DT; SignalsHash Text 128 M; ComputedAt DT M; LegacyUuid | (DealId) unique; (TenantId, Score) |
| ForecastSnapshot | TenantId; Period Text 50 M; OwnerId User; Category Text 50 M; TotalAmountMinor Long M 0 (source total_amount, money); DealCount Int M 0; CapturedAt DT M; LegacyUuid | (TenantId, Period, CapturedAt) |

### Result S3
All seven entities match the plan. On DealRiskScore the automatic DealId index clashed with the planned unique (DealId) index → Mentor removed the automatic one (only the unique index remains) — correct. Other child entities keep AutoIndex_DealId. TenantId Ignore (platform). No other deviations.

## S4 Sales entities CrmTask, Note, Comment, ActivityLog, SalesJobQueue — PUBLISHED rev 9 (env_app verified, 2026-11-11 08:22 UTC)

### Plan
| Entity | Attributes | Indexes |
|---|---|---|
| CrmTask | TenantId; Title Text 250 M; Description Text 2000; Status Text 50 M "todo"; Priority Text 50 M "medium"; Type Text 50; Recurrence Text 50; StageBlocking Text 50; DueDate Date; AssignedUserId User; CreatedBy User M; CompletedAt DT; EntityType Text 50; EntityId Long; CreatedAt, UpdatedAt M; DeletedAt; DeletedBy User; LegacyUuid | (TenantId, Status); (TenantId, AssignedUserId); (TenantId, DueDate); (TenantId, EntityType, EntityId); (TenantId, DeletedAt); LegacyUuid |
| Note | TenantId; Body Text 8000 M; AuthorUserId User M; EntityType Text 50 M; EntityId Long M; CreatedAt, UpdatedAt M; DeletedAt; DeletedBy; LegacyUuid | (TenantId, EntityType, EntityId, CreatedAt); (TenantId, AuthorUserId); (TenantId, DeletedAt); LegacyUuid |
| Comment | TenantId; EntityType Text 50 M (incl. task); EntityId Long M; AuthorUserId User M; ParentCommentId Comment Id (Ignore); Body Text 8000 M; MentionedUserIds Text 2000 M default "" (comma list); EditedAt DT; CreatedAt, UpdatedAt M; DeletedAt; DeletedBy; LegacyUuid | (TenantId, EntityType, EntityId, CreatedAt); (ParentCommentId, CreatedAt); (TenantId, AuthorUserId); (TenantId, DeletedAt); LegacyUuid |
| ActivityLog | TenantId; ActorUserId User; EntityType Text 50 M; EntityId Text 100 M (source text: holds non-uuid ids of memberships/invitations); Action Text 50 M; Metadata Text 4000 M default "{}"; CreatedAt M; LegacyUuid | (TenantId, CreatedAt); (TenantId, EntityType, EntityId, CreatedAt) |
| SalesJobQueue | TenantId; JobType Text 50 M (e.g. deal_recompute); EntityType Text 50 M; EntityId Long M; DueAt DT M; Status Text 50 M "pending" (pending, running, done, failed); Attempts Int M 0; LastError Text 2000; CreatedAt, UpdatedAt M | (Status, DueAt); (TenantId, EntityType, EntityId, JobType) — no source table; design from BRD-App-Logic l.88 DealRecomputeQueue (DueAt = now + 5 min, processed by a timer) |

### Result S4
All five entities match the plan. Index names prefixed IDX_ and cut to 30 characters by the platform. No deviations. Sales now has all 22 entities of the app map.

## S5 Sales stage helpers — PUBLISHED rev 10 (env_app verified, 2026-11-11 08:27 UTC)

### Plan
Source: notes/deal-governance-spec.md §1 (stage-governance.ts, validation.ts, probability, forecast). Server actions with Function = Yes (usable in expressions), Public = No; input Stage Text, compared after Trim+ToLower.
| Function | Output | Logic |
|---|---|---|
| Stage_GetRank | Integer | lead 1, qualified 2, proposal 3, negotiation 4, won 5, else 0 (source null) |
| Stage_IsClosed | Boolean | won or lost |
| Stage_IsParked | Boolean | hold or lost |
| Stage_NeedsCloseDate | Boolean | proposal, negotiation, won |
| Stage_ForbidsCloseDate (extra, R0) | Boolean | lead |
| Stage_NeedsMargin | Boolean | NOT (lead, hold, lost) — unknown/empty → True (source) |
| Stage_DefaultProbability | Integer | lead 10, qualified 30, proposal 60, negotiation 80, hold 40, won 100, lost 0, else 0 |
| Forecast_CategoryFor | Text | lead/qualified pipeline; proposal best_case; negotiation commit; won closed_won; hold/lost omitted; else pipeline |
| Stage_IsRollback(FromStage, ToStage) (extra) | Boolean | rank(from) > 0 and rank(to) > 0 and rank(to) < rank(from) |
Self-check: service action Sales_StageSelfCheck (Public = Yes, no data access) → Passed (Boolean), FailedCount (Integer), Report (Text): runs every row of the spec table §1.2 (7 stages + "xyz" + "" + " WON ") and the 9 rollback pairs, compares with the expected values, lists mismatches. Tenant probability overrides (organization_settings.stage_probabilities) are not in our OrganizationSettings yet → not built (later Stage_ProbabilityFor).

### Result S5
Created: 9 helper server actions (Function = Yes, Public = No) with exactly the planned expressions (Mentor report quotes each If-chain); service action Sales_StageSelfCheck (Public = Yes, 89 checks = 10 inputs × 8 helpers + 9 rollback pairs); user exceptions SalesError and ValidationError (first one created as StageSelfCheckError, renamed in the one corrective prompt so that all Sales errors use the "SALES_ERROR: " / "VALIDATION_ERROR: " codes, decision 13).
Deviation: helpers have no handler (pure functions, allowed by ODC). A service action cannot re-raise the generic exception → handler raises SalesError with the original message (prefixed with "SALES_ERROR: " when missing).
Test for you (DevTools, after adding the Salenix_Sales reference): call Sales_StageSelfCheck → Passed True, FailedCount 0, Report "All 89 checks passed." Any FAIL line names the function, input, expected and actual value.

## S6 Sales Company actions — PUBLISHED rev 11 (env_app verified, 2026-11-11 08:42 UTC)

### Plan
Source: notes/company-contact-spec.md §A (companies/actions.ts, lib/companies/*, lib/dedup/*). Public service actions (consumed by the Salenix UI app); shared logic in private server actions (a service action cannot call a service action of its own app).
Shared private server actions (also used by S7/S8):
| Element | Rules |
|---|---|
| Sales_Context → ActingUserId, ActingTenantId, RoleCode, Persona | calls Salenix_Foundation Foundation_GetMyContext (raises MEMBERSHIP_MISSING when no active workspace) |
| Sales_RequirePermission(RoleCode, PermissionCode) | Foundation_HasPermission; false → ValidationError `VALIDATION_ERROR: Permission denied: requires "<perm>" (current role: <role>)` (source auth-guard.ts:22) |
| Sales_WriteActivity(TenantId, ActorUserId, EntityType, EntityId Text, Action, Metadata) | CreateActivityLog, CreatedAt = now |
| Company_RefreshRollups(TenantId, CompanyId) | recompute the 8 attributes from aggregates (DealCount non-deleted; OpenDealCount stage not won/lost; WonDealCount; OpenPipelineValueMinor / WonLifetimeValueMinor sums; TopStage = most recently updated open deal; ContactCount non-deleted non-merged; LastActivityAt = max(company ActivityLog, company Note, deal UpdatedAt)) — source VIEW 0065 |
| Company_FindMatches(TenantId, Name) → Matches (CompanyMatchItem: CompanyId, Name, Domain, Score, Kind exact/fuzzy) | Norm = Salenix_Crypto NormalizeCompanyName(Name); Length(Norm) < 2 → empty; candidates: tenant, DeletedAt null, MergedIntoId null, NormalizedName <> ""; exact = NormalizedName = Norm; score = max(TrigramSimilarity(stored, Norm), WordSimilarity(Norm, stored)); keep exact or score ≥ 0.55; sort exact first then score desc; top 5 (decision 9, queries.ts:39-82) |
Service actions (all: Sales_Context first; return Success / OutcomeCode / Message / FailedRules where several non-error results exist):
| Action | Rules |
|---|---|
| Company_FindSimilar(Name) → Matches | manageCompanies; flag DedupEnabled off (Foundation_GetFlags) → empty; else FindMatches |
| Company_Create(Name, Domain, Industry, OwnerId, DedupOverride) → Success, OutcomeCode, Message, FailedRules, CompanyId, Matches | manageCompanies; Name trim required "Name is required", ≤ 200 "Name must be 200 characters or fewer"; Domain ≤ 255, Industry ≤ 100 (empty → ""); validation fail → Success False, OutcomeCode validation, Message "Please fix the highlighted fields.", FailedRules = field messages; **server-side dedup** (source trusts the client — fix): flag on and not DedupOverride and matches found → Success False, OutcomeCode duplicate_warning, Matches; NormalizedName = NormalizeCompanyName; persona sales → OwnerId must be self or empty ("You can only assign records to yourself.") (scope rule, stricter than source); create; rollups zero; activity created {name} (+ dedup_override when DedupOverride and matches existed); OutcomeCode ok / ok_dedup_override |
| Company_Update(CompanyId, Name, Domain, Industry, OwnerId) | manageCompanies; same validation; row in tenant, not deleted, (sales persona: own or unassigned) else "Company not found in this tenant."; **NormalizedName recomputed on rename** (BRD fix; source stale); OwnerId change → activity owner_changed {name, from, to} + Foundation Notification_Create to the new owner "You now own <name>" (when not the actor); else activity updated {name}; UpdatedBy/At |
| Company_SoftDelete(CompanyId) | deleteCompanies; active row else "Company not found or already deleted."; DeletedAt/By; children untouched (source); activity deleted |
| Company_Restore(CompanyId) | restoreCompanies; deleted row in tenant else "Company not found in this tenant." (stricter: source restores active rows too); clear DeletedAt/By; activity restored |
| Company_Get(CompanyId) → Item (CompanyItem incl. rollups) | scope: sales persona own or unassigned (stricter than source detail page); not deleted, not merged |
| Company_List(Search, OwnerFilter mine/unassigned/"", IncludeDeleted, SortBy recent/deals/pipeline/won/activity, SortDesc, StartIndex, MaxRecords) → Items, TotalCount | scope; Search = Name contains (source name only); IncludeDeleted → deleted rows only (source listDeletedCompanies) for deleteCompanies roles; MaxRecords 25/50/100 else 50 |
Not built: hard delete (owner-only, not in the queue), merge, realtime events, embeddings/workflow events.

### Result S6
- Part A done in the Mentor session (no errors): structures CompanyMatchItem, CompanyItem; private Sales_Context (Foundation_GetMyContext from a server action works in ODC), Sales_RequirePermission, Sales_WriteActivity, Company_RefreshRollups (8 aggregates as in VIEW 0065; counts via LongIntegerToInteger), Company_FindMatches (≤ 2000 candidates, exact list + fuzzy list sorted by score, top 5).
- Part B (FindSimilar, Create, Update) sent.

## S9 Test cases for S8 (deal governance) — mapped to BRD UAT 1–15
Source of the expectations: notes/deal-governance-spec.md §3 (code wins). Actions: Salenix_Sales Deal_Create / Deal_Update / Deal_ChangeStage (S8). Base: workspace currency INR, today = Foundation_TodayFor (Asia/Kolkata), role in brackets, amounts in paise. Each result is (Success, OutcomeCode); "no write" = no Deal/DealAuditLog/DealStageHistory/DealDateChange row changes.
| # | BRD UAT | Steps (exact inputs) | Expected |
|---|---|---|---|
| 1 | Member creates at Lead with a close date | [member] Deal_Create(Name "UAT1", Stage "lead", AmountMinor 100000, Currency "INR", ExpectedCloseDate 2026-12-31) | (False, lead_close_date), FailedRules mentions expected close date, no write |
| 2 | Create at Proposal without close date | [member] Deal_Create(Name "UAT2", Stage "proposal", AmountMinor 100000, MarginBps 2000, ExpectedCloseDate NullDate) | (False, close_date_required), Message "An expected close date is required to be in Proposal." |
| 3 | Create at Qualified without margin | [member] Deal_Create(Name "UAT3", Stage "qualified", AmountMinor 100000, MarginBps 0/empty) | (False, margin_required), FailedRules "Margin % is required from Qualified onwards." (source top message "Please fix the highlighted fields.") |
| 4 | Negotiation → Qualified rollback | deal at negotiation (MarginBps 2000, date 2026-12-31). [member] Deal_ChangeStage(Id, "qualified") then Deal_ChangeStage(Id, "qualified", RollbackReasonCode "budget_lost", RollbackNote "x") | 1st (False, rollback_reason_required), no write. 2nd (True, ok): Stage qualified, RollbackCount +1, DealAuditLog (stage, negotiation→qualified, rollback, budget_lost, "x"), DealStageHistory negotiation closed + qualified open, LastStageChangedAt set, ForecastCategory pipeline, ActivityLog stage_changed |
| 5 | Proposal → Hold | deal at proposal. Deal_ChangeStage(Id, "hold") then Deal_ChangeStage(Id, "hold", HoldReason "timing", HoldRevisitOn 2027-01-15) | 1st (False, hold_reason_required). 2nd (True, ok): HoldReason/HoldRevisitOn set, ForecastCategory omitted, audit stage row change_type edit, RollbackCount unchanged |
| 6 | Push close date 4 times | Deal_Create at qualified with ExpectedCloseDate 2026-12-01 (MarginBps 2000); Deal_Update ExpectedCloseDate → 12-08, 12-15, 12-22, 12-29 | After 3rd: PushCount 3, QuotaWeight 1.00. After 4th: PushCount 4, QuotaWeight 0.50, TotalSlipDays 28, OriginalCloseDate 2026-12-01, LastPushedAt set; 4 DealDateChange rows IsPush True; 4 audit rows pushback |
| 7 | Pull date earlier | continue #6: Deal_Update ExpectedCloseDate → 2026-12-10 | DealDateChange IsPush False; PushCount 4, QuotaWeight 0.50 unchanged; TotalSlipDays 9; audit expected_close_date edit |
| 8 | Member moves to Won | deal at negotiation, date + margin set, DealOrderProof rows INR with Σ SubtotalMinor = AmountMinor ± 100; [member] Deal_ChangeStage(Id, "won") | **Until Salenix_Sales references Approval_Submit: (False, approval_unavailable)**, no write. After the reference: (True, ok), ApprovalStatus pending, ClosedAt set, ForecastCategory closed_won, audit forward_progress, ApprovalRequest pending in Foundation. Without proofs: (False, order_proof) |
| 9 | Manager rejects with a reason | after #8 with reference: [manager] Foundation Approval_Decide(RequestId, "reject", "price") then Sales applies (Deal_ApplyApprovalDecision — not in this queue) | Stage negotiation, ClosedAt null, ApprovalStatus rejected, RollbackCount +1, audit rollback ReasonCode approval_rejected Note "price" — **not testable yet** (apply action not built) |
| 10 | Manager moves to Won | as #8 as [manager] | with reference: ApprovalStatus approved immediately (auto_approved). Without: approval_unavailable |
| 11 | Stage gate min 2 stakeholders | — | **Not built** (stage gates not in S8) |
| 12 | Offerings required | — | **Not built** (offerings not in S8) |
| 13 | Value split 3 years | — | **Not built** (value split not in S8) |
| 14 | FY proration | — | **Not built** |
| 15 | Qualified → Lead on a dated deal | deal at qualified with date. Deal_ChangeStage(Id, "lead") → (Id, "lead", ClearCloseDate True) → (Id, "lead", ClearCloseDate True, RollbackReasonCode "wrong_stage_advance") | 1st (False, needs_close_date_clear); 2nd (False, rollback_reason_required); 3rd (True, ok): ExpectedCloseDate empty, DealDateChange (D → empty, IsPush False), audit date edit + stage rollback, OriginalCloseDate kept, ForecastCategory pipeline |
- Part B done (no errors): Company_FindSimilar, Company_Create, Company_Update (public service actions). Notification_Create from Company_Update is allowed in ODC. Handlers: OnValidationError + AllExceptions (MEMBERSHIP_MISSING from Sales_Context passes through as SalesError with the original message).
- Part C done: Company_SoftDelete, Company_Restore, Company_Get, Company_List; Company_RefreshRollups now called at the end of Create, Update, Restore. Mentor added the persona scope + MergedIntoId filter to SoftDelete/Restore (consistent with the addendum). List: dynamic sort through calculated SortKey/TieKey (dates as minutes since 2000-01-01; null LastActivityAt sorts oldest).
Deviations / open: OwnerName is empty (UserProfile not referenced in Sales → NEEDS REFERENCE: Salenix_Sales → Salenix_Foundation: UserProfile, or a Foundation lookup action). Hard delete, merge, realtime, embeddings not built. Server-side dedup (fix of the source's client trust). NormalizedName recomputed on every update (BRD fix).
Tests for you (DevTools wrappers or the UI app later; Salenix_Sales reference needed):
1. Flag DedupEnabled on. Company_Create("Acme Pvt Ltd", "acme.com", "IT", null, False) → (True, ok), CompanyId; NormalizedName "acme"; ActivityLog created.
2. Company_Create("ACME Limited", …, False) → (False, duplicate_warning), Matches[0] Kind exact. Same with DedupOverride True → (True, ok_dedup_override), activities created + dedup_override.
3. Company_FindSimilar("Acmee") → fuzzy match with Score ≥ 0.55. Flag off → empty.
4. Company_Create("") → (False, validation), FailedRules "Name is required"; 201-char name → "Name must be 200 characters or fewer".
5. As member (sales persona): Company_Create with OwnerId = another user → (False, owner_not_allowed). Company_List as member shows only own + unassigned.
6. Company_Update(id, "Acme Corp", …, OwnerId = manager) as admin → (True, ok); NormalizedName "acme"; ActivityLog owner_changed; Notification "You now own Acme Corp" for the manager.
7. Company_SoftDelete(id) as member → "Permission denied: requires "deleteCompanies" (current role: member)"; as admin → (True, ok); again → (False, not_found "Company not found or already deleted."). Company_List(IncludeDeleted True) as admin shows it; Company_Restore → (True, ok).
8. Company_Get of a deleted id → "Company not found in this tenant."


## S7 Sales Contact actions — PUBLISHED rev 12 (env_app verified, 2026-11-11 08:51 UTC)

### Plan
Source: notes/company-contact-spec.md §B (contacts/actions.ts, lib/contacts/*, lib/dedup/queries.ts:85-149). Same pattern as S6 (Sales_Context, Sales_RequirePermission, Sales_WriteActivity, Company_RefreshRollups).
| Element | Rules |
|---|---|
| ContactMatchItem (Public) | ContactId, Name, Email, CompanyName, Score, Kind |
| ContactItem (Public) | Id, FirstName, LastName, Email, Phone, Title, CompanyId, CompanyName, OwnerId, OwnerName, NormalizedName, CreatedAt, UpdatedAt, DeletedAt |
| Contact_FindMatches(TenantId, FirstName, LastName) private | Norm = NormalizeContactName(FirstName, LastName); Length < 3 → empty; candidates tenant, not deleted, not merged, NormalizedName <> ""; Score = TrigramSimilarity(stored, Norm) only (decision 9 contacts); keep ≥ 0.55; score desc; top 5; Kind "fuzzy" |
| Contact_FindSimilar(FirstName, LastName, Email) | manageContacts; flag DedupEnabled off → empty; EmailMatch (active contact with same lower email) + name Matches |
| Contact_Create(FirstName, LastName, Email, Phone, Title, CompanyId, OwnerId) → Success, OutcomeCode, Message, FailedRules, ContactId, Matches | manageContacts; trims; First/Last ≤ 100, Email valid "Enter a valid email address" ≤ 255 "Email is too long", Phone ≤ 50, Title ≤ 100 (source messages); identity: first, last or email → "Provide a first name, last name, or email."; CompanyId (if set) active in tenant else (False, company_invalid, "The selected company is not in this tenant."); sales persona owner rule (owner_not_allowed); **email uniqueness hard block** (always, independent of the flag): active contact with ToLower(Email) = ToLower(Trim(Email)) → (False, duplicate_email, "A contact with this email already exists: <name>."); Email stored as typed (source); NormalizedName = NormalizeContactName; create; activity created {name}; Company_RefreshRollups(company) |
| Contact_Update(ContactId, same fields) | manageContacts; same validation; scope; company check; **no email-uniqueness check (source)**; NormalizedName recomputed (fix); owner change → owner_changed + notification "You now own <display name>" subtitle "Contact"; else updated; refresh rollups of old and new company |
| Contact_SoftDelete / Contact_Restore | deleteContacts / restoreContacts; messages "Contact not found or already deleted." / "Contact not found in this tenant."; activity deleted/restored; refresh rollups of its company |
| Contact_Get / Contact_List(Search, CompanyId, NoCompany, OwnerFilter, IncludeDeleted, StartIndex, MaxRecords) | scope own+unassigned for sales persona; Search on FirstName, LastName, Email, Title; sort CreatedAt desc, Id desc (source); page 25/50/100 default 50; CompanyName from Company |

## S8 Sales deal governance core — PUBLISHED rev 13 (env_app verified, 2026-11-11 09:08 UTC)

### Plan
Source: notes/deal-governance-spec.md §2 (DB triggers 0045/0058/0060/0071 → server actions; deals/actions.ts). Return shape: Success, OutcomeCode, Message, FailedRules (Text, "; "-joined), DealId, Changed, FromStage, ToStage. Check order = spec 2.c.
Private server actions:
| Element | Rules (spec ref) |
|---|---|
| Deal_ApplyCloseDateChange(TenantId, ActorUserId, DealId, IsInsert, OldDate, NewDate, OldOriginal, OldPushCount, OldLastPushedAt, Reason) → OriginalCloseDate, PushCount, LastPushedAt, TotalSlipDays, IsPush, Changed | G1: insert → Original = NewDate, counters 0, no row; update not distinct → unchanged, no row; Original = Coalesce(OldOriginal, NewDate); IsPush = both set and New > Old; push → PushCount + 1, LastPushedAt now; TotalSlipDays = 0 if New or Original empty else Max(0, DiffDays(Original, New)); DealDateChange row (Reason trim, Left 500) |
| Deal_ApplyGovernance(TenantId, ActorUserId, DealId, OldStage, NewStage, OldDate, NewDate, OldAmountMinor, NewAmountMinor, OldRollbackCount, PushCount, RollbackReasonCode, RollbackNote) → RollbackCount, LastStageChangedAt (or NullDate = unchanged), QuotaWeight, StageChanged | G2: stage distinct → rank; rollback without reason → ValidationError ROLLBACK_REASON_REQUIRED (callers check earlier); rollback → RollbackCount + 1, change type rollback; both ranked → forward_progress; else edit; audit row stage (reason only on rollback, code Left 100, note Left 1000); date distinct → audit expected_close_date (pushback if both set and later, else edit; values YYYY-MM-DD); amount distinct → audit amount_minor edit; QuotaWeight = 0.50 if PushCount > 3 else 1.00. G3: stage distinct → close open DealStageHistory (ExitedAt now) + new open row |
| Deal_CheckOrderProof(TenantId, DealId, AmountMinor, Currency) → Passed, Message | G4 / OPS:46-85: count proofs; any other currency → currency message; Σ SubtotalMinor (deal currency) vs AmountMinor, gap > 100 → less/more message; none → "Attach the client's PO (or other order confirmation) before marking this deal Won." |
Service actions (manageDeals; sales persona scope on links and on the deal; ForecastCategory = Forecast_CategoryFor unless ForecastCategoryOverridden):
| Action | Rules |
|---|---|
| Deal_Create(Name, Description, Stage, AmountMinor, Currency, MarginBps, DealType, ExpectedCloseDate, CompanyId, ContactId, OwnerId, LostReason, HoldReason, HoldRevisitOn, HoldNote, NextStep, Competitor, Source) | shape (name 1–200, description ≤ 2000, stage in 7 codes, amount ≥ 0, currency in USD EUR GBP INR CAD AUD JPY, margin 1–10000 bps when given) → validation_error "Please fix the highlighted fields."; margin_required (Stage_NeedsMargin and MarginBps ≤ 0); lead_close_date; close_date_required; won_on_create; hold at create → hold_reason_required (proposed fix, D4); lost → lost_reason_required (BRD new server rule); links (company/contact in tenant, scope, contact belongs to company) → link_invalid; owner: empty → unassigned; set ≠ actor: member/viewer → owner = actor (source: silently); manager → target owner role refused (owner_assign_forbidden; target role via Foundation not available → **skipped, NEEDS REFERENCE: Membership role lookup**); insert with ForecastCategory, ApprovalStatus none, QuotaWeight 1, ClosedAt now if lost; G1 insert values; DealStageHistory open row; activity created {name, stage}; Company_RefreshRollups |
| Deal_Update(DealId, same editable fields, CloseDateReason) | not_found; hold_via_edit (target hold ≠ current) ; close_date_clear_blocked (proposal/negotiation, date cleared); rollback needs RollbackReasonCode (rollback_reason_required); R0/R1/margin/lost; entering won → order_proof then **approval_unavailable** (Approval_Submit not referenced); staying won with amount/currency change → order_proof; approval patch (leaving won → none); ClosedAt rules; G1 + G2 + G3; activity updated / stage_changed; rollups old + new company |
| Deal_ChangeStage(DealId, Stage, LostReason, HoldReason, HoldRevisitOn, HoldNote, ClearCloseDate, RollbackReasonCode, RollbackNote) | spec 2.b-2 order: hold_reason_required → needs_close_date_clear → not_found → no_change → rollback_reason_required → close_date_required → margin_required (MarginBps null) → (gates not built) → won: order_proof then approval_unavailable → lost_reason_required → write (target lead clears date; hold fields; LostReason; ClosedAt; approval none when leaving won) → G1/G2/G3 → activity stage_changed → rollups |
Not built in S8: stage gates, offerings, custom fields, Deal_Patch, approve/reject apply, notifications (won/lost to owner), realtime, score recompute queue, agent observations.

### Result S7
- Part A done (no errors): ContactMatchItem, ContactItem, Contact_FindMatches (TrigramSimilarity only, ≥ 0.55, top 5, Norm ≥ 3 chars), Contact_FindSimilar, Contact_Create (email uniqueness hard block always), Contact_Update (no email check; NormalizedName recomputed; owner_changed + notification; rollups old/new company).
- Permission arguments verified (RoleCode = Sales_Context.RoleCode, PermissionCode = "manageContacts" in all three) — the report text was only written in the other order; nothing changed.
- Part B done: Contact_SoftDelete, Contact_Restore, Contact_Get, Contact_List (Search on first/last/email/title, CompanyId, NoCompany, scope, OwnerFilter, CreatedAt desc + Id; deleted list by DeletedAt).
Open: OwnerName empty (UserProfile reference); no email re-check on update/restore (source).
Tests for you: 1. Contact_Create("Ravi", "Kumar", "Ravi@Acme.com", "", "", <Acme id>, null) → (True, ok); Acme ContactCount 1. 2. Same email in other case → (False, duplicate_email "A contact with this email already exists: Ravi Kumar."). 3. Contact_Create("", "", "", …) → (False, validation, "Provide a first name, last name, or email."). 4. CompanyId of another workspace / deleted company → company_invalid. 5. Contact_FindSimilar("Ravi", "Kumaar", "") with DedupEnabled → fuzzy match ≥ 0.55; flag off → empty. 6. Contact_Update moving the contact to another company → both companies' ContactCount refreshed. 7. Contact_SoftDelete as member → permission denied (deleteContacts); as admin → ok, ContactCount −1; Contact_Restore → +1. 8. Contact_List(Search "kum") finds Ravi; as member only own + unassigned.

### Result S8
- Part A done (no errors): Deal_ApplyCloseDateChange (G1), Deal_ApplyGovernance (G2 audit rows stage/date/amount, G3 stage history, QuotaWeight 0.50 only when PushCount > 3), Deal_CheckOrderProof (G4 messages; money formatted "<CUR> 1,234.56", JPY 0 decimals). Node text checked.
- Part B (Deal_Create) sent. Decisions applied: create at Hold without reason allowed (source; D4 open); lost reason required on the server (BRD; source UI only); owner assignment uses permission assignDeals (decision 6; source: only owner/manager), manager cannot assign to an owner (Membership role lookup — Membership is referenced, so no extra reference needed).
- Part B done: Deal_Create (public). Node text checked: validation order permission → shape (+margin) → lead_close_date → close_date_required → won_on_create → lost_reason_required → links → owner → G1 insert → CreateDeal → stage history → activity → rollups.
  **Gap:** the Salenix_Foundation Membership entity is visible in Sales with Id only → manager-cannot-assign-to-owner and "Pick an owner" (active membership) checks NOT built (Comment node in the flow). NEEDS REFERENCE: Salenix_Sales → Salenix_Foundation: Membership attributes UserId, Role, Status (reference refresh), or a Foundation service action Member_GetRole(UserId).
- Part C (Deal_ChangeStage) sent; Won path returns approval_unavailable (no Approval_Submit reference).
- Part C done: Deal_ChangeStage (public). Checks: permission → shape ("Invalid stage transition.") → hold_reason_required → not_found → needs_close_date_clear → no_change → rollback_reason_required → close_date_required → margin_required → won: order_proof / approval_unavailable → lost_reason_required → write (G1, G2, G3, ClosedAt, LostReason, Hold fields, approval none, forecast re-derive unless overridden) → activity stage_changed → rollups.
- Part D done: Deal_Update (public). Checks: permission → shape/margin → lead_close_date → close_date_required → lost_reason_required → links → owner → not_found → hold_via_edit → close_date_clear_blocked → rollback_reason_required → won: order_proof / approval_unavailable; staying Won with changed amount/currency → order_proof → write (G1 with CloseDateReason, G2 with amount audit, all fields, ClosedAt, approval none unless staying Won, forecast re-derive only on stage change) → activity → rollups old + new company. Output named **UpdatedDealId** (platform renames an output that clashes with the DealId input). Mentor hit its memory limit once and rebuilt in smaller steps (no leftovers).
Deviations / decisions for you: create at Hold without reason allowed (source; D4); lost reason required on the server (BRD); owner via assignDeals (decision 6); manager→owner and active-membership owner checks missing (Membership attributes not visible); Won path returns approval_unavailable until Approval_Submit is referenced; stage gates, offerings, custom fields, Deal_Patch, approve/reject apply, won/lost notifications, score/risk recompute not built.
Tests: see S9 (UAT 1–8, 10, 15 runnable now except the approval part of 8/10).


## Summary
| Batch | App | Revision | Status |
|---|---|---|---|
| App map | CLAUDE.md | – | done (commit 2cef2bf) |
| T1 | Salenix_Theme | 3 | done before the run |
| H1 | Salenix_Foundation | 20 | verified, no change |
| H2 | Salenix_Foundation | 21 | published |
| W1 | Salenix_Foundation | 22 | published |
| W2 | Salenix_Foundation | 23 | published |
| F8 | Salenix_Foundation | 24 | published |
| C1 | Salenix_Sales | 5 | published |
| S1–S4 | Salenix_Sales | 6–9 | published (22 entities) |
| S5 | Salenix_Sales | 10 | published |
| S6 | Salenix_Sales | 11 | published |
| S7 | Salenix_Sales | 12 | published |
| S8 | Salenix_Sales | 13 | published |
| S9 | report | – | written |
Every publish: revision checked before, env_app verified after, Mentor session closed after. No Production, GL_*, Salenix_Crypto, Salenix_Theme or Setting value touched. Nothing existing deleted or renamed (C1 removed only template elements, as the queue asked).

## Decisions waiting for you
1. TenantId delete rule is Ignore on every domain entity (ODC does not allow Delete across apps) → how should workspace deletion clean Sales data (event + Sales cleanup action)?
2. Approval engine design (W1/W2) is new — the source has only Deal.approval_status and DealCreditTerm approved_by/at. Keep the generic engine (Sales keeps its own status columns) or drop it and follow the source exactly?
3. Reject reason required (queue) vs optional (source).
4. D4 create at Hold without reason (source allows); D5 hold revisit date in the past; D7 unassigned deals by members; D16 Hold and the stall flag; D19 margin cleared on save at Lead/Hold/Lost (notes/deal-governance-spec.md §4).
5. Invitation_Accept contains an inline copy of EnsureMyProfile — extract a shared private action (changes the tested EnsureMyProfile)?
6. Contact email uniqueness only on create (source) — also on update/restore?
7. AppBaseUrl value (Setting) for invitation links.

## Manual steps for you (in this order)
1. **Studio, Salenix_Foundation (fresh copy of rev 24):** check OrgInvitation indexes — add TokenHash unique and (TenantId, Email) if missing. Check delete rules/indexes of the new approval entities and F8 entities.
2. **ODC Portal → Salenix_Foundation → Settings:** set AppBaseUrl.
3. **Studio, Salenix_Sales (fresh copy of rev 13), reference change only:** refresh Salenix_Foundation and add Approval_Submit, Approval_Cancel, Approval_Decide, Approval_Get, Approval_ListPending, ApprovalItem, ApprovalActionItem, UserProfile (for OwnerName), Membership attributes (UserId, Role, Status). Clear the app property "Global Exception Handler" (points to the deleted Common\OnException). Publish from Studio.
4. Then tell me to wire: Won approval (replace the approval_unavailable branches), manager/owner assignment check, OwnerName.
5. **DevTools:** reference refresh (Foundation rev 24 + Salenix_Sales) and Dev_* wrappers for Invitation_*, Approval_*, Company_*, Contact_*, Deal_*, Sales_StageSelfCheck — then run the tests in each batch section (H1, H2, W2, S5–S9).
6. `git push` (commits are local only).

---
# Follow-up after review (2026-11-11)

## X1 READ-ONLY index audit — DONE (no change)
Mentor listed every index of every entity in Salenix_Foundation (rev 24) and Salenix_Sales (rev 13); compared with CLAUDE.md F1 spec, MORNING-REPORT.md (F2/F3/E2) and the W1/F8/S1–S4 plans.
| App | Entity | Expected (spec) | Found | Status |
|---|---|---|---|---|
| Foundation | Tenant | Slug unique; CreatedAt; LegacyUuid | all 3 | OK |
| Foundation | UserProfile | UserId unique; Email unique; LegacyUserId | all 3 | OK |
| Foundation | Membership | (TenantId, UserId) unique; UserId; (TenantId, Role); LegacyUuid | all 4 + auto TenantId | OK |
| Foundation | OrgInvitation | TokenHash unique; Email; (TenantId, Email); LegacyUuid | all 4 + auto TenantId | OK — **the H2 note "no indexes" was a wrong Mentor statement; corrected here** |
| Foundation | LookupValue / MasterData / RolePermission | uniques (Category, Code) / (TenantId, Type, Code) / (RoleCode, PermissionCode) | present (+ sort indexes) | OK |
| Foundation | OrganizationSettings, CustomFieldDefinition, Notification, FileAttachment | MORNING-REPORT specs | all present | OK |
| Foundation | ApprovalRule/Request/Action, RealtimeEvent, WorkflowRun, ChartView, DashboardWidget | W1/F8 plans | all present | OK |
| Sales | all 22 entities | S1–S4 plans | all present (uniques: CustomFieldValue, DealContact, Offering, DealOffering, DealValueSplit, DealRiskScore, StageGate) | OK |
Missing or wrong: **none**. Optional (not in our specs, present in the source): Deal (TenantId, HealthBand); Deal (TenantId, PushCount/RollbackCount/QuotaWeight) — source partial indexes; ActivityLog LegacyUuid and LegacyUuid on child tables (DealAuditLog, DealDateChange, DealStageHistory, DealScore, DealRiskScore, ForecastSnapshot, DealContact, DealOffering, DealValueSplit, DealOrderProof) — only needed if the migration looks children up by LegacyUuid. Not added unless you ask.
Platform: no automatic index on cross-app references (TenantId in Sales, User Identifier anywhere) — our explicit (TenantId, …) indexes cover the queries.

## X2 Foundation — PUBLISHED rev 25 (env_app verified, 2026-11-11 09:25 UTC)
| Element | Old → New |
|---|---|
| Foundation_EnsureProfileCore (new, private) | profile logic moved here unchanged (sign-in check → MEMBERSHIP_MISSING "You are not signed in."; existing profile → Created False; else User email/username lower-trim, name split, 100-char truncation, CreateUserProfile → Created True); new output ProfileId; AllExceptions re-raises FoundationError with the original message |
| Foundation_EnsureMyProfile | inline logic → one call to EnsureProfileCore; output and handlers unchanged (unexpected errors still logged + generic message) |
| Invitation_Accept | inline profile block + comment + 5 locals → one call to EnsureProfileCore at the same place; all other nodes unchanged |
| Invitation_Create AcceptLink | AppBaseUrl + "/accept-invite?token=" + Token → AppBaseUrl + "/AcceptInvite?token=" + Token |
| UserProfile | Public No → Yes |
| Tenant | + DeletedAt (Date Time, optional, no index) |
| Member_GetRole (new, public service) | ResolveContext; Membership of the acting tenant for TargetUserId → IsMember, RoleCode, Status (lower-case); handlers as Member_Remove |
Indexes: none missing (X1). Validation: 0 errors.
Invitation_Accept handler check (after review): OnValidationError / OnMembershipMissing re-raise unchanged; AllExceptions (Log Error = False) logs the original and raises "FOUNDATION_ERROR: Something went wrong. Please try again." — a FoundationError from EnsureProfileCore already ends there, so no change was needed (my earlier remark that the original message would reach the caller was wrong).
Pending (not built, recorded): indexes Deal (TenantId, HealthBand) and Deal (TenantId, PushCount/RollbackCount/QuotaWeight) for the At-risk list and reports.
Tests X2: 1. Foundation_EnsureMyProfile for a new user → Created True, then False. 2. Invitation_Create → AcceptLink "https://vtpl-dev.outsystems.app/Salenix/AcceptInvite?token=…". 3. Invitation_Accept for a user without a profile → profile created, membership active. 4. Member_GetRole(member id) → IsMember True, RoleCode member, Status active; a user of another workspace → IsMember False.

## X3 Sales — PUBLISHED rev 14 (env_app verified, 2026-11-11 09:30 UTC)
| Element | Change |
|---|---|
| Deal_Create | after lost_reason_required: if hold → Foundation_TodayFor; reason or revisit empty → hold_reason_required "Moving to Hold needs a reason and a revisit date."; revisit < today → hold_revisit_past "The revisit date must be today or later." |
| Deal_ChangeStage | after hold_reason_required (before loading the deal): if hold → TodayFor; revisit < today → hold_revisit_past |
| Deal_Update | after hold_via_edit: if staying in hold → EffReason/EffRevisit (input or stored) empty → hold_reason_required; a newly set revisit date (≠ stored) < today → hold_revisit_past (an existing overdue date does not block other edits) |
| Contact_Update | after the company check: active contact with the same email (excluding this one) → duplicate_email |
| Contact_Restore | before clearing DeletedAt: same check on the stored email → duplicate_email (no FailedRules output in this action) |
| Indexes | IX_<Entity>_LegacyUuid (non-unique) on ActivityLog, DealAuditLog, DealDateChange, DealStageHistory, DealScore, DealRiskScore, ForecastSnapshot, DealContact, DealOffering, DealValueSplit, DealOrderProof |
Choices D7/D16/D19 (follow the source, no BRD rule): D7 a deal created without owner stays unassigned (also by members); D16 stall flag not built yet (when built, Hold counts as active, as in the source); D19 Deal_Update writes MarginBps from the input (0 = empty), so a full save at Lead/Hold/Lost with an empty margin clears it, as in the source.
Pending (not built): indexes Deal (TenantId, HealthBand), Deal (TenantId, PushCount/RollbackCount/QuotaWeight) for the At-risk list and reports.
Validation: 0 errors.
Deal_Update choice approved (CLAUDE.md 21i). Pending UI "Revisit due list" (21j).
Tests X3: 1. Deal_Create(Stage "hold", no HoldReason) → hold_reason_required; HoldRevisitOn yesterday → hold_revisit_past; today → ok. 2. Deal_ChangeStage(id, "hold", "timing", yesterday) → hold_revisit_past. 3. Deal_Update of a Hold deal with an empty HoldReason input → ok (stored reason kept); new HoldRevisitOn yesterday → hold_revisit_past; unchanged overdue date → ok. 4. Contact_Update setting the email of another active contact → duplicate_email. 5. Delete contact A, create B with A's email, Contact_Restore(A) → duplicate_email.

## Studio reference changes (manual, reference-only publishes)
**1. Salenix_Sales (fresh copy of rev 14) → Manage Dependencies → Salenix_Foundation (rev 25)** — keep everything already referenced, add:
- Service actions: Approval_Submit, Approval_Decide, Approval_Cancel, Approval_Get, Approval_ListPending, Member_GetRole
- Structures: ApprovalItem, ApprovalActionItem
- Entity: UserProfile (read-only)
Publish from Studio (reference change only). The global exception handler property is left for S10 (Mentor first; Studio only if Mentor still cannot clear it).

**2. Salenix_DevTools (fresh copy of rev 8) → Manage Dependencies**
- Salenix_Foundation (rev 25): Invitation_Create, Invitation_ListPending, Invitation_Revoke, Invitation_GetByToken, Invitation_Accept, InvitationItem; Approval_Submit, Approval_Decide, Approval_Cancel, Approval_Get, Approval_ListPending, ApprovalItem, ApprovalActionItem; Member_GetRole; Foundation_TodayFor, Foundation_NowFor (keep the existing ones).
- Salenix_Sales (rev 14, after step 1 its revision will be 15): Sales_StageSelfCheck; Company_FindSimilar, Company_Create, Company_Update, Company_SoftDelete, Company_Restore, Company_Get, Company_List, CompanyItem, CompanyMatchItem; Contact_FindSimilar, Contact_Create, Contact_Update, Contact_SoftDelete, Contact_Restore, Contact_Get, Contact_List, ContactItem, ContactMatchItem; Deal_Create, Deal_Update, Deal_ChangeStage.
Publish from Studio (reference change only).

## DevTools test sections (TestConsole, one Dev_* server wrapper per call, outputs shown raw + last error text)
| Section | Calls | Covers |
|---|---|---|
| T1 Time | TodayFor / NowFor (active workspace) | H1 |
| T2 Invitations | Create (Email, Role) → AcceptLink; ListPending table; Revoke(Id); GetByToken(Token); Accept(Token) | H2, X2 tests |
| T3 Members | Member_GetRole(TargetUserId text) | X2 |
| T4 Approvals | Submit(SubjectType, SubjectId, Label); ListPending table; Decide(RequestId, Decision, Reason); Cancel(SubjectType, SubjectId); Get(RequestId) + actions table | W2 tests |
| T5 Stage self-check | Sales_StageSelfCheck → Passed, FailedCount, Report | S5 |
| T6 Companies | FindSimilar; Create (Name, Domain, Industry, Owner, DedupOverride) → outcome + matches table; Update; SoftDelete; Restore; Get; List (Search, OwnerFilter, IncludeDeleted, SortBy, SortDesc, page) | S6 |
| T7 Contacts | same set for contacts (+ CompanyId, NoCompany filters) | S7, X3 |
| T8 Deals | Create (all fields); ChangeStage (Stage, LostReason, HoldReason, HoldRevisitOn, ClearCloseDate, RollbackReasonCode/Note); Update (all fields + RollbackReason + CloseDateReason) → Success/OutcomeCode/Message/FailedRules | S8, S9 (UAT 1–7, 15), X3 |
Gaps for full S9 verification (proposed for S10, read-only): **Deal_Get** (DealItem incl. governed fields PushCount, TotalSlipDays, OriginalCloseDate, QuotaWeight, RollbackCount, ApprovalStatus, ForecastCategory, ClosedAt, Hold*) and **Deal_GetHistory** (DealAuditLog, DealDateChange, DealStageHistory rows) — without them DevTools can only see outcome codes. UAT 8/10 success paths also need an order-proof add action (DealOrderProof + FileAttachment), not built yet.

## S10 Sales — Won approval, approve/reject, owner checks, read actions, order proofs — PUBLISHED rev 16 (env_app verified, 2026-11-11 10:18 UTC)
Pre-check: Salenix_Sales rev 15 deployed (Studio reference publish, 2026-11-11 09:34 UTC). One Mentor session, several prompts, one publish after the report is approved.

### Plan
Cross-app order (Foundation commits on its own): checks first → Approval_Submit right before the Deal write (idempotent: retry returns already_pending) → Deal write → Approval_Cancel as the LAST node (a raise there rolls back the Sales write, so deal and request stay consistent).
| Element | Rules |
|---|---|
| Deal.ApprovalRequestId (new attribute, Long Integer, optional, default 0) | id of the Foundation ApprovalRequest of the current Won entry (cross-app id, no FK). Needed by Deal_Approve/Reject to call Approval_Decide. |
| Deal_ChangeStage / Deal_Update, entering Won | replace approval_unavailable: Approval_Submit("deal_won", DealId, Deal.Name) → Status approved (auto_approved / not_required) → ApprovalStatus approved, ApprovedBy = actor, ApprovedAt = now; pending → pending, ApprovedBy/At empty; ApprovalRequestId = RequestId (0 when not_required). Source APR:41-54. |
| Deal_ChangeStage / Deal_Update, leaving Won | existing patch (none, empty by/at) + ApprovalRequestId 0; if the old ApprovalStatus was pending → Approval_Cancel("deal_won", DealId) as the last node. |
| Deal_Approve(DealId) → Success, OutcomeCode, Message, FailedRules, DealId | approveDeals; deal in tenant, not deleted, else not_found "Deal not found."; Stage won AND ApprovalStatus pending else (False, approval_unavailable, "This deal is not awaiting approval."); ApprovalRequestId > 0 → Approval_Decide(RequestId, "approve", "") (approved or already_decided both continue: apply-again path); = 0 (legacy) → apply locally; apply: ApprovalStatus approved, ApprovedBy actor, ApprovedAt now, UpdatedBy/At; activity deal_approved {name}. ACT:1638-1716. |
| Deal_Reject(DealId, Reason) | approveDeals; Reason trim required "Enter a reason for the rejection." (decision 21c), Left 500; same guard; Approval_Decide(RequestId, "reject", Reason) (rejected / already_decided continue); apply: Stage negotiation, ClosedAt empty, ApprovalStatus rejected, ApprovedBy/At = rejecter, UpdatedBy/At; Deal_ApplyGovernance(won → negotiation, RollbackReasonCode approval_rejected, RollbackNote Reason) → RollbackCount + 1, audit rollback, DealStageHistory, LastStageChangedAt, QuotaWeight; ForecastCategory NOT re-derived (source ACT:1773-1782, D10); activity deal_rejected {name, reason}; Company_RefreshRollups. ACT:1723-1820. |
| Owner checks (Deal_Create, Deal_Update) | replace the Comment node: OwnerId set and ≠ actor → Member_GetRole(OwnerId): not IsMember or Status ≠ active → owner_invalid "Pick an owner who is an active member of this workspace." (new, not in source); acting role manager and target role owner → owner_assign_forbidden "Managers cannot assign deals to an owner." (ACT:396, 751). |
| OwnerName | CompanyItem / ContactItem / DealItem: UserProfile of OwnerId → Trim(FirstName + " " + LastName), else Email, else "". |
| Deal_Get(DealId) → Item (DealItem), Proofs (DealOrderProofItem List) | any active member; scope (sales persona own + unassigned); not deleted; DealItem = all Deal attributes + CompanyName (Company), OwnerName, ProofCount, ProofSubtotalMinor. |
| Deal_GetHistory(DealId) → AuditRows, DateChanges, StageHistory | same access as Deal_Get; CreatedAt desc (stage history EnteredAt desc); ActorName via UserProfile. |
| DealOrderProof_Add(DealId, ProofType, ReferenceNo, IssuedOn, BuyerName, Currency, SubtotalMinor, TaxMinor, TotalMinor) → Success, OutcomeCode, Message, FailedRules, ProofId | source order-proof-actions.ts:118-330 + checks.ts: manageDeals; ProofType in the 6 codes; ReferenceNo trim 1–120 "Enter the PO / order number"; BuyerName ≤ 200; SubtotalMinor > 0 "Enter the value before tax"; TaxMinor ≥ 0; TotalMinor 0 → Subtotal + Tax; Total < Subtotal → "The total can’t be less than the value before tax."; deal not found "Deal not found in this tenant."; currency ≠ deal → "This deal is in X and the PO is in Y. Change the deal's currency to Y first, then submit the PO."; same reference on the deal (Trim+ToLower) → "PO R is already attached to this deal."; Won deal and Σ subtotal after > Amount + 100 → "This deal is Won at …; with this PO its POs would come to … before tax. Move the deal out of Won to change its value."; same reference on another non-deleted deal of the same company → "PO R is already attached to "N" for this client. One PO can back one deal."; BLOCKING date checks (today = Foundation_TodayFor): empty → "Enter the PO date as printed on the PO."; > today → "The PO is dated D, which is in the future."; > 180 days old → "The PO is dated D, more than 6 months ago."; before deal CreatedAt date → "The PO (D) is older than this deal, which was created on C." (blocks joined by a space, OutcomeCode po_date_blocked); insert (DocumentId 0, SubmittedBy actor); activity updated {name, field order_proof, op submitted, referenceNo, subtotalMinor, currency}. |
| DealOrderProof_Remove(ProofId, DealId) | manageDeals; deal not found; Won → "A Won deal keeps the PO it was won on. Move the deal out of Won first to replace it."; proof of this deal in tenant else "That PO is no longer on this deal."; delete; activity updated {op removed, referenceNo}. No update action exists (append-only). |
| Global Exception Handler | (none), if Mentor can set it. |
Not built (recorded): warning checks (buyer_missing/buyer_mismatch/supplier_mismatch/not_an_order/totals_mismatch) with confirmation, file upload + FileAttachment, PDF reading, bulk approve/reject, realtime, won/lost notifications.
Deviations: deal created-on date = UTC date of CreatedAt (source: UTC too), today = workspace date (decision 7, source UTC); ProofType list in the action (source CHECK) until LookupValue is readable in Sales; owner active-member check is new; reject reason required (21c).

### Result S10 (Mentor session 15cf3af4, prompts A1–A7, node text read back after each)
| # | Element | Old → New |
|---|---|---|
| A1 | Deal | + ApprovalRequestId (Long Integer, optional, default 0) |
| A1 | Deal_ChangeStage | approval_unavailable Assign/End + NEEDS REFERENCE comment → Approval_Submit("deal_won", deal Id, Name) → not Success → approval_failed (Message from Foundation) → WonApprovalStatus (approved/pending), WonRequestId → normal write path (lost check → GetDealForUpdate). Main Assign: ApprovalStatus = If(won, WonApprovalStatus, "none"); ApprovedBy/At only when won + approved; ApprovalRequestId = If(won, WonRequestId, 0). New locals OldApprovalStatus, OldStageBeforeWrite. Last node: If old stage won and new ≠ won and old status pending → Approval_Cancel("deal_won", Id) |
| A2 | Deal_Update | same wiring; Approval_Submit is the last check before GetDealForUpdate (after the staying-Won proof check branch); staying Won keeps ApprovalStatus/By/At/RequestId; leaving Won → none/empty/0; Approval_Cancel as the last node (OldStage = won, S ≠ won, old status pending) |
| A3 | Deal_Approve(DealId) (new, public) | approveDeals → not_found "Deal not found." → approval_unavailable "This deal is not awaiting approval." → ApprovalRequestId > 0: Approval_Decide(approve) (approved/already_decided continue; else approval_failed) → GetDealForUpdate + re-check → approved, ApprovedBy/At, UpdatedBy/At → activity deal_approved → ok "Deal approved." |
| A3 | Deal_Reject(DealId, Reason) (new, public) | approveDeals → reason_required "Enter a reason for the rejection." (Substr 500) → not_found / approval_unavailable → Approval_Decide(reject, reason) → lock + re-check → Deal_ApplyGovernance(won → negotiation, approval_rejected, reason) → Stage negotiation, ClosedAt empty, rejected, ApprovedBy/At = rejecter, RollbackCount/QuotaWeight/LastStageChangedAt from governance → activity deal_rejected {name, reason} → Company_RefreshRollups → ok. ForecastCategory, ApprovalRequestId, Hold/Lost untouched (source) |
| A4 | Deal_Create, Deal_Update | Comment removed → Member_GetRole(OwnerId) in the assignDeals branch: not member / not active → owner_invalid "Pick an owner who is an active member of this workspace."; manager → owner → owner_assign_forbidden "Managers cannot assign deals to an owner." (FailedRules "Pick a non-owner teammate") |
| A4 | Company_Get, Contact_Get | OwnerName "" → GetOwnerProfile (UserProfile by UserId, only when OwnerId set) → Trim(First + " " + Last) else Email; comment removed |
| A4 | Company_List, Contact_List | + UserProfile left join on OwnerId (UserId is unique → row counts unchanged); OwnerName same rule; filters/sort/paging unchanged |
| A5 | DealItem, DealOrderProofItem, DealAuditItem, DealDateChangeItem, DealStageItem (new, public) | DealItem = all Deal attributes (no TenantId; Id/CompanyId/ContactId Long Integer) + CompanyDisplayName, OwnerName, ProofCount, ProofSubtotalMinor |
| A5 | Deal_Get(DealId) (new, public) | any active member; tenant + not deleted + sales-persona scope; not found → ValidationError "VALIDATION_ERROR: Deal not found."; Proofs CreatedAt asc (max 1000); ProofSubtotalMinor = Σ subtotal in the deal currency |
| A5 | Deal_GetHistory(DealId) (new, public) | same access; AuditRows / DateChanges ChangedAt desc (500), StageHistory EnteredAt desc (200); names via UserProfile |
| A6 | Money_Format(AmountMinor, Currency) (new, private, Function) | extracted from Deal_CheckOrderProof ("<CUR> 1,234.56", JPY 0 decimals); Deal_CheckOrderProof messages unchanged (Divisor/Digits locals left unused) |
| A6 | DealOrderProof_Add (new, public) | as planned: validation (first message + all in FailedRules) → not_found → currency_mismatch → duplicate_reference → won_value_exceeded → reference_used → po_date_blocked (4 blocking date checks, today = Foundation_TodayFor) → insert (DocumentId 0) → activity updated {order_proof submitted} → ok, ProofId |
| A6 | DealOrderProof_Remove (new, public) | not_found → won_locked → proof_not_found → delete → activity updated {order_proof removed} → ok |
| A7 | Global Exception Handler | **not possible via Mentor**: property already reads empty, warning "Common\OnException" stays → clear by hand in Studio (reference-only style publish) |
Validation: 0 errors; warnings: Common\OnException, unused FailedRules output in Deal_Approve and DealOrderProof_Remove, 20 font offline-cache.
Handlers on all new actions: OnValidationError re-raise unchanged; AllExceptions (Log Error = False) LogMessage + SalesError (codes SALES_ERROR:/MEMBERSHIP_MISSING: pass through, else generic).
Notes: Approval_Submit/Decide commit in Foundation before the Sales write; a failed Sales write leaves the request pending/decided and a retry is idempotent (already_pending / already_decided). Owner checks run whenever OwnerId ≠ actor (also an unchanged owner on update, like the source's manager check); an owner who left the workspace blocks edits by an assigner until the deal is reassigned. Deal_Approve on a legacy pending deal without a request (ApprovalRequestId 0) applies locally.
| A8 | Deal_Reject | + Rec.ForecastCategory = If(Rec.ForecastCategoryOverridden, Rec.ForecastCategory, Forecast_CategoryFor("negotiation")) (CLAUDE.md 22a) |
| A8 | Deal_Update | owner checks moved after FindDeal/not_found, behind If FinalOwner <> empty and <> actor and <> stored OwnerId; Member_GetRole(FinalOwner); HasPermission True branch → FindDeal again. Deal_Create unchanged (CLAUDE.md 22b) |
Review decisions (user, 2026-11-11): A8 changes; pending order-proof warnings + file upload (CLAUDE.md 22d). Validation 0 errors. Published rev 16 (deployment cf2be92b), session closed.
Manual: Studio, Salenix_Sales fresh copy of rev 16 → app property Global Exception Handler = (none) → publish from Studio (nothing else).

## DevTools reference list (FINAL, after S10) — Salenix_DevTools (fresh copy of rev 8) → Manage Dependencies, keep the existing ones
- Salenix_Foundation (rev 25): Invitation_Create, Invitation_ListPending, Invitation_Revoke, Invitation_GetByToken, Invitation_Accept, InvitationItem; Approval_Submit, Approval_Decide, Approval_Cancel, Approval_Get, Approval_ListPending, ApprovalItem, ApprovalActionItem; Member_GetRole; Foundation_TodayFor, Foundation_NowFor.
- Salenix_Sales (rev 16, or 17 after the handler publish): Sales_StageSelfCheck; Company_FindSimilar, Company_Create, Company_Update, Company_SoftDelete, Company_Restore, Company_Get, Company_List, CompanyItem, CompanyMatchItem; Contact_FindSimilar, Contact_Create, Contact_Update, Contact_SoftDelete, Contact_Restore, Contact_Get, Contact_List, ContactItem, ContactMatchItem; Deal_Create, Deal_Update, Deal_ChangeStage, Deal_Approve, Deal_Reject, Deal_Get, Deal_GetHistory, DealOrderProof_Add, DealOrderProof_Remove; DealItem, DealOrderProofItem, DealAuditItem, DealDateChangeItem, DealStageItem.
Publish from Studio (reference change only).

---
# Archive: former CLAUDE.md "Foundation build plan" (moved 2026-11-11, text unchanged)
- F1 Tenant, UserProfile, Membership, OrgInvitation (entities only)  — DONE (rev 4, 2026-11-10)
- F2 LookupValue, MasterData (TenantId), RolePermission + create-only seed — F2a entities DONE (rev 5); F2b-1 RolePermission seed + helpers + timer DONE (rev 6, 2026-11-10; test passed: run 1 created=74 existing=0, run 2 created=0 existing=74); F2b-2 Seed_Lookups_Core (14 categories, 59 rows) DONE (rev 8, 2026-11-10; test passed: run 1 RolePermission created=0 existing=74, LookupCore created=59 existing=0; run 2 LookupCore created=0 existing=59; rev 7 = user publish from Studio, nothing lost); F2b-3 Seed_Lookups_DealFinance (13 categories, 47 rows) + Bootstrap_Run* runners (commit per batch, failure isolation) DONE (rev 9, 2026-11-10; test passed, no FAILED lines); Batch A = F2b-4 Seed_Lookups_CommsAi (17 cat, 76 rows) + F2b-5 Seed_Lookups_Settings (10 cat, 58 rows) + 2 runners DONE (rev 10, 2026-11-10; test passed, no FAILED lines); Batch B = template UI cleanup (screens, blocks, template actions, flows, EmailTheme, Charts/Maps refs, settings TenantId/TenantName; 3 pointers set to none) + Seed_Lookups_IndianState (37 rows from india-geo.ts) + Seed_Lookups_OrderProof (7 rows) + CommsAi 75 rows (risk_explanation removed) DONE (rev 11, 2026-11-10; test passed: run 2 RolePermission 74, LookupCore 59, LookupDealFinance 47, LookupCommsAi 75, LookupSettings 58, LookupIndianState 37, LookupOrderProof 7, all existing, no FAILED lines); Batch C (F3) OrganizationSettings + CustomFieldDefinition entities published (rev 12, 2026-11-11, Studio check pending; see MORNING-REPORT.md); Batch D (F4) Foundation_EnsureMyProfile / GetMyContext / HasPermission / GetFlags service actions + private Foundation_ResolveContext + exceptions MembershipMissing, FoundationError; Tenant Public = Yes (rev 13, 2026-11-11, tested); Foundation_TodayFor pending (C# library); Batch E1 (F5) Workspace_ListMine / SetActive / Create + MasterData_EnsureRow + WorkspaceItem (rev 14) and E2 (F7) Notification entity + Notification_Create / ListMine / MarkRead / MarkAllRead + NotificationItem + FileAttachment entity (rev 15) published 2026-11-11, tested; Batch F (F6 partial) Member_ChangeRole + Member_Remove + private Foundation_CheckPermission published (rev 16, 2026-11-11, tested); invitations pending (hashing library + email). Follow-up G published (rev 17, 2026-11-11): optional Notification inputs, exception codes, Workspace_Provision (private) + Workspace_CreateFirst, Settings DefaultWorkspaceOwnerEmail + BootstrapOwnerEmail (secret, value set in Portal), active-only last-admin count, Log Error = False on LogMessage handlers, 11 unused variables removed from Workspace_Create. Read-only review vs FR-TEAM-05/06 and BRD #3/#4 passed. DevTools rev 4 (TestConsole + 13 Dev_* wrappers) and Foundation rev 18 (Setting DevToolsEnabled + DevOnly_AddMember, see decision 19) published 2026-11-11; DevTools rev 5 = reference refresh from Studio (DevOnly_AddMember added, Notification_Create removed); DevTools rev 6 = DevOnly_AddMember section on TestConsole (published 2026-11-11). DevTools tests PASSED 2026-11-11: Batches D, E, F tested (details in MORNING-REPORT.md). External library Salenix_Crypto BUILT 2026-11-11 (code in crypto/, plan SALENIX-CRYPTO-PLAN.md, .NET 10, SDK 1.7.0, NodaTime 3.3.7, 13 actions, 84 unit tests passed, zip crypto/dist/Salenix_Crypto.zip, NOT uploaded; trigram expected values come from a pg_trgm port: replace them with real Postgres values when the user gives them). Salenix_Crypto rev 1 uploaded + published by the user; DevTools rev 7 = Crypto reference (Studio), rev 8 = TestConsole section "Crypto smoke test" + Dev_Crypto_SmokeTest (MCP publish 2026-11-11). Crypto smoke test PASSED in ODC 2026-11-11: ConvertUtcToZone exactly +5:30, TodayIn 2026-11-11, SHA-256, token 43 chars, trigram 0.45454547 / 0.625, normalizer [acme]. Batch H plan APPROVED 2026-11-11 (H1 TodayFor/NowFor + private TimezoneFor; H2 invitations; H3 DevTools rev 9). H decisions: (a) unknown timezone → fallback Asia/Kolkata + LogMessage, no raise; (b) Invitation_Create deletes older pending invites for the same email in the same tenant; (c) "already a member" counts active memberships only (suspended members can be re-invited, accept reactivates); (d) Invitation_Accept grants ODC role SalenixUser if ODC allows it, otherwise stop and report. Plan changes: Accept finds the row by aggregate, then GetOrgInvitationForUpdate(Id) and re-checks AcceptedAt empty and ExpiresAt > now after the lock; never CurrDateTime() inside an aggregate filter (local variable first); GetByToken returns a masked email (first letter + "****@" + domain); Accept runs the EnsureMyProfile logic first; accept on an existing owner membership keeps owner (BRD, source downgrades); Revoke only pending rows, not found → "Invalid invitation id."; manager may invite member only. Foundation rev 19 = Salenix_Crypto reference (Studio). H1 PUBLISHED rev 20 (2026-11-11, env_app verified): Foundation_TodayFor / NowFor (public, no permission check, TenantId input), private Foundation_TimezoneFor + Foundation_SafeTodayIn / SafeConvertUtcToZone (fallback only on TIMEZONE_UNKNOWN:, otherwise raise FoundationError with the original message; Safe* handler Abort Transaction = No because the fallback must end normally, callers abort). Test pending (H3 DevTools). Long run 2026-11-11 (see LONG-RUN-REPORT.md): H2 invitations rev 21, W1 approval entities rev 22, W2 approval logic rev 23, F8 rev 24; Salenix_Sales C1 cleanup rev 5, S1–S4 entities rev 6–9, S5 stage helpers rev 10, S6 Company actions rev 11, S7 Contact actions rev 12, S8 deal governance rev 13. ODC facts learned: a service action cannot call a service action of the same app; cross-app entity references allow only delete rule Ignore (TenantId on domain entities = Ignore). NEXT: user manual steps in LONG-RUN-REPORT.md (Sales reference refresh for Approval_*, UserProfile, Membership attributes), then wire Won approval
- Batch plan: A = F2b-4 + F2b-5; B = F2b-6 + Step 2b cleanup; C = F3; D = F4; E = F5 + F7; F = F6.
- F3 OrganizationSettings (incl. flags + credit days), CustomFieldDefinition
- F4 Foundation_GetMyContext, Foundation_HasPermission, Foundation_GetFlags, Foundation_TodayFor
- F5 Workspace_ListMine / SetActive / Create
- F6 Invitations (hash token, 7-day expiry), role change, remove member (last-admin guards)
- F7 Notification, FileAttachment + service actions

---
# DevTools T1–T8 — PUBLISHED rev 10 (env_app verified, 2026-11-11 11:16 UTC)
Pre-check: DevTools rev 9 (Studio reference publish), Foundation rev 25, Sales rev 17 deployed. Mentor session 8cf23f26 (read-only check, P1–P7b, read-only verification, publish), closed after the publish.
| Prompt | Section | Dev_* wrappers (no handler, existing pattern) | Screen |
|---|---|---|---|
| P1 | T1 Time, T3 Member_GetRole, T5 StageSelfCheck | Dev_Time_Today (GetMyContext → TodayFor + NowFor, UtcNow), Dev_Member_GetRole, Dev_Sales_StageSelfCheck | Run_* + Result_* lists |
| P2 | T2 Invitations | Dev_Invitation_Create / ListPending / Revoke / GetByToken / Accept | token copied from AcceptLink into In_Invitation_Token; InvitationTable |
| P3 | T4 Approvals | Dev_Approval_Submit / ListPending / Decide / Cancel / Get | RequestId copied into In_Approval_RequestId; ApprovalTable, ApprovalActionTable |
| P4 | T6 Companies | Dev_Company_FindSimilar / Create / Update / SoftDelete / Restore / Get / List | CompanyMatchTable, CompanyTable; new CompanyId copied into In_Company_Id |
| P5 | T7 Contacts | Dev_Contact_FindSimilar / Create / Update / SoftDelete / Restore / Get / List | ContactMatchTable, ContactTable |
| P6a | T8 deal form + write | Dev_Deal_Create / Update / ChangeStage | Block_Deal_Form (23 inputs, OutSystemsUI Columns2), new DealId copied into In_Deal_Id |
| P6b | T8 read / approval / PO | Dev_Deal_Get / GetHistory / Approve / Reject, Dev_DealOrderProof_Add | Get (34 lines + DealProofTable), Load into form, History (audit, date change, stage tables), Refresh_DealView |
| P7a/b | T8 UAT buttons | – | Block_Deal_UAT at the top of T8: 25 case buttons + Clear log; helpers UAT_ResetForm, UAT_Log, UAT_ClearLog; log line = label (with expected result) → result lines |
Totals: 35 new Dev_* (50 in the app), 36 new Run_* (all with AllExceptions → "ERROR: " + message), 28 UAT_* actions, Refresh_DealView. Validation 0 errors; 18 warnings = existing font offline-cache warnings.
Conversions: ids TextToLongInteger (empty → 0); OwnerId TextToIdentifier (empty → empty id); dates YYYY-MM-DD, empty → NullDate; booleans "true"/"false" text.
Gap: **DealOrderProof_Remove is not available in DevTools** — it exists and is Public in Sales rev 17 (checked below); only the DevTools reference is missing.
UAT 8.2 uses the browser date (CurrDate) for IssuedOn — fine when the browser is in India (workspace timezone Asia/Kolkata).

## Test order (DevTools rev 10, https://vtpl-dev.outsystems.app/Salenix_DevTools)
Users: A = workspace owner (bootstrap), B = second account. Add B with DevOnly_AddMember(email, "member"); switch B's role with Member_ChangeRole as A. Get user ids from GetMyContext (ActingUserId) while logged in as each user. Check flags first (GetFlags); dedup tests need DedupEnabled = True.
| # | Who | Step | Expected |
|---|---|---|---|
| 1 | A | T1 Run | OK; TenantId = active workspace; Today 2026-11-11 (workspace date); Timezone Asia/Kolkata; LocalNow = UtcNow + 5:30 |
| 2 | A | T5 Run | Passed True, FailedCount 0, Report "All 89 checks passed." |
| 3 | A | T3 with B's id; then with a random number | IsMember True, RoleCode member, Status active; random → IsMember False |
| 4 | A | T2 Create("NewUser@Example.com ", "member") | InvitationId > 0; AcceptLink ".../Salenix/AcceptInvite?token=" + 43 chars; ExpiresAt ≈ now + 7 days; token filled |
| 5 | A | T2 Create same email again; ListPending | one row for newuser@example.com (old one deleted), InvitedByName filled |
| 6 | A | T2 Create(B's email, "member") / role "owner" / email "abc" | "already a member of this workspace." / "Pick a role" / "Enter a valid email address" |
| 7 | A | T2 GetByToken(token); then a wrong token | IsValid True, MaskedEmail "n****@example.com", RoleCode member; wrong → IsValid False |
| 8 | A | T2 Revoke(id); again | OK; second → ERROR "Invalid invitation id." |
| 9 | A | T2 Create(B2 email you can log in with) → copy token; log in as that user → GetByToken + Accept; Accept again | Accept: TenantId + WorkspaceName; second → "This invitation is invalid or has expired."; as a different email → "sent to a different email address" |
| 10 | B (member) | T4 Submit("deal_won", 101, "Test deal"); Submit again | pending, RequestId > 0; then already_pending, same id |
| 11 | B | T4 Decide(any id, "approve", "") | ERROR permission denied approveDeals (member) |
| 12 | A → B manager | Member_ChangeRole(B, "manager"); B: T4 ListPending(""), Decide(id, "reject", ""), (id, "maybe", ""), (id, "approve", ""), again, (id, "reject", "x") | list has 101; "Enter a reason for the rejection."; "Approve or reject?"; approved; already_decided; "no longer awaiting approval." |
| 13 | B manager | T4 Submit("credit_terms", 55, "Terms"); Get(id) | auto_approved, AutoApproved True; actions submitted + auto_approved |
| 14 | A → B member | Member_ChangeRole(B, "member"); B: T4 Submit("deal_won", 102, "x"), Cancel("deal_won", 102), Get(id) | pending; CancelledCount 1; actions submitted, cancelled |
| 15 | A | T6 Create("Acme Pvt Ltd", "acme.com", "IT", "", false) | ok, CompanyId; Get → NormalizedName "acme" |
| 16 | A | T6 Create("ACME Limited", …) then with DedupOverride true; FindSimilar("Acmee") | duplicate_warning (match Kind exact); then ok_dedup_override; fuzzy match Score ≥ 0.55 (flag on) |
| 17 | A | T6 Create("") | validation, FailedRules "Name is required" |
| 18 | B member | T6 Create with OwnerId = A; List | owner_not_allowed; list = own + unassigned only |
| 19 | A | T6 SoftDelete; again; List(IncludeDeleted true); Restore; Get of a deleted id | ok; not_found; row listed; ok; "Company not found in this tenant." (B member SoftDelete → permission denied deleteCompanies) |
| 20 | A | T7 Create("Ravi", "Kumar", "Ravi@Acme.com", "", "", Acme id, ""); same email upper case; all empty | ok (Acme ContactCount 1); duplicate_email; "Provide a first name, last name, or email." |
| 21 | A | T7 Update another contact to Ravi's email; delete Ravi, create new with same email, Restore Ravi | duplicate_email; duplicate_email (X3) |
| 22 | B member | T8 UAT1, UAT2, UAT3 | lead_close_date; close_date_required; margin_required |
| 23 | B member | UAT4 setup, step 1, step 2 | ok; rollback_reason_required; ok — Get: Stage qualified, RollbackCount 1, ForecastCategory pipeline; History: audit rollback budget_lost "x", stage rows |
| 24 | B member | UAT5 setup, step 1, step 2 | ok; hold_reason_required; ok — ForecastCategory omitted, HoldReason timing, HoldRevisitOn 2027-01-15 |
| 25 | B member | UAT6 setup, push 1–4 | PushCount 1,2,3,4; after push 3 QuotaWeight 1; after push 4 QuotaWeight 0.5, TotalSlipDays 28, OriginalCloseDate 2026-12-01; 4 DealDateChange rows IsPush True |
| 26 | B member | UAT7 pull | PushCount 4, QuotaWeight 0.5, TotalSlipDays 9, IsPush False |
| 27 | B member | UAT15 setup, step 1, 2, 3 | needs_close_date_clear; rollback_reason_required; ok — ExpectedCloseDate empty, OriginalCloseDate kept |
| 28 | B member | UAT8 setup, won no PO, add PO, UAT8/10 won | ok; order_proof; ok ProofId; ok — ApprovalStatus pending, ApprovalRequestId > 0, ForecastCategory closed_won. Note the deal id |
| 29 | A → B manager | B: type the UAT8 deal id into Deal Id, UAT9 reject | ok — Stage negotiation, ApprovalStatus rejected, RollbackCount 1, ClosedAt empty, ForecastCategory commit; audit rollback approval_rejected "price" |
| 30 | B manager | UAT8 setup, add PO, UAT8/10 won (UAT10) | ok — ApprovalStatus approved immediately (auto-approved) |
| 31 | B manager | Deal form: Create with OwnerId = user id of A (owner); then OwnerId = a random number | owner_assign_forbidden "Managers cannot assign deals to an owner."; owner_invalid |
| 32 | any | X3 by hand in the form: Create Stage hold without HoldReason; with HoldRevisitOn yesterday | hold_reason_required; hold_revisit_past |
Not testable yet: UAT 11-14 (stage gates, offerings, value split, FY proration not built). PO remove: added in rev 12 (see below).

## Sales rev 17 read-only check (2026-11-11, Mentor session 11f4b26e, nothing changed)
Every S10 element is present at rev 17 and matches S10: Deal.ApprovalRequestId (Long Integer, optional, default 0); structures DealItem (52 attributes), DealOrderProofItem (13), DealAuditItem (10), DealDateChangeItem (8), DealStageItem (4), all Public; service actions Deal_Approve, Deal_Reject, Deal_Get, Deal_GetHistory, DealOrderProof_Add, DealOrderProof_Remove(ProofId, DealId), all Public with OnValidationError + AllExceptions; Money_Format private Function (inputs optional); owner checks in Deal_Create (OwnerId ≠ empty and ≠ actor) and Deal_Update (after not_found, FinalOwner ≠ empty/actor/stored owner); Won wiring (Approval_Submit → approval_failed, ApprovalStatus/By/At/RequestId assigns, Approval_Cancel last node) in Deal_ChangeStage and Deal_Update; Deal_Reject ForecastCategory = If(Overridden, kept, Forecast_CategoryFor("negotiation")). Global Exception Handler empty. Warnings: 2 unused FailedRules outputs (Deal_Approve, DealOrderProof_Remove) + 18 font. Rev 17 lost nothing.

## FL1 Foundation Settings_SetFlags — PUBLISHED rev 26 (env_app verified, 2026-11-11 11:28 UTC)
| Element | Rules |
|---|---|
| Settings_SetFlags (service, Public) | inputs StageGatesEnabled, DealScoringEnabled, EmailSyncEnabled, ForecastsEnabled, DedupEnabled, OfferingsEnabled (Boolean, mandatory); outputs Created + StageGatesEnabledOut … OfferingsEnabledOut (ODC forbids an output with an input's name). ResolveContext → CheckPermission(role, "manageFeatureFlags") → not allowed: ValidationError "Permission denied: requires "manageFeatureFlags" (current role: <role>)" → OrganizationSettings of the acting tenant: missing → create (entity defaults, Timezone "Asia/Kolkata" as Workspace_Provision, the 6 flags, UpdatedBy, CreatedAt/UpdatedAt) Created True; else GetForUpdate → 6 flags + UpdatedBy/UpdatedAt only → Created False. Handlers as Member_ChangeRole; AllExceptions (Log Error = False) → LogMessage + "FOUNDATION_ERROR: Could not save the feature flags." |
Difference from the source: setWorkspaceFlags patches only the keys given; ours writes all six (the caller sends the current values of the others). Validation 0 errors.
Tests (after the DevTools reference + section): as owner/admin Settings_SetFlags(all False except DedupEnabled True) → Created False, DedupEnabledOut True; GetFlags → DedupEnabled True; as manager/member → permission denied manageFeatureFlags.
**DevTools reference (Studio, fresh copy of rev 10, reference-only publish):** Salenix_Foundation (rev 26) → Settings_SetFlags; Salenix_Sales (rev 17) → DealOrderProof_Remove. Then I add the "T0 Flags" and "Remove proof" sections.

## DevTools P8 — PUBLISHED rev 12 (env_app verified, 2026-11-11 11:38 UTC; rev 11 = Studio reference publish Settings_SetFlags + DealOrderProof_Remove)
| Element | Change |
|---|---|
| Section_T0_Flags (first container under the page heading) | 6 checkboxes Flag_*; "Load" → Run_Flags_Load (existing Dev_GetFlags → Flag_*); "Save" → Run_Flags_Save → new Dev_Settings_SetFlags → Created + 6 Out values |
| TestConsole OnReady (was empty) | Start → Run_Flags_Load → End (checkboxes prefilled) |
| Block_Deal_Proof | In_Proof_RemoveId + "Remove proof" → Run_DealOrderProof_Remove → new Dev_DealOrderProof_Remove(ProofId, DealId = In_Deal_Id) |
| Run_DealOrderProof_Add | + Assign In_Proof_RemoveId = If(ProofId > 0, LongIntegerToText(ProofId), In_Proof_RemoveId) |
Validation 0 errors, 18 font warnings.
Extra tests: T0 (owner/admin) tick DedupEnabled → Save → Created False, DedupEnabledOut True; reload page → box stays ticked; as manager/member Save → ERROR permission denied manageFeatureFlags. Remove proof: deal not Won → ok; same ProofId again → "That PO is no longer on this deal."; Won deal → won_locked "A Won deal keeps the PO it was won on. …".

## DevTools P9 fix — PUBLISHED rev 13 (env_app verified, 2026-11-11 16:09 UTC)
Bug found by the user: T1 showed UtcNow 17:23 and LocalNow 22:53 at 17:23 IST (the browser converted the DateTime outputs again: UTC +5:30, wall clock +5:30 twice). WorkspaceTable showed one row twice.
Cause (node text read first): Dev_Time_Today returned DateTimes to the client; every Run_ action cleared its Result_ list and table lists BEFORE the server call, so two overlapping runs (double click, OnReady + Load) both cleared, then both appended.
| Element | Old → New |
|---|---|
| Dev_Time_Today | outputs Today, LocalNow, UtcNow Date/DateTime → Text built on the server: Today "yyyy-MM-dd"; LocalNow FormatDateTime(NowFor.LocalNow, "yyyy-MM-dd HH:mm:ss") + " (<zone>, wall clock)"; UtcNow FormatDateTime(CurrDateTime(), …) + " UTC" |
| Run_Time_Today | shows the texts as is (no DateToText/DateTimeToText on the client) |
| All 53 Run_* | Start → ClearResult → [ClearTable] → Dev_ call → appends  ⇒  Start → Dev_ call → ClearResult → appends (Run_Deal_LoadForm, Run_Flags_Load: call → Assign → ClearResult) |
| 12 table-filling Run_* | ClearTable + For Each/ListAppend → one Assign of the whole list after the call (WorkspaceItems, NotificationItems, InvitationItems, ApprovalItems, ApprovalActions, CompanyMatches ×2, CompanyItems, ContactMatches, ContactItems, DealProofs, DealAuditRows/DealDateChanges/DealStages) |
Unchanged: UAT_*, Refresh_DealView, OnReady, RunCryptoSmokeTest, handlers. Validation 0 errors, 18 font warnings.
Rule added to CLAUDE.md (addendum): NowFor wall-clock DateTime is server-only; the UI gets UTC DateTimes and formats them on the client.
Note: other DevTools DateTime lines (ExpiresAt, CreatedAt, …) are UTC values formatted by the browser → shown in browser local time (correct per the rule).
Retest: T1 → UtcNow = IST − 5:30 with " UTC", LocalNow = IST wall clock; Workspace_ListMine double click → each workspace once, Count = number of rows.

---
# Run 2026-11-11 evening — UIKit + demo data (standing approval: publish after a clean report)

## U1 Salenix_UIKit cleanup — nothing to do (no publish)
REPORT ONLY (Mentor): the library was empty — no UI flows, blocks, themes, images, resources, scripts, actions, structures, settings; only the (System) reference; 0 errors / 0 warnings. Nothing removed, nothing published (rev 3 stayed).

## T2 Salenix_Theme v2.1 — PUBLISHED rev 4 (publish_status success, 2026-11-11 16:27 UTC; library, no env_app record)
Added because the blocks needed classes the theme did not have (rule: no CSS outside the Theme app). Appended at the end of the style sheet (1172 → 1292 lines), nothing existing changed; same text as theme/salenix-theme.css lines 1173–1292; THEME-GUIDE.md updated (v2.1).
| Class | For | Source |
|---|---|---|
| sx-shell, sx-shell-main, sx-shell-content (+ phone rule: rail on top, full width) | AppLayout | dashboard-shell.tsx |
| sx-topbar-workspace | AppLayout workspace name | workspace-switcher.tsx trigger |
| sx-breadcrumb | PageHeader | page header rows |
| sx-card-footer | Card | card footer rows |
| sx-trend-flat | KpiTile neutral delta | metric-card.tsx TrendIndicator |
| sx-empty-icon | EmptyState | dashboard empty states |
| sx-path, sx-path-step, sx-path-done, sx-path-current | StagePathBar | deals/[id]/_components/deal-path-bar.tsx |
Validation 0 errors, 1 warning (existing Google Fonts offline-cache).

## U2 Salenix_UIKit display blocks — PUBLISHED rev 4 (publishSucceeded 2026-11-11 16:34 UTC; library, confirm through a consumer)
UI flow UIKit; all blocks Public; widgets = Containers / Expressions / If / List / Placeholders; only Salenix_Theme classes; no inline styles, no colours, no server calls. Read-back of the whole library done; every class used exists in theme v2.1.
| Element | Inputs (default) | Placeholders | Classes | Source file |
|---|---|---|---|---|
| Stage_Label (client Function, Public) | StageCode → Label (Lead…Lost, else trimmed input) | – | – | lib/stage-gates/types.ts STAGE_LABELS |
| Money_FormatText (client Function + JS node, Public) | AmountMinor, CurrencyCode, Compact, FractionDigits → Text | – | – | lib/currency/format.ts, lib/incentives/format.ts |
| AppLayout | WorkspaceName (""), BrandName ("Salenix"), RailCollapsed (False) | Menu, UserMenu, MainContent | sx-shell, sx-rail (+ sx-rail-collapsed), sx-rail-brand, sx-rail-nav, sx-shell-main, sx-topbar, sx-topbar-workspace, sx-topbar-actions, sx-shell-content | components/dashboard-shell.tsx, sidebar.tsx, header.tsx |
| PageHeader | Title, Subtitle ("") | Breadcrumb, Actions | sx-breadcrumb, sx-page-header, sx-page-title, sx-page-subtitle, sx-row | dashboard page headers |
| Card | Title (""), Variant ("" / compact / flush), ShowFooter (False) | HeaderActions, Body, Footer | sx-card (+ compact / flush), sx-card-header, sx-card-title, sx-row, sx-card-footer | metric-card.tsx, card panels |
| KpiTile | Label, Value (Text), Delta (""), Tone ("neutral" / positive / negative) | – | sx-kpi, sx-row, sx-metric-label, sx-trend + sx-trend-up / -down / -flat, sx-kpi-value | kpi-strip.tsx, metric-card.tsx |
| StageBadge | StageCode, Label ("") | – | "sx-stage sx-stage-" + ToLower(Trim(StageCode)) | components/deal-highlights.tsx STAGE_BADGE_STYLES |
| StatusBadge | Label, Tone ("neutral"; info / success / warning / danger) | – | sx-badge + sx-badge-neutral / info / positive / warning / negative | lib/brand/tokens.ts STATUS_TINTS |
| MoneyText | AmountMinor, CurrencyCode ("INR"), Compact (False), FractionDigits (2) | – | sx-mono | as Money_FormatText |
| EmptyState | IconName ("inbox", Font Awesome), Title, Message (""), Inline (False) | Action | sx-empty / sx-empty-inline, sx-empty-icon fa fa-<icon>, sx-empty-title, sx-muted, sx-row | dashboard empty states |
| StagePathBar | StageCodesCsv ("lead,qualified,proposal,negotiation,won"), CurrentStageCode | – | List "sx-path"; items "sx-path-step" + sx-path-done / sx-path-current | deal-path-bar.tsx (gate pop-ups not built) |
Internal: structure PathStep (Code, Label, StateClass; Public No), StagePathBar local Steps + BuildSteps (OnInitialize + OnParametersChanged). Hold / lost → no step done or current (as the source).
Deviations: StagePathBar takes a CSV text instead of a Text List (easier to pass from screens); Card got ShowFooter, Variant and HeaderActions (an empty footer would still draw its top border); Money_FormatText not run yet (expected "₹12,34,567.89", compact "₹12.3L" / "₹1.2Cr"; non-INR compact uses Intl compact). Validation 0 errors, 0 warnings. Mentor offered a library "release version" — not done (not asked); if Studio does not offer UIKit rev 4 / Theme rev 4 in Manage Dependencies, release them first.

## D1 Salenix_Sales demo data — PUBLISHED rev 18 (env_app verified, 2026-11-11 16:58 UTC) — NOT RUN YET
Data spec: notes/demo-data.json (+ notes/demo-*.min.json = the exact JSON stored in the actions).
| Element | Kind | Rules |
|---|---|---|
| Setting DevToolsEnabled | Text, secret, default "" | own Sales setting (Sales cannot read Foundation settings); value "true" set by hand in the Portal |
| DevOnly_DemoGuard(TenantId Long) → ActingUserId, ActingTenantId, Today, NowUtc | private | DevToolsEnabled ≠ "true" → "VALIDATION_ERROR: Demo data tools are disabled (Setting DevToolsEnabled is not true)."; Sales_Context: TenantId ≠ acting tenant or role ≠ owner → "VALIDATION_ERROR: Only the owner of this workspace can seed or clear demo data. Make it your active workspace first."; Today = Foundation_TodayFor |
| DemoSeed_Companies / Contacts / Deals | private batch runners | JSON text → JSONDeserialize → Length check (12 / 25 / 30) → For Each create-only (by Name / Email / Name; tenant, not deleted); handler AllExceptions (Abort Yes, Log Error No) → LogMessage, Success False, ErrorText |
| DemoCompanyNameByKey, DemoContactEmailByKey | private Functions | key → name / email (If chains) |
| DevOnly_SeedDemoData(TenantId, SecondOwnerEmail) → Success, OutcomeCode (ok / already_seeded), Message, Report, SecondOwnerFound, 7 counts | public service | guard → "[DEMO] Tata Steel Ltd" exists → already_seeded → second owner: UserProfile by email + Member_GetRole active, else unassigned + Report line → 3 batches → any failed → raise "SALES_ERROR: Demo seed failed and was rolled back. " + Report → Company_RefreshRollups for every "[DEMO] " company → ok |
| DevOnly_ClearDemoData(TenantId) → Success, Message, Deals / Contacts / Companies / NotesDeleted | public service | guard → soft-delete (DeletedAt/By, UpdatedAt/By) Deals (Name), Contacts (FirstName), Notes (Body), Companies (Name) starting with "[DEMO] " (aggregate filter Substr(x, 0, 7) = "[DEMO] ", untested at runtime) → rollups per company |
Data per run: 12 companies (Mumbai, Pune, Bengaluru, Delhi NCR, Chennai, Ahmedabad; GSTIN-shaped values with the right state codes) + 12 company Notes ("[DEMO] HQ: City, State - GSTIN …"); 25 contacts (+91 phones, @example.com, NormalizedName via Salenix_Crypto); 30 deals (lead 5, qualified 5, proposal 5, negotiation 5, hold 3, won 4, lost 3; ₹2L–₹1.5Cr; won/lost closed 6–52 days ago, open deals close in 14–90 days, leads without close date; Probability / ForecastCategory from the stage helpers; QuotaWeight 1; hold with HoldReason budget_deferred / timing / champion_left and HoldRevisitOn +14…+30 days; lost with LostReason); expected 41 DealContact (30 primary decision_maker + 11 influencer), 94 DealStageHistory (one per path step, evenly spaced from CreatedAt to ClosedAt / yesterday, last step open), 4 DealOrderProof (one per Won deal: PO/DEMO/2026/000N, subtotal = deal amount, 18% tax, IssuedOn = close − 2 days).
Owners: slot A = the caller (owner), slot B = SecondOwnerEmail if an active member (e.g. Dylan), slot "" = unassigned (companies 5 / 4 / 3, deals A 12, B 10, unassigned 8 — see notes/demo-data.json).
Deviations from the request (and why):
- **No "discount approval"**: the approval engine knows only deal_won and credit_terms (decision 8), and an owner's Approval_Submit is auto-approved. Instead deal Key 4 "[DEMO] Forging dies retrofit" is Won with ApprovalStatus pending and ApprovalRequestId 0 — the existing legacy path of Deal_Approve / Deal_Reject handles it. Foundation Approval_ListPending stays empty, so the Approvals screen must list Won deals with ApprovalStatus pending (as the source does: deals/queries.ts awaiting list).
- **Owners "Pranav / Dylan"**: CLAUDE.md has no test-data user ids → owner A = the caller, owner B = the SecondOwnerEmail input.
- **City / GSTIN / industry**: Company has no City or GSTIN attribute and Sales cannot read LookupValue → industries are free text (no preset in the source, decision 6); city + GSTIN go into one company Note.
- **Commit per batch not possible**: Sales has no CommitTransaction in its references (adding it is a Studio reference step) → the seed is all-or-nothing.
- **Guard**: own Sales Setting DevToolsEnabled (not the Foundation one); owner check = owner of the ACTIVE workspace AND TenantId = acting tenant.
- Deals are written with entity actions, not Deal_Create (governance would refuse back-dated history); no ActivityLog rows.
Validation 0 errors; warnings = existing 2 FailedRules + 20 font.
Not done: the run for TenantId 3 and the counts — DevTools has no button, and adding T9 needs a DevTools reference refresh in Studio first (references are by hand). Next steps in the final report of this run.

---
# Run 2026-11-11 night — R1 read actions for the UI, T9 demo data (standing approval: publish after a clean report)

## R1 read-only check (Sales rev 18, Foundation rev 26, DevTools rev 14)
Nothing of R1 existed under any name (Mentor + context_actions). Related only: Company_List / Contact_List (full items), DealItem (full record), Foundation Approval_ListPending (approval requests, not deals). Foundation had no member list → R1b ran.
Side finding (not changed, test it): Company_List sorts ascending on SortKey × If(SortDesc, 1, -1) — SortDesc = True may give ASCENDING order (Mentor's reading; untested).

## R1b Foundation Member_ListOptions — PUBLISHED rev 27 (env_app verified, 2026-11-11 17:13 UTC)
| Element | Rules |
|---|---|
| MemberOptionItem (Public) | UserId (User Identifier), Label (Text 250), Email (Text 256), RoleCode (Text 50) |
| Member_ListOptions(Search) → Items (Public service) | any active member; Foundation_ResolveContext → Membership ⋈ UserProfile (Only With, UserId), TenantId = acting, Status active (Trim+ToLower), Search in "First Last" or Email (Index, lower case); sort FirstName, LastName, Email; max 200; Label = Trim(First + " " + Last) else Email; RoleCode lower case. Handlers copied from Member_GetRole (ValidationError / MembershipMissing re-raise; AllExceptions Log Error = No → LogMessage + "FOUNDATION_ERROR: Something went wrong. Please try again.") |
Name: Member_ListOptions (not Foundation_Member_ListOptions) — matches the existing public Member_GetRole / Member_ChangeRole / Member_Remove names. Validation 0 errors, 22 existing warnings.

## R1 Sales read actions — PUBLISHED rev 19 (env_app verified, 2026-11-11 17:26 UTC; Mentor session 28d81ccf, prompts A1–A5)
All service actions Public, TenantId from Sales_Context, handlers as Deal_Get (OnValidationError re-raise; AllExceptions Log Error = No → LogMessage "Salenix_Sales.<Action>" → SalesError, SALES_ERROR:/MEMBERSHIP_MISSING: passed through). Scope filter as Company_List: Persona ≠ "sales" or OwnerId = actor or OwnerId empty. Soft-deleted excluded (Company/Contact also MergedIntoId empty).
| # | Element | Rules |
|---|---|---|
| A1 | DealListItem, StageTotalItem, PipelineColumn, OptionItem (Public structures) | DealListItem: DealId, Title, CompanyId (Long), CompanyName, StageCode, ValueMinor, CurrencyCode, ExpectedCloseDate, OwnerUserId, OwnerName, ApprovalStatus, HealthBand, UpdatedAt. StageTotalItem: StageCode, StageLabel, Count, ValueMinor. PipelineColumn: StageCode, StageLabel, DealCount, ValueMinor, Deals (DealListItem List). OptionItem: Id (Long), Label |
| A1 | Stage_LabelFor (private Function) | lead…lost → Lead…Lost, else trimmed input (same labels as UIKit Stage_Label) |
| A1 | Deal_List(Search, StageCode, OwnerUserId, ApprovalStatus, IncludeClosed, SortBy, StartIndex, MaxRecords) → Items, TotalCount | Deal ⟕ Company ⟕ UserProfile; Search on deal or company name (like); StageCode / OwnerUserId / ApprovalStatus optional; IncludeClosed False hides won + lost unless a StageCode is given; SortBy: updated (default, newest), value (largest), close (soonest, empty last), created (newest), name (A–Z) — all through ascending calculated keys NumKey / TextKey / TieKey (no direction flag); page size 1–200 else 50; TotalCount = aggregate Count; CompanyName = Company.Name else Deal.CompanyName; OwnerName = First Last else Email |
| A2 | Deal_ListPipeline(OwnerUserId, Search) → Columns | always 5 columns lead, qualified, proposal, negotiation, hold (won/lost not on the board); DealCount / ValueMinor = all matching deals (grouped aggregate); Deals max 50 per column ordered ExpectedCloseDate asc (empty last), UpdatedAt desc, Id desc |
| A3 | Dashboard_GetSummary() | Today = Foundation_TodayFor; month start in UTC = local midnight of day 1 − workspace offset (offset = DiffSeconds(UTC now, NowFor.LocalNow) rounded to 15 min); OpenPipelineMinor / OpenDealCount = open (not won/lost, hold included); WonThisMonth* = won with ClosedAt ≥ month start; ClosingNext30Count = open, not hold, close date today…today+30; OverdueCloseCount = open, not hold, close date < today; PendingApprovalCount = won + pending; StageTotals = the 5 open stages; TopDeals = 5 open by AmountMinor; RecentlyUpdated = 5 any stage by UpdatedAt; all in persona scope |
| A4 | Deal_ListPendingApproval() → Items, CanApprove | Foundation_HasPermission(role, "approveDeals").HasPermission; false → empty list, no error; else won + pending, whole tenant (no persona filter, as source listDealsAwaitingApproval), ClosedAt desc, max 200 |
| A4 | Company_ListOptions(Search) → Items (OptionItem) | Name like, sort Name, max 20, scope |
| A4 | Contact_ListOptions(CompanyId, Search) → Items | CompanyId 0 = any; Search on FirstName / LastName / Email; Label "First Last (email)" / name / email; sort First, Last, Email; max 20; scope |
| A5 | DevOnly_DemoCounts(TenantId) → Companies, CompanyNotes, Contacts, Deals, DealContacts, DealStageHistory, DealOrderProofs, Report | DEV ONLY (decision 19 list): DevOnly_DemoGuard; current "[DEMO] " rows (Substr(x, 0, 7), not soft-deleted); child tables via Only-With join to a non-deleted [DEMO] deal; handler as DevOnly_ClearDemoData |
Validation 0 errors; 20 existing warnings (2 FailedRules + 18 font).
Deviations from the request / source: pipeline order = ExpectedCloseDate (as asked; source board = UpdatedAt desc, all 7 stages); closing/overdue counts exclude Hold (parked deals); open pipeline sums AmountMinor across currencies as stored (all demo deals INR; workspace-currency conversion not built); Deal_List has no SortDesc input (direction is part of the SortBy code).

## S9a DevTools T9 Demo data — PUBLISHED rev 15 (env_app verified, 2026-11-11 17:34 UTC)
| Element | Change |
|---|---|
| T9CountRow (structure, private) | Entity, Expected, Actual, Status |
| Dev_T9_Check | GetMyContext + Workspace_ListMine → TenantIdText, RoleCode, Persona, WorkspaceName |
| Dev_T9_Seed(SecondOwnerEmail, ExpectedTenantId) | acting tenant ≠ expected → Ran False, "Active workspace is X, expected Y. Nothing seeded."; else DevOnly_SeedDemoData → all outputs (…Created counts) |
| Dev_T9_Clear(ExpectedTenantId) | same tenant check → DevOnly_ClearDemoData |
| Section_T9_DemoData (last section) | inputs Second owner email (dylan.rivera@example.com), Expected TenantId (3); buttons Check workspace / Seed / Clear; Result_T9 list; T9CountTable (Expected 12/12/25/30/41/94/4 vs the seed's Created counts, OK/DIFF) |
Validation 0 errors, 18 font warnings. R1 Run buttons + DevOnly_DemoCounts need DevTools references first (Studio) → S9b.

## S9b — SKIPPED (user decision 2026-11-11)
R1 Run buttons in DevTools not built; the Salenix screens will exercise the R1 actions. DevTools stays at rev 15 (no R1 / DevOnly_DemoCounts / Member_ListOptions references).

## V1 — docs note (no MCP check possible)
- Active workspace + role: resolved from the user's login session (UserProfile.ActiveTenantId), so it cannot be read through the MCP; T9 "Check workspace" shows TenantId / Workspace / Role and the Seed wrapper refuses when the acting tenant ≠ Expected TenantId (default 3).
- User report 2026-11-11: "Seeded, counts OK" — T9 counts table all OK: 12 companies, 12 company notes, 25 contacts, 30 deals, 41 DealContact, 94 DealStageHistory, 4 DealOrderProof (from DevOnly_SeedDemoData …Created outputs).
- R1 actions not run yet (no DevTools buttons); first runtime check = the Salenix screens. Expected on the demo data as owner: Deal_ListPipeline 5 columns lead 5 / qualified 5 / proposal 5 / negotiation 5 / hold 3; Deal_List (IncludeClosed False) TotalCount 23, (True) 30; Dashboard OpenDealCount 23, PendingApprovalCount ≥ 1 (deal Key 4 "[DEMO] Forging dies retrofit", legacy pending), WonThisMonthCount depends on the close offsets (won closed 6–52 days ago); Deal_ListPendingApproval as owner → CanApprove True, ≥ 1 row. (Counts add any non-demo deals of the workspace.)

## Salenix app reference list (ONE Studio step; Salenix web app, fresh copy, Manage Dependencies, then publish from Studio)
- **Salenix_Theme (rev 4):** the theme (set as the app's default theme in app properties; the style sheet is the only CSS, decision 20).
- **Salenix_UIKit (rev 4):** blocks AppLayout, PageHeader, Card, KpiTile, StageBadge, StatusBadge, MoneyText, EmptyState, StagePathBar; client functions Stage_Label, Money_FormatText. (Release the library version first if Studio does not offer rev 4.)
- **Salenix_Foundation (rev 27):** role SalenixUser; service actions Foundation_EnsureMyProfile, Foundation_GetMyContext, Foundation_HasPermission, Foundation_GetFlags, Foundation_TodayFor, Workspace_ListMine, Workspace_SetActive, Workspace_Create, Workspace_CreateFirst, Notification_ListMine, Notification_MarkRead, Notification_MarkAllRead, Invitation_GetByToken, Invitation_Accept (AcceptInvite screen lives in Salenix, decision 21g), Member_ListOptions; structures WorkspaceItem, NotificationItem, MemberOptionItem.
- **Salenix_Sales (rev 19):** service actions Dashboard_GetSummary; Deal_List, Deal_ListPipeline, Deal_ListPendingApproval, Deal_Get, Deal_GetHistory, Deal_Create, Deal_Update, Deal_ChangeStage, Deal_Approve, Deal_Reject, DealOrderProof_Add, DealOrderProof_Remove; Company_List, Company_Get, Company_FindSimilar, Company_Create, Company_Update, Company_SoftDelete, Company_Restore, Company_ListOptions; Contact_List, Contact_Get, Contact_FindSimilar, Contact_Create, Contact_Update, Contact_SoftDelete, Contact_Restore, Contact_ListOptions; structures DealListItem, PipelineColumn, StageTotalItem, OptionItem, DealItem, DealOrderProofItem, DealAuditItem, DealDateChangeItem, DealStageItem, CompanyItem, CompanyMatchItem, ContactItem, ContactMatchItem.
- NOT referenced by Salenix (on purpose): Notification_Create (server-to-server), Approval_* (Sales calls them; UI uses Deal_Approve / Deal_Reject), Member_ChangeRole / Member_Remove / Invitation_Create / ListPending / Revoke / Settings_SetFlags / Member_GetRole (→ Salenix_Admin), Foundation_NowFor (server-only, never to the browser), Sales_StageSelfCheck, every DevOnly_* (decision 19), entities (UI gets structures only).

---
# Run 2026-11-11 night 2 — Salenix UI app (standing approval: publish after each clean batch)
Pre-check: app_list — Salenix rev 4 (Studio reference publish by the user), Theme rev 5, UIKit rev 4, Foundation rev 27, Sales rev 19. Mentor session 3b7a6eb1. Read-only inventory: references present = UIKit 9 blocks + 2 functions; Foundation 30 service actions (incl. Approval_*, Invitation_*, DevOnly_AddMember — more than the list), 6 structures, role SalenixUser, entities Tenant/UserProfile/Membership (not used by the UI); Sales 33 service actions + 13 structures. OutSystemsMaps / OutSystemsCharts still referenced (not removed: no reference changes in this run).

## A0 template cleanup + shell — PUBLISHED rev 5 (env_app verified, 2026-11-11 18:09 UTC)
| Prompt | Element | Change |
|---|---|---|
| A0-1 | removed | screens RecoverPasswordRequest, RecoverPasswordReset, UserProfile, ChangePassword; blocks LayoutTopMenu, LayoutSideMenu, Layout, Menu, MenuIcon, UserInfo, ApplicationTitle; UI flow Emails (ChangeEmail, ResetPassword); theme EmailTheme; client + server actions SendChangeEmail, SendResetPasswordEmail, UpdateUser; client vars UserName, UserPhotoURL; image User |
| A0-1 | kept | Login, InvalidPermissions (now LayoutBlank, "Go to login" link always shown), LayoutBlank, DoLogin, DoLogout (2 assigns to deleted client vars removed), LastURL, RedirectToURL, Logo, resources, local role Salenix, OnException |
| A0-1 | UI_StartSession (server) | Foundation_EnsureMyProfile → Foundation_GetMyContext → HasWorkspace; AllExceptions → ErrorMessage (GetMyContext has no workspace name → WorkspaceName stays "") |
| A0-2 | Login LoginOnClick | role check CheckSalenixRole (local role Salenix — users do not have it) → **CheckSalenixUserRole**; after login UI_StartSession → HasWorkspace ? (LastURL empty → Home, else LastURL) : Workspaces |
| A0-2 | UI_StripCode, UI_IsMembershipMissing (client functions) | strips MEMBERSHIP_MISSING: / VALIDATION_ERROR: / SALES_ERROR: / FOUNDATION_ERROR:; empty → "Something went wrong. Please try again." |
| A0-2 | UI_Workspace_SetActive, UI_Notification_MarkRead, UI_Notification_MarkAllRead (server wrappers) | Success + ErrorMessage |
| A0-2 | screens Home (Default Screen), Deals, Pipeline, Companies, Contacts, Approvals, Workspaces | role SalenixUser only |
| A0-3 | block MainLayout(ActiveItem, IsWorkspacePicker; placeholder Content) | data action GetShell: EnsureMyProfile → Workspace_ListMine (name of IsActive row) → GetMyContext (RoleCode, Persona) → Notification_ListMine(False, 20) → System User name/email; on MEMBERSHIP_MISSING → Workspaces (unless IsWorkspacePicker). UIKit AppLayout; rail: Workspace › Dashboard; Sales › Deals, Pipeline, Approvals, Companies, Contacts (sx-rail-item + sx-active); top bar: workspace dropdown (when > 1 workspace) → SetActive → Home; bell + unread StatusBadge → notifications drawer (sx-drawer: list, Mark read, Mark all read, EmptyState "You're all caught up"); user label + Log out (DoLogout → Login) |
| A0-4 | 6 screens | MainLayout + PageHeader + EmptyState "Coming soon" (replaced by later batches) |
| A0-4 | Workspaces | data action GetWorkspaces (EnsureMyProfile → ListMine); skeleton / sx-alert-error / EmptyState / Card flush + sx-table (Name, Role badge, Active badge or "Open" → SetActive → Home). Source: app/select-org/page.tsx |
Source files: dashboard-shell.tsx, sidebar.tsx, header.tsx, workspace-switcher.tsx, notification-bell.tsx, user-menu.tsx.
Deviation: data actions and UI_* wrappers catch AllExceptions and return ErrorMessage (no log; the Foundation/Sales action already logged) so each screen can show an error state — display only, nothing is swallowed silently. Notifications open as a right drawer (theme has no dropdown/popover class).
Validation 0 errors, 1 warning (Login is public and calls UI_StartSession — expected).

## A1 Home dashboard — PUBLISHED rev 6 (env_app verified, 2026-11-11 18:16 UTC)
Source: app/dashboard/page.tsx, _components/kpi-strip.tsx, stage-conversion-bars.tsx, overview-widgets.tsx.
| Element | Change |
|---|---|
| Home | data action GetSummary = Dashboard_GetSummary (outputs OpenPipelineMinor, OpenDealCount, WonThisMonthMinor, WonThisMonthCount, ClosingNext30Count, OverdueCloseCount, PendingApprovalCount, StageTotals, TopDeals, RecentlyUpdated) + MaxStageValueMinor + ErrorMessage; PageHeader "Dashboard" + "Open pipeline" link; skeleton / sx-alert-error / content: 6 KpiTiles (money compact via Money_FormatText INR), Card "Pipeline by stage" (StageBadge, count, MoneyText, OutSystemsUI ProgressBar relative to the largest stage), Card "Top open deals" (link → DealDetail, View all → Deals), Card "Recently updated"; inline EmptyStates |
| DealDetail(DealId) | new placeholder screen (filled in A3) |
Validation 0 errors, 2 warnings (Login public screen; unused DealId until A3).

## A2 Deals list + Pipeline board — PUBLISHED rev 7 (env_app verified, 2026-11-11 18:34 UTC; Mentor session 72af0ef8)
Source: deals/page.tsx, deals/_components/deals-view.tsx, deal-filter-bar.tsx, _components/pagination-controls.tsx; deals/board/page.tsx; hold-reason-modal.tsx, lost-reason-modal.tsx, rollback-reason-modal.tsx, clear-close-date-modal.tsx; _components/drawer.tsx.
| Prompt | Element | Change |
|---|---|---|
| A2-1 | UiOption (structure), UI_StageOptions / UI_ApprovalOptions / UI_DealSortOptions / UI_HoldReasonOptions / UI_RollbackReasonOptions (client actions — ListAppend is not allowed in a Function) | option lists; hold + rollback codes/labels copied from lib/deals/hold.ts and stage-governance.ts |
| A2-1 | UI_ApprovalTone, UI_HealthTone (Functions) | status → StatusBadge tone |
| A2-1 | Deals | GetOwners (Member_ListOptions), GetDeals (Deal_List: Search, StageCode, OwnerUserId = TextToIdentifier(OwnerFilter), ApprovalStatus, IncludeClosed, SortBy, StartIndex, MaxRecords 25); filter bar (search + Apply/Enter, stage, owner, approval, include won/lost, sort); table Deal (→ DealDetail), Company, Stage, Value, Expected close, Owner, Approval badge, Health badge; Previous/Next pagination "Showing x–y of N"; skeleton / error / EmptyState |
| A2-2 | UI_Deal_ChangeStage (server wrapper), UI_StageRank (Function) | Deal_ChangeStage(DealId, Stage, LostReason, HoldReason, HoldRevisitOn, HoldNote, ClearCloseDate, RollbackReasonCode, RollbackNote) |
| A2-2 | block StageChangeDrawer(DealId, DealTitle, FromStage, ToStage; event Closed(Changed)) | sx-drawer; fields by target: lost → reason (mandatory); hold → reason dropdown + revisit date + note; backward move → rollback reason + note; lead → "clear close date" checkbox; won → PO/approval hint; Sales outcome Message + FailedRules shown in the drawer (e.g. order_proof) |
| A2-3 | Pipeline | GetOwners, GetPipeline (Deal_ListPipeline); OutSystemsUI Gallery 5/2/1 with one UIKit Card per stage (StageBadge, count, MoneyText total), deal cards (link, company, value, close date, owner, "Move stage…" dropdown → StageChangeDrawer → refresh); "Showing 50 of N" |
Deviations: no drag-and-drop (as asked); hold / rollback reason lists are client-side copies of the source lists (Foundation has no public lookup read action — LookupValue/MasterData not reachable from the UI; NEEDS a Foundation Lookup_List service action later); dropdown "Move stage…" shares one variable across cards (reset right after the change).
Validation 0 errors, 2 warnings (Login public screen; unused DealDetail.DealId).

## A3 Deal detail — PUBLISHED rev 8 (env_app verified, 2026-11-11 18:48 UTC; Mentor session afcf99f8) — form follows (A3-3)
Source: deals/[id]/page.tsx, deals/[id]/_components/deal-path-bar.tsx, _components/deal-highlights.tsx, deals/approvals/page.tsx, deals/[id]/order-proof-actions.ts, _components/entity-timeline.tsx.
| Prompt | Element | Change |
|---|---|---|
| A3-1 | UI_Deal_Approve, UI_Deal_Reject (server wrappers) | Deal_Approve / Deal_Reject |
| A3-1 | DealDetail | GetDeal = Deal_Get + GetMyContext + HasPermission approveDeals (CanApprove) / manageDeals (CanEdit); PageHeader (name, company · owner, breadcrumb Deals, Edit, "Change stage…" → StageChangeDrawer); StagePathBar; approval banner (pending → Approve / Reject with required reason; approved/rejected badge); 6 KpiTiles (value, stage, expected close, owner, probability, pushes); Card Details (company, primary contact, forecast, source, next step, competitor, hold / lost reason, description); Card Governance (original close, slip days, pushes, rollbacks, quota weight, last stage change, created, updated) |
| A3-2 | UI_DealOrderProof_Add / _Remove, UI_ProofTypeOptions | proof types from LookupValue OrderProofType (client copy) |
| A3-2 | DealDetail | Card "Order proofs (PO)" (count + Σ subtotal of value; table type/ref/date/buyer/subtotal/tax/total; Remove; Add PO form: type, number, date, buyer, value before tax ₹, tax ₹ → minor units, total 0 = server computes); GetHistory = Deal_GetHistory → Stage history, Close date changes, Audit log tables; history refreshed after every change |
Gap: Contacts card shows only the primary contact (DealItem.ContactName/ContactEmail) — Sales has no public DealContact list action. Not built: PO file upload (decision 22d).
| A3-3 | UI_Deal_Create, UI_Deal_Update (server wrappers) | all Deal_Create / Deal_Update inputs; DealType, LostReason, RollbackReasonCode/Note empty |
| A3-3 | DealForm(DealId default 0) — **PUBLISHED rev 9 (env_app verified, 2026-11-11 18:55 UTC)** | GetFormContext (GetMyContext, Company_ListOptions, Member_ListOptions), GetDeal (Deal_Get when editing → prefill), GetContacts (Contact_ListOptions(CompanyId)); fields name, company (resets contact), primary contact, owner (default = me on create), stage (create: lead/qualified/proposal/negotiation/hold; edit: read-only badge — stage changes via the drawer), value ₹ → AmountMinor, margin % → MarginBps, expected close, hold reason/revisit/note when hold, close-date reason (edit); Sales Message + FailedRules shown in sx-alert-error; success → DealDetail. Links: DealDetail Edit → DealForm; "New deal" on Deals and Pipeline |
A3-3 deviation (Mentor): Next step, Source, Competitor, Description fields show only in edit mode (create sends ""). Polish item.
Source A3-3: deals/_components/create-deal-form.tsx, edit-deal-drawer.tsx, deal-amount-fields.tsx, deal-company-contact-fields.tsx.

## A4 Companies
### A4-0 Company_List SortDesc check (Sales rev 19, READ-ONLY, Mentor session b60f8f24) — NOT CONFIRMED, nothing changed
Node text: aggregate GetCompanies sorts SortKey **Descending**, then TieKey Descending; SortKey = (DeletedAt / DealCount / OpenPipelineValueMinor / WonLifetimeValueMinor / LastActivityAt / CreatedAt key) × If(IncludeDeleted or SortDesc, 1, -1). So SortDesc = True → newest / largest first; False → oldest / smallest first. The R1 suspicion came from reading the multiplier without the Descending sort. Sales not changed, not published.
Other findings (not fixed, logged): Medium — Deal_List has no CompanyId input (UI filters client-side, see A4-1); Company_List PageSize silently 50 unless MaxRecords is 25/50/100; IncludeDeleted ignores SortBy (most recently deleted first). Low — "recent" is not a defined SortBy (falls back to CreatedAt, which is what we want); "activity" puts companies without activity last; no StartIndex < 0 guard; Like filter does not escape % and _; unused local NowUtc.

### A4-1 / A4-2 Companies screens — PUBLISHED rev 10 (env_app verified, 2026-11-11 19:14 UTC; Mentor session 0c9b41fe)
Source: app/dashboard/companies/page.tsx, companies/[id]/page.tsx, companies/_components, lib/dedup.
| Prompt | Element | Change |
|---|---|---|
| A4-1 | UI_Company_SoftDelete, UI_Company_Restore | wrappers (Success, OutcomeCode, Message) |
| A4-1 | Companies | Company_List (Search, OwnerFilter all/mine/unassigned, IncludeDeleted = "Show deleted", SortBy recent/deals/pipeline/won/activity, SortDesc True, page 25); table company (→ CompanyDetail) + domain, industry, owner, open deals, open pipeline, won, contacts; Restore column when showing deleted; pagination; New company → CompanyForm |
| A4-1 | CompanyDetail(CompanyId) | Company_Get + permissions (deleteCompanies, manageCompanies); KPIs (deals, open, won, open pipeline, won lifetime, contacts); Details card; Contacts card (Contact_List by CompanyId → ContactDetail); Deals card (Deal_List(Search = company name, IncludeClosed, 200) filtered client-side by CompanyId — Deal_List has no company input); Edit (CanEdit) / Delete with inline confirm → Company_SoftDelete → Companies |
| A4-1 | ContactDetail(ContactId) | placeholder (A5) |
| A4-2 | UI_Company_FindSimilar / _Create / _Update; CompanyForm(CompanyId default 0) | name, domain, industry, owner (default me); create: FindSimilar first → "Possible duplicates" panel (Exact match / NN% similar badge, "Open existing" → CompanyDetail, "Create anyway" → Create(DedupOverride True)); server duplicate_warning handled the same way; update → CompanyDetail |
Note: FindSimilar returns nothing when the workspace flag DedupEnabled is off (Sales rule) — then Create runs directly. Validation 0 errors, 1 warning (unused ContactDetail.ContactId until A5).

## A5 Contacts — PUBLISHED rev 11 (env_app verified, 2026-11-11 19:28 UTC; Mentor session 1088a46f)
Source: app/dashboard/contacts/page.tsx, contacts/[id]/page.tsx, contacts/_components.
| Prompt | Element | Change |
|---|---|---|
| A5-1 | UI_Contact_SoftDelete / _Restore | wrappers |
| A5-1 | Contacts | Contact_List (Search name/email/title, CompanyId filter via Company_ListOptions, OwnerFilter, Show deleted, page 25); table name (→ ContactDetail) + title, company (→ CompanyDetail), email, phone, owner; Restore column; pagination |
| A5-1 | ContactDetail(ContactId) | Contact_Get + permissions (deleteContacts, manageContacts); Details card; Company card (Open company, New deal); Edit; Delete with inline confirm |
| A5-2 | UI_Contact_FindSimilar / _Create / _Update; ContactForm(ContactId, CompanyId) | first/last name, email, phone, job title, company, owner (default me); create: FindSimilar → "Possible duplicates" panel (NN% similar, Open existing, Create anyway); duplicate_email hard block shown as the Sales message; links: Contacts New contact, ContactDetail Edit, CompanyDetail "Add contact" (company pre-selected) |
Notes: Contact_FindSimilar EmailMatch/HasEmailMatch are passed through but not shown (Create blocks a duplicate email anyway). Email/Phone inputs are Text type (Email type failed validation on Text variables). Contact detail has no deals list (DealListItem has no ContactId). Validation 0 errors, 1 warning (Login).

## A6 Approvals — PUBLISHED rev 12 (env_app verified, 2026-11-11 19:34 UTC)
Source: app/dashboard/deals/approvals/page.tsx, approvals/_components.
| Element | Change |
|---|---|
| Approvals | GetPending = Deal_ListPendingApproval() (Items DealListItem, CanApprove); not CanApprove → EmptyState "You can't approve deals"; empty → "Nothing waiting for approval"; table Deal (→ DealDetail) + company, value, owner, updated, Pending badge, Approve (UI_Deal_Approve) / Reject (inline reason, required) (UI_Deal_Reject) → refresh |
Not built: recently approved / rejected lists, bulk approve/reject (no Sales list actions for them).

## Smoke test (END OF RUN, READ-ONLY, Mentor session 0042684f, rev 12) — 0 High, 5 Medium
Every screen/block data action, After Fetch and button action read back. Verdict per Medium item:
| # | Finding | Decision |
|---|---|---|
| 1 | DealDetail SaveProofOnClick sends TotalMinor 0 | by design — DealOrderProof_Add computes Subtotal + Tax when TotalMinor = 0 (S10). No change |
| 2 | DealForm edit sends LostReason "" → editing a Lost deal fails with lost_reason_required | **fixed in A7** |
| 3 | DealForm / CompanyForm / ContactForm load errors silent | **fixed in A7** |
| 4 | CompanyForm / ContactForm "Unassigned" sends an empty OwnerId to a mandatory input | not a bug — mandatory means "must be wired"; an empty identifier = unassigned in Sales. No change |
| 5 | Workspace switch navigates to Home; on Home nothing re-fetches | **fixed in A7** (full reload) |
No screen should error with the demo data: no Get call with Id 0 (forms skip Deal_Get/Company_Get/Contact_Get in create mode), no List.Current outside a list, bar width guarded against /0, no navigation loop (Workspaces uses IsWorkspacePicker). Low items left: GetOwners errors silent on Deals/Pipeline; CompanyDetail calls Company_Get twice (deals filter); Login runs the role check also after a failed login (harmless); Workspaces empty state is a dead end for a user without any workspace (Workspace_CreateFirst UI not built).

## A7 polish + smoke-test fixes — PUBLISHED rev 13 (env_app verified, 2026-11-11 19:55 UTC)
| Element | Old → New |
|---|---|
| DealForm | + local LostReason loaded from the deal and sent by Deal_Update (was ""); Next step / Source / Competitor / Description visible in create mode too; load-error alert (GetFormContext / GetDeal) + Save disabled on load error |
| CompanyForm, ContactForm | load-error alert + Save disabled; IsSaving set around the update call |
| MainLayout SwitchWorkspace, Workspaces OpenWorkspaceOnClick | navigate to Home → RedirectToURL(GetOwnerURLPath()) (full reload) |
| ContactDetail, CompanyDetail, Home | NullDate guards → "—" (Created/Updated, Last activity, Recently updated) |
| CompanyDetail | Contacts / Deals cards: skeleton while loading, error alert, EmptyState only after fetch |
| DealDetail | Edit visible only when CanEdit; GetHistory error alert |
| StageChangeDrawer | backdrop event OnClick → onclick (same as MainLayout) |
Not done in A7 (needs a visual check in the browser; no screenshots possible through the MCP): spacing / heading comparison with the source pages.
Validation 0 errors, 1 warning (Login public screen calls UI_StartSession — expected).

---
# Run 2026-11-12 morning — UI parity fixes P1–P7 (Salenix only, standing approval, hard stop 9:15)
Mentor session dd6e4202. No Sales / Foundation / reference change. Start: Salenix rev 13.

## Parity backlog (after demo)
Every L item from the Step 1 parity table, with the server change it needs (none of these can be done UI-only).
| # | Screen | Missing vs source | Server change needed |
|---|---|---|---|
| L1 | Deals list | Contact column, risk score / band, offering chip, Created column | DealListItem + ContactName, RiskScore, RiskBand, OfferingName, CreatedAt (Sales Deal_List / Deal_ListPipeline) |
| L2 | Deals list | Chips Unassigned, Stale only, Won · PO missing, Revisit due, Slipped only; amount range, close-date range, company filter, score filter | Deal_List inputs Unassigned, StaleDays, PoMissing, RevisitDue, SlippedOnly, AmountMinorMin/Max, CloseFrom/To, CompanyIds, ScoreBand |
| L3 | Pipeline | Won / Lost columns, weighted total and at-risk total per column, card contact name, drag-and-drop | Deal_ListPipeline: closed columns (opt-in), WeightedMinor + AtRiskMinor per PipelineColumn, ContactName on DealListItem |
| L4 | Home (admin) | Admin KPI set (weighted pipeline, win rate, avg deal size, avg sales cycle), revenue trend chart, stage donut, rep filter, range filter | Dashboard_GetSummary: WeightedPipelineMinor, WinRateBps, AvgDealSizeMinor, AvgCycleDays, monthly revenue trend list, Range + OwnerUserId inputs; companies / contacts counts as outputs (UI now uses Company_List / Contact_List TotalCount) |
| L5 | Home | Persona-specific homes (manager team view, member "my work" with tasks) | tasks (CrmTask actions), team metrics (Finance TeamMetricSnapshot), per-rep summary |
| L6 | Approvals | Recently approved / recently rejected lists, bulk approve / reject, margin % on the card | Deal_ListRecentDecisions(Days), Deal_ApproveMany / RejectMany; DealListItem + MarginBps |
| L7 | Deal detail | Activity timeline, tasks, notes, comments, documents (FileAttachment), value split, deal score, risk score, relationship health, viewers, compose email, all deal contacts | ActivityLog / CrmTask / Note / Comment list + create actions; FileAttachment upload (Integration storage); DealValueSplit, DealScore, DealRiskScore read actions; DealContact list action |
| L8 | Companies | Chip "Has active deals", Last activity + Created columns on the list, bulk select / bulk owner change / bulk delete | Company_List HasActiveDeals input; Company_BulkAssign / BulkSoftDelete (CompanyItem already has LastActivityAt / CreatedAt — column add is UI-only, left for after the demo) |
| L9 | Contacts | Remaining chips (has email, recently added), bulk select / owner change / delete | Contact_List inputs; Contact_BulkAssign / BulkSoftDelete |
| L10 | Lists | Server-side lookup lists (hold / rollback reasons, PO types) instead of client copies | Foundation Lookup_List(ListCode) service action |
| L11 | Deals list (L3 toolbar, 2026-11-12) | Chips Amount ▾, Close date ▾, Company ▾, Unassigned, Stale only, Won · PO missing, Score ▾, Revisit due, Slipped only (= L2) | Deal_List inputs AmountMinorMin/Max, CloseFrom/To, CompanyIds, Unassigned, StaleDays, PoMissing, ScoreBand, RevisitDue, SlippedOnly |
| L12 | Deals list (L3) | Stage chip is single-select (source StagePopover = multi-select); Owner chip single-select (source OwnerPopover = multi) | Deal_List StageCodes (list / comma text) and OwnerUserIds inputs; then popover multi-select chips |
| L13 | Deals list (L3) | Export (CSV/Excel of the filtered list), Bulk upload, saved views, bulk select (stage / owner / delete) | Deal_Export (same filters → file), Deal_BulkImport, SavedView entity + actions, Deal_BulkChangeStage / BulkAssign / BulkSoftDelete |
| L14 | Deals list (L3) | Sort direction toggle (asc/desc per column), "Updated" and Risk sort columns (source has only recent / risk toggle) | Deal_List SortDesc input + "risk" SortBy code (needs DealRiskScore) |
| L15 | Companies list (L3b, 2026-11-12) | Chips Has active deals, Recently active (14d), New (30d), Stale (>30d or never) (= L8 chip); Last activity + Created sort columns; sort direction toggle (fixed ↓) | Company_List inputs HasActiveDeals, RecentActivityDays, CreatedWithinDays, Stale; Last activity / Created columns are UI-only (CompanyItem has LastActivityAt / CreatedAt, SortBy activity / recent exist) |
| L16 | Contacts list (L3b) | Chips Recently active (14d), New (30d) (= L9) ("No company" chip DONE in L4, rev 30, UI-only via Contact_List NoCompany) | Contact_List inputs RecentActivityDays, CreatedWithinDays |
| L18 | Deal create/edit drawer (L4, 2026-11-12) | Offerings field (source OfferingsField, workspace switch 0067), unit price × quantity pricing mode (source DealAmountFields), custom fields, JPY (0-decimal currency; form sends amount ×100), create directly in Lost (source shows Lost reason when stage = lost; our create stages = 5 open stages), several deal contacts | Offering list + DealOffering write in Deal_Create / Update; UnitPriceMinor / Quantity inputs; CustomFieldDefinition list + CustomFieldValue write actions; currency minor-unit table (Foundation or Sales function); Deal_Create Stage lost + LostReason check; DealContact list / add actions |
| L19 | Company + Contact create/edit drawers (L4) | Custom fields (source CustomFieldInputs on both forms); bulk edit stays in L17 | CustomFieldDefinition list (entity company / contact) + CustomFieldValue write in Company_/Contact_Create / Update |
| L17 | Companies + Contacts (L3b) | Bulk select + bulk owner change + bulk delete; Export (CSV/Excel of the filtered list) | Company_/Contact_BulkAssign, BulkSoftDelete; Company_/Contact_Export (same filters → file) |
| L20 | Deal create/edit drawer (L5, 2026-11-12) | Company dropdown option "Other" = individual customer without a company (submits no company, Contact list shows company-less contacts); Contact dropdown enabled only after Company or "Other" is chosen and lists only that company's contacts; an existing deal with a contact but no company opens on "Other" (source deal-company-contact-fields.tsx) | Contact options need CompanyId (or a Contact_ListOptions CompanyId / NoCompany filter — check the current inputs); the rest is UI |
| L21 | Deal create/edit drawer (L5) | Owner field only for owner + manager (source create-deal-form.tsx canAssign; manager list excludes owners); every other role submits itself as owner. Today every role sees the field and the server rejects a forbidden owner | UI needs the acting role (Foundation_GetMyContext RoleCode) — check whether the Salenix UI wrappers already expose it |
| L22 | Edit deal drawer (L5b) | Delete button left in the edit-deal footer (company / contact have it since L5b) | Sales Deal_SoftDelete (+ Deal_Restore, Deal_List IncludeDeleted) with permission and audit log; Deals "Recently deleted" view |
| L23 | Deal detail Context card (L5b) | Inline edit of Next step / Source / Competitor on the detail page (source); today they are edited only in the edit drawer because Deal_Update overwrites every field | Sales Deal_UpdateContextField(DealId, FieldCode, Value) — partial update of one context field |
| L24 | App shell top bar (seen in the live source app, 2026-11-13) | Global search / command palette: top-bar button "Search deals, companies, contacts…" with the shortcut hint (⌘K on Mac, Ctrl K elsewhere); palette with quick actions (New deal / company / contact, tasks) and live results grouped by deals, companies, contacts (+ workflows), 5 per group, ILIKE on deal name, company name / domain, contact first / last name / email; deal → detail, company / contact → list filtered by the name (source header.tsx, command-palette.tsx, api/search/route.ts) | Sales Search_Global(Term) service action (acting tenant + persona scope, MaxPerGroup 5) returning deals / companies / contacts; UI: palette block in MainLayout + JS Ctrl K / ⌘K listener |
| L25 | Rail (seen in the live source app, 2026-11-13) | "Collapse" button at the bottom of the rail (ChevronsLeft + "Collapse"; collapsed rail w-14 shows only icons, ChevronsRight to expand, aria-label / title "Collapse sidebar" / "Expand sidebar"); state kept per browser in localStorage crm.sidebar.collapsed (source sidebar.tsx CollapseToggle, dashboard-shell.tsx) | none — UI only: theme already has sx-rail-collapsed + sx-rail-footer and `.sx-rail-collapsed + .sx-shell-main` margin; needs the toggle in UIKit AppLayout / MainLayout, an IsCollapsed input and a localStorage read/write (JS) |

## Batch results (all published clean, 0 errors / 1 known warning: Login public screen; env_app verified each time)
| Batch | Rev | Elements | Deviations / logged choices |
|---|---|---|---|
| P1 Sidebar | 14 | MainLayout: 8 rail widgets → If IfRailPersona (GetShell.Persona = "admin" or "manager" → ADMIN tree; else member tree "My Work" › Dashboard, My Deals, Pipeline + CRM) | Manager tree = admin tree (other MANAGER_NAV items do not exist). Persona values from Foundation: admin / manager / sales |
| P2 Home | 15 | data actions GetGreeting (Workspace_ListMine, GetMyContext, System User first name), GetCounts (Company_List / Contact_List TotalCount); GreetingRow replaces PageHeader (client-side time of day); IfCountChips (admin only) | Lists called with MaxRecords 25 (Company_List coerces other sizes to 50); greeting falls back to "Hello." |
| P3 Deals + Pipeline | 16 | GetWorkspaceName on both; ViewTabs List/Board (sx-btn secondary/ghost); subtitles; Deals columns Name · Stage · Amount · Company · Owner · Close date · Approval · Health; chips MineChip / PendingChip / ApprovedChip (ToggleMine / TogglePending / ToggleApproved) on the shared filter variables; old "Pipeline view" / "List view" links deleted | Chips styled as sx-btn primary (active) / secondary (theme sx-chip is for removable chips) |
| P4 Pipeline cards | 17 | card class + sx-metric-success / -warning / -danger by HealthBand; IfCompany line; IsHoldExpanded, ExpandHold / CollapseHold, IfHoldCollapsed (HoldSummary) / IfHoldExpanded (Collapse link) | LOGGED: theme has no left-border class → closest = sx-metric-* tint (border + background) |
| P5 Deal detail | 18 | AllDealsLink, HeaderSummaryLine, DealColumns = OutSystemsUI ColumnsMediumLeft (phone break); left: Deal Info (incl. governance + probability), Relationship, Context, Record; right: PO card, history error, Stage history, Close date changes, Audit log (re-created by copy; AddProofButton2 / SaveProofButton2 / CancelProofButton2 / IfHistoryError3) | LOGGED: 6 KPI tiles dropped (source has none; values in Deal Info + header line); left column ≈ 1/3 not 320px |
| P6 Approvals | 19 | AllDealsLink, title "Deal approvals", ApprovalsSubtitle (source text), ApprovalsStack / ApprovalsList card list (buttons rebuilt, same screen actions + RejectReason) | Container = sx-page (no max-w-4xl class); Updated column + Pending badge dropped (not in source card) |
| P7 Companies + Contacts | 20 | OwnerChips (MineChip / UnassignedChip, ToggleMine / ToggleUnassigned) replace OwnerDropdown on both; Companies columns Name · Industry · Open deals · Open · Won · Contacts · Owner; Contacts Name · Email · Title · Company · Phone · Owner · Created | Value kept as two columns Open / Won (fallback); OwnerFilter "all" = "" |
| L3 Deals toolbar | 27 | Header subtitle in PageHeader (Deals + Pipeline); DealsToolbar (search icon + live search 250 ms JS debounce → ApplySearch, New deal at row end); FilterBar chips Stage ▾ / My deals / Awaiting approval / Approved / Include won/lost / Owner ▾ / Clear all (immediate); header sort links Name / Amount / Close date → SortByColumn; EmptyState 3 variants | LOGGED: debounce 250 ms (source) not 300; Stage + Owner single-select (Deal_List StageCode / OwnerUserId, L12); fixed sort arrows, no direction toggle (L14); empty text "with the New deal button" (source "form on the right"); Pipeline keeps header New deal; UI_ApprovalOptions / UI_DealSortOptions unused (2 warnings); Mentor read-back: no High, Medium = $actions in debounce untested at runtime |
| L3b Companies + Contacts toolbar | 28 | Subtitle = workspace name (GetWorkspaceName); count in pagination line; toolbar search (icon, 250 ms JS debounce → ApplySearch) + New button at row end; FilterBar chips My companies / My contacts, Unassigned, Company ▾ (Contacts), Clear all, Recently deleted → ToggleDeleted + DeletedBanner "Showing deleted … · Back"; Companies header sort links Open deals / Open / Won ↓ → SortByColumn; EmptyState 4 variants | LOGGED: debounce 250 ms (source + L3) not 300; fixed ↓ arrows, second click → recent (no direction toggle); no Contacts sort (no server input, none in source); empty text "with the New … button" (source "form on the right"); "No company" chip possible UI-only (Contact_List NoCompany exists) but left with L16; separate Recently deleted page = open list; OwnerOptions unused (cleanup); read-back: no High / Medium, 3 Low (platform OnChange already debounced; Clear all ignores search + deleted view; SortDesc fixed) |
| L4 Drawers + PageHeader | 29–30 | PageHeader subtitle on its own line (MainLayout CSS: .sx-page-header .sx-page-title / .sx-page-subtitle display block; UIKit renders sibling spans); SxDrawer block (Title, Subtitle, IsOpen, IsWide, ShowFooter, OnClose, Body / Footer placeholders, theme sx-drawer* classes, backdrop + X + Esc close, body scroll lock, WasOpen guard); CompanyFormBlock / ContactFormBlock / DealFormBlock (logic moved from the form screens, events Saved(Saved*Id, SavedIsNew) / Cancelled, sticky sx-form-footer); 8 drawers: Deals / Pipeline Create deal, DealDetail Edit deal, Companies Add company, CompanyDetail Edit company + Add contact, Contacts Add contact, ContactDetail Edit contact; old DealForm / CompanyForm / ContactForm screens host the same blocks (direct URLs); Deal form + placeholders, Deal type (new_logo / renewal / expansion / upsell), Currency (USD EUR GBP INR CAD AUD), "Amount" label; Company / Contact placeholders as source; Contacts "No company" chip | LOGGED: drawer width = source max-w-md 28rem (theme), not 40%; source create toasts do not exist → kept "X created." / "X saved."; CompanyDetail / ContactDetail "New deal" still open DealForm (no company / contact prefill); Home has no New deal (none in source either); JPY left out; no focus trap / no unsaved-changes prompt; review fixes in the same batch: Saved event params renamed Saved* (screen handler read the screen input → Id 0 on create), Validation None + Is Default False on non-Save buttons, client AllExceptions on save actions ("Could not save. Check your connection and try again."); left Low: double click before FindSimilar returns, filters can hide a new record, Companies list MoneyText uses CurrencyCode "INR" fixed (rollups are cross-currency sums, as source); check the deal screens once a non-INR deal exists |
| L5 Drawer + form parity | 31 | SxDrawer header: title / subtitle block (sx-drawer-heading) + "×" close (sx-drawer-close); form blocks without the OutSystems form card (sx-form-block, gap 16); Deal create order Name, Description, Stage, Deal type + hint, Amount + Currency row, Margin % (optional in lead / hold / lost, "%" suffix, hint), Expected close date only from qualified (required from proposal), Company, Contact, Owner; Next step / Source / Competitor edit only; create footer full-width "+ Create …"; Lost reason mandatory in edit | Backlog L20 (Company "Other" + contact enable rule), L21 (Owner field only for owner / manager). Details of the Mentor session lost with the old laptop (commit 9c2469d never pushed); re-created 2026-11-13 from the user's notes + live read |
| L5b Edit footer + live dedup + max lengths | 32 | Edit footer as source: Delete left (company / contact, inline confirm), Cancel + Save changes right; live duplicate check (company 300 ms ≥ 2 chars amber box in the Name field, Save with DedupOverride; contact 400 ms red email box / amber name box; "Create anyway" removed); Max Length = lower of entity and server limit (Deal / Company Name 200, Industry 100, contact names / Title 100, Phone 50, NextStep 500, Source 100, Competitor 250) | LOGGED: inline Delete confirm = deliberate difference (source deletes without confirm); deal Delete → L22; Next step / Source / Competitor stay in the edit drawer (Deal_Update full overwrite) → L23; OPEN: Sales Company_Create accepts Domain 255, attribute 250 (commit 045658c lost, re-created) |
| TC1 part 1 Theme v2.2 + logo | Theme 6, UIKit 5–6, Salenix 33 | Theme v2.2 section (all Salenix app CSS appended, duplicates merged, sx-dup-* contact default / company under .sx-field); UIKit rev 5 PageHeader TitleGroup sx-page-titles; Studio by hand: UIKit rev 6 image SalenixLogo (salenix-logo-128.png) + Image RailLogo (sx-rail-logo) in AppLayout RailBrand, released; Theme rev 6 released; Salenix rev 33 = references + app icon + favicon | Studio publish of Salenix = deliberate exception (Mentor cannot import images). Commit f40f6b2 lost, re-created |
| TC1 part 2 App CSS removal + Login logo | Salenix 34, UIKit 7 | Live read 2026-11-13: every Salenix stylesheet empty; Login Image AuthLogo (UIKit SalenixLogo, sx-auth-logo) in sx-auth-brand; app icon set | Published together 2026-11-12 11:21 UTC (Studio-style); no record of the session. NEW: 27 Phosphor 2.0 icon warnings (see Recovery section) |
| P8 review fixes | 21 | UI_HealthTone + "strong" → success, "moderate" → warning (source bands strong / moderate / at_risk); Approvals: old PageHeader "Deals" button deleted | — |

## Read-back review (READ-ONLY, rev 20) — no High findings
| # | Sev | Finding | Decision |
|---|---|---|---|
| 1 | Medium | Persona compared case-sensitively ("admin", "manager") | not a bug: Foundation ResolveContext returns lower-case admin / manager / sales |
| 2 | Medium | UI_HealthTone did not know source bands strong / moderate | **fixed in rev 21**. Left: UI_HealthTone maps at_risk → warning (amber) while the board card uses danger (rose, as source) — align after the demo |
| 3 | Medium | GetCounts passes Company_List OwnerFilter "all" | not a bug: Company_List accepts all / mine / unassigned (A4); failure only hides the chips |
| 4 | Medium | My deals chip: owner dropdown may show "All owners" if Member_ListOptions lacks the current user | check in browser (Member_ListOptions returns every active member) |
| 5 | Low | Rail shows member tree for a moment until GetShell loads (admin flash) | after demo |
| 6 | Low | Companies / Contacts still fill unused OwnerOptions; Deals subtitle lost "N deals" count | after demo |
| 7 | Low | DealDetail columns break only on phone (tablet keeps 2 columns); Relationship company is plain text | after demo |
Checked OK: no List.Current on empty lists, NullDate guards, no Get with Id 0, new data-action reads behind fetched checks, copied PO form still bound to ReferenceNo / IssuedOn / BuyerName / SubtotalRupees / TaxRupees / ProofType, all Approve / Reject / Confirm / Cancel and PO buttons wired to the same screen actions with their arguments.

# Recovery 2026-11-13 (new laptop; READ-ONLY, no app changed, nothing published)
The old laptop died. GitHub main ends at 2248206 (L4); the unpushed commits 1f0354b, 9c2469d (L5), 045658c (L5b) and f40f6b2 (TC1 part 1) are lost. Re-created in one recovery commit: CLAUDE.md SOURCE PARITY FIRST rule + build-status lines L5 / L5b / TC1, the batch rows and backlog rows L20–L23 above, theme/salenix-theme.css (read back from Salenix_Theme rev 6), theme/THEME-GUIDE.md v2.2, theme/logo-png/ (re-rendered from theme/logo.svg with resvg).

## Theme read-back (Mentor session 011a7888, REPORT ONLY)
- Salenix_Theme rev 6: one theme Salenix_Theme (base OutSystemsUI, icon library Phosphor 2.0), one stylesheet: 1,542 lines, 51,247 chars, no CR, no trailing line break. Sections: v2 header line 3, v2.1 line 1174, v2.2 line 1294 (App shell rail 1302, Page header title group 1368, Auth shell 1380, List toolbar 1453, Drawer 1483, Form blocks 1494, Live duplicate warning 1524).
- Lines 1–1292 equal the committed v2.1 file (same line numbers, 38,207 chars; Mentor: lines 1–1293 = 38,208 incl. blank line 1293); lines 1294–1542 printed verbatim (13,039 chars). Restored file: 1,542 lines / 51,247 chars — matches.

## TC1 part 2 live status (2026-11-13)
| App | Live rev (Development) | Note |
|---|---|---|
| Salenix | 34 (2026-11-12 11:21:47 UTC) | rev 33 11:16 = Studio (refs + icon + favicon); rev 34 published in the same second as UIKit rev 7 |
| Salenix_Theme | 6 (11:04 UTC) | library |
| Salenix_UIKit | 7 (11:21:46 UTC) | library; 0 errors / 0 warnings |
| Salenix_Sales | 19 | unchanged since R1 |
| Salenix_Foundation | 28 | unchanged since F-DEV1 |
| Salenix_DevTools | 17 | unchanged since T10 |
- Salenix stylesheets: MainLayout, Login, Deals, Companies, Contacts, SxDrawer, DealFormBlock, CompanyFormBlock, ContactFormBlock = empty (0 chars); every other screen / block empty; no local theme; default theme Salenix_Theme.
- Login: Image AuthLogo → UIKit image SalenixLogo, class sx-auth-logo, alt "", parent sx-auth-brand (inside sx-auth-header), followed by the text "Salenix". There is no widget named SalenixLogo — SalenixLogo is the UIKit image resource; the rail widget is RailLogo.
- Salenix validation: 0 errors, 30 warnings = 1 Login UI_StartSession (known), 2 unused client actions UI_ApprovalOptions / UI_DealSortOptions (known), 27 icons not in the Phosphor 2.0 icon library (NEW).
- Unfinished Mentor runs: cannot be listed (session ids lived on the old laptop); a closed session discards its draft, and the live revisions show no half-published state.

# TC1-fix 2026-11-13 — logo size, Phosphor icons, chip arrow (Mentor sessions 160d285c change + publish, cb9a744d read-back)
Browser check after TC1 (Salenix rev 34 /Deals): rail logo ~120px pushing the wordmark ("Salen"), empty search icon gap, two arrows on the Stage / Owner chips, Dashboard rail item without icon.

## Read-only findings
| # | Symptom | Cause |
|---|---|---|
| 1 | Rail logo ~120px | Salenix rev 34 references UIKit rev 6 (fixed): RailLogo Style Classes "sx-Rail-logo" (capital R) → `.sx-rail-logo` never matched; Width empty; SalenixLogo = 128×128 PNG. UIKit rev 7 already had the right case but was not referenced |
| 2, 4, 5 | No search / rail / button icons | Salenix_Theme theme property IconLibrary = Phosphor 2.0 (base OutSystemsUI = FontAwesome4.0); all 27 Icon widgets used Font Awesome names. The rail Dashboard item had icon "home" (invalid), not "no icon" |
| 3 | Two arrows on dropdown chips | Native Dropdown renders `.dropdown-container > select.dropdown-display`; OutSystemsUI 3.0.0 draws the arrow with `.dropdown-container:after` (content var(--osui-icon-arrow-down)); our `.sx-fbar select.sx-fchip.sx-fchip` draws a second caret |
OutSystemsUI reference: 3.0.0 (Salenix producer rev 174). Whether IconLibrary was already Phosphor before Theme rev 6 cannot be read (Mentor cannot open earlier revisions).

## Parity checklist
| # | Item | Source | Result | Status |
|---|---|---|---|---|
| 1 | Rail logo | SaleinxIcon size 22, shrink-0, gap 8px, wordmark 20px | RailLogo Width 22px + sx-rail-logo (flex 0 0 22px, max 22px, object-fit contain) | same after the Studio reference refresh |
| 2 | Login logo | 36px | AuthLogo Width 36px + sx-auth-logo 36px | same after refresh (class already matched) |
| 3 | Search icon | lucide Search, slate-400, left 12px | magnifying-glass, sx-dsearch-icon unchanged | same (live in rev 35) |
| 4 | Chip dropdown arrow | one ChevronDown, slate-400 / indigo-600 active | CSS caret (currentColor) kept, OSUI arrow hidden in .sx-fbar | same after refresh |
| 5 | Rail icons | LayoutDashboard, GitBranch, Briefcase, BadgeCheck, UsersRound, Building2 | squares-four, git-branch, briefcase, seal-check, users, buildings | same (closest Phosphor) |
| 6 | New … buttons / filter label / Clear all / drawer close / bell | Plus, Filter, X, X, Bell | plus, funnel, x, x, bell | same |
| 7 | EmptyState icons | lucide per screen | UIKit renders "fa fa-<name>" classes; not changed | Parity backlog (open list) |

## Changes (all published, env_app / app_list verified)
| App | Rev | Change |
|---|---|---|
| Salenix_Theme | 7 | `.sx-rail-logo, .sx-rail-brand img.sx-rail-logo` and `.sx-auth-logo, .sx-auth-brand img.sx-auth-logo` = display block, flex 0 0 22/36px, width/height + max 22/36px, object-fit contain; new `.sx-fbar .dropdown-container::after { display: none; }` after the select caret rule. 1,542 → 1,551 lines, 51,247 → 51,492 chars; 0 errors, 1 known warning (Google Fonts @import) |
| Salenix_UIKit | 8 | AppLayout RailLogo Width 22px (no Height property on the Image widget). 0 / 0 |
| Salenix | 35 | Icon names: search → magnifying-glass (Deals, Companies, Contacts SearchIcon); filter → funnel (3 FilterLabel); times → x (3 ClearAllLink, MainLayout CloseDrawer, StageChangeDrawer); home → squares-four (rail Dashboard ×2); columns → git-branch (rail Pipeline ×2); check-circle → seal-check (rail Approvals); building → buildings (rail Companies ×2); re-selected same name: plus (4 New buttons), briefcase (×2), users (×2), bell. Login AuthLogo Width 36px. 0 errors, 3 known warnings; stylesheets empty; references unchanged (Theme 6, UIKit 6) |
Read-back (fresh session cb9a744d, Salenix 35 / UIKit 8 / Theme 7): 29 Icon widgets, none warns; RailLogo 22px "sx-rail-logo"; AuthLogo 36px "sx-auth-logo"; theme rules as above.

## Studio step (user, by hand) — DONE 2026-11-13 (Salenix rev 36)
1. Salenix_Theme: open rev 7 in ODC Studio → release it (as done for rev 6).
2. Salenix_UIKit: open rev 8 → release it.
3. Salenix: open a fresh copy of rev 35 → Manage Dependencies → refresh Salenix_Theme (→ 7) and Salenix_UIKit (→ 8), nothing else → publish from Studio (expected Salenix rev 36).
4. Then: app_list / env_app check + browser test (rail logo 22px with full "Salenix", Login logo 36px, search icons, one arrow on Stage / Owner / Company chips, rail icons, EmptyState icons).

## TC1-fix close-out (2026-11-13, READ-ONLY, Mentor session baad1935)
- app_list / env_app: Salenix rev 36 (Studio publish 2026-11-12 20:06 UTC), Salenix_Theme 7, Salenix_UIKit 8.
- Salenix rev 36 references: Salenix_Theme 7 (pinned), Salenix_UIKit 8 (pinned), OutSystemsUI 174 / 3.0.0, OutSystemsCharts 13, OutSystemsMaps 17, Salenix_Foundation 28, Salenix_Sales 19, (System) 17. Default theme Salenix_Theme; no local theme; all screen / block stylesheets empty.
- Browser check (user): rail logo 22px with the full wordmark, Login logo 36px, all icons visible, one arrow on the chips; EmptyState Font Awesome classes still render (empty Deals list shows the briefcase) → EmptyState item closed.
- Validation rev 36: 0 errors, 30 warnings = 27 "icon not available in the current icon library (Phosphor2.0)" (now on the Phosphor names; icons render) + Login UI_StartSession + 2 unused client actions. Logged as a low open item in CLAUDE.md (re-select the icons in the next Mentor change of Salenix).
- New Parity backlog rows: L24 global search (Ctrl K), L25 rail Collapse button.

## T10 fix + customer import (2026-11-13)
| Step | App / rev | Result |
|---|---|---|
| T10 fix | Foundation 29, DevTools 19 | DevOnly_AddMember builds the profile from UserId (pending-registration users are not visible in the User entity); T10 shows real reasons. TESTED: 4 founder accounts created and signed in. |
| Core refactor | Sales 20 | Deal_CreateCore / Company_CreateCore / Contact_CreateCore private; Deal_Create / Company_Create / Contact_Create = Sales_Context → Core (signatures unchanged). T8 recheck pending (2026-11-14, CGB India demo workspace). |
| Import | Sales 20–21, DevTools 19–20 | DevOnly_ImportBatch + T11. Fixes: T11 Text Area Max Length 500 → none (DevTools 20); swapped companies / deals Switch targets (Sales 21). |
| T11 run (Vizion, owner = importer) | — | TESTED: 72 batches; companies 250 created, contacts 437, notes 437, deals 249 (lead, 0 INR), deal-contact links 437, failed 0. Re-run check TESTED (1 Companies): created 0, existing 250, failed 0. |
Source file: data/salenix-import-contacts.json (git-ignored; counts only here).

# Overnight run N — Salenix_Sales server part of P2 (2026-11-13 night)
Standing approval (user): batches N1–N9, Salenix_Sales only, ADD-only (new actions / structures / attributes / optional inputs), no data writes (Vizion = real data), publish + env_app verify + commit per batch. Source specs for N3–N9: notes/overnight-N-source-specs.md (file:line refs). Mentor session fefd9199 (read-only pattern check first: Company_SoftDelete node text, exceptions SalesError / ValidationError, every existing deal aggregate already filters DeletedAt = NullDate()).

## N1 L22 Deal soft delete + N2 L23 context field — PUBLISHED Sales rev 22 (env_app verified 2026-11-12 22:19 UTC)
### Parity checklist
| # | Item | Source | Rebuild | Status |
|---|---|---|---|---|
| 1 | Delete permission | deleteDeals (owner, admin) ACT:2019 | Sales_RequirePermission deleteDeals | same |
| 2 | Delete guard + text | active row in tenant, "Deal not found or already deleted." | same + persona scope (owner/admin are never sales → no effect) | same |
| 3 | Delete write | deleted_at, deleted_by; activity deleted {name} | + UpdatedAt; activity deleted {name}; Company_RefreshRollups | same (+ rollups; source VIEW recomputes itself) |
| 4 | Restore | restoreDeals; any row in tenant; "Deal not found in this tenant."; activity restored | only deleted rows (stricter, as Company_Restore) | different on purpose (consistency) |
| 5 | Deleted list | listDeletedDeals: deleted only, deleted_at desc | Deal_List IncludeDeleted → deleted only, all stages, most recently deleted first; roles without deleteDeals get an empty list | same |
| 6 | Realtime deal.deleted / restored, embeddings | yes | not built (no realtime / AI yet) | not possible yet |
| 7 | Hard delete (hardDeleteDeals, owner) | yes | not in the queue | backlog |
| 8 | Context field | updateDealFieldAction: field nextStep / competitor / source, manageDeals, value trimmed, empty → null, max 2000, "Invalid field update.", "Deal not found in this tenant.", activity updated {name, field} | Deal_UpdateContextField(DealId, Field next_step / source / competitor, Value): same; limits = column lengths 500 / 100 / 250 (source 2000 exceeds our columns); persona scope added; output SavedValue | same (limits = our columns) |
| 9 | Score recompute on edit | queueDealScoreRecompute | not built (no scoring job) | not possible yet |
### Elements (rev 22)
| Element | Change |
|---|---|
| Deal_SoftDelete(DealId) → Success, OutcomeCode (ok / not_found), Message | NEW public service action |
| Deal_Restore(DealId) → same | NEW public service action |
| Deal_UpdateContextField(DealId, Field, Value) → Success, OutcomeCode (ok / invalid_field / validation / not_found), Message, SavedValue | NEW public service action (Switch next_step → NextStep, source → Source, competitor → Competitor; Otherwise unreachable → same next node) |
| DealListItem.DeletedAt (Date Time) | NEW attribute |
| Deal_List IncludeDeleted (optional, default False) | NEW input: Foundation_HasPermission(deleteDeals) gate → empty list; filter If(IncludeDeleted, DeletedAt <> NullDate, = NullDate); closed-stage filter skipped; NumKey = deleted time first |
Read-back: node text of all three actions, both If branches and every Switch target checked; validation 0 errors (warnings = known: unused FailedRules outputs, font URLs). A deleted deal cannot be opened (Deal_Get etc. keep their DeletedAt filter) — same as source.
Tests (CGB India only): as admin Deal_SoftDelete → ok; again → not_found; Deal_List(IncludeDeleted True) lists it; Deal_Restore → ok; member → "Permission denied: requires "deleteDeals" …". Deal_UpdateContextField(id, "next_step", " Call CFO ") → SavedValue "Call CFO"; Field "stage" → invalid_field.

## N3 list filters (L11, L12, L14, L15, L16) — PUBLISHED Sales rev 23 (env_app verified 2026-11-12 22:33 UTC)
Source: notes/overnight-N-source-specs.md §N3 (lib/deals/filters.ts + queries.ts listDeals, lib/companies, lib/contacts). Mentor prompt in 3 + 1 parts (one long prompt was refused by the MCP gateway with a CloudFront 403 "Request blocked" → split into smaller prompts written partly in words; no change was lost).
### Parity checklist
| # | Filter | Source | Rebuild | Status |
|---|---|---|---|---|
| 1 | Unassigned | owner_id IS NULL | Deal_List Unassigned | same |
| 2 | Stale | open (not won/lost, hold counts) and updated_at < now − staleDays (default 14) | StaleOnly + StaleDays (≤ 0 → 14), StaleBefore local | same |
| 3 | PO missing | won and NOT (all proofs in deal currency and |Σ subtotal − amount| ≤ 100) | PoMissing: GetPoSums (DealOrderProof ⋈ Deal, won, group DealId + AmountMinor, Σ SubtotalMinor, Σ CurrencyMismatch) → ValidPoDealIds ",id,…," → filter Index = -1; won/lost shown even without IncludeClosed | same |
| 4 | Revisit due | hold and hold_revisit_on <= CURRENT_DATE (UTC) | RevisitDue: hold, date set, ≤ Foundation_TodayFor | same (workspace date, decision 7) |
| 5 | Slipped only | push_count >= 1 | SlippedOnly | same |
| 6 | Score band | health_band = strong / moderate / at_risk | ScoreBand (any text, trim/lower) | same |
| 7 | Amount min / max | inclusive, minor units, >= 0 | AmountMinorMin / Max inclusive; **0 or less = no filter** (so "max 0" cannot be asked) | different on purpose (optional input default 0) |
| 8 | Close from / to | inclusive, NULL dates never match | CloseFrom / CloseTo inclusive, NullDate = no filter | same |
| 9 | Company | companyIds = company NAMES (deal company_name or linked company) | CompanyId (one linked company id) | different on purpose (ids; our deals always link Company) |
| 10 | Stages multi | stage IN list | StageCodes comma text (lower, spaces removed); also shows won/lost | same |
| 11 | Owners multi | owner_id IN list | OwnerUserIds comma text (IdentifierToText(OwnerId)) | same |
| 12 | Sort direction | source: recent / risk only, no direction | SortDir "" (today's direction per code) / asc / desc via SortFlip, NumKeyDir, TextKeyDesc; deleted view ignores it | our addition (L14) |
| 13 | Risk sort, missing-field, offering filters | yes | not built (no DealRiskScore data, completeness, offerings) | backlog |
| 14 | Companies Has active deals | open_deal_count > 0 | HasActiveDeals → OpenDealCount > 0 | same |
| 15 | Companies recently active N days | COALESCE(last_activity_at, epoch) >= now − N | RecentActivityDays → LastActivityAt set and >= RecentSince | same |
| 16 | Companies created within N days | created_at >= now − N | CreatedWithinDays | same |
| 17 | Companies stale | last_activity_at NULL or < now − staleDays (30) | Stale + StaleDays (≤ 0 → 30) | same |
| 18 | Contacts recently active | EXISTS activity_logs (contact, created_at >= now − N) | RecentActivityDays → GetRecentContactActivity (ActivityLog contact, group EntityId) → RecentContactIds ",id," → Index filter | same |
| 19 | Contacts created within N days | created_at >= now − N | CreatedWithinDays | same |
### Elements (rev 23)
- Deal_List: NEW optional inputs Unassigned, StaleOnly, StaleDays, PoMissing, RevisitDue, SlippedOnly, ScoreBand, AmountMinorMin, AmountMinorMax, CloseFrom, CloseTo, CompanyId, StageCodes, OwnerUserIds, SortDir; locals TodayLocal (Foundation_TodayFor), StaleBefore, StageList, OwnerList, ValidPoDealIds, SortFlip; If PoMissing (False → GetDeals; True → GetPoSums → For Each → final comma → GetDeals); GetDeals filters 9–21 new, filter 8 extended (StageCodes / PoMissing also show closed stages); calculated NumKeyDir, TextKeyDesc; sort NumKeyDir ↑, TextKeyDesc ↓, TextKey ↑, TieKey ↑ (SortDir empty → identical order).
- Company_List: NEW optional inputs HasActiveDeals, RecentActivityDays, CreatedWithinDays, Stale, StaleDays; locals RecentSince, CreatedSince, StaleBefore; GetCompanies filters 7–10.
- Contact_List: NEW optional inputs RecentActivityDays, CreatedWithinDays; locals RecentSince, CreatedSince, RecentContactIds; If RecentActivityDays > 0 (True → GetRecentContactActivity → For Each → final comma → GetContacts; False → GetContacts); GetContacts filters 9–10.
Read-back: every filter text, both If branches and the loop targets checked; validation 0 errors (known warnings only).
Tests (read-only, any workspace): Deal_List(PoMissing True) → only Won deals without a matching PO; StageCodes "lead,qualified" → both stages; OwnerUserIds = one id → same as OwnerUserId; SortBy "value" SortDir "asc" → smallest first; StaleOnly → open deals not updated for 14 days; Company_List(Stale True) → companies without activity in 30 days (imported companies with no activity included); Contact_List(RecentActivityDays 14).

## N4 tasks — PUBLISHED Sales rev 24 (env_app verified 2026-11-12 22:46 UTC)
Source: notes/overnight-N-source-specs.md §N4 (app/dashboard/tasks/actions.ts, lib/tasks/queries.ts + filters.ts, db/schema/tasks.ts).
### Parity checklist
| # | Item | Source | Rebuild | Status |
|---|---|---|---|---|
| 1 | Permission | manageDeals on every task action, no owner check | manageDeals + persona scope (sales: assigned to me or unassigned) on list AND writes | different on purpose (addendum scope) |
| 2 | List filters | search title, status list, priority list, assignedToMe, unassigned, dueWithinDays (incl. overdue), overdue (not completed / cancelled), entity panel (type + id, 50) | Task_List Search, StatusCodes, PriorityCodes, Mine, Unassigned, DueWithinDays, Overdue, EntityType + EntityId; page 25/50/100 default 50; dates vs Foundation_TodayFor | same (workspace date) |
| 3 | List sort | created_at desc, id desc | same | same |
| 4 | Linked name | deal name / company name / "First Last" or email | LinkedName via 3 left joins with EntityType in the join condition | same |
| 5 | Title / description length | 280 / 4000 | 250 / 2000 (CrmTask column sizes) | different on purpose (columns) |
| 6 | Codes | status todo/in_progress/completed/cancelled (default todo); priority low/medium/high/urgent (default medium); type call/email/meeting/internal/followup; recurrence daily/weekly/custom | same, messages "Pick a valid …" (source = zod enum errors) | same |
| 7 | Create | created_by actor; activity task created {taskTitle, taskPriority, dueDate, linked*}; notify assignee ≠ actor "New task: <title>", subtitle "Due <date>"; email to assignee | same; no email (Integration not built) | same / email not possible yet |
| 8 | Full update | writes title, description, status, priority, due, assignee, entity — NOT type / recurrence / stage_blocking (source quirk) | Task_Update writes ALL fields incl. type / recurrence / stage blocking | different on purpose (source bug: edits silently dropped) — OPEN QUESTION |
| 9 | Completed_at | now on first completion; kept otherwise (not cleared on reopen via full update) | same | same |
| 10 | Activity on update | task_completed if just completed, else updated {taskTitle, previousStatus, status} | same | same |
| 11 | Notifications on update | reassigned to someone ≠ actor: "Reassigned to you: <title>"; just completed and creator ≠ actor: "Task completed: <title>" to creator | same | same |
| 12 | Toggle | no-op when already in target state; complete → completed + now; reopen → todo + completed_at null | Task_SetCompleted(TaskId, Completed): "unchanged" outcome for the no-op; same writes | same |
| 13 | Recurrence | on every transition into completed: new task (copy, todo, created_by actor), due = old due or today + 1 day / + 7 days / + 1 month (custom); activity created {recurredFrom} | private Task_SpawnNext, called by Task_Update and Task_SetCompleted; SpawnedTaskId output | same |
| 14 | Delete | soft delete, idempotent, activity deleted {taskTitle} | Task_Delete same | same |
| 15 | Inline patch updateTaskFieldsAction | yes | not built (Task_Update covers it) | backlog |
| 16 | Task comments (task_comments table) | separate table | NOT built — Sales has no TaskComment entity (Comment.EntityType "task" is the CLAUDE.md design); Comment_* in N5 accept only deal/company/contact | OPEN QUESTION |
| 17 | Home "My tasks" summary (open / overdue / completed this week / top 5 upcoming) | yes | not built (Task_List Mine + Overdue covers counts via TotalCount) | backlog |
### Elements (rev 24)
| Element | Kind |
|---|---|
| TaskItem (Id, Title, Description, StatusCode, PriorityCode, TypeCode, RecurrenceCode, StageBlocking, DueDate, IsOverdue, AssignedUserId, AssigneeName, CreatedBy, CompletedAt, EntityType, EntityId, LinkedName, CreatedAt, UpdatedAt) | NEW public structure |
| Task_List(Search, Mine, Unassigned, EntityType, EntityId, StatusCodes, PriorityCodes, Overdue, DueWithinDays, StartIndex, MaxRecords) → Items, TotalCount | NEW public service action |
| Task_Create(Title, Description, StatusCode, PriorityCode, TypeCode, RecurrenceCode, StageBlocking, DueDate, AssignedUserId, EntityType, EntityId) → Success, OutcomeCode (ok / validation), Message, FailedRules (Text "; "-joined, as Company_Create), TaskId | NEW public service action |
| Task_Update(TaskId + same inputs) → Success, OutcomeCode (ok / validation / not_found), Message, FailedRules, SpawnedTaskId | NEW public service action |
| Task_SetCompleted(TaskId, Completed) → Success, OutcomeCode (ok / unchanged / not_found), Message, SpawnedTaskId | NEW public service action |
| Task_Delete(TaskId) → Success, OutcomeCode (ok / not_found), Message | NEW public service action |
| Task_Validate, Task_SpawnNext | NEW private server actions |
Read-back: full node text of all actions, every If branch target checked; validation 0 errors (known warnings).
Tests (CGB India only): Task_Create("Call CFO", PriorityCode "high", DueDate today−1, AssignedUserId = other member) → ok + notification "New task: Call CFO"; Task_List(Overdue True) lists it with IsOverdue True; Task_Create(RecurrenceCode "weekly", DueDate D) → Task_SetCompleted(id, True) → SpawnedTaskId > 0, new task due D+7, status todo; again → "unchanged"; Task_SetCompleted(id, False) → status todo, CompletedAt empty; Task_Delete twice → ok both; Title "" → validation "Title is required"; as viewer → permission denied manageDeals.

## N5 notes, comments, timeline — PUBLISHED Sales rev 25 (env_app verified 2026-11-12 23:04 UTC)
Source: notes/overnight-N-source-specs.md §N5 (app/dashboard/notes/actions.ts, comments/actions.ts, lib/comments/mentions.ts, lib/timeline/queries.ts).
### Parity checklist
| # | Item | Source | Rebuild | Status |
|---|---|---|---|---|
| 1 | Entity types | deal / company / contact | same; Sales_EntityVisible (exists, not deleted, not merged, persona scope) gates list / create | same + scope (addendum) |
| 2 | Note create permission | by type: manageDeals / manageCompanies / manageContacts | same (Switch) | same |
| 3 | Note body | trimmed, 1–8000, "Note can’t be empty" | same; > 8000 "Note must be 8000 characters or fewer" (source = zod max) | same |
| 4 | Note create activity | parent: updated {noteId, notePreview 120, fields ["notes"]} | same; + Company_RefreshRollups for company notes (LastActivityAt counts company notes) | same |
| 5 | Note update / delete | manageDeals; author only ("You can only edit / delete your own notes."); "Note not found."; delete idempotent + soft; no activity | same (OutcomeCode forbidden / not_found) | same |
| 6 | Note list | not deleted, created desc, 100 | same + AuthorName, CanEdit | same |
| 7 | Comment create | per-type permission; body 1–8000 "Comment can’t be empty"; one reply level (parent of a reply → its parent; missing parent → top level); mentions @[Name](id) → mentioned_user_ids; notify mentioned ≠ actor "Mentioned you in a comment", subtitle preview (@Name, whitespace collapsed, 80 + …); activity parent updated {commentId, commentPreview 80, kind comment} | same (private Comment_ParseMentions, max 50 tokens, ids ≤ 128) | same; LOW: an invalid token (empty id) stops parsing, the rest of the body is kept as text (source regex skips it) |
| 8 | Comment edit | author only, re-parse mentions, edited_at, no new notifications | NOT built (queue asked List / Create / Delete) | backlog |
| 9 | Comment delete | manageDeals, author only, idempotent, soft, no activity | same | same |
| 10 | Comment list | not deleted, created asc, 200 | same | same |
| 11 | Timeline | activity + notes + comments + emails, created desc, limit 150; activity rows with noteId / commentId / emailId skipped; company = company + up to 500 child deals + 500 child contacts; items carry source {type, id, name} | Activity_ListFor(EntityType, EntityId, MaxItems 1–300 default 150): activity + notes + comments, ListSort desc, trimmed; company child sets via ",id," text + Index; SourceName for child notes / comments | same except: no emails (Comms not built); activity SourceName empty (ActivityLog.EntityId is Text, cannot join) |
| 12 | Timeline UI filters (All / Mine / Teammates / Emails / …) | client side | UI filters on Kind / ActorUserId / Action | UI |
### Elements (rev 25)
| Element | Kind |
|---|---|
| NoteItem (Id, EntityType, EntityId, Body, AuthorUserId, AuthorName, CanEdit, CreatedAt, UpdatedAt) | NEW public structure |
| CommentItem (Id, EntityType, EntityId, ParentCommentId, Body, AuthorUserId, AuthorName, MentionedUserIds, CanEdit, EditedAt, CreatedAt) | NEW public structure |
| TimelineItem (Kind, Id, CreatedAt, ActorUserId, ActorName, Action, Body, Metadata, EntityType, EntityId, SourceName) | NEW public structure |
| Note_List(EntityType, EntityId) → Items | NEW public service action |
| Note_Create(EntityType, EntityId, Body) → Success, OutcomeCode (ok / validation / not_found), Message, NoteId | NEW |
| Note_Update(NoteId, Body) → Success, OutcomeCode (ok / validation / not_found / forbidden), Message | NEW |
| Note_Delete(NoteId) → Success, OutcomeCode (ok / forbidden), Message | NEW |
| Comment_List(EntityType, EntityId) → Items | NEW |
| Comment_Create(EntityType, EntityId, Body, ParentCommentId) → Success, OutcomeCode, Message, CommentId | NEW |
| Comment_Delete(CommentId) → Success, OutcomeCode, Message | NEW |
| Activity_ListFor(EntityType, EntityId, MaxItems) → Items (TimelineItem List) | NEW |
| Sales_EntityVisible, Comment_ParseMentions | NEW private server actions |
Read-back: full node text, every Switch / If / loop target checked; validation 0 errors (known warnings).
Tests (CGB India only): Note_Create("deal", id, "First call done") → ok; Note_List shows it with CanEdit True; as another member Note_Update → forbidden; Comment_Create("deal", id, "Ping @[Asha](<her user id>)") → Asha gets "Mentioned you in a comment" / subtitle "Ping @Asha"; reply to a reply → stored under the top comment; Activity_ListFor("company", id) → company rows + child deal / contact rows with SourceName on notes / comments, newest first, ≤ 150.

## N6 admin home KPIs — PUBLISHED Sales rev 26 (env_app verified 2026-11-12 23:15 UTC)
Source: notes/overnight-N-source-specs.md §N6 (lib/dashboard/kpis.ts, range.ts, queries.ts, trend-shaping.ts, kpi-strip.tsx, admin-home.tsx). Dashboard_GetSummary NOT changed.
### Parity checklist
| # | KPI | Source | Rebuild (Dashboard_GetKpis) | Status |
|---|---|---|---|---|
| 1 | Range | last_7d / last_30d / last_90d (default) / this_quarter / this_year / custom (≤ 730 days); UTC | same codes; dates from Foundation_TodayFor; window [From 00:00, To+1 00:00) UTC; RangeFrom / RangeTo / RangeLabel outputs | same (workspace "today") |
| 2 | Rep view | view=rep:<id> → owner filter (admin / manager) | OwnerUserId input; persona sales → always self | same (+ scope) |
| 3 | Total pipeline | live snapshot, stage not won / lost (hold included) | same (current stage; no history replay) | same for "now" |
| 4 | Prior pipeline + deltas | snapshot at range.from via deal_stage_history; deltas vs prior window | NOT built | backlog (needs stage-history replay) |
| 5 | Weighted pipeline | Σ stage total × stage probability / 100 (table lead 10 … hold 40; org overrides) | Stage_DefaultProbability table | same, without per-workspace overrides (no setting yet) |
| 6 | Win rate | won / (won + lost), closed_at in window | WinRatePct (1 decimal), WonCount, LostCount | same |
| 7 | Avg deal size | won sum / won count in window | AvgDealSizeMinor, WonValueMinor | same |
| 8 | Avg sales cycle | avg(closed_at − created_at) days, won, closed > created, 1 decimal | AvgCycleDays | same |
| 9 | Quota attainment + coverage | FY targets, quota_weight credit, pending, coverage ratio | NOT built (SalesTarget lives in Salenix_Finance) | not possible yet |
| 10 | Record counts | companies / contacts non-deleted (merged not excluded), active deals; tenant-wide | CompanyCount, ContactCount, ActiveDealCount | same |
| 11 | Revenue trend | daily won / lost by closed_at, new pipeline by created_at, shaped into ISO weeks (Monday) | RevenueTrend: TrendWeekItem per Monday week from RangeFrom to RangeTo (WonRevenueMinor, WonCount, LostCount, NewPipelineMinor, NewDealCount) | same (95th-percentile axis cap = UI) |
| 12 | Stage bars + donut | count + sum per stage, all 7, all time, owner filter | StageTotals (existing StageTotalItem) fixed order lead … lost, zeros included | same |
| 13 | Deal highlights (top wins, progress, new, at risk, closing soon) | 5 each | NOT built | backlog |
### Elements (rev 26)
- NEW public structure TrendWeekItem (WeekStart, WonRevenueMinor, WonCount, LostCount, NewPipelineMinor, NewDealCount).
- NEW public service action Dashboard_GetKpis(RangeCode, FromDate, ToDate, OwnerUserId) → RangeFrom, RangeTo, RangeLabel, TotalPipelineMinor, WeightedPipelineMinor, WonCount, LostCount, WonValueMinor, WinRatePct, AvgDealSizeMinor, AvgCycleDays, CompanyCount, ContactCount, ActiveDealCount, StageTotals (StageTotalItem List), RevenueTrend (TrendWeekItem List). Aggregates GetOpenByStage, GetClosed, GetCompanyCount, GetContactCount, GetActiveDeals, GetAllByStage, GetCreatedInRange; week index = Trunc(DiffDays(FirstWeek, Monday of date) / 7) with bounds check.
Read-back: full node text, every If / loop target; validation 0 errors (known warnings). Read-only action → safe to call in Vizion.
Tests: Dashboard_GetKpis() as owner → last 90 days, counts equal Companies / Contacts / Deals screens; StageTotals 7 rows; RevenueTrend ≈ 14 weeks; RangeCode "custom" FromDate > ToDate → falls back to last 90 days; as member → numbers only for own deals.

## N7 full pipeline board — PUBLISHED Sales rev 27 (env_app verified 2026-11-12 23:24 UTC)
Source: notes/overnight-N-source-specs.md §N7 (lib/deals/queries.ts 710-1054, board-view.tsx, board-column.tsx, deal-card.tsx, lib/deals/slip.ts). Deal_ListPipeline NOT changed.
### Parity checklist
| # | Item | Source | Rebuild (Deal_ListBoard) | Status |
|---|---|---|---|---|
| 1 | Columns | all 7 in order lead … lost; stage filter hides others; Hold hidden when empty, collapsed when not | 7 columns, StageCodes filter; Hold returned even when empty (UI hides / collapses) | same (hide = UI) |
| 2 | Won / Lost bound | not time-bounded | same | same |
| 3 | Cards | updated_at desc, id desc, 50 per column; more via cursor | same, 50; IsCapped flag; no per-column paging | same / paging backlog |
| 4 | Column totals | over all matching deals: count, amount, slipped (push_count ≥ 1) count + amount; weighted = round(amount × probability / 100) | DealCount, ValueMinor, WeightedMinor, SlippedCount, SlippedMinor, SlippedWeightedMinor, ProbabilityPct (Stage_DefaultProbability) | same (no workspace probability overrides) |
| 5 | Filters | list filters minus offerings; risk via score floor | Search (deal / company name), OwnerUserId, Unassigned, SlippedOnly, ScoreBand, CompanyId + persona scope | same subset (risk / amount / close filters: backlog) |
| 6 | Card flags | overdue (close < today, open), slip severity none / watch / severe (push ≥ 3 or slip ≥ 30 days), de-weighted (quota_weight < 1), rolled back (rollback_count > 0), health band | IsOverdue (workspace today), SlipSeverity, IsDeweighted, RollbackCount, HealthBand | same |
| 7 | Card contact name, drag-and-drop | yes | not built (UI / L3) | backlog |
### Elements (rev 27)
- NEW public structures BoardCardItem (DealId, Title, CompanyId, CompanyName, StageCode, ValueMinor, CurrencyCode, ExpectedCloseDate, OwnerUserId, OwnerName, ApprovalStatus, HealthBand, PushCount, TotalSlipDays, SlipSeverity, IsOverdue, IsDeweighted, RollbackCount, UpdatedAt) and BoardColumn (StageCode, StageLabel, ProbabilityPct, DealCount, ValueMinor, WeightedMinor, SlippedCount, SlippedMinor, SlippedWeightedMinor, IsCapped, Cards).
- NEW public service action Deal_ListBoard(Search, OwnerUserId, Unassigned, StageCodes, SlippedOnly, ScoreBand, CompanyId) → Columns. GetBoardTotals (group by stage, Σ amount, Σ slipped flag, Σ slipped amount) once; per stage: Col = EmptyCol, totals, ListAppend column, GetStageCards (Max 50), IsCapped, cards appended to Columns[Columns.Length − 1].Cards (fix in the same session: the first version cleared Col.Cards after appending the column — possible shared nested list; now append-first, same reset pattern as Deal_ListPipeline).
Read-back: full node text and every loop / If target; validation 0 errors. Read-only action.
Tests: Deal_ListBoard() → 7 columns, Won / Lost included; counts = Deal_List per stage; a stage with > 50 deals → IsCapped True and 50 cards; WeightedMinor of Proposal = 60 % of ValueMinor; StageCodes "won,lost" → 2 columns; every column shows only its own cards (checks the nested-list pattern).

## N8 global search (L24) — PUBLISHED Sales rev 28 (env_app verified 2026-11-12 23:35 UTC)
Source: notes/overnight-N-source-specs.md §N8 (app/api/search/route.ts, command-palette.tsx).
### Parity checklist
| # | Item | Source | Rebuild (Search_All) | Status |
|---|---|---|---|---|
| 1 | Query | trimmed, cut to 100, min 1 char, empty → no hits | same | same |
| 2 | Match | ILIKE %q%, % and _ escaped | lower-cased column like lower-cased %Q%; % and _ NOT escaped (as every existing list search) | LOW difference (a typed % matches anything) |
| 3 | Groups / limit | deals, companies, contacts, workflows (admin), tasks; 5 each | Deals, Companies, Contacts, Tasks; 5 each; no workflows (none in Salenix yet) | same / workflows not possible yet |
| 4 | Deal hit | name; not deleted; updated desc; subtitle = linked company name; stage | same | same |
| 5 | Company hit | name or domain; not deleted / merged; subtitle domain | same | same |
| 6 | Contact hit | first / last / email; title "First Last" / email / "(unnamed)"; subtitle email else company | same | same |
| 7 | Task hit | title; open first, then updated desc; subtitle "due <date>"; status | same | same |
| 8 | Scope | sales persona own + unassigned (tasks: assignee) | same | same |
| 9 | Client | 120 ms debounce, abort previous; Ctrl K / ⌘K; deal → detail, company / contact → list filtered by name | UI (MainLayout palette) | UI |
### Elements
- NEW public structure SearchHitItem (Kind, Id, Title, Subtitle, StageCode, StatusCode).
- NEW public service action Search_All(Query) → Deals, Companies, Contacts, Tasks (SearchHitItem lists). Fix in the same session: every mapping starts from an empty hit (no StageCode leak between groups).
Read-only action. Tests: Search_All("acme") → ≤ 5 per group; "" → all empty; as member → only own / unassigned records.

## N9 offerings — PUBLISHED Sales rev 28 together with N8 (env_app verified 2026-11-12 23:35 UTC)
Source: notes/overnight-N-source-specs.md §N9 (lib/offerings/queries.ts + types.ts, deals/[id]/offering-actions.ts, deals/actions.ts syncDealOfferings).
### Parity checklist
| # | Item | Source | Rebuild | Status |
|---|---|---|---|---|
| 1 | Workspace switch | offeringsEnabled (default off) | Foundation_GetFlags.OfferingsEnabled; off → offerings_disabled with the source message | same |
| 2 | Picker list | active only, category asc, sort_order asc, name asc | Offering_List(IncludeInactive) | same |
| 3 | Deal tags list | is_primary desc, then catalog order | Offering_ListForDeal(DealId) (deal visibility + scope) | same |
| 4 | Set tags (create / update sync) | none chosen → "Pick at least one offering — what is this deal selling?"; tagged deal emptied → "This deal is already tagged, so it can’t be left with no offering. …"; new ids must be active → "One of those offerings is no longer available. Reopen the deal and pick again."; existing retired tags kept; primary = requested if chosen, else the only one, else "Mark one offering as primary."; deselected removed | Deal_SetOfferings(DealId, OfferingIds comma text, PrimaryOfferingId): same rules and texts, max 100 ids, nothing written before every check passed | same |
| 5 | Permission | manageDeals (tagging); manageOfferings (catalog) | manageDeals + persona scope | same |
| 6 | Activity | detail add: updated {name, field offering, op added}; remove: none | one activity updated {name, field "offerings"} per save | different on purpose (one save action instead of add / remove / set-primary) |
| 7 | Catalog admin (create / rename / reorder / retire, 14 default offerings seed) | settings/offerings | NOT built (Salenix_Admin + seed; Vizion must not get seed writes tonight) | backlog |
| 8 | Deal_Create / Deal_Update required offerings when the switch is on | yes | NOT wired into Deal_Create / Deal_Update (signatures unchanged); UI calls Deal_SetOfferings after save | different on purpose (no change to tested create / update) — OPEN QUESTION |
| 9 | Deal list offering / uncategorized filters | yes | not built | backlog |
### Elements (rev 28)
- NEW public structure OfferingItem (Id, Name, Category, SortOrder, Active, IsPrimary).
- NEW public service actions Offering_List(IncludeInactive) → Items; Offering_ListForDeal(DealId) → Items; Deal_SetOfferings(DealId, OfferingIds, PrimaryOfferingId) → Success, OutcomeCode (ok / offerings_disabled / not_found / required / cannot_empty / unavailable / primary_required), Message.
Read-back: full node text, every If / loop target (parse loop capped at 100, snapshot of existing tags read once); validation 0 errors.
Tests (CGB India; needs Offering rows + OfferingsEnabled on — no offering data exists yet, the catalog seed is backlog): Deal_SetOfferings(id, "") on an untagged deal → required; (id, "5,7", 0) → primary_required; (id, "5,7", 7) → ok, Offering_ListForDeal shows 7 first; (id, "") → cannot_empty; flag off → offerings_disabled.

## Overnight run N — close-out
- All 9 batches done, none skipped, no batch failed. Sales rev 21 → 28 (22 N1+N2, 23 N3, 24 N4, 25 N5, 26 N6, 27 N7, 28 N8+N9). Mentor session fefd9199 closed after the rev 28 publish.
- Fresh read-only check (context index, rev 28): 22 new public service actions present with the expected inputs / outputs; Deal_List / Company_List / Contact_List keep every existing input with its old Is Mandatory flag; the new inputs are all optional.
- No data written by me in any workspace (no DevOnly_*, seed, import or test call). Nothing deleted or renamed. Only Salenix_Sales changed. No Studio, no Production.
- MCP note: one long Mentor prompt (N3a, ~5.5 KB with many quoted expressions) was refused by the gateway with a CloudFront 403 "Request blocked"; splitting it into smaller prompts written partly in words worked.
- git push was blocked by the local auto-mode permission check; commits are local — push them by hand.

# N10 task comments — Salenix_Sales rev 29 (2026-11-13, user decision 25(2))
Decisions on the run N open questions recorded in CLAUDE.md decision 25. N10 = item 2: Comment_* accept EntityType "task". Mentor session c3d7ed48; read-only read of Sales_EntityVisible / Comment_Create / Comment_List / Comment_Delete / Task_List first.
### Parity checklist (source app/dashboard/tasks/comments-actions.ts, lib/task-comments/queries.ts)
| # | Item | Source | Rebuild | Status |
|---|---|---|---|---|
| 1 | Storage | separate task_comments table | Comment rows with EntityType "task" (no TaskComment entity) | different on purpose (user decision) |
| 2 | Permission | manageDeals for load / create / edit / delete | create: manageDeals (new Switch branch); delete: manageDeals (unchanged, all types); list: no permission, visibility only (as deal / company / contact comments) | same for writes |
| 3 | Parent task | must exist in tenant (deleted tasks not excluded), else "Task not found" | Sales_EntityVisible task branch: exists in tenant, not deleted, persona scope (sales: assigned to me or unassigned) → else "Record not found in this tenant." | different on purpose (task scope, soft delete) |
| 4 | Body | trimmed, 1–4000 | 1–8000 (same rule as other comments, Comment.Body 8000) | different on purpose (one comment model) |
| 5 | Create activity | on the task: updated {kind task_comment, commentId, commentPreview 80, taskTitle} | same (Metadata If(Type = "task", …)) | same |
| 6 | Mentions / notifications | none | @[Name](id) mentions notify as for other comments | different on purpose (one comment model; UI may hide mentions on tasks) |
| 7 | Edit | author only, edited_at | not built for any comment type (backlog N5 #8) | backlog |
| 8 | Delete | author only, hard delete, no activity | author only, soft delete, idempotent, no activity | different on purpose (soft delete rule) |
| 9 | List | created asc, 200 | same (Comment_List) | same |
| 10 | Replies | flat | one reply level (UI may send ParentCommentId 0 only) | UI |
### Elements (rev 29)
| Element | Old → New |
|---|---|
| Sales_EntityVisible Switch | deal / company / contact / Otherwise → + T = "task" → GetTask (CrmTask: Id, TenantId, DeletedAt = NullDate(), persona scope on AssignedUserId; max 1) → Assign IsVisible, EntityName = Title, CompanyId = 0 → End |
| Comment_Create permission Switch | + Type = "task" → Sales_RequirePermission_task (manageDeals) → CleanBody check (same as deal) |
| Comment_Create local EscTitle | NEW (escaped Sales_EntityVisible.EntityName) |
| Comment_Create Sales_WriteActivity Metadata | If(Type = "task", {"commentId":N,"commentPreview":"…","kind":"task_comment","taskTitle":"…"}, old expression unchanged) |
Unchanged: Comment_List, Comment_Delete (they work for tasks through Sales_EntityVisible / author check), all signatures. Side effect (read-only, accepted): Activity_ListFor("task", id) now returns the task's activity + task comments within task scope; Note_List("task", id) returns an empty list (Note_Create still rejects "task").
Read-back: both Switches with every condition and first target, Metadata JSON checked character by character for both branches; validation 0 errors, 20 known warnings. No data written.
Tests (CGB India only): Comment_Create("task", <task id>, "Called back") → ok; Comment_List("task", id) shows it; Activity_ListFor("task", id) shows the comment; as a sales member on a task assigned to someone else → not_found; Comment_Delete by another member → forbidden.

# UI batch U (Salenix, uses Sales rev 29) — started 2026-11-13
Instruction (user, 2026-11-13): U1–U6 in Salenix only; CSS only in Salenix_Theme, then STOP for the user's release / reference refresh; publish after each clean batch; tests only in CGB India; commit + push per batch.
Source specs (file:line, exact strings): notes/u2-deal-detail-ui-spec.md, notes/u3-u4-board-home-ui-spec.md, notes/u5-u6-lists-tasks-palette-ui-spec.md; U1 = source deal-filter-bar.tsx read directly.
Read-only check first (Mentor session 45ea6603): Salenix rev 38 references Sales rev 29 (all 22 new actions + 9 structures visible), Foundation 29 actions, OutSystemsCharts (already referenced → usable for U4 charts); Deals screen calls Salenix_Sales.Deal_List directly from data action GetDeals (no UI_ wrapper); validation 0 errors / 30 warnings (27 Phosphor icon warnings = known open item, 2 unused UI_ApprovalOptions / UI_DealSortOptions, 1 Login UI_StartSession).

## T3 Theme v2.3 — Salenix_Theme rev 8 (published 2026-11-13 04:37 UTC, Mentor session 62ca59eb)
All CSS for U1–U6 in one theme step so there is only one release / refresh stop. 6 append-only parts (big prompts get a CloudFront 403), each verbatim; style sheet 1,551 → 1,908 lines (51,492 → 93,303 chars); validation 0 errors, 1 known warning (Google Fonts URL). Classes: theme/THEME-GUIDE.md "v2.3 classes"; source copy theme/v23-additions.css (appended to theme/salenix-theme.css). No existing rule changed.
Read-back (REPORT ONLY after publish): v2.3 section = lines 1553–1908, 356 lines; rule and comment text match what was sent, braces balance (355 / 355), content "\2713" and "" intact, LF endings, no trailing spaces. Mentor stored 4 extra leading spaces on 339 of the appended lines (cosmetic, no render effect) → not re-published; theme/salenix-theme.css keeps the unindented text.
Deliberate choices: popovers = If + absolutely placed panel + transparent full-screen backdrop Link (no OutSystems UI popover pattern, so the sx-fchip look stays); stage bar widths via classes sx-w-0/2/5/…/100 (no inline styles); KPI tint via a CSS variable per tint class.
**User step (Studio):** release Salenix_Theme rev 8, then refresh the Salenix reference to it (references only) and publish from Studio. After that the U1–U6 UI batches run.

## Decisions taken for U (recommended options from the specs; say if you want another)
| # | Question | Decision | Why |
|---|---|---|---|
| D1 | Deals chip "Score" vs source "Health" | label "Health" with options "Strong (≥70)", "Moderate (40–69)", "Weak (<40)", active "Health: Strong" | source label (deal-filter-bar.tsx:453-474) |
| D2 | Company chip (source multi-select company names) | single-select popover with search ("Search companies…"), active "Company: <name>" → CompanyId | Deal_List has one CompanyId (decision 25(6)) |
| D3 | Stage / Owner chips | multi-select popovers as source → StageCodes / OwnerUserIds comma lists; "Include won/lost" chip (L3) kept | source + existing L3 chip |
| D4 | Sort direction | header click: asc → desc → back to "updated" (arrow ↑/↓ shows the direction), SortDir sent | user asked (L14); source has no direction |
| D5 | Recently deleted (Deals) | in-place deleted view like Companies / Contacts (L3b): link "Recently deleted" (owner / admin only = deleteDeals) + banner "Showing deleted deals · Back", columns Name / Amount / Deleted on / Restore | same pattern as L3b; source has a separate page (open item) |
| D6 | Delete deal on DealDetail edit drawer | Delete left in the footer for deleteDeals (owner / admin) with the inline confirm used on company / contact (L5b); after delete → message "Deal deleted." + navigate to Deals | source deletes without confirm and only from the list drawer; consistency with L5b |
| D7 | Board empty column text | "No deals" (source "Drop here" promises drag-and-drop) | no DnD |
| D8 | Board capped column | "Showing 50 of <n> — open the List view for the rest" | no per-column paging |
| D9 | Board card health chip | band word chip ("Strong" / "Moderate" / "At risk") until BoardCardItem has HealthScore | Sales unchanged in this run |
| D10 | Home for manager | admin layout, titles "Team …", range fixed last 90 days (no picker) | one block; manager-only widgets backlog |
| D11 | KPI delta chips, quota / coverage tiles | omitted | no prior-period / quota data |
| D12 | Timeline "Emails" chip | dropped (deal: All / Calls / Meetings / Stage changes / Notes; company / contact: All / Mine / Teammates / Comments / Notes / Activity / Automation) | Comms not built |
| D13 | "Log activity" button, comment Edit | not built | no server action (call / meeting log, Comment_Update) |
| D14 | Tasks search | keeps the active chips | source quirk drops them |
| D15 | Task row company / contact link | detail screen | source links the unfiltered list (quirk) |
| D16 | Palette company / contact hit | detail screen | no list Search input needed |
| D17 | Palette task badge | status label (SearchHitItem has no priority) | Sales unchanged |
| D18 | Company timeline source pill without SourceName | "Deal" / "Contact" without name | activity rows have no SourceName |
| D19 | Tasks rail item | all personas (source: sales only) | user asked for the rail item |
| D20 | Viewer on task drawer | edit controls hidden | source quirk shows them |

## U1 Deals list chips, sort direction, Recently deleted — parity checklist (source deal-filter-bar.tsx, deals-view.tsx, page.tsx)
Studio step done by the user: Theme rev 8 released, Salenix rev 39 = reference refresh (env_app verified).
| # | Item | Source | Rebuild (Deals screen) | Status |
|---|---|---|---|---|
| 1 | Row start | funnel icon + "Filter" | existing FilterLabel | same |
| 2 | Chip order | Stage▾ · Amount▾ · Close date▾ · My deals · Unassigned · Stale only · Awaiting approval · Approved · Won · PO missing · Health▾ · Revisit due · Slipped only · Company▾ · Owner▾ · Clear all (right) | same order; "Include won/lost" (L3) after Owner▾ | same + L3 chip kept |
| 3 | Chip look | inactive slate border, active indigo tint; popover chips with ▾ and × when active | sx-fchip / sx-fchip-active, caret sx-fchip-caret; × = Link sx-fchip-x right after the chip (not inside its border: a Link inside the chip would take the link colour) | different on purpose (tiny visual) |
| 4 | Stage▾ | multi-select list of 7 stages with check squares; label "Stage" / "Stage: Lead, Qualified"; × clears | popover sx-fpop + sx-fpop-item / sx-fpop-check; StageCodes comma text → Deal_List StageCodes; label from StageOptions labels | same |
| 5 | Amount▾ | Min / Max (major units, placeholders "0" / "∞"), hint "Values are in major currency units (e.g. 5000 = ₹5,000 or $5,000 depending on the deal's own currency).", Clear / Apply; label "Amount: ≥5,000 · ≤20,000" | same texts; ×100 → AmountMinorMin / Max; ≤ 0 = no filter (decision 25(5)) | same |
| 6 | Close date▾ | From / To date, Clear / Apply; label "Close: 2026-11-01 → 2026-11-30" (… for an open side) | same → CloseFrom / CloseTo | same |
| 7 | My deals / Unassigned | toggles, mutually exclusive | MineChip (OwnerFilter = me) / UnassignedChip → Unassigned; each clears the other | same |
| 8 | Stale only | stale=true | StaleOnly (server default 14 days) | same |
| 9 | Awaiting approval / Approved | mutually exclusive | existing chips | same |
| 10 | Won · PO missing | tooltip "Won deals whose client PO is missing or doesn't match the deal value" | PoMissing, Title = tooltip | same |
| 11 | Health▾ | single select "Strong (≥70)" / "Moderate (40–69)" / "Weak (<40)" with dots; label "Health" / "Health: Strong"; picking the active one clears; closes on pick | ScoreBand strong / moderate / at_risk | same (D1) |
| 12 | Revisit due / Slipped only | toggles; Slipped tooltip "Deals whose expected close date has been pushed out at least once" | RevisitDue / SlippedOnly | same |
| 13 | Company▾ | search "Search companies…", multi-select names, "No matches", Clear; label "Company: <name>" / "<n> selected" | single select (D2): Company_ListOptions(Search) (max 20), selected row active; label "Company: <name>" | different on purpose |
| 14 | Owner▾ | search "Search teammates…", multi-select, Clear; label "Owner: <name>" / "Owner: <n> selected"; shown only when teammates exist | GetOwners (Member_ListOptions) filtered on the client by Label / Email (max 50); OwnerIds → OwnerUserIds | same |
| 15 | Clear all | only when any filter active, clears all filters (keeps search) | ClearFilters resets every chip variable + popover; search kept | same |
| 16 | Popover close | click outside (transparent layer) | sx-fpop-backdrop Link → ClosePopover; one popover open at a time (OpenPopover text) | same |
| 17 | Sort | source: recent / risk only | header links Name / Amount / Close date: 1st click asc (Amount: desc as today), 2nd click flips, 3rd back to "updated"; arrow ↑ / ↓; SortDir asc / desc | our addition (D4, L14) |
| 18 | Recently deleted | separate page /dashboard/recently-deleted | link "Recently deleted" in sx-fbar-end for deleteDeals (owner / admin) → ShowDeleted → Deal_List IncludeDeleted; banner "Showing deleted deals · Back"; columns Name, Amount, Company, Deleted on, Restore (UI_Deal_Restore, message "Deal restored."); chips stay visible and still filter the deleted list | different on purpose (D5, as L3b) |
| 19 | Empty states | "No matches" / filters / "No deals yet" | + deleted view "No deleted deals" / "Deleted deals show up here for 30 days." → kept short: "No deleted deals" + "Deals you delete show up here." | rebuild text (source page differs) |
| 20 | Server inputs | — | Deal_List: StageCode "" (StageCodes instead), OwnerUserIds, Unassigned, StaleOnly, PoMissing, RevisitDue, SlippedOnly, ScoreBand, AmountMinorMin / Max, CloseFrom / To, CompanyId, SortDir, IncludeDeleted | same |
| 21 | Not built | saved views, bulk select / delete, column chooser, risk sort / filter, offering filters, custom fields | — | backlog |

### U1 result — Salenix rev 40 (published 2026-11-13 05:13 UTC, env_app verified; Mentor session 1bdd272c, steps U1a logic / U1b chips / U1c popovers + deleted view)
| Element | Change |
|---|---|
| Deals variables | NEW StageCodes, OwnerIds, CompanyFilterId, CompanyFilterName, ScoreBand, AmountMinMinor, AmountMaxMinor, AmountMinText, AmountMaxText, CloseFrom, CloseTo, CloseFromDraft, CloseToDraft, Unassigned, StaleOnly, PoMissing, RevisitDue, SlippedOnly, ShowDeleted, SortDir, OpenPopover, CompanySearch, OwnerSearch (StageFilter kept, unused) |
| GetDeals | Deal_List StageCode "" + StageCodes, OwnerUserIds, Unassigned, StaleOnly, PoMissing, RevisitDue, SlippedOnly, ScoreBand, AmountMinorMin / Max, CloseFrom / To, CompanyId, SortDir, IncludeDeleted = ShowDeleted (read back) |
| GetCanDelete (NEW data action) | Foundation_GetMyContext → Foundation_HasPermission(RoleCode, "deleteDeals") → CanDelete; AllExceptions → False |
| GetCompanyOptions (NEW, on demand) | Company_ListOptions(CompanySearch) |
| Client actions NEW | TogglePopover, ClosePopover, ToggleStage, ToggleOwnerId (comma add / remove, read back), SetScoreBand, SelectCompany, ApplyAmount (×100, DecimalToLongInteger), ApplyClose, ClearStages / Amount / Close / Score / Company / Owners, ToggleUnassigned, ToggleStale, TogglePoMissing, ToggleRevisitDue, ToggleSlipped, ToggleDeleted, CompanySearchChanged, RestoreDeal, functions StageChipLabel(Codes, StageOptions), OwnerChipLabel(Ids, Owners) |
| Client actions CHANGED | ToggleMine (+ Unassigned = False), ClearFilters (+ all new filters, SortDir, OpenPopover; Search kept), SortByColumn (new column → asc / value desc; same column first direction → flip; else back to "updated") |
| UI_Deal_Restore (NEW server) | as UI_Company_Restore, calls Deal_Restore |
| FilterBar | order: Filter · Stage▾ · Amount▾ · Close date▾ · My deals · Unassigned · Stale only · Awaiting approval · Approved · Won · PO missing · Health▾ · Revisit due · Slipped only · Company▾ · Owner▾ (only with members) · Include won/lost · FilterEnd (Clear all + Recently deleted for deleteDeals). StageDropdown and OwnerDropdown deleted |
| Deleted view | If ShowDeleted: DeletedBanner "Showing deleted deals · Back"; ResultsTables If → DeletedTable (Name, Amount, Company, Deleted on, Restore) else DealsTable (recreated by Mentor inside the If; cells read back = unchanged); EmptyState "No deleted deals" / "Deals you delete show up here." / trash |
| Sort headers | Name ↑/↓, Amount ↓/↑, Close date ↑/↓ by SortDir |
Validation 0 errors; warnings = known icon warnings + unused ApplyFilters, UI_ApprovalOptions, UI_DealSortOptions, StageFilter + 1 "If directly under a List" suggestion (Owner list). Cleanup later.
Browser tests U1 (CGB India; filters are read-only, safe anywhere):
1. Stage▾ → tick Lead + Qualified → chip "Stage: Lead, Qualified", list shows only those; × clears.
2. Amount▾ Min 100000 → Apply → chip "Amount: ≥100,000", only deals ≥ ₹1,00,000; Clear.
3. Close date▾ From/To this month → chip "Close: 2026-11-01 → 2026-11-30".
4. My deals then Unassigned → only one active at a time.
5. Health▾ Strong → chip "Health: Strong"; pick Strong again → cleared.
6. Company▾ type part of a name → list narrows → pick → chip "Company: <name>".
7. Owner▾ tick two owners → "Owner: 2 selected".
8. Won · PO missing → only Won deals without matching PO (tooltip on hover).
9. Clear all resets every chip, search text stays.
10. Header Amount: ↓ (desc) → click again ↑ → third click back to default order.
11. As owner/admin: "Recently deleted" visible; as member/manager: hidden. Delete a demo deal (U2 adds the button; until then via DevTools) → appears here with "Deleted on"; Restore → "Deal restored." and it leaves the list.
12. Click outside an open popover closes it; only one popover open at a time.

## U2 Deal detail — parity checklist (notes/u2-deal-detail-ui-spec.md; source deals/[id]/page.tsx + _components, dashboard/_components entity-tasks-panel, notes-timeline, comments-thread, entity-timeline)
| # | Item | Source | Rebuild | Status |
|---|---|---|---|---|
| 1 | Delete deal | only in the list's edit drawer, no confirm, owner / admin | DealDetail edit drawer footer: "Delete" left (sx-btn-delete) for deleteDeals, inline confirm as company / contact (L5b), UI_Deal_SoftDelete → message "Deal deleted." → Deals screen | different on purpose (D6) |
| 2 | Edit drawer fields Next step / Source / Competitor | not in the drawer (inline cards) | removed from the edit drawer (widgets hidden); the save still sends the loaded values (Deal_Update is a full write) | same (L23) |
| 3 | Context section | "Context" section title; cards Competitor (input, placeholder "Who else is in this deal?"), Source (select "—", "Outbound", "Referral", "Inbound-Website", "Partner", "Other"; saves on pick), Next step (textarea 2 rows, "What has to happen to close?", ✕ / ✓); pencil "Edit <label>" only for manageDeals; empty: placeholder / "Add…" (Source) / "—" read-only | block DealContextCard: same order, labels, placeholders, editors; Enter saves / Esc cancels (Competitor), blur saves (Competitor); Deal_UpdateContextField via UI_Deal_UpdateContextField; unchanged value → no call; error → value reverts + error message; Max Length 250 / 100 / 500 | same (lengths = columns) |
| 4 | Tasks panel | collapsed "Tasks — none yet" when empty; header "<n> overdue" badge + "<open> open · <done> done"; quick add "Add a task — Enter to create, Tab for details"; details Due date, Assignee (Unassigned + members, default deal owner else me), Type ("—", Call, Email, Meeting, Internal, Follow-up), Priority (Low, Medium default, High, Urgent), Repeat (No repeat, Daily, Weekly, Custom (monthly)), Blocks stage (None + 7 stages); "Cancel" / "Create task"; groups Overdue / Today / Upcoming / No due date (+ counts), sort due asc then priority; "▸ Done (n)" collapsed, max 30; row: check, priority dot, title, recurring mark, due chip ("<n>d overdue", "Due today", "Due tomorrow", "Due in <n>d", "Nov 20"), "Blocks <stage>"; toast "Recurring task rolled forward to next due date"; no edit / delete in the panel | block EntityTasksPanel(EntityType, EntityId, DefaultAssigneeId, CanEdit, ShowStageBlocking): same texts / rules; buckets by workspace today (server IsOverdue + DueDate vs today from Foundation_TodayFor); Tab-to-expand via JS keydown; type icon not shown (text only) | same except: persona scope on Task_List (decision 25(2)); type icon omitted |
| 5 | Notes card | "Notes" + count; composer "Add a note — context, next steps, intel from a call…" + "Add note"; empty "No notes yet. Use this space to capture call notes, intel from meetings, or the next-step plan."; newest first; author + relative time; own notes: Edit / Delete (no confirm); edit "Cancel (esc)" / "Save (⌘⏎)", Ctrl/⌘+Enter saves, Esc cancels | block NotesPanel(EntityType, EntityId, CanEdit): same | same |
| 6 | Comments | "Comments" + count; empty "No comments yet. Mention a teammate with @ to start a discussion."; oldest first; avatar initials, author, relative time + " · edited"; Reply (top level only) "Reply to <author>…", "Reply" / "Cancel"; own: Edit / Delete; composer at the bottom "Add a comment — type @ to mention a teammate", "Comment" + "⌘⏎"; mention picker above the textarea "Mention teammate", max 8, ↑ ↓ Enter Tab Esc, inserts @[Name](id); chips amber for me, indigo others | block CommentsPanel(EntityType, EntityId, CanEdit, AllowMentions): same; picker via JS (caret query) + Member_ListOptions; body rendered as encoded HTML with sx-mention spans | same except: Edit not built (no Comment_Update); no typing indicator / live merge (no realtime) |
| 7 | Timeline | title "Activity"; deal chips All / Emails / Calls / Meetings / Stage changes / Notes with counts; empty "No activity logged yet. Log a call or meeting, or take an action on this deal." / "No <chip> yet."; rows icon + sentence (+ detail) + relative time; sentences per action (spec §6 table) | block TimelinePanel(EntityType, EntityId, Title, FilterSet, GroupByMonth, ShowSource, EmptyText): server action UI_Timeline_ListFor builds rows (sentence, detail, icon, filter keys) from Activity_ListFor + Metadata JSON; chips client-side | same except: Emails chip dropped (D12), "Log activity" not built (D13) |
| 8 | Layout | left aside: Deal Info, Relationship, Context, Record; right: Description, Activity, risk / score, Tasks, AI, order proof, value split, documents, Comments, Notes | existing DealDetail 2-column kept; Context section added to the left column; right column order: Description (existing), Activity, Tasks, existing order-proof / history cards, Comments, Notes | same order for the new parts |
| 9 | Relative time | "just now", "<n>m ago", "<n>h ago", "<n>d ago", else "Nov 13"; tooltip absolute | client function UI_RelativeTime | same (no 60 s auto refresh) |

### U2 result — Salenix rev 41–44 (2026-11-13; interim publishes per finished block; final rev 44 06:43 UTC, env_app verified; Mentor session 3182210e — survived one MCP login expiry)
| Rev | Elements |
|---|---|
| 41 | NEW server wrappers UI_Deal_SoftDelete, UI_Deal_UpdateContextField, UI_Task_Create / Update / SetCompleted / Delete, UI_Note_Create / Update / Delete, UI_Comment_Create / Delete (pattern of UI_Company_SoftDelete: AllExceptions → ErrorMessage; the app has no ValidationError exception); client functions UI_RelativeTime, UI_Initials, UI_CommentHtml (EncodeHtml + sx-mention spans) in folder UI; structures TimelineMeta, TimelineRow; server actions UI_Timeline_ListFor (Activity_ListFor 150 → sentences per action, icons, filter keys; connectors read back) + UI_Timeline_ParseMeta (JSON parse isolated so one bad row does not stop the loop); DealFormBlock: CanDelete input, Deleted event, inline Delete confirm in FooterLeft ("Delete this deal?" Delete / Keep), IfEditContext (Next step / Source / Competitor inputs) deleted — values still loaded and sent back; DealDetail: GetDeal.CanDelete (deleteDeals), DealDeleted → "Deal deleted." + Deals; NEW block DealContextCard (Competitor input Enter / Esc / blur, Source dropdown save-on-pick, Next step text area ✕ / ✓; optimistic with revert; listeners guarded on the element); Deal Info card gained Forecast + Lost reason rows; old Context card deleted; Description card at the top of the right column |
| 42 | NEW blocks EntityTasksPanel + TaskPanelRow (GetTasks: Task_List 100 + Foundation_TodayFor → Overdue / Today / Upcoming / No due date sorted due + priority, Done by CompletedAt max 30; quick add with Enter / Tab, details grid, Create task; toggle with recurring message), NotesPanel (add / edit own / delete own, Ctrl+Enter / Esc), CommentsPanel (top level + one reply level, Reply / Delete own, @mention picker max 8 with ↑ ↓ Enter Tab Esc, token @[Name](id), body HTML via data-sx-html + OnRender innerHTML because Mentor cannot set Escape Content = No) |
| 43 | NEW block TimelinePanel (chips with counts, deal set All / Calls / Meetings / Stage changes / Notes, default set All / Mine / Teammates / Comments / Notes / Activity / Automation, month groups with counts, source pills "Deal · name" / "Contact · name"), TimelineRow + ShowMonth / MonthCount, functions RowMatches / ChipLabel / FilterCount |
| 44 | DealDetail right column: Description → Activity (TimelinePanel) → Tasks → Order proofs → stage / date / audit cards → Comments → Notes |
Validation 0 errors; warnings = known Phosphor icon warnings, unused actions, 1 "List should have one child" suggestion (replies list).
Known limits (U2): panels do not refresh after the Edit drawer / stage drawer saves (they reload on page open); comment Edit and "Log activity" not built (no server action); Emails chip dropped; icon names pencil-simple, check, x, check-square, note, chat-circle, arrows-clockwise, arrows-left-right, lightning, pulse to be checked in the browser.
Browser tests U2 (CGB India only — writes):
1. Owner/admin: open a demo deal → Edit → footer shows Delete left; Delete → "Delete this deal?" → Keep (nothing) → Delete → "Deal deleted." → Deals; Recently deleted shows it → Restore.
2. Member: Edit drawer has no Delete.
3. Context: pencil on Competitor → type → Enter → value stays; Esc cancels; Source pick "Referral" saves at once; Next step ✓ saves, ✕ cancels; reload → values kept; viewer sees no pencils and "—" for empty.
4. Edit drawer no longer shows Next step / Source / Competitor; saving the drawer keeps the context values.
5. Tasks: empty deal shows "Tasks — none yet" → click → quick add; type "Call CFO" Enter → task under "No due date"; Tab with text opens details; create with due yesterday → "Overdue (1)" red + "1d overdue"; tick → moves to Done; weekly task tick → "Recurring task rolled forward…" and a new open task.
6. Notes: add, edit own (Ctrl+Enter saves, Esc cancels), delete own; another member's note has no Edit / Delete.
7. Comments: type "@" → picker "Mention teammate" → ↓ Enter inserts @[Name](id) → Comment → chip "@Name" (amber when it is you); Reply → "Reply to <name>…" → Reply; Delete own.
8. Activity: rows for created / stage change ("… moved deal X from lead to qualified") / notes / comments; chip counts; "Stage changes" chip filters.

## U3 Pipeline board — parity checklist (notes/u3-u4-board-home-ui-spec.md part U3; source deals/board/*)
| # | Item | Source | Rebuild | Status |
|---|---|---|---|---|
| 1 | Header | "Pipeline", subtitle "<workspace> — drag deals across stages.", List / Board tabs, no New deal | "Pipeline", subtitle workspace name only, tabs kept, New deal link kept (L3) | different on purpose (no drag-and-drop; L3 link) |
| 2 | Data | 7 stages, Won / Lost not time-bounded, persona scope | Deal_ListBoard (replaces Deal_ListPipeline) | same |
| 3 | Filters | full deal filter bar + "Show slipped deals only (n)" + active-filter pills | chips the board action supports: Stage▾ (multi → StageCodes), My deals, Unassigned, Health▾, Slipped only, Company▾ (single), Owner▾ (single) + Clear all; SlippedOnly toggle "Show slipped deals only (n)" (hidden when n = 0 and off); Amount / Close / Stale / approval / PO / revisit chips not on the board; no pills row | subset (backlog: Deal_ListBoard inputs) |
| 4 | Read-only banner | "Read-only — your role can't move deals between stages." when no manageDeals | same (sx-board-banner) | same |
| 5 | Column order / visibility | lead … lost; stage filter shows selected; Hold hidden when 0 unless filtered; Hold collapsed until clicked | same (HoldExpanded variable) | same |
| 6 | Column header | count (tooltip "<n> in stage") + stage pill; right (only count > 0): value, "<x> weighted" (tooltip "Weighted at <p>% stage probability"), "<x> at risk (<n> pushed)" in rose when slipped > 0 (tooltip with raw value) | same; money via Money_FormatText | same |
| 7 | Collapsed Hold | "Hold" + count, amount, "parked · click to expand", whole lane clickable | same (sx-board-hold) | same |
| 8 | Empty column | "Drop here" | "No deals" | different on purpose (D7) |
| 9 | Capped column | infinite scroll | "Showing 50 of <n> — open the List view for the rest" (List link → Deals) | different on purpose (D8) |
| 10 | Card | left spine by health / won / lost, extra red spine when severe; name link → detail; chip "On hold" (hold) or health score; row 2 company · contact; "Pending approval" (won + pending); footer amount (mono) + close date (red when overdue), "Overdue", "Pushed ×n" (watch amber / severe rose + flag / de-weighted "· 50%"), risk chip, "Rolled back ×n" | same except health chip shows the band word ("Strong" / "Moderate" / "At risk") — no HealthScore in BoardCardItem (D9); row 2 company only (no contact); no risk chip (no risk data); slip tooltip "Pushed <n>× (<d> days slipped)" (no original date) | partly (data gaps) |
| 11 | Move stage | drag-and-drop with reason modals | keep the existing "Move stage…" control per card → StageChangeDrawer (if present today) | different on purpose (no DnD) |

### U3 result — Salenix rev 45–46 (2026-11-13; rev 46 07:06 UTC env_app verified; Mentor session 42411b22)
| Rev | Elements |
|---|---|
| 45 | NEW block BoardDealCard (spine class by stage / health band + sx-dcard-severe; name link → DealDetail; "On hold" or health band pill; company line; "Pending approval"; amount Money_FormatText 2 decimals; close date (red when overdue), "Overdue", "Pushed ×n" watch / severe + flag / de-weighted "· 50%", "Rolled back ×n", tooltips; per-card "Move stage…" dropdown with its own local MoveTarget → event MoveRequested) and BoardColumnBlock (count + stage pill, value / "<x> weighted" / "<x> at risk (<n> pushed)" with tooltips, "No deals", capped line "Showing 50 of n — open the List view for the rest") |
| 46 | Pipeline screen: GetBoard (Deal_ListBoard + SlippedTotal), GetMyContext (MyUserIdText, CanEdit = manageDeals), GetCompanyOptions; filter bar Filter · Stage▾ · My deals · Unassigned · Health▾ · Slipped only · Company▾ · Owner▾ (single) · Clear all; "Show slipped deals only (n)" toggle; read-only banner; List BoardList (class sx-board on the list) with column visibility (Hold hidden when 0 unless filtered) and collapsed Hold lane → ExpandHold (no collapse again, as source); subtitle = workspace name. Deleted (references reported first): Form1 card, Gallery + old lists / cards, GetPipeline, GetPipelineAfterFetch, RefreshPipeline, OpenMove, CollapseHold, MoveTarget, Search |
Validation 0 errors (known warnings + 2 single-child suggestions). Deal_ListPipeline is no longer used by Salenix (Sales action kept).
Browser tests U3 (read-only except Move):
1. Pipeline shows Lead, Qualified, Proposal, Negotiation, (Hold collapsed lane when it has deals), Won, Lost; column counts = Deals list per stage.
2. Header lines: value, "… weighted" (hover "Weighted at 60% stage probability" on Proposal), rose "… at risk (n pushed)" only where pushed deals exist.
3. Cards: health spine colours, "Overdue" on past-close open deals, "Pushed ×2" amber / "Pushed ×3" rose + flag, "Rolled back ×1" violet, "Pending approval" on pending Won deals.
4. Click the Hold lane → expands to a normal column.
5. Stage▾ Won + Lost → only those 2 columns; My deals / Unassigned / Health / Company / Owner filter the cards; "Show slipped deals only (n)" toggles.
6. Viewer: amber read-only banner, no Move dropdown.
7. Member (CGB India): Move stage… → Qualified on a Proposal card → StageChangeDrawer asks for the rollback reason → card moves.

## U4 Home (admin / manager) — parity checklist (notes/u3-u4-board-home-ui-spec.md part U4; source dashboard/page.tsx, admin-home.tsx, kpi-strip.tsx, home/greeting.tsx, rep-filter.tsx, dashboard-range-filter.tsx, revenue-trend-chart.tsx, stage-conversion-bars.tsx, stage-donut.tsx)
| # | Item | Source | Rebuild | Status |
|---|---|---|---|---|
| 1 | Which home | owner / admin → AdminHome; manager → ManagerHome; member / viewer → SalesHome | admin + manager → new layout (manager: titles "Team …", range fixed last 90 days, no picker); sales persona keeps today's home | different on purpose (D10); sales home backlog |
| 2 | Greeting | "<Good morning / afternoon / evening / Hello>, <first name>." + workspace · "Administrator" / "Manager" pill | existing greeting kept (server time of the workspace) + persona pill | same |
| 3 | Toolbar | RepFilter "Viewing" + "Whole team" / members; range "7d / 30d / 90d / QTD / YTD / Custom" (default 90d) with "Apply"; range label; record counts "<n> companies · <n> contacts · <n> active deals" right (hidden on phone) | same; RangeLabel from Dashboard_GetKpis | same |
| 4 | KPI strip | 5 tiles: Total pipeline (sub "vs … at period start", scope "Open deals, as of today"), Weighted pipeline ("Σ amount × stage win probability"), Win rate ("{won} won · {lost} lost" / "no deals closed in period"; "—"), Avg deal size ("won deals in period" / "no wins in period"), Avg sales cycle ("{n}d", "created → won"); scope lines; tooltips; delta chips; quota / coverage tiles | 5 tiles with the same labels, sub and scope texts; Total pipeline sub "open deals" (no prior value); tooltips without the delta sentence; no delta chips, no quota / coverage | partly (D11) |
| 5 | Revenue trend | card "<n>-day trend" + "REVENUE" badges; title "Organization revenue trend" / "<rep> · revenue trend" / manager "Team revenue trend"; subtitle "<range label> · weekly buckets · <workspace>"; weekly bars New pipeline (maroon, left axis) + lines Won revenue (emerald) / Lost revenue (rose dashed) right axis; 95th-percentile cap; legend; empty "No revenue activity in the last <n> days. Once deals start moving, the trend will fill in here automatically." | OutSystemsCharts column + line chart from RevenueTrend if the chart blocks allow 2 axes, else one axis; cap skipped if not supported (logged); legend + empty text same | same / cap = backlog if not feasible |
| 6 | Stage bars | "Organization pipeline by stage — live snapshot" (rep / Team variants), subtitle "Every deal open right now, all time · does not follow the date filter above", badge "SNAPSHOT · REVENUE"; rows Lead … Won with bar, value + "<n> deals", "<p>%" close prob, ratio chip vs stage above ("—" first) | CSS bars (sx-sbar-*, width class sx-w-N) from StageTotals + fixed probabilities (10 / 30 / 60 / 80 / 100) | same |
| 7 | Stage donut | "Organization-wide — deal counts, all time", "Every deal on record by stage, all owners · does not follow the date filter", badge "COUNTS", "TOTAL DEALS" + total, donut 7 stages, legend with counts and % | OutSystemsCharts pie (donut) + legend list; empty "No data yet — once deals enter the pipeline, the breakdown will appear." | same |
| 8 | Not built | forecast card, deal highlights, ops strip, AI signals, admin shortcuts, Chart Studio, manager widgets (leaderboard, stalled, comms, forecast) | — | backlog |

### U4 result — Salenix rev 47–49 (2026-11-13; rev 49 07:33 UTC env_app verified; Mentor session 7fedcb3f)
Charts: OutSystemsCharts chart blocks are not imported in Salenix (only ChartYAxis) and importing them is a Studio reference step, so the trend chart and the donut are inline SVG built by a client JS node and injected through data-sx-html + OnRender innerHTML (numbers and fixed labels only, no user text). No chart library, no Studio step.
| Rev | Elements |
|---|---|
| 47 | NEW block AdminHome(IsManager, WorkspaceName): GetKpis (Dashboard_GetKpis; manager fixed last_90d), GetMembers; toolbar = rep filter "Viewing" / "Whole team", range segments 7d / 30d / 90d / QTD / YTD / Custom (date → date + Apply; admin only), range label, record counts; 5 KPI tiles with tint bar, sub, scope and tooltips (texts per checklist) |
| 48 | Revenue trend card: "<n>-day trend" + "REVENUE" badges, title Organization / Team / "<rep> · revenue trend", subtitle "<range> · weekly buckets · <workspace>"; SVG = New pipeline bars (maroon, left axis, zig-zag on capped bars) + Won revenue line (emerald, right axis), 95th-percentile cap per axis (≥ 4 non-zero weeks and max > 3 × median), x labels "Nov 3", hover tooltip "WEEK OF Mon, Nov 3, 2026 / New pipeline / Won revenue / Lost deals" + capped note; empty text; legend |
| 49 | Stage bars card ("SNAPSHOT · REVENUE", Lead … Won, bar width classes, value + deals, close prob 10 / 30 / 60 / 80 / 100 %, ratio vs stage above good / warn / bad, "—" first) + donut card ("COUNTS", TOTAL DEALS, SVG donut 7 stage colours with slice tooltips "<Stage>: n (p%)", legend rows with counts and %, empty text); structure StageBarRow; functions StageWidthClass, StageRatio, TrendIsEmpty; Home: IfSalesHome (old home unchanged) / IfAdminHome (AdminHome for admin + manager); GetSummary / GetCounts only fetched for the sales persona |
Data gap: TrendWeekItem has no lost revenue amount → the "Lost revenue" dashed line is not drawn (tooltip shows "Lost deals n"); add LostRevenueMinor to Dashboard_GetKpis later (Sales, add-only) to complete it.
Validation 0 errors (known warnings + 1 single-child suggestion).
Browser tests U4 (read-only, any workspace):
1. Owner / admin Home: greeting, then toolbar (Viewing Whole team, 90d active, "Last 90 days", "<n> companies <n> contacts <n> active deals"), 5 KPI tiles, trend chart, stage bars + donut side by side (stacked on phone).
2. Click 30d → tiles + chart change; Custom → pick dates → Apply → "Nov 1, 2026 – Nov 13, 2026" style label.
3. Viewing → pick a rep → titles "<name> · revenue trend" / "<name> pipeline by stage — live snapshot"; record counts stay tenant-wide.
4. Hover a trend week → tooltip; hover a donut slice → "Proposal: 5 (17%)".
5. Manager: titles "Team …", no range segments.
6. Member / viewer: old home unchanged.

## U5 Companies / Contacts — parity checklist (notes/u5-u6-lists-tasks-palette-ui-spec.md U5a / U5b)
| # | Item | Source | Rebuild | Status |
|---|---|---|---|---|
| 1 | Companies chips | My companies · Unassigned · Has active deals ("Deals column counts at least one open deal") · Recently active ("Last Activity within 14 days") · New (30d) ("Created in the last 30 days") · Stale ("Last Activity older than 30 days (or never)"); Clear all only when a chip is on, also resets sort | same order, tooltips, inputs HasActiveDeals / RecentActivityDays 14 / CreatedWithinDays 30 / Stale + StaleDays 30; Clear all + Recently deleted (L3b) kept | same (+ L3b diffs kept) |
| 2 | Contacts chips | My contacts · Unassigned · Recently active ("Activity in the last 14 days") · No company ("Contacts not linked to a company") · New (30d) | same order (No company moved to position 4), RecentActivityDays 14, CreatedWithinDays 30; Company▾ chip (L3b) kept after New (30d) | same (+ L3b diff) |
| 3 | Company detail panels | center column: Tasks → "Account activity" (month groups, source pills "Deal · name" / "Contact · name", empty "Nothing has happened on this account, its deals, or its contacts yet.") → Comments → Notes | same blocks and order (EntityTasksPanel, TimelinePanel default chip set, CommentsPanel, NotesPanel) | same (child activity rows without name show "Deal" / "Contact", D18) |
| 4 | Contact detail panels | Tasks (no teammates → assignee "Owner (default)") → Comments → Notes → "Timeline" (no month groups, no source, empty "No activity yet for this record.") | same order; assignee list = members (diff: source passes no teammates) | same / small diff |
| 5 | Permissions | notes / comments by entity permission; tasks manageDeals | CanEdit = manageCompanies / manageContacts for notes + comments, manageDeals for tasks | same |

### U5 result — Salenix rev 50–51 (2026-11-13; rev 51 07:47 UTC env_app verified; Mentor session f86166d3)
| Rev | Elements |
|---|---|
| 50 | Companies: chips Has active deals / Recently active / New (30d) / Stale with source tooltips (HasActiveDeals, RecentActivityDays 14, CreatedWithinDays 30, Stale + StaleDays 30), ClearFilters + Clear all visibility + empty-state tests extended. Contacts: chips Recently active / No company (moved, title added) / New (30d) before the Company▾ chip; RecentActivityDays 14, CreatedWithinDays 30 |
| 51 | CompanyDetail: GetCompany.CanManageDeals; after the Deals card: EntityTasksPanel (company), TimelinePanel "Account activity" (month groups + source pills), CommentsPanel, NotesPanel. ContactDetail: GetContact.CanManageDeals; sx-grid-2 wrapped in a new sx-stack (Mentor copied the grid into the wrapper and deleted the original; contents checked: same cards / links / widget count), then EntityTasksPanel, CommentsPanel, NotesPanel, TimelinePanel "Timeline" |
Layout difference (on purpose): source company detail is 3 columns (left fields, center panels, right contacts / deals) and contact detail 2 columns; the rebuild keeps its current cards and puts the panels below them in the source order.
Browser tests U5 (chips read-only; panels write → CGB India): Companies chips filter (Stale + Recently active together → 0 rows, as source); Contacts "No company" sits between Recently active and New (30d); Company detail → add a task / note / comment; Account activity shows month headers ("November 2026 · n") and "Deal · <name>" pills on child deal notes / comments; Contact detail panels in order Tasks, Comments, Notes, Timeline.

## U6 Tasks page + command palette — parity checklist (notes/u5-u6-lists-tasks-palette-ui-spec.md U6a–c)
| # | Item | Source | Rebuild | Status |
|---|---|---|---|---|
| 1 | Tasks page header | "Tasks", subtitle workspace | same | same |
| 2 | Toolbar | search "Search tasks by title…" (250 ms), "New task" (editors) → drawer "Create task" / "Add an action item — optionally link it to a deal, company, or contact." | same; search keeps the chips | same / D14 |
| 3 | Chips | My tasks ("Tasks assigned to me") · Overdue ("Past-due, not completed") · Due this week · To do · In progress (exclusive with To do) · High / urgent · Unassigned; Clear all | same → Mine, Overdue, DueWithinDays 7, StatusCodes todo / in_progress, PriorityCodes "urgent,high", Unassigned | same |
| 4 | List | one card, no headers; active rows first; "No active tasks — <n> completed below."; "Completed (<n>)" bar with Show / Hide, collapsed | same | same |
| 5 | Row | check (editors), title (opens drawer), priority badge (click cycles), status badge (click cycles), due label, "<type>: <name>" link, hover delete (no confirm, toast "Task deleted") | same; link to the detail screen | same / D15 |
| 6 | Pagination | "Showing <n> rows", 25 / 50 / 100 (50), Prev / Next | "Showing x–y of n tasks" + Previous / Next, page 50 | different on purpose (L3b style) |
| 7 | Empty | "No tasks yet" / "No tasks match your search" + texts | same | same |
| 8 | Create form | Title ("Follow up with Razorpay on Q3 renewal"), Description ("Optional — context, links, expected outcome"), Priority (Medium) / Status (Todo), Due date / Assignee (me), Linked to ("— None —", Deal, Company, Contact) / Entity ("— Pick a type first —"…), "Create task"; toast "Task created — <title>" | same; entity lists from Deal_List / Company_List / Contact_List (first 200) | same |
| 9 | Task drawer | title = task title, "Created MMM d, yyyy"; click-to-edit title + description; priority / status badges cycle; due chip; Assignee / Creator / Due / Updated grid; LINKED TO pill → detail; comments ("No comments yet. Start the discussion below.", "Add a comment… (⌘⏎ to post)", "Post", delete own with confirm "Delete this comment?"); footer Delete (confirm "Delete this task? This action soft-deletes the row.") / Mark complete / Mark incomplete | same; viewers see read-only (D20); creator name via members list | same |
| 10 | Rail | "Tasks" (CheckSquare) only in the sales tree after At Risk | "Tasks" for every persona: sales tree after Pipeline (no At Risk page), admin / manager tree in the Sales group after Approvals; icon check-square | different on purpose (D19) |
| 11 | Palette trigger | top bar button "Search deals, companies, contacts…" + "Ctrl K" / "⌘K"; Ctrl/⌘ K toggles | same | same |
| 12 | Palette | input "Search deals, companies, contacts, workflows…", clear ×, "esc"; 120 ms debounce; empty query → "Recent" (6, localStorage per tenant) + "Quick actions" (Go to Dashboard, Go to Tasks, Create deal / company / contact, Create task); groups Deals / Companies / Contacts / Tasks; "No results for “q”."; footer "Searching…" / "<n> results" / "Quick actions + recent" + "↑ ↓ move  ↵ open"; ↑ ↓ wrap, Enter, Esc | same except: placeholder "Search deals, companies, contacts, tasks…" (no workflows), no "Go to Workflows"; company / contact hits open the detail screen (D16); task badge = status (D17); Create * open the screen with its create drawer (new OpenNew inputs) | mostly same |

### U6 result — Salenix rev 52–57 (2026-11-13; rev 57 08:41 UTC env_app verified; Mentor session f0f555ae)
| Rev | Elements |
|---|---|
| 52 | NEW blocks TaskListRow (check, title → Open, priority / status badges that cycle via UI_Task_Update (full write, other fields unchanged), due label, "<type>: <name>" link to the detail screen, hover delete "Task deleted") and TaskCreateForm (Title / Description / Priority / Status / Due date / Assignee / Linked to + Entity (Deal_List / Company_List / Contact_List, 200 each) / "Create task", toast "Task created — <title>") |
| 53 | NEW block TaskDetailBlock (click-to-edit title + description, badge cycles, due chip, Assignee dropdown / Creator (function MemberLabel) / Due / Updated grid, LINKED TO pill, task comments via Comment_* "task" with confirm delete, footer Delete (confirm) + Mark complete / incomplete); Sales has no Task_Get, so the drawer takes the TaskItem from the list and re-finds it after each refresh |
| 54 | NEW screen Tasks (inputs Mine, Search, OpenNew): search (250 ms, keeps chips), New task, 7 chips + Clear all, active list + "No active tasks — n completed below." + "Completed (n)" Show / Hide, pagination "Showing x–y of n tasks", empty states, Create task drawer, task drawer |
| 55 | Rail item "Tasks" (check-square) in both persona menus (admin / manager after Approvals, others after Pipeline); input OpenNew on Deals, Companies, Contacts (opens the create drawer); Tasks empty text with “New task” |
| 56 | NEW block CommandPalette logic + structure PaletteRow: Search_All with 120 ms debounce, Recent (localStorage "crm.palette.recent.<tenant id>", 6, de-duplicated) + Quick actions (Go to Dashboard, Go to Tasks (Mine), Create deal / company / contact (OpenNew), Create task), one global Ctrl/⌘ K handler (window.sxPaletteKey, removed on destroy), ↑ ↓ wrap, Enter, Esc |
| 57 | CommandPalette widgets (top-bar trigger "Search deals, companies, contacts…" + "Ctrl K" / "⌘K", overlay, input "Search deals, companies, contacts, tasks…", clear ×, "esc", groups, rows with kind icons / subtitle / badge / ↵ on the selected row, "No results for “q”.", footer counts + key hints) placed as the first item of the MainLayout top bar |
Validation 0 errors (known warnings).
Browser tests U6 (writes → CGB India):
1. Rail shows "Tasks" for every role; Tasks page lists tasks; chips filter (To do / In progress exclusive); search "call" keeps chips.
2. New task → drawer → link to a deal → "Create task" → toast "Task created — …" → row with "deal: <name>" link.
3. Row: click priority badge cycles Low → Medium → High → Urgent; status badge cycles; check marks complete → row moves to "Completed (n)"; trash deletes ("Task deleted").
4. Title click → drawer: edit title (Enter), description (Ctrl+Enter), assignee, due; add / delete a comment (confirm); Delete task (confirm) closes the drawer.
5. Ctrl K (⌘K on Mac) anywhere → palette with Recent + Quick actions; type "acme" → groups Deals / Companies / Contacts / Tasks; ↓ ↓ Enter opens; reopen → the hit is under Recent; "Create deal" → Deals with the create drawer open; Esc / backdrop closes.

# UI batch U — close-out (2026-11-13)
- U1–U6 done: Salenix rev 39 → 57 (Theme rev 8 for the CSS). Every revision env_app-verified; only Salenix and Salenix_Theme changed; no Sales / Foundation change, no data written by me, no Production.
- Data gaps for later Sales add-only work: BoardCardItem HealthScore / ContactName / OriginalCloseDate / risk; Deal_ListBoard amount / close / stale / approval / PO / revisit inputs; TrendWeekItem LostRevenueMinor; SearchHitItem PriorityCode; Comment_Update (comment edit); call / meeting log action ("Log activity"); Task_Get; Task_List due sort + done-in-7-days count; Dashboard prior-period values, quota, highlights.
- Open UI follow-ups: panels on DealDetail do not refresh after the edit / stage drawers; sales-persona home (My tasks etc.); company / contact detail 3-column source layout; Recently deleted page; cleanup of unused ApplyFilters / UI_ApprovalOptions / UI_DealSortOptions / StageFilter; re-select Phosphor icons (open warning list).
