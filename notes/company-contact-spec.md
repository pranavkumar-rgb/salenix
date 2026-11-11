# Company & Contact spec (source-verified, 2026-11-11)

Scope: `source/src/app/dashboard/companies/actions.ts` (CA), `source/src/app/dashboard/contacts/actions.ts` (TA), `source/src/lib/companies/*`, `source/src/lib/contacts/*`, `source/src/lib/dedup/*`. All paths below are relative to `source/src/` unless they start with `drizzle/` or `BRD`.
BRD = `Salenix-BRD-Application-Logic.md`. Code wins; every difference is in section 7.

Common facts (both entities)
| Topic | Source behaviour | Cite |
|---|---|---|
| Permission check | `checkPermission(code)` → on fail returns `{ok:false, message}` with message `Permission denied: requires "<perm>" (current role: <role>)` | lib/auth-guard.ts:22, 39-41, 78-89 |
| Matrix | manageCompanies / manageContacts = owner, admin, manager, member; deleteCompanies / deleteContacts / restoreCompanies / restoreContacts = owner, admin; hardDeleteCompanies / hardDeleteContacts = owner; assignDeals = owner, manager (not used by company/contact) | lib/rbac.ts:26-38, 49 |
| Persona | owner, admin → admin; manager → manager; member, viewer (and null) → sales | lib/personas/index.ts:22-35 |
| Tenant | every write filters `tenant_id = tenant.id` (+ RLS) | CA:223, TA:316 |
| Soft-delete filter | writes (update/patch/owner/soft-delete) require `deleted_at IS NULL`; restore and hard delete do not | CA:224, 312, 375, 481-485, 434-437 |
| UpdatedAt | DB `$onUpdate(() => new Date())` on every UPDATE | db/schema/companies.ts:29-32 |
| UpdatedBy | set = actor on create, update, patch, owner change. NOT set on soft delete (sets DeletedBy) or restore (clears DeletedBy) | CA:113, 217, 303, 370, 480, 581 |
| Custom fields | `parseCustomFieldsForm(defs, formData)`; fail → "Please fix the highlighted fields." + `cf_*` field errors. Patch actions do not touch custom fields | CA:90-98, TA:158-166 |
| Owner input | free string (min 1, max 120 on patch/owner action); NOT validated as a tenant member; no persona rule (a member may set any owner or unassign); no assignDeals-like gate | lib/companies/schema.ts:19-24, 41; CA:527-530; lib/contacts/schema.ts:30-35, 81; TA:636-639 |
| Post-commit side effects | Inngest `crm/ai.embed_entity` (reason created / updated / restored) best effort; revalidatePath | CA:157-163, 249-255, 509-515 |
| Realtime | `publishRealtimeEvent` inside the transaction | CA:138-142 etc. |
| Feature flag | `dedupEnabled`, WorkspaceSettings, default false | db/schema/workspace-settings.ts:33 |

---

## A. Companies

