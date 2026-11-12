# Overnight N — source semantics (N3–N9)

Read from `source/src` on 2026-11-13. All paths are relative to `source/src/`. "file:line" = line in that file.
Common facts used below:
- Persona: `personaOf(role)` → owner/admin = `admin`, manager = `manager`, member/viewer/none = `sales` (lib/personas/index.ts:22-30).
- Sales scope on list pages = `(owner_id = me OR owner_id IS NULL)` added as a non-overridable WHERE (deals app/dashboard/deals/page.tsx:52-54, board app/dashboard/deals/board/page.tsx:27-28, companies app/dashboard/companies/page.tsx:42-43, contacts app/dashboard/contacts/page.tsx:38-39; tasks use `assigned_user_id`, app/dashboard/tasks/page.tsx:38-40).
- Page sizes 25 / 50 / 100, default **50**; URL `size`, invalid → 50 (lib/pagination/page-size.ts). Keyset cursor `cursor` = base64url JSON `{ts, id, n?}`; queries fetch `size + 1` rows to detect a next page (lib/pagination/cursor.ts).
- Permission error text (every `checkPermission` failure): `Permission denied: requires "<permission>" (current role: <role|none>)` (lib/auth-guard.ts:22).
- RBAC used here (lib/rbac.ts:25-120): manageDeals / manageCompanies / manageContacts = owner, admin, manager, member; deleteDeals/Companies/Contacts = owner, admin; approveDeals = owner, admin, manager; **assignDeals = owner, manager** (source; CLAUDE.md decision 6 widened it); manageOfferings = owner, admin; viewer has no write permission.
- "Now"/"today" in the source = DB `NOW()` / `CURRENT_DATE` (server UTC), not the workspace timezone — our rebuild uses Foundation_TodayFor (decision 7), a deliberate difference.

---

## N3 List filters

### N3a Deals list (lib/deals/filters.ts, lib/deals/queries.ts `listDeals` 367-560)

Base WHERE: `tenant_id = T AND deleted_at IS NULL` (queries.ts:375-378). Search `q`: `name ILIKE '%term%'` (term trimmed, no escaping of % / _) (queries.ts:379). Groups are ANDed; values inside a group are ORed (`IN`).

| URL param | Parsing (filters.ts) | SQL (queries.ts) |
|---|---|---|
| `stage` (alias `stages`) | comma list; unknown codes dropped; empty → no filter (156-164) | `stage IN (...)` (387-389) |
| `amountMin` / `amountMax` | `Number(v)`, finite and **>= 0**, else ignored (166-170). Unit = **amount_minor** (minor units) (filters.ts header 13-14) | `amount_minor >= min` / `<= max` — **inclusive** (390-395) |
| `closeFrom` / `closeTo` | must match `YYYY-MM-DD` (171-174) | `expected_close_date >= from` / `<= to` — **inclusive**; NULL dates never match (396-401) |
| `stale=1` (`1` or `true`) + `staleDays` | staleDays = `Number(v) || undefined` (203-206) | `stage NOT IN ('won','lost') AND updated_at < NOW() - days` ; days = staleDays if > 0 else **14** (402-408). Column = **deals.updated_at**. Hold counts as open. Serializer omits staleDays when 14 (275) |
| `companyIds` (legacy `companyId`) | comma list or repeated param (180-195). Values are company **names**, not ids | `lower(deals.company_name) IN (lowered names) OR company_id IN (SELECT id FROM companies WHERE tenant=T AND lower(name) IN (...))` (companyFilterCondition 306-321). Options list = distinct `coalesce(deal.company_name, company.name)` of non-deleted deals, deduped case-insensitively, sorted (329-351) |
| `mine=1` | flag | `owner_id = currentUser` (412-414) |
| `unassigned=1` | flag | `owner_id IS NULL` (415-417) |
| `owner` | comma list of user ids | `owner_id IN (...)` (418-420) |
| `approval=pending` | → awaitingApproval (211) | `stage='won' AND approval_status='pending'` (421-426) |
| `approval=approved` | → approvedOnly | `stage='won' AND approval_status='approved'` (427-432) |
| `score` | only `strong` / `moderate` / `at_risk` (215-220) | `health_band = score` (433-435) |
| `revisitDue=1` | flag | `stage='hold' AND hold_revisit_on IS NOT NULL AND hold_revisit_on <= CURRENT_DATE` (436-440) — includes today |
| `slipped=1` | flag | `push_count >= 1` (441-443) |
| `missing=<key>` | must be a DEAL_FIELD_SPECS key: company, contact, contact_email, amount, margin, value_split, close_date, deal_type, source, next_step, description (lib/deals/completeness.ts) | "owes the field at its stage and it is empty": `(applies) AND NOT (filled)`; applies = TRUE when requiredFromRank <= 1, else `COALESCE(deal_stage_rank(stage),1) >= rank` (hold/lost rank 1) (completeness.ts:316-335; queries.ts:448-451) |
| `risk=elevated|high` | (228-229) | joined `deal_risk_scores.score >= 30` (elevated) or `>= 60` (high) (RISK_ELEVATED_FROM / RISK_HIGH_FROM, lib/deals/risk/score.ts:111-113; queries.ts:501-504). Unscored deals never match |
| `offering` | comma list | EXISTS deal_offerings with offering_id IN (...) (any tag, not only primary) (460-473) |
| `uncategorized=1` | flag | NOT EXISTS any deal_offerings row (478-485) |
| `po=missing` | `raw.po === 'missing'` (232) | `stage='won' AND NOT EXISTS (SELECT 1 FROM deal_order_proofs p WHERE p.deal_id = deal GROUP BY p.deal_id HAVING bool_and(p.currency = deals.currency) AND abs(sum(p.subtotal_minor) - deals.amount_minor) <= 100)` (488-498; ORDER_PROOF_ROUNDING_MINOR = 100, lib/order-proofs/shared.ts:14). "Valid" = **every** proof in the deal currency AND sum of pre-tax `subtotal_minor` within ±100 minor units of the deal amount. A won deal with no proofs = missing |

