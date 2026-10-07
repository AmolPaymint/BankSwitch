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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankSwitch.DAO
{
   public class FeeDAO:DataRepository
    {
       public FeeDAO()
       {

       }

       public List<Fee> SearchFee(string name)
       {
        List<Fee> result = new List<Fee>();
         try 
	     {	        
		 ICriteria criteria  = _Session.CreateCriteria(typeof(Fee));
                  if(!string.IsNullOrEmpty(name))
                  {
                      criteria.Add(Expression.Like("Name", name.Trim(), MatchMode.Anywhere));
                  }
            result = criteria.List<Fee>().ToList();  
	      }
	    catch (Exception ex)
	     {
		
		  throw;
	     }
           return result;
       }

       public IList<Fee> GetAllFees()
       {

           var fees = _Session.QueryOver<Fee>().List<Fee>();
           return fees;
       }
       public IList<Fee> Search(string queryparam, int pageIndex, int pageSize, out int totalCount)
       {
           var channels = _Session.QueryOver<Fee>();
           if (!string.IsNullOrEmpty(queryparam))
           {
               channels.Where(x => x.Name.IsInsensitiveLike(queryparam, MatchMode.Anywhere));
           }
           var result = channels.Skip(pageIndex).Take(pageSize);
           totalCount = result.RowCount();
           return result.List<Fee>();
       }
    }
}
