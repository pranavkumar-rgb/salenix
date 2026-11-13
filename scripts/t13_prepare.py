"""Build the T13 "Replace deals" input from the real-data files (run with python -I).

Reads  data/salenix-import-deals-v2.json   (oldDeals, deals)
       data/salenix-import-contacts.json   (companies key -> name, contacts row -> email)
Writes data/salenix-t13-input.json          (git-ignored; paste into DevTools T13)

Prints counts and row numbers only, never row contents.
Output shape (matches the Salenix_Sales structures ReplaceOldDealRow / ReplaceNewDealRow):
  {"OldDeals": [{"Row", "CompanyName", "Name"}],
   "Deals":    [{"Row", "CompanyName", "Name", "Stage", "Contacts": [{"Row", "Email"}], "PrimaryRow"}]}
"""
import collections
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
V2 = ROOT / "data" / "salenix-import-deals-v2.json"
T11 = ROOT / "data" / "salenix-import-contacts.json"
OUT = ROOT / "data" / "salenix-t13-input.json"
STAGES = {"lead", "qualified", "proposal", "negotiation", "hold"}


def main() -> int:
    v2 = json.loads(V2.read_text(encoding="utf-8"))
    t11 = json.loads(T11.read_text(encoding="utf-8"))
    company_by_key = {c["key"]: c["name"].strip() for c in t11["companies"]}
    company_names = set(company_by_key.values())
    email_by_row = {c["row"]: (c.get("email") or "").strip() for c in t11["contacts"]}
    problems: list[str] = []

    old = []
    for i, r in enumerate(v2["oldDeals"], 1):
        key = r.get("companyKey")
        name = company_by_key.get(key, "") if key not in (None, "") else (r.get("companyName") or "").strip()
        if key not in (None, "") and key not in company_by_key:
            problems.append(f"oldDeals #{i}: companyKey not in T11 companies (matched as 'no company', like T11)")
        old.append({"Row": i, "CompanyName": name, "Name": (r.get("name") or "").strip()})

    new = []
    for i, r in enumerate(v2["deals"], 1):
        key = r.get("companyKey")
        if key not in (None, ""):
            cname = company_by_key.get(key, "")
        else:
            cname = (r.get("companyName") or r.get("name") or "").strip()
        if cname not in company_names:
            problems.append(f"deals #{i}: company name not among T11 companies")
        stage = (r.get("stage") or "").strip().lower()
        if stage not in STAGES:
            problems.append(f"deals #{i}: stage not importable")
        rows = list(r.get("contactRows") or [])
        prim = r.get("primaryContactRow") or 0
        if prim and prim not in rows:
            rows.insert(0, prim)
        for cr in rows:
            if cr not in email_by_row:
                problems.append(f"deals #{i}: contact row {cr} not in T11 contacts")
        new.append({
            "Row": i,
            "CompanyName": cname,
            "Name": (r.get("name") or "").strip(),
            "Stage": stage,
            "Contacts": [{"Row": cr, "Email": email_by_row.get(cr, "")} for cr in rows],
            "PrimaryRow": prim,
        })

    old_pairs = collections.Counter((o["CompanyName"].lower(), o["Name"].lower()) for o in old)
    new_pairs = collections.Counter((n["CompanyName"].lower(), n["Name"].lower()) for n in new)
    overlap = [n["Row"] for n in new if (n["CompanyName"].lower(), n["Name"].lower()) in old_pairs]

    print("oldDeals", len(old), "| duplicate company+name", sum(v - 1 for v in old_pairs.values() if v > 1))
    print("deals", len(new), "| duplicate company+name", sum(v - 1 for v in new_pairs.values() if v > 1))
    print("stages", dict(collections.Counter(n["Stage"] for n in new)))
    print("deals without primary contact", [n["Row"] for n in new if not n["PrimaryRow"]])
    print("new deals with the same company+name as an old deal (handled by the t13 marker):", overlap)
    print("deal name length > 200:", [n["Row"] for n in new if len(n["Name"]) > 200])
    print("problems:", len(problems))
    for p in problems:
        print("  ", p)

    OUT.write_text(json.dumps({"OldDeals": old, "Deals": new}, ensure_ascii=False), encoding="utf-8")
    print("written", OUT.relative_to(ROOT), OUT.stat().st_size, "bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
