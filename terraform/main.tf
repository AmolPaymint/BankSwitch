terraform {
  required_version = ">= 1.7.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.100"
    }
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 2.48"
    }
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.28"
    }
    helm = {
      source  = "hashicorp/helm"
      version = "~> 2.13"
    }
  }

  backend "azurerm" {
    resource_group_name  = "bankswitch-tfstate-rg"
    storage_account_name = "bankswitchtfstate"
    container_name       = "tfstate"
    key                  = "bankswitch.terraform.tfstate"
  }
}

provider "azurerm" {
  features {
    resource_group {
      prevent_deletion_if_contains_resources = true
    }
    key_vault {
      purge_soft_delete_on_destroy    = false
      recover_soft_deleted_key_vaults = true
    }
  }
}

# ── Data sources ─────────────────────────────────────────────
data "azurerm_client_config" "current" {}

# ── Resource Group ───────────────────────────────────────────
resource "azurerm_resource_group" "main" {
  name     = var.resource_group_name
  location = var.location
  tags     = var.tags
}

# ── Log Analytics (for AKS Container Insights + diagnostics) ─
resource "azurerm_log_analytics_workspace" "main" {
  name                = "${var.prefix}-law"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = 90
  tags                = var.tags
}

# ── AKS Cluster ──────────────────────────────────────────────
module "aks" {
  source = "./modules/aks"

  prefix              = var.prefix
  resource_group_name = azurerm_resource_group.main.name
  location            = var.location
  kubernetes_version  = var.kubernetes_version

  # System node pool
  system_node_count    = var.aks_system_node_count
  system_vm_size       = var.aks_system_vm_size

  # Engine node pool — optimised for high TPS (CPU-heavy)
  engine_node_count    = var.aks_engine_node_count
  engine_min_count     = var.aks_engine_min_count
  engine_max_count     = var.aks_engine_max_count
  engine_vm_size       = var.aks_engine_vm_size

  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  tags                       = var.tags
}

# ── Azure SQL Server ─────────────────────────────────────────
module "sql" {
  source = "./modules/sql"

  prefix              = var.prefix
  resource_group_name = azurerm_resource_group.main.name
  location            = var.location
  sql_admin_login     = var.sql_admin_login
  sql_admin_password  = var.sql_admin_password
  sku_name            = var.sql_sku_name
  max_size_gb         = var.sql_max_size_gb
  tags                = var.tags
}

# ── Azure Cache for Redis ─────────────────────────────────────
module "redis" {
  source = "./modules/redis"

  prefix              = var.prefix
  resource_group_name = azurerm_resource_group.main.name
  location            = var.location
  sku_name            = var.redis_sku_name
  family              = var.redis_family
  capacity            = var.redis_capacity
  tags                = var.tags
}

# ── Azure Key Vault ───────────────────────────────────────────
resource "azurerm_key_vault" "main" {
  name                        = "${var.prefix}-kv"
  location                    = azurerm_resource_group.main.location
  resource_group_name         = azurerm_resource_group.main.name
  sku_name                    = "standard"
  tenant_id                   = data.azurerm_client_config.current.tenant_id
  soft_delete_retention_days  = 90
  purge_protection_enabled    = true
  enable_rbac_authorization   = true

  network_acls {
    bypass         = "AzureServices"
    default_action = "Deny"
    ip_rules       = var.key_vault_allowed_ips
  }

  tags = var.tags
}

# ── Store secrets in Key Vault ────────────────────────────────
resource "azurerm_key_vault_secret" "sql_connection_string" {
  name         = "switchdb-connection-string"
  value        = module.sql.connection_string
  key_vault_id = azurerm_key_vault.main.id
  content_type = "application/connection-string"
  depends_on   = [azurerm_key_vault.main]
}

resource "azurerm_key_vault_secret" "redis_connection_string" {
  name         = "redis-connection-string"
  value        = module.redis.connection_string
  key_vault_id = azurerm_key_vault.main.id
  content_type = "application/connection-string"
  depends_on   = [azurerm_key_vault.main]
}

# ── Deploy BankSwitch via Helm after AKS is ready ────────────
provider "kubernetes" {
  host                   = module.aks.kube_config.host
  client_certificate     = base64decode(module.aks.kube_config.client_certificate)
  client_key             = base64decode(module.aks.kube_config.client_key)
  cluster_ca_certificate = base64decode(module.aks.kube_config.cluster_ca_certificate)
}

provider "helm" {
  kubernetes {
    host                   = module.aks.kube_config.host
    client_certificate     = base64decode(module.aks.kube_config.client_certificate)
    client_key             = base64decode(module.aks.kube_config.client_key)
    cluster_ca_certificate = base64decode(module.aks.kube_config.cluster_ca_certificate)
  }
}

resource "kubernetes_secret" "bankswitch_secrets" {
  metadata {
    name      = "bankswitch-secrets"
    namespace = "bankswitch"
  }

  data = {
    switchdb-connection-string = module.sql.connection_string
    redis-connection-string    = module.redis.connection_string
    admin-bootstrap-password   = var.admin_bootstrap_password
  }

  type       = "Opaque"
  depends_on = [module.aks]
}