### A1. Create — `createCompanyAction(formData)` CA:65-171
| Item | Rule | Cite |
|---|---|---|
| Permission | `manageCompanies` | CA:68 |
| Inputs | name, domain, industry, ownerId, `cf_*` custom fields, `dedupOverride` ("1" = true) | CA:75-80, 91, 102 |
| name | trim, required "Name is required", max 200 "Name must be 200 characters or fewer" | lib/companies/schema.ts:4-8 |
| domain | trim, max 255 "Domain must be 255 characters or fewer", empty → null. **No format check, no lower-casing, no protocol/www stripping** | schema.ts:9-13; CA:77 |
| industry | trim, max 100 "Industry must be 100 characters or fewer", empty → null; free text (no list) | schema.ts:14-18; CA:78 |
| ownerId | optional; "" → null; no member/persona check | schema.ts:19-24; CA:112 |
| Validation fail message | "Please fix the highlighted fields." + `fieldErrors` | CA:81-88 |
| Defaults | ownerId null; customFields {}; dedupOverride false; mergedIntoId null | db/schema/companies.ts:36-39, 52-54 |
| NormalizedName | `normalizeCompanyName(name)`: NFKD → strip U+0300-036F → lower → `[^a-z0-9\s]`→space → collapse/trim → drop whole tokens ltd, limited, pvt, private, llp, inc, incorporated, corp, corporation, co, company, plc, llc, gmbh, sarl, bv, ag → if empty, keep pre-suffix form | CA:115; lib/dedup/normalize.ts:12-30, 42-54 |
| Dedup at create | **Server does NOT run dedup and does NOT check the flag.** It trusts the hidden form field: `dedupOverride = matches.length > 0 ? '1' : '0'` set by the client after the live lookup | CA:100-102, 116; app/dashboard/companies/_components/create-company-form.tsx:137 |
| Live lookup | `findSimilarCompaniesAction(name)`: perm manageCompanies (fail → []), `name.trim().length < 2` → [], flag dedupEnabled off → [], else `findCompanyMatches(tenant, name)`; client debounce 300 ms | CA:37-51; create-company-form.tsx:63-73 |
| Match query | norm = normalizeCompanyName(typed); norm < 2 chars → []; rows: same tenant, `deleted_at IS NULL AND merged_into_id IS NULL`, normalized_name not null/empty, AND (`normalized_name = norm` OR `similarity(normalized_name, norm) >= 0.55` OR `word_similarity(norm, normalized_name) >= 0.55`); score = `GREATEST(similarity(stored, typed), word_similarity(typed, stored))`; ORDER BY exact DESC, score DESC; LIMIT 5 | lib/dedup/queries.ts:20, 39-82 |
| Match result | `{id, label=name, sublabel=domain, similarity=score, kind: exact/fuzzy}` → **warning only**, never blocks; UI "Similar existing company/companies", "Use this instead", "Or continue below to create a new company anyway." | queries.ts:22-29, 75-81; create-company-form.tsx:106-133 |
| Activity | `created` {name}; plus `dedup_override` {name} when dedupOverride | CA:120-137 |
| Realtime | `company.created` {company} | CA:138-142 |
| Workflow | `crm/company.created` {tenantId, companyId, actorUserId, name}; `crm/ai.embed_entity` reason created | CA:151-163 |
| Insert fail | "Failed to create company." | CA:146 |
| Rollups | none written (view computed on read, see A8) | drizzle/0065_company_rollups.sql:116-179 |

### A2. Update — `updateCompanyAction(formData)` CA:175-262 and inline `patchCompanyAction(input)` CA:271-342
| Item | Full update | Inline patch | Cite |
|---|---|---|---|
| Permission | manageCompanies | manageCompanies | CA:178, 274 |
| Editable | name, domain, industry, custom fields (ownerId ignored) | any subset of name, domain, industry, ownerId | CA:185-190, 213-219; CA:295-301 |
| id | uuid "Invalid company id" | same | schema.ts:30, 37 |
| Validation | same as create; fail "Please fix the highlighted fields." | name trim 1-200 ("Name is required"), domain ≤255, industry ≤100, ownerId 1-120 or null; fail message = first form/field error or "Invalid update." | CA:191-198; schema.ts:36-42; CA:278-288 |
| Empty → null | via `|| null` on form | domain/industry trimmed, "" → null | CA:188-189, 297-300 |
| Empty patch | — | no fields → returns "Company not found." (null) | CA:302, 333 |
| NormalizedName | **NOT recomputed** (stale after rename) | **NOT recomputed** | CA:213-219, 295-303 (schema comment claims "Kept in sync on create/rename": db/schema/companies.ts:44) |
| Owner change | n/a | ownerId patched directly: activity `updated` (not owner_changed), **no notification**, no from/to; used by bulk reassign | CA:301, 317-324; companies-view.tsx:382-393 |
| Activity | `updated` {name} | `updated` {name} | CA:229-236, 317-324 |
| Realtime | `company.updated` | `company.updated` | CA:237-241, 325-329 |
| Not found / deleted | "Company not found in this tenant." | "Company not found." | CA:246, 333 |
| Workflow | embed reason updated | none | CA:249-255 |

