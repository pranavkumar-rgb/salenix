# U5 / U6 — UI parity spec: list chips, Company / Contact detail, Tasks, command palette, rail

Source root: `source/src/app/dashboard/` (paths below are relative to it unless they start with `lib/`, which means `source/src/lib/`).
Server semantics (filters, task rules, search SQL) are already in `notes/overnight-N-source-specs.md` (N3, N4, N5, N8); this file is the **UI** layer only.
Markers: **[GAP]** = the rebuild lacks data / an input for this; **[QUIRK]** = source behaviour that looks like a bug (decide: copy or deliberate difference); **[DIFF]** = difference already taken in the rebuild.

---

## U5a — Companies / Contacts filter chips

### Shared component `_components/toggle-filter-bar.tsx`
- Row: `flex flex-wrap items-center gap-1.5`. Leading label: Filter icon (lucide `Filter`, 12px) + text "Filter" (11px slate-500) (:80-83).
- Each chip = button, 11px medium, `rounded-md border px-2 py-1`; inactive `border-slate-200 bg-white text-slate-600`, active `border-indigo-200 bg-indigo-50 text-indigo-700` (:92-97). Tooltip = `title` (:91).
- Click toggles one fixed `key=value` URL pair; clicking an active chip removes the key; every toggle also deletes `cursor` (back to page 1) (:51-65). Chips with the **same key** replace each other (only one value per key).
- "Clear all" (X icon + "Clear all", pushed right with `ml-auto`) shows **only when at least one chip is active** (:103-112). It deletes every toggle key + `alsoClear` keys + `cursor`. It does **not** clear `q` (search) or any key not listed.
- Chips combine with AND (each key adds a WHERE condition), and AND with the search box and the persona scope.

### Companies (`companies/_components/companies-view.tsx:478-489`)
Order and exact strings:

| # | Label | URL key=value | Tooltip (`title`) | Rebuild input |
|---|---|---|---|---|
| 1 | "My companies" | `mine=1` | "Companies you own" | (existing Mine) |
| 2 | "Unassigned" | `unassigned=1` | "No owner assigned" | (existing Unassigned) |
| 3 | "Has active deals" | `hasDeals=1` | "Deals column counts at least one open deal" | HasActiveDeals = True |
| 4 | "Recently active" | `recentActivityDays=14` | "Last Activity within 14 days" | RecentActivityDays = 14 |
| 5 | "New (30d)" | `createdWithinDays=30` | "Created in the last 30 days" | CreatedWithinDays = 30 |
| 6 | "Stale" | `stale=1` | "Last Activity older than 30 days (or never)" | Stale = True, StaleDays = 30 |

- `alsoClear={[...COMPANY_SORT_URL_KEYS]}` = `sort`, `dir` (lib/companies/filters.ts:65): Clear all also resets the sort to default "recent".
- Semantics (lib/companies/queries.ts:166-204): Has active deals = rollup open deal count > 0; Recently active = last activity ≥ now − 14 d; Stale = last activity < now − 30 d **or never** (complement of Recently active at the same threshold, so "Recently active" + "Stale" together always gives 0 rows — no UI guard); `staleDays` URL key exists (default 30, lib/companies/filters.ts:151) but no chip sets it.
- "My companies" + "Unassigned" together → 0 rows (owner = me AND owner is null); source does not prevent it.
- URL keys `minOpen`, `minWon` exist in the parser (filters.ts:12-13) but have **no chip** → not needed.
- Chip row sits directly under the search + "New company" row (:416-489), above the error line and bulk bar.

### Contacts (`contacts/_components/contacts-view.tsx:381-390`)

| # | Label | URL key=value | Tooltip | Rebuild input |
|---|---|---|---|---|
| 1 | "My contacts" | `mine=1` | "Contacts you own" | (existing) |
| 2 | "Unassigned" | `unassigned=1` | "No owner assigned" | (existing) |
| 3 | "Recently active" | `recentActivityDays=14` | "Activity in the last 14 days" | RecentActivityDays = 14 |
| 4 | "No company" | `noCompany=1` | "Contacts not linked to a company" | (existing NoCompany) |
| 5 | "New (30d)" | `createdWithinDays=30` | "Created in the last 30 days" | CreatedWithinDays = 30 |

