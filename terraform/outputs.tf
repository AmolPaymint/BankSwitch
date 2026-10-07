output "resource_group_name" {
  description = "Name of the created resource group."
  value       = azurerm_resource_group.main.name
}

output "aks_cluster_name" {
  description = "AKS cluster name."
  value       = module.aks.cluster_name
}

output "aks_cluster_fqdn" {
  description = "AKS cluster fully-qualified domain name."
  value       = module.aks.cluster_fqdn
}

output "kube_config" {
  description = "Raw kubeconfig for kubectl access. Store securely — do not log."
  value       = module.aks.kube_config_raw
  sensitive   = true
}

output "sql_server_fqdn" {
  description = "Azure SQL Server fully-qualified domain name."
  value       = module.sql.server_fqdn
}

output "redis_hostname" {
  description = "Azure Cache for Redis hostname."
  value       = module.redis.hostname
}

output "key_vault_uri" {
  description = "Azure Key Vault URI for use in application configuration."
  value       = azurerm_key_vault.main.vault_uri
}

output "log_analytics_workspace_id" {
  description = "Log Analytics workspace ID for diagnostics configuration."
  value       = azurerm_log_analytics_workspace.main.workspace_id
}
