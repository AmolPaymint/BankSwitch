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
using NHibernate;
using NHibernate.Criterion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankSwitch.DAO
{
   public class SchemeDAO:DataRepository
    {
       public SchemeDAO()
       {

       }

       public object Create(Scheme model)
       {
           object result = null;
           try
           {
               using (ISession session = _Session.SessionFactory.OpenSession())
               {
                   using (_Session.BeginTransaction())
                   {
                       Scheme scheme = new Scheme
                       {
                           Name = model.Name,
                           Route = model.Route,
                           Description = model.Description,
                       };
                       //session.SaveOrUpdate(scheme);

                       IList<TransactionTypeChannelFee> tcf = model.TransactionTypeChannelFees;
                       scheme.TransactionTypeChannelFees = tcf;

                       foreach (var item in tcf)
                       {
                           item.Scheme = scheme;
                           //session.Save(item);
                       }
                    result = session.Save(scheme);
                       
                   }
               }
               return result;
           }
           catch (Exception ex)
           {
               System.Diagnostics.Trace.TraceError("Structured exception in BankSwitch.DAO/SchemeDAO.cs: " + ex.GetType().Name + ": " + ex.Message);
               _Session.Transaction.Rollback();
               throw;
           }
       }

       public IList<Scheme> Search(string queryString, int pageIndex, int pageSize, out int totalCount)
       {
           var query = _Session.QueryOver<Scheme>();

           if (string.IsNullOrEmpty(queryString))
           {
               totalCount = query.RowCount();
               var schemes = query.List<Scheme>();
               return schemes;
           }
           else
           {
               query.Where(x => x.Name.IsLike(queryString, MatchMode.Anywhere)).List<Scheme>();
           }

           var result = query.Skip(pageIndex).Take(pageSize);
           totalCount = result.RowCount();
           return result.List<Scheme>();
       }

       public object UpdateScheme(Scheme model)
       {
           object result = null;
           using (var session = DataAccess.OpenSession())
           {
               using (var trnx = session.BeginTransaction())
               {
                   try
                   {
                result = session.Merge(model);
                   trnx.Commit();
                   }
                   catch (Exception ex)
                   {
                       System.Diagnostics.Trace.TraceError("Structured exception in BankSwitch.DAO/SchemeDAO.cs: " + ex.GetType().Name + ": " + ex.Message);
                       System.Diagnostics.Trace.TraceError("Structured exception in BankSwitch.DAO/SchemeDAO.cs: " + ex.GetType().Name + ": " + ex.Message);
                       trnx.Rollback();
                       throw;
                   }
                   return result;
               }
           }
       }
    }
}