- Note the order: **No company sits between Recently active and New (30d)**.
- No `alsoClear` → Clear all removes only these 5 keys (not `companyId`, not `q`).
- Contacts "Recently active" = an `activity_logs` row on the contact in the last 14 days (lib/contacts/queries.ts:162-171) — not a rollup column.
- `companyId=<uuid>` (lib/contacts/filters.ts:5) has **no chip** in the source; it is only set by the Company detail "View all" / "+N more contacts" links (`companies/[id]/_components/account-contacts-card.tsx:28`). Clear all does not remove it.
- There is **no "Stale" and no "Has active deals"** chip on contacts.

### How the new chips sit next to the rebuild's existing chips (L3b, Salenix rev 28)
| Rebuild element | Source | Status |
|---|---|---|
| Mine, Unassigned chips | same keys, same labels | same |
| Contacts "Company" dropdown chip | none (companyId only via URL from company detail) | **[DIFF]** already taken (L3b). Keep; Clear all in the rebuild clears it (source does not). |
| Contacts "No company" chip | yes, position 4 | move it to position 4 (between Recently active and New (30d)) |
| "Recently deleted" link + deleted banner | none on the list pages (source has a separate page `/dashboard/recently-deleted`, admin rail "System" group, nav-config.ts:130) | **[DIFF]** already taken (L3b, open item "Recently deleted page") |
| Companies Clear all resets sort | yes (alsoClear sort/dir) | same (L3b ClearFilters sets SortBy "recent") |
| Clear all visible only when a chip is active | yes | check rebuild: show ClearAllLink only when any chip variable is set |

Final chip order for the rebuild:
- Companies: My companies · Unassigned · Has active deals · Recently active · New (30d) · Stale · [Clear all] · (Recently deleted — DIFF)
- Contacts: My contacts · Unassigned · Recently active · No company · New (30d) · Company ▾ (DIFF) · [Clear all] · (Recently deleted — DIFF)

Empty state when chips give 0 rows: unchanged from L3b (source `EmptyState searchActive` only distinguishes search vs not: companies-view.tsx:520).

---

## U5b — Company detail ("Account 360") and Contact detail

### Company detail (`companies/[id]/page.tsx`)
- Tab title "Company" (:39-41).
- Back link: ArrowLeft + "All companies" → companies list (:145-151), 12px slate-500.
- Header (:153-185), bottom border: H1 = company name (24px semibold). Subline (14px slate-500): `<industry or "Company">` · `<domain>` (mono, only if set) · `"<n> deal"/"<n> deals"` · `"<n> contact"/"<n> contacts"`. Right: ComposeEmailLauncher (email; Comms not built → omit, Parity backlog).
- Grid (:187): `lg: 300px | 1fr`; `xl: 300px | 1fr | 320px`. Gap 16.
- **Left column** (:189-274), each a small card (label 10px uppercase slate-500, value 14px):
  1. "Name" — inline editable (canEdit = manageCompanies).
  2. "Website" — value domain or "—", mono, placeholder "acme.com"; below it link "Open site" + ExternalLink icon (opens `https://<domain>` in new tab) only when domain set.
  3. "Type" — value industry or "—", placeholder "Manufacturing" (industry is shown as "Type").
  4. Owner card (EntityOwnerCard, change owner).
  5. "Created" — date "MMM d, yyyy" (en-US), read only.
  6. "Updated" — same format, read only.
  7. "Custom fields" box only when a custom value exists (**[GAP]** custom fields not in rebuild, backlog L19).
  8. Below xl: the two rollup cards fold in here (:273).
- **Center column** (:277-317), order top → bottom:
  1. **Tasks** panel (EntityTasksPanel, entityType "company", teammates passed).
  2. **Timeline** titled **"Account activity"**, `groupByMonth`, `showSource`, empty text **"Nothing has happened on this account, its deals, or its contacts yet."** (:288-296).
  3. **Comments** (CommentsThread).
  4. **Notes** (NotesTimeline).
- **Right column** (xl only, :320): Contacts card then Deals card.

Contacts card (`account-contacts-card.tsx`): heading Users icon + "Contacts" + "(total)"; link "View all" (only total > 0) → `/dashboard/contacts?companyId=<id>`. Rows (max 5, `updatedAt desc`, not deleted / not merged; lib/companies/account-360.ts:39,146-154): name ("First Last" or email or "Unnamed contact") → contact detail; title line; email (Mail icon); phone (Phone icon). Footer "+N more contact(s)" → same View all link. Empty: UserPlus icon, "No contacts yet", "Nobody at this account is on record. Add a contact to start tracking who you know here.", button-link "Add a contact" → contacts list (:49-65).