- Offering filters are dropped when the workspace has `offeringsEnabled` off (`withoutOfferingFilters`, filters.ts; app/dashboard/deals/page.tsx:40-43).
- Sales scope: `(owner_id = me OR owner_id IS NULL)` (queries.ts:507-511).
- **Sort** (URL `sort`): only `recent` (default) and `risk` (app/dashboard/deals/page.tsx:46; queries.ts:104-114).
  - recent: `ORDER BY created_at DESC, id DESC`, cursor `(created_at,id) < (ts,id)` (523-525, 546-549).
  - risk: `ORDER BY deal_risk_scores.score DESC, id DESC`, and **unscored deals are excluded** (`score IS NOT NULL`) (533-535).
  - No user-selectable direction. (Our Deal_List SortBy codes updated/value/close/created/name are a rebuild choice, not source.)
- `hasActiveDealFilters` does not count `po` (filters.ts:331-353) — UI detail only.
- Related lists: Approvals queue = won + pending, `ORDER BY closed_at DESC`, LIMIT **200** (queries.ts:590-612); recently approved = won + approved, `approved_at DESC`, LIMIT 50 (619-642); recently rejected = `approval_status='rejected'` (any stage), `approved_at DESC`, LIMIT 50 (650-672); deleted deals `deleted_at DESC, id DESC` keyset (674-708).

### N3b Companies list (lib/companies/filters.ts, lib/companies/queries.ts `listCompanies` 146-330)

Base WHERE: `tenant_id = T AND deleted_at IS NULL AND merged_into_id IS NULL`; search `name ILIKE '%term%'` (queries.ts:156-161). All rollup values come from the view `company_rollups` (drizzle/0065_company_rollups.sql), LEFT JOINed per company:
- deal_count = non-deleted deals of the company (any stage); open_deal_count = stage NOT IN (won, lost) (hold = open); won_deal_count; open_pipeline_value = sum(amount_minor) open; won_lifetime_value = sum(amount_minor) won (all currencies summed); top_stage = stage of the most recently updated open deal; contact_count = non-deleted, non-merged contacts.
- **last_activity_at = GREATEST(max(activity_logs.created_at where entity_type='company' and entity_id=company), max(notes.created_at where entity_type='company', not deleted), max(deals.updated_at of its non-deleted deals))**. GREATEST ignores NULLs; all NULL → NULL. Contact activity does NOT count.