### A2b. Change owner — `updateCompanyOwnerAction({id, ownerId})` CA:546-636
| Item | Rule | Cite |
|---|---|---|
| Permission | manageCompanies (no assignDeals, no persona rule, no member check) | CA:549 |
| Validation | id uuid "Invalid company id"; ownerId string 1-120 or null; fail "Invalid owner payload" | CA:527-530, 556-559 |
| Lookup | active row (deleted_at null) in tenant; missing → "Company not found" | CA:562-573, 607-609 |
| No-op | same owner → ok, changed=false, no activity | CA:575-577 |
| Write | ownerId, updatedBy | CA:579-583 |
| Activity | `owner_changed` {name, from, to} | CA:586-597 |
| Notification | post-commit, best effort, only if new owner set and ≠ actor: type `owner_changed`, title `You now own ${company.name}`, subtitle "Company", metadata {from} | CA:612-629 |
| Realtime | **none** | — |
| Result | {ok, company, changed} | CA:633 |

### A3. Soft delete — `softDeleteCompanyAction(id)` CA:346-407
| Item | Rule | Cite |
|---|---|---|
| Permission | deleteCompanies (owner, admin) | CA:347 |
| id check | "Invalid company id." | CA:364-365 |
| Write | deletedAt = now, deletedBy = actor, only where deleted_at IS NULL | CA:368-378 |
| Children | **nothing**: contacts and deals keep company_id; contact list still shows the deleted company's name (join has no deleted filter) | CA:367-395; lib/contacts/queries.ts:220 |
| Fail | "Company not found or already deleted." | CA:398 |
| Activity | `deleted` {name} | CA:380-387 |
| Realtime | `company.deleted` {companyId, name} | CA:388-393 |
| Bulk | UI loops the single action; confirm "Delete N companies? They can be restored from Recently deleted." | companies-view.tsx:395-409 |

Hard delete `hardDeleteCompanyAction` CA:411-454: perm hardDeleteCompanies (owner); "Invalid company id."; DELETE without requiring a prior soft delete; FK `set null` on contacts.company_id and deals.company_id (db/schema/contacts.ts:23-24, deals.ts:29-30); fail "Company not found in this tenant."; **no activity, no realtime**.

### A4. Restore — `restoreCompanyAction(id)` CA:458-523
| Item | Rule | Cite |
|---|---|---|
| Permission | restoreCompanies (owner, admin) | CA:459 |
| id check | "Invalid company id." | CA:474-475 |
| Write | deletedAt = null, deletedBy = null; **no `deleted_at IS NOT NULL` condition** (restoring an active row "succeeds" and logs) | CA:478-487 |
| Conflicts | **none checked**: no name/dedup check; merged loser (mergedIntoId set) is restorable, stays hidden from lists because mergedIntoId is kept | CA:478-487; lib/companies/queries.ts:159 |
| Fail | "Company not found in this tenant." | CA:506 |
| Activity / realtime | `restored` {name}; `company.restored` {company}; embed reason restored | CA:489-501, 509-515 |

### A5. Get and list
| Item | Rule | Cite |
|---|---|---|
| List scope | sales persona → `owner_id = me OR owner_id IS NULL` (applied server-side on the page) | app/dashboard/companies/page.tsx:40-43; lib/companies/queries.ts:205-210 |
| Base filters | tenant, deleted_at IS NULL, merged_into_id IS NULL | queries.ts:156-160 |
| Search `q` | trimmed; `name ILIKE %q%` (name only, not domain) | page.tsx:29-30; queries.ts:161 |
| Filters | hasDeals (openDealCount > 0); recentActivityDays N (lastActivity ≥ now-N d; chip 14); createdWithinDays N; stale + staleDays (default 30; lastActivity < now-N d, never-touched = epoch → stale); minOpen / minWon = **value floors in MAJOR units** (×100 → minor) on openPipelineValue / wonLifetimeValue; mine (owner = me); unassigned (owner null). Numbers must be > 0 | queries.ts:163-204, 119-123; filters.ts:28-40, 70-76, 151 (filters.ts lines = file numbering) |
| Sort | recent (createdAt, default), deals (dealCount), pipeline (openPipelineValue), won (wonLifetimeValue), activity (COALESCE(lastActivityAt, epoch)); dir asc/desc, default desc; tie-break id same direction; unknown → recent | queries.ts:216-226, 244, 299; filters.ts:53-59, 102-111 |
| Paging | keyset cursor; size ∈ {25, 50, 100}, default 50, else default | lib/pagination/page-size.ts:19-28; queries.ts:300 |
| Returned | full Company + rollup {dealCount, openDealCount, wonDealCount, openPipelineValue, wonLifetimeValue, topStage, contactCount, lastActivityAt}; missing rollup → zeros/null | queries.ts:271-284, 325-339; lib/companies/rollup.ts:40-49 |
| Display | counts ≤0 → "—"; open value shown only when openDealCount > 0; won only when wonDealCount > 0 | rollup.ts:60-63, 74-81 |
| Get (by id) | `getCompanyById`: tenant + deleted_at null (merged not filtered, but merge also sets deleted_at). Detail page uses `getAccount360`: deleted and merged excluded. **No persona scope on detail** | queries.ts:345-363; lib/companies/account-360.ts:114-115; app/dashboard/companies/[id]/page.tsx:80 |
| IncludeDeleted | separate `listDeletedCompanies`: deleted_at NOT NULL (includes merged losers), order deletedAt desc, id desc, same page sizes | queries.ts:368-398 |
| Options | `listCompanyOptions` unscoped (used on contacts page); `listCompanyOptionsScoped` own+unassigned for deal form | queries.ts:41-55, 62-85; app/dashboard/contacts/page.tsx:50 |

