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

## F8 Foundation entities RealtimeEvent, WorkflowRun, ChartView, DashboardWidget — PLAN
Source: notes/source-schema-sales.md (workflow-runs.ts 0006, chart-views.ts 0022, dashboard-widgets.ts 0023); RealtimeEvent has no source table (BRD-App-Logic l.977). jsonb → Text 8000 (Metadata 4000 like Notification). All Public = No, Optimized, Id AutoNumber, TenantId mandatory Delete, user ids User Identifier (Ignore), LegacyUuid Text 36 (non-unique index) except RealtimeEvent.
| Entity | Attributes | Indexes |
|---|---|---|
| RealtimeEvent | TenantId; Type Text 100 M; EntityType Text 50; EntityId Long Integer; ActorUserId User Identifier; Payload Text 4000; CreatedAt DT M | (TenantId, CreatedAt) |
| WorkflowRun | TenantId; WorkflowId Text 250 M; EventName Text 250; ExternalRunId Text 250 (source inngest_run_id); Status Text 50 M default "running" (running, completed, failed, skipped); StartedAt DT M; CompletedAt DT; Error Text 2000; Metadata Text 4000; LegacyUuid | (TenantId, StartedAt); ExternalRunId |
| ChartView | TenantId; UserId User Identifier M; ChartKey Text 100 M; Title Text 250; RoleScope Text 50 M default "user" (user, team, tenant); Config Text 8000 M; IsDefault Boolean M default False; CreatedAt, UpdatedAt DT M; LegacyUuid | (TenantId, UserId, ChartKey) — source partial unique "one default per user+chart" cannot be an index in ODC → enforced by the save action later |
| DashboardWidget | TenantId; UserId User Identifier M; WidgetType Text 100 M; ChartType, Metric, Dimension Text 100; Title Text 250; LayoutX, LayoutY Integer M default 0; LayoutW, LayoutH Integer M default 4; ConfigJson Text 8000 M; CreatedAt, UpdatedAt DT M; LegacyUuid | (TenantId, UserId) |
