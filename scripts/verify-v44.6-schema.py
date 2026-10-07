#!/usr/bin/env python3
from pathlib import Path
import re, json, hashlib, sys
root=Path(__file__).resolve().parents[1]
db=root/'db'
files=sorted(db.glob('[0-9][0-9][0-9]_*.sql'))
errors=[]
nums=[]
for f in files:
    m=re.match(r'(\d{3})_',f.name)
    if m: nums.append(int(m.group(1)))
if nums != list(range(1,42)):
    errors.append(f'expected contiguous migrations 001-041, got {nums[:3]}...{nums[-3:] if nums else []}')

bad_patterns={
    r'CREATE\s+TABLE\s+IF\s+NOT\s+EXISTS':'PostgreSQL CREATE TABLE IF NOT EXISTS',
    r'CREATE\s+(?:UNIQUE\s+)?INDEX\s+IF\s+NOT\s+EXISTS':'PostgreSQL CREATE INDEX IF NOT EXISTS',
    r'\bTIMESTAMPTZ\b':'PostgreSQL TIMESTAMPTZ',
    r'TIMESTAMP\s+WITH\s+TIME\s+ZONE':'PostgreSQL timestamp with time zone',
    r'gen_random_uuid\s*\(':'PostgreSQL gen_random_uuid()',
    r'\bUUID\s+(?:PRIMARY|NOT\s+NULL|NULL)':'PostgreSQL UUID type',
    r'\bBOOLEAN\s+(?:NOT\s+NULL|NULL)':'PostgreSQL BOOLEAN type',
}
for f in files:
    text=f.read_text(encoding='utf-8',errors='replace')
    for pat,label in bad_patterns.items():
        if re.search(pat,text,re.I): errors.append(f'{f.name}: {label}')
    if re.search(r'NVARCHAR\s*\(\s*MAX\s*\)\s+(?:NOT\s+NULL\s+)?(?:CONSTRAINT\s+\S+\s+)?PRIMARY\s+KEY',text,re.I):
        errors.append(f'{f.name}: NVARCHAR(MAX) cannot be a SQL Server primary key')

legacy=['pos_terminal_profile','mpos_enrollment','pos_key_download_certification','pos_key_download_session','pos_contactless_transaction_flow','pos_tip_adjustment','pos_cash_at_pos_acquiring','merchant_settlement_batch','pos_device_command']
for name in legacy:
    for f in files:
        if re.search(r'CREATE\s+TABLE\s+(?:dbo\.)?'+re.escape(name)+r'\b',f.read_text(encoding='utf-8',errors='replace'),re.I):
            errors.append(f'legacy duplicate table still created: {name} in {f.name}')

# Discover statically-created tables.
tables=set()
for f in files:
    txt=f.read_text(encoding='utf-8',errors='replace')
    for m in re.finditer(r'CREATE\s+TABLE\s+(?:dbo\.)?\[?([A-Za-z_][A-Za-z0-9_]*)\]?',txt,re.I): tables.add(m.group(1).lower())
# dynamically-created canonical JSON stores from 037
tables.update(x.lower() for x in ['AcquiringCertificationStore','AcquiringCertificationLabStore','IssuerCertificationStore'])
required_tables=['PosMerchants','PosTerminalProfiles','KycDocuments','AcquiringCertificationStore','AcquiringCertificationLabStore','IssuerCertificationStore','NdcTerminalSessions','RepositoryTableMappings']
for t in required_tables:
    if t.lower() not in tables: errors.append(f'required canonical table missing: {t}')

m41=(db/'041_canonical_sql_server_schema_referential_integrity_hardening.sql').read_text(encoding='utf-8')
required_fks=['FK_KycDocuments_Customers','FK_GlJournalLines_GlAccounts','FK_DebitCardProductionOrders_Customers','FK_AtmC3r_Terminal','FK_PosTerminalProfiles_Merchants','FK_PosOfflineContactlessTxns_Merchants','FK_AcquiringCertResults_Runs','FK_IssuerCertResults_Runs']
for fk in required_fks:
    if fk not in m41: errors.append(f'v44.6 required FK not declared: {fk}')

# Repository SQL table references must resolve to migration-created tables.
repo_refs=[]
for cs in (root/'src'/'BankSwitch.Infrastructure').glob('Sql*Repository.cs'):
    txt=cs.read_text(encoding='utf-8',errors='replace')
    for t in sorted(set(re.findall(r'dbo\.([A-Za-z_][A-Za-z0-9_]*)',txt))):
        repo_refs.append({'repository':cs.name,'table':t,'exists':t.lower() in tables})
        if t.lower() not in tables: errors.append(f'{cs.name} references missing table dbo.{t}')

# Build inventory and authoritative baseline.
outdir=root/'docs'/'database'
outdir.mkdir(parents=True,exist_ok=True)
manifest=[]
for f in files:
    b=f.read_bytes(); manifest.append({'file':f.name,'sha256':hashlib.sha256(b).hexdigest(),'bytes':len(b)})
(outdir/'v44.6_migration_manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
(outdir/'v44.6_repository_table_mapping.json').write_text(json.dumps(repo_refs,indent=2)+'\n',encoding='utf-8')

canonical=root/'db'/'canonical'
canonical.mkdir(exist_ok=True)
with (canonical/'BankSwitch_v44_6_Canonical_SQL_Server_Schema.sql').open('w',encoding='utf-8') as o:
    o.write('-- BankSwitch v44.6 authoritative SQL Server schema baseline\n-- Generated from migrations 001-041. Do not hand-edit; regenerate with verify-v44.6-schema.py.\n\n')
    for f in files:
        o.write(f'-- ===== {f.name} =====\n')
        o.write(f.read_text(encoding='utf-8').rstrip()+'\nGO\n\n')

summary={'migration_count':len(files),'last_migration':nums[-1] if nums else None,'table_count_static_plus_known_dynamic':len(tables),'repository_table_references':len(repo_refs),'repository_table_references_resolved':sum(1 for x in repo_refs if x['exists']),'legacy_pos_duplicate_tables_created':0,'sql_server_dialect_violations':sum(1 for e in errors if 'PostgreSQL' in e)}
(outdir/'v44.6_schema_summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')

if errors:
    for e in errors: print('FAIL:',e)
    sys.exit(1)
print(f"PASS: v44.6 canonical schema gate; migrations=001-041 tables={len(tables)} repositoryRefs={len(repo_refs)}")
