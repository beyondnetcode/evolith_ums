variable "api_name" {
  description = "Name for the API Gateway"
  type        = string
  default     = "ums-api"
}

variable "subnet_ids" {
  description = "List of subnet IDs for API Gateway integration"
  type        = list(string)
}

variable "eks_cluster_name" {
  description = "Name of the EKS cluster (used for IAM role association)"
  type        = string
}
