import { SetMetadata, applyDecorators } from '@nestjs/common';
import {
  UMS_AUTH_METADATA,
  type UmsAuthRequirement,
  type UmsDenialBehavior
} from './metadata.js';

interface DecoratorOptions {
  readonly onDenied?: UmsDenialBehavior;
  readonly auditOnly?: boolean;
}

function attach(requirement: UmsAuthRequirement): MethodDecorator {
  return (target, key, descriptor) => {
    const existing: UmsAuthRequirement[] =
      Reflect.getMetadata?.(UMS_AUTH_METADATA, descriptor.value as object) ??
      [];
    const next = [...existing, requirement];
    SetMetadata(UMS_AUTH_METADATA, next)(target, key, descriptor);
  };
}

/**
 * Requires the OAuth2-style scope `"RESOURCE.ACTION"` to be Granted in the user's graph.
 * Stacks with other UMS decorators — every requirement on a handler must pass.
 */
export function RequiresScope(scope: string, options?: DecoratorOptions): MethodDecorator {
  return applyDecorators(
    attach({
      type: 'RequiresScope',
      scope,
      onDenied: options?.onDenied,
      auditOnly: options?.auditOnly
    })
  );
}

export function RequiresMenuOption(optionCode: string, options?: DecoratorOptions): MethodDecorator {
  return applyDecorators(
    attach({
      type: 'RequiresMenuOption',
      optionCode,
      onDenied: options?.onDenied,
      auditOnly: options?.auditOnly
    })
  );
}

export function RequiresDomainAccess(
  resourceCode: string,
  actionCode: string,
  options?: DecoratorOptions
): MethodDecorator {
  return applyDecorators(
    attach({
      type: 'RequiresDomainAccess',
      resourceCode,
      actionCode,
      onDenied: options?.onDenied,
      auditOnly: options?.auditOnly
    })
  );
}

export function RequiresFeatureFlag(flagCode: string, options?: DecoratorOptions): MethodDecorator {
  return applyDecorators(
    attach({
      type: 'RequiresFeatureFlag',
      flagCode,
      onDenied: options?.onDenied,
      auditOnly: options?.auditOnly
    })
  );
}
