import { CanActivate, ExecutionContext, Inject, Injectable, Optional } from '@nestjs/common';
import { Reflector } from '@nestjs/core';
import type { AuthorizationGraph } from '@evolith-ums/ums-sdk-contracts';
import {
  type AuthGraphAccessor,
  type AuthorizationDecision,
  type AuthorizationLogger,
  type AuthorizationValidator,
  AuthorizationDeniedError,
  isGranted
} from '@evolith-ums/ums-sdk-authorization';
import {
  UMS_AUTH_GRAPH_ACCESSOR,
  UMS_AUTHORIZATION_LOGGER,
  UMS_AUTHORIZATION_VALIDATOR,
  UMS_SDK_OPTIONS
} from './tokens.js';
import { UMS_AUTH_METADATA, type UmsAuthRequirement } from './metadata.js';
import type { UmsSdkModuleOptions } from './options.js';

/**
 * Single NestJS guard implementing all four UMS authorization primitives by reading metadata
 * attached via the `@Requires*` decorators. Decorator-free handlers pass through.
 *
 * Apply globally (`{ provide: APP_GUARD, useClass: UmsAuthGuard }`) or per-controller via
 * `@UseGuards(UmsAuthGuard)`.
 */
@Injectable()
export class UmsAuthGuard implements CanActivate {
  constructor(
    private readonly reflector: Reflector,
    @Inject(UMS_AUTH_GRAPH_ACCESSOR) private readonly accessor: AuthGraphAccessor,
    @Inject(UMS_AUTHORIZATION_VALIDATOR) private readonly validator: AuthorizationValidator,
    @Inject(UMS_SDK_OPTIONS) private readonly options: UmsSdkModuleOptions,
    @Optional() @Inject(UMS_AUTHORIZATION_LOGGER) private readonly logger: AuthorizationLogger | null = null
  ) {}

  canActivate(context: ExecutionContext): boolean {
    const requirements = this.reflector.get<UmsAuthRequirement[] | undefined>(
      UMS_AUTH_METADATA,
      context.getHandler()
    );
    if (requirements === undefined || requirements.length === 0) return true;

    const graph = this.accessor.current();
    for (const req of requirements) {
      const decision = this.evaluate(req, graph);
      if (isGranted(decision)) continue;

      const auditOnly = req.auditOnly === true || this.options.mode === 'audit-only';
      this.logDenial(decision, auditOnly);

      if (auditOnly) continue;
      if (req.onDenied === 'ignore') continue;

      throw new AuthorizationDeniedError(decision);
    }
    return true;
  }

  private evaluate(req: UmsAuthRequirement, graph: AuthorizationGraph | null): AuthorizationDecision {
    switch (req.type) {
      case 'RequiresScope':
        return this.validator.requireScope(graph, req.scope);
      case 'RequiresMenuOption':
        return this.validator.requireMenuOption(graph, req.optionCode);
      case 'RequiresDomainAccess':
        return this.validator.requireDomainAccess(graph, req.resourceCode, req.actionCode);
      case 'RequiresFeatureFlag':
        return this.validator.requireFeatureFlag(graph, req.flagCode);
    }
  }

  private logDenial(decision: AuthorizationDecision, auditOnly: boolean): void {
    if (this.logger === null) return;
    this.logger.warn({
      event: auditOnly ? 'AuthorizationDeniedEvent (audit-only)' : 'AuthorizationDeniedEvent',
      primitive: decision.primitive,
      target: decision.target,
      code: decision.errorCode,
      reason: decision.reason
    });
  }
}
