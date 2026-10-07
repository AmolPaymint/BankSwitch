#!/usr/bin/env python3
from pathlib import Path
import sys, xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
results = root / "release" / "v44.4" / "evidence" / "TestResults"
files = sorted(results.rglob("coverage.cobertura.xml"))
minimum = float(sys.argv[1]) if len(sys.argv)>1 else 60.0
if not files:
    print("FAIL: coverage.cobertura.xml not found")
    sys.exit(1)
rates=[]
for f in files:
    tree=ET.parse(f)
    rate=float(tree.getroot().attrib.get("line-rate","0"))*100
    rates.append((f,rate))
# Report the lowest collected result to avoid accidentally hiding a weak shard.
lowest=min(rate for _,rate in rates)
for f,rate in rates:
    print(f"coverage {rate:.2f}%  {f.relative_to(root)}")
if lowest < minimum:
    print(f"FAIL: line coverage {lowest:.2f}% < required {minimum:.2f}%")
    sys.exit(1)
print(f"PASS: line coverage {lowest:.2f}% >= required {minimum:.2f}%")
