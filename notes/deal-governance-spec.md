# Deal governance spec (port of source triggers + deal server actions)

Scope: Salenix_Sales `Deal_*` actions and stage helper functions. Source = `source/` (read-only, code wins). All citations are `file:line` relative to `source/` unless prefixed `BRD-App` (= `Salenix-BRD-Application-Logic.md`) or `BRD-DB` (= `Salenix-BRD-Database.md`).
Short names: `SG` = `src/lib/deals/stage-governance.ts`, `VAL` = `src/lib/deals/validation.ts`, `SCH` = `src/lib/deals/schema.ts`, `PROB` = `src/lib/deals/stage-probability.ts`, `FC` = `src/lib/deals/forecast/categories.ts`, `APR` = `src/lib/deals/approval.ts`, `ACT` = `src/app/dashboard/deals/actions.ts`, `OPS` = `src/lib/order-proofs/shared.ts`, `OPC` = `src/lib/order-proofs/check.ts`, `M45/M58/M60/M71` = `drizzle/0045_deal_detail_expansion.sql` / `0058_deal_close_date_pushback.sql` / `0060_deal_stage_governance.sql` / `0071_order_proofs.sql`.

Input normalisation for every helper in OutSystems: `Code = ToLower(Trim(Stage))` (Salenix addendum). The source compares exact, case-sensitive strings (e.g. `SG:51`, `VAL:22`), so `"Won"` is an unknown stage there; after our normalisation it is `won`.

---

## 1. Stage helpers (server functions)

### 1.1 Definitions

| OS function | Source | Exact logic | Notes |
|---|---|---|---|
| `Stage_GetRank(Stage) → Integer` | `SG:31-37` `FUNNEL_STAGE_ORDER`, `SG:41-52` `stageRank`; SQL mirror `M60:99-108` `deal_stage_rank` | lead 1, qualified 2, proposal 3, negotiation 4, won 5, anything else → null | OS has no null Integer: return **0** = unranked. `IsRollback = Old>0 AND New>0 AND New<Old` (`SG:58-62`, `M60:184-186`); `IsForward = Old>0 AND New>0 AND New>Old` (`SG:65-69`). |
| `Stage_IsClosed(Stage) → Boolean` | inline in `ACT:717-718` (`isClosedStage`) and `ACT:1179-1180` (`isClosed`) | `stage = 'won' OR stage = 'lost'` | Drives `ClosedAt`. No shared function in source. |
| `Stage_IsParked(Stage) → Boolean` | `SG:21-29` (comment: hold/lost are "PARKING states"); no function in source | `stage IN ('hold','lost')` | Not `Rank = 0`: unknown codes are also rank 0 but are not parked. |
| `Stage_NeedsCloseDate(Stage) → Boolean` | `SG:149-158` `CLOSE_DATE_REQUIRED_STAGES` / `closeDateRequiredForStage`; DB check `M60:80-91` | `stage IN ('proposal','negotiation','won')` | R1. "Missing" = null or blank after trim (`SG:164-173`). |
| `Stage_ForbidsCloseDate(Stage)` (R0, extra helper) | `VAL:14-27` `violatesLeadCloseDate` | `stage = 'lead'` AND date non-blank → violation | Message `VAL:6-7`. |
| `Stage_NeedsMargin(Stage) → Boolean` | `VAL:38` `MARGIN_OPTIONAL_STAGES = ['lead','hold','lost']`, `VAL:41-43` `marginRequiredForStage` | `NOT (stage IN ('lead','hold','lost'))` | **Unknown/empty → True** (negated list). Violation = margin not a number > 0 (`VAL:50-55`). |
| `Stage_DefaultProbability(Stage) → Integer` | `PROB:11-19` `DEFAULT_STAGE_PROBABILITY`; overrides `PROB:24-30`, `PROB:36-49`; unknown → `ELSE 0` in `src/lib/deals/forecast/rollup.ts:163-171` | lead 10, qualified 30, proposal 60, negotiation 80, hold 40, won 100, lost 0, else 0 | Tenant override (`organization_settings.stage_probabilities`, `src/db/schema/organization-settings.ts:87`) only for lead/qualified/proposal/negotiation/hold, accepted only if finite number 0–100 (`PROB:42-44`); won/lost are fixed. Suggest a second function `Stage_ProbabilityFor(TenantId, Stage)` that applies overrides. `deals.probability` is **not** an input (see §4 D13). |
| `Forecast_CategoryFor(Stage) → Text` | `FC:39-56` `deriveForecastCategory`; SQL mirror `rollup.ts:174-180` | lead/qualified → pipeline; proposal → best_case; negotiation → commit; won → closed_won; lost/hold → omitted; default → pipeline | Inputs are **stage only**. Probability is never used; the override flag decides whether the derived value is written (see 1.3). |

Labels (`SCH:18-26`): Lead, Qualified, Proposal, Negotiation, Hold, Won, Lost — used in `closeDateRequiredMessage` (`SG:176-179`, unknown stage → raw code) and gate `blockMessage` (`src/lib/stage-gates/enforce.ts:51-58`).

### 1.2 Self-check table (one row per input)

| Input (after Trim+ToLower) | GetRank | IsClosed | IsParked | NeedsCloseDate | ForbidsCloseDate | NeedsMargin | DefaultProbability | Forecast_CategoryFor |
|---|---|---|---|---|---|---|---|---|
| lead | 1 | F | F | F | **T** | F | 10 | pipeline |
| qualified | 2 | F | F | F | F | T | 30 | pipeline |
| proposal | 3 | F | F | T | F | T | 60 | best_case |
| negotiation | 4 | F | F | T | F | T | 80 | commit |
| hold | 0 | F | T | F | F | F | 40 | omitted |
| won | 5 | T | F | T | F | T | 100 | closed_won |
| lost | 0 | T | T | F | F | F | 0 | omitted |
| `xyz` (unknown) | 0 | F | F | F | F | **T** | 0 | pipeline |
| `""` (empty) | 0 | F | F | F | F | **T** | 0 | pipeline |
| `" WON "` | 5 | T | F | T | F | T | 100 | closed_won (source would treat as unknown) |

Rollback self-check (`Stage_IsRollback(From, To)`): negotiation→qualified T; won→negotiation T; qualified→lead T; proposal→hold F; hold→lead F; lost→qualified F; won→lost F; lead→won F (forward); same stage F.

### 1.3 Forecast category — inputs and write rule

Stored on the deal: `ForecastCategory` (text, may be empty for legacy rows) and `ForecastCategoryOverridden` (bool, default False) (`src/db/schema/deals.ts:171-174`).

