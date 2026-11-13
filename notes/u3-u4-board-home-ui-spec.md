# U3 Pipeline board + U4 admin / manager Home — UI parity spec

Read-only research 2026-11-13. Source = `source/src` (paths below are relative to it). Line refs are per file.
Rebuild server data already exists: `Deal_ListBoard` (Sales rev 27, N7) and `Dashboard_GetKpis` (Sales rev 26, N6), `Task_List` (rev 24, N4).
Status tags: **same** = build as source; **diff** = different on purpose (reason); **backlog** = needs data / server work the rebuild does not have.

---

# Part U3 — Pipeline board

## 1. Files
| File | Role |
|---|---|
| `app/dashboard/deals/board/page.tsx` | page shell, header, data load |
| `app/dashboard/deals/board/_components/board-view.tsx` (1062 lines) | filter bar, slipped toggle, chips row, banners, column list, drag-and-drop, modals |
| `.../_components/board-column.tsx` | one expanded column (header totals, cards, empty / load-more) |
| `.../_components/collapsed-hold-column.tsx` | slim Hold lane |
| `.../_components/deal-card.tsx` | card |
| `.../_components/slipped-only-toggle.tsx` | "Show slipped deals only" button |
| `app/dashboard/deals/_components/deal-filter-bar.tsx` | shared filter chip bar (same as Deals list) |
| `app/dashboard/_components/filter-chips.tsx` | "Filters" active-chip row |
| `app/dashboard/deals/_components/deal-view-tabs.tsx` | List / Board tabs |
| `lib/deals/slip.ts`, `lib/deals/stage-governance.ts`, `lib/deals/currency.ts`, `lib/deals/stage-probability.ts`, `lib/deals/queries.ts` 717, 914-1054 | helpers |

## 2. Page shell (`page.tsx`)
- Browser title: `"Deal board"` (page.tsx:14).
- H1 `"Pipeline"` (page.tsx:50); subtitle `"{tenant.name} — drag deals across stages."` (page.tsx:51-53).
- Right of the header: `DealViewTabs` = two tabs `"List"` (→ /dashboard/deals) and `"Board"` (→ /dashboard/deals/board), icons List / KanbanSquare; active tab indigo (deal-view-tabs.tsx:9-12). Source board header has **no** "New deal" button.
- Scope: persona sales (member, viewer) → own + unassigned deals (page.tsx:26-28). Rebuild: `Deal_ListBoard` applies persona scope server-side — **same**.
- Rebuild note: the current Salenix Pipeline screen keeps a "New deal" link in the header (L3) — **diff** (kept, CLAUDE.md L3).
- Rebuild note: subtitle — Salenix L3 uses PageHeader Subtitle = workspace name; to match source use `"<workspace> — drag deals across stages."` only if drag-and-drop is built; until then **diff**: `"<workspace>"` alone (do not promise dragging).

## 3. Layout order inside the board (board-view.tsx:859-962)
1. `DealFilterBar` (full chip bar, basePath board) — 861-866.
2. Row: `SlippedOnlyToggle` + `FilterChips` (active filters) — 868-876.
3. Error banner (red, role alert) — 879-885; notice banner (green) — 888-894.
4. Read-only banner when the role cannot move deals: `"Read-only — your role can't move deals between stages."` (897-900, amber, text-xs). Shown when `!canManageDeals(role)` (200).
5. A 1-px top horizontal scrollbar mirroring the columns (903-911) — cosmetic.
6. Columns row: horizontal flex, gap 16px, `overflow-x-auto`, columns fixed width 18rem (w-72), Hold collapsed 10rem (w-40) (927-937).
7. Modals: Lost reason, Hold reason, Rollback reason, Clear close date, Gate block panel (drag flows only).

## 4. Column order and visibility
- Order = `DEAL_STAGES` (lib/deals/schema.ts:6-14): **lead, qualified, proposal, negotiation, hold, won, lost** — all 7, Won and Lost included and NOT time-bounded (queries.ts:931-939).
- Stage filter selected → only the selected columns render (board-view.tsx:252-256).
- **Hold hidden when empty**: Hold column is removed when `stageCounts.hold == 0` unless the stage filter explicitly includes hold (board-view.tsx:257-263).
- **Hold collapsed by default** when not empty (`holdExpanded` false, board-view.tsx:329-330; rendered 784-792). After a drag onto Hold is confirmed, it auto-expands (983). There is no "collapse again" control (expanded stays until reload).
- Rebuild: `Deal_ListBoard` always returns 7 columns incl. empty Hold → UI must hide Hold when DealCount = 0 and StageCodes does not contain "hold" — **same (UI rule)**.

## 5. Expanded column header (board-column.tsx:188-238)
Left side (flex, gap 8):
- Count `{totalCount}` (text-xs, slate-500, tabular) — tooltip: when more exist than loaded `"{loaded} of {total} loaded — scroll to load more"`, else `"{total} in stage"` (190-199).
- Stage pill `{DEAL_STAGE_LABELS[stage]}` = "Lead" / "Qualified" / "Proposal" / "Negotiation" / "Hold" / "Won" / "Lost" (schema.ts:18-26), pill colours (22-33): lead slate-100/700, qualified blue-100/700, proposal indigo-100/700, negotiation amber-100/800, hold orange-100/700, won emerald-100/700, lost red-100/700.

