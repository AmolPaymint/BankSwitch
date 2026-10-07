#!/usr/bin/env python3
import json, pathlib, re, sys
root=pathlib.Path(__file__).resolve().parents[2]
files=[root/'src/BankSwitch.Admin/appsettings.Production.json', root/'src/BankSwitch.Engine/appsettings.Production.json']
errors=[]

def get(d,*keys):
    for k in keys:
        if not isinstance(d,dict) or k not in d: return None
        d=d[k]
    return d

for f in files:
    data=json.loads(f.read_text(encoding='utf-8'))
    label=f.relative_to(root)
    if str(get(data,'Repository','Provider')).lower()!='sqlserver': errors.append(f'{label}: Repository.Provider must be SqlServer')
    text=f.read_text(encoding='utf-8')
    if re.search(r'"Provider"\s*:\s*"InMemory"',text,re.I): errors.append(f'{label}: InMemory provider is forbidden in production')
    if re.search(r'"Mode"\s*:\s*"(Mock|Bypass|InMemory)"',text,re.I): errors.append(f'{label}: Mock/Bypass/InMemory mode detected')
    for m in re.finditer(r'"(?:Password|Secret|Key)"\s*:\s*"([^"]+)"',text,re.I):
        value=m.group(1)
        if value and not ('${' in value or 'REPLACE-' in value or 'DISABLE-' in value):
            errors.append(f'{label}: possible literal secret value detected')

engine=json.loads((root/'src/BankSwitch.Engine/appsettings.Production.json').read_text())
if get(engine,'SourceGateway','RequireMutualTls') is not True: errors.append('Engine: SourceGateway.RequireMutualTls must be true')
if get(engine,'Sink','UseTls') is not True: errors.append('Engine: Sink.UseTls must be true')
if get(engine,'SourceGateway','AllowInvalidClientCertificateForDevelopmentOnly') is not False: errors.append('Engine: invalid client certificates must be disabled')
if get(engine,'Sink','AllowInvalidServerCertificateForDevelopmentOnly') is not False: errors.append('Engine: invalid server certificates must be disabled')
if str(get(engine,'Hsm','Mode')).lower() in {'mock','software','bypass','inmemory'}: errors.append('Engine: production HSM mode cannot be mock/software/bypass/inmemory')

if errors:
    print('\n'.join('FAIL: '+e for e in errors)); sys.exit(1)
print('PASS: production configuration safety controls')
