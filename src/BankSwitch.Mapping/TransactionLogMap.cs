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
using BankSwitch.Core.Entities;
using FluentNHibernate.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankSwitch.Core.Mappings
{
   public class TransactionLogMap:ClassMap<TransactionLog>
    {
       public TransactionLogMap()
       {
           Id(x => x.Id);
           Map(x => x.CardPAN);
           Map(x => x.PanToken);
           Map(x => x.PanHash);
           Map(x => x.Amount);
           Map(x => x.Account1);
           Map(x => x.Account2);
           Map(x => x.Account1Token);
           Map(x => x.Account2Token);
           Map(x => x.Account1Hash);
           Map(x => x.Account2Hash);
           Map(x => x.TrackDataPresent);
           Map(x => x.PinBlockPresent);
           Map(x => x.EmvDataPresent);
           Map(x => x.MTI);
           Map(x => x.CorrelationId);
           Map(x => x.CurrencyCode);
           Map(x => x.LatencyMilliseconds);
           Map(x => x.MacValidationStatus);
           Map(x => x.ReversalStatus);
           Map(x => x.ResponseCode);
           Map(x => x.ResponseDescription);
           Map(x => x.Route);
           Map(x => x.Scheme);
           Map(x => x.SinkNode);
           Map(X => X.SourceNode);
           Map(x => x.TransactionType);
           Map(x => x.Fee);
           Map(x => x.Channel);
           Map(x => x.STAN);
           Map(x => x.RetrievalReferenceNumber);
           Map(x => x.TransactionDate);
           Map(x => x.DateCreated);
           Map(x => x.DateModified);
           Map(x => x.OriginalDataElement);
           Map(x => x.IsReversePending);
           Map(x => x.IsReversed);
           Map(x => x.OriginalTransactionId);
           Map(x => x.ReversalTransactionId);
           Map(x => x.ReversalRetryCount);
           Map(x => x.NextReversalAttemptAt);
           Map(x => x.LastReversalAttemptAt);
           Map(x => x.LastReversalError);
           Map(x => x.ReversalAcceptedAt);
           Map(x => x.ReversalManuallyResolvedAt);
           Map(x => x.ReversalManualResolutionReason);
           Map(x => x.Charge);
       }
    }
}