Right side — **only when totalCount > 0** (209), right-aligned, stacked:
- Line 1 value: `formatCurrency(totalAmountMinor, workspaceCurrency)` (text-xs semibold slate-700) (211-213).
- Line 2 weighted: `"{formatCurrency(weighted)} weighted"` (10px slate-400); tooltip `"Weighted at {probability}% stage probability"` (214-219). Weighted = round(amount × probability / 100) (169).
- Line 3 slipped — **only when slippedCount > 0** (222): `"{formatCurrency(slippedWeighted)} at risk ({slippedCount} pushed)"` (10px, rose-600, medium) (232-233). Tooltip: `"{n} deal|deals in this stage have had their close date pushed at least once. {formatCurrency(slippedRaw)} raw, weighted at {probability}% stage probability."` (225-230). Slipped = push_count ≥ 1 (queries.ts:1035-1036).
- Default probabilities (stage-probability.ts:11-18): lead 10, qualified 30, proposal 60, negotiation 80, hold 40, won 100, lost 0 (workspace overrides exist in source; rebuild uses defaults — **diff**, no setting yet).

Rebuild mapping: DealCount, StageLabel, ValueMinor, WeightedMinor, ProbabilityPct, SlippedCount, SlippedWeightedMinor, SlippedMinor (tooltip) — all present.

Column body (296-341): cards gap 8px, padding 8px, min-height 6rem.
- **Empty column text**: `"Drop here"` (dashed border box, centered, text-xs slate-400) (318-322). Rebuild without drag-and-drop: **diff** → use `"No deals"` (same box) — "Drop here" would promise a gesture that does not exist. (Decide; if DnD is built later revert to "Drop here".)
- Load more: source pages 50 at a time by infinite scroll; sentinel text `"Scroll to load more"` / `"Loading more…"` (326-339). **There is no "capped at 50" text in the source.**
  Rebuild has no per-column paging (IsCapped only) → **diff / backlog**: when IsCapped show under the last card (same 11px slate-400 style) `"Showing 50 of {DealCount} — open the List view for the rest"` with "List" linking to Deals filtered by this stage; tooltip on the count as source: `"50 of {DealCount} loaded"`.
- Bulk select on Qualified column ("Select", "{n} selected", "All ({n})", "Clear", "Move to Lead", "Cancel", note "Their expected close date will be cleared — a Lead can't hold one.") is a TEMPORARY source migration tool (240-294, board-view.tsx:340-343) — **do not build**.
- Duplicate names in one column get a suffix `"{name} — {DealType label}"` (New Logo / Renewal / Expansion / Upsell), or `"#1"`, `"#2"` when no type (145-165; deal-card.tsx:104). Rebuild: BoardCardItem has no DealType → **backlog** (low); without it fall back to `"#n"` suffix or skip.

## 6. Collapsed Hold column (collapsed-hold-column.tsx)
- Width 10rem, orange tint background `orange-50/60`, border orange-200 (dashed orange-300 while dragging) (33-38).
- Header row is a **button** (whole row clickable → expand) (40-49): PauseCircle icon (orange-600) · `"Hold"` (xs medium orange-800) · `{count}` (xs semibold orange-700) · ChevronRight at the far right.
- Body (50-57): `formatCurrency(totalAmountMinor)` (xs semibold orange-800) and below `"parked · click to expand"` (10px orange-600).
- aria-label `"Hold column (collapsed)"`. No weighted / slipped lines in collapsed mode.
- Click → renders the normal expanded column for hold (board-view.tsx:791).
- Rebuild icon: Phosphor `pause-circle` + `caret-right` (THEME-GUIDE icons).

## 7. Card (deal-card.tsx)
Container (143-170): white, rounded-md, border slate-200, **left border 4px** coloured:
- stage won → emerald-500; lost → slate-400; else health band: strong emerald-500, moderate amber-400, at_risk rose-500, not scored slate-200 (75-82, 156-160).
- Slip severity **severe** adds a second 4px rose-500 inner spine on the left (175-180).
- padding 12px, text-sm, shadow-sm.

Row 1 header (182-238): left = deal name **link** (font-medium, underline on hover) → `/dashboard/deals/{id}` (198-208) — the **name is the click target**, not the whole card (the card body is the drag handle). Right = one chip:
- stage hold → `"ON HOLD"` (text `"On hold"`, uppercase 10px, orange-100/700), tooltip `"On hold — score frozen"` (211-217);
- else open stage with a health score → score number chip (`{healthScore}`, 11px semibold, band colours 84-93: strong emerald-100/700, moderate amber-100/700, at_risk rose-100/700, none slate-100/500), tooltip `"Health {score}/100 — higher is better. The red triangle chip, when present, is the separate risk score where higher is worse."` (221-229).
- Rebuild: BoardCardItem has HealthBand but **no HealthScore number** → **backlog** (add HealthScore to BoardCardItem) or **diff**: show band word chip ("Strong" / "Moderate" / "At risk")? Source shows only the number; prefer adding HealthScore (Sales, read-only change).

