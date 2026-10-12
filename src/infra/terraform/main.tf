terraform {
  required_version = ">= 1.6"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = ">= 5.0"
    }
  }
}

provider "aws" {
  region = var.aws_region
}

module "vpc" {
  source     = "./modules/vpc"
  vpc_name   = var.vpc_name
  cidr_block = var.vpc_cidr
}

module "eks" {
  source              = "./modules/eks"
  vpc_id              = module.vpc.vpc_id
  subnet_ids          = module.vpc.private_subnets
  cluster_name        = var.eks_cluster_name
  node_instance_type  = var.eks_node_instance_type
}

module "rds" {
  source          = "./modules/rds"
  vpc_id          = module.vpc.vpc_id
  subnet_ids      = module.vpc.private_subnets
  db_name         = var.rds_db_name
  engine_version  = var.rds_engine_version
  instance_class  = var.rds_instance_class
}

module "apigw" {
  source           = "./modules/apigw"
  vpc_id           = module.vpc.vpc_id
  subnet_ids       = module.vpc.private_subnets
  eks_cluster_name = module.eks.cluster_name
}

module "cloudfront" {
  source        = "./modules/cloudfront"
  api_gw_domain = module.apigw.api_domain_name
}
