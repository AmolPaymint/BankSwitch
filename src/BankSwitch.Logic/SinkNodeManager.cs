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
using BankSwitch.Core.DAO;
using BankSwitch.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankSwitch.Logic
{
   public class SinkNodeManager
    {
        private SinkNodeDAO _db;
        public SinkNodeManager(SinkNodeDAO db)
        {
            _db = db;
        }
        public SinkNodeManager()
        {
            _db = new SinkNodeDAO();
        }
        public bool AddSinkNode(SinkNode sinkNode)
        {
            bool result = false;
            try
            {
                var node = _db.GetAll<SinkNode>().FirstOrDefault(x => x.IPAddress == sinkNode.IPAddress && x.Port==sinkNode.Port);
                if (node != null)
                {
                    throw new Exception("This Sink Node With this IP Address and Port already Exist");
                }
                else
                {
                    result = _db.Add(sinkNode);
                }
                return result;
            }
            catch (Exception ex)
            {
                _db.Rollback();
                throw;
            }
        }
        public bool Edit(SinkNode model)
        {
            try
            {
                bool result = false;
                var sinkNode = _db.GetAll<SinkNode>().FirstOrDefault(x => x.IPAddress == model.IPAddress);
                if (sinkNode != null)
                {
                    sinkNode.Name = model.Name;
                    sinkNode.HostName = model.HostName;
                    sinkNode.IPAddress = model.IPAddress;
                    sinkNode.Port = model.Port;
                     sinkNode.IsActive = model.IsActive;
                    result = _db.Update(sinkNode);
                    _db.Commit();
                }
                return result;
            }
            catch (Exception ex)
            {
                _db.Rollback();
                throw;
            }
        }

        public IList<SinkNode> GetSinkNodes(string name, string hostName, string iPAddress, string port, int start, int limit, out int total)
       {
           return _db.GetSinkNodes(name, hostName, iPAddress, port, start, limit , out total);
       }
       public IList<SinkNode> GetAllSinkNode()
       {
           var query = _db.GetAllSinkNodes();
           return query;
       }

       public SinkNode GetById(int ID)
       {
           var sinkNode = _db.GetById<SinkNode>(ID);
           return sinkNode;
       }

       public void Update(SinkNode sinkNode)
       {
           if(sinkNode!=null)
           {
               _db.Update(sinkNode);
           }
       }
       public SinkNode GetByName(string name)
       {
           var query = _db.GetAll<SinkNode>().Where(x => x.Name == name).FirstOrDefault();
           return query;
       }
    }
}
