variable "prefix"              { type = string }
variable "resource_group_name" { type = string }
variable "location"            { type = string }
variable "sql_admin_login"     { type = string }
variable "sql_admin_password"  { type = string; sensitive = true }
variable "sku_name"            { type = string }
variable "max_size_gb"         { type = number }
variable "tags"                { type = map(string) }

resource "azurerm_mssql_server" "main" {
  name                         = "${var.prefix}-sqlserver"
  resource_group_name          = var.resource_group_name
  location                     = var.location
  version                      = "12.0"
  administrator_login          = var.sql_admin_login
  administrator_login_password = var.sql_admin_password

  # PCI DSS: Minimum TLS 1.2
  minimum_tls_version          = "1.2"
  public_network_access_enabled = false   # VNet-only in production

  azuread_administrator {
    login_username = "AKS-Managed-Identity"
    object_id      = "00000000-0000-0000-0000-000000000000"  # Set to AKS identity
  }

  tags = var.tags
}

resource "azurerm_mssql_database" "main" {
  name           = "SwitchDB"
  server_id      = azurerm_mssql_server.main.id
  sku_name       = var.sku_name
  max_size_gb    = var.max_size_gb
  zone_redundant = true   # Multi-AZ for production HA

  # Transparent Data Encryption (PCI DSS Req 3.4)
  transparent_data_encryption_enabled = true

  # Short-term backup retention
  short_term_retention_policy {
    retention_days           = 35
    backup_interval_in_hours = 12
  }

  # Long-term backup (monthly backups for 1 year)
  long_term_retention_policy {
    monthly_retention = "P12M"
  }

  tags = var.tags
}

output "server_fqdn" {
  value = azurerm_mssql_server.main.fully_qualified_domain_name
}

output "connection_string" {
  sensitive = true
  value     = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Database=SwitchDB;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=5;"
}
