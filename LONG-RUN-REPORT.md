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
