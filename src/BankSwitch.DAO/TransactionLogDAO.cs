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
   public class TransactionLogDAO:DataRepository
    {
       public TransactionLogDAO()
       {

       }

       public object AddTransactionLog(TransactionLog model)
       {
           object result = null;
           using (var session = DataAccess.OpenSession())
           {
               using (var transactn = session.BeginTransaction())
               {
                   try
                   {
                       result = session.Save(model);
                       transactn.Commit();
                   }
                   catch (Exception ex)
                   {
                       System.Diagnostics.Trace.TraceError("Structured exception in BankSwitch.DAO/TransactionLogDAO.cs: " + ex.GetType().Name + ": " + ex.Message);
                       transactn.Rollback();
                       throw;
                   }
               }
           }
           return result;
       }

       public bool ExistsDuplicate(string sourceNode, string stan, string retrievalReferenceNumber, double amount, DateTime transactionDateUtc)
       {
           if (string.IsNullOrWhiteSpace(sourceNode) || string.IsNullOrWhiteSpace(stan))
           {
               return false;
           }

           DateTime startOfDay = transactionDateUtc.Date;
           DateTime endOfDay = startOfDay.AddDays(1).AddTicks(-1);

           using (var session = DataAccess.OpenSession())
           {
               var query = session.QueryOver<TransactionLog>()
                   .Where(x => x.SourceNode == sourceNode)
                   .And(x => x.STAN == stan)
                   .And(x => x.Amount == amount)
                   .And(x => x.TransactionDate >= startOfDay)
                   .And(x => x.TransactionDate <= endOfDay);

               if (!string.IsNullOrWhiteSpace(retrievalReferenceNumber))
               {
                   query = query.And(x => x.RetrievalReferenceNumber == retrievalReferenceNumber);
               }

               return query.RowCount() > 0;
           }
       }


       public IList<TransactionLog> GetReversalsDue(DateTime nowUtc, int maxAttempts)
       {
           using (var session = DataAccess.OpenSession())
           {
               return session.QueryOver<TransactionLog>()
                   .Where(x => x.IsReversePending == true)
                   .And(x => x.IsReversed == false)
                   .And(x => x.ReversalRetryCount < maxAttempts)
                   .List()
                   .Where(x => !x.NextReversalAttemptAt.HasValue || x.NextReversalAttemptAt.Value <= nowUtc)
                   .ToList();
           }
       }

       public bool HasAcceptedReversalForOriginal(int originalTransactionId)
       {
           using (var session = DataAccess.OpenSession())
           {
               return session.QueryOver<TransactionLog>()
                   .Where(x => x.OriginalTransactionId == originalTransactionId)
                   .And(x => x.ReversalStatus == "Accepted")
                   .RowCount() > 0;
           }
       }

       public TransactionLog GetByReversalTransactionId(string reversalTransactionId)
       {
           if (string.IsNullOrWhiteSpace(reversalTransactionId)) return null;
           using (var session = DataAccess.OpenSession())
           {
               return session.QueryOver<TransactionLog>()
                   .Where(x => x.ReversalTransactionId == reversalTransactionId)
                   .List<TransactionLog>()
                   .FirstOrDefault();
           }
       }

       public TransactionLog GetByOriginalDataElement(string originalDataElement)
       {
           TransactionLog trxLog = null;
           if (!string.IsNullOrEmpty(originalDataElement))
           {

                trxLog = _Session.QueryOver<TransactionLog>().Where(x => x.OriginalDataElement == originalDataElement).List<TransactionLog>().FirstOrDefault();
           }
           else
           {
               trxLog = null;
           }
           return trxLog;
       }
       public IList<TransactionLog> GetAllTransactionLog(string cardPAN, string mti, string responseCode, DateTime? transactionDateFrom, DateTime? transactionDateTo, int start, int limit, out int total)
       {
           List<TransactionLog> result = new List<TransactionLog>();
          try
          {
           ICriteria criteria = _Session.CreateCriteria(typeof(TransactionLog));
               if(!string.IsNullOrEmpty(cardPAN))
               {
                   criteria.Add(Expression.Like("CardPAN", cardPAN));
               }
               if (!string.IsNullOrEmpty(mti))
               {
                   criteria.Add(Expression.Like("MTI", mti));
               }
               if (!string.IsNullOrEmpty(responseCode))
              {
                  criteria.Add(Expression.Like("ResponseCode", responseCode));
              }
               if (transactionDateFrom.HasValue && !transactionDateFrom.Value.Equals(DateTime.MinValue))
               {
                   criteria.Add(Expression.Ge("TransactionDate", transactionDateFrom.Value));
               }
              if(transactionDateTo.HasValue && !transactionDateTo.Value.Equals(DateTime.MinValue))
              {
               criteria.Add(Expression.Le("TransactionDate", transactionDateTo.Value.AddDays(1).AddSeconds(-1)));
             }
             ICriteria countCriteria = CriteriaTransformer.Clone(criteria).SetProjection(Projections.RowCountInt64());
                ICriteria listCriteria = CriteriaTransformer.Clone(criteria).SetFirstResult(start).SetMaxResults(limit);
                listCriteria.AddOrder(Order.Desc("Id"));

              IList allResults = _Session.CreateMultiCriteria().Add(listCriteria).Add(countCriteria).List();

              foreach (var o in (IList)allResults[0])
              {
                  result.Add((TransactionLog)o);
              }

              total = Convert.ToInt32((long)((IList)allResults[1])[0]);

            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Structured exception in BankSwitch.DAO/TransactionLogDAO.cs: " + ex.GetType().Name + ": " + ex.Message);

                throw;
            }
            return result;
       }
    }
}
