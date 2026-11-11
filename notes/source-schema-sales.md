# Source schema notes (long run 2026-11-11) — F8 + Sales tables
Code = truth. All tables: uuid id, tenant_id NN FK tenants CASCADE, timestamptz, text unlimited, RLS. Money = bigint minor units (only numeric = deals.quota_weight 3,2). OutSystems: Id AutoNumber, TenantId Tenant Identifier (mandatory, Delete), LegacyUuid Text 36 (non-unique index), user ids = User Identifier (Ignore), uuid FKs = entity identifiers.

## No source table
- realtime_events: none (Postgres NOTIFY → SSE). BRD-App-Logic l.977: RealtimeEvent (TenantId, Type, EntityType, EntityId, CreatedAt; purge after 1 day).
- SalesJobQueue: none in code or BRD-Database. BRD-App-Logic l.88: DealRecomputeQueue (DealId, DueAt = now + 5 min, timer).
- custom_field_values: none; values are jsonb custom_fields on deals/companies/contacts keyed by definition key. BRD §5.6: CustomFieldValue (TenantId, EntityType, EntityId, DefinitionId, ValueText, ValueNumber Decimal(28,8), ValueDate, ValueBoolean).

## workflow_runs (workflow-runs.ts, 0006)
workflow_id text NN; event_name text; inngest_run_id text; status text NN 'running' (running, completed, failed, skipped); started_at NN now(); completed_at; error text; metadata jsonb NN '{}'. Idx (tenant_id, started_at); (inngest_run_id).

## chart_views (0022)
user_id text NN; chart_key text NN; title text; role_scope text NN 'user' (user, team, tenant); config jsonb NN; is_default bool NN false; created_at, updated_at. Idx (tenant_id, user_id, chart_key); partial unique (tenant, user, chart_key) WHERE is_default.

## dashboard_widgets (0023)
user_id text NN; widget_type text NN; chart_type, metric, dimension, title text; layout_x, layout_y int NN 0; layout_w, layout_h int NN 4; config_json jsonb NN '{}'; created_at, updated_at. Idx (tenant_id, user_id).

## companies
name NN; domain; industry; owner_id text; created_at, updated_at; updated_by; custom_fields jsonb NN '{}'; deleted_at; deleted_by; normalized_name; merged_into_id (self FK SET NULL); dedup_override bool NN false. Idx tenant+name, +domain, +owner, +deleted_at, +merged_into_id; GIN trigram normalized_name.
Rollups (VIEW company_rollups 0065) → 8 attributes on Company: DealCount int, OpenDealCount int (not won/lost; hold = open), WonDealCount int, OpenPipelineValueMinor, WonLifetimeValueMinor (sum amount_minor, no FX), TopStage (most recently updated open deal), ContactCount (not deleted, not merged), LastActivityAt (latest of company activity_logs, notes, deal updated_at).

## contacts
company_id (FK SET NULL); first_name, last_name, email, phone, title; owner_id; created_at, updated_at; updated_by; custom_fields jsonb; deleted_at; deleted_by; normalized_name; merged_into_id (self FK SET NULL); dedup_override bool NN false. Idx tenant+email, +last_name, +company, +owner, +deleted, +merged.

## merge_audit (0053)
entity_type text NN (company, contact); winner_id, loser_id uuid NN (no FK); loser_snapshot jsonb NN; repointed_counts jsonb NN '{}'; merged_by text; created_at. Idx (tenant, created_at), (tenant, winner_id).

## deals
company_id (SET NULL); contact_id (SET NULL); name NN; description; company_name, contact_name, contact_email; custom_fields jsonb; stage NN 'lead' (lead, qualified, proposal, negotiation, hold, won, lost); deal_type (new_logo, renewal, expansion, upsell); amount_minor bigint NN 0; unit_price_minor; quantity int; offer_price_minor; margin_bps int; approval_status NN 'none' (none, pending, approved, rejected); approved_by; approved_at; lost_reason; hold_reason (budget_deferred, champion_left, timing, competitor_evaluation, client_unresponsive, other); hold_revisit_on date; hold_note; next_step, competitor, source; currency NN 'INR'; expected_close_date date; original_close_date date; push_count int NN 0; last_pushed_at; total_slip_days int NN 0; rollback_count int NN 0; quota_weight numeric(3,2) NN 1.00; last_stage_changed_at; closed_at; owner_id; probability smallint (0–100 CHECK); health_score smallint; health_band text; forecast_category (pipeline, best_case, commit, closed_won, omitted); forecast_category_overridden bool NN false; created_at, updated_at; updated_by, deleted_by; deleted_at.
CHECK chk_close_date_required: stage in proposal/negotiation/won needs expected_close_date. Trigger trg_deal_order_proof_check (0071): won needs ≥ 1 proof, same currency, sum subtotal within 100 minor units of amount_minor → ORDER_PROOF_REQUIRED.
Idx tenant+stage, +owner, +company, +contact, +expected_close_date, +deleted_at, +forecast_category, +health_band, +hold_revisit_on, +push_count, +rollback_count, +quota_weight.