Row 2 (240-246): `"{companyName} · {contactName or contactEmail}"` (text-xs slate-600, truncate), only when any part exists. Rebuild: CompanyName only (no contact) → **backlog** (N7 checklist #7).

Row 3 (248-252): only when stage = won AND approvalStatus = pending → pill `"Pending approval"` (10px medium, amber-100/800). Rebuild: ApprovalStatus — **same**.

Footer (257-331), flex wrap, space-between, text-xs:
- Left: amount `formatCurrency(amountMinor, deal currency)` in **font-mono** (258-260).
- Right, in this order, each only when applicable:
  1. Close date `"MMM d"` (en-US short month + day, e.g. "Nov 21") (24-27, 262-271); slate-500, becomes **red-600 medium** when overdue. Hidden when no date.
  2. `"Overdue"` chip (rose-100/700, 10px semibold) tooltip `"Past its close date and still open"` — when close date < today and stage not won / lost (274-281; slip.ts:46-54). Rebuild IsOverdue uses the workspace date — **same**.
  3. Slip chip when pushCount ≥ 1 (282-299): text `"Pushed ×{pushCount}"` (slip.ts:88-90); severe adds a Flag icon before the text; colours watch = amber-100/800, severe = rose-100/700 (50-54); **de-weighted** = rose-100/800 + 1px inset ring rose-500 and suffix `"· 50%"` (bold) (62-64, 297).
     Tooltip (slipTooltip, slip.ts:72-85): `"Originally committed for {Mon d, yyyy}, now {Mon d, yyyy} — pushed {n}× ({d} day|days slipped)"`; when de-weighted append `" — Counted at 50% toward quota — pushed {n}× (more than 3)."` (deal-card.tsx:132-137; stage-governance.ts:260-265). Empty date → "—".
     Severity (slip.ts:13-36): none = 0 pushes; severe = pushes ≥ 3 OR totalSlipDays ≥ 30; watch otherwise. De-weighted = quota weight < 1 (pushes > 3) (stage-governance.ts:219-234).
     Rebuild: SlipSeverity, PushCount, TotalSlipDays, IsDeweighted present; tooltip needs OriginalCloseDate → **backlog** (not in BoardCardItem); interim tooltip `"Pushed {n}× ({d} days slipped)"`.
  4. Risk chip (AlertTriangle + `{riskScore}`), only risk band medium / high (304-317) — **backlog** (no DealRiskScore in BoardCardItem; risk scoring not built).
  5. Rollback chip (rollbackCount > 0) (318-329): Undo icon + `"Rolled back ×{n}"` (stage-governance.ts:268-271), violet-100/700; tooltip `"Moved backward through the funnel {n}×. Open the deal's Audit tab for the reasons."`. Rebuild RollbackCount — **same**.
- **Owner is NOT shown on the source card.** BoardCardItem has OwnerName — do not show it (**same** as source), or keep for a later tooltip.
- Source has no "Approved" badge and no stage label on cards.

Rebuild icons (Phosphor): flag, warning, arrow-u-up-left (rollback), pause-circle.

## 8. Money format
`formatCurrency(minor, code)` (lib/deals/currency.ts:43-61): `Intl.NumberFormat('en-US', currency, 2 decimals; JPY 0)`, null → "—". Example INR 12345600 → "₹123,456.00". Board totals use the workspace default currency (page.tsx:64); card amounts use each deal's currency.
Rebuild: UIKit `Money_FormatText` — check it gives en-US grouping + 2 decimals for the board; **same** target.

## 9. Filters on the board
A) `DealFilterBar` (deal-filter-bar.tsx:162-247) — one wrapped row: leading `"Filter"` label with funnel icon, then chips in this order:
`"Stage"` (popover, active label `"Stage: Lead, Qualified"`) · `"Amount"` · `"Close date"` · toggles `"My deals"`, `"Unassigned"`, `"Stale only"`, `"Awaiting approval"`, `"Approved"`, `"Won · PO missing"` · `"Health"` popover (options `"Strong (≥70)"`, `"Moderate (40–69)"`, `"Weak (<40)"`; active `"Health: Strong"`) · `"Revisit due"` · `"Slipped only"` (title `"Deals whose expected close date has been pushed out at least once"`) · `"Company"` (active `"Company: {name}"` or `"Company: {n} selected"`) · `"Owner"` (active `"Owner: {name}"` / `"{n} selected"`) · `"Clear all"` (right, only when any filter active). "My deals" and "Unassigned" are mutually exclusive (110-119). **No search box on the board** (search lives only in the Deals list toolbar).
B) `SlippedOnlyToggle` (slipped-only-toggle.tsx): button `"Show slipped deals only"` + `"({n})"` (n = Σ SlippedCount of all columns); Flag icon; tooltip `"Deals whose expected close date has been pushed out at least once"`; active = rose border/bg; **hidden when n = 0 and not active** (43). Same filter as the "Slipped only" chip.
C) `FilterChips` row (filter-chips.tsx:52-84; chips built board-view.tsx:207-246): label `"FILTERS"` (10px uppercase) then removable pills `"Stage: {n} selected"`, `"Company: {n} selected"`, `"Owner: {n} selected"`, `"Mine"`, `"Unassigned"`, `"Slipped only"` (each with ×, tooltip `"Clear {label}"`), `"Clear all"` link when > 1 chip. Hidden when no chips.

