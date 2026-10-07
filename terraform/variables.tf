variable "prefix" {
  description = "Short prefix used in all Azure resource names (e.g. 'bsw', 'bankswitch')."
  type        = string
  default     = "bsw"
}

variable "resource_group_name" {
  description = "Name of the Azure resource group."
  type        = string
  default     = "bankswitch-prod-rg"
}

variable "location" {
  description = "Azure region for all resources."
  type        = string
  default     = "westeurope"
}

variable "kubernetes_version" {
  description = "AKS Kubernetes version."
  type        = string
  default     = "1.29"
}

variable "aks_system_node_count" {
  description = "Number of system (control-plane) nodes."
  type        = number
  default     = 3
}

variable "aks_system_vm_size" {
  description = "VM size for system node pool."
  type        = string
  default     = "Standard_D2s_v5"
}

variable "aks_engine_node_count" {
  description = "Initial number of Engine worker nodes."
  type        = number
  default     = 3
}

variable "aks_engine_min_count" {
  description = "Minimum Engine nodes (auto-scaler)."
  type        = number
  default     = 3
}

variable "aks_engine_max_count" {
  description = "Maximum Engine nodes (auto-scaler)."
  type        = number
  default     = 20
}

variable "aks_engine_vm_size" {
  description = "VM size for Engine node pool (compute-optimised for high TPS)."
  type        = string
  default     = "Standard_F8s_v2"   # 8 vCPU, 16 GB — ideal for ISO 8583 processing
}

variable "sql_admin_login" {
  description = "SQL Server admin username."
  type        = string
  default     = "sqladmin"
}

variable "sql_admin_password" {
  description = "SQL Server admin password (stored in Key Vault)."
  type        = string
  sensitive   = true
}

variable "sql_sku_name" {
  description = "Azure SQL Database SKU."
  type        = string
  default     = "GP_Gen5_8"   # General Purpose, 8 vCores
}

variable "sql_max_size_gb" {
  description = "SQL Database maximum size in GB."
  type        = number
  default     = 512
}

variable "redis_sku_name" {
  description = "Azure Cache for Redis SKU."
  type        = string
  default     = "Standard"
}

variable "redis_family" {
  description = "Redis family (C = Standard/Basic, P = Premium)."
  type        = string
  default     = "C"
}

variable "redis_capacity" {
  description = "Redis cache capacity (1 = 1 GB for Standard C1)."
  type        = number
  default     = 1
}

variable "admin_bootstrap_password" {
  description = "BankSwitch Admin portal bootstrap password."
  type        = string
  sensitive   = true
}

variable "key_vault_allowed_ips" {
  description = "IP addresses allowed to access Key Vault."
  type        = list(string)
  default     = []
}

variable "tags" {
  description = "Common Azure tags applied to all resources."
  type        = map(string)
  default = {
    project     = "BankSwitch"
    environment = "production"
    managed-by  = "terraform"
    pci-scope   = "true"
  }
}
