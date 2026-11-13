# U2 — Deal detail UI parity spec (source → Salenix rebuild)

Source root: `source/src`. All strings are exact (quotes = user-visible text). Line refs are `file:line` at the cloned revision.
Scope: (1) edit-deal drawer footer + Delete, (2) Context card inline fields, (3) Tasks panel, (4) Notes card, (5) Comments + @mentions, (6) Activity timeline, (7) page layout.
"canEdit" on the deal page = `canManageDeals(role)` = owner, admin, manager, member (viewer = read-only) — `app/dashboard/deals/[id]/page.tsx:122`, `lib/rbac.ts:40`.

---

## 7. Page layout (read first)

File: `app/dashboard/deals/[id]/page.tsx`

Top (full width), in order:
1. Back link "All deals" with arrow-left icon → `/dashboard/deals` (`:249-255`).
2. `DealInlineHeader` (name, stage, amount, currency, close date, margin) (`:259-272`).
3. `DealPathBar` only when stage gates enabled (`:274-276`).
4. Row: `EntityViewers` (live viewers) + `ComposeEmailLauncher` ("Email") (`:278-292`).

Body grid `lg:grid-cols-[320px_1fr]`, gap 16px, margin-top 24px (`:294`); one column on narrow screens (left column first).

Left column `<aside>` (flex column, gap 20px) (`:295-688`):
- `RelationshipHealthCard` (`:296-300`)
- SidebarSection **"Deal Info"** (`:302`): Stage card, "Deal type", "Amount", "Unit price × Qty" (unit-priced only), "Margin", "Approval" + approval controls, close-date card, credit terms, forecast category, hold control, stage history, audit card.
- SidebarSection **"Relationship"** (`:529`): "Company", "Contact", owner picker, offerings card (or disabled notice), deal contacts card.
- SidebarSection **"Context"** (`:622-654`): Competitor, Source, Next step — see §2.
- SidebarSection **"Record"** (`:656-687`): "Created", "Last updated" ("<name> · <date>", fallback name "Someone"), "Closed" (only when closed), "Custom fields" box (only when any value).
- SidebarSection header style: 11px, semibold, uppercase, wide tracking, slate-400, bottom border (`:877-891`). Sections stack cards with gap 12px.

Right column `<section>` (space-y 16px) (`:690-851`), order (comment `:702-706`: "Activity → Tasks → AI → Documents"):
1. "Description" card — only when the deal has a description (`:691-700`; whitespace preserved).
2. **Activity timeline** `EntityTimeline` title "Activity" (`:707-725`) — §6.
3. `DealRiskCard` (only when a stored risk exists) (`:729`).
4. `DealScoreCard` (only when a score exists) (`:731-741`).
5. **Tasks panel** `EntityTasksPanel` (`:743-751`) — §3.
6. AI cards ("AI deal analysis", AI summary `SummaryCard`) — only when AI features are on (`:759-782`). Rebuild: no AI yet → absent (source also hides them when off).
7. `DealOrderProofCard` (`:785-811`).
8. `DealValueSplitCard` (`:814-822`).
9. `DealDocumentsCard` (`:824-829`).
10. **Comments** `CommentsThread` (`:831-840`) — §5.
11. **Notes** `NotesTimeline` (`:842-849`) — §4.

Date format for "Created" / "Last updated" / "Closed": `en-US` "Nov 13, 2026" (`:64-68`).

Note: the source deal DETAIL page has no Delete button and no Edit-deal drawer; editing is inline (header + cards). The edit drawer with Delete lives on the Deals LIST (`deals-view.tsx:956`). The rebuild's DealDetail "Edit deal" drawer (L4) is therefore a rebuild-only host; §1 describes the source drawer footer as-is.

---

## 1. Edit-deal drawer footer + Delete

Files: `app/dashboard/deals/_components/edit-deal-drawer.tsx`, caller `app/dashboard/deals/_components/deals-view.tsx`, permission `lib/rbac.ts`, server `app/dashboard/deals/actions.ts`.

Drawer: title "Edit deal", description (subtitle) = deal name (`edit-deal-drawer.tsx:218-221`).