Rebuild (Deal_ListBoard inputs Search, OwnerUserId, Unassigned, StageCodes, SlippedOnly, ScoreBand, CompanyId):
| Chip | Rebuild | Status |
|---|---|---|
| Stage (multi) | StageCodes | same (reuse Deals L3 dropdown; source is multi-select) |
| My deals | OwnerUserId = acting user (GetMyContext) | same |
| Unassigned | Unassigned | same |
| Health | ScoreBand strong / moderate / at_risk | same |
| Slipped only + SlippedOnlyToggle | SlippedOnly | same (build both, or toggle only — both are source) |
| Company | CompanyId (single) | diff: single company (decision 25(6)) |
| Owner | OwnerUserId (single) | diff: single owner |
| Amount, Close date, Stale only, Awaiting approval, Approved, Won · PO missing, Revisit due | not in Deal_ListBoard (Deal_List has them) | backlog (add inputs to Deal_ListBoard) |
| FilterChips "Filters" row | UI only | same (optional; duplicates the active chip state) |

## 10. Drag-and-drop (note only)
dnd-kit; whole card is the drag handle (4px activation), name link still clickable. Drop on a column → `moveDealStage`. Lost → LostReasonModal; Hold → HoldReasonModal (reason + revisit date + note); backward move → RollbackReasonModal; dated deal back to Lead → ClearCloseDateModal; stage gate failure → GateBlockPanel. Optimistic move with rollback on error. Only roles with manageDeals.
Rebuild: **not possible yet / backlog** — use the existing StageChangeDrawer from DealDetail (or a "Move" action on the card) instead. Hide the drag affordance entirely.

## 11. Rebuild build list U3
1. Screen Pipeline: header (Pipeline, subtitle, List / Board tabs), filter chips (table §9), SlippedOnly toggle, read-only banner for viewer (HasPermission manageDeals).
2. Block BoardColumnBlock (UIKit display or Salenix data block) with header lines exactly as §5, cards, "No deals" box, capped line.
3. Block HoldCollapsedBlock (§6) + client variable HoldExpanded (default False).
4. Block BoardCard (§7).
5. Sales add-only (small): BoardCardItem + HealthScore, DealType, OriginalCloseDate, ContactName (backlog list above).

---

# Part U4 — admin / manager Home

## 12. Which home is shown (app/dashboard/page.tsx)
- One route `/dashboard`, no redirects (page.tsx:17-26). `personaOf(role)` (lib/personas/index.ts:22-35): owner, admin → **admin**; manager → **manager**; member, viewer, none → **sales**.
- admin → `AdminHome` (with `?view=` and `?range=`) (46-54); manager → `ManagerHome` (only `?view=`, **no range picker**, fixed 90 days) (56-64); else `SalesHome` (66-70).
- Rebuild: Persona from Foundation_GetMyContext — same 3-way switch.

## 13. AdminHome section order (admin-home.tsx:206-316)
| # | Section | Lines | Rebuild data | Status |
|---|---|---|---|---|
| 1 | Greeting | 208-213 | profile first name, workspace name | same |
| 2 | Toolbar: RepFilter · DashboardRangeFilter · record counts (right) | 215-225 | Member_ListOptions, RangeCode / FromDate / ToDate, Company/Contact/ActiveDealCount | same |
| 3 | KPI strip | 227-232 | Dashboard_GetKpis | partly (no deltas, no quota / coverage) |
| 4 | Revenue trend chart | 234-246 | RevenueTrend | same |
| 5 | Pipeline by stage bars (2/3 width) + Stage donut (1/3) | 248-282 | StageTotals | same |
| 6 | Forecast breakdown (only if flag forecastsEnabled) | 284-296 | none | backlog |
| 7 | Key deal highlights | 298-305 | none | backlog |
| 8 | Operations status strip | 307-309 | none | backlog |
| 9 | AI signals card + Admin shortcuts | 311-314 | none / links | backlog / partial |
Container: max-width 88rem, padding 24px; sections separated by 16px (mt-4).

