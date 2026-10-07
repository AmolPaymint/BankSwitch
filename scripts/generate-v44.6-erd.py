#!/usr/bin/env python3
from pathlib import Path
import re,csv,json
root=Path(__file__).resolve().parents[1]
db=root/'db'; out=root/'docs'/'database'; out.mkdir(parents=True,exist_ok=True)
files=sorted(db.glob('[0-9][0-9][0-9]_*.sql'))
tables=set(); edges=set(); columns={}
for f in files:
    txt=f.read_text(encoding='utf-8',errors='replace')
    # parse table blocks loosely
    pos=0
    while True:
        m=re.search(r'CREATE\s+TABLE\s+(?:dbo\.)?\[?([A-Za-z_][A-Za-z0-9_]*)\]?\s*\(',txt[pos:],re.I)
        if not m: break
        table=m.group(1); start=pos+m.end(); tables.add(table)
        depth=1; i=start
        while i<len(txt) and depth:
            ch=txt[i]
            if ch=='(': depth+=1
            elif ch==')': depth-=1
            i+=1
        block=txt[start:i-1]
        cols=[]
        for line in block.splitlines():
            cm=re.match(r'\s*\[?([A-Za-z_][A-Za-z0-9_]*)\]?\s+([A-Za-z][A-Za-z0-9_]*(?:\s*\([^\)]*\))?)',line)
            if cm and cm.group(1).upper() not in {'CONSTRAINT','PRIMARY','FOREIGN','UNIQUE','CHECK'}:
                cols.append({'name':cm.group(1),'type':cm.group(2).strip()})
        if cols: columns[table]=cols
        for r in re.finditer(r'(?:FOREIGN\s+KEY\s*\(\s*\[?([A-Za-z_][A-Za-z0-9_]*)\]?\s*\)\s*)?REFERENCES\s+(?:dbo\.)?\[?([A-Za-z_][A-Za-z0-9_]*)\]?\s*\(\s*\[?([A-Za-z_][A-Za-z0-9_]*)\]?',block,re.I):
            child_col=r.group(1) or '?'; edges.add((table,child_col,r.group(2),r.group(3)))
        pos=i
    # ALTER TABLE FK statements
    for m in re.finditer(r'ALTER\s+TABLE\s+(?:dbo\.)?\[?([A-Za-z_][A-Za-z0-9_]*)\]?.*?FOREIGN\s+KEY\s*\(\s*\[?([A-Za-z_][A-Za-z0-9_]*)\]?\s*\)\s+REFERENCES\s+(?:dbo\.)?\[?([A-Za-z_][A-Za-z0-9_]*)\]?\s*\(\s*\[?([A-Za-z_][A-Za-z0-9_]*)\]?',txt,re.I|re.S):
        edges.add((m.group(1),m.group(2),m.group(3),m.group(4)))
# known dynamic stores
for t in ['AcquiringCertificationStore','AcquiringCertificationLabStore','IssuerCertificationStore']:
    tables.add(t)

with (out/'BankSwitch_v44_6_Relationships.csv').open('w',newline='',encoding='utf-8') as f:
    w=csv.writer(f); w.writerow(['child_table','child_column','parent_table','parent_column'])
    for e in sorted(edges,key=lambda x:(x[0].lower(),x[2].lower(),x[1].lower())): w.writerow(e)
(out/'BankSwitch_v44_6_Schema_Inventory.json').write_text(json.dumps({'tables':sorted(tables,key=str.lower),'columns':columns,'relationships':[{'child_table':a,'child_column':b,'parent_table':c,'parent_column':d} for a,b,c,d in sorted(edges)]},indent=2)+'\n',encoding='utf-8')

def domain(t):
    x=t.lower()
    if x.startswith(('pos','merchant_')): return 'POS / Merchant'
    if x.startswith(('atm','ndc')): return 'ATM / NDC'
    if x.startswith(('issuer_cert','acquiring_cert','emv_contactless')): return 'Certification'
    if x.startswith(('configuration','featureflags','certificateinventory','secretreferences','repositorytable')): return 'Configuration'
    if x.startswith(('risk_','aml_','fraud')): return 'Risk / AML'
    if x.startswith(('ops_','alert','cluster','failover','disaster')): return 'Operations'
    if x.startswith(('gl','settlement','clearing','reconciliation','netsettlement')): return 'Financial'
    if x.startswith(('prepaid','customer','wallet','card','limit','kyc','authorization','debitcard','hotlist')): return 'Cards / CMS'
    if x.startswith(('network_','iso8583','enterprise_','cbs_','dwh_')): return 'Network / Integration'
    if x.startswith(('compliance','audit_','security_','secure_','access_','retention_','pci','hsm','dukpt','totp')): return 'Security / Compliance'
    return 'Other'

clusters={}
for t in tables: clusters.setdefault(domain(t),[]).append(t)

dot=['digraph BankSwitchV446 {','  graph [rankdir=LR, overlap=false, splines=polyline, bgcolor="white", fontname="Arial"];','  node [shape=box, style="rounded,filled", fillcolor="#f7f9fc", color="#8aa0bd", fontname="Arial", fontsize=8];','  edge [color="#8a98aa", arrowsize=0.6, fontname="Arial", fontsize=7];']
for idx,(d,ts) in enumerate(sorted(clusters.items())):
    dot.append(f'  subgraph cluster_{idx} {{ label="{d}"; color="#d7dee8"; style="rounded";')
    for t in sorted(ts,key=str.lower): dot.append(f'    "{t}";')
    dot.append('  }')
for a,b,c,d in sorted(edges):
    if a in tables and c in tables: dot.append(f'  "{a}" -> "{c}" [label="{b} → {d}"];')
dot.append('}')
(out/'BankSwitch_v44_6_Authoritative_ERD.dot').write_text('\n'.join(dot)+'\n',encoding='utf-8')
print(f'tables={len(tables)} relationships={len(edges)}')