Deals card (`account-deals-card.tsx`): Briefcase icon + "Deals" + "(total)"; "View all" → `/dashboard/deals?companyIds=<company NAME>` (:71) → rebuild: Deals screen with CompanyId filter. Rows (max 5, **all stages incl. closed**, `updatedAt desc`): name + stage badge (stage label, 9px uppercase) on line 1; amount (mono, deal currency) + close date or "No close date" on line 2; row → deal detail. Footer "+N more deal(s)". Empty: PlusCircle icon, "No deals yet", "Nothing is in play with this account. Create a deal to put it on the pipeline.", "Create a deal" → deals list (:95-107).
- **[GAP]** Rollup cards: rebuild has Contact_List(CompanyId, MaxRecords 5) and Deal_List(CompanyId) — Deal_List applies persona scope and hides closed by default; source 360 shows every deal of the account without owner scope. Use IncludeClosed = True; scope difference = [DIFF] (keep rebuild scope).
- **[GAP]** Header counts: need total deals (all stages) and total contacts — use the two list TotalCounts.

### Contact detail (`contacts/[id]/page.tsx`)
- Tab title "Contact" (:33-35). Back link ArrowLeft + "All contacts" (:88-94).
- Header: H1 = "First Last" or email or "(unnamed)" (:81-84); subline `<title or "Contact">` · `<email>` (mono). Right: ComposeEmailLauncher (omit, Comms).
- Grid **2 columns** `lg: 320px | 1fr` (:122) — no right rail.
- Left cards (:124-153): "Title" (or "—"), "Email" (mono), "Phone" (mono), Owner card, "Created" (MMM d, yyyy), Custom fields box (GAP). **Read-only cards** here (no inline edit; editing is via the Edit drawer). No "Updated" card. No company card in the source (**note:** company is not shown on the contact detail page at all).
- Center order (:156-191) — **different from company**:
  1. **Tasks** (EntityTasksPanel, entityType "contact", **no teammates passed** → quick-add Assignee select has one option "Owner (default)").
  2. **Comments**.
  3. **Notes**.
  4. **Timeline** — default title **"Timeline"**, no month groups, no source chips, default empty text **"No activity yet for this record."** (entity-timeline.tsx:118, 265-267).

### Shared panels (same component on deal, company, contact)
Deal detail order for comparison (deals/[id]/page.tsx:707-842): "Activity" timeline first (deal filter set, "Log activity" button), …, Tasks, …, Comments, Notes.

**Tasks panel** (`_components/entity-tasks-panel.tsx`)
- Empty (no open, no done): collapsed `<details>`: CheckSquare icon + "Tasks" + "— none yet" + chevron (:403-412); open body = quick add + "No tasks here yet." + (editors) " Type above to capture the next action." (:415-419).
- Non-empty header: "Tasks"; right side: rose pill "<n> overdue" (only > 0) + "<open> open · <done> done" (:426-439).
- Quick add (editors only, :236-395): input placeholder **"Add a task — Enter to create, Tab for details"**; Enter creates; Tab (with text) expands details; expand toggle aria "Add task details" / "Collapse task details". Detail fields: "Due date" (date), "Assignee" (options "Unassigned" + teammates, default = record owner else me; without teammates only "Owner (default)"), "Type" ("—", "Call", "Email", "Meeting", "Internal", "Follow-up"), "Priority" (Low/Medium/High/Urgent, default Medium), "Repeat" ("No repeat", "Daily", "Weekly", "Custom (monthly)"), "Blocks stage" (deal only: "None", Qualified, Proposal, Negotiation…; :359-366); buttons "Cancel", "Create task". Toast on recurring completion: "Recurring task rolled forward to next due date" (:235).
- Groups (:444-470): "Overdue", "Today", "Upcoming", "No due date"; within a group sort by due date asc then priority urgent→low (:668-673). Done section: "▸ Done (n)" / "▾ Done (n)", collapsed, max 30 rows (:475-492).
- Row: checkbox (aria "Mark complete"/"Mark incomplete"), priority dot (tooltip "<Priority> priority"), type icon, recurrence icon, title (struck through when done); second line due chip ("<n>d overdue" rose, "Due today"/"Due tomorrow"/"Due in <n>d" amber, else "MMM d" plain) and "Blocks <stage>" (:575-664, 676-693).

