# GraphQL Query Side

UMS exposes GraphQL only as a CQRS query-side API.

## Boundary

GraphQL is for read models only:

- tenant list
- tenant detail
- future dashboard/composite reads

Commands remain on REST Minimal APIs:

- `POST /api/v1/tenants`
- `POST /api/v1/tenants/{tenantId}/activate`
- `POST /api/v1/tenants/{tenantId}/suspend`
- branch, branding, and identity-provider commands

## Endpoint

```text
POST /graphql
```

The endpoint is intentionally outside `/api/v{version}` because the GraphQL schema is versioned as a contract, not as REST routes.

## Initial Queries

### Tenants

```graphql
query Tenants {
  tenants(page: 1, pageSize: 20, status: "all", sortBy: "name", sortOrder: "asc") {
    items {
      tenantId
      code
      name
      type
      status
      parentTenantId
      companyReference
    }
    page
    pageSize
    totalItems
    totalPages
  }
}
```

### Tenant By ID

```graphql
query TenantById($tenantId: UUID!) {
  tenantById(tenantId: $tenantId) {
    tenantId
    code
    name
    status
  }
}
```

### Tenant Child Objects

```graphql
query TenantMaintenance($tenantId: UUID!) {
  tenantBranches(tenantId: $tenantId) {
    branchId
    code
    name
    isActive
    geofencingMetadata
  }

  tenantBranding(tenantId: $tenantId) {
    logo
    logoFormat
    primaryColor
    backgroundStyle
    headlineText
    customDomain
    magicLinkFallbackEnabled
    dnsVerificationStatus
  }

  tenantIdentityProviders(tenantId: $tenantId) {
    identityProviderId
    code
    name
    description
    strategy
    isActive
  }
}
```

## Current Coverage

GraphQL query coverage now includes 100% of the Domain aggregates across all Bounded Contexts:

- **Identity**: Tenant, UserAccount, UserManagementDelegation
- **Authorization**: Profile, SystemSuite, Template
- **IGA**: PromotionRequest, RoleMaturityStatus
- **Approvals**: AccessEnforcementPolicy, ApprovalRequest, ApprovalWorkflow, DocumentType, NotificationRule, UserDocument
- **Configuration**: AppConfiguration, FeatureFlag, IdpConfiguration
- **Audit**: AuditRecord

All these aggregates currently expose their respective Application query handlers and are fully registered in the GraphQL schema via `GraphQlServiceCollectionExtensions.cs`.

## Safety Rules

- No GraphQL mutations for MVP.
- No direct domain aggregate exposure.
- No direct EF entity exposure.
- Resolvers call application query handlers.
- Query depth is limited.
- Execution timeout is limited.
- ASP.NET rate limiting applies to `/graphql`.
- Future nested resolvers must use DataLoader to avoid N+1 behavior.

## Persistence Direction

The long-term query side should use EF Core projections directly into DTO/read models.

Recommended future shape:

```text
GraphQL Resolver
  -> Application Query Service
    -> EF Core DbContext
      -> AsNoTracking projection
      -> DTO / read model
```

Tenant isolation must still be enforced through the primary application/EF layer, with database RLS as optional failsafe where infrastructure supports it.
