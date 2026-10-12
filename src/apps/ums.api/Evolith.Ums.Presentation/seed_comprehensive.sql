-- =============================================================================
-- COMPREHENSIVE SEED DATA - ALL TENANTS WITH FULL DATA
-- =============================================================================

-- =============================================================================
-- 1. GLOBAL SYSTEM SUITE (for all tenants)
-- =============================================================================
INSERT INTO SystemSuites (Id, TenantId, Code, Name, Description, StatusId, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan, RowVersion)
VALUES
('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000000', 'UMS', 'User Management System', 'Core UMS functionality', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001');

-- =============================================================================
-- 2. INTERNAL ADMIN TENANT
-- =============================================================================
INSERT INTO Tenants (Id, Code, Name, OrganizationTypeId, IdpStrategyId, CompanyReference, ParentTenantId, StatusId, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan, RowVersion, IsDeleted, DeletedAtUtc, DeletedBy)
VALUES
('11111111-1111-1111-1111-111111111111', 'INTERNAL_ADMIN', 'Internal Admin Tenant', 1, 1, 'INTERNAL', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL);

-- INTERNAL_ADMIN Branches
INSERT INTO TenantBranches (Id, TenantId, Code, Name, GeofencingMetadata, IsActive, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan)
VALUES
('11111111-1111-1111-1111-111111111101', '11111111-1111-1111-1111-111111111111', 'HQ', 'Headquarters', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S');

-- INTERNAL_ADMIN Branding
INSERT INTO TenantBrandings (Id, TenantId, Logo, LogoFormatId, PrimaryColor, BackgroundStyleId, HeadlineText, SecondaryText, PrimaryButtonLabel, FooterText, CustomDomain, DnsVerificationStatusId, DnsCnameTarget, MagicLinkFallbackEnabled, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan)
VALUES
('11111111-1111-1111-1111-111111111101', '11111111-1111-1111-1111-111111111111', 'internal_admin_logo', 1, '#1a1a2e', 1, 'UMS Admin Portal', 'System Administration', 'Sign In', '© 2026 BeyondNet', NULL, 1, 'internal.admin.cname', 0, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S');

-- INTERNAL_ADMIN Identity Provider (Local)
INSERT INTO TenantIdentityProviders (Id, TenantId, Code, Name, Description, StrategyId, IsActive, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan)
VALUES
('11111111-1111-1111-1111-111111111101', '11111111-1111-1111-1111-111111111111', 'LOCAL', 'Local Authentication', 'Internal local authentication', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S');

-- INTERNAL_ADMIN SystemSuite
INSERT INTO SystemSuites (Id, TenantId, Code, Name, Description, StatusId, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan, RowVersion)
VALUES
('11111111-1111-1111-1111-111111111101', '11111111-1111-1111-1111-111111111111', 'UMS_ADMIN', 'UMS Administration', 'Platform administration suite', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001');

-- INTERNAL_ADMIN Role (Global Admin)
INSERT INTO Roles (Id, TenantId, SystemSuiteId, ParentRoleId, Code, Value, Description, HierarchyLevel, PromotionOrder, IsActive, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan, RowVersion)
VALUES
('11111111-1111-1111-1111-111111111101', '11111111-1111-1111-1111-111111111111', '11111111-1111-1111-1111-111111111101', NULL, 'GLOBAL_ADMIN', 'Global Administrator', 'Full platform administration access', 0, 0, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001');

-- INTERNAL_ADMIN Permission Template
INSERT INTO PermissionTemplates (Id, TenantId, RoleId, SystemSuiteId, Version, StatusId, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan, RowVersion)
VALUES
('11111111-1111-1111-1111-111111111101', '11111111-1111-1111-1111-111111111111', '11111111-1111-1111-1111-111111111101', '11111111-1111-1111-1111-111111111101', '1.0', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001');

-- INTERNAL_ADMIN Permission Template Items (full access)
INSERT INTO PermissionTemplateItems (Id, TemplateId, ResourceType, ResourceId, ActionId, IsAllowed, IsOverride, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan)
VALUES
('11111111-1111-1111-1111-111111111101', '11111111-1111-1111-1111-111111111101', 'TENANT', '*', 1, 1, 0, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('11111111-1111-1111-1111-111111111102', '11111111-1111-1111-1111-111111111101', 'USER', '*', 1, 1, 0, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('11111111-1111-1111-1111-111111111103', '11111111-1111-1111-1111-111111111101', 'ROLE', '*', 1, 1, 0, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('11111111-1111-1111-1111-111111111104', '11111111-1111-1111-1111-111111111101', 'PROFILE', '*', 1, 1, 0, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('11111111-1111-1111-1111-111111111105', '11111111-1111-1111-1111-111111111101', 'BRANCH', '*', 1, 1, 0, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S');

-- INTERNAL_ADMIN User Account
INSERT INTO UserAccounts (Id, TenantId, BranchId, Email, CategoryId, StatusId, IdentityReference, IdentityReferenceTypeId, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan, RowVersion, IsDeleted, DeletedAtUtc, DeletedBy, AnonymizedAtUtc)
VALUES
('22222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111', '11111111-1111-1111-1111-111111111101', 'admin@ums.local', 2, 2, 'admin', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL, NULL);

-- INTERNAL_ADMIN Password
INSERT INTO UserAccountPasswordCredentials (Id, UserAccountId, PasswordHash, IsActive, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan)
VALUES
('22222222-2222-2222-2222-222222222201', '22222222-2222-2222-2222-222222222222', '$2a$11$SqJUQs1BkxYO/UIkRUsp7e8efRJ/P9OcB89axCwTTbmWlvoYvfi2u', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S');

-- INTERNAL_ADMIN Profile
INSERT INTO Profiles (Id, TenantId, UserId, RoleId, BranchId, ScopeId, IsActive, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan, RowVersion)
VALUES
('22222222-2222-2222-2222-222222222201', '11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111101', '11111111-1111-1111-1111-111111111101', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001');

-- =============================================================================
-- 3. BUSINESS TENANTS (10 tenants)
-- =============================================================================

-- TENANTS INSERT
INSERT INTO Tenants (Id, Code, Name, OrganizationTypeId, IdpStrategyId, CompanyReference, ParentTenantId, StatusId, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan, RowVersion, IsDeleted, DeletedAtUtc, DeletedBy)
VALUES
('00000001-0000-0000-0000-000000000001', 'TECHNO', 'TechnoCorp', 2, 2, 'TECH001', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL),
('00000002-0000-0000-0000-000000000001', 'LOGISTICA', 'LogisticaGlobal', 1, 1, 'LOGI001', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL),
('00000003-0000-0000-0000-000000000001', 'RETAIL', 'RetailMax', 1, 1, 'RTEL001', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL),
('00000004-0000-0000-0000-000000000001', 'SALUD', 'SaludTotal', 2, 3, 'SLUD001', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL),
('00000005-0000-0000-0000-000000000001', 'EDU', 'EduLearn', 1, 2, 'EDUC001', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL),
('00000006-0000-0000-0000-000000000001', 'FINANCE', 'FinancePro', 2, 1, 'FINC001', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL),
('00000007-0000-0000-0000-000000000001', 'MEDIA', 'MediaHub', 1, 3, 'MEDA001', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL),
('00000008-0000-0000-0000-000000000001', 'AGILE', 'AgileFlow', 2, 2, 'AGIL001', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL),
('00000009-0000-0000-0000-000000000001', 'NEXTGEN', 'NextGenTech', 1, 1, 'NEXT001', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL),
('0000000a-0000-0000-0000-000000000001', 'QUANTUM', 'QuantumLabs', 2, 3, 'QNTM001', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL);

UPDATE Tenants
SET IsManagementOwner = CASE WHEN Code = 'INTERNAL_ADMIN' THEN 1 ELSE 0 END;

-- =============================================================================
-- 4. TENANT BRANCHES (2-3 per tenant)
-- =============================================================================
INSERT INTO TenantBranches (Id, TenantId, Code, Name, GeofencingMetadata, IsActive, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan)
VALUES
-- TECHNO branches
('00000011-0000-0000-0000-000000000001', '00000001-0000-0000-0000-000000000001', 'HQ', 'TechnoCorp Headquarters', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000011-0000-0000-0000-000000000002', '00000001-0000-0000-0000-000000000001', 'BR01', 'Austin Office', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000011-0000-0000-0000-000000000003', '00000001-0000-0000-0000-000000000001', 'BR02', 'Seattle Lab', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- LOGISTICA branches
('00000012-0000-0000-0000-000000000001', '00000002-0000-0000-0000-000000000001', 'HQ', 'LogisticaGlobal HQ', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000012-0000-0000-0000-000000000002', '00000002-0000-0000-0000-000000000001', 'BR01', 'Miami Warehouse', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- RETAIL branches
('00000013-0000-0000-0000-000000000001', '00000003-0000-0000-0000-000000000001', 'HQ', 'RetailMax HQ', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000013-0000-0000-0000-000000000002', '00000003-0000-0000-0000-000000000001', 'BR01', 'NYC Flagship', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000013-0000-0000-0000-000000000003', '00000003-0000-0000-0000-000000000001', 'BR02', 'LA Store', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- SALUD branches
('00000014-0000-0000-0000-000000000001', '00000004-0000-0000-0000-000000000001', 'HQ', 'SaludTotal HQ', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000014-0000-0000-0000-000000000002', '00000004-0000-0000-0000-000000000001', 'BR01', 'Miami Clinic', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- EDU branches
('00000015-0000-0000-0000-000000000001', '00000005-0000-0000-0000-000000000001', 'HQ', 'EduLearn HQ', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000015-0000-0000-0000-000000000002', '00000005-0000-0000-0000-000000000001', 'BR01', 'Boston Campus', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000015-0000-0000-0000-000000000003', '00000005-0000-0000-0000-000000000001', 'BR02', 'Online Platform', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- FINANCE branches
('00000016-0000-0000-0000-000000000001', '00000006-0000-0000-0000-000000000001', 'HQ', 'FinancePro HQ', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000016-0000-0000-0000-000000000002', '00000006-0000-0000-0000-000000000001', 'BR01', 'NYC Office', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- MEDIA branches
('00000017-0000-0000-0000-000000000001', '00000007-0000-0000-0000-000000000001', 'HQ', 'MediaHub HQ', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000017-0000-0000-0000-000000000002', '00000007-0000-0000-0000-000000000001', 'BR01', 'LA Studio', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000017-0000-0000-0000-000000000003', '00000007-0000-0000-0000-000000000001', 'BR02', 'NYC Office', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- AGILE branches
('00000018-0000-0000-0000-000000000001', '00000008-0000-0000-0000-000000000001', 'HQ', 'AgileFlow HQ', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000018-0000-0000-0000-000000000002', '00000008-0000-0000-0000-000000000001', 'BR01', 'Portland Office', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- NEXTGEN branches
('00000019-0000-0000-0000-000000000001', '00000009-0000-0000-0000-000000000001', 'HQ', 'NextGenTech HQ', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000019-0000-0000-0000-000000000002', '00000009-0000-0000-0000-000000000001', 'BR01', 'SF Office', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000019-0000-0000-0000-000000000003', '00000009-0000-0000-0000-000000000001', 'BR02', 'San Diego Lab', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- QUANTUM branches
('0000001a-0000-0000-0000-000000000001', '0000000a-0000-0000-0000-000000000001', 'HQ', 'QuantumLabs HQ', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('0000001a-0000-0000-0000-000000000002', '0000000a-0000-0000-0000-000000000001', 'BR01', 'Houston Research', NULL, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S');

-- =============================================================================
-- 5. TENANT BRANDINGS
-- =============================================================================
INSERT INTO TenantBrandings (Id, TenantId, Logo, LogoFormatId, PrimaryColor, BackgroundStyleId, HeadlineText, SecondaryText, PrimaryButtonLabel, FooterText, CustomDomain, DnsVerificationStatusId, DnsCnameTarget, MagicLinkFallbackEnabled, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan)
VALUES
('00000021-0000-0000-0000-000000000001', '00000001-0000-0000-0000-000000000001', 'techno_logo', 1, '#0f4c75', 1, 'TechnoCorp Portal', 'Innovation at Scale', 'Sign In', '© 2026 TechnoCorp', 'login.techno.io', 1, 'techno.cname', 0, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000022-0000-0000-0000-000000000001', '00000002-0000-0000-0000-000000000001', 'logistica_logo', 1, '#1b4332', 1, 'LogisticaGlobal', 'Supply Chain Excellence', 'Sign In', '© 2026 LogisticaGlobal', 'login.logistica.io', 1, 'logistica.cname', 0, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000023-0000-0000-0000-000000000001', '00000003-0000-0000-0000-000000000001', 'retail_logo', 1, '#7b2cbf', 1, 'RetailMax', 'Retail Redefined', 'Sign In', '© 2026 RetailMax', 'login.retail.io', 1, 'retail.cname', 0, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000024-0000-0000-0000-000000000001', '00000004-0000-0000-0000-000000000001', 'salud_logo', 1, '#00b4d8', 1, 'SaludTotal', 'Healthcare Innovation', 'Sign In', '© 2026 SaludTotal', 'login.salud.io', 1, 'salud.cname', 0, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000025-0000-0000-0000-000000000001', '00000005-0000-0000-0000-000000000001', 'edu_logo', 1, '#f77f00', 1, 'EduLearn', 'Learn Without Limits', 'Sign In', '© 2026 EduLearn', 'login.edu.io', 1, 'edu.cname', 0, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000026-0000-0000-0000-000000000001', '00000006-0000-0000-0000-000000000001', 'finance_logo', 1, '#2d6a4f', 1, 'FinancePro', 'Financial Excellence', 'Sign In', '© 2026 FinancePro', 'login.finance.io', 1, 'finance.cname', 0, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000027-0000-0000-0000-000000000001', '00000007-0000-0000-0000-000000000001', 'media_logo', 1, '#e63946', 1, 'MediaHub', 'Content Everywhere', 'Sign In', '© 2026 MediaHub', 'login.media.io', 1, 'media.cname', 0, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000028-0000-0000-0000-000000000001', '00000008-0000-0000-0000-000000000001', 'agile_logo', 1, '#06d6a0', 1, 'AgileFlow', 'Flow Through Agility', 'Sign In', '© 2026 AgileFlow', 'login.agile.io', 1, 'agile.cname', 0, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000029-0000-0000-0000-000000000001', '00000009-0000-0000-0000-000000000001', 'nextgen_logo', 1, '#7209b7', 1, 'NextGenTech', 'Future Today', 'Sign In', '© 2026 NextGenTech', 'login.nextgen.io', 1, 'nextgen.cname', 0, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('0000002a-0000-0000-0000-000000000001', '0000000a-0000-0000-0000-000000000001', 'quantum_logo', 1, '#3a0ca3', 1, 'QuantumLabs', 'Quantum Computing', 'Sign In', '© 2026 QuantumLabs', 'login.quantum.io', 1, 'quantum.cname', 0, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S');

-- =============================================================================
-- 6. TENANT IDENTITY PROVIDERS
-- =============================================================================
INSERT INTO TenantIdentityProviders (Id, TenantId, Code, Name, Description, StrategyId, IsActive, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan)
VALUES
-- TECHNO (Federated with Entra)
('00000031-0000-0000-0000-000000000001', '00000001-0000-0000-0000-000000000001', 'ENTRA', 'Microsoft Entra ID', 'Azure AD for TechnoCorp', 2, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000031-0000-0000-0000-000000000002', '00000001-0000-0000-0000-000000000001', 'LOCAL', 'Local Authentication', 'Local bcrypt auth', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- LOGISTICA (Local only)
('00000032-0000-0000-0000-000000000001', '00000002-0000-0000-0000-000000000001', 'LOCAL', 'Local Authentication', 'Local bcrypt auth', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- RETAIL (Local + Google)
('00000033-0000-0000-0000-000000000001', '00000003-0000-0000-0000-000000000001', 'LOCAL', 'Local Authentication', 'Local bcrypt auth', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000033-0000-0000-0000-000000000002', '00000003-0000-0000-0000-000000000001', 'GOOGLE', 'Google Workspace', 'Google OAuth for RetailMax', 2, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- SALUD (Federated)
('00000034-0000-0000-0000-000000000001', '00000004-0000-0000-0000-000000000001', 'ENTRA', 'Microsoft Entra ID', 'Azure AD for SaludTotal', 2, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- EDU (Federated + Local)
('00000035-0000-0000-0000-000000000001', '00000005-0000-0000-0000-000000000001', 'ENTRA', 'Microsoft Entra ID', 'Azure AD for EduLearn', 2, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000035-0000-0000-0000-000000000002', '00000005-0000-0000-0000-000000000001', 'LOCAL', 'Local Authentication', 'Local backup auth', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- FINANCE (Local only, high security)
('00000036-0000-0000-0000-000000000001', '00000006-0000-0000-0000-000000000001', 'LOCAL', 'Local Authentication', 'Local bcrypt auth - high security', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- MEDIA (Google)
('00000037-0000-0000-0000-000000000001', '00000007-0000-0000-0000-000000000001', 'GOOGLE', 'Google Workspace', 'Google OAuth for MediaHub', 2, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- AGILE (Federated)
('00000038-0000-0000-0000-000000000001', '00000008-0000-0000-0000-000000000001', 'ENTRA', 'Microsoft Entra ID', 'Azure AD for AgileFlow', 2, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- NEXTGEN (Local)
('00000039-0000-0000-0000-000000000001', '00000009-0000-0000-0000-000000000001', 'LOCAL', 'Local Authentication', 'Local bcrypt auth', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- QUANTUM (Federated)
('0000003a-0000-0000-0000-000000000001', '0000000a-0000-0000-0000-000000000001', 'ENTRA', 'Microsoft Entra ID', 'Azure AD for QuantumLabs', 2, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S');

-- =============================================================================
-- 7. SYSTEM SUITES (one per tenant)
-- =============================================================================
INSERT INTO SystemSuites (Id, TenantId, Code, Name, Description, StatusId, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan, RowVersion)
VALUES
('00000101-0000-0000-0000-000000000001', '00000001-0000-0000-0000-000000000001', 'TECHNO_CORE', 'TechnoCorp Core', 'Core business suite for TechnoCorp', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000102-0000-0000-0000-000000000001', '00000002-0000-0000-0000-000000000001', 'LOGISTICA_CORE', 'LogisticaGlobal Core', 'Core business suite for LogisticaGlobal', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000103-0000-0000-0000-000000000001', '00000003-0000-0000-0000-000000000001', 'RETAIL_CORE', 'RetailMax Core', 'Core business suite for RetailMax', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000104-0000-0000-0000-000000000001', '00000004-0000-0000-0000-000000000001', 'SALUD_CORE', 'SaludTotal Core', 'Core business suite for SaludTotal', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000105-0000-0000-0000-000000000001', '00000005-0000-0000-0000-000000000001', 'EDU_CORE', 'EduLearn Core', 'Core business suite for EduLearn', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000106-0000-0000-0000-000000000001', '00000006-0000-0000-0000-000000000001', 'FINANCE_CORE', 'FinancePro Core', 'Core business suite for FinancePro', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000107-0000-0000-0000-000000000001', '00000007-0000-0000-0000-000000000001', 'MEDIA_CORE', 'MediaHub Core', 'Core business suite for MediaHub', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000108-0000-0000-0000-000000000001', '00000008-0000-0000-0000-000000000001', 'AGILE_CORE', 'AgileFlow Core', 'Core business suite for AgileFlow', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000109-0000-0000-0000-000000000001', '00000009-0000-0000-0000-000000000001', 'NEXTGEN_CORE', 'NextGenTech Core', 'Core business suite for NextGenTech', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('0000010a-0000-0000-0000-000000000001', '0000000a-0000-0000-0000-000000000001', 'QUANTUM_CORE', 'QuantumLabs Core', 'Core business suite for QuantumLabs', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001');

-- =============================================================================
-- 8. SYSTEM SUITE MODULES (for each suite)
-- =============================================================================
INSERT INTO SystemSuiteModules (Id, SystemSuiteId, Code, Name, Description, StatusId, SortOrder, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan)
VALUES
-- TechnoCorp Modules
('00000201-0000-0000-0000-000000000001', '00000101-0000-0000-0000-000000000001', 'TENANT_MGMT', 'Tenant Management', 'Manage tenant settings and configuration', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000201-0000-0000-0000-000000000002', '00000101-0000-0000-0000-000000000001', 'USER_MGMT', 'User Management', 'Manage users and accounts', 1, 2, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000201-0000-0000-0000-000000000003', '00000101-0000-0000-0000-000000000001', 'ROLE_MGMT', 'Role Management', 'Manage roles and permissions', 1, 3, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000201-0000-0000-0000-000000000004', '00000101-0000-0000-0000-000000000001', 'PROFILE_MGMT', 'Profile Management', 'Manage profiles and access', 1, 4, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000201-0000-0000-0000-000000000005', '00000101-0000-0000-0000-000000000001', 'BRANCH_MGMT', 'Branch Management', 'Manage branches and locations', 1, 5, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- LogisticaGlobal Modules
('00000202-0000-0000-0000-000000000001', '00000102-0000-0000-0000-000000000001', 'TENANT_MGMT', 'Tenant Management', 'Manage tenant settings', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000202-0000-0000-0000-000000000002', '00000102-0000-0000-0000-000000000001', 'USER_MGMT', 'User Management', 'Manage users', 1, 2, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000202-0000-0000-0000-000000000003', '00000102-0000-0000-0000-000000000001', 'ROLE_MGMT', 'Role Management', 'Manage roles', 1, 3, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000202-0000-0000-0000-000000000004', '00000102-0000-0000-0000-000000000001', 'PROFILE_MGMT', 'Profile Management', 'Manage profiles', 1, 4, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000202-0000-0000-0000-000000000005', '00000102-0000-0000-0000-000000000001', 'BRANCH_MGMT', 'Branch Management', 'Manage branches', 1, 5, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- RetailMax Modules
('00000203-0000-0000-0000-000000000001', '00000103-0000-0000-0000-000000000001', 'TENANT_MGMT', 'Tenant Management', 'Manage tenant settings', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000203-0000-0000-0000-000000000002', '00000103-0000-0000-0000-000000000001', 'USER_MGMT', 'User Management', 'Manage users', 1, 2, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000203-0000-0000-0000-000000000003', '00000103-0000-0000-0000-000000000001', 'ROLE_MGMT', 'Role Management', 'Manage roles', 1, 3, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000203-0000-0000-0000-000000000004', '00000103-0000-0000-0000-000000000001', 'PROFILE_MGMT', 'Profile Management', 'Manage profiles', 1, 4, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000203-0000-0000-0000-000000000005', '00000103-0000-0000-0000-000000000001', 'BRANCH_MGMT', 'Branch Management', 'Manage branches', 1, 5, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- SaludTotal Modules
('00000204-0000-0000-0000-000000000001', '00000104-0000-0000-0000-000000000001', 'TENANT_MGMT', 'Tenant Management', 'Manage tenant settings', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000204-0000-0000-0000-000000000002', '00000104-0000-0000-0000-000000000001', 'USER_MGMT', 'User Management', 'Manage users', 1, 2, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000204-0000-0000-0000-000000000003', '00000104-0000-0000-0000-000000000001', 'ROLE_MGMT', 'Role Management', 'Manage roles', 1, 3, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000204-0000-0000-0000-000000000004', '00000104-0000-0000-0000-000000000001', 'PROFILE_MGMT', 'Profile Management', 'Manage profiles', 1, 4, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000204-0000-0000-0000-000000000005', '00000104-0000-0000-0000-000000000001', 'BRANCH_MGMT', 'Branch Management', 'Manage branches', 1, 5, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- EduLearn Modules
('00000205-0000-0000-0000-000000000001', '00000105-0000-0000-0000-000000000001', 'TENANT_MGMT', 'Tenant Management', 'Manage tenant settings', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000205-0000-0000-0000-000000000002', '00000105-0000-0000-0000-000000000001', 'USER_MGMT', 'User Management', 'Manage users', 1, 2, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000205-0000-0000-0000-000000000003', '00000105-0000-0000-0000-000000000001', 'ROLE_MGMT', 'Role Management', 'Manage roles', 1, 3, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000205-0000-0000-0000-000000000004', '00000105-0000-0000-0000-000000000001', 'PROFILE_MGMT', 'Profile Management', 'Manage profiles', 1, 4, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000205-0000-0000-0000-000000000005', '00000105-0000-0000-0000-000000000001', 'BRANCH_MGMT', 'Branch Management', 'Manage branches', 1, 5, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- FinancePro Modules
('00000206-0000-0000-0000-000000000001', '00000106-0000-0000-0000-000000000001', 'TENANT_MGMT', 'Tenant Management', 'Manage tenant settings', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000206-0000-0000-0000-000000000002', '00000106-0000-0000-0000-000000000001', 'USER_MGMT', 'User Management', 'Manage users', 1, 2, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000206-0000-0000-0000-000000000003', '00000106-0000-0000-0000-000000000001', 'ROLE_MGMT', 'Role Management', 'Manage roles', 1, 3, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000206-0000-0000-0000-000000000004', '00000106-0000-0000-0000-000000000001', 'PROFILE_MGMT', 'Profile Management', 'Manage profiles', 1, 4, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000206-0000-0000-0000-000000000005', '00000106-0000-0000-0000-000000000001', 'BRANCH_MGMT', 'Branch Management', 'Manage branches', 1, 5, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- MediaHub Modules
('00000207-0000-0000-0000-000000000001', '00000107-0000-0000-0000-000000000001', 'TENANT_MGMT', 'Tenant Management', 'Manage tenant settings', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000207-0000-0000-0000-000000000002', '00000107-0000-0000-0000-000000000001', 'USER_MGMT', 'User Management', 'Manage users', 1, 2, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000207-0000-0000-0000-000000000003', '00000107-0000-0000-0000-000000000001', 'ROLE_MGMT', 'Role Management', 'Manage roles', 1, 3, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000207-0000-0000-0000-000000000004', '00000107-0000-0000-0000-000000000001', 'PROFILE_MGMT', 'Profile Management', 'Manage profiles', 1, 4, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000207-0000-0000-0000-000000000005', '00000107-0000-0000-0000-000000000001', 'BRANCH_MGMT', 'Branch Management', 'Manage branches', 1, 5, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- AgileFlow Modules
('00000208-0000-0000-0000-000000000001', '00000108-0000-0000-0000-000000000001', 'TENANT_MGMT', 'Tenant Management', 'Manage tenant settings', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000208-0000-0000-0000-000000000002', '00000108-0000-0000-0000-000000000001', 'USER_MGMT', 'User Management', 'Manage users', 1, 2, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000208-0000-0000-0000-000000000003', '00000108-0000-0000-0000-000000000001', 'ROLE_MGMT', 'Role Management', 'Manage roles', 1, 3, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000208-0000-0000-0000-000000000004', '00000108-0000-0000-0000-000000000001', 'PROFILE_MGMT', 'Profile Management', 'Manage profiles', 1, 4, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000208-0000-0000-0000-000000000005', '00000108-0000-0000-0000-000000000001', 'BRANCH_MGMT', 'Branch Management', 'Manage branches', 1, 5, '00000000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- NextGenTech Modules
('00000209-0000-0000-0000-000000000001', '00000109-0000-0000-0000-000000000001', 'TENANT_MGMT', 'Tenant Management', 'Manage tenant settings', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000209-0000-0000-0000-000000000002', '00000109-0000-0000-0000-000000000001', 'USER_MGMT', 'User Management', 'Manage users', 1, 2, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000209-0000-0000-0000-000000000003', '00000109-0000-0000-0000-000000000001', 'ROLE_MGMT', 'Role Management', 'Manage roles', 1, 3, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000209-0000-0000-0000-000000000004', '00000109-0000-0000-0000-000000000001', 'PROFILE_MGMT', 'Profile Management', 'Manage profiles', 1, 4, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000209-0000-0000-0000-000000000005', '00000109-0000-0000-0000-000000000001', 'BRANCH_MGMT', 'Branch Management', 'Manage branches', 1, 5, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- QuantumLabs Modules
('0000020a-0000-0000-0000-000000000001', '0000010a-0000-0000-0000-000000000001', 'TENANT_MGMT', 'Tenant Management', 'Manage tenant settings', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('0000020a-0000-0000-0000-000000000002', '0000010a-0000-0000-0000-000000000001', 'USER_MGMT', 'User Management', 'Manage users', 1, 2, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('0000020a-0000-0000-0000-000000000003', '0000010a-0000-0000-0000-000000000001', 'ROLE_MGMT', 'Role Management', 'Manage roles', 1, 3, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('0000020a-0000-0000-0000-000000000004', '0000010a-0000-0000-0000-000000000001', 'PROFILE_MGMT', 'Profile Management', 'Manage profiles', 1, 4, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('0000020a-0000-0000-0000-000000000005', '0000010a-0000-0000-0000-000000000001', 'BRANCH_MGMT', 'Branch Management', 'Manage branches', 1, 5, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S');

-- =============================================================================
-- 9. SYSTEM SUITE MENUS
-- =============================================================================
INSERT INTO SystemSuiteMenus (Id, ModuleId, Code, Label, Description, SortOrder, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan)
VALUES
-- For TechnoCorp (module 00000201-0000-0000-0000-000000000001)
('00000301-0000-0000-0000-000000000001', '00000201-0000-0000-0000-000000000001', 'TENANTS', 'Tenants', 'Tenant management menu', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000301-0000-0000-0000-000000000002', '00000201-0000-0000-0000-000000000002', 'USERS', 'Users', 'User management menu', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000301-0000-0000-0000-000000000003', '00000201-0000-0000-0000-000000000003', 'ROLES', 'Roles', 'Role management menu', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000301-0000-0000-0000-000000000004', '00000201-0000-0000-0000-000000000004', 'PROFILES', 'Profiles', 'Profile management menu', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000301-0000-0000-0000-000000000005', '00000201-0000-0000-0000-000000000005', 'BRANCHES', 'Branches', 'Branch management menu', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S');

-- Note: For brevity, we'll use same menu IDs for all tenants (they're independent due to module scoping)

-- =============================================================================
-- 10. ROLES (Admin and User roles per tenant)
-- =============================================================================
INSERT INTO Roles (Id, TenantId, SystemSuiteId, ParentRoleId, Code, Value, Description, HierarchyLevel, PromotionOrder, IsActive, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan, RowVersion)
VALUES
-- TECHNO roles
('00000401-0000-0000-0000-000000000001', '00000001-0000-0000-0000-000000000001', '00000101-0000-0000-0000-000000000001', NULL, 'TECHNO_ADMIN', 'TechnoCorp Administrator', 'Full administration access for TechnoCorp', 0, 0, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000401-0000-0000-0000-000000000002', '00000001-0000-0000-0000-000000000001', '00000101-0000-0000-0000-000000000001', '00000401-0000-0000-0000-000000000001', 'TECHNO_USER', 'TechnoCorp User', 'Standard user access for TechnoCorp', 1, 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- LOGISTICA roles
('00000402-0000-0000-0000-000000000001', '00000002-0000-0000-0000-000000000001', '00000102-0000-0000-0000-000000000001', NULL, 'LOGISTICA_ADMIN', 'LogisticaGlobal Administrator', 'Full administration access for LogisticaGlobal', 0, 0, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000402-0000-0000-0000-000000000002', '00000002-0000-0000-0000-000000000001', '00000102-0000-0000-0000-000000000001', '00000402-0000-0000-0000-000000000001', 'LOGISTICA_USER', 'LogisticaGlobal User', 'Standard user access for LogisticaGlobal', 1, 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- RETAIL roles
('00000403-0000-0000-0000-000000000001', '00000003-0000-0000-0000-000000000001', '00000103-0000-0000-0000-000000000001', NULL, 'RETAIL_ADMIN', 'RetailMax Administrator', 'Full administration access for RetailMax', 0, 0, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000403-0000-0000-0000-000000000002', '00000003-0000-0000-0000-000000000001', '00000103-0000-0000-0000-000000000001', '00000403-0000-0000-0000-000000000001', 'RETAIL_USER', 'RetailMax User', 'Standard user access for RetailMax', 1, 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- SALUD roles
('00000404-0000-0000-0000-000000000001', '00000004-0000-0000-0000-000000000001', '00000104-0000-0000-0000-000000000001', NULL, 'SALUD_ADMIN', 'SaludTotal Administrator', 'Full administration access for SaludTotal', 0, 0, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000404-0000-0000-0000-000000000002', '00000004-0000-0000-0000-000000000001', '00000104-0000-0000-0000-000000000001', '00000404-0000-0000-0000-000000000001', 'SALUD_USER', 'SaludTotal User', 'Standard user access for SaludTotal', 1, 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- EDU roles
('00000405-0000-0000-0000-000000000001', '00000005-0000-0000-0000-000000000001', '00000105-0000-0000-0000-000000000001', NULL, 'EDU_ADMIN', 'EduLearn Administrator', 'Full administration access for EduLearn', 0, 0, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000405-0000-0000-0000-000000000002', '00000005-0000-0000-0000-000000000001', '00000105-0000-0000-0000-000000000001', '00000405-0000-0000-0000-000000000001', 'EDU_USER', 'EduLearn User', 'Standard user access for EduLearn', 1, 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- FINANCE roles
('00000406-0000-0000-0000-000000000001', '00000006-0000-0000-0000-000000000001', '00000106-0000-0000-0000-000000000001', NULL, 'FINANCE_ADMIN', 'FinancePro Administrator', 'Full administration access for FinancePro', 0, 0, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000406-0000-0000-0000-000000000002', '00000006-0000-0000-0000-000000000001', '00000106-0000-0000-0000-000000000001', '00000406-0000-0000-0000-000000000001', 'FINANCE_USER', 'FinancePro User', 'Standard user access for FinancePro', 1, 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- MEDIA roles
('00000407-0000-0000-0000-000000000001', '00000007-0000-0000-0000-000000000001', '00000107-0000-0000-0000-000000000001', NULL, 'MEDIA_ADMIN', 'MediaHub Administrator', 'Full administration access for MediaHub', 0, 0, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000407-0000-0000-0000-000000000002', '00000007-0000-0000-0000-000000000001', '00000107-0000-0000-0000-000000000001', '00000407-0000-0000-0000-000000000001', 'MEDIA_USER', 'MediaHub User', 'Standard user access for MediaHub', 1, 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- AGILE roles
('00000408-0000-0000-0000-000000000001', '00000008-0000-0000-0000-000000000001', '00000108-0000-0000-0000-000000000001', NULL, 'AGILE_ADMIN', 'AgileFlow Administrator', 'Full administration access for AgileFlow', 0, 0, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000408-0000-0000-0000-000000000002', '00000008-0000-0000-0000-000000000001', '00000108-0000-0000-0000-000000000001', '00000408-0000-0000-0000-000000000001', 'AGILE_USER', 'AgileFlow User', 'Standard user access for AgileFlow', 1, 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- NEXTGEN roles
('00000409-0000-0000-0000-000000000001', '00000009-0000-0000-0000-000000000001', '00000109-0000-0000-0000-000000000001', NULL, 'NEXTGEN_ADMIN', 'NextGenTech Administrator', 'Full administration access for NextGenTech', 0, 0, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000409-0000-0000-0000-000000000002', '00000009-0000-0000-0000-000000000001', '00000109-0000-0000-0000-000000000001', '00000409-0000-0000-0000-000000000001', 'NEXTGEN_USER', 'NextGenTech User', 'Standard user access for NextGenTech', 1, 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- QUANTUM roles
('0000040a-0000-0000-0000-000000000001', '0000000a-0000-0000-0000-000000000001', '0000010a-0000-0000-0000-000000000001', NULL, 'QUANTUM_ADMIN', 'QuantumLabs Administrator', 'Full administration access for QuantumLabs', 0, 0, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('0000040a-0000-0000-0000-000000000002', '0000000a-0000-0000-0000-000000000001', '0000010a-0000-0000-0000-000000000001', '0000040a-0000-0000-0000-000000000001', 'QUANTUM_USER', 'QuantumLabs User', 'Standard user access for QuantumLabs', 1, 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001');

-- =============================================================================
-- 11. PERMISSION TEMPLATES
-- =============================================================================
INSERT INTO PermissionTemplates (Id, TenantId, RoleId, SystemSuiteId, Version, StatusId, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan, RowVersion)
VALUES
-- TECHNO templates
('00000501-0000-0000-0000-000000000001', '00000001-0000-0000-0000-000000000001', '00000401-0000-0000-0000-000000000001', '00000101-0000-0000-0000-000000000001', '1.0', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000501-0000-0000-0000-000000000002', '00000001-0000-0000-0000-000000000001', '00000401-0000-0000-0000-000000000002', '00000101-0000-0000-0000-000000000001', '1.0', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- LOGISTICA templates
('00000502-0000-0000-0000-000000000001', '00000002-0000-0000-0000-000000000001', '00000402-0000-0000-0000-000000000001', '00000102-0000-0000-0000-000000000001', '1.0', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000502-0000-0000-0000-000000000002', '00000002-0000-0000-0000-000000000001', '00000402-0000-0000-0000-000000000002', '00000102-0000-0000-0000-000000000001', '1.0', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- RETAIL templates
('00000503-0000-0000-0000-000000000001', '00000003-0000-0000-0000-000000000001', '00000403-0000-0000-0000-000000000001', '00000103-0000-0000-0000-000000000001', '1.0', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000503-0000-0000-0000-000000000002', '00000003-0000-0000-0000-000000000001', '00000403-0000-0000-0000-000000000002', '00000103-0000-0000-0000-000000000001', '1.0', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- SALUD templates
('00000504-0000-0000-0000-000000000001', '00000004-0000-0000-0000-000000000001', '00000404-0000-0000-0000-000000000001', '00000104-0000-0000-0000-000000000001', '1.0', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000504-0000-0000-0000-000000000002', '00000004-0000-0000-0000-000000000001', '00000404-0000-0000-0000-000000000002', '00000104-0000-0000-0000-000000000001', '1.0', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- EDU templates
('00000505-0000-0000-0000-000000000001', '00000005-0000-0000-0000-000000000001', '00000405-0000-0000-0000-000000000001', '00000105-0000-0000-0000-000000000001', '1.0', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000505-0000-0000-0000-000000000002', '00000005-0000-0000-0000-000000000001', '00000405-0000-0000-0000-000000000002', '00000105-0000-0000-0000-000000000001', '1.0', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- FINANCE templates
('00000506-0000-0000-0000-000000000001', '00000006-0000-0000-0000-000000000001', '00000406-0000-0000-0000-000000000001', '00000106-0000-0000-0000-000000000001', '1.0', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000506-0000-0000-0000-000000000002', '00000006-0000-0000-0000-000000000001', '00000406-0000-0000-0000-000000000002', '00000106-0000-0000-0000-000000000001', '1.0', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- MEDIA templates
('00000507-0000-0000-0000-000000000001', '00000007-0000-0000-0000-000000000001', '00000407-0000-0000-0000-000000000001', '00000107-0000-0000-0000-000000000001', '1.0', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000507-0000-0000-0000-000000000002', '00000007-0000-0000-0000-000000000001', '00000407-0000-0000-0000-000000000002', '00000107-0000-0000-0000-000000000001', '1.0', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- AGILE templates
('00000508-0000-0000-0000-000000000001', '00000008-0000-0000-0000-000000000001', '00000408-0000-0000-0000-000000000001', '00000108-0000-0000-0000-000000000001', '1.0', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000508-0000-0000-0000-000000000002', '00000008-0000-0000-0000-000000000001', '00000408-0000-0000-0000-000000000002', '00000108-0000-0000-0000-000000000001', '1.0', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- NEXTGEN templates
('00000509-0000-0000-0000-000000000001', '00000009-0000-0000-0000-000000000001', '00000409-0000-0000-0000-000000000001', '00000109-0000-0000-0000-000000000001', '1.0', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000509-0000-0000-0000-000000000002', '00000009-0000-0000-0000-000000000001', '00000409-0000-0000-0000-000000000002', '00000109-0000-0000-0000-000000000001', '1.0', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- QUANTUM templates
('0000050a-0000-0000-0000-000000000001', '0000000a-0000-0000-0000-000000000001', '0000040a-0000-0000-0000-000000000001', '0000010a-0000-0000-0000-000000000001', '1.0', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('0000050a-0000-0000-0000-000000000002', '0000000a-0000-0000-0000-000000000001', '0000040a-0000-0000-0000-000000000002', '0000010a-0000-0000-0000-000000000001', '1.0', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001');

-- =============================================================================
-- 12. USER ACCOUNTS (one admin + one user per tenant)
-- =============================================================================
INSERT INTO UserAccounts (Id, TenantId, BranchId, Email, CategoryId, StatusId, IdentityReference, IdentityReferenceTypeId, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan, RowVersion, IsDeleted, DeletedAtUtc, DeletedBy, AnonymizedAtUtc)
VALUES
-- TECHNO users
('00000601-0000-0000-0000-000000000001', '00000001-0000-0000-0000-000000000001', '00000011-0000-0000-0000-000000000001', 'admin@techno.io', 2, 2, 'admin@techno.io', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL, NULL),
('00000601-0000-0000-0000-000000000002', '00000001-0000-0000-0000-000000000001', '00000011-0000-0000-0000-000000000001', 'user@techno.io', 1, 2, 'user@techno.io', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL, NULL),
-- LOGISTICA users
('00000602-0000-0000-0000-000000000001', '00000002-0000-0000-0000-000000000001', '00000012-0000-0000-0000-000000000001', 'admin@logistica.io', 2, 2, 'admin@logistica.io', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL, NULL),
('00000602-0000-0000-0000-000000000002', '00000002-0000-0000-0000-000000000001', '00000012-0000-0000-0000-000000000001', 'user@logistica.io', 1, 2, 'user@logistica.io', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL, NULL),
-- RETAIL users
('00000603-0000-0000-0000-000000000001', '00000003-0000-0000-0000-000000000001', '00000013-0000-0000-0000-000000000001', 'admin@retail.io', 2, 2, 'admin@retail.io', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL, NULL),
('00000603-0000-0000-0000-000000000002', '00000003-0000-0000-0000-000000000001', '00000013-0000-0000-0000-000000000001', 'user@retail.io', 1, 2, 'user@retail.io', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL, NULL),
-- SALUD users
('00000604-0000-0000-0000-000000000001', '00000004-0000-0000-0000-000000000001', '00000014-0000-0000-0000-000000000001', 'admin@salud.io', 2, 2, 'admin@salud.io', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL, NULL),
('00000604-0000-0000-0000-000000000002', '00000004-0000-0000-0000-000000000001', '00000014-0000-0000-0000-000000000001', 'user@salud.io', 1, 2, 'user@salud.io', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL, NULL),
-- EDU users
('00000605-0000-0000-0000-000000000001', '00000005-0000-0000-0000-000000000001', '00000015-0000-0000-0000-000000000001', 'admin@edu.io', 2, 2, 'admin@edu.io', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL, NULL),
('00000605-0000-0000-0000-000000000002', '00000005-0000-0000-0000-000000000001', '00000015-0000-0000-0000-000000000001', 'user@edu.io', 1, 2, 'user@edu.io', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL, NULL),
-- FINANCE users
('00000606-0000-0000-0000-000000000001', '00000006-0000-0000-0000-000000000001', '00000016-0000-0000-0000-000000000001', 'admin@finance.io', 2, 2, 'admin@finance.io', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL, NULL),
('00000606-0000-0000-0000-000000000002', '00000006-0000-0000-0000-000000000001', '00000016-0000-0000-0000-000000000001', 'user@finance.io', 1, 2, 'user@finance.io', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL, NULL),
-- MEDIA users
('00000607-0000-0000-0000-000000000001', '00000007-0000-0000-0000-000000000001', '00000017-0000-0000-0000-000000000001', 'admin@media.io', 2, 2, 'admin@media.io', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL, NULL),
('00000607-0000-0000-0000-000000000002', '00000007-0000-0000-0000-000000000001', '00000017-0000-0000-0000-000000000001', 'user@media.io', 1, 2, 'user@media.io', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL, NULL),
-- AGILE users
('00000608-0000-0000-0000-000000000001', '00000008-0000-0000-0000-000000000001', '00000018-0000-0000-0000-000000000001', 'admin@agile.io', 2, 2, 'admin@agile.io', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL, NULL),
('00000608-0000-0000-0000-000000000002', '00000008-0000-0000-0000-000000000001', '00000018-0000-0000-0000-000000000001', 'user@agile.io', 1, 2, 'user@agile.io', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL, NULL),
-- NEXTGEN users
('00000609-0000-0000-0000-000000000001', '00000009-0000-0000-0000-000000000001', '00000019-0000-0000-0000-000000000001', 'admin@nextgen.io', 2, 2, 'admin@nextgen.io', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL, NULL),
('00000609-0000-0000-0000-000000000002', '00000009-0000-0000-0000-000000000001', '00000019-0000-0000-0000-000000000001', 'user@nextgen.io', 1, 2, 'user@nextgen.io', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL, NULL),
-- QUANTUM users
('0000060a-0000-0000-0000-000000000001', '0000000a-0000-0000-0000-000000000001', '0000001a-0000-0000-0000-000000000001', 'admin@quantum.io', 2, 2, 'admin@quantum.io', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL, NULL),
('0000060a-0000-0000-0000-000000000002', '0000000a-0000-0000-0000-000000000001', '0000001a-0000-0000-0000-000000000001', 'user@quantum.io', 1, 2, 'user@quantum.io', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001', 0, NULL, NULL, NULL);

-- =============================================================================
-- 13. PASSWORD CREDENTIALS
-- =============================================================================
INSERT INTO UserAccountPasswordCredentials (Id, UserAccountId, PasswordHash, IsActive, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan)
VALUES
-- TECHNO passwords
('00000701-0000-0000-0000-000000000001', '00000601-0000-0000-0000-000000000001', '$2a$11$vWzJmzYumeBGlF0E7LschORL5Ec7j6gWr2.zeOnnoEMWtWKxlo6pS', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000701-0000-0000-0000-000000000002', '00000601-0000-0000-0000-000000000002', '$2a$11$vWzJmzYumeBGlF0E7LschORL5Ec7j6gWr2.zeOnnoEMWtWKxlo6pS', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- LOGISTICA passwords
('00000702-0000-0000-0000-000000000001', '00000602-0000-0000-0000-000000000001', '$2a$11$JZrsSco/8jnqjEI2.RBzMuxh62s7PSNOdW1u.JWngZfyTRV7GfWLS', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000702-0000-0000-0000-000000000002', '00000602-0000-0000-0000-000000000002', '$2a$11$JZrsSco/8jnqjEI2.RBzMuxh62s7PSNOdW1u.JWngZfyTRV7GfWLS', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- RETAIL passwords
('00000703-0000-0000-0000-000000000001', '00000603-0000-0000-0000-000000000001', '$2a$11$QLHU1TK6LvRHH29ltZk7nOupdtDmUs.Ytb.gdijzAwPzXgWdjsmVi', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000703-0000-0000-0000-000000000002', '00000603-0000-0000-0000-000000000002', '$2a$11$QLHU1TK6LvRHH29ltZk7nOupdtDmUs.Ytb.gdijzAwPzXgWdjsmVi', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- SALUD passwords
('00000704-0000-0000-0000-000000000001', '00000604-0000-0000-0000-000000000001', '$2a$11$dcwt..vieyEN0gx66Zhm0u/adhKKKwj4eqRl3TgOTA5pvpwfhWnji', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000704-0000-0000-0000-000000000002', '00000604-0000-0000-0000-000000000002', '$2a$11$dcwt..vieyEN0gx66Zhm0u/adhKKKwj4eqRl3TgOTA5pvpwfhWnji', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- EDU passwords
('00000705-0000-0000-0000-000000000001', '00000605-0000-0000-0000-000000000001', '$2a$11$0/yktykdidAXsGdgwa7WmO0Kcm06bDYm.duZRfAg12VKi9/abjoga', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000705-0000-0000-0000-000000000002', '00000605-0000-0000-0000-000000000002', '$2a$11$0/yktykdidAXsGdgwa7WmO0Kcm06bDYm.duZRfAg12VKi9/abjoga', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- FINANCE passwords
('00000706-0000-0000-0000-000000000001', '00000606-0000-0000-0000-000000000001', '$2a$11$WMyYZSeghTIDdiLdJNmA0ut/hUxDqHlkjHMpQ2fpKukOnxY6SRAjm', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000706-0000-0000-0000-000000000002', '00000606-0000-0000-0000-000000000002', '$2a$11$WMyYZSeghTIDdiLdJNmA0ut/hUxDqHlkjHMpQ2fpKukOnxY6SRAjm', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- MEDIA passwords
('00000707-0000-0000-0000-000000000001', '00000607-0000-0000-0000-000000000001', '$2a$11$zuHS9keunUcIDUKOb4t7xerEW2sll8NYjmC5mbwVKVBmJz0y0Yl8u', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000707-0000-0000-0000-000000000002', '00000607-0000-0000-0000-000000000002', '$2a$11$zuHS9keunUcIDUKOb4t7xerEW2sll8NYjmC5mbwVKVBmJz0y0Yl8u', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- AGILE passwords
('00000708-0000-0000-0000-000000000001', '00000608-0000-0000-0000-000000000001', '$2a$11$msZW1j2iPPzwgdbJHfs8buayVJgpG3XJL8dFnMnoKglifHXCWNBRq', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000708-0000-0000-0000-000000000002', '00000608-0000-0000-0000-000000000002', '$2a$11$msZW1j2iPPzwgdbJHfs8buayVJgpG3XJL8dFnMnoKglifHXCWNBRq', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- NEXTGEN passwords
('00000709-0000-0000-0000-000000000001', '00000609-0000-0000-0000-000000000001', '$2a$11$Gfqf2.DDE6htHB7cBZCvWObhaz34Ez9EBhQTFa36xM5qN9Cq9II8K', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('00000709-0000-0000-0000-000000000002', '00000609-0000-0000-0000-000000000002', '$2a$11$Gfqf2.DDE6htHB7cBZCvWObhaz34Ez9EBhQTFa36xM5qN9Cq9II8K', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
-- QUANTUM passwords
('0000070a-0000-0000-0000-000000000001', '0000060a-0000-0000-0000-000000000001', '$2a$11$2TcLj6zmixAPAbu9LXMgPOWojftK31M2WT7F6c0vvlY30rGPQBDR2', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S'),
('0000070a-0000-0000-0000-000000000002', '0000060a-0000-0000-0000-000000000002', '$2a$11$2TcLj6zmixAPAbu9LXMgPOWojftK31M2WT7F6c0vvlY30rGPQBDR2', 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S');

-- =============================================================================
-- 14. PROFILES
-- =============================================================================
INSERT INTO Profiles (Id, TenantId, UserId, RoleId, BranchId, ScopeId, IsActive, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc, AuditTimeSpan, RowVersion)
VALUES
-- TECHNO profiles
('00000801-0000-0000-0000-000000000001', '00000001-0000-0000-0000-000000000001', '00000601-0000-0000-0000-000000000001', '00000401-0000-0000-0000-000000000001', '00000011-0000-0000-0000-000000000001', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000801-0000-0000-0000-000000000002', '00000001-0000-0000-0000-000000000001', '00000601-0000-0000-0000-000000000002', '00000401-0000-0000-0000-000000000002', '00000011-0000-0000-0000-000000000001', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- LOGISTICA profiles
('00000802-0000-0000-0000-000000000001', '00000002-0000-0000-0000-000000000001', '00000602-0000-0000-0000-000000000001', '00000402-0000-0000-0000-000000000001', '00000012-0000-0000-0000-000000000001', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000802-0000-0000-0000-000000000002', '00000002-0000-0000-0000-000000000001', '00000602-0000-0000-0000-000000000002', '00000402-0000-0000-0000-000000000002', '00000012-0000-0000-0000-000000000001', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- RETAIL profiles
('00000803-0000-0000-0000-000000000001', '00000003-0000-0000-0000-000000000001', '00000603-0000-0000-0000-000000000001', '00000403-0000-0000-0000-000000000001', '00000013-0000-0000-0000-000000000001', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000803-0000-0000-0000-000000000002', '00000003-0000-0000-0000-000000000001', '00000603-0000-0000-0000-000000000002', '00000403-0000-0000-0000-000000000002', '00000013-0000-0000-0000-000000000001', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- SALUD profiles
('00000804-0000-0000-0000-000000000001', '00000004-0000-0000-0000-000000000001', '00000604-0000-0000-0000-000000000001', '00000404-0000-0000-0000-000000000001', '00000014-0000-0000-0000-000000000001', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000804-0000-0000-0000-000000000002', '00000004-0000-0000-0000-000000000001', '00000604-0000-0000-0000-000000000002', '00000404-0000-0000-0000-000000000002', '00000014-0000-0000-0000-000000000001', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- EDU profiles
('00000805-0000-0000-0000-000000000001', '00000005-0000-0000-0000-000000000001', '00000605-0000-0000-0000-000000000001', '00000405-0000-0000-0000-000000000001', '00000015-0000-0000-0000-000000000001', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000805-0000-0000-0000-000000000002', '00000005-0000-0000-0000-000000000001', '00000605-0000-0000-0000-000000000002', '00000405-0000-0000-0000-000000000002', '00000015-0000-0000-0000-000000000001', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- FINANCE profiles
('00000806-0000-0000-0000-000000000001', '00000006-0000-0000-0000-000000000001', '00000606-0000-0000-0000-000000000001', '00000406-0000-0000-0000-000000000001', '00000016-0000-0000-0000-000000000001', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000806-0000-0000-0000-000000000002', '00000006-0000-0000-0000-000000000001', '00000606-0000-0000-0000-000000000002', '00000406-0000-0000-0000-000000000002', '00000016-0000-0000-0000-000000000001', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- MEDIA profiles
('00000807-0000-0000-0000-000000000001', '00000007-0000-0000-0000-000000000001', '00000607-0000-0000-0000-000000000001', '00000407-0000-0000-0000-000000000001', '00000017-0000-0000-0000-000000000001', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000807-0000-0000-0000-000000000002', '00000007-0000-0000-0000-000000000001', '00000607-0000-0000-0000-000000000002', '00000407-0000-0000-0000-000000000002', '00000017-0000-0000-0000-000000000001', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- AGILE profiles
('00000808-0000-0000-0000-000000000001', '00000008-0000-0000-0000-000000000001', '00000608-0000-0000-0000-000000000001', '00000408-0000-0000-0000-000000000001', '00000018-0000-0000-0000-000000000001', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000808-0000-0000-0000-000000000002', '00000008-0000-0000-0000-000000000001', '00000608-0000-0000-0000-000000000002', '00000408-0000-0000-0000-000000000002', '00000018-0000-0000-0000-000000000001', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- NEXTGEN profiles
('00000809-0000-0000-0000-000000000001', '00000009-0000-0000-0000-000000000001', '00000609-0000-0000-0000-000000000001', '00000409-0000-0000-0000-000000000001', '00000019-0000-0000-0000-000000000001', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('00000809-0000-0000-0000-000000000002', '00000009-0000-0000-0000-000000000001', '00000609-0000-0000-0000-000000000002', '00000409-0000-0000-0000-000000000002', '00000019-0000-0000-0000-000000000001', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
-- QUANTUM profiles
('0000080a-0000-0000-0000-000000000001', '0000000a-0000-0000-0000-000000000001', '0000060a-0000-0000-0000-000000000001', '0000040a-0000-0000-0000-000000000001', '0000001a-0000-0000-0000-000000000001', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001'),
('0000080a-0000-0000-0000-000000000002', '0000000a-0000-0000-0000-000000000001', '0000060a-0000-0000-0000-000000000002', '0000040a-0000-0000-0000-000000000002', '0000001a-0000-0000-0000-000000000001', 1, 1, '00000000-0000-0000-0000-000000000001', datetime('now'), NULL, NULL, 'PT0S', X'0000000000000001');

SELECT 'Seed completed successfully' as result;
