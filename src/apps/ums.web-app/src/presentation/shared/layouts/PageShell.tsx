import React from 'react';

/**
 * PageShell — lightweight page wrapper that all screens compose inside.
 *
 * Responsibilities:
 * 1. Ensures every page occupies the full available height inside MainLayout.
 * 2. Provides a consistent context boundary for future cross-cutting concerns
 *    (breadcrumbs, page-level error boundaries, analytics, etc.).
 * 3. Keeps pages decoupled from the flex model of MainLayout — if the outer
 *    layout changes, only PageShell needs adjusting.
 *
 * Usage:
 *   <PageShell>
 *     <MasterDetailLayout master={…} detail={…} />
 *   </PageShell>
 */

export interface PageShellProps {
  children: React.ReactNode;
  /** Extra Tailwind classes on the root element (e.g. padding overrides). */
  className?: string;
}

export const PageShell: React.FC<PageShellProps> = ({ children, className = '' }) => (
  <div className={`flex flex-col flex-1 min-h-0 h-full ${className}`}>{children}</div>
);
