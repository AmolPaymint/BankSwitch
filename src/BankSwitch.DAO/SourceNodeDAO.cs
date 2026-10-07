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
using BankSwitch.Core.DataAccess;
using BankSwitch.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NHibernate.Criterion;
using NHibernate;
using System.Collections;

namespace BankSwitch.Core.DAO
{
    public class SourceNodeDAO : DataRepository
    {
        public SourceNodeDAO()
        {

        }

        public object Save(SourceNode model)
        {
            object result = null;
            using (var session = BankSwitch.Core.DataAccess.DataAccess.OpenSession())
            {
                using (var transactn = session.BeginTransaction())
                {
                  result = session.Save(model);
                    transactn.Commit();
                }
            }
            return result;
        }
        public object Edit(SourceNode model)
        {
            object result = null;
            using (var session = BankSwitch.Core.DataAccess.DataAccess.OpenSession())
            {
                using (var transactn = session.BeginTransaction())
                {
                    session.Merge(model);
                    transactn.Commit();
                }
            }
            return result;
        }
        public IList<SourceNode> SearchSinkNode(string name, string hostName, string iPAddress, string port, int start, int limit, out int total)
        {

            List<SourceNode> result = new List<SourceNode>();
            try
            {
                ICriteria criteria = _Session.CreateCriteria(typeof(SourceNode));
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
                    result.Add((SourceNode)o);
                }

                total = Convert.ToInt32((long)((IList)allResults[1])[0]);

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Structured exception in BankSwitch.DAO/SourceNodeDAO.cs: " + ex.GetType().Name + ": " + ex.Message);

                throw;
            }
        }

        public SourceNode GetByName(string name)
        {
            try
            {
                var query = _Session.QueryOver<SourceNode>()
                       .Where(x => x.Name == name)
                       .List<SourceNode>()
                       .FirstOrDefault();
                return query;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Structured exception in BankSwitch.DAO/SourceNodeDAO.cs: " + ex.GetType().Name + ": " + ex.Message);
                
                throw;
            }
        }

        public IList<SourceNode> Retrieve()
        {
            var query = _Session.QueryOver<SourceNode>().List<SourceNode>();
            return query;
        }

        public object UpdateSourceNode(SourceNode sourceNode)
        {
            object result = false;
            using (var session = BankSwitch.Core.DataAccess.DataAccess.OpenSession())
            {
                using (var transactn = session.BeginTransaction())
                {
                    try
                    {
                        result = session.Merge(sourceNode);
                        transactn.Commit();
                    }
                    catch (Exception ex)
                    {
                        transactn.Rollback();
                        throw;
                    }
                }
            }
            return result;
        }

    }
}
