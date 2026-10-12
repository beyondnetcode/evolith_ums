import type {
  AccessEffect,
  ActionRef,
  AuthorizationGraph,
  DomainResourcePermissions,
  IdpProviderRef,
  MenuModule,
  PermissionSource
} from '@evolith-ums/ums-sdk-contracts';
import { SchemaVersion } from '@evolith-ums/ums-sdk-contracts';

type FlagCriteria =
  | 'TenantId'
  | 'BranchId'
  | 'UserProfileId'
  | 'RoleCode'
  | 'Environment'
  | 'DateRange'
  | 'PercentageHash'
  | 'CustomRule';

interface MenuOptionSpec {
  optionCode: string;
  actionCode: string;
  effect: AccessEffect;
  source: PermissionSource;
}

interface DomainPermSpec {
  resource: string;
  action: string;
  effect: AccessEffect;
  source: PermissionSource;
}

interface FlagSpec {
  flagCode: string;
  enabled: boolean;
  matchedCriteriaType: FlagCriteria | null;
}

/**
 * Fluent builder for fabricating valid {@link AuthorizationGraph} instances in tests.
 * Same surface as the .NET `AuthGraphBuilder` — produces graphs without JSON parsing,
 * HTTP, or UMS connectivity.
 */
export class AuthGraphBuilder {
  private schemaVersion: string = SchemaVersion.Current;
  private tenantCode: string = 'TEST_TENANT';
  private tenantName: string = 'Test Tenant';
  private userEmail: string = 'test.user@example.com';
  private systemSuiteCode: string = 'TEST_SUITE';
  private roleCode: string = 'TEST_ROLE';
  private profileScope: 'OrgWide' | 'BranchScoped' = 'OrgWide';
  private branchCode: string | null = null;
  private authMethod: 'Local' | 'IDP' = 'Local';
  private provider: IdpProviderRef | null = null;
  private validityMs: number = 60 * 60 * 1000;
  private readonly scopes: string[] = [];
  private readonly menuOptions: MenuOptionSpec[] = [];
  private readonly domainPerms: DomainPermSpec[] = [];
  private readonly flags: FlagSpec[] = [];

  static forTenant(code: string): AuthGraphBuilder {
    const b = new AuthGraphBuilder();
    b.tenantCode = code;
    return b;
  }

  withTenantName(name: string): this {
    this.tenantName = name;
    return this;
  }

  withUser(email: string): this {
    this.userEmail = email;
    return this;
  }

  withSystemSuite(code: string): this {
    this.systemSuiteCode = code;
    return this;
  }

  withRole(code: string): this {
    this.roleCode = code;
    return this;
  }

  withBranchScopedProfile(branchCode: string): this {
    this.profileScope = 'BranchScoped';
    this.branchCode = branchCode;
    return this;
  }

  withSchemaVersion(version: string): this {
    this.schemaVersion = version;
    return this;
  }

  withValidity(ms: number): this {
    this.validityMs = ms;
    return this;
  }

  withIdpAuth(providerName: string, providerCode: string, strategy: string): this {
    this.authMethod = 'IDP';
    this.provider = { code: providerCode, name: providerName, value: strategy };
    return this;
  }

  withScope(scope: string): this {
    this.scopes.push(scope);
    return this;
  }

  withDeny(scope: string): this {
    const idx = scope.indexOf('.');
    if (idx <= 0 || idx === scope.length - 1) {
      throw new Error(`Invalid scope format '${scope}'. Expected 'RESOURCE.ACTION'.`);
    }
    this.domainPerms.push({
      resource: scope.substring(0, idx),
      action: scope.substring(idx + 1),
      effect: 'Deny',
      source: 'Override'
    });
    return this;
  }

  withDomainPermission(
    resourceCode: string,
    actionCode: string,
    effect: AccessEffect = 'Allow',
    source: PermissionSource = 'Template'
  ): this {
    this.domainPerms.push({ resource: resourceCode, action: actionCode, effect, source });
    if (effect === 'Allow') this.scopes.push(`${resourceCode}.${actionCode}`);
    return this;
  }

  withMenuOption(
    optionCode: string,
    actionCode: string,
    effect: AccessEffect = 'Allow',
    source: PermissionSource = 'Template'
  ): this {
    this.menuOptions.push({ optionCode, actionCode, effect, source });
    if (effect === 'Allow') this.scopes.push(`${optionCode}.${actionCode}`);
    return this;
  }

  withFeatureFlag(flagCode: string, enabled: boolean = true, matchedCriteriaType?: FlagCriteria): this {
    this.flags.push({
      flagCode,
      enabled,
      matchedCriteriaType: enabled ? matchedCriteriaType ?? 'TenantId' : null
    });
    return this;
  }