| Event | Category written | Overridden written | Source |
|---|---|---|---|
| Deal_Create | `Forecast_CategoryFor(Stage)` | False (default) | `ACT:512-513` |
| Stage change, Overridden = False | `Forecast_CategoryFor(NewStage)` | unchanged | `ACT:912-915`, `ACT:1442-1444`, `src/lib/deals/revert-to-lead.ts:111-113` |
| Stage change, Overridden = True | unchanged | unchanged | same lines |
| Reject (won→negotiation) | **unchanged** (source writes nothing → stays `closed_won`) | unchanged | `ACT:1773-1782`, `ACT:1970-1979` — see §4 D10 |
| Manual set to X (`manageForecast`) | X | True | `src/app/dashboard/deals/[id]/actions.ts:291-300` |
| Manual set `auto` | `Forecast_CategoryFor(current Stage)` | False | same |
| Read (effective) | Stored if not empty, else `Forecast_CategoryFor(Stage)` | — | `rollup.ts:172-180` |

Full truth table for the stage-change write (`NewCategory = If(Overridden, Stored, Forecast_CategoryFor(NewStage))`):

| NewStage | Overridden=F (any Stored) | Overridden=T, Stored=commit | Overridden=T, Stored="" |
|---|---|---|---|
| lead | pipeline | commit | "" (read → pipeline) |
| qualified | pipeline | commit | "" (read → pipeline) |
| proposal | best_case | commit | "" (read → best_case) |
| negotiation | commit | commit | "" (read → commit) |
| hold | omitted | commit | "" (read → omitted) |
| won | closed_won | commit | "" (read → closed_won) |
| lost | omitted | commit | "" (read → omitted) |
| unknown/empty | pipeline | commit | "" (read → pipeline) |

Rollup use (not written to the deal): overdue = stage NOT IN (won, lost, hold) AND close date < today (`rollup.ts:181-182`, uses DB `CURRENT_DATE`, i.e. UTC — we use `Foundation_TodayFor`); Commit = commit + closed_won (`FC:59`); Best case = commit + closed_won + best_case (`FC:62-66`).

---

## 2. Deal governance

### 2.0 What the database did (port into one private `Deal_ApplyGovernance` called by every Deal write)

Trigger firing order on an UPDATE of `deals` (BEFORE triggers fire in name order, `M60:34-38`): `trg_deal_close_date_pushback_upd` → `trg_deal_governance_upd` → `trg_deal_order_proof_check`; then AFTER `trg_deal_stage_history_upd`. On INSERT: `trg_deal_close_date_pushback_ins`, `trg_deal_order_proof_check` (BEFORE), `trg_deal_stage_history_ins` (AFTER). The governance trigger is UPDATE-only (`M60:280-282`): **no audit row and no governance init on insert**.

**G1 Close date (`M58:103-177`)** — fires when `expected_close_date` is in the SET list (`M58:187-189`).
1. INSERT: `OriginalCloseDate = ExpectedCloseDate` (may be null), `PushCount = 0`, `LastPushedAt = null`, `TotalSlipDays = 0`; **no DealDateChange row** (`M58:109-117`).
2. UPDATE, date not distinct (null = null counts as equal): keep all four derived values, no row (`M58:122-128`).
3. `OriginalCloseDate = Coalesce(old.OriginalCloseDate, new.ExpectedCloseDate)` — write-once, survives a later clear (`M58:132-133`).
4. `IsPush = old.Date <> null AND new.Date <> null AND new.Date > old.Date` (`M58:137-139`).
5. IsPush → `PushCount = old.PushCount + 1`, `LastPushedAt = now`; else both unchanged (`M58:141-147`).
6. `TotalSlipDays = 0 if new.Date null or OriginalCloseDate null, else Max(0, DiffDays(OriginalCloseDate, new.Date))` — recomputed on every change, so a pull-in can lower it (`M58:149-153`).
7. Insert `DealDateChange(TenantId, DealId, OldCloseDate, NewCloseDate, ChangedBy = actor or null, ChangedAt = now, Reason = trimmed, empty→null, Left 1000, IsPush)` (`M58:155-173`). Reason comes only from the inline patch (`ACT:2369-2377`), trimmed and cut to **500** by `src/lib/deals/close-date-reason.ts:27-33`; full edit and stage move send none.

**G2 Governance (`M60:160-275`)** — fires when stage, expected_close_date or amount_minor is in the SET list (`M60:281`).
1. Stage distinct: `OldRank/NewRank = Stage_GetRank`; `IsRollback` as in 1.1 (`M60:179-186`).
   - IsRollback and ReasonCode empty (trim) → abort: `ROLLBACK_REASON_REQUIRED: moving % from % to % needs a reason code` (`M60:192-197`).
   - IsRollback → `RollbackCount = old + 1`, ChangeType `rollback` (`M60:198-199`); both ranked → `forward_progress` (`M60:200-201`); else (to/from hold, lost) → `edit` (`M60:202-206`).
   - `LastStageChangedAt = now` (`M60:208`).
   - Insert `DealAuditLog(FieldName 'stage', OldValue old code, NewValue new code, ChangeType, ReasonCode = Left(code,100) and ReasonNote = Left(note,1000) **only when rollback**, else null)` (`M60:210-220`).
   - Stage not distinct → RollbackCount, LastStageChangedAt unchanged (`M60:221-224`).
2. Date distinct: ChangeType `pushback` if both set and new > old, else `edit` (first date, pull-in, clear) → insert audit row `expected_close_date`, values as `YYYY-MM-DD` text or null, no reason (`M60:231-249`).
3. Amount distinct → audit row `amount_minor`, values as integer text, ChangeType `edit` (`M60:252-262`).
4. `QuotaWeight = 0.50 if Coalesce(PushCount,0) > 3 else 1.00` (strictly greater: 3 pushes = 1.00, 4th push = 0.50) (`M60:268-271`; `SG:219-229`). Uses the PushCount from G1.
Audit rows: append-only (`M60:306-317`, `M60:357-361`). ChangedBy = acting user (GUC `app.user_id`, `M60:170`). Vocabulary: field names `SG:278-282`, change types `SG:285-290`, labels `SG:293-304`.

**G3 Stage history (`M45:134-158`)** — INSERT: open row `(Stage, EnteredAt = Deal.CreatedAt, ExitedAt null)` (`M45:136-139`). UPDATE with stage distinct: set `ExitedAt = now` on the open row, insert new open row `EnteredAt = now` (`M45:141-146`). Invariant: max 1 open row per deal (`M45:102-103`).

