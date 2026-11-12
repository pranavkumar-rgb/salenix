# Morning report — overnight run N (2026-11-13, Salenix_Sales server part of P2)

## Result
All 9 batches DONE, none skipped, none failed. Only **Salenix_Sales** changed: **rev 21 → rev 28** (Development, every revision env_app-verified). ADD-only: no entity, attribute, action, input or output was deleted or renamed; every existing public signature works unchanged (new inputs are optional). **No data was written** in any workspace (no DevOnly_*, seed, import or test call) — Vizion untouched. No UI, Foundation, DevTools, Theme, UIKit, Studio or Production change.

| Batch | What | Sales rev |
|---|---|---|
| N1 (L22) | Deal soft delete + restore, deleted-deals list | 22 |
| N2 (L23) | Inline edit of Next step / Source / Competitor | 22 |
| N3 (L11, L12, L14, L15, L16) | Deal / Company / Contact list filters + sort direction | 23 |
| N4 | Tasks (list, create, update, complete / reopen with recurrence, delete) | 24 |
| N5 | Notes, comments (with @mentions), merged activity timeline | 25 |
| N6 | Admin home KPIs + weekly revenue trend + stage totals | 26 |
| N7 | Full pipeline board (7 columns, weighted / slipped totals, card flags) | 27 |
| N8 (L24) | Global search for the command palette | 28 |
| N9 | Offerings list + set a deal's offerings | 28 |

Details (parity checklist per batch, read-back notes, test steps): LONG-RUN-REPORT.md → "Overnight run N".

**git:** local commits only — `git push` was blocked by the local permission check. Please run `git push`.

## Your one Studio step
Open a fresh copy of **Salenix** (rev 36) → Manage Dependencies → **Salenix_Sales** → refresh to rev 28 and tick everything in the list below (nothing else) → publish from Studio. Existing screens keep working unchanged; Deal_List / Company_List / Contact_List only get new optional inputs.

## Reference list for the Salenix app (Salenix_Sales rev 28), grouped by screen
All are public service actions; the client never sends tenant / user / role (acting context on the server; persona "sales" = own + unassigned).