| URL param | Parsing | SQL |
|---|---|---|
| `hasDeals=1` | flag (`1`/`true`) | `open_deal_count > 0` (166-168) — "at least one OPEN deal" |
| `recentActivityDays=N` | `Number`, finite and **> 0** (filters.ts:70-76) | `COALESCE(last_activity_at, epoch) >= NOW() - N days` (169-173) |
| `createdWithinDays=N` | > 0 | `companies.created_at >= NOW() - N days` (174-178) |
| `stale=1` + `staleDays` | default **30** (DEFAULT_STALE_DAYS, filters.ts:151) | `COALESCE(last_activity_at, epoch) < NOW() - days` (183-188) → never-touched companies ARE stale (epoch sentinel) |
| `minOpen=X` | > 0, **major** units | `open_pipeline_value >= round(X*100)` (189-193) |
| `minWon=X` | > 0, major units | `won_lifetime_value >= round(X*100)` (194-198) |
| `mine=1` / `unassigned=1` | flag | `owner_id = me` / `owner_id IS NULL` (199-204) |

- Sales scope `(owner_id = me OR owner_id IS NULL)` (205-210).
- Sort `sort` ∈ recent (default) | deals | pipeline | won | activity; `dir` = `asc` only if exactly `asc`, else **desc** (filters.ts:53-60, 102-111). Keys: recent = created_at; deals = deal_count; pipeline = open_pipeline_value; won = won_lifetime_value; activity = COALESCE(last_activity_at, epoch). `ORDER BY key dir, id dir` (queries.ts:216-225, 299).
- Company options for pickers: non-deleted, non-merged, `ORDER BY name ASC` (41-55); scoped variant adds owner = me OR null (62-85).

### N3c Contacts list (lib/contacts/filters.ts, lib/contacts/queries.ts `listContacts`)

Base WHERE: `tenant_id = T AND deleted_at IS NULL AND merged_into_id IS NULL`. Search: `first_name ILIKE p OR last_name ILIKE p OR email ILIKE p OR title ILIKE p`, p = `%term%`.

| URL param | Parsing | SQL |
|---|---|---|
| `companyId=<uuid>` | non-empty string | `company_id = X` |
| `noCompany=1` | flag | `company_id IS NULL` |
| `recentActivityDays=N` | > 0 | EXISTS activity_logs with `entity_type='contact' AND entity_id = contact.id AND created_at >= NOW() - N days` — any action. Notes and comments on a contact count, because note/comment create writes an activity row on the contact (see N5). Linked deals and emails do NOT count |
| `createdWithinDays=N` | > 0 | `created_at >= NOW() - N days` |
| `mine=1` / `unassigned=1` | flag | `owner_id = me` / `owner_id IS NULL` |

- Sales scope `(owner_id = me OR owner_id IS NULL)`. Sort fixed: `created_at DESC, id DESC` (no sort param). No "stale" filter for contacts.
- Picker options: `ORDER BY coalesce(last_name, first_name, email) ASC`; label = "First Last" or email or `(unnamed)`.

---

## N4 Tasks (db/schema/tasks.ts, lib/tasks/*, app/dashboard/tasks/actions.ts, comments-actions.ts, page.tsx, _components)

### Data (db/schema/tasks.ts; enums db/schema/enums.ts:41-67)
| Column | Rule |
|---|---|
| title | text NOT NULL |
| description | text null |
| status | enum `todo` (default), `in_progress`, `completed`, `cancelled` |
| priority | enum `low`, `medium` (default), `high`, `urgent` |
| type | enum `call`, `email`, `meeting`, `internal`, `followup`; null = generic |
| recurrence | enum `daily`, `weekly`, `custom`; null = one-off |
| stage_blocking | deal stage enum, null |
| due_date | date null |
| assigned_user_id | text null (null = unassigned) |
| created_by | text NOT NULL |
| completed_at | timestamptz null |
| entity_type / entity_id | `deal` / `company` / `contact` / null; no FK |
| deleted_at / deleted_by | soft delete |