### 13.1 Greeting (home/greeting.tsx)
- H1 (20px semibold): `"{tod}, {firstName}."` or `"{tod}."` when no name (42-45). tod by hour: < 5 `"Hello"`, < 12 `"Good morning"`, < 17 `"Good afternoon"`, else `"Good evening"` (23-31) — source uses **server** clock; rebuild: hour of Foundation_NowFor (workspace time) — **diff (better)**, build the text on the server.
- Line below (12px): `{workspaceName}` (medium slate-700) · `"·"` · persona pill uppercase 10px bordered: admin `"Administrator"` (admin-home.tsx:211), manager `"Manager"` (manager-home.tsx:201).

### 13.2 RepFilter (home/rep-filter.tsx:35-60)
Rounded-full bordered label: Users icon · `"Viewing"` · native select: first option `"Whole team"` (value team), then every teammate `{displayName}`. Changing it sets `?view=rep:<id>` and keeps the range. Rebuild: dropdown fed by Member_ListOptions; value → OwnerUserId (empty = whole team) — **same**. Phosphor icon `users`.
When a rep is selected, titles switch to `"{repName} · revenue trend"` etc. (scopeLabel, admin-home.tsx:189-190; fallback `"Selected rep"`).

### 13.3 DashboardRangeFilter (dashboard-range-filter.tsx:69-138; lib/dashboard/range.ts)
- Segmented pill (rounded-full, border, 12px): buttons `"7d"`, `"30d"`, `"90d"`, `"QTD"`, `"YTD"` (range.ts:31-38) then `"Custom"` with CalendarRange icon. Active segment = indigo-600 bg, white text.
- Default preset **last_90d** (range.ts:40).
- Custom opens an inline pill: date input (aria "Range start") `"→"` date input ("Range end") + button `"Apply"` (disabled when empty or end < start) (105-133). Custom ≤ 730 days (range.ts:84-101).
- After the control: range label text (11px slate-500) = `"Last 7 days"`, `"Last 30 days"`, `"Last 90 days"`, `"This quarter"`, `"This year"` (range.ts:110-116), custom `"Nov 1, 2026 – Nov 13, 2026"` (en-US, 138-147).
- Windows (lib/reports/filters.ts:57-82): last_Nd = today − N days .. tomorrow; this_quarter = calendar quarter start .. next quarter start (full quarter incl. future days); this_year = Jan 1 .. next Jan 1. Rebuild: same codes; RangeLabel output — **same** (workspace date instead of UTC).

### 13.4 Record counts (admin-home.tsx:220-224, 362-371)
Right end of the toolbar, hidden on phone (`hidden sm:flex`), 11px slate-500: `"{n} companies"` `"{n} contacts"` `"{n} active deals"` — number bold mono slate-700 with en-US grouping (`toLocaleString`). Not tiles. Active deals = not won / lost (lib/dashboard/queries.ts:20-21). Rebuild CompanyCount / ContactCount / ActiveDealCount — **same**; tenant-wide (do not follow the rep filter — source getDashboardOverview has no owner input).

## 14. KPI strip (app/dashboard/_components/kpi-strip.tsx)
Grid: 2 cols phone, 3 sm, **5 lg** with the 5 base tiles (342-350). aria-label `"Key metrics · {range label}"`.
Tile (360-407): white card, rounded-lg, 2px left tint bar; lines:
1. Label (10px uppercase semibold slate-500).
2. Value (mono, 13–19px clamp, semibold) + DeltaChip on the same line.
3. Sub (10.5px slate-500).
4. Scope line (9.5px uppercase slate-400, top border).
5. Footnote (10px amber-700) — quota / coverage only.
Tooltip on the whole tile = `title`.

| # | Label | Value | Sub | Scope | Tint | Rebuild |
|---|---|---|---|---|---|---|
| 1 | `"Total pipeline"` | money(pipeline) | `"vs {money(prior)} at period start"` | `"Open deals, as of today"` | indigo | TotalPipelineMinor; sub needs prior → **diff**: sub `"open deals"` until backlog |
| 2 | `"Weighted pipeline"` | money(weighted) | `"Σ amount × stage win probability"` | `"Open deals, as of today"` | violet | WeightedPipelineMinor — same |
| 3 | `"Win rate"` | `"{round(rate×100)}%"`, `"—"` when nothing closed | `"{won} won · {lost} lost"` or `"no deals closed in period"` | `"Deals closed in {range label lower}"` | emerald | WinRatePct (round to integer for the value), WonCount, LostCount — same |
| 4 | `"Avg deal size"` | money(avg) or `"—"` when 0 wins | `"won deals in period"` / `"no wins in period"` | `"Deals won in {range lower}"` | sky | AvgDealSizeMinor — same |
| 5 | `"Avg sales cycle"` | `"{n}d"` or `"—"` when 0 | `"created → won"` | `"Deals won in {range lower}"` | amber | AvgCycleDays (source shows the stored number; rebuild has 1 decimal → show e.g. "23.5d", same as source which also keeps 1 decimal) — same |
| 6 | `"Quota attainment · {FY label}"` | `"{pct}%"` cap `"999%+"` | `"{achieved} of {target}"` | `"Fiscal year to date · {FY}"` | slate | **backlog** (SalesTarget in Finance) — omit tile (source also omits when no target) |
| 7 | `"Coverage ratio"` | `"{x.x}x"` or `"Met"` | `"{open} open vs {gap} to go"` / `"target already closed"` | `"Open pipeline today vs remaining {FY} quota"` | emerald/amber/slate | **backlog** — omit |

