#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"; cd "$root"
fail(){ echo "FAIL: $*" >&2; exit 1; }
pass(){ echo "PASS: $*"; }

[ -f db/043_ndc_lod_parser_configuration_deployment_engine.sql ] || fail "migration 043 missing"
[ -f src/BankSwitch.Application/NdcLodManagement.cs ] || fail "LOD application service missing"
[ -f src/BankSwitch.Infrastructure/SqlNdcLodRepository.cs ] || fail "SQL LOD repository missing"
[ -f tests/fixtures/ndc/ANDC306EMVXT25G.lod ] || fail "golden LOD fixture missing"
[ -f tests/BankSwitch.Tests/NdcLodManagementV448Tests.cs ] || fail "v44.8 tests missing"

grep -q 'INdcLodManagementService' src/BankSwitch.Admin/Program.cs || fail "LOD service not registered"
grep -q 'SqlNdcLodRepository' src/BankSwitch.Admin/Program.cs || fail "SQL LOD repository not registered"
grep -q 'InMemoryNdcLodRepository' src/BankSwitch.Admin/Program.cs || fail "test LOD repository not registered"
grep -q 'MapGroup("/api/atm-lod")' src/BankSwitch.Admin/Endpoints/AtmDrivingEndpoints.cs || fail "ATM LOD API group missing"
grep -q 'ConfigMakerOrChecker' src/BankSwitch.Admin/Endpoints/AtmDrivingEndpoints.cs || fail "maker-checker policy missing"
grep -q "'atm-lod'" src/BankSwitch.Admin/wwwroot/command-center/assets/js/main.js || fail "Command Center route missing"
node --check src/BankSwitch.Admin/wwwroot/command-center/assets/js/pages/atm-lod.js >/dev/null
node --check src/BankSwitch.Admin/wwwroot/command-center/assets/js/main.js >/dev/null

python - <<'PY'
from pathlib import Path
import hashlib,re,sys
p=Path('tests/fixtures/ndc/ANDC306EMVXT25G.lod'); b=p.read_bytes()
assert len(b)==18469, len(b)
records=[x.lstrip(b'\r\n') for x in b.split(b'\x03') if x.strip(b'\r\n')]
assert len(records)==36, len(records)
screens=states=cfg=emv=0
for r in records:
    if r.startswith(b'3\x1c\x1c\x1c11\x1c'):
        screens += sum(1 for f in r.split(b'\x1c')[4:] if len(f)>=4 and f[:3].isdigit())
    elif r.startswith(b'3\x1c\x1c\x1c12\x1c'):
        states += sum(1 for f in r.split(b'\x1c')[4:] if len(f)>=5 and f[:3].isdigit())
    elif r.startswith((b'3\x1c\x1c\x1c15\x1c',b'3\x1c\x1c\x1c1A\x1c')): cfg+=1
    elif r.startswith(b'8\x1c\x1c'): emv+=1
assert screens==393, screens
assert states==267, states
assert cfg==3, cfg
assert emv==4, emv
t=b.decode('ascii','ignore')
aids=[]
for m in re.finditer(r'[\x1c\x1d][0-9]{3}([0-9]{1,2})(A[0-9A-F]{10,40})',t,re.I):
    n=int(m.group(1)); h=m.group(2); aids.append(h[:n*2].upper())
expected={'A0000000031010','A0000000041010','A0000000043060','A0000000651010','A0000005241010','A0000001523010'}
assert expected.issubset(aids), (expected,set(aids))
print('Golden LOD SHA256:',hashlib.sha256(b).hexdigest())
print('Records/screens/states/config/emv:',len(records),screens,states,cfg,emv)
PY
pass "golden ANDC306EMVXT25G structural expectations"

python - <<'PY'
from pathlib import Path
files=sorted(Path('db').glob('[0-9][0-9][0-9]_*.sql'))
nums=[int(p.name[:3]) for p in files]
expected=list(range(1,max(nums)+1))
assert nums==expected,(nums,expected)
assert nums[-1]==43,nums[-1]
print('Migration chain:',nums[0],'-',nums[-1], 'count',len(nums))
PY
pass "migrations contiguous 001-043"

grep -q 'NdcLodPackages' db/canonical/BankSwitch_v44_8_Canonical_SQL_Server_Schema.sql || fail "canonical v44.8 schema missing LOD tables"
pass "canonical v44.8 schema"

echo "PASS: v44.8 NCR NDC LOD parser/configuration/deployment static gate"
