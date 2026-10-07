# BankSwitch.LEGACY — MIGRATION COMPLETE

This project is **tombstoned**. All functionality has been migrated to the clean-architecture projects.

| This class | Replaced by |
|---|---|
| `DataAccess` / `DataRepository` | `SecureSqlConnectionFactory` + Dapper in `BankSwitch.Infrastructure` |
| `TransactionLogDAO` | `SqlSwitchRepository.SaveTransactionAsync` + `ExistsDuplicateAsync` |
| `TransactionLogManager.ExistsDuplicate` | `ITransactionRepository.ExistsDuplicateAsync` + `IReplayCache` |
| `RouteManager` / `RouteDAO` | `SwitchConfigurationService.SaveRouteAsync` + `ISwitchConfigurationRepository` |
| `SourceNodeManager` / `SinkNodeManager` | `SwitchConfigurationService.SaveSourceNodeAsync/SaveSinkNodeAsync` |
| `SchemeManager` / `FeeManager` | `SwitchConfigurationService.SaveSchemeAsync/SaveFeeAsync` |
| `*Map` (FluentNHibernate) | Dapper `SqlCommand` column binding in `SqlSwitchRepository` |

## NHibernate → Dapper

The ORM was migrated from NHibernate + FluentNHibernate to raw `SqlCommand` via Dapper-style
column binding. Connection management moved from `ISession` to `SecureSqlConnectionFactory`.

## Do not add new code here. File an issue to track removal once all callers are confirmed dead.