  build(): AuthorizationGraph {
    const now = new Date();
    const validUntil = new Date(now.getTime() + this.validityMs);

    const actions: ActionRef[] = [];
    const menuAccess = this.buildMenuAccess(actions);
    const domainPermissions = this.buildDomainPermissions(actions);

    return {
      schemaVersion: this.schemaVersion,
      onboardingPending: false,
      // Convive con `onboardingPending` (ADR-0156 §5.1). El builder solo fabrica grafos con acceso
      // concedido; los otros dos estados los cubren los fixtures.
      accessState: 'Granted',
      context: {
        user: {
          email: this.userEmail,
          username: this.userEmail.split('@')[0] ?? this.userEmail,
          value: this.userEmail,
          status: 'Active'
        },
        tenant: { code: this.tenantCode, value: this.tenantName, status: 'Active', isManagementOwner: false },
        systemSuite: { code: this.systemSuiteCode, value: this.systemSuiteCode, status: 'Active' },
        role: { code: this.roleCode, value: this.roleCode, hierarchyLevel: 1 },
        profile: { scope: this.profileScope, isActive: true },
        branch:
          this.profileScope === 'BranchScoped' && this.branchCode !== null
            ? { code: this.branchCode, value: this.branchCode }
            : null,
        // La API lo emite SIEMPRE, también como null: un cliente no debería tener que distinguir
        // «no se pidió sistema» de «la clave no existe en esta versión». El builder no lo emitía y
        // el contrato ya lo exigía — el grafo que fabricaba para las pruebas era, según el propio
        // contrato del SDK, inválido (mismo mal que G-207).
        requestedSystem: null
      },
      authentication: {
        method: this.authMethod,
        provider: this.provider,
        mfaRequired: false,
        issuedAt: now.toISOString(),
        sessionExpiresAt: validUntil.toISOString()
      },
      actions,
      profiles: [],
      menuAccess,
      domainPermissions,
      featureFlags: this.flags.map((f) => ({
        flagCode: f.flagCode,
        isEnabled: f.enabled,
        matchedCriteriaType: f.matchedCriteriaType
      })),
      settings: {},
      effectiveConfig: {
        sessionTimeoutMinutes: 60,
        maxLoginAttempts: 5,
        minPasswordLength: 12,
        mfaRequiredForAdmin: true,
        mfaAllowedMethods: ['Totp'],
        accessTokenDurationMs: 3600000,
        authUseExternalIdp: this.authMethod === 'IDP'
      },
      scopes: Array.from(new Set(this.scopes)),
      generatedAt: now.toISOString(),
      validUntil: validUntil.toISOString()
    };
  }

  buildExpired(): AuthorizationGraph {
    const graph = this.build();
    const past = new Date(Date.now() - 5 * 365 * 24 * 60 * 60 * 1000);
    return {
      ...graph,
      generatedAt: past.toISOString(),
      validUntil: new Date(past.getTime() + 60 * 60 * 1000).toISOString()
    };
  }

  private buildMenuAccess(actions: ActionRef[]): MenuModule[] {
    if (this.menuOptions.length === 0) return [];
    const options = this.menuOptions.map((m) => {
      const existing = actions.find((a) => a.code === m.actionCode);
      const actionRef = existing ?? { code: m.actionCode, value: m.actionCode };
      if (!existing) actions.push(actionRef);
      return {
        code: m.optionCode,
        value: m.optionCode,
        actionCode: m.actionCode,
        effect: m.effect,
        source: m.source
      };
    });
    // Un nodo hoja por opción, agrupando sus acciones: la forma del contrato v2.0.0.
    const hojas = [...new Set(this.menuOptions.map(m => m.optionCode))].map(codigo => ({
      code: codigo,
      value: codigo,
      kind: 'Option' as const,
      sortOrder: 1,
      icon: null,
      route: `/${codigo.toLowerCase()}`,
      actions: options.filter(o => o.code === codigo).map(o => ({
        actionCode: o.actionCode,
        effect: o.effect,
        source: o.source,
      })),
      children: [],
    }));

    return [
      {
        code: 'TEST_MODULE',
        value: 'Test Module',
        sortOrder: 1,
        status: 'Active',
        // Se emite siempre, también como null: sin él un satélite sabe cómo se llama el módulo
        // pero no con qué pintarlo, y acaba resolviendo el icono por código.
        icon: null,
        nodes: [
          {
            code: 'TEST_MENU',
            value: 'Test Menu',
            kind: 'Menu' as const,
            sortOrder: 1,
            icon: null,
            route: null,
            actions: [],
            children: [
              {
                code: 'TEST_SUB',
                value: 'Test SubMenu',
                kind: 'SubMenu' as const,
                sortOrder: 1,
                icon: null,
                route: null,
                actions: [],
                children: hojas,
              },
            ],
          },
        ],
      },
    ];
  }

  private buildDomainPermissions(actions: ActionRef[]): DomainResourcePermissions[] {
    if (this.domainPerms.length === 0) return [];
    const byResource = new Map<string, DomainPermSpec[]>();
    for (const dp of this.domainPerms) {
      const list = byResource.get(dp.resource) ?? [];
      list.push(dp);
      byResource.set(dp.resource, list);
    }
    const result: DomainResourcePermissions[] = [];
    for (const [resourceCode, perms] of byResource) {
      const resolutions = perms.map((p) => {
        const existing = actions.find((a) => a.code === p.action);
        const actionRef = existing ?? { code: p.action, value: p.action };
        if (!existing) actions.push(actionRef);
        return {
          actionCode: p.action,
          value: p.action,
          effect: p.effect,
          source: p.source
        };
      });
      result.push({
        resourceType: 'Aggregate',
        resourceCode,
        value: resourceCode,
        actions: resolutions
      });
    }
    return result;
  }
}