## deal_contacts (0045)
deal_id NN CASCADE; contact_id NN CASCADE; role NN 'other' (decision_maker, influencer, champion, blocker, other); is_primary bool NN false; created_at. Unique (deal, contact); one primary per deal.

## offerings (0066)
name NN; category NN (product, service); active bool NN true; sort_order int NN 0; created_at, updated_at. Unique (tenant, lower(name)); idx (tenant, category, sort_order, name).

## deal_offerings (0066)
deal_id NN CASCADE; offering_id NN NO ACTION (Protect); is_primary bool NN false; created_at. Unique (deal, offering); one primary per deal.

## deal_value_splits (0055, 0057)
deal_id NN CASCADE; element_index smallint NN 1; component NN 'other' (license, implementation, support, amc, training, hardware, other); component_label text default 'Contract value'; billing_type NN 'recurring' (one_time, recurring); year_index smallint NN; period_start, period_end date NN; amount_minor bigint NN 0; created_at, updated_at. CHECK year_index ≥ 1, element_index ≥ 1, period_end > period_start, other needs label. Unique (deal, element_index, year_index).

## deal_order_proofs (0071; missing from BRD)
deal_id NN CASCADE; document_id NN → deal_documents CASCADE (= FileAttachment); proof_type NN 'purchase_order' (purchase_order, work_order, signed_contract, letter_of_intent, email_confirmation, other); reference_no NN (non-blank); issued_on date; buyer_name; currency NN; subtotal_minor NN; tax_minor NN 0; total_minor NN; ai_read jsonb; submitted_by text NN; created_at. CHECK subtotal > 0, tax ≥ 0, total ≥ subtotal. Unique (deal, lower(trim(reference_no))); idx (tenant, deal).

## deal_stage_history (0045)
deal_id NN CASCADE; stage NN; entered_at NN now(); exited_at. Idx (deal, entered_at), (tenant, stage); one open row per deal.

## deal_date_changes (0058)
deal_id NN CASCADE; old_close_date, new_close_date date; changed_by; changed_at NN now(); reason text; is_push bool NN false. Idx (tenant, deal, changed_at desc).

## deal_audit_log (0060) — append-only
deal_id NN CASCADE; changed_by; changed_at NN now(); field_name NN (stage, expected_close_date, amount_minor); old_value, new_value text; change_type NN (forward_progress, rollback, pushback, edit); reason_code, reason_note. Idx (tenant, deal, changed_at desc). Update/delete forbidden.

## stage_gates (0049)
stage NN; rule_type NN (field_required, min_stakeholders, min_activities, amount_required, task_complete, custom_note); rule_config jsonb NN '{}'; severity NN 'block' (block, warn); is_active bool NN true; created_by; created_at, updated_at. Unique (tenant, stage, rule_type). BRD "stage ≠ lead" has no SQL CHECK.

## deal_scores (0050)
deal_id NN CASCADE; score smallint NN; band NN (strong, moderate, at_risk); reasons jsonb NN '[]'; computed_at NN now(). Idx (deal, computed_at), (tenant, deal).

## deal_risk_scores (0062)
deal_id NN CASCADE unique; score smallint NN; band NN (high, elevated, low); factors jsonb NN '[]'; signals jsonb NN '{}'; explanation; explanation_model; explained_at; signals_hash NN; computed_at NN now(). Idx (tenant, score desc).

## forecast_snapshots (0052)
period text NN; owner_id; category NN (forecast categories + overdue); total_amount bigint NN 0 (money → TotalAmountMinor); deal_count int NN 0; captured_at NN now(). Idx (tenant, period, captured_at).

## tasks → CrmTask
title NN; description; status NN 'todo' (todo, in_progress, completed, cancelled); priority NN 'medium' (low, medium, high, urgent); type (call, email, meeting, internal, followup); recurrence (daily, weekly, custom); stage_blocking (deal stage); due_date date; assigned_user_id; created_by NN; completed_at; entity_type; entity_id (polymorphic); created_at, updated_at; deleted_at; deleted_by. Idx tenant+status, +assigned, +due, +(entity_type, entity_id), +deleted.

## notes
body NN; author_user_id NN; entity_type NN; entity_id NN; created_at, updated_at; deleted_at; deleted_by. Idx (tenant, entity_type, entity_id, created_at), +author, +deleted.

## comments
entity_type NN; entity_id NN; author_user_id NN; parent_comment_id (self SET NULL); body NN; mentioned_user_ids text[] NN '{}' (→ Text); edited_at; created_at, updated_at; deleted_at; deleted_by. Idx (tenant, entity, created_at), (parent, created_at), +author, +deleted.

## activity_logs
actor_user_id; entity_type NN (company, contact, deal, membership, invitation, task, note, email); entity_id **text** NN; action NN (23 codes: created, updated, deleted, restored, stage_changed, invited, invitation_revoked, member_removed, role_changed, automation_note, task_completed, owner_changed, email_sent, email_failed, deal_approved, deal_rejected, call_logged, meeting_logged, gate_warning, email_synced, merged, dedup_override, value_split_changed); metadata jsonb NN '{}'; created_at. Idx (tenant, created_at), (tenant, entity_type, entity_id, created_at).