### A8. Rollups (decision 10: attributes on Company, updated by write actions)
Source = view `company_rollups`, computed on read (drizzle/0065_company_rollups.sql:116-179). Definitions to reproduce:
| Attribute | Definition | Writers in the rebuild (must refresh) |
|---|---|---|
| DealCount | non-deleted deals, any stage (0065:133, 139-142) | Deal create / soft delete / restore / hard delete / company change; company merge |
| OpenDealCount | stage NOT IN (won, lost); hold counts as open (0065:134) | + every stage change |
| WonDealCount | stage = won (0065:135) | stage change |
| OpenPipelineValueMinor | SUM(amount_minor) open deals, currencies summed blindly (0065:65-69, 136) | + AmountMinor change |
| WonLifetimeValueMinor | SUM(amount_minor) won (0065:137) | same |
| TopStage | stage of most recently updated open deal (ORDER BY updated_at DESC, id DESC); null if none (0065:147-156) | any deal update on the company |
| ContactCount | contacts non-deleted, non-merged (0065:157-164) | Contact create (with company), update/patch companyId change (old and new company), soft delete, restore, hard delete, contact merge, company merge (contacts repointed, lib/dedup/queries.ts:331-333) |
| LastActivityAt | GREATEST(max activity_logs on entity company, max non-deleted note on company, max deal.updated_at) (0065:127, 138, 165-179) | every ActivityLog with EntityType company (created, updated, owner_changed, deleted, restored, dedup_override, merged), company note create, any deal write. Contact activity does NOT count |

---

## B. Contacts

