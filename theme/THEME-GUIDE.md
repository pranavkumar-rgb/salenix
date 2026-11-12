# Salenix theme guide (v2.2)

Theme file: `theme/salenix-theme.css` (Salenix_Theme -> theme "Salenix", Public). UI prompts use only the classes below; no inline styles, no new CSS in apps.

Source of the design: `source/src/app/globals.css`, `source/src/lib/brand/tokens.ts`, `source/src/app/layout.tsx`, `source/src/app/dashboard/_components/*`. Light only (the source forces light).

v2.2 (Salenix_Theme rev 6, 2026-11-12, batch TC1 part 1): every rule that lived in the Salenix app stylesheets (MainLayout, Login, Deals, Companies, Contacts, SxDrawer, DealFormBlock, CompanyFormBlock, ContactFormBlock) was appended as the "v2.2" section at the end of the theme (same selectors, duplicates merged), so the cascade order is unchanged. From v2.2 on the Salenix app holds no CSS of its own. New classes are marked (v2.2) below.

## Icons (v2.2, TC1-fix)
Salenix_Theme sets the theme property IconLibrary = Phosphor 2.0 (overrides the OutSystemsUI default FontAwesome 4). Every Icon widget uses Phosphor 2.0 names; Font Awesome names (search, home, times, building, columns, check-circle, filter) do not render and raise "not available in the Phosphor 2.0 icon library". Mapping used (source lucide → Phosphor): Search → magnifying-glass, Plus → plus, Filter → funnel, X → x, LayoutDashboard → squares-four, GitBranch → git-branch, Briefcase → briefcase, BadgeCheck → seal-check, UsersRound → users, Building2 → buildings, Bell → bell. After renaming, an icon with the same name may still warn until it is re-selected from the library.

## Fonts
Loaded by `@import` from Google Fonts at the top of the theme: Inter (body, 400-700), Fraunces (page titles, opsz + wght), Geist Mono (numbers: KPI values, money columns). If the ODC stage gets a Content Security Policy, allow `fonts.googleapis.com` (style) and `fonts.gstatic.com` (font).

## Tokens (most used)
| Token | Value | Source |
|---|---|---|
| `--sx-bg` / `--sx-cream-50` | #fef8f4 | `--background`, slate-50 |
| `--sx-surface` | #ffffff | `--surface` |
| `--sx-border` / `--sx-cream-200` | #f0e2d8 | `--border`, slate-200 |
| `--sx-divider` / `--sx-cream-100` | #f9efe8 | slate-100 row dividers |
| `--sx-text` / `--sx-ink-900` | #1a1110 | `--foreground`, slate-900 |
| `--sx-text-body` / `--sx-ink-700` | #4b403c | slate-700 |
| `--sx-text-muted` / `--sx-ink-500` | #7a6e68 | slate-500 |
| `--sx-maroon-500` / `--sx-accent` | #7d0712 | `--accent`, indigo-500 |
| `--sx-maroon-600` | #6b0710 | indigo-600 (primary button) |
| `--sx-marigold-500` | #f8a01a | active nav pill |
| `--sx-info-500` | #2d6a8a | info blue (blue/sky) |
| `--sx-rail-gradient` | #6b0a13 -> #5c0a11 -> #34040a | `.askmira-rail` |
| `--sx-fs-10 .. --sx-fs-20` | 10/11/12/13/14/15/20px | Tailwind text-[..] sizes |
| `--sx-radius-sm/md/lg/full` | 4/6/8/9999px | rounded / -md / -lg / -full |

OutSystems UI variables (`--color-primary`, `--color-neutral-0..10`, `--color-background-body`, status colours, `--color-focus-*`, `--font-family-body`, `--border-radius-soft`, shadows) are mapped to these tokens, so standard widgets already look close.

## Classes

### Page layout
| Class | Where to use | Source |
|---|---|---|
| `sx-page` | Outer container of every screen's main content (max-width 88rem) | dashboard pages `max-w-[88rem] px-4 py-6` |
| `sx-page-header` | Row with page title on the left, actions on the right | page header row |
| `sx-page-title` | Screen title (Fraunces 20px) | `main h1` + `text-xl font-semibold` |
| `sx-page-subtitle` | One line under the page title | `p mt-0.5 text-[12px] text-slate-500` |
| `sx-breadcrumb` | Breadcrumb row above the page title (v2.1) | `text-[12px] text-slate-500 gap-1.5` |
| `sx-section-title` | Heading of a page section (15px) | `h2 text-[15px] font-semibold` |
| `sx-card-title` | Heading inside a card (13px) | `h2 text-[13px] font-semibold` |
| `sx-stack` | Vertical list of sections, 12px gap | `flex flex-col gap-3` |
| `sx-row` | Horizontal group (buttons, inline items), 8px gap | `flex items-center gap-2` |
| `sx-grid-2` | Two-column form / card grid (1 column on phones) | `grid gap-3 sm:grid-cols-2` |