### Permissions and validation
- **Every task action uses `manageDeals`** (TASK_PERMISSION, actions.ts:104; comments-actions.ts:16) → owner/admin/manager/member; viewer cannot write. **No ownership check**: any member with manageDeals may edit, complete, reopen or delete ANY task in the tenant (actions.ts:334-846). The UI shows edit controls when `canManageDeals(role)` (tasks-view.tsx:71).
- Create schema (actions.ts:106-122): title `min 1 'Title is required'`, **max 280**; description max **4000**; status default `todo`; priority default `medium`; type/recurrence/stageBlocking optional enums; dueDate regex `^\d{4}-\d{2}-\d{2}$` message `Use YYYY-MM-DD`; assignedUserId string max 120 (not checked against members); entityType enum deal/company/contact; entityId uuid. Entity existence is not checked. Blank form fields → null (parseFormData 138-160). Validation failure message = first form error or `Please fix the highlighted fields.` + fieldErrors.
- Create (213-332): insert with `created_by = actor`; activity `entity_type='task', action='created'`, metadata {taskTitle, taskPriority, dueDate, linkedEntityType, linkedEntityId}. If assignee ≠ actor: notification type `task_assigned`, title `New task: <title>`, subtitle `Due <date>` or null; plus automated email (only when linked to deal/company/contact) subject `Task assigned to you: <title>`. Queues deal re-score when linked to a deal.
- Full update `updateTaskAction` (334-490): same schema + id. Updates **only** title, description, status, priority, due_date, assigned_user_id, entity_type, entity_id (**type, recurrence, stage_blocking are NOT updated** — source quirk). Not found → throws `Task not found`. completed_at = now if status becomes completed (was not), else keeps the previous value (does not clear on reopen). Activity action `task_completed` if just completed else `updated` (metadata taskTitle, previousStatus, status, linked*). If just completed → spawn recurrence. Reassigned to someone ≠ actor → notification `task_assigned` title `Reassigned to you: <title>` + email `Task reassigned to you: <title>`. Just completed and creator ≠ actor → notification `task_completed` title `Task completed: <title>` to the creator.
- Toggle complete / reopen `toggleTaskCompletedAction` (502-613): no-op if already in the target state. Complete → status `completed`, completed_at now; **reopen → status `todo`** (not the previous status), completed_at null. Activity `task_completed` / `updated`. Complete → recurrence spawn + creator notification as above. Bad payload → `Invalid toggle payload`.
- Inline patch `updateTaskFieldsAction` (622-803): optional title (1-280), description (≤4000), priority, status, assignedUserId (≤120), dueDate. Status change sets completed_at = now if `completed` else **null**. Nothing given → `Nothing to update`; bad payload → `Invalid update payload`; not found → `Task not found`. Activity `updated` with metadata fields[] and from/to for priority, status, assignedTo. Recurrence spawn when status changes to completed. Reassign notification title `Task assigned: <title>`.
- Delete `softDeleteTaskAction` (805-846): **soft delete** (deleted_at, deleted_by); silently ok if already deleted; activity `deleted` {taskTitle}. Bad id → `Invalid id`.

### Recurrence (actions.ts:84-96, 171-211)
On every transition into `completed` (full update, toggle, inline): if recurrence set, insert a NEW task copying title, description, priority, type, recurrence, stage_blocking, assignee, entity link; status `todo`; created_by = actor; due_date = completed task's due_date (or today UTC when none) + 1 day (daily), + 7 days (weekly), **+ 1 calendar month (custom)** (the schema comment says +30d; the code adds a month). Activity `created` on the new task with metadata recurredFrom. The completed task keeps its recurrence value.

### stage_blocking
Stored and displayed only ("Blocks <stage>" chip, entity-tasks-panel.tsx:640-642). **Not enforced anywhere** — stage gates read completed task TYPES (`task_complete` gate: completed, non-deleted tasks of the deal, lib/stage-gates/evaluate.ts:228-245), never stage_blocking.

### Lists
- `listTasks` (lib/tasks/queries.ts:63-215): base `tenant_id = T AND deleted_at IS NULL`; search `title ILIKE '%term%'`; sort `created_at DESC, id DESC`; page size default 50. linkedName = deal name / company name / "First Last" or email of contact.
- URL filters (lib/tasks/filters.ts): `status` comma list, `priority` comma list, `assignedToMe=1` (assigned_user_id = me), `unassigned=1` (IS NULL), `dueWithinDays=N` (N > 0: `due_date IS NOT NULL AND due_date <= CURRENT_DATE + N` — includes overdue and completed), `overdue=1` (`due_date < CURRENT_DATE AND status NOT IN ('completed','cancelled')`) (queries.ts:79-109).
- Sales scope: `(assigned_user_id = me OR assigned_user_id IS NULL)`.
- Toolbar chips (tasks-view.tsx:258-268): My tasks (assignedToMe), Overdue, Due this week (dueWithinDays=7), To do (status=todo), In progress (status=in_progress), High / urgent (priority=urgent,high), Unassigned.
- UI grouping: active (status ≠ completed, includes cancelled) above, completed in a collapsed section (tasks-view.tsx:309-345). Due label (client local date): `<n>d overdue` if past, `Due today`, `Due tomorrow`, `Due in <n>d` up to 7, else date (task-row.tsx:35-53).
- Entity panel `listTasksForEntity`: entity_type + entity_id, not deleted, `created_at DESC`, LIMIT 50 (queries.ts:222-244).
- Home "My tasks" summary (264-340): assigned to me, not deleted: openCount = status not completed/cancelled; overdueCount = open AND due_date < CURRENT_DATE; completedThisWeek = completed AND completed_at >= NOW() - 7 days; upcoming = top **5** open tasks ordered `(due_date IS NULL) ASC, due_date ASC, priority urgent→high→medium→low`.