### B1. Create — `createContactAction(formData)` TA:114-241
| Item | Rule | Cite |
|---|---|---|
| Permission | manageContacts | TA:117 |
| Inputs | firstName, lastName, email, phone, title, companyId, ownerId, `cf_*`; every blank string → null before parsing | TA:124-135 |
| firstName / lastName | trim, max 100 "First name must be 100 characters or fewer" / "Last name must be 100 characters or fewer" | lib/contacts/schema.ts:5-14 |
| email | valid "Enter a valid email address", max 255 "Email is too long"; stored **as typed** (not lower-cased) | schema.ts:15-18; TA:190 |
| phone | trim, max 50 "Phone must be 50 characters or fewer"; no format check | schema.ts:19-23 |
| title | trim, max 100 "Title must be 100 characters or fewer" | schema.ts:24-28 |
| companyId | uuid "Pick a valid company"; must exist in tenant and not deleted (not persona-scoped) → "The selected company is not in this tenant." + field error "Pick a company from the list" | schema.ts:29; TA:92-110, 147-156 |
| ownerId | optional, no checks | schema.ts:30-35 |
| Identity rule | at least one of first, last, email: "Provide a first name, last name, or email." (field firstName) | schema.ts:37-40 |
| Validation fail message | first form error or "Please fix the highlighted fields." | TA:137-145 |
| Email uniqueness | **hard block, independent of dedupEnabled**: active (not deleted, not merged) contact in tenant with `lower(email) = trim(lower(typed))` → message `A contact with this email already exists: ${existing.name}.`, field error "This email is already on another contact.", `duplicateContact {id,name}` (name = first+last or the email) | TA:168-180; lib/dedup/queries.ts:85-112 |
| NormalizedName | `normalizeContactName(first, last)`: `first + " " + last`, same steps as company without suffix removal | TA:196-199; normalize.ts:57-69 |
| Name dedup | not run at create on the server; live only (`checkDuplicateContactAction`) | TA:41-62 |
| Live check | perm manageContacts (fail → empty); flag off → empty; email match (block in UI) + name matches: norm < 3 chars → []; `similarity(normalized_name, norm) >= 0.55` only (no word_similarity, no exact clause), not deleted/merged, ORDER BY score DESC LIMIT 5, kind fuzzy → warning; client debounce 400 ms | TA:41-62; lib/dedup/queries.ts:115-149; contacts/_components/create-contact-form.tsx:64-76 |
| DedupOverride | column exists, **never set** for contacts | db/schema/contacts.ts:52; TA:184-200 |
| Activity | `created` {name = first+last, else email, else id} | TA:81-85, 203-210 |
| Realtime | `contact.created` | TA:211-215 |
| Workflow | `crm/contact.created` {contactId, displayName}; embed reason created | TA:221-233 |
| Insert fail | "Failed to create contact." | TA:219 |
| Company rollup | none (view) → rebuild: ContactCount +1 on CompanyId | 0065:157-164 |

### B2. Update — `updateContactAction` TA:245-355, `patchContactAction` TA:364-461
| Item | Full update | Inline patch | Cite |
|---|---|---|---|
| Permission | manageContacts | manageContacts | TA:248, 367 |
| Editable | first, last, email, phone, title, companyId, custom fields (no owner) | first, last, email, phone, title, ownerId (**no companyId**) | TA:303-312; TA:404-409 |
| Validation | same limits but default zod messages for max (no custom text), email "Enter a valid email address", companyId "Pick a valid company", identity "Provide a first name, last name, or email." | email "" → null; identity on merged row → "Keep a name or email on the contact."; fail = first error or "Invalid update." | schema.ts:44-57, 66-82; TA:371-381, 412-418, 454-456 |
| Company check | same as create | n/a | TA:279-288 |
| Email uniqueness | **not checked** | **not checked** | TA:300-320, 403-431 |
| NormalizedName | **not recomputed** | **not recomputed** | TA:303-312, 403-409 |
| Owner via patch | activity `updated`, no notification (bulk reassign path) | | TA:409, 433-440; contacts-view.tsx:296-307 |
| Activity / realtime | `updated` {name}; `contact.updated` | same | TA:322-334, 433-445 |
| Not found | "Contact not found in this tenant." | "Contact not found." | TA:338-340, 449 |
| Empty patch | — | returns previous row, ok, no activity | TA:410 |

Change owner `updateContactOwnerAction` TA:650-746: same as A2b; messages "Invalid owner payload", "Contact not found"; activity `owner_changed` {name, from, to}; notification title `You now own ${displayName}` (displayName = first+last, else email, else "this contact"), subtitle "Contact"; no realtime.

### B3. Soft delete — `softDeleteContactAction` TA:465-523
deleteContacts; "Invalid contact id."; deletedAt/deletedBy where not deleted; fail "Contact not found or already deleted."; activity `deleted`; realtime `contact.deleted` {contactId, name}. Children: **nothing** (deals.contact_id, deal_contacts untouched). Rebuild: ContactCount −1 on its company. Bulk confirm "Delete N contacts? They can be restored from Recently deleted." (contacts-view.tsx:309-323).
Hard delete TA:527-567: hardDeleteContacts (owner); fail "Contact not found in this tenant."; no activity/realtime; FK set null on deals.contact_id (db/schema/deals.ts:33).

### B4. Restore — `restoreContactAction` TA:571-632
restoreContacts; "Invalid contact id."; clears deletedAt/deletedBy with no deleted condition; **no email-uniqueness re-check** (a restored contact can duplicate an email created meanwhile); merged loser restorable but stays hidden; fail "Contact not found in this tenant."; activity `restored`; realtime `contact.restored`; embed reason restored.

