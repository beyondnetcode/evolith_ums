import type { AuthorizationGraph } from '@evolith-ums/ums-sdk-contracts';
import { isSchemaVersionSupported } from '@evolith-ums/ums-sdk-contracts';
import { UmsErrorCodes } from '@evolith-ums/ums-sdk-contracts';
import { Decisions, type AuthorizationDecision } from './authorization-decision.js';
import { GraphIndex } from './graph-index.js';

const PrimScope = 'RequiresScope';
const PrimMenu = 'RequiresMenuOption';
const PrimDomain = 'RequiresDomainAccess';
const PrimFlag = 'RequiresFeatureFlag';

/**
 * Pure validator. Same semantics as `Evolith.Ums.Sdk.Authorization.AuthorizationValidator` (.NET) and
 * the NestJS guard — given a graph and a probe, returns a decision. No I/O, no framework deps.
 *
 * Enforces (in pre-check order): graph presence → schema version present → schema in range → not
 * expired. Then per-primitive: deny precedence over allow, override over template, missing entries
 * yield NotGranted.
 */
export class AuthorizationValidator {
  requireScope(graph: AuthorizationGraph | null, scope: string): AuthorizationDecision {
    const pre = this.preCheck(graph, PrimScope, scope);
    if (pre !== null) return pre;

    const parsed = parseScope(scope);
    if (parsed === null) {
      return Decisions.notGranted(
        PrimScope,
        scope,
        UmsErrorCodes.ScopeNotGranted,
        `Scope '${scope}' is not in the canonical 'RESOURCE.ACTION' format.`,
        graph!.validUntil
      );
    }

    if (isDeniedInGraph(graph!, parsed.resource, parsed.action)) {
      return Decisions.deny(
        PrimScope,
        scope,
        UmsErrorCodes.ScopeDenied,
        `Scope '${scope}' is explicitly denied in the authorization graph.`,
        graph!.validUntil
      );
    }

    // Los scopes viajan en minúsculas (UMS los deriva con ToLowerInvariant), pero el
    // llamador los escribe con el código tal como lo conoce: 'STOCK_VIEW.VIEW'. Comparar
    // sensible a mayúsculas convertía esa diferencia de forma en un 403 (G-167).
    return GraphIndex.for(graph!).hasScope(scope)
      ? Decisions.granted(PrimScope, scope, graph!.validUntil)
      : Decisions.notGranted(
          PrimScope,
          scope,
          UmsErrorCodes.ScopeNotGranted,
          `Scope '${scope}' is not present in the authorization graph.`,
          graph!.validUntil
        );
  }

  requireMenuOption(graph: AuthorizationGraph | null, optionCode: string): AuthorizationDecision {
    const pre = this.preCheck(graph, PrimMenu, optionCode);
    if (pre !== null) return pre;

    const opt = GraphIndex.for(graph!).option(optionCode);
    if (opt !== undefined) {
      switch (opt.effect) {
        case 'Allow':
          return Decisions.granted(PrimMenu, optionCode, graph!.validUntil);
        case 'Deny':
          return Decisions.deny(
            PrimMenu,
            optionCode,
            UmsErrorCodes.MenuOptionDenied,
            `Menu option '${optionCode}' is explicitly denied (source: ${opt.source}).`,
            graph!.validUntil
          );
        default:
          return Decisions.notGranted(
            PrimMenu,
            optionCode,
            UmsErrorCodes.MenuOptionNotGranted,
            `Menu option '${optionCode}' resolves to NotGranted.`,
            graph!.validUntil
          );
      }
    }

    return Decisions.notGranted(
      PrimMenu,
      optionCode,
      UmsErrorCodes.MenuOptionNotGranted,
      `Menu option '${optionCode}' is not present in the authorization graph.`,
      graph!.validUntil
    );
  }

