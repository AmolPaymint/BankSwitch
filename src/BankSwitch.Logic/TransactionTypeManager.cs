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
using BankSwitch.DAO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankSwitch.Logic
{
   public class TransactionTypeManager
    {
       private TransactionTypeDAO _db;
       public TransactionTypeManager(TransactionTypeDAO db)
       {
           _db = db ?? throw new ArgumentNullException("db");
       }
       public TransactionTypeManager()
       {
           _db = new TransactionTypeDAO();
       }
       public bool AddTransactionType(TransactionType model)
       {
           var check = _db.Get<TransactionType>().FirstOrDefault(x => x.Name == model.Name && x.Code == model.Code);
           if (check != null)
           {
               throw new Exception("This TransactionType already Exist");
           }
           else
           {
               return _db.Add(new TransactionType
               {
                   Code = model.Code,
                   Name = model.Name,
                   Description = model.Description
               });
           }
       }
       public bool Edit(TransactionType model)
       {
           bool result = false;
           try
           {
               var transx = _db.Get<TransactionType>().FirstOrDefault(x =>x.Id==model.Id);
               if (transx != null)
               {
                   transx.Code = model.Code;
                   transx.Description = model.Description;
                   transx.Name = model.Name;
                  result =_db.Update(transx);
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

       public IList<TransactionType> GetAllTransactionType()
       {
          return  _db.GetAll<TransactionType>().ToList<TransactionType>();     
       }
       public TransactionType GetByName(string name)
       {
         var trnx = new TransactionType();
         if(string.IsNullOrEmpty(name))
         {
             return trnx;
         }
         else
         {
           trnx = _db.Get<TransactionType>().FirstOrDefault(x=>x.Name==name);
           return trnx;
         }
       }

       public IList<TransactionType> Search(string name,string code, int start, int limit, out int total)
       {
           return _db.Search(name, code, start, limit, out total);
       }

       public TransactionType GetByCode(string transactionTypeCode)
       {
           var trxType = _db.Get<TransactionType>().Where(x => x.Code == transactionTypeCode).FirstOrDefault();
           return trxType;
       }
    }
}
