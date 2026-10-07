#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

echo "=== v44.7 end-to-end transaction & failure-recovery static gate ==="
python3 scripts/validate-migrations.py

test -f db/042_end_to_end_transaction_failure_recovery_certification.sql
test -f src/BankSwitch.Infrastructure/TransactionRecoveryRepositories.cs
test -f src/BankSwitch.Domain/TransactionRecoveryEntities.cs
test -f tests/BankSwitch.Tests/TransactionFailureRecoveryV447Tests.cs
test -f tests/certification/v44.7_failure_recovery_matrix.json
test -f tests/sql/v44.7_failure_recovery_validation.sql

grep -q "SqlTransactionStateRepository" src/BankSwitch.Engine/Program.cs
grep -q "SqlPreAuthStore" src/BankSwitch.Engine/Program.cs
grep -q "SqlStandInRepository" src/BankSwitch.Engine/Program.cs
grep -q "SqlTransactionRecoverySnapshotRepository" src/BankSwitch.Engine/Program.cs
grep -q "UpsertPendingAsync" src/BankSwitch.Application/TransactionProcessor.cs
grep -q "TXN-RECOVERY-REVERSAL" src/BankSwitch.Application/TransactionProcessor.cs
grep -q "MarkTimedOutAsync" src/BankSwitch.Application/TransactionProcessor.cs
grep -q "MarkResolvedAsync" src/BankSwitch.Application/TransactionProcessor.cs
grep -q 'string.Equals(responseCode, "68", StringComparison.Ordinal)' src/BankSwitch.Application/TransactionProcessor.cs
grep -q 'SinkNodeId = sinkNode.Id' src/BankSwitch.Application/TransactionProcessor.cs
grep -q "snapshot.SinkNodeId" src/BankSwitch.Application/TransactionRecoveryService.cs
grep -q "MarkReversedAsync" src/BankSwitch.Application/TransactionRecoveryService.cs
grep -q 'MapGet("/recovery-queue"' src/BankSwitch.Admin/Endpoints/CommandCenterEndpoints.cs

# Ensure recovery scanning uses oldest-safe cutoff, not the historical inverted comparison.
if grep -q "NewState == state && r.OccurredAt >= since" src/BankSwitch.Infrastructure/InMemoryEftRepositories.cs; then
  echo "FAIL: inverted stuck-transaction cutoff still present"; exit 1
fi

# Sensitive recovery payload must be protected before persistence.
grep -q '_dataProtector.Protect(Convert.ToBase64String(recoveryBytes), "TXN-RECOVERY-REVERSAL")' src/BankSwitch.Application/TransactionProcessor.cs

# JavaScript syntax regression gate.
while IFS= read -r -d '' js; do node --check "$js" >/dev/null; done < <(find src/BankSwitch.Admin/wwwroot/command-center -name '*.js' -print0 2>/dev/null)

echo "PASS: v44.7 static failure-recovery certification gate"
