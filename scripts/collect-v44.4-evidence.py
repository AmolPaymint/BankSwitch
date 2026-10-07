#!/usr/bin/env python3
from pathlib import Path
import hashlib, json, datetime

root = Path(__file__).resolve().parents[1]
ev = root / "release" / "v44.4" / "evidence"
ev.mkdir(parents=True, exist_ok=True)

files = sorted(p for p in ev.rglob("*") if p.is_file() and p.name not in {"SHA256SUMS.txt", "closure-summary.json"})
lines=[]
for p in files:
    h=hashlib.sha256(p.read_bytes()).hexdigest()
    lines.append(f"{h}  {p.relative_to(root).as_posix()}")
(ev/"SHA256SUMS.txt").write_text("\n".join(lines)+("\n" if lines else ""), encoding="utf-8")

markers = {
    "static": ev/"static-gate.txt",
    "restore": ev/"dotnet-restore.txt",
    "build": ev/"dotnet-build.txt",
    "tests": ev/"dotnet-test.txt",
    "fresh_sql": ev/"sql-fresh-validation.txt",
    "upgrade_sql": ev/"sql-upgrade-validation.txt",
}
summary={"generatedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "gates":{}}
for name,p in markers.items():
    if not p.exists():
        status="NOT_RUN"
    else:
        text=p.read_text(encoding="utf-8",errors="ignore")
        upper=text.upper()
        status="FAIL" if any(x in upper for x in ["BUILD FAILED","TEST RUN FAILED","FAIL:","ERROR:"]) else "PASS"
    summary["gates"][name]={"status":status,"evidence":str(p.relative_to(root))}
(ev/"closure-summary.json").write_text(json.dumps(summary,indent=2),encoding="utf-8")
print(json.dumps(summary,indent=2))