### Task comments (separate table task_comments; app/dashboard/tasks/comments-actions.ts)
- Permission manageDeals for load/create/edit/delete. Body trimmed, min 1 (`Comment cannot be empty`), **max 4000**. Parent task must exist in tenant (deleted tasks are not excluded) else `Task not found`.
- Create: insert author = actor; activity on the TASK: action `updated`, metadata {kind:'task_comment', commentId, commentPreview = first 80 chars, taskTitle}. No mentions, no notifications.
- Edit: only the author (`Comment not found or not yours`); sets body, edited_at, updated_at; no activity.
- Delete: only the author; **hard delete**; no activity.
- List: `created_at ASC`, LIMIT 200 (lib/task-comments/queries.ts).

---

## N5 Notes, comments, timeline

### Notes (app/dashboard/notes/actions.ts, lib/notes/queries.ts)
- Entity types: `deal`, `company`, `contact` (actions.ts:15). Create permission by type: deal → manageDeals, company → manageCompanies, contact → manageContacts (17-21).
- Create: body min 1 (`Note can’t be empty`), **max 8000**, stored trimmed; entityId uuid (existence not checked) (28-32, 67-78). Activity on the PARENT entity: action **`updated`**, metadata {noteId, notePreview = first 120 chars of body, fields:['notes']} (82-93).
- Update: gate manageDeals ("weakest permission", 114); body 1-8000; note must exist and not be deleted (`Note not found.`); **author only** (`You can only edit your own notes.`) (136-138). No activity, no edited flag.
- Delete: gate manageDeals; missing/already deleted → ok (idempotent); **author only** (`You can only delete your own notes.`); **soft delete** (deleted_at, deleted_by); no activity (156-198).
- List per entity: not deleted, `created_at DESC`, LIMIT 100 (lib/notes/queries.ts).

### Comments (app/dashboard/comments/actions.ts, lib/comments/*)
- Entity types deal / company / contact; create permission per type as notes (21-28). Body min 1 (`Comment can’t be empty`), **max 8000**, stored trimmed (36-41).
- Threads: `parentCommentId` optional; **one level only**: if the parent is itself a reply, the new comment attaches to the parent's parent; if the parent is missing/deleted → top-level (87-109). Parent entity is not cross-checked.
- Mentions: token format `@[Display Name](userId)` (lib/comments/mentions.ts:1, formatMentionToken). mentioned_user_ids = distinct ids parsed from the body (81, 120). After commit: notification type `comment_mention`, title `Mentioned you in a comment`, subtitle = preview (mentions rendered as `@Name`, whitespace collapsed, max 80 chars with `…`), entity = the commented entity, metadata {commentId}; recipients deduped, **actor excluded** (lib/notifications/recorder.ts:123-137). Mentioned ids are not checked against membership.
- Activity on the parent entity: action `updated`, metadata {commentId, commentPreview (80), kind:'comment'} (129-140). Realtime event `comment.created`.
- Edit: gate manageDeals; **author only** (`You can only edit your own comments.`; missing → `Comment not found`); re-parses mentions, sets edited_at; **no new notifications**, no activity (192-248).
- Delete: gate manageDeals; idempotent; **author only** (`You can only delete your own comments.`); soft delete; no activity (250-295). Managers/admins cannot delete others' comments or notes.
- List per entity: not deleted, `created_at ASC`, LIMIT 200 (lib/comments/queries.ts).

