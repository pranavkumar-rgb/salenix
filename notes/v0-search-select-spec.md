# V0-2 SxSearchSelect — block spec (Salenix)

Theme classes (Salenix_Theme rev 9, v2.4): sx-ssel, sx-ssel-trigger (+ sx-input), sx-ssel-value, sx-ssel-placeholder, sx-ssel-disabled, sx-ssel-pop, sx-ssel-search, sx-ssel-list, sx-ssel-item, sx-ssel-item-active, sx-ssel-add, sx-ssel-sub, sx-ssel-empty, sx-ssel-backdrop.

## Generic block SxSearchSelect
Inputs
- Options (UiOption List) — Value / Label
- Value (Text) — selected value ("" = none)
- ValueLabel (Text, default "") — label shown when Value is not in Options (current value always visible)
- Placeholder (Text, default "Select…")
- EmptyLabel (Text, default "") — when not empty: first row with Value "" (e.g. "Unassigned", "— None —")
- IsDisabled (Boolean, default False)
- ServerSearch (Boolean, default False) — True: typing raises SearchChanged after 250 ms (parent reloads Options); False: client filter (Label contains text, case-insensitive)
- AllowAdd (Boolean, default False), AddPrefix (Text, default "+ Add"), AddSuffix (Text, default "")
- SearchPlaceholder (Text, default "Search…")
Events
- Changed(Value Text, Label Text) — mandatory
- SearchChanged(Search Text) — optional
- AddRequested(Text Text) — optional
Locals: IsOpen (Boolean), SearchText (Text)
Widget tree
- Container sx-ssel (+ " sx-ssel-disabled" when IsDisabled)
  - Link Trigger (classes "sx-input sx-ssel-trigger"), OnClick ToggleOpen (no-op when disabled)
    - Expression class sx-ssel-value (DisplayLabel) or sx-ssel-placeholder (Placeholder) when nothing selected and EmptyLabel = ""
  - If IsOpen:
    - Link Backdrop sx-ssel-backdrop → Close
    - Container sx-ssel-pop
      - Input SearchInput (variable SearchText, class sx-ssel-search, prompt SearchPlaceholder) OnChange SearchOnChange
      - Container sx-ssel-list
        - If EmptyLabel <> "" and SearchText = "": Link sx-ssel-item (+ active when Value = "") → Pick("", EmptyLabel)
        - List over Options; item If (ServerSearch or Trim(SearchText) = "" or Index(ToLower(Label), ToLower(Trim(SearchText))) <> -1) → Link sx-ssel-item (+ sx-ssel-item-active when Value = Current.Value) → Pick(Value, Label)
        - If no visible rows and add row hidden → Text "No matches" sx-ssel-empty
        - If AllowAdd and Trim(SearchText) <> "" and no option Label equals Trim(SearchText) (case-insensitive) → Link sx-ssel-item sx-ssel-add text AddPrefix + " “" + Trim(SearchText) + "”" + AddSuffix → AddRequested(Trim(SearchText)), Close
Actions
- ToggleOpen: if IsDisabled end; IsOpen = not IsOpen; when opening: SearchText = ""; if ServerSearch trigger SearchChanged(""); JS focus the search input (setTimeout 0)
- Close: IsOpen = False
- Pick(V, L): IsOpen = False; trigger Changed(V, L)
- SearchOnChange: if ServerSearch: JS debounce 250 ms (window timer per widget id) → $actions.FireSearch(); else nothing (client filter)
- FireSearch: trigger SearchChanged(SearchText)
- Esc key in the search input closes (JS keydown on the input)
- OnDestroy: clear the debounce timer
DisplayLabel = if Value = "": EmptyLabel; else label of the option with that Value, else ValueLabel, else Value.

## Data wrappers (Salenix blocks)
- CompanySelect(CompanyId Long Integer, CompanyLabel Text, Placeholder, EmptyLabel, IsDisabled) → Changed(CompanyId, Label). Data action GetOptions(Search) = Company_ListOptions(Search, IncludeId = CompanyId) → UiOption (Value = LongIntegerToText(Id)). ServerSearch True.
- ContactSelect(CompanyId filter, ContactId, ContactLabel, Placeholder, EmptyLabel, IsDisabled) → Changed(ContactId, Label). Contact_ListOptions(CompanyId, Search, IncludeId = ContactId). ServerSearch True. Refetch when CompanyId changes (OnParametersChanged).
- MemberSelect(UserIdText, EmptyLabel, Placeholder, IsDisabled) → Changed(UserIdText, Label). Member_ListOptions("") once (≤ 200 rows), client filter; label Label, sub Email.
- OfferingNameSelect(NameValue Text, IsDisabled) → Changed(Name, OfferingId). Offering_List(False) → UiOption (Value = Name, Label = Name); AllowAdd True, AddSuffix " as a new deal name"; AddRequested → Offering_Create(Name) wrapper → on ok / existing: refresh list, Changed(Name, OfferingId); on retired / validation_error: show message under the field.