**Comments** (`_components/comments-thread.tsx`): heading "Comments" + count (:215-219); empty "No comments yet. Mention a teammate with `@` to start a discussion." (:224-226); composer placeholder "Add a comment — type @ to mention a teammate", button "Comment" (:264-265); per comment "Reply", "Edit", "Delete"; reply placeholder "Reply to <author>…", button "Reply"; author fallback "Teammate"; ⌘/Ctrl+Enter submits, Esc cancels.

**Notes** (`_components/notes-timeline.tsx`): heading "Notes" + count (:118-121); composer placeholder "Add a note — context, next steps, intel from a call…", button "Add note" (:130-146); empty "No notes yet. Use this space to capture call notes, intel from meetings, or the next-step plan." (:154-155); author "You" / name / "Teammate"; "Edit", "Delete".

**Timeline** (`_components/entity-timeline.tsx`)
- Default filter chips (FILTER_ORDER :72-81) used on company + contact: "All", "Mine", "Teammates", "Emails", "Comments", "Notes", "Activity", "Automation" — each with a count; empty per filter: "No <label lowercased> yet." (:267).
- Company only: `groupByMonth` → month headers "November 2026" style (en-US long month + year, UTC) with a rule and the item count (:268-289, 324-341).
- Company only: `showSource` → after the relative time, a grey pill link **"Deal · <deal name>"** or **"Contact · <contact name>"** → deal / contact detail (:367-379). (Not "on <name>".) Items of the company itself have no pill.
- Icon circle per kind: comment indigo MessageCircle, note amber StickyNote, email sky Mail (failed rose AlertCircle), automation violet, call teal Phone, meeting cyan Users, stage change indigo ArrowLeftRight, task indigo CheckSquare, else slate Activity (:405-440).
- **[GAP]** Rebuild TimelineItem has SourceName but no source kind/id field; derive: source shown when item.EntityType ≠ "company", kind = EntityType ("deal" → "Deal", "contact" → "Contact"), link id = EntityId. Activity rows of child deals/contacts have **empty SourceName** (LONG-RUN-REPORT N5) → show "Deal" / "Contact" without name, or skip the pill (decide).
- **[GAP]** No emails (Comms not built): drop the "Emails" chip or show it with 0 → recommend drop (deliberate).

---

## U6a — Tasks page (`tasks/page.tsx`, `tasks/_components/*`)

### Page
- Tab title "Tasks". Header: H1 **"Tasks"**, subtitle = workspace name (page.tsx:60-65). Same max width / paddings as Companies.
- No page-level role gate (any member); edit controls only when `canManageDeals(role)` (tasks-view.tsx:71) → owner/admin/manager/member; viewer read-only.
- Sales persona scope: assigned to me OR unassigned (page.tsx:38-40).

### Toolbar (tasks-view.tsx:208-256)
- Search box: Search icon left, placeholder **"Search tasks by title…"**, aria "Search tasks", spinner right while loading; 250 ms debounce (:113-122).
- **[QUIRK]** The search effect builds a **new** URL with only `q` and `size` (:115-120): typing a search **drops every active chip** (and the cursor). Recommend deliberate difference: keep chips (as L3/L3b).
- Button (editors only) **"New task"** → CreateDrawer title **"Create task"**, description **"Add an action item — optionally link it to a deal, company, or contact."** (:234-238). Global "c" key and `?new=1` also open it (create-drawer.tsx:52-68).

### Filter chips (tasks-view.tsx:258-268), ToggleFilterBar, order:

| # | Label | URL | Tooltip | Task_List input |
|---|---|---|---|---|
| 1 | "My tasks" | `assignedToMe=1` | "Tasks assigned to me" | Mine = True |
| 2 | "Overdue" | `overdue=1` | "Past-due, not completed" | Overdue = True |
| 3 | "Due this week" | `dueWithinDays=7` | — | DueWithinDays = 7 |
| 4 | "To do" | `status=todo` | — | StatusCodes = "todo" |
| 5 | "In progress" | `status=in_progress` | — | StatusCodes = "in_progress" |
| 6 | "High / urgent" | `priority=urgent,high` | — | PriorityCodes = "urgent,high" |
| 7 | "Unassigned" | `unassigned=1` | — | Unassigned = True |