### Timeline (lib/timeline/queries.ts)
- `getEntityTimeline(entityType, entityId, limit)` (64-172): four reads for the same (entity_type, entity_id), each `created_at DESC LIMIT limit`: activity_logs (all actions, no filter), notes (not deleted), comments (not deleted; replies included), email_messages. Activity rows whose metadata has `commentId`, `noteId` or `emailId` are skipped (the note/comment/email item already shows it) (135-136). Merge, sort by created_at DESC, cut to `limit`. Item kinds: activity / note / comment / email.
- Used with **limit 150** on deal detail (app/dashboard/deals/[id]/page.tsx:153) and contact detail (app/dashboard/contacts/[id]/page.tsx:56).
- Company ("account") timeline `getAccountTimeline` (208-388), limit **150** (ACCOUNT_TIMELINE_LIMIT, lib/companies/account-360.ts:42): child set = up to **500** non-deleted deals with company_id = company (`updated_at DESC`) and up to 500 non-deleted, non-merged contacts of the company. Each of the 4 sources reads rows where (type='company' AND id=company) OR (type='deal' AND id IN deals) OR (type='contact' AND id IN contacts), `created_at DESC LIMIT limit`; same skip rule; each item carries `source` {type deal|contact, id, name} for child rows. Merge, sort DESC, cut.
- Task activity (entity_type 'task') is never on these timelines.
- UI filter chips (app/dashboard/_components/entity-timeline.tsx:45-216): All, Mine, Teammates (actor ≠ me, not null), Emails, Comments, Notes, Activity (activity with an actor and action ≠ automation_note), Automation (activity with actor NULL or action `automation_note`); deal detail uses All, Emails, Calls (`call_logged`), Meetings (`meeting_logged`), Stage changes (`stage_changed`), Notes (DEAL_ACTIVITY_FILTERS 84-91).
- Activity actions that exist (db/schema/activity-logs.ts): created, updated, deleted, restored, stage_changed, invited, invitation_revoked, member_removed, role_changed, automation_note, task_completed, owner_changed, email_sent, email_failed, deal_approved, deal_rejected, call_logged, meeting_logged, gate_warning, email_synced, merged, dedup_override, value_split_changed. Entity types: company, contact, deal, membership, invitation, task, note, email.
- Generic activity feed `listActivityForEntity`: LIMIT 50 default, `created_at DESC` (lib/activity/queries.ts:31, 164-185).

---

## N6 Admin home KPIs (app/dashboard/page.tsx, _components/home/admin-home.tsx, _components/kpi-strip.tsx, lib/dashboard/*)

### Routing, scope, range
- `/dashboard` renders AdminHome for persona admin (owner/admin), ManagerHome for manager, SalesHome otherwise (app/dashboard/page.tsx:34-66).
- Admin/manager `view` param: `team` (default) or `rep:<userId>` (userId ≤ 128 chars), anything else → team (lib/dashboard/view-scope.ts). rep → every KPI query adds `owner_id = userId`.
- Range (lib/dashboard/range.ts): `range` ∈ last_7d | last_30d | last_90d (**default**) | this_quarter | this_year, or `range=custom&from=YYYY-MM-DD&to=YYYY-MM-DD` (to inclusive, to ≥ from, span capped at 730 days). Presets in UTC (lib/reports/filters.ts:46-82): last_Nd = [today 00:00 UTC − N days, tomorrow 00:00) (so last_7d spans 8 calendar days); this_quarter = [quarter start, next quarter start); this_year = [Jan 1, next Jan 1). `days` = round(span/1 day); prior window = same length immediately before `from`.

### KPI formulas (lib/dashboard/kpis.ts)
| KPI | Formula |
|---|---|
| Total pipeline (value) | Live snapshot NOW: sum amount_minor of non-deleted deals with stage **NOT IN (won, lost)** — hold included — created ≤ now and (closed_at NULL or > now), optional owner (107-152). Stage at time `at` = deal_stage_history row with entered_at ≤ at < exited_at, else current stage |
| Total pipeline (prior) | Same snapshot evaluated at `range.from` (335) |
| Weighted pipeline | Σ per stage (stage total × stage probability / 100), rounded (145-151). Probability = **stage table only** (deal.probability is NOT used): defaults lead 10, qualified 30, proposal 60, negotiation 80, hold 40, won 100, lost 0; lead…hold overridable in `organization_settings.stage_probabilities` (values 0–100, else ignored); won/lost fixed (lib/deals/stage-probability.ts) |
| Win rate | won / (won + lost) for deals with `closed_at` in [from, to), stage IN (won, lost), not deleted; 0 when none (162-210, 342-343). UI shows `—` and "no deals closed in period" when won+lost = 0 (kpi-strip.tsx:170-190) |
| Avg deal size | won amount sum / won count in the window, rounded; won only (344-345). `—` when no wins |
| Avg sales cycle | AVG((closed_at − created_at) in days) over **won** deals in the window with closed_at > created_at; rounded to 1 decimal (182-186, 208). `—` when 0; lower is better |
| Won / Lost counts | from the same window |
| Quota attainment (tile only when the FY target > 0) | Fiscal year = current FY from `fiscalYearStartMonth`; window [FY start, now). achieved = Σ ROUND(amount_minor × quota_weight) of won deals with approval_status IN ('none','approved'); prior achieved = same with closed_at < range.from; pending = Σ amount_minor of won deals with approval_status IN ('pending','rejected') + count; deweighted loss = Σ (amount − ROUND(amount × quota_weight)) where quota_weight < 1.00 (239-307; lib/targets/quota-credit.ts:46-107). Target = Σ revenue_target_minor of the FY targets (filtered to the rep in rep view) (349-351). Attainment % = achieved/target, display capped at PCT_DISPLAY_CAP |
| Coverage ratio | Shown when a target exists: gap = max(0, target − achieved); gap 0 → "Met"; ratio = open pipeline now / gap; band healthy ≥ 3x, thin ≥ 2x, else at-risk (lib/dashboard/coverage.ts) |

