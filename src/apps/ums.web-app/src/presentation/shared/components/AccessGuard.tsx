import React from 'react';
import { useAccessResolution } from '@app/authorization/hooks/use-access-resolution';

interface AccessGuardProps {
  moduleCode?: string;
  menuCode?: string;
  optionCode?: string;
  featureCode?: string;
  fallback?: React.ReactNode;
  children: React.ReactNode;
}

/**
 * AccessGuard
 * Oculta o muestra el contenido según el grafo de autorización del usuario.
 * Si no se cumplen los requisitos de acceso, muestra el `fallback` (por defecto null).
 */
export const AccessGuard: React.FC<AccessGuardProps> = ({
  moduleCode,
  menuCode,
  optionCode,
  featureCode,
  fallback = null,
  children,
}) => {
  const { hasModuleAccess, hasMenuAccess, hasOptionAccess, hasFeatureFlag } = useAccessResolution();

  let hasAccess = true;

  if (featureCode && !hasFeatureFlag(featureCode)) {
    hasAccess = false;
  }

  if (hasAccess && moduleCode && !hasModuleAccess(moduleCode)) {
    hasAccess = false;
  }

  if (hasAccess && menuCode && optionCode && !hasOptionAccess(menuCode, optionCode)) {
    hasAccess = false;
  } else if (
    hasAccess &&
    menuCode &&
    !optionCode &&
    moduleCode &&
    !hasMenuAccess(moduleCode, menuCode)
  ) {
    hasAccess = false;
  }

  if (!hasAccess) {
    return <>{fallback}</>;
  }

  return <>{children}</>;
};

/**
 * HOC: withAccessGuard
 * Envuelve un componente entero con el AccessGuard.
 */
export function withAccessGuard<P extends object>(
  WrappedComponent: React.ComponentType<P>,
  guardProps: Omit<AccessGuardProps, 'children'>
) {
  return function WithAccessGuardWrapper(props: P) {
    return (
      <AccessGuard {...guardProps}>
        <WrappedComponent {...props} />
      </AccessGuard>
    );
  };
}
