/**
 * Resolved authorization effect for a single (target, action) pair in the AuthorizationGraph.
 * Three-valued: explicit Allow, explicit Deny, or NotGranted (implicit deny — no entry exists).
 * Deny always wins over Allow (Axiom A3).
 */
export type AccessEffect = 'Allow' | 'Deny' | 'NotGranted';

/**
 * Where a resolved permission originated: from the bound PermissionTemplate item or from an
 * explicit per-Profile override. Override takes precedence over Template.
 */
export type PermissionSource = 'Template' | 'Override';