Deltas: value vs prior; win rate and quota as percentage points; cycle "higher is worse" (kpi-strip.tsx).

### Other admin-home blocks
- Record counts (lib/dashboard/queries.ts:50-70), **tenant-wide, ignore rep view and range**: companies = non-deleted (merged rows are NOT excluded), contacts = non-deleted (merged not excluded), active deals = non-deleted with stage NOT IN (won, lost).
- Revenue trend (getRevenueTrend 260-330): days = range.days; daily series from CURRENT_DATE − (days−1) to CURRENT_DATE: won_revenue/won_deals by `closed_at::date` (stage won), lost by closed_at (stage lost), new_pipeline/new_deals by `created_at::date` (any stage), optional owner. Shaped into **ISO weeks (Monday, UTC)** by lib/dashboard/trend-shaping.ts (axis capped at the 95th percentile).
- Stage bars + donut: pipelineByStageRunner = count and sum(amount_minor) per stage of ALL non-deleted deals (all 7 stages, **all time, ignores the date range**), optional owner (lib/reports/queries/pipeline-by-stage.ts).
- Deal highlights (lib/dashboard/highlights.ts:84-320), 5 rows each (limit+1 probe): Top wins = won in [from,to) by amount DESC; Progress = stage-history moves entered in window (from_stage not null), newest first; New deals = created in window, newest first; **At risk = open deals (not won/lost) whose last touch = GREATEST(updated_at, last deal activity, last deal note) is older than 14 days**, oldest first; Closing soon = open deals with expected_close_date between CURRENT_DATE and CURRENT_DATE + 14, by date ASC then amount DESC.

---

## N7 Pipeline board (lib/deals/queries.ts:710-1054, board-view.tsx, board-column.tsx, deal-card.tsx, lib/deals/slip.ts)

- Columns: all 7 stages in order lead, qualified, proposal, negotiation, hold, won, lost (queries.ts:938-946; lib/deals/schema.ts:6-14). A `stage` filter hides unselected columns. **Hold column is hidden when it has 0 deals** (unless stage filter includes hold) and is shown collapsed by default when non-empty (board-view.tsx:252-263, 329-330).
- **Won and Lost are not time-bounded**: they show all non-deleted won/lost deals (subject to filters and the 50 cap).
- Per column cards: `ORDER BY updated_at DESC, id DESC` LIMIT **50** (BOARD_PER_STAGE_LIMIT, 717, 1004-1017). Further pages per column via cursor `(updated_at, id) <` (873-912). `capped` = any stage count > loaded rows.
- Per column totals over ALL matching deals (not just loaded): count, amount = sum(amount_minor), slipped count/amount = deals with push_count >= 1 (1026-1048). Weighted = round(amount × stage probability / 100); slipped weighted likewise (board-column.tsx:166-174). Probability = stage table (N6), from org settings.
- Filters: same as the list (boardSharedConditions 761-854) **except** offering / uncategorized filters are not applied on the board, and risk uses EXISTS deal_risk_scores score >= floor. Sales scope same.
- Slipped-only toggle = `slipped=1` → push_count >= 1.
- Card indicators: overdue = expected_close_date < today (client local midnight) and stage not won/lost (slip.ts isDealOverdue); slip severity: pushCount 0 → none; pushCount >= **3** OR totalSlipDays >= **30** → severe; else watch (slip.ts SEVERE_PUSH_COUNT/SEVERE_SLIP_DAYS); de-weighted = quota_weight < 1.00 (stage-governance.ts:232); rolled back = rollback_count > 0; health band border strong / moderate / at_risk / unscored (deal-card.tsx:76-93). There is no separate "at-risk" column rule on the board; "at risk" = health_band `at_risk` (card) or the slipped amount line in the column header.

