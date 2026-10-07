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

namespace BankSwitch.DAO
{
    public class ChannelDAO : DataRepository
    {
        public ChannelDAO()
        {

        }
        public IList<Channel> SearchByName(string querystring)
        {
            if (string.IsNullOrEmpty(querystring))
            {
                return new List<Channel>();
            }
            else
            {

                return _Session.QueryOver<Channel>().Where(x => x.Code.IsInsensitiveLike(querystring, MatchMode.Anywhere)
                    || x.Name.IsInsensitiveLike(querystring, MatchMode.Anywhere)).List<Channel>();
            }
        }
        public IList<Channel> Search(string queryparam, int pageIndex, int pageSize, out int totalCount)
        {
            var channels = _Session.QueryOver<Channel>();
            if (!string.IsNullOrEmpty(queryparam))
            {
                channels.Where(x => x.Name.IsInsensitiveLike(queryparam, MatchMode.Anywhere)
                     || x.Code.IsInsensitiveLike(queryparam, MatchMode.Anywhere)).List<Channel>();
            }
            var result = channels.Skip(pageIndex).Take(pageSize);
            totalCount = result.RowCount();
            return result.List<Channel>();
        }

        public IList<Channel> GetAllChannel()
        {
            var channels = _Session.QueryOver<Channel>().List<Channel>();
            return channels;
        }

        public IList<Channel> GetAllChannel(string name, string code, int start, int limit, out int total)
        {
            List<Channel> result = new List<Channel>();
            try
            {
                ICriteria criteria = _Session.CreateCriteria(typeof(Channel));
                if (!string.IsNullOrEmpty(name))
                {
                    criteria.Add(Expression.Like("Name", name));
                }
                if (!string.IsNullOrEmpty(code))
                {
                    criteria.Add(Expression.Like("Code", code));
                }
                ICriteria countCriteria = CriteriaTransformer.Clone(criteria).SetProjection(Projections.RowCountInt64());
                ICriteria listCriteria = CriteriaTransformer.Clone(criteria).SetFirstResult(start).SetMaxResults(limit);
                listCriteria.AddOrder(Order.Desc("Id"));

                IList allResults = _Session.CreateMultiCriteria().Add(listCriteria).Add(countCriteria).List();

                foreach (var o in (IList)allResults[0])
                {
                    result.Add((Channel)o);
                }

                total = Convert.ToInt32((long)((IList)allResults[1])[0]);

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Structured exception in BankSwitch.DAO/ChannelDAO.cs: " + ex.GetType().Name + ": " + ex.Message);

                throw;
            }
        }
    }
}
