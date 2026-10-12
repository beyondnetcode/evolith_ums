variable "tf_state_bucket" {
  description = "S3 bucket name for remote Terraform state"
  type        = string
}

variable "tf_state_lock_table" {
  description = "DynamoDB table name for state locking"
  type        = string
}
