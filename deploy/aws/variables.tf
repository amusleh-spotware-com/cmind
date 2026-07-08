variable "region" {
  type    = string
  default = "eu-west-1"
}

variable "name_prefix" {
  type    = string
  default = "cmind"
}

variable "image_registry" {
  type        = string
  description = "Registry + repo prefix, e.g. ghcr.io/your-org/cmind (images: -web, -mcp, -node-agent)"
}

variable "image_tag" {
  type    = string
  default = "latest"
}

variable "pg_password" {
  type      = string
  sensitive = true
}

variable "owner_email" {
  type = string
}

variable "owner_password" {
  type      = string
  sensitive = true
}

variable "discovery_join_token" {
  type      = string
  sensitive = true
}