- "To do" and "In progress" share the key `status` → **mutually exclusive** (clicking one replaces the other). Copy as a single StatusFilter variable ("", "todo", "in_progress").
- Other chips AND together; "My tasks" + "Unassigned" → 0 rows (not prevented). "Due this week" includes overdue and completed tasks with a due date ≤ today + 7 (server rule, N4).
- "Clear all" as in the shared bar (removes these keys only, keeps `q`).

### List (tasks-view.tsx:316-403, task-row.tsx)
- One card (border, white, rounded). **Not a table — no column headers.** Rows ordered `created_at desc` (server).
- Split: active rows (status ≠ completed — includes cancelled) first; if no active but some completed: line **"No active tasks — <n> completed below."** (:339-343).
- Completed section: full-width bar button with a triangle + **"Completed (<n>)"** left and **"Show" / "Hide"** right, collapsed by default (:357-377); expanded rows on `bg-slate-50/30`.
- Pagination under the list (`PaginationControls`, :395-400): "Showing <n> rows" / "Showing 1 row", page-size select 25 / 50 / 100 (default 50) + "per page", buttons "Prev" / "Next" (pagination-controls.tsx:82-124; lib/pagination/page-size.ts:19). Rebuild: StartIndex / MaxRecords + TotalCount (rebuild may show "Showing x–y of n tasks" like L3b — DIFF already used on Companies / Contacts).

Row (task-row.tsx:102-235), left → right:
1. Complete checkbox (16px square; editors only; aria "Mark complete" / "Mark incomplete"; done = emerald filled with check). Viewer: grey empty box. Click → toggle (reopen sets status todo, server rule).
2. Title (13px medium; button → opens the task detail drawer; done = line-through). Spinner while pending.
3. Second line (10.5px):
   - Priority badge (uppercase, tint low slate / medium blue / high amber / urgent rose; label "Low" / "Medium" / "High" / "Urgent"); editors: click cycles low → medium → high → urgent → low, tooltip "Cycle priority" (:81-88, 165-180).
   - Status badge (todo slate "To do", in_progress indigo "In progress", completed emerald "Completed", cancelled slate "Cancelled"); click cycles todo → in_progress → completed → todo (cancelled → todo), tooltip "Cycle status" (:90-100, 181-196; labels lib/tasks/filters.ts:108-113).
   - Due label (client local date, :35-53): past → "<n>d overdue" (rose pill), 0 → "Due today", 1 → "Due tomorrow", 2–7 → "Due in <n>d" (amber pill), later → "MMM d" (plain grey). No due date → nothing. **Note:** overdue label shows even for completed tasks (computed from date only); rebuild TaskItem.IsOverdue excludes completed/cancelled — use IsOverdue for the rose border/pill and the date diff for the text (decide; [QUIRK]).
   - Linked record link: `"<entityType>: <linkedName>"` (lower-case type, e.g. "deal: Acme renewal") grey, hover indigo underline; deal → deal detail, **company / contact → the unfiltered list page** (:55-59, 211-220) **[QUIRK]** — the drawer links to the detail page; recommend detail screen in the rebuild (deliberate).
4. Delete icon (Trash2, editors only, visible on row hover, aria "Delete task") → **deletes immediately, no confirm** (:225-234); toast "Task deleted" (tasks-view.tsx:195).
- Overdue row: 2px rose left border (:108). Done / cancelled rows: opacity 70%.
- **Assignee is not shown in the row** (only in the drawer).
- Toasts: create "Task created — <title>" (:164); errors = server message.

### Empty state (tasks-view.tsx:405-423)
Dashed box, Trash2 icon (source uses the trash icon), heading **"No tasks yet"** / with search **"No tasks match your search"**; text **"Use \"New task\" to capture an action item — optionally link it to a deal."** / **"Try a different title or clear the filter chips."** Chips without search show the "No tasks yet" text (source has no separate "no matches" for chips).

