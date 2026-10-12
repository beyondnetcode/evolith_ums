resource "aws_cloudfront_distribution" "this" {
  origin {
    domain_name = var.api_gw_domain
    origin_id   = "api-gw-origin"
    custom_origin_config {
      http_port              = 80
      https_port             = 443
      origin_protocol_policy = "https-only"
      origin_ssl_protocols   = ["TLSv1.2"]
    }
  }

  enabled             = true
  is_ipv6_enabled    = true
  comment             = "CloudFront distribution for UMS API Gateway"
  default_root_object = ""

  default_cache_behavior {
    allowed_methods  = ["GET", "HEAD", "OPTIONS"]
    cached_methods   = ["GET", "HEAD"]
    target_origin_id = "api-gw-origin"
    viewer_protocol_policy = "redirect-to-https"
    compress = true
    cache_policy_id = var.cache_policy_id
    origin_request_policy_id = var.origin_request_policy_id
  }

  price_class = "PriceClass_All"
  restrictions {
    geo_restriction {
      restriction_type = "none"
    }
  }

  viewer_certificate {
    cloudfront_default_certificate = true
  }

  tags = {
    Name = "ums-cloudfront"
  }
}
