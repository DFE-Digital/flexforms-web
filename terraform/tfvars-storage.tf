resource "azurerm_storage_account" "tfvars" {
  name                          = "${replace("${local.environment}${local.project_name}", "-", "")}tfvar"
  resource_group_name           = local.existing_resource_group
  location                      = local.azure_location
  account_tier                  = "Standard"
  account_replication_type      = "LRS"
  min_tls_version               = "TLS1_2"
  https_traffic_only_enabled    = true
  public_network_access_enabled = true

  tags = local.tags
}

resource "azurerm_storage_container" "tfvars" {
  name                  = "${local.environment}${local.project_name}-tfvar"
  storage_account_name  = azurerm_storage_account.tfvars.name
  container_access_type = "private"
}

resource "azurerm_storage_blob" "tfvars" {
  name                   = local.tfvars_filename
  storage_account_name   = azurerm_storage_account.tfvars.name
  storage_container_name = azurerm_storage_container.tfvars.name
  type                   = "Block"
  source                 = local.tfvars_filename
  content_md5            = filemd5(local.tfvars_filename)
  access_tier            = "Cool"
}

resource "azurerm_storage_account_network_rules" "tfvars" {
  storage_account_id         = azurerm_storage_account.tfvars.id
  default_action             = length(local.tfvars_access_ipv4) > 0 ? "Deny" : "Allow"
  bypass                     = []
  virtual_network_subnet_ids = []
  ip_rules                   = local.tfvars_access_ipv4
}

resource "null_resource" "tfvars" {
  provisioner "local-exec" {
    interpreter = [local.bash, "-c"]
    command     = "./scripts/check-tfvars-against-remote.sh -c \"${azurerm_storage_container.tfvars.name}\" -a \"${azurerm_storage_account.tfvars.name}\" -f \"${local.tfvars_filename}\""
  }

  triggers = {
    tfvar_file_md5 = filemd5(local.tfvars_filename)
  }
}