### Create task form (`create-task-form.tsx`), in the drawer, vertical gap 16
1. "Title" — text, required, autofocus, placeholder **"Follow up with Razorpay on Q3 renewal"** (:70-77). Max 280 (server).
2. "Description" — textarea 3 rows, placeholder **"Optional — context, links, expected outcome"** (:79-86). Max 4000.
3. Row of 2: "Priority" select (Low, Medium, High, Urgent; default **Medium**) | "Status" select (Todo, In progress, Completed, Cancelled — note the label **"Todo"** here vs "To do" on badges; default **todo**) (:88-105).
4. Row of 2: "Due date" (date) | "Assignee" (OwnerFormField: button showing avatar + name or "Unassigned", right text "Change"/"Assign", opens TeammatePicker with unassign option; **default = current user**) (:107-122; owner-form-field.tsx:40-90).
5. Row of 2: "Linked to" select ("— None —", "Deal", "Company", "Contact") | "Entity" select: disabled until a type is picked; first option "— Pick a type first —" / "— No <kind>s yet —" / "— Pick a <kind> —"; first 200 options of that kind; changing the type resets the entity (:124-167).
6. Error line, then full-width-style primary button **"Create task"** with spinner (:170-183).
- No Type / Repeat / Blocks-stage fields on this page form (only in the entity quick add).
- **[GAP]** Entity picker options: deals → need a deal option list (Deal_List or a Deal option action; Company_ListOptions / Contact_ListOptions exist). 200-row cap fine.

### Task detail drawer (`task-detail-drawer.tsx`) — opened by clicking a title; this is the edit UI (no separate edit form)
- Drawer title = task title (fallback "Task"), description **"Created MMM d, yyyy"** (:109-116).
- Body (:226-356), top → bottom:
  1. Inline title: click to edit ("Click to edit" tooltip), Enter saves, Esc cancels, empty shows "Untitled task" (:380-420).
  2. Badge row: priority pill + status pill (cycle on click, same cycles as row), due chip "MMM d, yyyy" (+ " · <n>d overdue"; rose when overdue, amber when ≤ 3 days, else slate) (:233-262, 592-612), spinner.
  3. "DESCRIPTION" section: click-to-edit textarea, placeholder "Add context, links, expected outcome… (⌘⏎ to save, Esc to cancel)", empty text "Click to add a description" (:263-271, 440-487).
  4. 2-column grid: "Assignee" (name or "Unassigned", button "Change"/"Assign" → TeammatePicker incl. unassign) | "Creator" (User icon; name, fallback raw id, "(system)") | "Due" (date input; clearing sets null) | "Updated" (datetime "MMM d, yyyy, h:mm AM") (:273-291, 490-560).
  5. "LINKED TO" (only when linked): pill link (entity type badge + linked name) → **detail page** of deal / company / contact (:80-84, 293-321) + ComposeEmailLauncher + note "Email sends attach to the linked <type> and appear on its timeline." (**omit**, Comms not built).
  6. Task comments (below).
  7. Sticky footer: left **"Delete"** (rose outline, Trash2) → `confirm("Delete this task? This action soft-deletes the row.")` → toast "Task deleted", drawer closes; right **"Mark complete"** (emerald) / **"Mark incomplete"** (slate) (:204-220, 323-353).
- **[QUIRK]** The drawer shows all edit controls to every role (no canEdit check); viewers get a server error toast. Rebuild: hide/disable for viewers (deliberate).
- Every edit = partial patch (Task_Update in the rebuild is a full write → send the whole TaskItem with one field changed; Task_Update keeps type / recurrence / stage blocking, decision 25(1)).

### Task comments (`task-comments.tsx`) — inside the drawer
- Heading "COMMENTS" (+ count pill when > 0) (:111-117). Loaded when the drawer opens (task-detail-drawer.tsx:146-156).
- Empty: **"No comments yet. Start the discussion below."** (:121-124).
- List (oldest first): avatar (image or initial), author name (fallback raw id), relative time, " · edited" when edited, body (pre-wrap). Own comments: hover delete icon (aria "Delete comment") → `confirm("Delete this comment?")` (:84-90, 126-180). No edit, no reply, **no @mentions** in the source task comments.
- Composer: textarea 2 rows, max 4000, placeholder **"Add a comment… (⌘⏎ to post)"**; ⌘/Ctrl+Enter posts; button **"Post"** disabled when empty; spinner while pending (:183-210).
- Rebuild: Comment_List / Comment_Create / Comment_Delete with EntityType "task" (decision 25(2)). [DIFF] rebuild Comment_Create parses @mentions also on tasks; UI shows no mention picker here (same as source).

