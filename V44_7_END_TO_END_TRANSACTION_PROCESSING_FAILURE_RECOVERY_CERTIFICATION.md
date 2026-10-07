# v44.7 — End-to-End Transaction Processing & Failure-Recovery Certification

## Objective

v44.7 hardens the transaction path around the highest-risk failure window: a request has been forwarded to an issuer/acquirer but the switch crashes, times out, disconnects, or loses the response before a final transaction log is committed.

## Implemented

- Durable transaction recovery snapshot written **before upstream send**.
- Recovery snapshot contains an encrypted/protected pre-built ISO 8583 0420 reversal payload; PAN is never stored in clear text in the recovery table.
- Exact original sink node id is persisted for recovery; recovery no longer guesses the sink from current routing configuration.
- SQL Server production implementation for `ITransactionStateRepository`.
- SQL Server production implementation for `IPreAuthStore`.
- SQL Server production implementation for `IStandInRepository`, including persistent velocity events.
- SQL Server production implementation for `ITransactionRecoverySnapshotRepository`.
- `TransactionRecoveryService` now consumes durable recovery snapshots and performs accepted/retry/exhausted outcomes without falsely marking rejected reversals as successful.
- Corrected historical stuck-state cutoff logic: only the **latest** transaction state at/before the configured threshold is considered stuck.
- Normal sink responses mark recovery snapshots `Resolved`; sink timeouts mark them `TimedOut` and leave them eligible for recovery.
- The TCP sink client's ISO response `68` timeout sentinel is explicitly classified as `TimedOut` rather than `Resolved`, preserving compensating-reversal eligibility.
- Recovery retry uses `ReversalOptions` backoff and maximum-attempt policy.
- Command Center read-only Operations API: `GET /api/command-center/recovery-queue`.
- Migration `042_end_to_end_transaction_failure_recovery_certification.sql`.
- Certification evidence tables for controlled execution evidence.
- 15-scenario failure-recovery certification matrix.
- xUnit v44.7 deterministic failure-recovery tests.
- SQL Server schema assertions for v44.7.
- PowerShell and Linux certification runners.

## Certification scenarios

The qualification matrix covers normal authorization, issuer decline, timeout, unknown outcome after process crash, recovery reversal acceptance, retry, attempt exhaustion, duplicate suppression, primary/fallback routing, STIP, pre-auth persistence, crash/restart persistence, encrypted recovery payload, observability and SQL migration integrity.

## Critical correction from pre-v44.7 behavior

Earlier crash recovery depended on transaction lifecycle records plus a reversal work item / transaction log that might not exist if the process died immediately after writing `ForwardedToSink`. It could also mark a transaction as reversed when no reversal had actually been dispatched. v44.7 removes that unsafe assumption by committing the encrypted recovery snapshot before network send and only transitioning to `Reversed` after an accepted recovery response.

## Production behavior

In `Repository:Provider=SqlServer` mode the engine now uses SQL-backed lifecycle, pre-auth, stand-in and recovery repositories. In-memory implementations remain for development/tests only.

## Remaining external proof

v44.7 supplies the code and automated qualification framework. Final production certification still requires execution with .NET 8, SQL Server, Redis, a certified HSM/CBS/network test environment, controlled sink timeout/disconnect injection, and signed evidence from the bank/scheme test team.

## Execute

Windows:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File ".\scripts\run-v44.7-certification.ps1"
```

Linux:

```bash
./scripts/run-v44.7-certification.sh
```
