#!/usr/bin/env python3
import hashlib, pathlib, re, sys, json
root=pathlib.Path(__file__).resolve().parents[1]
db=root/'db'
files=sorted(db.glob('*.sql'))
errors=[]
nums=[]
manifest=[]
for f in files:
    m=re.match(r'^(\d{3})_',f.name)
    if not m:
        errors.append(f'non-versioned migration: {f.name}'); continue
    n=int(m.group(1)); nums.append(n)
    b=f.read_bytes()
    manifest.append({'version':n,'file':f.name,'sha256':hashlib.sha256(b).hexdigest(),'bytes':len(b)})
expected=list(range(1,max(nums)+1 if nums else 1))
if nums!=expected: errors.append(f'migration sequence is not contiguous: {nums}')
if nums and nums[-1] < 42: errors.append(f'expected v44.7 migration 042 or later, found {nums[-1]:03d}')
for f in files:
    text=f.read_text(encoding='utf-8',errors='replace')
    if re.search(r'\bDROP\s+DATABASE\b|\bTRUNCATE\s+TABLE\b',text,re.I): errors.append(f'destructive statement requires explicit review: {f.name}')
    if re.search(r'\bALTER\s+TABLE\b.+\bDROP\s+COLUMN\b',text,re.I|re.S): errors.append(f'drop-column migration requires explicit review: {f.name}')
(root/'release/v44.7/evidence').mkdir(parents=True,exist_ok=True)
(root/'release/v44.7/evidence/migration-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
if errors:
    print('\n'.join('FAIL: '+x for x in errors)); sys.exit(1)
print(f'PASS: {len(files)} migrations are contiguous 001-{nums[-1]:03d} and manifest generated')