### Cards and KPIs
| Class | Where to use | Source |
|---|---|---|
| `sx-card` | Any white panel | `rounded-lg border bg-white p-4 shadow-hairline` |
| `sx-card sx-card-compact` | Small panel, 12px padding | `rounded-lg border bg-white p-3` |
| `sx-card sx-card-flush` | Card that wraps a table (no padding) | `overflow-hidden rounded-lg border shadow-sm` |
| `sx-card-header` | Title + action row at the top of a card | `flex justify-between gap-2` |
| `sx-card-footer` | Action row at the bottom of a card (v2.1) | `mt-3 border-t pt-3 flex justify-end gap-2` |
| `sx-kpi-strip` | Grid of KPI tiles (2 / 3 / 5 columns) | kpi-strip.tsx grid |
| `sx-kpi` | One KPI tile in the strip | kpi-strip.tsx tile |
| `sx-kpi-value` | Big number in a KPI tile (auto-sized 13-19px mono) | kpi-strip.tsx value |
| `sx-kpi-footer` | Bottom caption line of a KPI tile | kpi-strip.tsx footer |
| `sx-metric` | Metric card, neutral tone | metric-card.tsx |
| `sx-metric-info` / `-success` / `-warning` / `-danger` | Add to `sx-metric` for a tone | metric-card.tsx toneClass |
| `sx-metric-empty` | Add to `sx-metric` when there is NO data (never for a real zero) | metric-card.tsx emptyState |
| `sx-metric-label` | Label (10px uppercase), used in `sx-metric` and `sx-kpi` | metric-card.tsx label |
| `sx-metric-value` | Value (20px mono), tone colour follows the card | metric-card.tsx value |
| `sx-metric-hint` | Small hint under the value | metric-card.tsx hint |
| `sx-trend sx-trend-up` / `sx-trend sx-trend-down` / `sx-trend sx-trend-flat` | +12% / -4% / 0% pill next to the label (flat = v2.1) | metric-card.tsx TrendIndicator |

### Tables and pagination
| Class | Where to use | Source |
|---|---|---|
| `sx-table` | Style Classes of every Table widget (13px, 10px uppercase header, row divider, hover) | `min-w-full divide-y divide-slate-100 text-[13px]` + thead |
| `sx-num` | Numeric / money cell (right, mono) | `text-right font-mono tabular-nums` |
| `sx-cell-strong` | Main text of a row (name, title) | `text-[13px] font-medium text-slate-900` |
| `sx-pagination` | Bar under a table | pagination-controls.tsx |
| `sx-pagination-btn` | Prev / Next buttons in that bar | pagination-controls.tsx buttons |

### Buttons and links
| Class | Where to use | Source |
|---|---|---|
| `sx-btn sx-btn-primary` | Main action (Save, Create) | `bg-indigo-600 hover:bg-indigo-500` |
| `sx-btn sx-btn-secondary` | Cancel, secondary actions | `border bg-white text-slate-700 shadow-sm` |
| `sx-btn sx-btn-ghost` | Icon buttons, close X, low-emphasis | `rounded-md p-1.5 text-slate-500 hover:bg-slate-100` |
| `sx-btn sx-btn-danger` | Delete / destructive confirm | `bg-rose-600 hover:bg-rose-500` |
| add `sx-btn-sm` | Small inline row action | `px-2 py-1 text-[11px]` |
| `sx-link` | Text link ("View all", "Open") | `text-indigo-600 hover:text-indigo-500` |

Replace the default `btn` / `btn-primary` Style Classes with these; do not combine with `btn`. Exception: a Button inside an OutSystems UI ButtonLoading placeholder MUST keep `btn` (else OS-CLRT-00000 "There are no '.btn' element as a ButtonLoading placeholder child"), e.g. Login Sign in = `btn btn-primary sx-btn sx-btn-primary sx-auth-submit`.

| Class | Where to use | Source |
|---|---|---|
| `sx-btn sx-btn-secondary sx-btn-sm sx-btn-delete` | Delete button on the left of an edit-drawer footer (red border and text, red-50 hover) (v2.2) | edit-*-drawer.tsx `border-red-200 text-red-700 hover:bg-red-50` |
| add `sx-btn-block` | Full-width button inside `sx-form-footer-full` (v2.2) | create-*-form.tsx `w-full` |