### B5. Get and list — `listContacts` lib/contacts/queries.ts:132-230
| Item | Rule | Cite |
|---|---|---|
| Scope | sales persona → owner = me OR null | app/dashboard/contacts/page.tsx:36-39; queries.ts:183-188 |
| Base | tenant, deleted_at null, merged_into_id null | queries.ts:139-144 |
| Search `q` | ILIKE on firstName, lastName, email, title (not phone, not company) | queries.ts:145-154 |
| Filters | companyId, noCompany, recentActivityDays (EXISTS activity_logs on the contact, not a rollup), createdWithinDays, mine, unassigned | queries.ts:155-182; lib/contacts/filters.ts:11-18, 30-44 |
| Sort | createdAt desc, id desc only | queries.ts:222 |
| Paging | keyset, sizes 25/50/100, default 50 | queries.ts:137, 223; page-size.ts:19-21 |
| Returned | all contact columns + companyName (LEFT JOIN, deleted company names still shown) | queries.ts:198-220 |
| Get | `getContactById`: tenant + not deleted; **no persona scope** on detail page | queries.ts:232-250; app/dashboard/contacts/[id]/page.tsx:46 |
| Deleted list | `listDeletedContacts` deletedAt desc | queries.ts:252-282 |
| Pickers | `listContactOptions` unscoped; `listContactPickerOptions` own+unassigned | queries.ts:40-69, 82-116 |

---

## 6. Outcome codes / messages per action (proposed codes for ODC; messages verbatim from source)
| Action | Proposed OutcomeCode | Message (verbatim) |
|---|---|---|
| any | permission_denied | `Permission denied: requires "<perm>" (current role: <role>)` |
| Company_Create | validation | "Please fix the highlighted fields." (+ field: "Name is required", "Name must be 200 characters or fewer", "Domain must be 255 characters or fewer", "Industry must be 100 characters or fewer") |
| | create_failed | "Failed to create company." |
| | ok / ok_dedup_override | — |
| Company_Update | validation / not_found | "Please fix the highlighted fields." / "Company not found in this tenant." |
| Company_Patch | validation / not_found | first error or "Invalid update." / "Company not found." |
| Company_ChangeOwner | invalid / not_found / unchanged / ok | "Invalid owner payload" / "Company not found" |
| Company_SoftDelete | invalid_id / not_found | "Invalid company id." / "Company not found or already deleted." |
| Company_Restore, Company_HardDelete | invalid_id / not_found | "Invalid company id." / "Company not found in this tenant." |
| Contact_Create | validation | first error or "Please fix the highlighted fields."; "Provide a first name, last name, or email."; "Enter a valid email address"; "Email is too long"; length messages (B1) |
| | company_invalid | "The selected company is not in this tenant." (field "Pick a company from the list") |
| | duplicate_email | `A contact with this email already exists: ${name}.` (field "This email is already on another contact.") |
| | create_failed | "Failed to create contact." |
| Contact_Update | validation / company_invalid / not_found | as above / "Contact not found in this tenant." |
| Contact_Patch | identity / validation / not_found | "Keep a name or email on the contact." / first error or "Invalid update." / "Contact not found." |
| Contact_ChangeOwner | invalid / not_found | "Invalid owner payload" / "Contact not found" |
| Contact_SoftDelete | invalid_id / not_found | "Invalid contact id." / "Contact not found or already deleted." |
| Contact_Restore, Contact_HardDelete | invalid_id / not_found | "Invalid contact id." / "Contact not found in this tenant." |
| Merge (lib/dedup/queries.ts) | self / inactive | "Cannot merge a record into itself." / "Both records must exist and be active." (queries.ts:288, 324) |

Activity action codes used: created, updated, deleted, restored, owner_changed, dedup_override (company only), merged (winner; metadata {loserId, auditId, repointed}, queries.ts:404-411).
Realtime: company.created/updated/deleted/restored, contact.created/updated/deleted/restored (lib/realtime/events.ts:37, 47).

---

