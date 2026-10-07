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
   public class SourceNode
    {
       public virtual int Id { get; set; }
       public virtual string Name { get; set; }
       public virtual string HostName { get; set;}
       public virtual string IPAddress { get; set;}
       public virtual string Port { get; set; }
       public virtual bool IsActive { get; set;}
       public virtual string NodeCode { get; set; }
       public virtual string AllowedIpRanges { get; set; }
       public virtual string CertificateFingerprint { get; set; }
       public virtual string PermittedMtis { get; set; }
       public virtual string PermittedChannels { get; set; }
       public virtual int DailyTransactionLimit { get; set; }
       public virtual int TpsLimit { get; set; }
       public virtual string AllowedBinRanges { get; set; }
       public virtual string KeyProfile { get; set; }
       public virtual string SettlementProfile { get; set; }
       public virtual IList<Scheme> Schemes { get; set;}

       public SourceNode()
       {
           Schemes = new List<Scheme>();
       }
    }

}