  requireDomainAccess(
    graph: AuthorizationGraph | null,
    resourceCode: string,
    actionCode: string
  ): AuthorizationDecision {
    const target = `${resourceCode}.${actionCode}`;
    const pre = this.preCheck(graph, PrimDomain, target);
    if (pre !== null) return pre;

    const act = GraphIndex.for(graph!).domain(resourceCode, actionCode);
    if (act !== undefined) {
      switch (act.effect) {
        case 'Allow':
          return Decisions.granted(PrimDomain, target, graph!.validUntil);
        case 'Deny':
          return Decisions.deny(
            PrimDomain,
            target,
            UmsErrorCodes.DomainAccessDenied,
            `Domain access '${target}' is explicitly denied (source: ${act.source}).`,
            graph!.validUntil
          );
        default:
          return Decisions.notGranted(
            PrimDomain,
            target,
            UmsErrorCodes.DomainAccessNotGranted,
            `Domain access '${target}' resolves to NotGranted.`,
            graph!.validUntil
          );
      }
    }

    return Decisions.notGranted(
      PrimDomain,
      target,
      UmsErrorCodes.DomainAccessNotGranted,
      `Domain access '${target}' is not present in the authorization graph.`,
      graph!.validUntil
    );
  }

  requireFeatureFlag(graph: AuthorizationGraph | null, flagCode: string): AuthorizationDecision {
    const pre = this.preCheck(graph, PrimFlag, flagCode);
    if (pre !== null) return pre;

    const flag = graph!.featureFlags.find((f) => f.flagCode === flagCode);
    if (flag === undefined) {
      return Decisions.notGranted(
        PrimFlag,
        flagCode,
        UmsErrorCodes.FeatureFlagNotFound,
        `Feature flag '${flagCode}' is not present in the authorization graph.`,
        graph!.validUntil
      );
    }
    return flag.isEnabled
      ? Decisions.granted(PrimFlag, flagCode, graph!.validUntil)
      : Decisions.notGranted(
          PrimFlag,
          flagCode,
          UmsErrorCodes.FeatureFlagDisabled,
          `Feature flag '${flagCode}' is present but isEnabled is false.`,
          graph!.validUntil
        );
  }

  assertTenant(graph: AuthorizationGraph | null, expectedTenantCode: string): AuthorizationDecision {
    const pre = this.preCheck(graph, 'AssertTenant', expectedTenantCode);
    if (pre !== null) return pre;

    return graph!.context.tenant.code === expectedTenantCode
      ? Decisions.granted('AssertTenant', expectedTenantCode, graph!.validUntil)
      : Decisions.tenantMismatch(expectedTenantCode, graph!.context.tenant.code);
  }

  private preCheck(graph: AuthorizationGraph | null, primitive: string, target: string): AuthorizationDecision | null {
    if (graph === null) return Decisions.graphMissing(primitive, target);
    if (graph.schemaVersion === undefined || graph.schemaVersion === null || graph.schemaVersion === '') {
      return Decisions.schemaMissing(primitive, target);
    }
    if (!isSchemaVersionSupported(graph.schemaVersion)) {
      return Decisions.schemaUnsupported(primitive, target, graph.schemaVersion);
    }
    if (new Date(graph.validUntil).getTime() <= Date.now()) {
      return Decisions.expired(primitive, target, graph.validUntil);
    }
    return null;
  }
}

function parseScope(scope: string): { resource: string; action: string } | null {
  const idx = scope.indexOf('.');
  if (idx <= 0 || idx === scope.length - 1) return null;
  return { resource: scope.substring(0, idx), action: scope.substring(idx + 1) };
}

/**
 * Denegación explícita en cualquiera de las dos superficies del grafo. Delega en el índice:
 * antes recorría el árbol de menús completo y todos los recursos en cada comprobación.
 */
function isDeniedInGraph(graph: AuthorizationGraph, resourceCode: string, actionCode: string): boolean {
  return GraphIndex.for(graph).isDenied(resourceCode, actionCode);
}
