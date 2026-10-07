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
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankSwitch.Core.Interfaces
{
   public interface IDataRepository
    {
        IList<T> GetAll<T>() where T : class;
        T GetById<T>(int id) where T : class;
        IQueryable<T> Get<T>() where T : class;
        bool Add<T>(T entity) where T : class;
        bool Add<T>(IEnumerable<T> items) where T : class;
        bool Update<T>(T entity) where T : class;
        T FindBy<T>(T id) where T : class;
        void Commit();
        void BeginTransaction();
        void Rollback(); 
    }
}
