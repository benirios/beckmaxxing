"""Report explicit cBot coverage against the current Obsidian catalog.

This is an inventory, not a strategy verifier. A mapping alone never proves
that a bot follows its hypothesis. Retired headings (####) are not counted.
"""
import json
import re
from pathlib import Path


def inventory():
    directory = Path(__file__).resolve().parent
    catalog = directory.parent / "01 - Research" / "Thesis Catalog.md"
    entries = re.findall(r"^### (T\d{3}) — (.+)$", catalog.read_text(), re.M)
    coverage = json.loads((directory / "catalog-coverage.json").read_text())
    missing = []
    for number, title in entries:
        source = coverage.get(number)
        if source and (directory / source).is_file():
            print(f"SOURCE {number} {source}")
        else:
            missing.append(number)
            print(f"MISSING {number} {title}")
    print(f"Catalog: {len(entries)} active hypotheses; sources: {len(entries)-len(missing)}; missing: {len(missing)}; target: 100")
    return entries, missing


if __name__ == "__main__":
    inventory()
