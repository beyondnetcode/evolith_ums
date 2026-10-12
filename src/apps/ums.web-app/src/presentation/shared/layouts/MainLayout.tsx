import React, { useState, useCallback, useEffect } from 'react';
import { useAuthStore } from '@app/stores/auth.store';
import { useNotificationStore } from '@app/stores/notification.store';
import { useI18n } from '@app/i18n/use-i18n';
import { useIdleTimeout } from '@app/hooks/use-idle-timeout';
import { useIsMobile } from '@shared/hooks/use-breakpoint';
import { useSystemSettings } from '@app/authorization/hooks/use-system-settings';
import { TopAppBar } from './TopAppBar';
import { NavRail } from './NavRail';

export const MainLayout: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const { user, logout, isAuthenticated } = useAuthStore();
  const { addNotification } = useNotificationStore();
  const t = useI18n();
  const isMobile = useIsMobile();
  const { ui } = useSystemSettings();
  // Disposición inicial que declara el sistema (`settings.ui.layout`, G-178). El producto entiende
  // dos: `nav-rail` —el rail desplegado— y `nav-rail-compact` —el mismo rail en solo-iconos, para
  // sistemas con pocas pantallas donde la columna de texto sobra. Cualquier otro valor cae en la
  // primera: es preferible una disposición conocida a una pantalla sin navegación.
  // Es el estado INICIAL, no un candado: la hamburguesa sigue mandando después.
  const [navCollapsed, setNavCollapsed] = useState(ui.layout === 'nav-rail-compact');
  const [mobileNavOpen, setMobileNavOpen] = useState(false);

  // La hamburguesa colapsa el rail en escritorio y abre/cierra el drawer en móvil.
  const handleToggleNav = useCallback(() => {
    if (isMobile) setMobileNavOpen(open => !open);
    else setNavCollapsed(collapsed => !collapsed);
  }, [isMobile]);

  // Cerrar el drawer al pasar a escritorio (el cierre por navegación lo hace NavRail).
  useEffect(() => {
    if (!isMobile) setMobileNavOpen(false);
  }, [isMobile]);

  const handleIdleLogout = useCallback(() => {
    logout();
    addNotification({
      title: t.sessionExpired,
      message: t.sessionExpiredMsg,
      type: 'warning',
    });
  }, [logout, addNotification, t]);

  useIdleTimeout({
    timeoutMs: 15 * 60 * 1000,
    onIdle: handleIdleLogout,
    enabled: !!user,
  });

  if (!isAuthenticated) {
    return (
      <div className="min-h-screen flex flex-col overflow-x-hidden transition-colors duration-500 bg-m3-surface text-m3-on-surface">
        <div className="m3-glow top-0 left-1/4 w-[450px] h-[450px] bg-m3-primary/10 dark:bg-m3-primary/5" />
        <div className="m3-glow bottom-10 right-10 w-[350px] h-[350px] bg-indigo-500/10 dark:bg-indigo-500/5" />
        <main className="flex-1 flex flex-col relative">
          <div className="flex-1 flex flex-col p-4 sm:p-5 min-h-0">{children}</div>
        </main>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex flex-col overflow-x-hidden transition-colors duration-500 bg-m3-surface text-m3-on-surface">
      <div className="m3-glow top-0 left-1/4 w-[450px] h-[450px] bg-m3-primary/10 dark:bg-m3-primary/5" />
      <div className="m3-glow bottom-10 right-10 w-[350px] h-[350px] bg-indigo-500/10 dark:bg-indigo-500/5" />

      <TopAppBar onToggleNav={handleToggleNav} />

      <div className="flex-1 flex overflow-hidden">
        <NavRail
          collapsed={navCollapsed}
          mobileOpen={mobileNavOpen}
          onCloseMobile={() => setMobileNavOpen(false)}
        />

        <main className="flex-1 min-w-0 overflow-y-auto flex flex-col relative">
          <div className="flex-1 flex flex-col p-3 sm:p-5 min-h-0">{children}</div>
        </main>
      </div>
    </div>
  );
};
