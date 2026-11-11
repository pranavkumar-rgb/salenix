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
