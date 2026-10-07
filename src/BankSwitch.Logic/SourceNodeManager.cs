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
﻿using BankSwitch.Core.DAO;
using BankSwitch.Core.DataAccess;
using BankSwitch.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankSwitch.Logic
{
    public class SourceNodeManager
    {
        public SourceNodeDAO _db;
        public SourceNodeManager()
        {
            _db = new SourceNodeDAO();
        }
        public object CreateSourceNode(SourceNode model)
        {
            object result = null;
            try
            {

                result = _db.Save(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Structured exception in BankSwitch.Logic/SourceNodeManager.cs: " + ex.GetType().Name + ": " + ex.Message);
                throw;
            }
            return result;
        }
        public bool EditSourceNode(SourceNode model)
        {
            bool result = false;
            try
            {
                object obj = _db.Edit(model);
                if (obj != null)
                {
                    result = true;
                }

            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Structured exception in BankSwitch.Logic/SourceNodeManager.cs: " + ex.GetType().Name + ": " + ex.Message);
                throw;
            }
            return result;
        }

        public IList<SourceNode> RetrieveAll()
        {
            return _db.Retrieve();
        }
        public IList<SourceNode> GetAllSourceNode(string name, string hostName, string iPAddress, string port, int start, int limit, out int total)
        {
            return _db.SearchSinkNode(name, hostName, iPAddress, port, start, limit, out total);
        }

        public SourceNode GetByID(int sourceID)
        {
            var sourceNode = _db.GetById<SourceNode>(sourceID);
            return sourceNode;
        }

        public void Update(SourceNode model)
        {
            using (var session = DataAccess.OpenSession())
            {
                using (var transactn = session.BeginTransaction())
                {
                     session.Merge(model);
                    transactn.Commit();
                }
            }
        }
    }
}