---

## N8 Global search (app/api/search/route.ts, command-palette.tsx)

- GET `/api/search?q=`; unauthenticated → 401 `Unauthorized`. q trimmed, cut to **100 chars**; min length **1** (empty → `{query:'', hits:[]}`). `%` and `_` escaped; pattern `%q%`, ILIKE (case-insensitive).
- Scope: persona sales → each group adds `owner_id = me OR owner_id IS NULL` (tasks: assigned_user_id). Workflow runs only for persona admin.
- Max **5** per group; all groups queried in parallel; results concatenated in group order deals, companies, contacts, workflows, tasks.

| Group | Match | Filters | Order | Returns |
|---|---|---|---|---|
| deal | `name` | not deleted | updated_at DESC | id, title = name, subtitle = **linked** company name (company_id join; free-text deal company_name not used), stage, href `/dashboard/deals/<id>` |
| company | `name` OR `domain` | not deleted, not merged | updated_at DESC | id, title = name, subtitle = domain, href `/dashboard/companies?q=<name>` |
| contact | `first_name` OR `last_name` OR `email` (not title, not full name) | not deleted, not merged | updated_at DESC | id, title = "First Last" or email or `(unnamed)`, subtitle = email else company name, href `/dashboard/contacts?q=<title>` |
| workflow (admin only) | `workflow_id` | — | started_at DESC | id, title, subtitle = start time, status |
| task | `title` | not deleted | open (todo/in_progress) first, then updated_at DESC | id, title, subtitle `due <date>` or null, status, priority, href `/dashboard/tasks?q=<title>` |

- Client: debounce **120 ms**, aborts the previous request (command-palette.tsx:197-236).

---

## N9 Offerings (lib/offerings/queries.ts, types.ts, app/dashboard/deals/actions.ts, deals/[id]/offering-actions.ts, settings/offerings/actions.ts)

- Workspace switch `offeringsEnabled` (workspace_settings, default **false**); set by `setOfferingsEnabledAction`, permission manageOfferings.
- Catalog: name trimmed 1–**120** (`Give the offering a name` / `Keep the name under 120 characters`), category `product` | `service` (`Pick Product or Service`); unique per tenant (name; comment says on lower(name)) → `“<name>” already exists in this workspace.`; new sort_order = max in category + 10; reorder sets (i+1)×10; retire = active false (no delete in this file). 14 default offerings seeded (queries.ts DEFAULT_OFFERINGS). Catalog order everywhere = category ASC, sort_order ASC, name ASC.
- Options list for pickers: active only by default, catalog order (listOfferingOptions).
- Tag rules (types.ts): `Pick at least one offering — what is this deal selling?` (none chosen); `Mark one offering as primary.` (primary missing); `This deal is already tagged, so it can’t be left with no offering. To change what it sells, add the right one first, then remove this.` (emptying a tagged deal). Primary default: the requested one if in the list; else the only one when exactly 1; else none (error). No maximum number of tags.
- Deal create (deals/actions.ts:413-436, 525-541), only when switch on: selection required and validated; ids resolved against **active** offerings of the tenant (inactive silently dropped; none left → required message); inserted with is_primary on the primary. Form fields `offeringIds` (comma list) and `primaryOfferingId`.
- Deal update (760, 941-957), only when switch on: `syncDealOfferings` — keeps existing tags even if retired; new ones must be active (else `One of those offerings is no longer available. Reopen the deal and pick again.`); empty selection on an untagged deal = no-op; on a tagged deal = cannot-empty error; removes deselected; resets primary to the chosen one. Switch off → tags untouched.
- Deal detail actions (permission **manageDeals**; switch off → `Offerings have been turned off for this workspace by an admin, so this deal’s offerings can’t be changed right now. Its existing offerings are kept.`): add (active only, else `That offering is no longer available.`; first tag becomes primary; duplicate → `That offering is already on this deal.`; activity `updated` metadata {name, field:'offering', op:'added'}); set primary (clear then set; `Offering tag not found.`); remove (refused when it is the last tag; removing the primary promotes another remaining tag; no activity). Deal must exist and not be deleted (`Deal not found in this tenant.`).
- Deal list projection returns all tags ordered is_primary DESC then catalog order (deals/queries.ts:280-297).
