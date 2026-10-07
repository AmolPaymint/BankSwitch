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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankSwitch.Core.DAO
{
   public class SinkNodeDAO:DataRepository
    {
       public SinkNodeDAO()
       {

       }

       public IList<SinkNode> GetSinkNodes(string name, string hostName, string iPAddress, string port, int start, int limit, out int total)
       {
           List<SinkNode> result = new List<SinkNode>();
           try
           {
               ICriteria criteria = _Session.CreateCriteria(typeof(SinkNode));
               if (!string.IsNullOrEmpty(name))
               {
                   criteria.Add(Expression.Like("Name", name));
               }
               if (!string.IsNullOrEmpty(hostName))
               {
                   criteria.Add(Expression.Like("HostName", hostName));
               }
               if (!string.IsNullOrEmpty(iPAddress))
               {
                   criteria.Add(Expression.Like("IPAddress", iPAddress));
               }
               if (!string.IsNullOrEmpty(port))
               {
                   criteria.Add(Expression.Like("Port", port));
               }
               ICriteria countCriteria = CriteriaTransformer.Clone(criteria).SetProjection(Projections.RowCountInt64());
               ICriteria listCriteria = CriteriaTransformer.Clone(criteria).SetFirstResult(start).SetMaxResults(limit);
               listCriteria.AddOrder(Order.Desc("Id"));

               IList allResults = _Session.CreateMultiCriteria().Add(listCriteria).Add(countCriteria).List();

               foreach (var o in (IList)allResults[0])
               {
                   result.Add((SinkNode)o);
               }

               total = Convert.ToInt32((long)((IList)allResults[1])[0]);

               return result;
           }
           catch (Exception ex)
           {
               System.Diagnostics.Trace.TraceError("Structured exception in BankSwitch.DAO/SinkNodeDAO.cs: " + ex.GetType().Name + ": " + ex.Message);

               throw;
           }
       }

       public IList<SinkNode> GetAllSinkNodes()
       {
           try
           {
               var query = _Session.QueryOver<SinkNode>().List<SinkNode>();
               return query;
           }
           catch (Exception ex)
           {
               System.Diagnostics.Trace.TraceError("Structured exception in BankSwitch.DAO/SinkNodeDAO.cs: " + ex.GetType().Name + ": " + ex.Message);
               throw;
           }
       }
    }
}