Tooltips (lines 151, 166, 187-189, 209-212, 228): e.g. Win rate `"{x.x}% of the {n} deals that CLOSED in {range} were won. Counts only deals with a close date in the window — not every won deal the workspace has ever recorded, which is what the stage chart below shows."`; nothing closed: `"No deals closed in {range}, so there is no win rate to report. This is not a 0% win rate."`; Avg deal `"Average won deal: {exact}"`; Avg cycle `"Mean days from deal creation to close, across the {n} deals won in {range}."`; pipeline / weighted tooltips mention the delta → rebuild drop the delta sentence: `"Open deal value right now: {exact}."` / `"Weighted value right now: {exact}."`.

Money format in tiles (124-128): compact (`"₹51L"`, `"$1.2M"`) only when OrganizationSettings.compactNumbers (default **false**, db/schema/organization-settings.ts:70); otherwise standard with **0 decimals** in the workspace locale (lib/currency/format.ts:59-82) — e.g. en-IN INR `"₹1,23,456"`. Note: this differs from the board (en-US, 2 decimals).

DeltaChip (409-466) — **backlog** (no prior-period data): source shows `"n/a"` (Minus icon, slate-400) when no delta; arrows + `"+12%"`, `"+4.0 pts"`, `"New this period"`, `"flat"` (lib/dashboard/delta.ts:145-160), green / rose by `higherIsBetter` (cycle: lower is better). Rebuild interim: **omit the chip** (do not show "n/a" on every tile) — diff, reason: no prior figures.

## 15. Revenue trend chart (revenue-trend-chart.tsx; lib/dashboard/trend-shaping.ts)
Default config (lib/chart-studio/config.ts:148-159): variant **weekly**, metric revenue, legend on, smooth lines, lost line on, not cumulative. Chart Studio (gear button, ⌘E, saved per user in ChartView) — **backlog** (ChartView entity exists in Foundation, no UI).
- Card: rounded-2xl, white, border.
- Header (240-298): badge row: emerald pill TrendingUp icon `"{days}-day trend"` + bordered pill `"REVENUE"` (metric label); H2 (15px semibold) title; subtitle (11.5px slate-500).
  - Admin title `"Organization revenue trend"`, rep selected `"{rep} · revenue trend"`; subtitle `"{range label} · weekly buckets · {workspace}"` (admin-home.tsx:238-241).
  - Manager title `"Team revenue trend"` / `"{rep} · revenue trend"`, subtitle `"Last 90 days · weekly buckets · {workspace}"` (manager-home.tsx:272-273).
  - `{days}` = whole days of the range (range.ts:129) — rebuild: DiffDays(RangeFrom, RangeTo) (+1 if RangeTo inclusive).
