#!/usr/bin/env python3
"""Merge new or updated summaries into summaries.json (web and Android copies), with a backup and validation.

Input: a JSON file of new entries keyed by NRIS ref number:
    {"14001042": {"summary": "...", "heading": "Statement of Significance"}, ...}

Usage (from the repo root):
    python tools/summaries/merge_summaries.py new_entries.json
    python tools/summaries/merge_summaries.py new_entries.json --dry-run

Both targets must be identical before the merge; they are identical again afterward.
"""
import argparse, filecmp, json, shutil, sys, time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TARGETS = [ROOT / "web" / "summaries.json", ROOT / "android" / "app" / "src" / "main" / "assets" / "summaries.json"]


def valid_entry(v):
    return isinstance(v, dict) and isinstance(v.get("summary"), str) and v["summary"].strip() and isinstance(v.get("heading", ""), str)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("entries", help="JSON file of {ref: {summary, heading}}")
    ap.add_argument("--dry-run", action="store_true")
    a = ap.parse_args()

    new = json.loads(Path(a.entries).read_text(encoding="utf-8"))
    bad = [r for r, v in new.items() if not valid_entry(v)]
    if bad:
        sys.exit(f"Invalid entries: {bad[:10]}")
    main_file, *copies = TARGETS
    for c in copies:
        if not filecmp.cmp(main_file, c, shallow=False):
            sys.exit(f"{main_file} and {c} differ; fix that before merging.")

    data = json.loads(main_file.read_text(encoding="utf-8"))
    added = [r for r in new if r not in data]
    updated = [r for r in new if r in data and data[r] != new[r]]
    for r, v in new.items():
        data[r] = {"summary": v["summary"].strip(), "heading": v.get("heading", "")}
    out = json.dumps(data, indent=1, ensure_ascii=False)
    json.loads(out)  # round-trip check
    print(f"{len(data)} entries after merge: {len(added)} added, {len(updated)} updated")
    if a.dry_run:
        return

    backup = main_file.with_name(f"summaries.backup_{time.strftime('%Y%m%d_%H%M%S')}.json")
    shutil.copy(main_file, backup)
    tmp = main_file.with_suffix(".tmp")
    tmp.write_text(out, encoding="utf-8")
    tmp.replace(main_file)
    for c in copies:
        shutil.copy(main_file, c)
    print(f"Wrote {', '.join(str(t.relative_to(ROOT)) for t in TARGETS)} (backup: {backup.name}, not committed)")


if __name__ == "__main__":
    main()
