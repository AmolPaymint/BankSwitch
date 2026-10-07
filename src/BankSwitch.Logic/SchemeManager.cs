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
﻿using BankSwitch.Core.Entities;
using BankSwitch.DAO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankSwitch.Logic
{
   public class SchemeManager
    {
       private SchemeDAO _db;
       public SchemeManager()
       {
           _db = new SchemeDAO();
       }
       public object CreateScheme(Scheme model)
       {
           object result = null;
           try
           {
               var scheme = _db.Get<Scheme>().FirstOrDefault(x => x.Name == model.Name);
               if (scheme == null)
               {
                  result =  _db.Create(model);
               }
               else
               {
                   throw new Exception("This Scheme Already Exist");
               }
               if(result!=null)
               {
                   return true;
               }
           }
           catch (Exception ex)
           {
               System.Diagnostics.Trace.TraceError("Structured exception in BankSwitch.Logic/SchemeManager.cs: " + ex.GetType().Name + ": " + ex.Message);
               _db.Rollback();
               throw;
           }
           return result;
       }

       public IList<Scheme> RetrieveAll()
       {
           return _db.GetAll<Scheme>().ToList();
       }

       public IList<Scheme> Search(string queryString, int pageIndex, int PageSize, out int totalCount)
       {
           return _db.Search(queryString, pageIndex, PageSize, out totalCount);
       }
       public object UpdateScheme(Scheme model)
       {
           object result = null;
           var scheme = _db.Get<Scheme>().FirstOrDefault(x => x.Id==model.Id);
           if (scheme != null)
           {
               scheme.Name = model.Name;
               scheme.Route = model.Route;
               foreach(var item in model.TransactionTypeChannelFees)
               {
                   item.Scheme = scheme;
               }
             result = _db.UpdateScheme(model);
           }
           return result;
       }
    }
}
