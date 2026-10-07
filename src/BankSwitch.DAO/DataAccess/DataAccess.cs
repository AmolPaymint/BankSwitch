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
using BankSwitch.Core.Mappings;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using Microsoft.Data.SqlClient;
using NHibernate;
using NHibernate.Tool.hbm2ddl;
using System;
using System.Configuration;
using System.Threading;

namespace BankSwitch.Core.DataAccess
{
   public class DataAccess
    {
       private static readonly object SessionFactorySync = new object();
       private static readonly AsyncLocal<ISession> ContextSession = new AsyncLocal<ISession>();
       private static ISessionFactory _sessionFactory;

        private static ISessionFactory sessionFactory
        {
            get
            {
               if(_sessionFactory == null)
               {
                   InitializeSessionFactory();
               }
               return _sessionFactory;
           }
       }

       private static void ValidateConnectionSecurity(string connectionString)
       {
           var builder = new SqlConnectionStringBuilder(connectionString);

           if (builder.ContainsKey("User ID") && string.Equals(Convert.ToString(builder["User ID"]), "sa", StringComparison.OrdinalIgnoreCase))
           {
               throw new ConfigurationErrorsException("Refusing to use SQL Server sa account. Configure a least-privilege Switch runtime user or Integrated Security.");
           }

           if (connectionString.IndexOf("Password=redmond", StringComparison.OrdinalIgnoreCase) >= 0 ||
               connectionString.IndexOf("Pwd=redmond", StringComparison.OrdinalIgnoreCase) >= 0)
           {
               throw new ConfigurationErrorsException("Refusing known sample/default database password.");
           }

           if (!builder.IntegratedSecurity && string.IsNullOrWhiteSpace(builder.UserID))
           {
               throw new ConfigurationErrorsException("Database connection must use Integrated Security or an explicitly named least-privilege service user.");
           }

           var encryptValue = builder.ContainsKey("Encrypt") ? Convert.ToString(builder["Encrypt"]) : null;
           var encryptionEnabled = string.Equals(encryptValue, "True", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(encryptValue, "Mandatory", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(encryptValue, "Strict", StringComparison.OrdinalIgnoreCase);

           if (!encryptionEnabled)
           {
               throw new ConfigurationErrorsException("SQL Server TLS is required. Add Encrypt=True, Encrypt=Mandatory, or Encrypt=Strict to the connection string.");
           }

           var trustServerCertificateValue = builder.ContainsKey("TrustServerCertificate")
               ? Convert.ToString(builder["TrustServerCertificate"])
               : "False";

           if (string.Equals(trustServerCertificateValue, "True", StringComparison.OrdinalIgnoreCase))
           {
               throw new ConfigurationErrorsException("TrustServerCertificate=False is required so SQL Server certificate validation is enforced.");
           }
       }

       private static string ResolveConnectionString()
       {
           var configured = ConfigurationManager.ConnectionStrings["SwitchDB"] != null
               ? ConfigurationManager.ConnectionStrings["SwitchDB"].ConnectionString
               : null;

           var connectionString = string.IsNullOrWhiteSpace(configured)
               ? Environment.GetEnvironmentVariable("SWITCHDB_CONNECTION_STRING")
               : configured;

           if (string.IsNullOrWhiteSpace(connectionString))
           {
               throw new ConfigurationErrorsException("SwitchDB connection string is missing. Configure connectionStrings/SwitchDB or SWITCHDB_CONNECTION_STRING.");
           }

           ValidateConnectionSecurity(connectionString);
           return connectionString;
       }

       private static void InitializeSessionFactory()
       {
           lock (SessionFactorySync)
           {
               if (_sessionFactory != null) return;

               var connectionstring = ResolveConnectionString();

               _sessionFactory = Fluently.Configure()
                  .Database(MsSqlConfiguration.MsSql2012.ConnectionString(connectionstring))
                  .Mappings(m => m.FluentMappings.AddFromAssemblyOf<RouteMap>())
                  .ExposeConfiguration(cfg => new SchemaUpdate(cfg).Execute(false, true))
                  .BuildConfiguration()
                  .BuildSessionFactory();
           }
       }

       public ISession GetSession()
       {
           var session = ContextSession.Value;
           if (session == null || !session.IsOpen)
           {
               InitializeSessionFactory();
               session = sessionFactory.OpenSession();
               session.BeginTransaction();
               ContextSession.Value = session;
           }

           return session;
       }

       public static ISession OpenSession()
       {
           return sessionFactory.OpenSession();
       }
    }
}