**G4 Order-proof backstop (`M71:96-133`)** — skip when stage ≠ won or deal deleted (`M71:105-107`); skip on UPDATE when old stage = won and amount and currency unchanged (`M71:110-115`); else `count(proofs)`, `count(proofs with currency ≠ deal currency)`, `sum(SubtotalMinor of proofs in deal currency)`; fail when `count = 0 OR other-currency > 0 OR Abs(sum − AmountMinor) > 100` → `ORDER_PROOF_REQUIRED: …` (`M71:117-130`). Fires on INSERT too, so a deal can never be inserted at Won. Same rule in app code: `OPS:46-59` (`ORDER_PROOF_ROUNDING_MINOR = 100`, `OPS:14`).

**G5 DB check `chk_close_date_required`** (`M60:80-91`, NOT VALID): stage in (proposal, negotiation, won) ⇒ date not null. App maps it to `closeDateRequiredMessage` (`ACT:1065-1072`, `ACT:2444-2449`).

Derived columns the app never writes: OriginalCloseDate, PushCount, LastPushedAt, TotalSlipDays (`src/db/schema/deals.ts:106-124`), RollbackCount, QuotaWeight, LastStageChangedAt (`deals.ts:125-146`).

### 2.a `Deal_Create` (source `createDealAction`, `ACT:294-607`) — ordered

| # | Step | Rule / effect | Failure message (verbatim) | Source |
|---|---|---|---|---|
| 1 | Permission `manageDeals` (owner, admin, manager, member) | | `Permission denied: requires "manageDeals" (current role: {role})` | `ACT:297-298`, `src/lib/rbac.ts:40`, `src/lib/auth-guard.ts:22` |
| 2 | Blank description / expectedCloseDate / ownerId → null | | | `ACT:320-322` |
| 3 | Shape validation (all field errors at once) | name trim 1–200; description ≤ 2000; stage in 7 codes; amount 0–999,999,999,999 major units; unitPrice ≥ 0; quantity integer 0–1,000,000; marginPct > 0, ≤ 100 (blank allowed); currency in USD EUR GBP INR CAD AUD JPY; dealType new_logo/renewal/expansion/upsell or null; date `YYYY-MM-DD`; companyId/contactId uuid | `Name is required`, `Name must be 200 characters or fewer`, `Description must be 2000 characters or fewer`, `Pick a valid stage`, `Enter a valid amount`, `Amount cannot be negative`, `Amount is too large`, `Unit price must be a number`, `Cannot be negative`, `Qty must be a number`, `Whole numbers only`, `Enter a valid margin`, `Margin must be greater than 0`, `Margin cannot exceed 100%`, `Pick a supported currency`, `Use a valid date`, `Pick a valid company`, `Pick a valid contact`, `Pick an owner`. Top-level message: `Please fix the highlighted fields.` | `SCH:38-107`, `ACT:324-332`, `src/lib/deals/currency.ts:1-9` |
| 3b | (same parse) Margin rule M1: `Stage_NeedsMargin(stage)` and marginPct not > 0 | field error on marginPct; top-level message is still `Please fix the highlighted fields.` | `Margin % is required from Qualified onwards.` | `SCH:114-127`, `VAL:28-29` |
| 4 | R0: stage = lead and date set | | `A deal with a close date can’t be in Lead — move it to Qualified or a higher stage.` (curly apostrophe U+2019) | `ACT:335-341`, `VAL:6-7` |
| 5 | R1: `Stage_NeedsCloseDate` and date blank | | `An expected close date is required to be in {StageLabel}.` | `ACT:346-355`, `SG:176-179` |
| 6 | Won on create forbidden | | `A deal can't be created straight at Won. Create it at an earlier stage, attach the client's PO, then move it to Won.` (straight apostrophes) | `ACT:359-363` |
| 7 | Links (tenant + sales scope) | | `Selected company not found.` / `You can't link that company.` / `Selected contact not found.` / `You can't link that contact.` / `Contact does not belong to the selected company.` | `ACT:366-380`, `ACT:237-266` |
| 8 | Owner: if ownerId set and ≠ actor: role not owner/manager → silently owner = actor; manager → target role owner refused. ownerId empty → **null (unassigned)** | | `Managers cannot assign deals to an owner.` (field: `Pick a non-owner teammate`) | `ACT:385-401` |
| 9 | Custom fields | | `Please fix the highlighted fields.` | `ACT:403-411` |
| 10 | Offerings (flag on): ≥ 1 id, one primary | | `Pick at least one offering — what is this deal selling?` / `Mark one offering as primary.` | `ACT:422-436`, `src/lib/offerings/types.ts:138-142`, `:206-219` |
| 11 | Amount: unitPrice and qty both given → `AmountMinor = round(unitPrice × 10^dec) × qty`, else `round(amount × 10^dec)` (JPY 0 decimals, others 2) | | | `ACT:440-449`, `currency.ts:13-34` |
| 12 | `MarginBps = round(pct × 100)` or null | | | `ACT:452-455`, `APR:10-12` |
| 13 | LostReason = trim, Left 1000, empty→null, only if stage = lost (**not required**) | | (none in source; see 2.c `lost_reason_required`) | `ACT:456-459` |
| 14 | `ClosedAt = now` if stage = lost, else null | | | `ACT:460-462` |
| 15 | TX: stage ≠ lead → stage gates as `lead → stage` with form values (counts = 0) | block → `Can't move to {StageLabel}: {n} requirement(s) not met.` + failures | `ACT:471-490`, `enforce.ts:51-58` |
| 16 | Insert deal: ForecastCategory = `Forecast_CategoryFor(stage)`, UpdatedBy = actor; defaults: ApprovalStatus `none`, Probability **null**, QuotaWeight 1.00, RollbackCount 0, LastStageChangedAt null, Hold* null | G1 insert, G3 insert, G4 (never fires: no Won) | | `ACT:492-516`, `deals.ts:76-78,140-147,155` |
| 17 | Offerings rows in same TX; none active → rollback | | `Pick at least one offering — what is this deal selling?` | `ACT:527-542` |
| 18 | Activity `created {name, stage}`; realtime `deal.created` | | | `ACT:544-559` |
| 19 | After commit: workflow `crm/deal.created`, score recompute (`created`), embed | | `Failed to create deal.` if insert returned nothing | `ACT:582-598` |

Not done on create in source: hold reason/revisit (create at Hold is allowed with no reason — create form offers Hold: `src/app/dashboard/deals/_components/create-deal-form.tsx:154`), approval, audit/date-change rows, gate warnings activity (dealId null → skipped, `src/lib/stage-gates/enforce.ts:90-93`).