### Forms
| Class | Where to use | Source |
|---|---|---|
| `sx-field` | Wrapper of label + input + error | `flex flex-col gap-1.5` |
| `sx-label` | Label widget | `text-xs font-medium text-slate-700` |
| `sx-input` | Input, Text Area and Dropdown widgets (focus ring maroon) | `rounded-md border px-3 py-1.5 text-[13px] focus:ring-indigo` |
| `sx-field-error` | Validation message text | `text-xs text-red-600` |
| `sx-checkbox` | Checkbox widget | `h-3.5 w-3.5 text-indigo-600` |
| `sx-alert-error` | Error banner at the top of a form | `rounded-md bg-red-50 px-3 py-2 text-xs text-red-700` |
| `sx-form-block` | Root container of a form block (Deal / Company / Contact): no card; inner `sx-stack` gap 16; hides empty `sx-field` and empty validation messages (v2.2) | create-*-form.tsx `flex flex-col gap-4` |
| `sx-req` | Red asterisk after a required label (v2.2) | `text-red-600` |
| `sx-label-optional` | "(optional)" text inside a label (v2.2) | `font-normal text-slate-400` |
| `sx-amount-row` | Amount + Currency grid (1fr + 88px) inside `sx-form-block` (v2.2) | deal-amount-fields.tsx |
| `sx-margin-field` | Half-width Margin % field (v2.2) | deal-amount-fields.tsx |
| `sx-input-suffix` + `sx-input-suffix-text` | Wrapper + "%" suffix text over the right edge of the input (v2.2) | deal-amount-fields.tsx |
| `sx-form-footer` | Button row at the end of a form block; sticky at the drawer bottom inside `sx-drawer-body` (v2.2) | drawer.tsx footer |
| add `sx-form-footer-create` | Create mode: not sticky, no border, at the end of the form (v2.2) | create-*-form.tsx |
| add `sx-form-footer-full` | Column layout; use with `sx-btn-block` for the full-width "+ Create …" button (v2.2) | create-*-form.tsx |
| add `sx-form-footer-edit` | Edit mode: Delete left, Cancel + Save changes right; an `sx-alert-error` inside takes the full row (v2.2) | edit-*-drawer.tsx |
| `sx-footer-left` | Left group of an edit footer (Delete + inline confirm) (v2.2) | edit-*-drawer.tsx |

### Live duplicate warning (v2.2)
| Class | Where to use | Source |
|---|---|---|
| `sx-dup-warn` | Amber "possible duplicate" box. Contact form: at the top of the form (default). Company form: inside the Name `sx-field` (then 4px top margin, 11px title, 4px list gap) | create-contact-form.tsx / create-company-form.tsx |
| `sx-dup-title` | Bold first line of the box | same |
| `sx-dup-list` / `sx-dup-row` | List of matches / one match row (label left, link right) | same |
| `sx-dup-label` / `sx-dup-sub` | Match name (ellipsis) / secondary text | same |
| `sx-dup-link` | Link of a match | same |
| `sx-dup-foot` | Footnote under the list (10.5px) | create-company-form.tsx |
| `sx-alert-error sx-dup-email` | Red box: contact email already exists | create-contact-form.tsx |
| `sx-dup-title-error` / `sx-dup-email-text` / `sx-dup-email-link` | Title / text / link inside the red email box | create-contact-form.tsx |

### Badges and chips
| Class | Where to use | Source |
|---|---|---|
| `sx-badge sx-badge-positive` | Success status (sent, paid, active) | STATUS_TINTS.positive |
| `sx-badge sx-badge-warning` | Pending / at risk | STATUS_TINTS.warning |
| `sx-badge sx-badge-negative` | Failed / overdue / rejected | STATUS_TINTS.negative |
| `sx-badge sx-badge-info` | Informational (maroon tint, as in source) | STATUS_TINTS.info |
| `sx-badge sx-badge-neutral` | Draft / inactive / other | STATUS_TINTS.neutral |
| `sx-stage sx-stage-<code>` | Deal stage pill; `<code>` = lead, qualified, proposal, negotiation, hold, won, lost (Style Classes expression `"sx-stage sx-stage-" + ToLower(Stage)`) | STAGE_BADGE_STYLES |
| `sx-chips` | Active-filters bar above a list | filter-chips.tsx bar |
| `sx-chips-label` | "Filters" word at the start of the bar | filter-chips.tsx label |
| `sx-chip` | One removable filter pill | filter-chips.tsx pill |
| `sx-chips-clear` | "Clear all" link | filter-chips.tsx clear all |

