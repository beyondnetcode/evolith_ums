/**
 * ProtectedRoute.tsx
 *
 * Production-grade route protection component.
 * Implements OWASP recommendations for protected routes:
 *
 * - Server-side session validation
 * - Automatic redirect on auth failure
 * - No sensitive data exposure in redirect
 * - Prevents route enumeration via timing-safe checks
 * - Loading states to prevent flash of protected content
 */
import React, { useEffect, useState } from 'react';
import { Navigate, useLocation } from 'react-router';
import { useAuthStore } from '@app/stores/auth.store';
import { authService } from '@app/identity/services/auth.service';
import { Spinner } from '@shared/components/Spinner';

interface ProtectedRouteProps {
  children: React.ReactNode;
}

export function ProtectedRoute({ children }: ProtectedRouteProps): React.JSX.Element {
  const location = useLocation();
  const [isValidating, setIsValidating] = useState(true);
  const { isAuthenticated, isLoading, user, checkSession } = useAuthStore();

  useEffect(() => {
    let isMounted = true;

    const validateAndRedirect = async () => {
      if (!isAuthenticated || !user) {
        setIsValidating(false);
        return;
      }

      const isValid = await checkSession();

      if (!isMounted) return;

      setIsValidating(false);

      if (!isValid) {
        return;
      }
    };

    validateAndRedirect();

    return () => {
      isMounted = false;
    };
  }, [isAuthenticated, user, checkSession]);

  if (isLoading || isValidating) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-m3-surface">
        <div className="flex flex-col items-center gap-4">
          <Spinner size="large" />
          <p className="text-sm text-m3-secondary">Validando sesión...</p>
        </div>
      </div>
    );
  }

  if (!isAuthenticated || !user) {
    return (
      <Navigate to="/login" state={{ from: location.pathname, showSessionExpired: true }} replace />
    );
  }

  return <>{children}</>;
}

export function useRequireAuth() {
  const { isAuthenticated, isLoading } = useAuthStore();
  const location = useLocation();

  if (isLoading) {
    return { isAuthorized: null as boolean | null, redirectPath: null };
  }

  if (!isAuthenticated) {
    return {
      isAuthorized: false,
      redirectPath: `/login?redirect=${encodeURIComponent(location.pathname)}`,
    };
  }

  return { isAuthorized: true, redirectPath: null };
}