- Body height 280px. Weekly composed chart (WeeklyBody 860-978):
  - X axis = week label `"Nov 3"` (Monday of ISO week, en-US short, trend-shaping.ts:60-65, 167-173), tick 10px slate-400, no axis line, dashed horizontal grid.
  - **Bars** `New pipeline` on the LEFT axis, fill **#7d0712** (maroon) opacity 0.75, radius 2.
  - **Line** `Won revenue` on the RIGHT axis, **#10b981** (emerald) 2px, dots r 2.5.
  - **Line** `Lost revenue` on the RIGHT axis, **#f43f5e** (rose) 1.25px dashed 4 4, no dots.
  - Axis ticks: `"1.2M"`, `"45k"`, `"0"`, else full money (602-628).
  - **95th-percentile cap** (trend-shaping.ts:77-94, 106-165): per axis independently (left = new pipeline weeks; right = won + lost values together). Only when ≥ 4 non-zero weeks AND max > 3 × median(non-zero): cap = max(p95(non-zero, linear interpolation), median); applies only if cap < max. Capped bar is drawn at the cap with a zig-zag top edge (4 teeth, outline #4338ca) (1001-1037); lines are clipped at the cap.
  - Tooltip (WeeklyTooltip 1039-1086): dark card; heading `"WEEK OF {Mon, Nov 3, 2026}"` (weekday short, en-US); rows with coloured dots: `"New pipeline"` {money}, `"Won revenue"` {money}, `"Lost revenue"` {money}; when the week is capped: amber note `"Values above are the real figures — this week runs past the chart axis, which is capped so the other weeks stay readable."`.
- Legend footer (325-343): dots + `"New pipeline"` (dot class bg-indigo-500 — note: legend dot is indigo while the bar is maroon, a source inconsistency; rebuild: use the bar colour for the dot — diff, cosmetic), `"Won revenue"` (emerald), `"Lost revenue"` (rose, dashed); right `"⌘E while focused → Chart Studio"` (drop in rebuild).
- Empty state (when every week is 0) (302-308): dashed box `"No revenue activity in the last {days} days. Once deals start moving, the trend will fill in here automatically."`.
- Values: money in tooltip uses tenant formatter (compact if set, else 0 decimals) (147-153).
Rebuild: TrendWeekItem (WeekStart, WonRevenueMinor, NewPipelineMinor, + counts for a future "Deal count" metric) — **same data**. ODC chart: OutSystems UI Charts (Highcharts) ColumnChart + LineChart combo with two Y axes via AdvancedFormat JSON; cap computed client-side (JS) — feasible; if dual axis is not workable, **diff**: one axis + cap.

## 16. Stage bars (stage-conversion-bars.tsx) — default variant bars, metric revenue
- Admin title `"Organization pipeline by stage — live snapshot"` / `"{rep} pipeline by stage — live snapshot"`; subtitle `"Every deal open right now, all time · does not follow the date filter above"` (admin-home.tsx:253-258). Manager: `"Team pipeline by stage — live snapshot"`, subtitle `"Every deal open right now, all time · not the 90-day window above"` (manager-home.tsx:285-290).
- Badge `"SNAPSHOT · REVENUE"` (ArrowDown icon, indigo pill) (302-305).
- Rows = **Lead, Qualified, Proposal, Negotiation, Won** (no Hold, no Lost) (104-118). Note: Won is all-time won value, despite "open right now" (source quirk; keep).
- Column header row (9.5px uppercase slate-400): `"Stage"` · bar · `"Revenue · deals"` · `"Close prob"` (title `"Configured win probability for the stage (Settings → Pipeline). Forward-looking, not an observed rate."`) · `"Share of / stage above"` (two lines) (397-422).
- Each row (433-515): stage label (10.5px uppercase semibold) · horizontal bar (h 10px, rounded, track slate-100, width = value / max, min 2%) · value money (mono 12.5px) + `"{n} deal|deals"` · `"{prob}%"` (tooltip `"Deals in {Stage} are weighted at {p}% win probability."`) · ratio chip `"{round(v/prev×100)}%"` (cap `"999%+"`), tone ≥ 60 good emerald, ≥ 30 warn amber, else bad rose; first row `"—"` (title `"First stage — nothing above it to compare against."`).
- Bar colours (137-174): lead indigo-200 #c7d2fe, qualified indigo-300 #a5b4fc, proposal violet-400 #a78bfa, negotiation amber-400 #fcd34d, won emerald-500.
- Empty text (other variants, 722): `"No data yet — once deals enter the pipeline, the breakdown will appear."` (bars variant shows zero rows instead).
- Range-independent, follows the rep filter (admin-home.tsx:68-82). Rebuild: StageTotals (all time, owner filter) — **same**. Plain HTML/CSS bars (no chart widget) recommended.

## 17. Stage donut (stage-donut.tsx) — default donut, metric dealCount
- Admin title `"Organization-wide — deal counts, all time"` / `"{rep} — deal counts, all time"`; subtitle `"Every deal on record by stage, all owners · does not follow the date filter"` / rep: `"Every deal on record by stage · does not follow the date filter"` (admin-home.tsx:268-277). Manager: `"Team — deal counts, all time"`, subtitle `"Every deal on record by stage · not the 90-day window above"`.
- Badge `"COUNTS"` (PieChart icon); header right: `"TOTAL DEALS"` + total (mono xl) (199-232).
- Donut: all 7 stages in order lead … lost, zero slices skipped; centre `{total}` + `"TOTAL"` (364-371); slice tooltip `"{Stage}: {n} ({pct}%)"`.
- Legend list right of the donut: dot · stage label · count (bold mono) · `"{pct}%"` (rounded) — only non-zero stages (375-405).
- Colours (78-90): lead #c7d2fe, qualified #a5b4fc, proposal #a78bfa, negotiation #fcd34d, hold #fdba74, won #6ee7b7, lost #fca5a5.
- Empty (total 0) (236-239): `"No data yet — once deals enter the pipeline, the breakdown will appear."`.
Rebuild: StageTotals counts; OutSystems UI PieChart (donut via AdvancedFormat innerSize) or CSS conic-gradient — **same data**.

## 18. Admin sections without rebuild data (backlog, note only)
- ForecastBreakdownCard (only when forecastsEnabled) — period = fiscal quarter.
- DealHighlightsCard: badge `"HIGHLIGHTS"`, title `"Key deal highlights"`, subtitle `"{range label} · movement worth knowing about without opening Pipeline"`, tabs `"Big wins"`, `"Progressed"`, `"New deals"`, `"At risk"`, `"Closing soon"`, link `"View all"` (deal-highlights.tsx:36-40, 80-100).
- OpsStatusStrip: `"All systems normal"` / `"View Operations"` (ops-status-strip.tsx:109-159).
- AISignalsCard: badge `"INTELLIGENCE"`, title `"AI signals"`, link `"Open intelligence"`, tiles `"Recommendations"`, `"Risk flags"`, `"AI calls · 24h"`, `"Tokens · 24h"` (admin-home.tsx:377-418).
- AdminQuickLinks `"Admin shortcuts"`: `"Command Center"` / `"Operational intelligence at a glance"`, `"Operations"` / `"Workflow, AI, mail provider health"`, `"Workflows"` / `"Run history & retry triage"`, `"Settings"` / `"Org-wide controls"` (admin-home.tsx:457-517). Rebuild: only "Settings" has a target (Salenix_Admin, later) — show nothing now.
- **Admin home has no "My tasks" block** in the source.

## 19. ManagerHome (manager-home.tsx) — summary
Order: Greeting ("Manager") · RepFilter + ViewToggle pill `"Team view"` (link ?view=team) (204-210) · 8 compact KPI tiles (213-262) · Revenue trend (fixed last 90 days) · Stage bars + donut · Pipeline trend strip + Rep leaderboard (top 6) · Stale deals card (14 days) + Communication responsiveness (`"Active threads"`, `"Unread"`, `"Failed"`) · Forecast card (6 months) + AI coaching.
KPI tiles (label / value / sub): `"Team pipeline"` / money / `"open stages"` · `"Weighted forecast"` / money / `"{n} open deals"` · `"Team revenue · 90d"` / money / `"{n} won"` · `"Conversion"` / `"{pct}%"` / `"{n} lost"` · `"Active reps"` / n / `"of {reps}"` · `"Avg cycle"` / `"{n}d"` or `"—"` / `"to close"` · `"Stalled"` / n / `"14d+ idle"` (rose if > 0) · `"At risk"` / n / `"AI-flagged"` (rose if > 0). Manager KPI tiles are **team-level always** (not affected by the rep picker, 212).
Rebuild proposal (decide): manager uses the admin layout (§13–17) with title prefix "Team" and the range fixed to last_90d (no range picker) — **diff** (one Home block for admin + manager; manager-only widgets Rep leaderboard, Stale, Forecast, Comms, AI = backlog). Active reps / Stalled / At risk need data the rebuild lacks (Stalled could use Deal_List StaleOnly StaleDays 14 → TotalCount).

## 20. "My tasks" block (SalesHome only — intel-widgets.tsx:641-704; lib/tasks/queries.ts:264-330)
Shown on SalesHome (sales-home.tsx:154), not on admin / manager homes. Spec for when the sales home gets it:
- Header: CheckSquare icon (indigo) + H2 `"My tasks"` (13px semibold); right link `"All my tasks →"` → tasks list filtered to me.
- 3 stats: `"Open"` (open = status not completed / cancelled, assigned to me), `"Overdue"` (open AND due < today; rose when > 0), `"Done · 7d"` (completed in the last 7 days; emerald).
- List: top 5 open tasks, order: has due date first, due asc, priority urgent → low. Each row (link to the tasks list): priority pill `"Low"` / `"Medium"` / `"High"` / `"Urgent"` (slate / blue / amber / rose), title, due text: overdue `"{n}d late"` (rose), else `"today"`, `"tmrw"`, `"{n}d"` (≤ 7), else `"Nov 21"`.
- Empty (no open, no upcoming): `"Nothing on your plate. Create a task on a deal or directly from /dashboard/tasks to track your next action."`.
Rebuild: Task_List(Mine True, StatusCodes "todo,in_progress", MaxRecords 5) → Items + TotalCount (Open); Task_List(Mine, Overdue True) → TotalCount (Overdue); **"Done · 7d" backlog** (no completed-since filter); order of Task_List = created desc (source: due asc / priority) → **diff / backlog** (add a SortBy "due" to Task_List). Note Task_List Mine uses persona scope; for admins Mine = assigned to me — same.

## 21. Data gaps summary (rebuild lacks)
| Gap | Needed for | Owner |
|---|---|---|
| Prior-period values (pipeline at range start, prior win rate / avg deal / cycle) | KPI delta chips, Total pipeline sub | Sales (stage-history replay) |
| SalesTarget / quota credit, FY label | Quota attainment + Coverage tiles | Finance |
| Deal highlights (5 lists) | Highlights card | Sales |
| Workspace stage probabilities | weighted values everywhere | Foundation OrganizationSettings |
| compactNumbers setting, currency locale | KPI money format | Foundation OrganizationSettings (read action) |
| ChartView save | Chart Studio | Foundation (UI later) |
| Board card HealthScore, DealType, ContactName, OriginalCloseDate, risk score | card chip, duplicate suffix, row 2, slip tooltip, risk chip | Sales (add-only to BoardCardItem) |
| Board per-column paging | "Scroll to load more" | Sales (StartIndex per stage) |
| Deal_ListBoard filters amount / close / stale / approval / PO / revisit | full filter bar on the board | Sales |
| Task "done in 7 days" count, due-date sort | My tasks block | Sales |
| Manager widgets: rep leaderboard, active reps, stalled count, forecast, comms | ManagerHome | Sales / Comms / Agent |
