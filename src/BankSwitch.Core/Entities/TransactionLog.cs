// =============================================================
// LEGACY CODE — SUPERSEDED BY CLEAN ARCHITECTURE
// =============================================================
// This file is part of the original BankSwitch monolith that
// pre-dates the clean-architecture rewrite (BankSwitch v20+).
//
// STATUS: TOMBSTONED — DO NOT MODIFY OR ADD NEW CALLERS.
//
// All business logic in this file has been migrated to:
//   BankSwitch.Domain   — domain entities & value objects
//   BankSwitch.Application — use-cases & service interfaces
//   BankSwitch.Infrastructure — SQL / HSM / external adapters
//
// This file is retained only so the legacy project still
// compiles during the transition period. It will be removed
// in a future release once all consumers have been confirmed
// dead (grep for the class name before deleting).
//
// See ARCHITECTURE_MIGRATION.md at the repository root for
// the full migration guide.
// =============================================================
#pragma warning disable CS0618 // suppress Obsolete warnings inside legacy code itself
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankSwitch.Core.Entities
{
   public class TransactionLog
    {
        public virtual int Id { get; set; }
        public virtual string MTI { get; set; }
        public virtual string CorrelationId { get; set; }
        public virtual string CurrencyCode { get; set; }
        public virtual long LatencyMilliseconds { get; set; }
        public virtual string MacValidationStatus { get; set; }
        public virtual string ReversalStatus { get; set; }
        public virtual string CardPAN { get; set; }
        public virtual string PanToken { get; set; }
        public virtual string PanHash { get; set; }
        public virtual string STAN { get; set; }
        public virtual string RetrievalReferenceNumber { get; set; }
        public virtual DateTime? TransactionDate { get; set; }
        public virtual string Account1 { get; set; }
        public virtual string Account2 { get; set; }
        public virtual string Account1Token { get; set; }
        public virtual string Account2Token { get; set; }
        public virtual string Account1Hash { get; set; }
        public virtual string Account2Hash { get; set; }
        public virtual bool TrackDataPresent { get; set; }
        public virtual bool PinBlockPresent { get; set; }
        public virtual bool EmvDataPresent { get; set; }
        public virtual string TransactionType { get; set; }
        public virtual string Channel { get; set; }
        public virtual string SourceNode { get; set; }
        public virtual string SinkNode { get; set; }
        public virtual string Route { get; set; }
        public virtual string Scheme { get; set; }
        public virtual double Amount { get; set; }
        public virtual string Fee { get; set; }
        public virtual string ResponseCode { get; set; }
        public virtual string ResponseDescription { get; set; }
        public virtual decimal Charge { get; set; }
        public virtual bool IsReversePending { get; set; }
        public virtual bool IsReversed { get; set; }
        public virtual int? OriginalTransactionId { get; set; }
        public virtual string ReversalTransactionId { get; set; }
        public virtual int ReversalRetryCount { get; set; }
        public virtual DateTime? NextReversalAttemptAt { get; set; }
        public virtual DateTime? LastReversalAttemptAt { get; set; }
        public virtual string LastReversalError { get; set; }
        public virtual DateTime? ReversalAcceptedAt { get; set; }
        public virtual DateTime? ReversalManuallyResolvedAt { get; set; }
        public virtual string ReversalManualResolutionReason { get; set; }
        public virtual string OriginalDataElement { get; set; }
        public virtual DateTime? DateCreated { get; set; }
        public virtual DateTime? DateModified { get; set; }
    }
}
