# BankSwitch v43 Build Error Fixes

This patch addresses the compile errors reported in Error (1).txt against the v43 codebase.

## Fixed
- Removed stale AuditEvidenceService dependency on obsolete IJournalEngine.VerifyChainIntegrityAsync.
- AuditEvidenceService now uses ILedgerIntegrityService.VerifyFullChainAsync and LedgerIntegrityResult.
- Removed obsolete NullJournalEngine methods referencing missing ChainIntegrityResult and PostJournalRequest.
- Updated Engine/Admin DI to inject ILedgerIntegrityService.
- Updated ComplianceB7Tests to supply LedgerIntegrityService.
- Added Microsoft.AspNetCore.App FrameworkReference to BankSwitch.Infrastructure for middleware types (RequestDelegate, HttpContext, IApplicationBuilder).
- PciDssComplianceService EvaluateControl calls already use the required result tuple and required no patch.
- Reworked TpsLoadTestHarness async worker counters to remove ref parameters from async method; counters now use a shared LoadCounters object with Interlocked/Volatile.

## Build verification
The current sandbox does not contain the .NET SDK/MSBuild/C# compiler, so a real `dotnet build` could not be executed here. Static checks were completed for the reported error signatures.