### Rebuild data check (Task_List / TaskItem)
| Source need | Rebuild | Note |
|---|---|---|
| Search, chips, scope, pagination | Task_List(Search, Mine, Unassigned, StatusCodes, PriorityCodes, Overdue, DueWithinDays, StartIndex, MaxRecords) → Items, TotalCount | OK |
| Row: title, priority, status, due, linked name/type/id, created order | TaskItem Title, PriorityCode, StatusCode, DueDate, IsOverdue, EntityType, EntityId, LinkedName | OK |
| Drawer: Creator name | CreatedBy (user id) only | **[GAP]** resolve name via Member_ListOptions (already used for owners) |
| Drawer: Updated datetime | UpdatedAt (UTC) | client format |
| Assignee picker | Member_ListOptions | OK |
| Create form Entity options for deals | — | **[GAP]** need deal options (e.g. Deal_List MaxRecords 200 or a Deal_ListOptions) |
| Task comments count / author name / edited | CommentItem (check fields: author name, edited flag) | verify before UI batch |

---

## U6b — Command palette (`_components/command-palette.tsx`, shell `dashboard-shell.tsx`, `header.tsx`)

### Triggers
- Global keydown on window: **Ctrl+K or ⌘+K** (`k`/`K` with metaKey or ctrlKey) **toggles** the palette; ignored inside contentEditable; preventDefault (dashboard-shell.tsx:55-67). Works while focus is in an input.
- Closes on route change (:92-96).
- Top bar trigger (header.tsx:63-75): left side of the 48px sticky header, next to the mobile menu button; button max-width 28rem (sm) with Search icon, text **"Search deals, companies, contacts…"**, right `<kbd>` showing **"⌘K"** on Mac else **"Ctrl K"** (hidden on phone); aria "Open command palette (<shortcut>)", tooltip "Search and jump — <shortcut>".
- Also "c" (no modifier, not in a field) dispatches create-on-current-page (dashboard-shell.tsx:69-89) — separate feature, not part of the palette.

### Dialog
- Modal overlay (button aria "Close" covers backdrop, click closes); dialog aria "Command palette" (:346-359).
- Input row: placeholder **"Search deals, companies, contacts, workflows…"** (:370) → rebuild has no workflows: use "Search deals, companies, contacts, tasks…" ([DIFF], tasks are searched in the source too). Clear button (X, aria "Clear") when text present; `<kbd>` **"esc"** (:375-390). Autofocus on open; state reset on every open (query empty, selection 0).
- Debounce **120 ms**, previous request aborted; results replaced only when the response matches the current term; on error the previous results stay (:197-236). Search_All(Query) in the rebuild (max 5 per group).

### Modes and groups
- **Empty query** (:242-255): section **"Recent"** (History icon before the header; up to **6** recent hits from `localStorage` key `crm.palette.recent.<tenantId>`, most recent first, de-duplicated by kind+id, saved when a hit is opened, :133-163, 275-290) then section **"Quick actions"**. If there are no rows at all: **"Type to search — or pick a quick action below."** (:399-402) (only reachable when actions list is empty — effectively never).
- **Query** (:257-262): one section per kind in server order, headings from KIND_LABEL (:61-67): **"Deals"**, **"Companies"**, **"Contacts"**, ("Workflows" admin only — omit), **"Tasks"**. A heading is printed when the section changes (:330-342).
- No results (query, not loading, 0 rows): Search icon + **"No results for “<query>”."** (:394-398).
- Footer (:440-459): left **"Searching…"** while loading, else **"<n> result(s)"** when query non-empty, else **"Quick actions + recent"**; right hints `↑` `↓` "move", `↵` "open".

Quick actions (QUICK_ACTIONS :77-131, in this order; icon slate):

| Title | Hint | Target (source) | Rebuild target |
|---|---|---|---|
| "Go to Dashboard" | — | /dashboard | Home |
| "Go to Workflows" | — | /dashboard/workflows | **omit** (no workflows) |
| "Go to Tasks" | — | /dashboard/tasks?assignedToMe=1 | Tasks with My tasks chip on (**[GAP]** Tasks screen input `Mine`) |
| "Create deal" | "opens the new-deal form" | /dashboard/deals?new=1 | Deals + open create drawer (**[GAP]** screen input `OpenNew`) |
| "Create company" | "opens the new-company form" | /dashboard/companies?new=1 | Companies + OpenNew |
| "Create contact" | "opens the new-contact form" | /dashboard/contacts?new=1 | Contacts + OpenNew |
| "Create task" | "opens the tasks page" | /dashboard/tasks | Tasks (no auto-open in source) |

