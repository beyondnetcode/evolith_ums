/**
 * RouteLoader.tsx — Loading fallback for lazy-loaded route components.
 *
 * Displays a minimal skeleton while the route chunk is being fetched.
 */
import React from 'react';

export const RouteLoader: React.FC = () => (
  <div className="flex items-center justify-center min-h-[60vh]" role="status" aria-live="polite">
    <div className="flex flex-col items-center gap-4">
      <div className="w-10 h-10 border-4 border-m3-primary/30 border-t-m3-primary rounded-full animate-spin" />
      <p className="text-sm text-m3-secondary font-medium">Loading...</p>
    </div>
  </div>
);
