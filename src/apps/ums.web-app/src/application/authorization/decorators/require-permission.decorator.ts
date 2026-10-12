/**
 * require-permission.decorator.ts
 *
 * Provides a TypeScript method decorator to enforce authorization at the service level
 * in the frontend. If the user lacks the required permission, it throws an error
 * or prevents the method from executing.
 */
import { useAuthStore } from '@app/stores/auth.store';

/**
 * Decorator to require a specific permission to execute a method.
 * @param resourceCode The code of the domain resource (e.g. 'TENANTS', 'USERS')
 * @param actionCode The code of the action (e.g. 'VIEW', 'MANAGE')
 */
export function RequirePermission(resourceCode: string, actionCode: string) {
  return function (target: any, propertyKey: string, descriptor: PropertyDescriptor) {
    const originalMethod = descriptor.value;

    descriptor.value = function (...args: any[]) {
      // In a non-React-component context, we can read the store state directly from Zustand
      const state = useAuthStore.getState();

      if (!state.isAuthenticated || !state.user) {
        throw new Error(`Unauthorized access: No active session. Method ${propertyKey} aborted.`);
      }

      // Internal admins bypass all permission checks
      if (state.user.isInternalAdmin) {
        return originalMethod.apply(this, args);
      }

      // Check against user's flat permissions list
      const requiredPermission = `${resourceCode.toLowerCase()}.${actionCode.toLowerCase()}`;

      const hasPermission = state.user.permissions?.some(
        p => p.toLowerCase() === requiredPermission
      );

      if (!hasPermission) {
        // You might want to format a user-friendly error or integrate with a notification system
        const error = new Error(
          `Access Denied: Missing required permission '${requiredPermission}' for action.`
        );
        error.name = 'AuthorizationError';
        throw error;
      }

      // If authorized, proceed with the original method
      return originalMethod.apply(this, args);
    };

    return descriptor;
  };
}
