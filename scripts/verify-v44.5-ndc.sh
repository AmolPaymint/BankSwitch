#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
python3 scripts/validate-migrations.py
required=(
  src/BankSwitch.Application/NdcProtocolEngine.cs
  src/BankSwitch.Infrastructure/SqlNdcProtocolRepository.cs
  db/040_production_ndc_ndcplus_atm_protocol_engine.sql
  tests/BankSwitch.Tests/NdcProtocolEngineV445Tests.cs
  tests/sql/v44.5_ndc_schema.sql
  V44_5_PRODUCTION_NDC_NDCPLUS_ATM_PROTOCOL_ENGINE.md
)
for f in "${required[@]}"; do test -f "$f" || { echo "FAIL: missing $f"; exit 1; }; done
! grep -q 'class NdcProtocolDriver : DelimitedTextAtmProtocolDriver' src/BankSwitch.Application/AtmDrivingService.cs
! grep -q 'class NdcPlusProtocolDriver : DelimitedTextAtmProtocolDriver' src/BankSwitch.Application/AtmDrivingService.cs
grep -q 'class NdcProtocolDriver : IAtmProtocolDriver' src/BankSwitch.Application/NdcProtocolEngine.cs
grep -q 'class NdcPlusProtocolDriver : IAtmProtocolDriver' src/BankSwitch.Application/NdcProtocolEngine.cs
grep -q 'INdcProtocolRepository, SqlNdcProtocolRepository' src/BankSwitch.Admin/Program.cs
grep -q '/ndc/simulator/scenario' src/BankSwitch.Admin/Endpoints/AtmDrivingEndpoints.cs
grep -q 'Production NDC/NDC+ requires Ndc:MacKeyHex' src/BankSwitch.Admin/Program.cs
echo 'PASS: v44.5 NDC/NDC+ static verification'