## 7. Differences BRD vs code (code wins unless a decision says otherwise)
| # | BRD | Code | Cite |
|---|---|---|---|
| 1 | FR-CO-02 "min open deals, min won deals" | minOpen / minWon are **money floors in major units** on open pipeline value / won lifetime value, not deal counts | BRD:342; lib/companies/queries.ts:189-198; filters.ts:28-40 |
| 2 | BR-DD-02 company match = exact OR "similarity ≥ 0.55" | exact OR similarity(stored,typed) ≥ 0.55 OR word_similarity(typed,stored) ≥ 0.55; score = GREATEST (matches CLAUDE.md decision 9) | BRD:371; lib/dedup/queries.ts:51-63 |
| 3 | FR-CO-03 "If dedup is on and similar companies were shown, set DedupOverride" | server never checks the flag nor re-runs matching; trusts client hidden field | CA:100-102; create-company-form.tsx:137 |
| 4 | FR-CO-04 fix: recompute NormalizedName on rename | confirmed missing for companies (update + patch); **also missing for contacts** (BRD does not list the contact fix) | CA:213-219, 295-303; TA:303-312, 403-409 |
| 5 | FR-CO-05 fix: inline/bulk owner changes via the owner action | confirmed: patch writes ownerId with activity `updated`, no notification; same for contacts | CA:301, 317-324; TA:409 |
| 6 | FR-CO-06 messages "… not found or already deleted." | only soft delete uses it; restore and hard delete say "… not found in this tenant." | CA:398, 445, 506; TA:514, 558, 615 |
| 7 | BR-CT-02 fix: email uniqueness on update/patch | confirmed missing on update, patch and **restore** (restore not in BRD) | TA:300-320, 403-431, 586-596 |
| 8 | BR-RBAC-01 scope "list, board, export, search…" | company/contact scope is applied only to list pages; get/detail, update, patch, owner change and the contacts-page company picker are unscoped (a member can open/edit any company by id) | page.tsx:40-43; queries.ts:345-363; app/dashboard/contacts/page.tsx:50 |
| 9 | BRD has no owner rule for companies/contacts (BR-RBAC-03 is deal-only) | confirmed: any manageCompanies/manageContacts role (incl. member) may set any owner string; owner not validated as a member | CA:527-530, 549; TA:636-639 |
| 10 | BRD silent | contact DedupOverride column exists but is never written | db/schema/contacts.ts:52 |
| 11 | BRD silent | restore has no "was deleted" check, no conflict check; a merged loser can be restored and stays hidden (mergedIntoId kept) | CA:478-487; TA:587-596 |
| 12 | BRD silent | hard delete writes no activity and does not require a prior soft delete | CA:431-442; TA:544-555 |
| 13 | FR-CT-02 list "same rules" | contacts: search first/last/email/title; filters companyId, noCompany, recentActivityDays (activity_logs EXISTS), createdWithinDays, mine, unassigned; sort createdAt desc only | lib/contacts/queries.ts:145-188, 222 |
| 14 | FR-CT-01 company "in the workspace and not deleted" | matches; merged not checked separately (merge also sets deleted_at) | TA:92-110; lib/dedup/queries.ts:387 |
| 15 | BRD silent | contact email stored as typed (case kept); uniqueness compares lower() | TA:190; lib/dedup/queries.ts:102 |
| 16 | BRD silent | full contact update cannot change owner; patch cannot change companyId | TA:303-312, 404-409 |
| 17 | FR-DD-01 company pairs "same name or ≥ 0.55" | pair filter uses similarity only, score shows GREATEST(similarity, word_similarity) | lib/dedup/queries.ts:171-181 |
| 18 | BRD 3.3 assignDeals = owner, manager | code same; CLAUDE.md decision 6 adds admin (deal only, no effect here) | lib/rbac.ts:49 |
| 19 | BRD silent | soft delete of a company leaves contacts/deals linked; contact list still shows the deleted company's name | CA:367-395; lib/contacts/queries.ts:220 |
| 20 | BRD silent | patch/update changes to updatedBy only; soft delete/restore do not set updatedBy | CA:370, 480 |

Open decisions for the rebuild (not taken): server-side re-check of dedup at Company_Create (flag + match) instead of trusting the client; NormalizedName recompute on every name change (both entities); email uniqueness on update/patch/restore; owner = active member of the tenant; whether sales persona may assign others / edit records outside own+unassigned; ContactCount refresh on contact companyId change.
