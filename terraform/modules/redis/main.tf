variable "prefix"              { type = string }
variable "resource_group_name" { type = string }
variable "location"            { type = string }
variable "sku_name"            { type = string }
variable "family"              { type = string }
variable "capacity"            { type = number }
variable "tags"                { type = map(string) }

resource "azurerm_redis_cache" "main" {
  name                = "${var.prefix}-redis"
  location            = var.location
  resource_group_name = var.resource_group_name
  capacity            = var.capacity
  family              = var.family
  sku_name            = var.sku_name

  # TLS only — PCI DSS Req 4.2
  enable_non_ssl_port = false
  minimum_tls_version = "1.2"

  redis_configuration {
    # Enable persistence on Premium tier for durability
    rdb_backup_enabled = var.sku_name == "Premium" ? true : false
    rdb_backup_frequency         = var.sku_name == "Premium" ? 60 : null
    rdb_backup_max_snapshot_count = var.sku_name == "Premium" ? 1 : null

    # Max memory policy: LRU eviction for cache (not queue)
    maxmemory_policy  = "allkeys-lru"
    maxmemory_reserved = 50   # MB reserved for non-cache operations
  }

  tags = var.tags
}

output "hostname" {
  value = azurerm_redis_cache.main.hostname
}

output "ssl_port" {
  value = azurerm_redis_cache.main.ssl_port
}

output "connection_string" {
  sensitive = true
  value     = "${azurerm_redis_cache.main.hostname}:${azurerm_redis_cache.main.ssl_port},password=${azurerm_redis_cache.main.primary_access_key},ssl=True,abortConnect=False"
}