Row content:
- Hit row (:465-494): kind icon (indigo; deal Briefcase, company Building2, contact UsersRound, task CheckSquare), **title** (medium) + subtitle (10.5px grey, same line, truncate), right badge (9px uppercase grey): deal → **raw stage code** (e.g. "NEGOTIATION"; source shows the code, not the label — rebuild may show the stage label, decide), task → **priority** code (e.g. "HIGH"); company / contact no badge.
- Subtitles (server, N8): deal = linked company name; company = domain; contact = email else company name; task = "due <yyyy-mm-dd>" or none.
- Action row (:496-510): icon, title, hint in grey.
- Selected row: `bg-indigo-50 text-indigo-700` + CornerDownLeft icon at the right; mouse hover sets the selection (:415-432).

Keyboard (:300-326): ↓ / ↑ move with **wrap-around**; Enter opens the selected row; Esc closes. The doc comment mentions Ctrl/Cmd+1..5 section jumps (:43) but **they are not implemented** — skip.

Navigation per kind (route.ts:191-243 → rebuild):
| Kind | Source href | Rebuild |
|---|---|---|
| deal | /dashboard/deals/<id> | DealDetail(DealId) |
| company | /dashboard/companies?q=<name> (list filtered by name, not detail) | Companies with search = Title (**[GAP]** screen input `Search`); or CompanyDetail(Id) as [DIFF] |
| contact | /dashboard/contacts?q=<display title> | Contacts with search = Title (**[GAP]** input `Search`); or ContactDetail(Id) as [DIFF] |
| task | /dashboard/tasks?q=<title> | Tasks with Search = Title (**[GAP]**); source does not open the drawer |

Rebuild data check (SearchHitItem: Kind, Id, Title, Subtitle, StageCode, StatusCode):
- **[GAP]** Task badge shows **priority** in the source; SearchHitItem has StatusCode only → add PriorityCode to SearchHitItem (Sales, add-only) or show status ([DIFF]).
- No `href` field → build the target on the client from Kind + Id + Title (table above).
- Recents need the full hit (Kind, Id, Title, Subtitle, StageCode/StatusCode) in localStorage per tenant; key per tenant needs the active tenant id or name on the client (GetMyContext) — localStorage is a per-viewer convenience (allowed).

---

## U6c — Rail navigation (`_components/nav-config.ts`)

- **"Tasks" exists only in the member/sales tree** (MEMBER_NAV, persona "sales" = member + viewer): group **"My Work"**, order: "Dashboard" (LayoutDashboard) · "My Deals" (Briefcase) · "Pipeline" (GitBranch) · "At Risk" (AlertTriangle) · **"Tasks" (lucide `CheckSquare`)** (:198-216). Then group "CRM": Contacts (UsersRound), Companies (Building2), Invoices (FileText).
- **Admin and manager trees have no Tasks item** (ADMIN_NAV :70-133, MANAGER_NAV :135-196; `grep Tasks` finds only :211). They reach Tasks through the palette ("Go to Tasks") or a direct URL; the page itself has no persona guard.
- Phosphor equivalent for `CheckSquare`: `check-square` (rebuild icon library is Phosphor 2.0, see THEME-GUIDE "Icons").
- Active-state matcher: default prefix match on `/dashboard/tasks`.
- Rebuild: add a "Tasks" rail item after "At Risk" (or after Pipeline if At Risk is not built yet) in the sales-persona rail only; decide whether admin/manager get it too (source: no → [DIFF] if added).

---

## Open decisions for the user (before the U5/U6 Mentor prompts)
1. Tasks search keeps the chips (deliberate fix of the source quirk)? — recommended yes.
2. Task row linked company/contact → detail page instead of the unfiltered list? — recommended yes.
3. Palette company/contact hit → list filtered by name (source) or detail page? Needs screen input `Search` either way for tasks.
4. Add PriorityCode to SearchHitItem (Sales add-only) for the task badge?
5. Company timeline source pill for child activity rows without SourceName: show "Deal" / "Contact" only, or skip?
6. Tasks rail item for admin / manager (source: none)?
7. Screen inputs needed: Deals / Companies / Contacts `OpenNew`; Companies / Contacts / Tasks `Search`; Tasks `Mine`.