Footer (`edit-deal-drawer.tsx:222-273`): one row, `justify-between`, gap 12px.
- LEFT: Delete button, only when `canDelete` (`:224-237`); otherwise an empty `<span/>` keeps Cancel/Save on the right (`:238-240`).
  - Label "Delete", trash icon (14px); while deleting the icon becomes a spinner, label stays "Delete".
  - Style: small (text-xs), red outline (border red-200, text red-700, hover red-50).
  - Disabled while delete pending OR save pending.
- RIGHT (gap 8px):
  - "Cancel" — outline button, closes the drawer (`onClose`) (`:242-248`).
  - "Save changes" — primary (indigo), save icon; submits form `edit-deal-form`; while pending: spinner + "Saving…", disabled (`:249-270`).

Confirm behaviour:
- Drawer Delete has **NO confirmation**: click → `handleDeleteFromDrawer` → `handleDelete(deal)` and the drawer closes immediately (`deals-view.tsx:555-559`).
- `handleDelete` (`deals-view.tsx:546-553`): clears the list error, optimistically removes the row, calls `softDeleteDealAction(id)`; on failure the message is shown in the list's red alert banner (`role="alert"`, `deals-view.tsx:879-886`). No success toast. (On failure the optimistic removal is not rolled back locally; the page revalidates.)
- For comparison (other entry points, same file): row trash icon → inline "Confirm" (red) / "Cancel" pair (`deals-view.tsx:2224-2249`); bulk delete → `window.confirm("Delete N deal(s)? They can be restored from Recently deleted.")` (`:313`).

Who sees Delete: `canDelete = canDeleteDeals(role)` (`deals-view.tsx:178`); `deleteDeals: ['owner', 'admin']` (`lib/rbac.ts:41`). Manager / member / viewer: no Delete button.

Server (`actions.ts:2018-2075`): permission `deleteDeals`; sets deleted_at / deleted_by; activity `deleted` {name}; errors "Invalid deal id.", "Deal not found or already deleted.".

Rebuild data: `Deal_SoftDelete(DealId)` → Success / OutcomeCode (ok / not_found) / Message — covers it. Role check in UI: `Foundation_HasPermission(ActingRoleCode, "deleteDeals")` (or role in owner/admin). After delete on DealDetail there is no list to remove the row from → navigate to Deals (rebuild decision; source has no detail-page delete).
Existing rebuild note (CLAUDE.md L5b): company/contact edit footers use an inline confirm — a deliberate difference; source deal drawer deletes without confirm.

---

## 2. Context card — inline Next step / Source / Competitor

Files: `app/dashboard/deals/[id]/page.tsx:622-654`, `app/dashboard/deals/[id]/_components/deal-field-editor.tsx`, server `app/dashboard/deals/[id]/actions.ts:160-240`.
(`editable-detail-card.tsx` is used for the Deal Info / Relationship cards, not for these three; `summary-card.tsx` is the AI summary card — neither is part of the Context section.)

Section title: "Context" (SidebarSection, uppercase small caps). Three separate cards, in this order:

| # | Label | field | Editor | Placeholder |
|---|---|---|---|---|
| 1 | "Competitor" | competitor | single-line input | "Who else is in this deal?" |
| 2 | "Source" | source | select; options "Outbound", "Referral", "Inbound-Website", "Partner", "Other", plus empty "—" first | none |
| 3 | "Next step" | nextStep | textarea, 2 rows, no resize | "What has to happen to close?" |

Card (`deal-field-editor.tsx:66-147`): white card, border, 12px padding. Header row: label (10px, uppercase, slate-500) left; pencil icon button right (only when canEdit and not editing; aria-label "Edit <label>", e.g. "Edit Competitor") (`:72-84`); spinner shown while a save is pending (`:85-87`).

Display mode (`:137-146`): value with whitespace preserved (text-sm, slate-800). Empty value: if canEdit → placeholder text, or "Add…" when the field has no placeholder (Source) — shown in slate-400; if read-only → "—".
Click-to-edit: ONLY the pencil button enters edit mode; clicking the value text does nothing. Edit mode starts with draft = current value (`:75-77`).

