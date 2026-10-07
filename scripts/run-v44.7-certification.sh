#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
mkdir -p release/v44.7/evidence
./scripts/verify-v44.7.sh | tee release/v44.7/evidence/static-gate.txt
dotnet restore BankSwitch.sln | tee release/v44.7/evidence/restore.txt
dotnet build BankSwitch.sln -c Release --no-restore -warnaserror | tee release/v44.7/evidence/build.txt
dotnet test tests/BankSwitch.Tests/BankSwitch.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~TransactionFailureRecoveryV447Tests' --logger 'trx;LogFileName=v44.7-failure-recovery.trx' | tee release/v44.7/evidence/failure-recovery-tests.txt
sha256sum db/042_end_to_end_transaction_failure_recovery_certification.sql > release/v44.7/evidence/migration-042-sha256.txt
echo "PASS: v44.7 certification runner completed"