### App shell
| Class | Where to use | Source |
|---|---|---|
| `sx-rail` | Left navigation container (maroon gradient, 200px) | sidebar.tsx DesktopSidebar |
| add `sx-rail-collapsed` | Collapsed rail (56px) | sidebar.tsx `w-14` |
| `sx-rail-brand` | Logo row at the top of the rail (v2.2: wordmark 20px semibold white) | sidebar.tsx Brand |
| `sx-rail-logo` | Image RailLogo (resource SalenixLogo, 128px PNG) left of the wordmark in UIKit AppLayout RailBrand; widget Width 22px + this class (flex 0 0 22px, max 22px, object-fit contain; theme rev 7) | lib/brand/logo.tsx SaleinxIcon size 22 |
| `sx-rail-nav` | Scrollable list of nav groups | sidebar.tsx NavList |
| `sx-rail-section` | Group label (10px uppercase, white 50%) | sidebar.tsx group h3 |
| `sx-rail-item` | One nav link; add `sx-active` for the current screen (marigold pill) | sidebar.tsx link |
| `sx-rail-footer` | Collapse toggle row at the bottom | sidebar.tsx CollapseToggle |
| `sx-topbar` | Sticky top header (48px, white 85% blur) | header.tsx |
| `sx-topbar-search` | Search / command trigger in the header | header.tsx search button |
| `sx-kbd` | Shortcut hint ("Ctrl K") | header.tsx kbd |
| `sx-topbar-actions` | Right cluster (bell, workspace, user menu) | header.tsx right div |
| `sx-topbar-workspace` | Workspace name text in the header (v2.1) | workspace-switcher.tsx trigger |
| `sx-shell` | Outer row of the app layout: rail + main (v2.1) | dashboard-shell.tsx |
| `sx-shell-main` | Right column: top bar + content (v2.1) | dashboard-shell.tsx |
| `sx-shell-content` | Content area under the top bar (v2.1) | dashboard-shell.tsx main |

v2.2 rail rules (no new class): `a.sx-rail-item` (white 80% text, white hover, marigold `sx-active` pill) beats the OutSystems UI base link colour; `.sx-shell > .sx-rail` is fixed full height (z-index 40) and `.sx-shell-main` gets margin-left = rail width (collapsed width after `sx-rail-collapsed`); both reset below 768px.

### Auth shell (v2.2, Login)
| Class | Where to use | Source |
|---|---|---|
| `sx-auth-layout` | ExtendedClass of LayoutBlank on Login (removes the `.main-content` padding) | auth-shell.tsx |
| `sx-auth` | Full-height centered page | auth-shell.tsx root |
| `sx-auth-inner` | 26rem column | auth-shell.tsx |
| `sx-auth-header` | Logo + wordmark + subtitle, centered | auth-shell.tsx header |
| `sx-auth-brand` | Wordmark row (28px semibold) | auth-shell.tsx |
| `sx-auth-logo` | Image AuthLogo (resource SalenixLogo) 36px left of the wordmark; widget Width 36px + this class (flex 0 0 36px, max 36px, object-fit contain; theme rev 7) | lib/brand/logo.tsx |
| `sx-auth-subtitle` | Line under the wordmark | auth-shell.tsx |
| `sx-auth-card` | White card around the form (radius 12, padding 24) | sign-in-form.tsx |
| `sx-auth-password` | Wrapper of the password input (room for the eye toggle) | sign-in-form.tsx |
| `sx-auth-actions` | Row of the submit button | sign-in-form.tsx |
| `sx-auth-submit` | Full-width Sign in button (with `btn btn-primary sx-btn sx-btn-primary`) | sign-in-form.tsx |

### Page title group (v2.2)
| Class | Where to use | Source |
|---|---|---|
| `sx-page-titles` | UIKit PageHeader TitleGroup: stacks `sx-page-title` over `sx-page-subtitle` | page header h1 + p |