Edit mode behaviour per editor:
- Input (Competitor) (`:118-134`): autofocus; Enter → save; Escape → cancel (discard draft); blur → save.
- Select (Source) (`:92-105`): autofocus; choosing an option saves immediately (`onChange`). No Esc / blur handling and no buttons (stays open until a value is picked; picking the current value closes without a call).
- Textarea (Next step) (`:106-117`): autofocus; NO key shortcuts (Enter = newline), NO blur save; two icon buttons below, right-aligned: ✕ (aria "Cancel", grey) then ✓ (aria "Save", indigo) (`:151-178`).

Save (`:43-64`): trim; empty → null; if equal to the current value → just close, no server call. Otherwise optimistic: show the new value, close the editor, call the server; on failure revert to the previous value and show a toast error with the server message.
No success toast. No client max length (inputs have no maxLength).

Server (`actions.ts:163-240`): permission manageDeals; value max 2000; messages "Invalid field update.", "Deal not found in this tenant."; activity `updated` {name, field}; score recompute.

Rebuild data: `Deal_UpdateContextField(DealId, Field next_step|source|competitor, Value)` → SavedValue. Differences: rebuild limits = column sizes NextStep 500 / Source 100 / Competitor 250 (source 2000) → set Max Length on the inputs (same as DealForm L5b) — on purpose. Source options are free strings stored as typed labels ("Inbound-Website"); MasterData DealSource (5 rows, decision 6) should match these labels — check codes vs labels before binding (source stores the label text itself). Read value from DealItem (NextStep / Source / Competitor). Toast → OutSystems feedback message (error) with UI_StripCode.

---

## 3. Tasks panel

File: `app/dashboard/_components/entity-tasks-panel.tsx` (props from `page.tsx:743-751`: entityType "deal", entityId, ownerId = deal owner, currentUserId, initialTasks, canEdit, teammates = all workspace members incl. owners).
Data: `listTasksForEntity(tenant, 'deal', id, {limit: 50})` — all non-deleted tasks of the deal, created desc, NO persona scope (`lib/tasks/queries.ts:222-244`).

### Header / states
- Empty (no open and no done tasks) (`:402-423`): a collapsed `<details>` one-liner: check-square icon, title "Tasks", grey "— none yet", chevron right (rotates when open). Expanded body: quick-add (if canEdit) + text "No tasks here yet." + (canEdit only) " Type above to capture the next action."
- Non-empty (`:425-440`): card; header left = icon + "Tasks"; right (10px, grey) = optional red badge "<n> overdue" (only when > 0) then "<open> open · <done> done".

### Quick-add (canEdit only) (`:240-398`)
Grey box at the top. Row: toggle button (plus icon; when expanded an X icon; aria "Add task details" / "Collapse task details") + text input placeholder "Add a task — Enter to create, Tab for details" + spinner while pending.
- Enter → create (title trimmed; empty = nothing).
- Tab (no Shift) with a non-empty title while collapsed → expand the details (prevents focus move).
- Collapsed create: priority medium, no due, no type, no repeat, assignee = default.
- Expanded details grid (2 cols, 3 on ≥ sm), each with a tiny uppercase label (9px) above:
  | Label | Control | Options / default |
  |---|---|---|
  | "Due date" | date | empty |
  | "Assignee" | select | "Unassigned" (value "") + each teammate display name; default = deal owner, else current user. (If no teammates: single option "Owner (default)".) |
  | "Type" | select | "—" (empty, default), "Call", "Email", "Meeting", "Internal", "Follow-up" (codes call, email, meeting, internal, followup) |
  | "Priority" | select | "Low", "Medium" (default), "High", "Urgent" |
  | "Repeat" | select | "No repeat" (default), "Daily", "Weekly", "Custom (monthly)" (codes daily, weekly, custom) |
  | "Blocks stage" (deals only) | select | "None" (default), "Lead", "Qualified", "Proposal", "Negotiation", "Hold", "Won", "Lost" |