### 2.b-1 `Deal_Update` (full form, source `updateDealAction`, `ACT:611-1096`) — ordered

| # | Step | Failure message | Source |
|---|---|---|---|
| 1 | Permission `manageDeals` | as 2.a-1 | `ACT:614-615` |
| 2 | Normalise + shape (incl. M1 against the **saved** stage) | as 2.a-3/3b | `ACT:638-650` |
| 3 | R0 against saved stage | LEAD message | `ACT:653-659` |
| 4 | R1 against saved stage | `An expected close date is required to be in {StageLabel}.` | `ACT:664-673` |
| 5 | Custom fields; links | as create | `ACT:675-699` |
| 6 | Amount / MarginBps (blank margin → **null**, clears an existing margin when the saved stage is lead/hold/lost); LostReason (only if lost, optional) | | `ACT:701-723` |
| 7 | RollbackReason = posted value only if in ROLLBACK_REASONS else null; RollbackNote trim, Left 1000 | | `ACT:729-736`, `SG:75-82` |
| 8 | Owner: only owner/manager write ownerId (null allowed); manager → owner refused; other roles: owner untouched | `Managers cannot assign deals to an owner.` | `ACT:741-757` |
| 9 | TX: load deal (tenant, not deleted) | `Deal not found in this tenant.` | `ACT:765-785`, `ACT:1076-1078` |
| 10 | H2: saved stage = hold and previous ≠ hold | `Put a deal on Hold from the pipeline board — it needs a reason and a revisit date.` | `ACT:789-791`, `ACT:1019-1025` |
| 11 | R3: previous date set, saved date blank, `Stage_NeedsCloseDate(saved stage)` | `The close date can’t be cleared while the deal is in Proposal or Negotiation. Set a new date, or move the deal back a stage with a reason.` (U+2019) | `ACT:799-807`, `SG:181-183`, `SG:194-204` |
| 12 | R2: `IsRollback(previous, saved)` and no valid reason | `Moving a deal backward needs a reason.` | `ACT:812-822`, `SG:130-131` |
| 13 | Stage changed → gates with post-edit values | `Can't move to {StageLabel}: {n} requirement(s) not met.` | `ACT:827-846` |
| 14 | Order proof: saved stage = won and (previous ≠ won or amount changed or currency changed) → `matchOrderProofs`. Entering Won → gate-shaped block (`Can't move to Won: 1 requirement not met.`, failure = proof message); staying Won → proof message itself (field amount) | see 2.c `order_proof` | `ACT:850-866`, `ACT:1010-1031` |
| 15 | ClosedAt: closed and previous null → now; not closed and previous set → null; else keep (won→lost keeps Won time) | | `ACT:868-870` |
| 16 | Approval patch (2.b-4) | | `ACT:875-881` |
| 17 | Write all fields; **clears free-text company/contact name + email**; Forecast re-derive only if stage changed and not overridden; previous hold and saved ≠ hold → Hold* = null | G1–G4 run here | `ACT:883-930` |
| 18 | Offerings sync (flag on): tagged deal can't be emptied; primary needed | `This deal is already tagged, so it can’t be left with no offering. To change what it sells, add the right one first, then remove this.` / `Mark one offering as primary.` | `ACT:951-957`, `src/lib/offerings/queries.ts:506-538`, `types.ts:144-145` |
| 19 | Activity `stage_changed {name, from, to, reason?, rollback, rollbackReason, rollbackNote?}` if stage changed else `updated {name}`; realtime | | `ACT:961-1005` |
| 20 | After commit: embed, score recompute (`deal_updated`). **No** won/lost notification, no `crm/deal.stage_changed` event, no agent observation (board only) | | `ACT:1080-1087` |

### 2.b-2 `Deal_ChangeStage` (board / path bar, source `updateDealStageAction`, `ACT:1100-1629`) — ordered

Inputs (`ACT:1100-1122`): Id, Stage (enum), Reason ≤ 1000 (lost), GateNote ≤ 2000, HoldReason (enum HOLD_REASONS), HoldRevisitOn `YYYY-MM-DD`, HoldNote ≤ 1000, ClearCloseDate, RollbackReason (enum ROLLBACK_REASONS), RollbackNote ≤ 1000.

