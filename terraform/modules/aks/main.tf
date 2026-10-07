variable "prefix"              { type = string }
variable "resource_group_name" { type = string }
variable "location"            { type = string }
variable "kubernetes_version"  { type = string }
variable "system_node_count"   { type = number }
variable "system_vm_size"      { type = string }
variable "engine_node_count"   { type = number }
variable "engine_min_count"    { type = number }
variable "engine_max_count"    { type = number }
variable "engine_vm_size"      { type = string }
variable "log_analytics_workspace_id" { type = string }
variable "tags"                { type = map(string) }

resource "azurerm_kubernetes_cluster" "main" {
  name                = "${var.prefix}-aks"
  location            = var.location
  resource_group_name = var.resource_group_name
  dns_prefix          = "${var.prefix}-aks"
  kubernetes_version  = var.kubernetes_version

  # System node pool — Kubernetes control-plane components
  default_node_pool {
    name                = "system"
    node_count          = var.system_node_count
    vm_size             = var.system_vm_size
    os_disk_size_gb     = 128
    type                = "VirtualMachineScaleSets"
    only_critical_addons_enabled = true   # Only system pods on this pool
    node_labels = {
      "nodepool" = "system"
    }
  }

  # Managed identity for AKS → Key Vault, ACR, etc.
  identity {
    type = "SystemAssigned"
  }

  # OIDC issuer for Workload Identity (replaces pod-level managed identity)
  oidc_issuer_enabled       = true
  workload_identity_enabled = true

  # Container Insights (log analytics)
  oms_agent {
    log_analytics_workspace_id = var.log_analytics_workspace_id
  }

  # Azure Policy for PCI DSS compliance gating
  azure_policy_enabled = true

  # Network: Azure CNI for production (VNet integration, security groups)
  network_profile {
    network_plugin    = "azure"
    network_policy    = "calico"   # Calico for NetworkPolicy enforcement
    load_balancer_sku = "standard"
  }

  # API server access — restrict to known CIDRs in production
  api_server_access_profile {
    authorized_ip_ranges = ["0.0.0.0/0"]  # Override in production
  }

  tags = var.tags
}

# Engine node pool — compute-optimised for ISO 8583 processing
resource "azurerm_kubernetes_cluster_node_pool" "engine" {
  name                  = "engine"
  kubernetes_cluster_id = azurerm_kubernetes_cluster.main.id
  vm_size               = var.engine_vm_size
  node_count            = var.engine_node_count
  enable_auto_scaling   = true
  min_count             = var.engine_min_count
  max_count             = var.engine_max_count
  os_disk_size_gb       = 128
  mode                  = "User"

  node_labels = {
    "nodepool"    = "engine"
    "workload"    = "iso8583"
    "pci-scope"   = "true"
  }

  node_taints = [
    "workload=iso8583:NoSchedule"   # Only Engine pods scheduled here
  ]

  tags = var.tags
}

output "cluster_name"    { value = azurerm_kubernetes_cluster.main.name }
output "cluster_fqdn"    { value = azurerm_kubernetes_cluster.main.fqdn }
output "kube_config_raw" { value = azurerm_kubernetes_cluster.main.kube_config_raw; sensitive = true }
output "kube_config"     { value = azurerm_kubernetes_cluster.main.kube_config[0] }
output "cluster_identity_principal_id" {
  value = azurerm_kubernetes_cluster.main.identity[0].principal_id
}