- Buttons (right, full row): "Cancel" (text button: collapse + reset form) and "Create task" (indigo, disabled when pending or title empty).
- Status always "todo". Create is optimistic (prepended); after submit the form resets and collapses; failure → row removed + toast error (server message). Server title max 280 (rebuild 250).

### List (`:444-499`)
Open tasks (status not completed/cancelled) grouped, groups hidden when empty, in order:
- "Overdue" (red heading), "Today", "Upcoming", "No due date" — heading 10px uppercase + "(n)".
- Bucket by due date vs browser-local today: < today overdue, = today, > today upcoming, none → no due date (`:658-667`).
- Sort inside a group: due date asc (no date last), then priority urgent > high > medium > low (`:669-675`).
- Cancelled tasks are never shown.
- Done: toggle link "▸ Done (n)" / "▾ Done (n)" (collapsed by default); shows max 30, completed time desc (`:475-498`).

Row (`:563-656`), one line + optional meta line:
- Checkbox square (16px): only when canEdit (and not optimistic); empty = open, green with "✓" = done; aria "Mark complete" / "Mark incomplete". Read-only users see a grey inert square.
- Title line: priority dot (low slate, medium blue, high amber, urgent rose; tooltip "<Priority> priority"), type icon (call phone, email mail, meeting users, internal building, follow-up corner arrow), title (truncated), repeat icon when recurring (aria "Recurring"). Done rows: grey + line-through.
- Meta line (only if due or stage blocking): due chip — "<n>d overdue" (red), "Due today" / "Due tomorrow" / "Due in <n>d" (≤ 7 days, amber), else "Nov 20" (grey); stage chip "Blocks <stage code>" (violet; raw lower-case code, e.g. "Blocks proposal").
- Assignee is NOT shown on the row. Title is not a link. No edit and NO delete in this panel (full editor is on /dashboard/tasks).
- Spinner at row end while a toggle is pending.

Complete toggle (`:200-238`): optimistic flip (completed ↔ todo); failure → rollback + toast error; success with a recurring task being completed → toast success "Recurring task rolled forward to next due date" (the spawned task is not added to the panel until reload).

Rebuild data: `Task_List(EntityType "deal", EntityId, MaxRecords 50)` → TaskItem; `Task_Create(...)` with StatusCode "todo"; `Task_SetCompleted(TaskId, Completed)` (SpawnedTaskId > 0 → the toast; rebuild may re-fetch to show it); assignee options = `Member_ListOptions("")` (UserId, Label). Bucket with server `IsOverdue` + DueDate vs workspace today (`Foundation_TodayFor`) instead of browser date (decision 7). FLAGS: (a) rebuild Task_List applies persona scope (sales see only tasks assigned to them or unassigned) — source panel shows all tasks of the deal (deliberate, decision 25(2)); (b) Task_Delete exists but the source panel has no delete — do not add; (c) title max 250 vs 280.

---

## 4. Notes card

File: `app/dashboard/_components/notes-timeline.tsx` (props `page.tsx:842-849`, canWrite = canEdit). Data: `listNotesForEntity(..., {limit: 100})`, created desc (`lib/notes/queries.ts`).

- Header (`:112-123`): note-plus icon + "Notes"; right = count (10px grey).
- Composer (canWrite only) (`:125-150`): textarea 2 rows, resizable, placeholder "Add a note — context, next steps, intel from a call…"; below, right-aligned: spinner while pending + button "Add note" (indigo, disabled when empty or pending). Submit only by button (Enter = newline, no shortcut).
- Create: optimistic, prepended (newest first), draft cleared; failure → removed + toast error.
- Empty (`:152-156`): dashed box "No notes yet. Use this space to capture call notes, intel from meetings, or the next-step plan."
- Order: newest first (created desc).
- Item (`:168-219`): light box; top row = author name (12px medium) left, relative time (10px) right; body below (whitespace preserved, 13px). Pending items at 60% opacity.
  - Author fallback: lookup name, else "You" (own) or "Teammate".
  - Own notes (canWrite && author = me): hover-revealed actions "Edit" (pencil) and "Delete" (trash, rose hover). Others' notes: none.