| # | Check | Outcome (source flag) | Message | Source |
|---|---|---|---|---|
| 1 | Permission `manageDeals` | | permission message | `ACT:1163-1164` |
| 2 | Shape (bad enum/regex/length) | | `Invalid stage transition.` (form-level fallback; field errors such as `Invalid stage`, `Use YYYY-MM-DD` are not returned) | `ACT:1170-1177` |
| 3 | H1: target hold and (no HoldReason or no HoldRevisitOn). No future-date rule. | | `Moving to Hold needs a reason and a revisit date.` | `ACT:1183-1190` |
| 4 | Target lead, deal has a date, ClearCloseDate ≠ true | `needsCloseDateClear` | LEAD message | `ACT:1195-1216` |
| 5 | Load deal (missing → step 13 not-found) | | `Deal not found in this tenant.` | `ACT:1235-1258`, `ACT:1576-1578` |
| 6 | Same stage → return deal, `changed = false`, no write | success | | `ACT:1260-1272` |
| 7 | R2: IsRollback and no RollbackReason | `needsRollbackReason`, fromStage, toStage | `Moving a deal backward needs a reason.` | `ACT:1279-1291`, `ACT:1545-1553` |
| 8 | R1 on resulting date (target lead → null, else current date) | `needsCloseDate` | `An expected close date is required to be in {StageLabel}.` | `ACT:1298-1308`, `ACT:1556-1563` |
| 9 | M1: `Stage_NeedsMargin(target)` and MarginBps **is null** (0 passes) | `blocked`, failure `{rule_type field_required, severity block, cta "Set margin", field marginPct}` | `Margin % is required from Qualified onwards.` | `ACT:1314-1339` |
| 10 | Stage gates (current values + GateNote); warn → activity `gate_warning` | `blocked` | `Can't move to {StageLabel}: {n} requirement(s) not met.` | `ACT:1344-1364`, `enforce.ts:65-115` |
| 11 | Target won → order proof on **current** amount/currency | `blocked`, toStage won, failure `{rule_type order_proof, cta "Open order confirmation"}` | `Can't move to Won: 1 requirement not met.` (+ failure message, 2.c) | `ACT:1369-1388`, `OPC:32-44` |
| 12 | Lost reason | **not checked** server-side (UI modal only, `src/app/dashboard/deals/_components/lost-reason-modal.tsx:76`) | | `ACT:1404-1410` |
| 13 | Write: Stage; ClosedAt (as 2.b-1 #15); LostReason = trimmed Left 1000 if lost else null; target lead → ExpectedCloseDate null; approval patch; Forecast re-derive unless overridden; target hold → HoldReason, HoldRevisitOn, HoldNote (trim or null), else all three null; UpdatedBy | G1 (date cleared → DealDateChange old→null IsPush F; audit `expected_close_date` `edit`; TotalSlipDays 0), G2, G3, G4 | | `ACT:1390-1462` |
| 14 | Activity `stage_changed {name, from, to, reason?, rollback, rollbackReason, rollbackNote?}`; agent observation `deal.won` (+1) / `deal.lost` (−1) / `deal.stage_changed`; realtime | | | `ACT:1470-1533` |
| 15 | After commit: notify owner if won/lost and owner ≠ actor: `Deal won: {name}` / `Deal lost: {name}`; workflow `crm/deal.stage_changed`; embed; score (`stage_changed`) | | | `ACT:1580-1620` |

Rollback reason pass-through: set before the write, cleared right after (`ACT:1417-1422`, `ACT:1465`; `src/lib/deals/stage-change-reason.ts:27-49`, code Left 100, note trim Left 1000). In OS pass `RollbackReasonCode/Note` as inputs to `Deal_ApplyGovernance`.

### 2.b-3 Other governed paths (all must call the same governance)

| Path | Behaviour | Source |
|---|---|---|
| Inline patch (`Deal_Patch`) | no stage change; R0/R3/R1 against **current** stage (`LEAD_CLOSE_DATE` → LEAD message, `CLOSE_DATE_CLEAR` → R3 message, `CLOSE_DATE_REQUIRED` → `closeDateRequiredMessage(current)`); amount derived → `Amount is set by Per Unit Price × Qty on the deal.`; `Set both a per-unit price and a quantity.`; Won deal amount/currency change → proof message; close-date reason ≤ 500 → DealDateChange.Reason; owner: `You cannot reassign deals.` / `Managers cannot assign deals to an owner.`; not found `Deal not found.` | `ACT:2199-2466`, `src/lib/deals/patch-columns.ts:25-30`, `SCH:161-163` |
| Reject (single/bulk, `approveDeals`) | only won + pending, else `This deal is not awaiting approval.`; system reason `approval_rejected`, note = reason trim Left 500; set Stage negotiation, ClosedAt null, ApprovalStatus rejected, ApprovedBy = rejecter, ApprovedAt = now; activity `deal_rejected {name, reason?}`; no forecast re-derive, no hold/lost clear | `ACT:1723-1820`, `ACT:1919-2014`, `SG:112-115` |
| Approve (single/bulk) | only won + pending, else `This deal is not awaiting approval.`; ApprovalStatus approved, ApprovedBy/At; activity `deal_approved`; no governed field → G1/G2 do not fire | `ACT:1638-1716`, `ACT:1836-1913` |
| Revert to Lead (temporary cleanup) | qualified → lead, date null, system reason `lead_close_date_cleanup` | `src/lib/deals/revert-to-lead.ts:19-30,106-115`, `SG:112-128` |
| CSV import | lost needs reason: `Missing "lostReason" — required when stage is "lost".`; Won refused | `ACT:2807-2817` |

Rollback reason codes + labels (`SG:75-92`): requirement_changed "Requirement changed"; budget_lost "Budget lost"; champion_left "Champion left"; wrong_stage_advance "Wrong stage advance (data entry error)"; competitor_reintroduced "Competitor reintroduced"; other "Other". System codes (not in dropdown, `SG:112-115`, labels `SG:118-128`): approval_rejected "Reopened by manager rejection"; lead_close_date_cleanup "Sent back to Lead (close-date cleanup)".
Hold reason codes + labels (`src/lib/deals/hold.ts:5-22`): budget_deferred "Budget deferred"; champion_left "Champion left"; timing "Timing"; competitor_evaluation "Evaluating competitor"; client_unresponsive "Client unresponsive"; other "Other" (in Salenix = MasterData HoldReason, 6 rows).

### 2.b-4 Approval transitions (`APR:41-81`)

| Previous → New | Actor has `approveDeals` (owner, admin, manager; `rbac.ts:55`) | Actor member |
|---|---|---|
| not won → won | approved, ApprovedBy = actor, ApprovedAt = now | pending, null, null |
| won → won (edit) | unchanged (patch null) | unchanged |
| won → not won | none, null, null | none, null, null |
| not won → not won | none, null, null (also clears a `rejected` marker) | same |

Quota credit: ApprovalStatus none/approved credited, pending/rejected withheld (`src/lib/targets/quota-credit.ts:45-53`); credited value = round(AmountMinor × QuotaWeight) (`SG:241-246`, `src/lib/targets/queries.ts:205`).

### 2.b-5 Company rollups affected (decision 10: attributes on Company, maintained by write actions; source = view `drizzle/0065_company_rollups.sql:118-138`)

| Attribute | Definition (source) | Deal writes that must refresh it (old and new CompanyId) |
|---|---|---|
| DealCount | non-deleted deals, any stage (`0065:133`) | create, soft delete, restore, hard delete, company link change |
| OpenDealCount | stage NOT IN (won, lost); hold is open (`0065:134`) | + every stage change across the closed boundary |
| WonDealCount | stage = won, any approval status (`0065:135`) | + enter/leave won (incl. reject) |
| OpenPipelineValue | Σ AmountMinor of open deals, no FX (`0065:136`) | + amount change on an open deal |
| WonLifetimeValue | Σ AmountMinor of won deals (`0065:137`) | + amount change on a won deal |
| TopStage | stage of most recently updated open deal (`0065:125`; BRD-DB tie-break Id desc) | **every** deal write (UpdatedAt changes) |
| LastActivityAt | GREATEST(company activity, company note, max deal UpdatedAt) (`0065:127,138`) | every deal write |
| ContactCount | contacts, not deleted/merged | none (contact writes) |

### 2.c Return shape and failures

Return structure for `Deal_Create / Deal_Update / Deal_ChangeStage / Deal_Patch / Deal_Approve / Deal_Reject`: `Success` (Boolean), `OutcomeCode` (Text, lower_snake), `Message` (Text, verbatim below), `FailedRules` (List of `{RuleType, Severity (block|warn), Field, Message, CtaLabel, CtaKind}`), plus `DealId`, `Changed` (ChangeStage), `FromStage`, `ToStage`.

| OutcomeCode | Success | Message (verbatim) | Raised by | Source flag / line |
|---|---|---|---|---|
| ok | T | — | all | |
| no_change | T | — (Changed = False) | ChangeStage same stage | `ACT:1260-1272` |
| permission_denied | F | `Permission denied: requires "{perm}" (current role: {role})` | all | `auth-guard.ts:22` |
| validation_error | F | `Please fix the highlighted fields.` (Create/Update), `Invalid stage transition.` (ChangeStage), first field message (Patch), `Invalid deal id.` | all | `ACT:329,647,1175,2209-2213,1644` |
| not_found | F | `Deal not found in this tenant.` / `Deal not found.` (Patch) | Update, ChangeStage, Patch | `ACT:1077,1577,2456` |
| lead_close_date | F | `A deal with a close date can’t be in Lead — move it to Qualified or a higher stage.` | Create, Update, Patch | `VAL:6-7` |
| needs_close_date_clear | F | same LEAD message; UI confirms and retries with ClearCloseDate | ChangeStage | `ACT:1209-1215` |
| close_date_required | F | `An expected close date is required to be in {StageLabel}.` | all | `SG:176-179` |
| close_date_clear_blocked | F | `The close date can’t be cleared while the deal is in Proposal or Negotiation. Set a new date, or move the deal back a stage with a reason.` | Update, Patch | `SG:181-183` |
| won_on_create | F | `A deal can't be created straight at Won. Create it at an earlier stage, attach the client's PO, then move it to Won.` | Create | `ACT:360-361` |
| hold_via_edit | F | `Put a deal on Hold from the pipeline board — it needs a reason and a revisit date.` | Update | `ACT:1022-1023` |
| hold_reason_required | F | `Moving to Hold needs a reason and a revisit date.` | ChangeStage (propose also Create at hold) | `ACT:1187` |
| rollback_reason_required | F | `Moving a deal backward needs a reason.` | Update, ChangeStage | `SG:130-131` |
| margin_required | F | `Margin % is required from Qualified onwards.` (FailedRules: field_required, CTA "Set margin", Field marginPct) | Create, Update, ChangeStage | `VAL:28-29`, `ACT:1322-1338` |
| lost_reason_required | F | `A reason is required when marking a deal Lost` (**new server rule**, text from client validator) | Create, Update, ChangeStage | `src/lib/deals/client-validate.ts:99-103` |
| gate_blocked | F | `Can't move to {StageLabel}: {n} requirement(s) not met.` (`requirement` when n = 1) + FailedRules per gate | Create, Update, ChangeStage | `enforce.ts:51-58` |
| order_proof | F | entering Won: `Can't move to Won: 1 requirement not met.` with FailedRules[0] = `{order_proof, block, CTA "Open order confirmation"}` and one of: `Attach the client's PO (or other order confirmation) before marking this deal Won.` / `The order proof is in {currencies} but the deal is in {currency}. Remove it, or change the deal's currency to match the PO.` / `The deal value ({value}) is {gap} less than the PO value before tax ({po}). Make the deal value match the PO.` / `The deal value ({value}) is {gap} more than the PO value before tax ({po}). Make the deal value match the PO, or attach the missing PO.`; staying Won with changed value: the failure message itself. Fallback: `This deal can't be Won at this value: its order confirmation (PO) is missing or doesn't match the deal value. Check the Order confirmation card on the deal.` | Update, ChangeStage, Patch | `OPS:65-85`, `OPC:32-44,65-66` |
| approval_unavailable | F | `This deal is not awaiting approval.` | Approve, Reject | `ACT:1706,1811` |
| owner_assign_forbidden | F | `Managers cannot assign deals to an owner.` / `You cannot reassign deals.` | Create, Update, Patch | `ACT:396,751,2221,2225` |
| link_invalid | F | the 5 link messages of 2.a-7 | Create, Update, Patch | `ACT:237-266` |
| offering_required | F | `Pick at least one offering — what is this deal selling?` | Create | `offerings/types.ts:138-139` |
| offering_primary_required | F | `Mark one offering as primary.` | Create, Update | `types.ts:141-142` |
| offering_cannot_empty | F | `This deal is already tagged, so it can’t be left with no offering. To change what it sells, add the right one first, then remove this.` | Update | `types.ts:144-145` |
| amount_derived | F | `Amount is set by Per Unit Price × Qty on the deal.` | Patch | `ACT:2433` |
| unit_pricing_incomplete | F | `Set both a per-unit price and a quantity.` | Patch | `patch-columns.ts:27` |
| custom_field_invalid | F | `Please fix the highlighted fields.` | Create, Update | `ACT:408,680` |

Check order inside one OS action (to reproduce the source's first-failure behaviour): permission → shape (incl. margin) → R0 → R1 → won_on_create → links → owner → custom fields → offerings → (TX) not_found → hold_via_edit → R3 → rollback → gates → order_proof → write → offerings sync. ChangeStage: permission → shape → hold → needs_close_date_clear → not_found → no_change → rollback → R1 → margin → gates → order_proof → (new) lost_reason → write. Put `lost_reason_required` for ChangeStage right after `hold_reason_required` (input-only check) if you prefer it before any read; BRD-App 6.7.7 lists it last (step 8).

---

## 3. BRD UAT 1–15 (BRD-App:1145-1159) mapped to the rules

Base deal for the examples: TenantId T, currency INR, actor role in brackets, today 2026-11-11.

| # | BRD text (verbatim) | Inputs | Expected per code | Rules |
|---|---|---|---|---|
| 1 | "Member creates a deal at Lead with a close date" → "Rejected (R0 message)" | [member] Deal_Create(Name "A", Stage lead, Amount 1000, Currency INR, ExpectedCloseDate 2026-12-31) | Success F, `lead_close_date`, LEAD message, FailedRules Field stage | 2.a #4, `ACT:335-341` |
| 2 | "Create at Proposal without close date" → "Rejected (R1)" | [member] Stage proposal, MarginPct 20, no date | `close_date_required`, `An expected close date is required to be in Proposal.` (Field expectedCloseDate). Without MarginPct the source stops earlier at margin (step 3b) | 2.a #5 |
| 3 | "Create at Qualified without margin" → "Rejected ("Margin % is required from Qualified onwards.")" | [member] Stage qualified, MarginPct blank | `margin_required`, message = margin text. Source returns top message `Please fix the highlighted fields.` with fieldErrors.marginPct = margin text | 2.a #3b, `SCH:114-127` |
| 4 | "Move Negotiation → Qualified without reason" → "NeedsRollbackReason; with reason → RollbackCount + 1, audit row `rollback` with reason" | [member] deal at negotiation, MarginBps 2000, date 2026-12-31; ChangeStage(qualified) then ChangeStage(qualified, RollbackReason budget_lost, Note "x") | 1st: `rollback_reason_required`, FromStage negotiation, ToStage qualified, nothing written. 2nd: ok; Stage qualified; RollbackCount +1; DealAuditLog(stage, negotiation, qualified, rollback, budget_lost, "x"); DealStageHistory negotiation closed + qualified open; LastStageChangedAt now; ForecastCategory pipeline (if not overridden); Approval none; date kept; activity stage_changed rollback true | 2.b-2 #7, G2, G3 |
| 5 | "Move Proposal → Hold" → "Needs reason + revisit date; forecast category omitted; excluded from score/risk/stale" | ChangeStage(hold) then ChangeStage(hold, HoldReason timing, HoldRevisitOn 2027-01-15) | 1st: `hold_reason_required`. 2nd: ok; Hold* set; ForecastCategory omitted; audit stage row `edit` (unranked), RollbackCount unchanged; margin/date not required; excluded from health score (`src/lib/deals/scoring/service.ts:126`), risk (`src/lib/deals/risk/service.ts:211`), forecast overdue (`rollup.ts:182`); **not** excluded from the detail-page stall flag (`src/lib/deals/stage-history.ts:44-50`, D16) | 2.b-2 #3, #13 |
| 6 | "Push close date 4 times (later each time)" → "PushCount 4, QuotaWeight 0.50, TotalSlipDays = new − original; 4 DealDateChange rows `IsPush`" | deal created at qualified with date D0 = 2026-12-01; Patch date to 12-08, 12-15, 12-22, 12-29 | after push 3: PushCount 3, QuotaWeight 1.00; after push 4: PushCount 4, QuotaWeight 0.50, TotalSlipDays 28, OriginalCloseDate 12-01, LastPushedAt = 4th write; 4 DealDateChange rows IsPush T; 4 audit rows `pushback`. If the deal was created dateless, the first date set adds a 5th DealDateChange row IsPush F (audit `edit`) and becomes the original | G1, G2.4, `SG:219-229` |
| 7 | "Pull close date earlier" → "Not a push; audit `edit`" | from 12-29 (orig 12-01) Patch to 12-10 | DealDateChange IsPush F; PushCount unchanged (4), QuotaWeight stays 0.50; TotalSlipDays 9; audit `expected_close_date` `edit` | G1.4-6, G2.2 |
| 8 | "Member moves a deal to Won" → "Approval pending; not counted in targets; appears in Approvals" | [member] deal at negotiation, date set, MarginBps set, proofs INR Σ subtotal = AmountMinor ±100; ChangeStage(won) | ok; ApprovalStatus pending; ClosedAt now; ForecastCategory closed_won; audit `forward_progress`; withheld from quota (`quota-credit.ts:49-53`). Without proofs: `order_proof` | 2.b-2 #11, 2.b-4 |
| 9 | "Manager rejects it with a reason" → "Stage negotiation, ClosedAt null, approval rejected, audit rollback reason `approval_rejected`" | [manager] Deal_Reject(Id, Reason "price") | ok; Stage negotiation; ClosedAt null; ApprovalStatus rejected, ApprovedBy manager, ApprovedAt now; RollbackCount +1; audit stage won→negotiation `rollback`, ReasonCode approval_rejected, Note "price"; DealStageHistory rows; activity `deal_rejected`; ForecastCategory stays closed_won in source (D10). Not pending → `approval_unavailable` | 2.b-3 Reject |
| 10 | "Manager moves a deal to Won" → "Approved immediately" | [manager] same as 8 | ApprovalStatus approved, ApprovedBy manager, ApprovedAt now | `APR:41-54` |
| 11 | "Stage gate "min 2 stakeholders, block" on Proposal; deal has 1" → "Blocked with rule CTA; with severity warn → allowed + `gate_warning`" | flag stageGatesEnabled on; gate(proposal, min_stakeholders, count 2, block); deal at qualified with date + margin, 1 DealContact; ChangeStage(proposal) | `gate_blocked`, `Can't move to Proposal: 1 requirement not met.`, FailedRules [{min_stakeholders, block, `Link at least 2 stakeholder(s) — have 1`, CTA "Add stakeholder"}]. Warn: ok + activity `gate_warning {name, from, to, rules}` | `src/lib/stage-gates/evaluate.ts:290-301`, `enforce.ts:87-111` |
| 12 | "Offerings on; create deal without offering" → "Rejected; remove last offering from tagged deal → rejected" | flag offeringsEnabled; Deal_Create no offering ids; Deal_Update of tagged deal with empty selection | `offering_required` / `offering_cannot_empty` | 2.a #10, 2.b-1 #18 |
| 13 | "Value split 3 years, recurring ₹10,00,001 (in paise 100000100)" → "Year 1 gets the remainder; Σ = amount; Σ ≠ amount → "short of"/"exceeds"" | spreadEvenly(100000100, 3) | [33333368, 33333366, 33333366], Σ 100000100; mismatch → `The breakdown is short of the deal amount.` / `The breakdown exceeds the deal amount.` | `src/lib/deals/value-split.ts:123-129,412-422` (outside governance) |
| 14 | "Support element starting 1 Nov 2026, FY April" → "FY display 5/12 in FY2026–27, 7/12 in FY2027–28" | proratedFiscalYearSplit([A], "2026-11-01", 4) | FY2026: 5 months, round(A×5/12); FY2027: 7 months, A − first share | `value-split.ts:457-494` |
| 15 | "Board drag Qualified → Lead on a dated deal" → "Confirmation, then date cleared" | ChangeStage(lead) → ChangeStage(lead, ClearCloseDate T) → ChangeStage(lead, ClearCloseDate T, RollbackReason wrong_stage_advance) | 1st `needs_close_date_clear`; 2nd `rollback_reason_required` (qualified→lead is a rollback); 3rd ok: date null, DealDateChange (D→null, IsPush F), audit date `edit` + stage `rollback`, TotalSlipDays 0, OriginalCloseDate kept, ForecastCategory pipeline. Board asks date first, then reason (`src/app/dashboard/deals/board/_components/board-view.tsx:645-674`) | 2.b-2 #4, #7, #13 |

---

## 4. Differences BRD vs code

| # | Topic | BRD says | Code does | Source | Suggested decision |
|---|---|---|---|---|---|
| D1 | Create at Won | BRD-App:442 "`ClosedAt = now` if won/lost; Won approval" on create | Create at Won refused (`won_on_create`); DB trigger also blocks | `ACT:357-363`, `M71:137-139` | Follow code (order proofs) |
| D2 | Lost reason | BRD-App:401,412 BR-DL-L1 required; 6.7.5 "L1 (if created at Lost)"; 6.7.7 step 8 | Required only in UI (`client-validate.ts:99-103`, `src/app/dashboard/deals/_components/lost-reason-modal.tsx:76`) and CSV (`ACT:2810-2815`); create/update/stage store null when empty | `ACT:456-459,720-723,1405-1410` | Enforce server-side (`lost_reason_required`), as BRD fix says |
| D3 | Order proof in ChangeStage / Update | BRD-App 6.7.7 lists 8 checks, no order proof | Order proof check after gates; Won deal value changes also checked | `ACT:850-866,1369-1388,2353-2366` | Add (CLAUDE.md already records 0071) |
| D4 | Create at Hold | BRD-App:400 Hold "only from board/detail 'Put on hold'"; H2 covers edit only | Create form offers Hold; create stores no HoldReason/RevisitOn | `src/app/dashboard/deals/_components/create-deal-form.tsx:154`, `ACT:492-516` | Reject create at hold (`hold_reason_required`) or require hold inputs; decide |
| D5 | Hold revisit date | BRD-App:413 "Recommended: revisit date must be in the future" | Only `YYYY-MM-DD` regex | `ACT:1109-1112` | Decide; if added, compare with `Foundation_TodayFor` |
| D6 | Assign rights | Decision 6: assignDeals = owner, admin, manager | Create/Update/Patch allow owner and manager only; admin's ownerId silently ignored (create) or not written (update) or refused (patch) | `ACT:387-388,741-742,2219-2221` | Keep decision 6 (records a source gap) |
| D7 | Unassigned on create | BRD-RBAC-03 not explicit | ownerId empty → deal saved with owner null even by a member | `ACT:385` | Decide: default owner = actor |
| D8 | UAT 3 message | top-level margin message | top-level `Please fix the highlighted fields.`, margin text only as field error | `ACT:325-331` | OS returns `margin_required` + margin text |
| D9 | UAT 15 | "Confirmation, then date cleared" | Also needs a rollback reason (qualified→lead is a rollback) | `ACT:1279-1291`, `src/app/dashboard/deals/board/_components/board-view.tsx:660-674` | Follow code |
| D10 | Reject and forecast category | not stated | Reject leaves ForecastCategory `closed_won` on a negotiation deal (no re-derive) | `ACT:1773-1782,1970-1979` | Re-derive (commit) unless overridden — fix |
| D11 | Rejected marker | BRD-App:496 "clears to none on the next stage move" | Broader: any full Update of the reopened deal clears it, even with the stage unchanged (not-won → not-won returns the CLEARED patch); Patch and Approve do not touch it | `APR:56-81`, `ACT:875-905` | Follow code |
| D12 | Close-date reason length | BRD-DB:1966 "≤ 1000 chars" | App trims to 500 (schema + helper); trigger cuts at 1000 | `SCH:161-163`, `close-date-reason.ts:31`, `M58:171` | 500 |
| D13 | Deal.Probability | BRD-DB Deal #39 attribute; BRD-App 6.7.1 defaults 10/30/60/80/40/100/0 | Column never written by the app; backfill used 10/25/50/75/100/0, else 25; weighting uses stage probabilities from settings, not the column | `drizzle/0015_forecast_columns.sql:23-33`, `rollup.ts:161-171`, `PROB:11-19` | Do not write Probability; weight by `Stage_ProbabilityFor` |
| D14 | Governance on insert | BRD-App:443 "governance init — database doc §9.1/9.2" | Insert writes no DealAuditLog / DealDateChange; LastStageChangedAt null; QuotaWeight default | `M60:280-282`, `M58:109-117` | Follow code |
| D15 | Quota weight "ALWAYS" | BRD-DB:1999-2000 "on any governed write" | Only when stage/date/amount is in the SET list; equivalent because PushCount changes only with the date | `M60:264-271,281` | Compute on every Deal write (same result) |
| D16 | Hold excluded from stale | BRD-App UAT 5 | Health/risk/forecast exclude hold; detail-page stall flag treats hold as active | `src/lib/deals/stage-history.ts:44-50`, `src/lib/deals/filters.ts:80` | Decide; BRD wording true for list filter only |
| D17 | Full-form side effects | BRD-App:481 parity fix | Update sends no won/lost notification, workflow event or observation | `ACT:1080-1087` vs `ACT:1580-1620` | Apply BRD fix |
| D18 | Free-text names on full save | BRD-App:485 quirk | Update clears company/contact name + email | `ACT:888-891` | BRD fix |
| D19 | Margin on full save in lead/hold/lost | not stated | Blank margin field writes MarginBps null (clears a stored margin) | `ACT:713-716` | Keep stored margin unless user cleared it — decide |
| D20 | Margin check in ChangeStage | BRD-App:411 "> 0" | Checks `MarginBps is null`; a stored 0 passes (e.g. pct 0.004 → round(0.4)=0 bps passes `> 0` in shape check) | `ACT:1314-1317`, `APR:10-12` | OS: require MarginBps > 0 |
| D21 | Won→Lost ClosedAt | BRD-App:399,401 "ClosedAt set" | ClosedAt kept from the Won date (only set when previously null) | `ACT:868-870,1390-1392` | Follow code |
| D22 | Rollback note in Update | BRD-DB 9.3 note ≤ 1000 | Same; but an unknown rollback code in Update is treated as missing (rollback_reason_required), in ChangeStage it fails shape (`Invalid stage transition.`) | `ACT:729-734,1120,1175` | OS: unknown code → `rollback_reason_required` |
| D23 | Hold reason list | MasterData HoldReason (per tenant, admin-editable; decision 6) | Fixed enum of 6 | `src/lib/deals/hold.ts:5-12` | Keep decision; validate against active MasterData rows |
| D24 | Order-proof rounding | "within ₹1" (`M71:11`) | 100 minor units for every currency (JPY 0 decimals → ¥100) | `OPS:14`, `M71:125` | Follow code; note for JPY |
| D25 | Gate warnings on create | BRD-App:448 warn writes `gate_warning` | Skipped on create (deal id unknown); comment says "caller logs post-insert" but create never does | `enforce.ts:90-93`, `ACT:471-490` | Log after insert in OS |
| D26 | Today for overdue | BRD-App:671 "close date < today" | DB `CURRENT_DATE` (server UTC) | `rollup.ts:182` | `Foundation_TodayFor(TenantId)` (decision 7) |
| D27 | Company rollups | BRD-DB 9.7 suggests SQL / view | Source is a view (always fresh) | `0065_company_rollups.sql:118-138` | Decision 10: attributes refreshed per 2.b-5 |
