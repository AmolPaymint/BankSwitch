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
  public  class SourceNodeMap:ClassMap<SourceNode>
    {
      public SourceNodeMap()
      {
          Id(x => x.Id);
          Map(x => x.Name);
          Map(x => x.HostName);
          Map(x => x.IPAddress);
          Map(x => x.IsActive);
          Map(x => x.NodeCode);
          Map(x => x.AllowedIpRanges);
          Map(x => x.CertificateFingerprint);
          Map(x => x.PermittedMtis);
          Map(x => x.PermittedChannels);
          Map(x => x.DailyTransactionLimit);
          Map(x => x.TpsLimit);
          Map(x => x.AllowedBinRanges);
          Map(x => x.KeyProfile);
          Map(x => x.SettlementProfile);
          Map(x => x.Port);
          HasMany(x => x.Schemes).Not.LazyLoad();
      }
    }
}