- Edit (`:228-275`): textarea (2 rows, autofocus) prefilled; Ctrl/⌘+Enter saves, Escape cancels; buttons "Cancel (esc)" (with X icon; "(esc)" grey) and "Save (⌘⏎)". Empty body on save → just closes. Optimistic; error → toast (local text is not reverted).
- Delete: NO confirm; optimistic removal; failure → toast error.
- Updated time is not shown (no "edited" marker).
- Server: body 1–8000 ("Note can’t be empty"), "You can only edit your own notes.", "Note not found.".

Rebuild data: `Note_List("deal", id)` → NoteItem (AuthorName, CanEdit); `Note_Create`, `Note_Update`, `Note_Delete`. All covered. Use CanEdit (author) AND canEdit role for the actions.

---

## 5. Comments with @mentions

Files: `app/dashboard/_components/comments-thread.tsx`, `lib/comments/mentions.ts`, server `app/dashboard/comments/actions.ts`. Props `page.tsx:831-840` (canWrite = canEdit, teammates = all workspace members). Data: `listCommentsForEntity(..., {limit: 200})`, created ASC.

- Header (`:211-220`): message-circle icon + "Comments"; right = total count (top-level + replies).
- Empty (`:222-227`): dashed box "No comments yet. Mention a teammate with `@` to start a discussion." (the "@" in a small code chip).
- Order: top-level oldest first; replies under their parent, oldest first (`:114-131`).
- Main composer at the BOTTOM of the card (`:258-267`), only when canWrite and no reply/edit is open: placeholder "Add a comment — type @ to mention a teammate", 3 rows; button "Comment" with small "⌘⏎" hint. Ctrl/⌘+Enter submits. Button disabled when empty or pending.
- Top-level comment row (`:328-430`): avatar circle (24px, initials: first letters of up to 2 name parts split on space or "-", upper case, "?" fallback); author name (semibold 12.5px); relative time + " · edited" when edited; spinner while pending; body with mention chips. Hover actions: "Reply" (canWrite, not optimistic), and for own comments "Edit" + "Delete".
- Reply (one level only) (`:417-429`): composer indented under the comment, placeholder "Reply to <author>…", button "Reply", "Cancel" (X icon) button, 2 rows, autofocus; Escape cancels. Clicking "Reply" again toggles it closed. Server re-parents a reply-to-a-reply to the top-level ancestor.
- Reply row (`:434-511`): indented with left border, corner-arrow icon, small avatar (16px), name, time (+ " · edited"), body; hover "Edit" / "Delete" for own replies; NO Reply button on replies.
- Edit (`:513-538`): same composer prefilled, button "Save", "Cancel", 2 rows, autofocus; sets " · edited".
- Delete own: NO confirm; optimistic removal; failure → toast error. Server messages: "You can only delete your own comments.", "You can only edit your own comments.", "Comment can’t be empty" (max 8000).
- Author fallback "Teammate".

Mention picker (`:576-797`):
- Trigger: the text before the caret contains "@" that is at the start or preceded by whitespace, and the text after "@" up to the caret has no whitespace → query = that text.
- List = teammates whose display name or email contains the query (case-insensitive); empty query → first 8; max 8 shown. Hidden when no match.
- Popup ABOVE the textarea, left-aligned, 18rem wide, max height 14rem scroll; header "Mention teammate" (10px uppercase); rows = small avatar + name (medium) + email (10px grey); highlighted row indigo.
- Keys while open: ArrowDown / ArrowUp cycle (wrap), Enter or Tab insert highlighted, Escape closes the picker only. Mouse hover highlights, click inserts.
- Insert: replaces "@query" with token `@[Display Name](userId)` + one space; caret placed after the space (`mentions.ts:82-87`, `comments-thread.tsx:690-712`). The raw token stays visible in the textarea.
- Render (`:540-569`): token → chip "@Display Name"; amber chip when it is me, indigo otherwise. Pattern `@\[([^\]]+)\]\(([^)]+)\)`.
- Server: mentioned ids stored; notification to each mentioned user ≠ author.

