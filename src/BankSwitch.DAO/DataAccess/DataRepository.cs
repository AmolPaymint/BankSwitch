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
using BankSwitch.Core.Interfaces;
using NHibernate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NHibernate.Criterion;
using NHibernate.Linq;

namespace BankSwitch.Core.DataAccess
{
   public class DataRepository:IDataRepository, IDisposable
    {
        private ISession _session;
        protected ISession _Session
        {
            get
            {
                if (_session == null)
                {
                    _session = DataAccess.OpenSession();
                    return _session;
                }
                else
                {
                    return _session;
                }
            }
            set
            {
                _session = value;
            }
        }
        protected ITransaction _transaction = null;
        public DataRepository()
        {
            _Session = DataAccess.OpenSession();

        }

        public DataRepository(ISession sesson)
        {
            _Session = sesson;
        }

        public void Commit()
        {
            if (_Session.Transaction.IsActive)
            {
                _Session.Transaction.Commit();
            }
        }
        public void Rollback()
        {
            if (_Session.Transaction.IsActive)
            {
                _Session.Transaction.Rollback();
                _Session.Clear();
            }
        }
        public void BeginTransaction()
        {
            Rollback();
            _Session.BeginTransaction();
        }
        public void CloseTransaction()
        {
            if (_transaction != null)
            {
                _transaction.Dispose();
                _transaction = null;
            }
        }

        private void CloseSession()
        {
            _Session.Close();
            _Session.Dispose();
            _Session = null;
        }
        public void Dispose()
        {
            if (_transaction != null)
            {
            
                Commit();
            }

            if (_Session != null)
            {
                if (_Session.IsOpen)
                {
                    _Session.Flush(); // commit session transactions
                }
                CloseSession();
            }
        }

        public IQueryable<T> Get<T>() where T : class
        {
            return _Session.Query<T>();
        }
        public T GetById<T>(int id) where T : class
        {
            return _Session.Get<T>(id);

        }

        public IList<T> GetAll<T>() where T : class
        {
            return _Session.QueryOver<T>().List();

        }

        public bool Add<T>(T entity) where T : class
        {
            _Session.Save(entity);
            return true;
        }
        public T FindBy<T>(T id) where T : class
        {
            _Session.CacheMode = CacheMode.Normal;
            return _Session.Get<T>(id);
        }

        public bool Add<T>(IEnumerable<T> items) where T : class
        {
            foreach (T item in items)
            {
                _Session.Save(item);
            }
            return true;
        }

        public bool Update<T>(T entity) where T : class
        {
            _Session.Update(entity);
            _Session.Flush();
            return true;
        }
    }
}
