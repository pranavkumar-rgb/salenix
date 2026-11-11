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
