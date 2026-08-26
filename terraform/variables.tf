variable "azure_client_id" {
  description = "Service Principal Client ID"
  type        = string
}

variable "azure_client_secret" {
  description = "Service Principal Client Secret"
  type        = string
  sensitive   = true
}

variable "azure_tenant_id" {
  description = "Service Principal Tenant ID"
  type        = string
}

variable "azure_subscription_id" {
  description = "Service Principal Subscription ID"
  type        = string
}

variable "environment" {
  description = "Environment name. Will be used along with `project_name` as a prefix for all resources."
  type        = string
}

variable "tfvars_access_ipv4" {
  description = "List of IPv4 Addresses that are permitted to access the tfvars storage"
  type        = list(string)
}

variable "tfvars_filename" {
  description = "tfvars filename. This file is uploaded and stored encrupted within Key Vault, to ensure that the latest tfvars are stored in a shared place."
  type        = string
}

variable "project_name" {
  description = "Project name. Will be used along with `environment` as a prefix for all resources."
  type        = string
}

variable "azure_location" {
  description = "Azure location in which to launch resources."
  type        = string
}

variable "tags" {
  description = "Tags to be applied to all resources"
  type        = map(string)
}

variable "container_apps_infra_subnet_cidr" {
  description = "Specify a subnet prefix to use for the container_apps_infra subnet"
  type        = string
  default     = "172.16.110.64/28"
}

variable "enable_container_registry" {
  description = "Set to true to create a container registry"
  type        = bool
  default     = false
}

variable "registry_server" {
  description = "Container registry server (required if `enable_container_registry` is false)"
  type        = string
  default     = ""
}

variable "registry_use_managed_identity" {
  description = "Create a User-Assigned Managed Identity for the Container App. Note: If you do not have 'Microsoft.Authorization/roleAssignments/write' permission, you will need to manually assign the 'AcrPull' Role to the identity"
  type        = bool
  default     = true
}

variable "registry_managed_identity_assign_role" {
  description = "Assign the 'AcrPull' Role to the Container App User-Assigned Managed Identity. Note: If you do not have 'Microsoft.Authorization/roleAssignments/write' permission, you will need to manually assign the 'AcrPull' Role to the identity"
  type        = bool
  default     = false
}

variable "registry_admin_enabled" {
  description = "Do you want to enable access key based authentication for your Container Registry?"
  type        = bool
  default     = false
}

variable "image_name" {
  description = "Image name"
  type        = string
}

variable "container_command" {
  description = "Container command"
  type        = list(any)
}

variable "container_secret_environment_variables" {
  description = "Container secret environment variables"
  type        = map(string)
  sensitive   = true
}

variable "container_scale_http_concurrency" {
  description = "When the number of concurrent HTTP requests exceeds this value, then another replica is added. Replicas continue to add to the pool up to the max-replicas amount."
  type        = number
  default     = 10
}

variable "container_apps_allow_ips_inbound" {
  description = "Restricts access to the Container Apps by creating a network security group rule that only allow inbound traffic from the provided list of IPs"
  type        = list(string)
  default     = []
}

variable "enable_monitoring" {
  description = "Create an App Insights instance and notification group for the Container App"
  type        = bool
}

variable "monitor_email_receivers" {
  description = "A list of email addresses that should be notified by monitoring alerts"
  type        = list(string)
}

variable "container_health_probe_path" {
  description = "Specifies the path that is used to determine the liveness of the Container"
  type        = string
}

variable "monitor_endpoint_healthcheck" {
  description = "Specify a route that should be monitored for a 200 OK status"
  type        = string
}

variable "existing_logic_app_workflow" {
  description = "Name, and Resource Group of an existing Logic App Workflow. Leave empty to create a new Resource"
  type = object({
    name : string
    resource_group_name : string
  })
  default = {
    name                = ""
    resource_group_name = ""
  }
}

variable "existing_network_watcher_name" {
  description = "Use an existing network watcher to add flow logs."
  type        = string
}

variable "existing_network_watcher_resource_group_name" {
  description = "Existing network watcher resource group."
  type        = string
}

variable "container_min_replicas" {
  description = "Container min replicas"
  type        = number
  default     = 1
}

variable "enable_health_insights_api" {
  description = "Deploys a Function App that exposes the last 3 HTTP Web Tests via an API endpoint. 'enable_app_insights_integration' and 'enable_monitoring' must be set to 'true'."
  type        = bool
  default     = false
}

variable "health_insights_api_cors_origins" {
  description = "List of hostnames that are permitted to contact the Health insights API"
  type        = list(string)
  default     = ["*"]
}

variable "health_insights_api_ipv4_allow_list" {
  description = "List of IPv4 addresses that are permitted to contact the Health insights API"
  type        = list(string)
  default     = []
}

variable "container_port" {
  description = "Container port"
  type        = number
  default     = 8080
}

variable "enable_init_container" {
  description = "Deploy an Init Container. Init containers run before the primary app container and are used to perform initialization tasks such as downloading data or preparing the environment"
  type        = bool
  default     = false
}

variable "init_container_image" {
  description = "Image name for the Init Container. Leave blank to use the same Container image from the primary app"
  type        = string
  default     = ""
}

variable "init_container_command" {
  description = "Container command for the Init Container"
  type        = list(any)
  default     = []
}

variable "monitor_http_availability_fqdn" {
  description = "Specify a FQDN to monitor for HTTP Availability. Leave unset to dynamically calculate the correct FQDN"
  type        = string
  default     = ""
}

variable "enable_monitoring_traces" {
  description = "Monitor App Insights traces for error messages"
  type        = bool
  default     = true
}

variable "existing_container_app_environment" {
  description = "Conditionally launch resources into an existing Container App environment. Specifying this will NOT create an environment."
  type = object({
    name           = string
    resource_group = string
  })
}

variable "existing_virtual_network" {
  description = "Conditionally use an existing virtual network. The `virtual_network_address_space` must match an existing address space in the VNet. This also requires the resource group name."
  type        = string
}

variable "existing_resource_group" {
  description = "Conditionally launch resources into an existing resource group. Specifying this will NOT create a resource group."
  type        = string
}

variable "container_app_name_override" {
  type        = string
  description = "A custom name for the Container App"
  default     = ""
}

variable "restrict_container_apps_to_cdn_inbound_only" {
  description = "Restricts access to the Container Apps by creating a network security group rule that only allows 'AzureFrontDoor.Backend' inbound, and attaches it to the subnet of the container app environment."
  type        = bool
  default     = false
}

variable "enable_dns_zone" {
  description = "Conditionally create a DNS zone"
  type        = bool
}

variable "dns_zone_domain_name" {
  description = "DNS zone domain name. If created, records will automatically be created to point to the CDN."
  type        = string
}

variable "dns_ns_records" {
  description = "DNS NS records to add to the DNS Zone"
  type = map(
    object({
      ttl : optional(number, 300),
      records : list(string)
    })
  )
}

variable "dns_txt_records" {
  description = "DNS TXT records to add to the DNS Zone"
  type = map(
    object({
      ttl : optional(number, 300),
      records : list(string)
    })
  )
}

variable "dns_mx_records" {
  description = "DNS MX records to add to the DNS Zone"
  type = map(
    object({
      ttl : optional(number, 300),
      records : list(
        object({
          preference : number,
          exchange : string
        })
      )
    })
  )
  default = {}
}

variable "dns_alias_records" {
  description = "DNS ALIAS records to add to the DNS Zone"
  type = map(
    object({
      ttl : optional(number, 300),
      target_resource_id : string
    })
  )
  default = {}
}