### List toolbar and filter chips (v2.2)
| Class | Where to use | Source |
|---|---|---|
| `sx-dtoolbar` | Toolbar row: search + New button | deals-view.tsx toolbar |
| `sx-dsearch` / `sx-dsearch-icon` / `sx-dsearch-input` | Search box wrapper / icon / input (with `sx-input`) | deals-view.tsx search |
| `sx-fbar` | Filter chip row | deal-filter-bar.tsx / toggle-filter-bar.tsx |
| `sx-fbar-label` | "Filters" label at the start of the row | same |
| `sx-fchip` (+ `sx-fchip-active`) | Chip button or dropdown chip. A native Dropdown chip gets ONE caret (CSS gradient, currentColor → maroon when active); the OutSystems UI arrow `.dropdown-container::after` is hidden inside `.sx-fbar` (theme rev 7) | deal-filter-bar.tsx PopoverChip ChevronDown |
| `sx-fbar-clear` | "Clear all" link (right) | same |
| `sx-fbar-end` | Right-aligned group (Clear all + Recently deleted) | toggle-filter-bar.tsx |
| `sx-fbar-link` (+ `sx-fbar-link-active`) | Text link in the chip row ("Recently deleted") | companies / contacts view |
| `sx-th-sort` (+ `sx-th-sort-active`) | Sortable table header link | deals-view.tsx headers |
| `sx-deleted-banner` / `sx-deleted-back` | "Showing deleted … · Back" banner / its Back link | companies / contacts view |

### Deal path bar (v2.1)
| Class | Where to use | Source |
|---|---|---|
| `sx-path` | Container of the stage chevron bar | deal-path-bar.tsx root |
| `sx-path-step` | One stage step; add `sx-path-done` (passed) or `sx-path-current` (current); upcoming = no extra class | deal-path-bar.tsx button |

### Drawer
| Class | Where to use | Source |
|---|---|---|
| `sx-drawer` | Outer container of an edit drawer (fixed, full screen); show with an If | drawer.tsx root |
| `sx-drawer-backdrop` | Dark backdrop; its OnClick closes the drawer | drawer.tsx backdrop |
| `sx-drawer-panel` | Right panel (max 28rem, slides in); add `sx-drawer-wide` for 42rem | drawer.tsx aside |
| `sx-drawer-header` | Title row with close button | drawer.tsx header |
| `sx-drawer-title` | Drawer title (16px) | drawer.tsx h2 |
| `sx-drawer-description` | Line under the title | drawer.tsx p |
| `sx-drawer-body` | Scrollable form area | drawer.tsx body |
| `sx-drawer-footer` | Save / Cancel / Delete row | drawer.tsx footer |
| `sx-drawer-heading` | Title + description block, left of the close button (v2.2) | drawer.tsx header |
| `sx-drawer-close` | "×" close button on the right (with `sx-btn sx-btn-ghost`) (v2.2) | drawer.tsx close button |

v2.2: the backdrop shows a pointer; the panel is full width below 640px; form blocks put their footer (`sx-form-footer`) inside `sx-drawer-body`.

### Empty, loading, text helpers
| Class | Where to use | Source |
|---|---|---|
| `sx-empty` | Full-page "no records" state | `border-dashed border-slate-300 bg-slate-50 py-16` |
| `sx-empty-inline` | "No items" inside a card | `border-dashed border-slate-200 bg-slate-50/60 py-5` |
| `sx-empty-title` | Bold first line of an empty state | `text-[13px] font-semibold` |
| `sx-empty-icon` | Icon above the empty-state title (v2.1) | `h-6 w-6 text-slate-400 mb-2` |
| `sx-skeleton` | Loading placeholder bar (set width by container) | loading.tsx `animate-pulse rounded bg-slate-200` |
| `sx-muted` | Secondary text 12px | `text-[12px] text-slate-500` |
| `sx-small` | Meta text 11px (dates, footnotes) | `text-[11px] text-slate-400` |
| `sx-text` | Body text 13px | `text-[13px] text-slate-700` |
| `sx-eyebrow` | Small uppercase label | `text-[10px] font-semibold uppercase` |
| `sx-mono` | Money, counts, ids | `font-mono tabular-nums` |
| `sx-truncate` | One-line ellipsis | `truncate` |
| `sx-text-positive` / `-warning` / `-negative` | Coloured status text | emerald/amber/rose-700 |

## Notes
- Values copied from Tailwind classes; Tailwind v4 status colours (emerald, amber, rose, red, orange) are written as their hex equivalents.
- Doubled selectors (`.sx-btn.sx-btn`) only raise specificity above OutSystems UI widget rules; use the single class name in Style Classes.
- Do not add CSS in other apps; change this file and re-publish Salenix_Theme (from v2.2 on the Salenix app stylesheets stay empty).
- Logo: theme/logo.svg; PNG renders in theme/logo-png/ (512/256/128/64/32, favicon.png = 32). Image SalenixLogo lives in Salenix_UIKit (rail) and on the Salenix Login screen; app icon + favicon are set by hand in Studio.
