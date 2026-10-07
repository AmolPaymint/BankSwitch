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
using BankSwitch.Core.Entities;
using BankSwitch.DAO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankSwitch.Logic
{
   public class TransactionLogManager
    {
       private TransactionLogDAO _db;
           public TransactionLogManager()
           {
               _db = new TransactionLogDAO();
           }

           public bool AddTransactionLog(TransactionLog model)
           {
               bool result = false;
               object obj = _db.AddTransactionLog(model);
               if (obj != null)
               {
                   result = true;
               }
               return result;
           }

           public bool ExistsDuplicate(string sourceNode, string stan, string retrievalReferenceNumber, double amount, DateTime transactionDateUtc)
           {
               return _db.ExistsDuplicate(sourceNode, stan, retrievalReferenceNumber, amount, transactionDateUtc);
           }

           public TransactionLog GetByOriginalDataElement(string originalDataElement, out string originalDataElement2)
           {
               originalDataElement = originalDataElement.Remove(0,0);
               originalDataElement2 = originalDataElement;
              return new TransactionLogDAO().GetByOriginalDataElement(originalDataElement);
           }

           public IList<TransactionLog> GetAllThatNeedsReversal()
           {
               var query = _db.GetAll<TransactionLog>().Where(x => x.IsReversePending).ToList();
               return query;
           }

           public IList<TransactionLog> GetReversalsDue(DateTime nowUtc, int maxAttempts)
           {
               return _db.GetReversalsDue(nowUtc, maxAttempts);
           }

           public bool HasAcceptedReversalForOriginal(int originalTransactionId)
           {
               return _db.HasAcceptedReversalForOriginal(originalTransactionId);
           }

           public TransactionLog GetByReversalTransactionId(string reversalTransactionId)
           {
               return _db.GetByReversalTransactionId(reversalTransactionId);
           }

           public void Update(TransactionLog thisLog)
           {
               if (thisLog != null)
               {
                   _db.Update(thisLog);
               }
           }
           public IList<TransactionLog> GetAllTransactionLog(string cardPAN, string mti, string responseCode, DateTime? transactionDatefrom, DateTime? transactionDateTo, int start, int limit, out int total)
           {
               return _db.GetAllTransactionLog(cardPAN, mti, responseCode, transactionDatefrom, transactionDateTo, start, limit, out total);
           }
    }
}