### Deals list
| Element | Enables in the UI |
|---|---|
| Deal_List — new optional inputs IncludeDeleted, Unassigned, StaleOnly, StaleDays, PoMissing, RevisitDue, SlippedOnly, ScoreBand, AmountMinorMin, AmountMinorMax, CloseFrom, CloseTo, CompanyId, StageCodes, OwnerUserIds, SortDir (refresh only) | chips Unassigned, Stale only, Won · PO missing, Revisit due, Slipped only, Score, Amount, Close date, Company; multi-select Stage / Owner (comma lists); asc / desc per sort column ("" keeps today's order); "Recently deleted" view (owner / admin) |
| DealListItem.DeletedAt (new attribute) | "Deleted on" column in the deleted view |
| Deal_Restore(DealId) | Restore button in the deleted view |

### Deal detail + edit-deal drawer
| Element | Enables |
|---|---|
| Deal_SoftDelete(DealId) | Delete button left in the edit-deal footer (L22) |
| Deal_UpdateContextField(DealId, Field next_step / source / competitor, Value) → SavedValue | click-to-edit Next step / Source / Competitor on the Context card (L23) |
| Task_List(EntityType "deal", EntityId) + Task_Create / Task_Update / Task_SetCompleted / Task_Delete; TaskItem | Tasks panel |
| Note_List / Note_Create / Note_Update / Note_Delete; NoteItem | Notes card (edit / delete own notes only: CanEdit) |
| Comment_List / Comment_Create / Comment_Delete; CommentItem | Comments with one reply level and @[Name](userId) mentions (mentioned members are notified) |
| Activity_ListFor(EntityType, EntityId, MaxItems); TimelineItem | merged timeline (activity + notes + comments, newest first, 150); filter chips are UI-only |
| Offering_ListForDeal(DealId), Offering_List(IncludeInactive), Deal_SetOfferings(DealId, OfferingIds, PrimaryOfferingId); OfferingItem | Offerings field (only when the workspace flag OfferingsEnabled is on) |

### Pipeline
| Element | Enables |
|---|---|
| Deal_ListBoard(Search, OwnerUserId, Unassigned, StageCodes, SlippedOnly, ScoreBand, CompanyId) → Columns; BoardColumn, BoardCardItem | 7 columns incl. Won / Lost; per column count, value, weighted value, slipped count / value, capped-at-50 flag; card badges overdue, slip severity, de-weighted, rolled back, health. Hide Hold when DealCount = 0 (source) |

### Home (admin / manager)
| Element | Enables |
|---|---|
| Dashboard_GetKpis(RangeCode last_7d / last_30d / last_90d / this_quarter / this_year / custom, FromDate, ToDate, OwnerUserId); TrendWeekItem (StageTotals reuses StageTotalItem) | KPI strip (total + weighted pipeline, win rate, avg deal size, avg sales cycle), range picker, rep view, record counts, weekly revenue trend chart, stage donut / bars |
| Task_List(Mine True, Overdue True) | "My tasks" counts (TotalCount) |

### Companies / Company detail
| Element | Enables |
|---|---|
| Company_List — new optional inputs HasActiveDeals, RecentActivityDays, CreatedWithinDays, Stale, StaleDays (refresh only) | chips Has active deals, Recently active (14d), New (30d), Stale (L15) |
| Note_* / Comment_* / Activity_ListFor("company", id) / Task_List("company", id) | company notes, comments, tasks, account timeline incl. child deals / contacts |

### Contacts / Contact detail
| Element | Enables |
|---|---|
| Contact_List — new optional inputs RecentActivityDays, CreatedWithinDays (refresh only) | chips Recently active (14d), New (30d) (L16) |
| Note_* / Comment_* / Activity_ListFor("contact", id) / Task_List("contact", id) | contact notes, comments, tasks, timeline |

### Tasks page (new) and app shell
| Element | Enables |
|---|---|
| Task_List(Search, Mine, Unassigned, StatusCodes, PriorityCodes, Overdue, DueWithinDays, StartIndex, MaxRecords) + Task_* | Tasks page with chips My tasks, Overdue, Due this week (DueWithinDays 7), To do, In progress, High / urgent, Unassigned; complete / reopen (recurring tasks create the next one → SpawnedTaskId) |
| Search_All(Query) → Deals, Companies, Contacts, Tasks; SearchHitItem | Ctrl K / Cmd K command palette in MainLayout (L24), 5 hits per group |

**Tick list in Studio (22 actions + 9 structures):** Deal_SoftDelete, Deal_Restore, Deal_UpdateContextField, Task_List, Task_Create, Task_Update, Task_SetCompleted, Task_Delete, Note_List, Note_Create, Note_Update, Note_Delete, Comment_List, Comment_Create, Comment_Delete, Activity_ListFor, Dashboard_GetKpis, Deal_ListBoard, Search_All, Offering_List, Offering_ListForDeal, Deal_SetOfferings; TaskItem, NoteItem, CommentItem, TimelineItem, TrendWeekItem, BoardColumn, BoardCardItem, SearchHitItem, OfferingItem; plus refresh of Deal_List, Company_List, Contact_List, DealListItem.

## Open questions (your decision)
1. Task_Update writes type / recurrence / stage blocking — the source's full update silently drops them (looks like a source bug). Kept as a fix; say if you want source behaviour.
2. Task comments: source has a separate task_comments table (author-only edit, hard delete). Sales has no such entity; Comment_* accept only deal / company / contact. Allow EntityType "task" in Comment_* (CLAUDE.md app map already says Comment.EntityType incl. task) or add a TaskComment entity?
3. Offerings on Deal_Create / Deal_Update: the source requires an offering on create when the switch is on. I did not touch the tested create / update; the UI would call Deal_SetOfferings after saving. OK, or wire into Deal_Create?
4. Offering catalog: no Offering rows exist (source seeds 14 defaults per workspace + admin page). Needs Salenix_Admin + a seed — never in Vizion without your go.
5. Amount filter: AmountMinorMin / Max ≤ 0 = no filter (optional-input default), so "max 0" cannot be asked. Fine?
6. Deal company filter uses one CompanyId (source: several company names).
7. KPI prior-period deltas and quota / coverage tiles not built (need stage-history replay and Finance SalesTarget). Weighted pipeline uses the fixed stage table (no workspace overrides).
8. Deal_Restore only restores deleted rows (stricter than source, same as Company_Restore).

## Known limits (low)
- Search / list "like" filters do not escape % and _ (same as all existing lists).
- Comment mentions: an invalid token (empty id) stops parsing; the rest of the body stays plain text.
- Activity_ListFor: child-deal / child-contact activity rows have an empty SourceName (ActivityLog.EntityId is text); notes / comments have it. No emails (Comms not built).
- Deal_ListBoard: no per-column "load more". Deal_List risk sort, missing-field and offering filters not built.
- MCP: one long Mentor prompt got a CloudFront 403 "Request blocked"; smaller prompts worked.

## Tests (CGB India only — never write tests in Vizion)
Steps per batch in LONG-RUN-REPORT.md. Read-only actions (Dashboard_GetKpis, Deal_ListBoard, Search_All, the list filters, Activity_ListFor, Note_List, Comment_List, Task_List, Offering_List) are safe in any workspace.

---

# Previous morning report (2026-11-11 night run)

# Morning report — Salenix UI app for the 10 AM demo (run 2026-11-11 night)

## What is live
**App:** https://vtpl-dev.outsystems.app/Salenix — **Salenix rev 21** after the 2026-11-12 parity run (see "Changes from the 2026-11-12 parity run" below); before that **rev 13** (Development, env_app verified 2026-11-11 19:55 UTC). Every batch A0–A7 was published clean (0 errors) and verified; nothing skipped, no batch failed. Sales was only read (Company_List bug check), not changed. No Studio publish, no Production, no GL_* app touched.

| Batch | Screens | Rev |
|---|---|---|
| A0 | Template cleanup, Login (role SalenixUser, then EnsureMyProfile + GetMyContext → Home or Workspaces), shell MainLayout (rail menu, workspace switcher, notifications drawer, log out), Workspaces | 5 |
| A1 | Home dashboard (6 KPIs, pipeline by stage bars, top 5 open deals, recently updated) | 6 |
| A2 | Deals list (search, stage, owner, approval, include closed, sort, paging) + Pipeline board (5 open stages, "Move stage…" on every card → stage drawer with hold / lost / rollback reasons) | 7 |
| A3 | Deal detail (path bar, approve/reject, KPIs, details, governance, order proofs add/remove, stage history, date changes, audit log) + deal create/edit form | 8, 9 |
| A4 | Companies list / detail / form with duplicate warning, soft delete + restore | 10 |
| A5 | Contacts list / detail / form with duplicate warning, soft delete + restore | 11 |
| A6 | Approvals (Won deals pending approval, approve / reject with reason) | 12 |
| A7 | Fixes from the read-back smoke test (edit of Lost deals, form load errors, full reload after workspace switch, date guards) | 13 |
Details, source file per screen and deviations: LONG-RUN-REPORT.md, section "Run 2026-11-11 night 2".

## Before the demo (5 minutes)
1. Log in at the URL above as the workspace owner; the active workspace must be **CGB India** (TenantId 3). If you land on Workspaces, click **Open** on CGB India.
2. Click through Home, Deals, Pipeline, Companies, Contacts, Approvals once. Nothing was seen in a browser yet (the MCP cannot render screens) — if a page looks wrong, tell me the screen and I fix it before 10.
3. Optional: Workspace flag **DedupEnabled** must be on to show the duplicate warning (DevTools T0 Flags → tick DedupEnabled → Save).

## 10-minute demo click path (demo data in CGB India)
1. **Home (1 min)** — KPI row: open pipeline, 23 open deals, won this month, closing in 30 days, overdue, pending approvals (≥ 1). Pipeline by stage bars. Click **[DEMO] Data centre migration** in Top open deals → deal detail.
2. **Deal detail (2 min)** — header line stage · value · close date, path bar; left column Deal Info (pushes, slip days, quota weight) and Relationship (company, contact, owner); right column order proofs, stage history, audit log.
3. **Pipeline (2 min)** — menu Pipeline: 5 columns (Lead 5, Qualified 5, Proposal 5, Negotiation 5, Hold 3) with count + value. On **[DEMO] Paint shop robotics** (Proposal) choose Move stage → Qualified: the drawer asks for a rollback reason (pick "Requirement changed") → Move deal. Card moves back; the deal's audit log shows a rollback.
4. **Order-proof check (2 min)** — open **[DEMO] Dealer portal revamp** (Negotiation, Maruti Suzuki, ₹48,00,000). Change stage… → Won → Move deal → the drawer shows **"Attach the client's PO (or other order confirmation) before marking this deal Won."** Close the drawer, **Add PO**: Purchase order, number PO/MSIL/2026/001, PO date = today, buyer "Maruti Suzuki India Ltd", value before tax 4800000, tax 864000 → Save PO. Change stage… → Won → Move deal → success (as owner it is auto-approved; badge "Approved").
5. **Approvals (1 min)** — menu Approvals: **[DEMO] Forging dies retrofit** (₹2,75,000, Won, pending). Click **Approve** → "Deal approved." and the list empties. (To show Reject instead: Reject → reason → Confirm reject → the deal goes back to Negotiation.)
6. **Companies (1 min)** — open **[DEMO] Tata Steel Ltd**: KPIs from the rollups, contacts, its deals. New company → type "[DEMO] Tata Steel Ltd" → Create company → **Possible duplicates** panel (exact match) with "Open existing" / "Create anyway" (needs DedupEnabled).
7. **Contacts + notifications (1 min)** — Contacts list with company filter; bell in the top bar opens the notifications drawer (Mark all read).

### Changes from the 2026-11-12 parity run (Salenix rev 14–21)
- Menu depends on the role: owner/admin/manager see Workspace › Dashboard · Sales › Pipeline, Deals, Approvals · CRM › Contacts, Companies; member/viewer see "My Work" › Dashboard, My Deals, Pipeline · CRM (no Approvals).
- Home starts with "Good morning, {name}." + workspace + Administrator pill; admin sees "12 companies · 25 contacts · 23 active deals" above the KPIs.
- Deals and Pipeline have **List | Board** tabs top right; Deals has chips **My deals / Awaiting approval / Approved** (step 5 alternative: Deals → Awaiting approval chip shows the pending Won deal).
- Pipeline: **Hold column is collapsed** ("Hold 3 · ₹… · parked · click to expand") — click it to show the 3 hold cards. Cards are tinted green / amber / red by health.
- Deal detail (step 2): no KPI tiles any more; "← All deals" link, line "{Stage} · {amount} · close {date}", left column Deal Info (incl. governance: pushes, slip days, quota weight) / Relationship / Context / Record, right column Order proofs → Stage history → Close date changes → Audit log. Step 4 "Add PO" is now at the top of the right column.
- Approvals (step 5): card list ("Deal approvals"), Approve / Reject on each card.
- Companies / Contacts: owner dropdown replaced by chips My companies / My contacts / Unassigned; Contacts has a Created column.

## Known gaps (not built — say "next sprint")
- Parity backlog (needs Sales / Foundation changes, listed in LONG-RUN-REPORT.md "Parity backlog (after demo)"): contact on deal cards, risk scores, admin KPI set + revenue trend, Won/Lost board columns, weighted totals, more deal chips (Unassigned, Stale, PO missing, Revisit due, Slipped), recently approved/rejected, bulk actions, activity/tasks/notes on deal detail.
- Health tint on board cards is a full tinted border (theme has no left-border class); Approvals page uses the standard page width (theme has no narrow container).
- No drag-and-drop on the board (Move stage dropdown instead); board shows only the 5 open stages (Won/Lost via Deals list "Include won/lost").
- Order proofs: metadata only, no PO file upload / PDF reading; warning checks (buyer/supplier mismatch) not built (decision 22d).
- Deal contacts: only the primary contact is shown (no DealContact list action in Sales). Contact detail has no deals list (DealListItem has no ContactId).
- Company detail deals come from Deal_List(search = company name, 200) filtered by company id in the UI — Deal_List has no CompanyId filter (add to Sales later).
- Hold / rollback reason and PO type lists are client copies of the source lists; Foundation has no public lookup read action yet.
- Approvals: no "recently approved / rejected" lists, no bulk approve.
- No visual comparison with the source pages yet (A7 was fixes only); light mode only. Login page still uses the template layout (LayoutBlank) with the Salenix theme.
- Not in this app: tasks, notes, comments, activity, reports, invoices, settings/admin (Salenix_Admin), AcceptInvite screen, first-workspace creation UI.
- Company_List: SortDesc bug suspected in R1 is **not real** (aggregate sorts descending; checked node text). Other Sales findings logged (PageSize only 25/50/100, no StartIndex guard).

## Manual steps for you
- `git push` (commit below is local).
- Studio check (optional, view only): Salenix rev 13 — MainFlow screens, blocks MainLayout / StageChangeDrawer.
- Still pending from earlier runs: DevTools T1–T8 tests (LONG-RUN-REPORT.md "Test order"); decision 19 clean-up before QA.

---

# Overnight run — Salenix_Foundation (2026-11-11)

Start state: Salenix_Foundation rev 11 (Batch B), live in Development. Batch B marked DONE in CLAUDE.md; migration check list item for AiInvocationKind/risk_explanation added.

---

## Batch C (F3) — OrganizationSettings, CustomFieldDefinition

**Status: PUBLISHED — rev 12** (env_app Development revision = 12, deployment 251c534d-…, 2026-11-10T18:36Z). Mentor validation: 0 errors, 22 warnings (all pre-existing, per Mentor). Verified with context_entities after publish: every attribute below exists with the listed type, length, mandatory flag and default.

### Elements created
| Entity | Attribute(s) | Type | Len | M | Default | Delete rule |
|---|---|---|---|---|---|---|
| OrganizationSettings | Id | Long Integer AutoNumber | | Y | | |
| | TenantId | Tenant Identifier | | Y | | Delete |
| | DisplayName | Text | 250 | | | |
| | LogoUrl, InvoiceLogoUrl | Text | 1000 | | | |
| | DefaultCurrency | Text | 3 | Y | "INR" | |
| | CurrencyLocale, Locale | Text | 50 | Y | "en-US" | |
| | Timezone | Text | 50 | Y | "UTC" | |
| | CompactNumbers, ReducedMotion, DealUnitPricingEnabled, EmailTracking | Boolean | | Y | False | |
| | FiscalYearStartMonth | Integer | | Y | 1 | |
| | DefaultTheme | Text | 50 | Y | "light" | |
| | Density | Text | 50 | Y | "comfortable" | |
| | StageProbabilities | Text (JSON) | 2000 | | | |
| | DefaultDealStage | Text | 50 | Y | "lead" | |
| | DefaultReportRange | Text | 50 | Y | "last_30d" | |
| | DashboardMetrics | Text | 250 | Y | "pipeline,won,wonRecent,activity" | |
| | SenderName, ReplyTo, AutoBcc | Text | 250 | | | |
| | SenderEmail | Email | 256 | | | |
| | DefaultSignature, InvoiceSellerAddress, InvoicePaymentTerms, InvoiceDeliveryTerms | Text | 4000 | | | |
| | AiEnabled, NotifyAssignments, NotifyRecommendations, NotifyWorkflows, NotifyEmailEvents, RealtimeEnabled | Boolean | | Y | True | |
| | AiAggressiveness | Text | 50 | Y | "balanced" | |
| | InsightRefresh | Text | 50 | Y | "on_change" | |
| | InvoiceSellerName, InvoiceGstin, InvoicePan, InvoiceCin, InvoiceBankName, InvoiceBankAccount, InvoiceBankIfsc, InvoiceBankBranch | Text | 250 | | | |
| | StageGatesEnabled, DealScoringEnabled, EmailSyncEnabled, ForecastsEnabled, DedupEnabled, OfferingsEnabled | Boolean | | Y | False | |
| | StandardCreditDays | Integer | | Y | 30 | |
| | MaterialExcessDays | Integer | | Y | 15 | |
| | UpdatedBy | User Identifier | | | | Ignore |
| | CreatedAt, UpdatedAt | Date Time | | Y | (none) | |
| CustomFieldDefinition | Id | Long Integer AutoNumber | | Y | | |
| | TenantId | Tenant Identifier | | Y | | Delete |
| | EntityType, FieldType | Text | 50 | Y | | |
| | Key, Label | Text | 250 | Y | | |
| | Options | Text (JSON array) | 4000 | Y | "[]" | |
| | Required, Archived | Boolean | | Y | False | |
| | SortOrder | Integer | | Y | 0 | |
| | CreatedAt, UpdatedAt | Date Time | | Y | (none) | |
| | LegacyUuid | Text | 36 | | | |

Indexes (Mentor report): OrganizationSettings `OrganizationSettings_TenantI` (TenantId, UNIQUE); CustomFieldDefinition `CustomFieldDefinition_Tenant` (TenantId, EntityType, Key, UNIQUE), `CustomFieldDefinition_Tenan2` (TenantId, EntityType, Archived, SortOrder), `CustomFieldDefinition_Legacy` (LegacyUuid, not unique), plus the auto FK index on TenantId.

### Deviations and how they were handled
| # | Deviation | Handling |
|---|---|---|
| C1 | Index names truncated to 28 characters by Studio (e.g. `OrganizationSettings_TenantI`). | Accepted (cosmetic). Rename in Studio only if you want readable names. |
| C2 | Mentor removed the auto-generated non-unique FK index on OrganizationSettings.TenantId because it duplicated the unique index. | Accepted (correct). |
| C3 | Source vs CLAUDE.md: source default Timezone is `UTC` and Locale/CurrencyLocale `en-US`; CLAUDE.md decision 7 says Timezone "default Asia/Kolkata". | Entity uses the **source** defaults as you asked ("same defaults"). Decision 7 is still honoured if `Foundation_TodayFor` falls back to Asia/Kolkata when no row exists. **Decision for you** (see end): should new workspaces get Asia/Kolkata / en-IN instead? |
| C4 | Source PK is tenant_id (no own id); we have Id AutoNumber + TenantId unique. | By design (ODC needs an Id). No LegacyUuid on OrganizationSettings (source had none). |
| C5 | DefaultDealStage must not accept "hold" (BRD gap #5; source UI offers it, DB CHECK rejects it). | Entity is plain Text; **pending**: the save action (later, Admin settings) must reject `hold` and only accept lead, qualified, proposal, negotiation, won, lost. |
| C6 | Source CHECK constraints (FiscalYearStartMonth 1–12, theme, density, report range, AI policy values) are not on the entity. | Pending: enforce in the settings save actions (Salenix_Admin phase). |
| C7 | BRD maps source "unlimited" text to "2000+"; I used Text 4000 for signature / address / terms / Options and 2000 for StageProbabilities. | Recorded. |

### Tests for you (morning)
1. ODC Studio → open Salenix_Foundation (fresh copy, rev 12+) → Data → OrganizationSettings: check TenantId delete rule = Delete, UpdatedBy delete rule = Ignore, index TenantId unique.
2. CustomFieldDefinition: TenantId delete rule = Delete; unique index on (TenantId, EntityType, Key).

---

## Batch D (F4) — context and permissions

**Status: PUBLISHED — rev 13** (env_app Development revision = 13, deployment 27faebb3-…, 2026-11-10T18:41Z). Mentor validation: 0 errors, 22 pre-existing warnings. Verified after publish with context_search: the 4 service actions are Public = Yes (actionType ServiceAction) with the inputs/outputs below; Foundation_ResolveContext is a private server action.

### Elements created / changed
| Element | Type | Public | Inputs | Outputs |
|---|---|---|---|---|
| MembershipMissing | User exception | n/a (ODC has no Public flag on exceptions) | | |
| FoundationError | User exception | n/a | | |
| Foundation_EnsureMyProfile | Service action | Yes | – | Created (Boolean) |
| Foundation_GetMyContext | Service action | Yes | – | ActingUserId (User Id), ActingTenantId (Tenant Id), RoleCode, Persona |
| Foundation_HasPermission | Service action | Yes | RoleCode, PermissionCode (Text, M) | HasPermission (Boolean) |
| Foundation_GetFlags | Service action | Yes | – | StageGatesEnabled, DealScoringEnabled, EmailSyncEnabled, ForecastsEnabled, DedupEnabled, OfferingsEnabled |
| Foundation_ResolveContext | Server action (private) | No | – | same 4 as GetMyContext |
| Tenant (entity) | **changed: Public = Yes** | Yes | | |

Logic as reported by Mentor (node text checked against spec):
- EnsureMyProfile: GetUserId() = NullTextIdentifier() → MembershipMissing "You are not signed in."; existing UserProfile → Created = False, no update; else reads ODC User, Email = ToLower(Trim(Email)) (fallback Username), First/Last name split at first space, cut to 100, ActiveTenantId empty, Created = True.
- ResolveContext: no user → "You are not signed in."; no profile or ActiveTenantId empty → "No active workspace. Choose a workspace to continue."; membership missing or Status <> "active" → "You are no longer a member of this workspace. Switch workspace to continue."; RoleCode = ToLower(Trim(Role)); Persona owner/admin → admin, manager → manager, else sales.
- HasPermission: empty input → False; aggregate with ToLower(Trim()) on both sides.
- GetFlags: ResolveContext → OrganizationSettings by ActingTenantId; missing row → all False; no row created.
- Handlers on all 4 service actions: MembershipMissing re-raised with original message (Abort = Yes); AllExceptions → LogMessage(original, "Salenix_Foundation.<Action>") → raise FoundationError "Something went wrong. Please try again." (Abort = Yes).

### Deviations and how they were handled
| # | Deviation | Handling |
|---|---|---|
| D1 | Tenant entity set to Public = Yes (needed for the public Tenant Identifier output). | Accepted; all domain apps need Tenant Identifier anyway (decision 2). Nothing else on Tenant changed. Note: consumers can now read Tenant rows read-only; the ADDENDUM rule (only Foundation reads Tenant unfiltered) is now a code-review rule. |
| D2 | User exceptions cannot be Public in ODC. A consumer app will see MembershipMissing as a generic exception with the message text. | Recorded. **Decision for you**: consumers detect "membership missing" by message, or we add an output flag variant (e.g. Foundation_TryGetMyContext with IsValid + Message) later. |
| D3 | Extra private server action Foundation_ResolveContext (a service action cannot call another service action in the same app). | Accepted (good pattern; Batch E/F reuse it). |
| D4 | NullTextIdentifier() used for the user id check (User Identifier is text in ODC). | Correct for ODC. |
| D5 | Foundation_TodayFor not built (needs C# timezone library). | **Pending** as instructed. |
| D6 | EnsureMyProfile does not set ActiveTenantId (FR-AUTH-02: first active membership by workspace CreatedAt). | Left to Workspace_ListMine / SetActive (Batch E). Pending: the UI login landing must call EnsureMyProfile → ListMine → SetActive. |
| D7 | UserProfile.Email is unique: a second ODC user with the same email (or empty email + empty username) hits the unique index → logged, user sees FoundationError. | Recorded; acceptable for now. |

### Tests for you (morning)
Use a temporary test screen or a timer in a consumer app is not possible yet, so test in Foundation itself with a throw-away admin screen in Studio **only if you want**; otherwise test after the UI app exists:
1. Signed-in user with no profile → EnsureMyProfile returns Created = True; row in UserProfile with lower-case Email, names split. Second call → Created = False, row unchanged (UpdatedAt same).
2. GetMyContext with ActiveTenantId empty → error "No active workspace. Choose a workspace to continue."
3. After Workspace_Create (Batch E) → GetMyContext returns RoleCode owner, Persona admin.
4. HasPermission("Owner ", "manageRoles") → True; ("member","manageRoles") → False; ("", "x") → False.
5. GetFlags for a tenant without OrganizationSettings → all False; with the row (Workspace_Create creates it) → all False (defaults); set one flag True in the DB → returned True.

---

## Batch E — split into E1 (F5 workspace) and E2 (F7 notification + attachment)

I split Batch E into two Mentor sessions / two publishes to keep each Mentor change small (team rule "one small task per change").

### E1 (F5) — Workspace_ListMine / SetActive / Create

**Status: PUBLISHED — rev 14** (env_app Development revision = 14, deployment e2d95306-…, 2026-11-10T18:50Z). 0 errors, 22 pre-existing warnings. Verified with context_search: 3 public service actions + private MasterData_EnsureRow with the inputs/outputs below.

| Element | Type | Public | Inputs | Outputs |
|---|---|---|---|---|
| ValidationError | User exception | n/a | | (already existed — reused, not created) |
| WorkspaceItem | Structure | Yes | | TenantId, Name, Slug, RoleCode, IsActive |
| Workspace_ListMine | Service action | Yes | – | Workspaces (List of WorkspaceItem), ActiveTenantId |
| Workspace_SetActive | Service action | Yes | TenantId (M) | – |
| Workspace_Create | Service action | Yes | Name (M) | TenantId |
| MasterData_EnsureRow | Server action (create-only) | No | TenantId, Type, Code, Name, SortOrder | Created |
| GenerateGuid | System reference added by Mentor | | | |

Logic (from Mentor node text, checked):
- ListMine: active memberships of GetUserId() joined to Tenant, sorted Tenant.CreatedAt asc; RoleCode lower-case; IsActive = tenant is UserProfile.ActiveTenantId.
- SetActive: membership (tenant, me) must exist and be "active", else MembershipMissing "You are not a member of this workspace."; profile missing → ValidationError "Your profile is missing. Sign in again."; updates ActiveTenantId + UpdatedAt only.
- Create: Foundation_ResolveContext (no active workspace → MembershipMissing = invite-only) → RoleCode must be owner else ValidationError "Only an admin can create a workspace." (exact source text) → Trim name, < 2 "Use at least 2 characters", > 80 "Use at most 80 characters" → slug = lower-case a–z0–9 with single dashes, trailing dash removed, cut to 40, empty → "workspace", + "-" + 6 hex chars from GenerateGuid, unique check on Tenant.Slug with max 5 attempts → Tenant → Membership(owner, active) → OrganizationSettings (entity defaults, UpdatedBy = actor) → MasterData HoldReason 6 rows + DealSource 5 rows → switch UserProfile.ActiveTenantId. One transaction. Offerings hook = Comment node only.
- Handlers on the 3 service actions: ValidationError and MembershipMissing re-raised unchanged; AllExceptions → LogMessage + FoundationError "Something went wrong. Please try again."

#### Deviations (E1)
| # | Deviation | Handling |
|---|---|---|
| E1-1 | Source shows "Only an admin can create a workspace." although the rule is owner-only. | Kept the exact source text. **Decision for you**: change to "Only an owner can create a workspace."? |
| E1-2 | Source max-80 message is the zod default; I used "Use at most 80 characters". | Recorded. |
| E1-3 | Source does NOT create organization_settings at workspace creation (lazy upsert on first read); we create it in Workspace_Create as you asked. | Recorded (better: GetFlags/TodayFor always find a row). |
| E1-4 | MasterData presets: source has presets only for HoldReason (src/lib/deals/hold.ts) and DealSource (deals/[id]/page.tsx: Outbound, Referral, Inbound-Website, Partner, Other). **No presets exist in the source for LostReason or Industry** (both free text). | Seeded HoldReason (6) and DealSource (5); none for LostReason / Industry. Note: source stores the deal source as the label text ("Outbound"); our MasterData code is "outbound" — the Sales migration must map label → code. |
| E1-5 | Default owner info@cgbindia.com (BRD gap #28) | NOT added, as instructed. **Decision pending.** |
| E1-6 | Offerings seed | NOT done; Comment node hook in Workspace_Create. Pending for Salenix_Sales (needs a cross-app call, best effort). |
| E1-7 | Source "switch workspace" (userHasMembership) does not check Status = active; we do. | Stricter on purpose (FR-AUTH-07 "re-validates membership"). |
| E1-8 | **Bootstrap gap:** Workspace_Create needs the caller to already be owner of an active workspace (source rule). Development has **no Tenant and no Membership yet**, so nobody can create the first workspace through these actions. | **Decision for you** (blocks testing D/E/F): (a) a one-time admin-only Bootstrap_FirstWorkspace action (creates tenant + owner membership for the ODC user who runs it, only when Tenant is empty), or (b) insert the first rows by hand. I did not build either. |
| E1-9 | FR-AUTH-02 (after login, active workspace = first active membership by CreatedAt) is not automatic. | Pending for the UI: login landing calls EnsureMyProfile → ListMine → SetActive(first) when ActiveTenantId is empty. |

#### Tests for you (E1)
After the bootstrap decision (E1-8) gives you one tenant where you are owner:
1. Workspace_ListMine → 1 item, RoleCode "owner", IsActive True.
2. Workspace_Create("A") → error "Use at least 2 characters"; 81 chars → "Use at most 80 characters".
3. Workspace_Create("  Acme  India Pvt. Ltd!! ") → Tenant Name "Acme  India Pvt. Ltd!!", Slug like `acme-india-pvt-ltd-3f9a1c`; Membership owner/active; one OrganizationSettings row (Timezone UTC, DefaultCurrency INR, all 6 flags False, StandardCreditDays 30); MasterData 11 rows (HoldReason 6, DealSource 5) for that tenant; your UserProfile.ActiveTenantId = new tenant.
4. Create as a member/manager/admin of the active workspace → "Only an admin can create a workspace."
5. Workspace_SetActive(tenant where you have no membership) → "You are not a member of this workspace."; set a membership Status to "suspended" → same error, and it disappears from ListMine.

### E2 (F7) — Notification entity + actions, FileAttachment entity

**Status: PUBLISHED — rev 15** (env_app Development revision = 15, deployment 75ec4108-…, 2026-11-10T19:01Z). 0 errors, 22 pre-existing warnings. One corrective prompt used (entity rename, see E2-1). Verified with context_search: Notification_* service actions Public = Yes; FileAttachment entity with all attributes below.

| Entity | Attribute | Type | Len | M | Default | Delete rule |
|---|---|---|---|---|---|---|
| Notification | TenantId | Tenant Identifier | | Y | | Delete |
| | RecipientUserId | User Identifier | | Y | | Ignore |
| | ActorUserId | User Identifier | | | | Ignore |
| | Type, EntityType | Text | 50 | Type Y | | |
| | EntityId | Long Integer (0 = none) | | | | |
| | Title | Text | 500 | Y | | |
| | Subtitle | Text | 1000 | | | |
| | Metadata | Text (JSON) | 4000 | Y | "{}" | |
| | ReadAt | Date Time | | | | |
| | CreatedAt | Date Time | | Y | (none) | |
| | LegacyUuid | Text | 36 | | | |
| FileAttachment | TenantId | Tenant Identifier | | Y | | Delete |
| | EntityType | Text | 50 | Y | | |
| | EntityId | Long Integer | | Y | | |
| | Category | Text | 50 | Y | "other" | |
| | FileName | Text | 500 | Y | | |
| | ContentType | Text | 250 | Y | "application/octet-stream" | |
| | SizeBytes | Long Integer | | Y | 0 | |
| | StoragePath | Text | 1000 | | | |
| | UploadedBy | User Identifier | | Y | | Ignore |
| | CreatedAt | Date Time | | Y | (none) | |
| | LegacyUuid | Text | 36 | | | |

Indexes: Notification (TenantId, RecipientUserId, CreatedAt), (TenantId, RecipientUserId, ReadAt), (TenantId, EntityType, EntityId), LegacyUuid; FileAttachment (TenantId, EntityType, EntityId, CreatedAt) `IX_FileAttachment_Entity_Cre`, LegacyUuid; plus auto TenantId indexes.

| Element | Type | Public | Inputs | Outputs |
|---|---|---|---|---|
| NotificationItem | Structure | Yes | | Id, Type, EntityType, EntityId, Title, Subtitle, Metadata, ActorUserId, IsRead, ReadAt, CreatedAt |
| Notification_Create | Service action | Yes | RecipientUserIds (comma list), Type, Title, Subtitle, EntityType, EntityId, Metadata | Success, CreatedCount |
| Notification_ListMine | Service action | Yes | UnreadOnly, MaxRecords | Notifications, UnreadCount |
| Notification_MarkRead | Service action | Yes | NotificationId | – |
| Notification_MarkAllRead | Service action | Yes | – | UpdatedCount |

Logic (Mentor node text, checked): Create — ResolveContext gives tenant + actor; splits the comma list, trims, skips empty, skips the actor, skips duplicates, skips ids without an **active membership in the acting tenant**; Type/EntityType lower-case; Title cut 500, Subtitle cut 1000, empty Metadata → "{}". Single AllExceptions handler: log + Success = False, no raise (approved "never fails the caller"). ListMine — cap 1..200 (default 50), newest first, unread filter optional, UnreadCount from a count aggregate. MarkRead — only own, unread, same tenant; not found = silent (source behaviour). MarkAllRead — own unread rows, one Now value. Handlers on ListMine/MarkRead/MarkAllRead: ValidationError / MembershipMissing re-raised; AllExceptions log + FoundationError.

#### Deviations (E2)
| # | Deviation | Handling |
|---|---|---|
| E2-1 | Entity could not be called `Attachment` (ODC has a built-in system structure with that name; Studio reverted the rename and Mentor created `Attachment2`). | Corrective prompt renamed it to **FileAttachment** before publish. CLAUDE.md app map says "Attachment" — I updated the app map note. **Decision for you** if you prefer another name (safe now: nothing references it). |
| E2-2 | Mentor set ALL inputs of Notification_Create (Subtitle, EntityType, EntityId, Metadata) and Notification_ListMine (UnreadOnly, MaxRecords) to **Mandatory = Yes** (spec: optional). Found during post-publish verification; my one corrective prompt was already used. | Not broken: callers just pass "" / 0 / False / 50. Fix later with a small prompt "set these inputs to Is Mandatory = No" (non-breaking change). |
| E2-3 | Source FileUrl column (deal_documents.file_url) is not on FileAttachment (not in your list). Storage decision pending. | Recorded: add FileUrl or derive it from StoragePath when the storage decision is taken. Category default "other" from the source enum. |
| E2-4 | Source mark-read actions require permission `manageDeals` (so viewers cannot clear their own badge). Ours allows any active member to mark **their own** notifications. | Deviation on purpose (source bug). **Decision for you** if you want strict parity. |
| E2-5 | Source does not check that a recipient belongs to the tenant; ours skips non-members. | Stricter on purpose (prevents leaking titles to other tenants). |
| E2-6 | Notification_Create derives tenant + actor from the signed-in user, so **timers / system jobs (task_due_soon, automation_failed) cannot call it** (no user context → Success = False, logged). | Pending: a system variant (e.g. Notification_CreateForTenant, callable only from server timers) when the first timer needs it. |
| E2-7 | Notification Type is not validated against LookupValue NotificationType. | Pending (cheap to add). Source types: task_assigned, task_completed, task_due_soon, deal_won, deal_lost, owner_changed, comment_mention, automation_failed. |
| E2-8 | Realtime publish (source publishRealtimeEvent) not done. | Pending (RealtimeEvent entity, F-later). |

#### Tests for you (E2)
With two users A and B, both active members of the same tenant, signed in as A:
1. Notification_Create("B-id, B-id ,A-id,,unknown", "task_assigned", "Test", "", "", 0, "") → Success True, CreatedCount 1 (only B; duplicate, actor, empty and non-member skipped).
2. Sign in as B: ListMine(False, 50) → 1 item, IsRead False, UnreadCount 1. MarkRead(id) → UnreadCount 0. MarkRead of A's notification id → no change, no error.
3. MarkAllRead → UpdatedCount = number of unread rows; second call → 0.
4. ListMine(…, MaxRecords 0) returns up to 50; 500 → up to 200.

---

## Batch F (F6 partial) — Member_ChangeRole, Member_Remove

**Status: PUBLISHED — rev 16** (env_app Development revision = 16, deployment b62dce46-…, 2026-11-10T19:09Z). 0 errors, 22 pre-existing warnings. Verified with context_search: both service actions Public = Yes with the inputs below.

| Element | Type | Public | Inputs | Outputs |
|---|---|---|---|---|
| Foundation_CheckPermission | Server action (private copy of HasPermission logic, for use inside Foundation) | No | RoleCode, PermissionCode | HasPermission |
| Member_ChangeRole | Service action | Yes | TargetUserId (User Id, M), NewRole (Text, M) | – |
| Member_Remove | Service action | Yes | TargetUserId (User Id, M) | – |
| (system User entity reference) | added by Mentor | | | |

Rules implemented (Mentor node text, checked), in order:
- **ChangeRole**: ResolveContext → permission `manageRoles` (owner only) else `Permission denied: requires "manageRoles" (current role: X)` (exact source text) → role ∈ admin/manager/member else "Pick a role" → not yourself "You cannot change your own role." → target must be a member "This person is not a member of this workspace." (**fix #3**: error instead of the source's silent success) → target owner → "An owner's role cannot be changed." (**fix #4**, server-side) → last admin/owner demotion → "Cannot change the role of the last admin or owner. Promote another member first." (**fix #3**) → same role = no-op → update Role + UpdatedAt.
- **Remove**: ResolveContext → permission `removeMembers` (owner, admin) → not yourself "Use the workspace switcher to leave the workspace instead." → target must be a member → admin/owner target needs an owner "Only an owner can remove an admin or owner." → never the last admin/owner "Cannot remove the last admin or owner. Promote another member first." → delete the Membership only (records keep their owner id; the removed person's next call gets MembershipMissing → switch-workspace recovery).
- Handlers: ValidationError / MembershipMissing re-raised; AllExceptions log + FoundationError.

#### Deviations (F)
| # | Deviation | Handling |
|---|---|---|
| F1 | ActivityLog `role_changed` / `member_removed` and realtime events not written (ActivityLog belongs to Salenix_Sales, not built yet). | Comment-node hooks in both actions. Pending. |
| F2 | The last-admin/owner count includes memberships of any Status (same as source). | Parity kept. Decide later whether suspended admins should count. |
| F3 | Target not found returns an error (source: silent no-op on change role; remove deletes 0 rows). | Fix per BRD #3 spirit. |
| F4 | Because owners can't be changed and only owners hold manageRoles, the "last admin" guard in ChangeRole can only trigger if the tenant has no owner row (data issue). Kept as defence. | Recorded. |
| F5 | Invitations (FR-TEAM-02/03/04) not built — needs SHA-256 hashing (C# library) and email sending. | **Pending** as instructed. |

#### Tests for you (F)
Tenant with: you (owner O), admin A, manager M, member U.
1. As O: ChangeRole(U, "Manager ") → U role manager. ChangeRole(U, "viewer") → "Pick a role". ChangeRole(O, "admin") → "You cannot change your own role." ChangeRole(<second owner>, "admin") → "An owner's role cannot be changed." ChangeRole(<user of another tenant>, "member") → "This person is not a member of this workspace."
2. As A (admin): ChangeRole(U, "member") → `Permission denied: requires "manageRoles" (current role: admin)`.
3. As A: Remove(U) → OK, U's membership gone, U's next GetMyContext → "You are no longer a member…". Remove(M) as A → OK. Remove(<other admin>) as A → "Only an owner can remove an admin or owner." Remove(A) as A → "Use the workspace switcher…".
4. As O with O the only admin/owner and A demoted: Remove(O) is blocked by "yourself"; set up a tenant with one admin + one owner, as owner remove the admin → OK; then try removing the last owner from another owner account → "Cannot remove the last admin or owner…".
5. As M or U: Remove(any) → `Permission denied: requires "removeMembers" (current role: manager)`.

---

## Summary
| Batch | Revision | Status |
|---|---|---|
| B (yesterday) | 11 | Marked DONE in CLAUDE.md (your test passed) |
| C (F3) | 12 | Published + verified |
| D (F4) | 13 | Published + verified (TodayFor pending) |
| E1 (F5) | 14 | Published + verified |
| E2 (F7) | 15 | Published + verified (1 corrective prompt: Attachment2 → FileAttachment) |
| F (F6 partial) | 16 | Published + verified (invitations pending) |

Nobody else published during the run (each revision was the one I expected before every publish). No Production, GL_*, Salenix_Theme or Setting was touched. Nothing existing was deleted or renamed. All Mentor sessions closed after their publish succeeded.

**Before any test:** open Salenix_Foundation fresh in ODC Studio (do not publish from an old Studio copy). Check delete rules and indexes of OrganizationSettings, CustomFieldDefinition, Notification, FileAttachment.

## Decisions waiting for you
1. **Bootstrap of the first workspace (blocks all tests of D/E/F).** Development has no Tenant/Membership. Workspace_Create is owner-only and invite-only (source rule), so no one can create the first one. Option (a) one-time admin-only `Bootstrap_FirstWorkspace` action that works only while Tenant is empty, or (b) insert the first Tenant + owner Membership by hand. (E1-8)
2. **Default timezone/locale for new workspaces**: entity defaults follow the source (Timezone UTC, Locale/CurrencyLocale en-US); CLAUDE.md decision 7 says Asia/Kolkata. Keep source defaults, or set Asia/Kolkata + en-IN in Workspace_Create? (C3)
3. **Consumer detection of MembershipMissing**: ODC user exceptions cannot be public, so other apps only see the message text. OK, or add a non-raising `Foundation_TryGetMyContext` (IsValid + Message)? (D2)
4. **Workspace_Create message** "Only an admin can create a workspace." (source text, but the rule is owner-only) — change to "Only an owner…"? (E1-1)
5. **Default owner info@cgbindia.com** on every new workspace (BRD gap #28) — still undecided; not built. (E1-5)
6. **Entity name FileAttachment** (Attachment is reserved by ODC) — keep? (E2-1)
7. **Notification mark-read permission**: source requires manageDeals (viewers can't clear their badge); I allowed any member for their own notifications. OK? (E2-4)
8. **Make the optional Notification inputs non-mandatory** (Mentor set them mandatory) — small follow-up prompt; approve? (E2-2)
9. **Storage decision for FileAttachment** (provider, FileUrl column or derive from StoragePath). (E2-3)
10. Pending builds that need libraries/setup: Foundation_TodayFor (C# timezone library), invitations (SHA-256 hashing + email), Notification system variant for timers, ActivityLog hooks (Salenix_Sales), offerings seed hook (Salenix_Sales), DefaultDealStage "hold" rejection + other CHECK rules in the settings save actions (Salenix_Admin).

## Follow-up G (2026-11-11 morning) — PUBLISHED rev 17
Approved follow-ups applied in one Mentor session, then 3 corrections in the same session, published, verified (env_app revision 17; context shows Workspace_Provision private, Workspace_CreateFirst public, Notification_Create optional inputs Is Mandatory = No). Closes decisions 1, 2, 3, 4, 5, 6, 7, 8 above.
- Notification optional inputs (defaults "", "", 0, "{}", False, 50).
- Exception codes MEMBERSHIP_MISSING: / VALIDATION_ERROR: / FOUNDATION_ERROR: on every raise of the listed actions.
- Workspace_Create message "Only a workspace owner can create a new workspace."; logic moved to private Workspace_Provision; new workspaces Timezone = Asia/Kolkata; optional default owner from Setting DefaultWorkspaceOwnerEmail (empty).
- Workspace_CreateFirst + secret Setting BootstrapOwnerEmail (empty).
- Last admin/owner count: Status = "active" only (deliberate source-bug fix).
- Log Error = False on the 14 AllExceptions handlers with LogMessage.
- 11 unused local variables removed from Workspace_Create.
Read-only review of GetMyContext, ResolveContext, HasPermission, CheckPermission, Member_ChangeRole, Member_Remove against FR-TEAM-05/06 and BRD #3/#4: all rules present, no bypass of the permission check; only gap = ActivityLog hooks (Salenix_Sales).

## DevTools tests (2026-11-11) — PASSED, Batches D, E, F tested
- Dylan (member): bootstrap refused; MEMBERSHIP_MISSING before SetActive; persona sales after SetActive; Member_ChangeRole / Member_Remove on the owner refused (permission denied).
- Owner: CreateFirst OK; Workspace_Create OK (new workspace becomes active); ChangeRole Dylan → manager OK; owner role refused ("Pick a role"); own role change / own removal refused; Remove Dylan OK.
- After removal Dylan gets "MEMBERSHIP_MISSING: You are no longer a member of this workspace" from GetMyContext and Notification_ListMine.

## Salenix_Crypto smoke test (2026-11-11) — PASSED
DevTools rev 8, TestConsole section "Crypto smoke test", run by the user in ODC Development:
- ConvertUtcToZone(now, "Asia/Kolkata") = UTC + 5:30 exactly (ODC does not re-shift the value)
- TodayIn("Asia/Kolkata") = 2026-11-11 (Date, not shifted)
- Sha256Hex as expected; NewToken(32) = 43 chars
- TrigramSimilarity 0.45454547, WordSimilarity 0.625, NormalizeCompanyName → [acme]
