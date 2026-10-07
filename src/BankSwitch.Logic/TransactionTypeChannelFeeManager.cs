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
﻿using BankSwitch.Core.DataAccess;
using BankSwitch.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankSwitch.Logic
{
    public class TransactionTypeChannelFeeManager
    {
        private DataRepository _db;

        public TransactionTypeChannelFeeManager()
        {
            _db = new DataRepository();
        }

        public bool CreateTransactionTypeChannelFee( TransactionTypeChannelFee model)
        {
            bool result = false;
            try
            {
                if (model!=null)
                {
                  
                    result =  _db.Add(model);
                    _db.Commit();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Structured exception in BankSwitch.Logic/TransactionTypeChannelFeeManager.cs: " + ex.GetType().Name + ": " + ex.Message);
                _db.Rollback();
                throw;
            }
            return result;
        }
        public bool Edit(TransactionTypeChannelFee model)
        {

            return _db.Update(model);
               
        }
        public IList<TransactionTypeChannelFee> RetrieveAll()
        {
            return _db.GetAll<TransactionTypeChannelFee>().ToList<TransactionTypeChannelFee>();
        }
        public IList<TransactionTypeChannelFee> Search(string name, int pageIndex, int pageSize, out int totalCount)
        {
            var query = _db.GetAll<TransactionTypeChannelFee>().ToList();
            var result = query.Skip(pageIndex).Take(pageSize);
           totalCount = result.Count();
           return result.ToList<TransactionTypeChannelFee>();
        }
    }
}
