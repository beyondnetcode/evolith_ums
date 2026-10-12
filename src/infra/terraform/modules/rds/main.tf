resource "aws_db_subnet_group" "this" {
  name       = "${var.db_name}-subnet-group"
  subnet_ids = var.subnet_ids
  description = "Subnet group for RDS PostgreSQL"
  tags = {
    Name = "${var.db_name}-subnet-group"
  }
}

resource "aws_db_instance" "this" {
  identifier = var.db_name
  engine     = "postgres"
  engine_version = var.engine_version
  instance_class = var.instance_class
  allocated_storage = var.allocated_storage
  name       = var.db_name
  username   = var.username
  password   = var.password
  vpc_security_group_ids = var.security_group_ids
  db_subnet_group_name = aws_db_subnet_group.this.name
  skip_final_snapshot   = true
  publicly_accessible   = false
  multi_az    = var.multi_az
  storage_type = "gp2"
  tags = {
    Name = var.db_name
  }
}