Not in rebuild (source realtime): typing strip ("<Name> is typing…", "<A> and <B> are typing…", "<A> and <n> others are typing…") (`:248-256`, `:270-278`) and live merge of others' comments (`:82-107`) — not possible yet (no realtime).

Rebuild data: `Comment_List("deal", id)` (asc), `Comment_Create(EntityType, EntityId, Body, ParentCommentId)`, `Comment_Delete`; mention list = `Member_ListOptions(Search)` (UserId, Label, Email) — filter client-side or pass the query as Search. FLAGS: (a) **no Comment_Update** in the rebuild (source has Edit; backlog item N5 #8) — Edit button not possible until a Sales action exists; (b) CommentItem.EditedAt exists for " · edited"; (c) source teammates = every membership row (no status filter), Member_ListOptions = active members only (fine).

---

## 6. Activity timeline (merged feed + filter chips)

Files: `app/dashboard/_components/entity-timeline.tsx`, `lib/timeline/queries.ts:57-171`, `lib/activity/format.ts`, `lib/dates/relative-time.tsx`, `lib/dates/format.ts`.
Data: `getEntityTimeline(tenant, 'deal', id, {limit: 150})`: activity_logs (entity = this deal) + notes + comments + emails, merged, created DESC, top 150. Activity rows whose metadata has commentId / noteId / emailId are skipped (the note/comment/email itself is shown instead) (`queries.ts:131-136`).

Header (`entity-timeline.tsx:236-262`): left = title "Activity" + header action "Log activity" button (canEdit only; see flag); right = filter icon + chips.

Deal chip set `DEAL_ACTIVITY_FILTERS` (`:83-90`), in order — each chip = label + count (9px grey); active chip indigo-50/indigo-700:

| Chip | Shows |
|---|---|
| "All" | everything (count = all items) |
| "Emails" | kind email |
| "Calls" | activity action call_logged |
| "Meetings" | activity action meeting_logged |
| "Stage changes" | activity action stage_changed |
| "Notes" | kind note |

The deal page does NOT show "Mine", "Teammates", "Comments", "Activity", "Automation" chips (those exist in the default set `:72-81`: All, Mine, Teammates, Emails, Comments, Notes, Activity, Automation — used on other pages). Definitions for completeness: Mine = actor = me; Teammates = actor present and ≠ me; Comments = kind comment; Activity = activity rows not automation; Automation = activity with no actor or action automation_note.
Default chip: "All". Filtering is client-side.

Empty (`:264-269`): dashed box. "All" → deal text "No activity logged yet. Log a call or meeting, or take an action on this deal." (`page.tsx:712`); other chip → "No <chip label lower case> yet." ("No emails yet.", "No calls yet.", "No meetings yet.", "No stage changes yet.", "No notes yet.").

Row (`:342-383`): icon circle (20px) left; body; under it relative time (10px grey). (Deal page: no month grouping, no source chip.)
Icon / tone (`:385-436`): comment = message-circle indigo; note = sticky-note amber; email = mail sky (failed: alert rose); activity: automation (metadata.automation or automation_note) = workflow violet; call_logged = phone teal; meeting_logged = users cyan; stage_changed = arrows indigo; entity task = check-square indigo; task_completed = check-circle, owner_changed = user-cog, others = activity icon slate.

Body text:
- Comment: "**<author>** commented" + body with mention chips (same as §5).
- Note: "**<author>** added a note" + body (bodies > 280 chars → whitespace-collapsed preview, 279 chars + "…").
- Email: "**<author>** sent email to <recipients>" ("tried to send" when failed), subject, 240-char preview, status chip + "via <provider>", error + Retry when failed.
- Activity: sentence (12.5px) + optional detail line (11px grey) from `formatActivity` (`format.ts:29-221`). actor = display name, "Teammate" when unknown id, "Someone" when no actor. entityLabel on this page = "deal". name = metadata.name, else metadata.email.

| action | sentence (detail) |
|---|---|
| created | "<actor> created deal <name>" / "<actor> created a deal" |
| updated | "<actor> updated deal <name>" / "<actor> updated a deal" |
| deleted | "<actor> deleted deal <name>" / "… deleted a deal" |
| restored | "<actor> restored deal <name>" / "… restored a deal" |
| stage_changed | "<actor> moved deal <name> from <from> to <to>" (or "… to <to>", or "<actor> moved deal <name>"); no name → "<actor> moved a deal …"; detail "Reason: <reason>" when metadata.reason. from/to are the raw stage codes (lower case) |
| deal_approved | "<actor> approved deal <name>" / "<actor> approved a deal" |
| deal_rejected | "<actor> rejected deal <name> (reopened to Negotiation)" / "<actor> rejected a deal"; detail "Reason: <reason>" |
| task_completed | "<actor> completed task <taskTitle or name>" / "<actor> completed a task" |
| owner_changed | "<actor> reassigned <name or 'deal'> to <toName>" / "<actor> changed the owner on <name or 'deal'>" |
| email_sent | "<actor> sent \"<subject>\" to <recipientLabel>" (parts optional; "<actor> sent an email") |
| email_failed | "<actor> couldn't send \"<subject>\"" / "… couldn't send an email"; detail = error |
| call_logged / meeting_logged | "<actor> logged a call on Nov 13, 2026" (date from metadata.occurredAt) / "<actor> logged a call"; detail = metadata.notes |
| automation_note | "Automation: <message>" (fallback "Automation event on deal"); detail "Workflow: <workflow>" |
| invited | "<actor> invited <email> as <role>" / "<actor> invited <email>" |
| invitation_revoked | "<actor> revoked invitation to <email>" |
| member_removed | "<actor> removed <targetName or targetUserId or 'a member'>" |
| role_changed | "<actor> changed <target>'s role to <to>" / "… changed <target>'s role" |
| any other (e.g. gate_warning) | "<actor> <action with _ → space> a deal" (e.g. "Asha gate warning a deal") |

Relative time (`lib/dates/format.ts:96-112`, `relative-time.tsx`): "just now" (< 60 s), "<n>m ago", "<n>h ago", "<n>d ago" (< 7 days), else "Nov 13" (month short + day, UTC). Tooltip = absolute "Nov 13, 2026, 5:32 PM" (UTC). Re-computed every 60 s. Same component is used by Notes and Comments.

Rebuild data: `Activity_ListFor("deal", id, 150)` → TimelineItem (Kind activity/note/comment, ActorName, Action, Body, Metadata JSON text). The UI must parse Metadata (keys used: name, email, from, to, reason, taskTitle, toName, subject, recipientLabel, error, notes, occurredAt, message, workflow, automation, role, targetName, targetUserId) — build the sentence server-side or in a client action. FLAGS: (a) no Kind "email" (Comms not built) → "Emails" chip always 0 / "No emails yet."; (b) "Log activity" header button needs a call/meeting log action (source `logDealActivityAction`: Call/Meeting toggle, "Date", "Notes" placeholder "What happened on the call/meeting?", "Cancel" / "Log", toast "Call logged" / "Meeting logged") — **no rebuild server action** writes call_logged / meeting_logged → "Calls" / "Meetings" chips will be 0 until one exists; (c) check that the rebuild writes the same metadata keys (name, from, to, reason, toName) on its Deal_* activity rows, else sentences fall back to "a deal"; (d) actor display: use ActorName; empty ActorUserId → "Someone"; ActorUserId set but empty name → "Teammate".

---

## Data gaps summary (rebuild vs source)

| Piece | Gap |
|---|---|
| Comments Edit | no Comment_Update (source updateCommentAction) |
| Comments realtime | typing indicator + live merge — no realtime |
| Timeline Emails | no email kind (Comms) |
| Log activity (Calls / Meetings) | no server action for call_logged / meeting_logged |
| Tasks panel scope | rebuild Task_List persona-scoped; source panel shows all deal tasks |
| Context field lengths | 500 / 100 / 250 vs source 2000 (on purpose) |
| Source field options | source = 5 fixed labels; confirm MasterData DealSource matches before binding |
| Delete on detail page | source has none on the detail page (drawer Delete is on the list); rebuild-only placement, no confirm in source drawer |
