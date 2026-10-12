import React, { useState, useMemo } from 'react';
import { useNavigate, useLocation } from 'react-router';
import { useI18n } from '@app/i18n/use-i18n';
import { useNavigationPrefetch } from '@app/shared/hooks/use-navigation-prefetch';
import { ChevronRight, ChevronDown, X } from 'lucide-react';
import { useShellNavigation, claveActiva } from './use-shell-navigation';
import type { ShellItem } from './use-shell-navigation';
import { useIsMobile } from '../hooks/use-breakpoint';

interface NavRailProps {
  /** Desktop: colapsa el rail a solo-iconos. */
  collapsed: boolean;
  /** Móvil: el drawer está abierto. */
  mobileOpen?: boolean;
  /** Móvil: cerrar el drawer (backdrop, botón o tras navegar). */
  onCloseMobile?: () => void;
}

export const NavRail: React.FC<NavRailProps> = ({
  collapsed,
  mobileOpen = false,
  onCloseMobile,
}) => {
  const t = useI18n();
  const navigate = useNavigate();
  const location = useLocation();
  const isMobile = useIsMobile();
  const { prefetchById } = useNavigationPrefetch();
  // Módulos plegados por el usuario. Ausencia = desplegado: el menú llega del grafo y sus códigos
  // no se conocen de antemano, así que no se puede sembrar un estado inicial por módulo.
  const [collapsedModules, setCollapsedModules] = useState<Record<string, boolean>>({});

  const modules = useShellNavigation();
  const activeKey = useMemo(
    () => claveActiva(modules, location.pathname),
    [modules, location.pathname]
  );

  const toggleModule = (moduleKey: string) => {
    setCollapsedModules(prev => ({
      ...prev,
      [moduleKey]: !prev[moduleKey],
    }));
  };

  /** Navega y, en móvil, cierra el drawer. */
  const go = (item: ShellItem) => {
    if (item.route) navigate(item.route);
    onCloseMobile?.();
  };

  const prefetch = (item: ShellItem) => {
    if (item.prefetchId) prefetchById(item.prefetchId);
  };

  // ── Cuerpo acordeón (desktop expandido + drawer móvil) ──
  const accordion = (
    <nav role="navigation" aria-label="Main navigation" className="space-y-4 px-3 select-none">
      {modules.map(mod => {
        const isExpanded = !collapsedModules[mod.key];
        return (
          <div key={mod.key} className="space-y-1.5">
            <button
              onClick={() => toggleModule(mod.key)}
              aria-expanded={isExpanded}
              aria-controls={`nav-module-${mod.key}`}
              className="w-full flex items-center justify-between px-2.5 py-2 rounded-lg hover:bg-m3-primary/5 transition-all text-left"
            >
              <div className="flex items-center gap-2">
                {mod.icon}
                <span className="text-[11px] font-semibold uppercase tracking-wider text-m3-on-surface/70">
                  {mod.label}
                </span>
              </div>
              {isExpanded ? (
                <ChevronDown className="w-3.5 h-3.5 text-m3-secondary transition-transform duration-200" />
              ) : (
                <ChevronRight className="w-3.5 h-3.5 text-m3-secondary transition-transform duration-200" />
              )}
            </button>

            {isExpanded && (
              <div
                id={`nav-module-${mod.key}`}
                className="pl-2.5 ml-2.5 border-l border-m3-outline/25 space-y-1 animate-slideDown"
              >
                {mod.items.map(item => {
                  const isActive = activeKey === item.key;
                  const cls = `w-full flex items-center gap-2.5 px-3 py-2.5 rounded-lg transition-all duration-150 text-left font-medium text-[13px] lg:text-[12px] ${
                    isActive
                      ? 'bg-m3-primary-container text-m3-on-primary-container font-semibold'
                      : 'text-m3-secondary hover:bg-m3-primary/10 hover:text-m3-primary'
                  }`;
                  const inner = (
                    <>
                      <span className={isActive ? 'text-m3-primary' : 'text-m3-secondary/70'}>
                        {item.icon}
                      </span>
                      <span>{item.label}</span>
                    </>
                  );
                  if (item.href) {
                    return (
                      <a
                        key={item.key}
                        href={item.href}
                        target="_blank"
                        rel="noopener noreferrer"
                        onClick={() => onCloseMobile?.()}
                        className={cls}
                      >
                        {inner}
                      </a>
                    );
                  }
                  return (
                    <button
                      key={item.key}
                      onClick={() => go(item)}
                      onMouseOver={() => prefetch(item)}
                      onFocus={() => prefetch(item)}
                      className={cls}
                    >
                      {inner}
                    </button>
                  );
                })}
              </div>
            )}
          </div>
        );
      })}
    </nav>
  );

  const footer = (
    <div className="px-6 py-4 border-t border-m3-outline/10 text-[10px] text-m3-secondary/50 font-normal leading-relaxed">
      <p>{t.portalFooter}</p>
      <p className="mt-1 font-normal text-indigo-400">{t.archVersion}</p>
    </div>
  );

  // ── Rail de escritorio (oculto en < lg) ──
  const desktopAside = collapsed ? (
    <aside
      data-testid="nav-rail"
      className="bg-m3-surface border-r border-m3-outline/25 select-none transition-all duration-300 w-20 hidden lg:block"
    >
      <div className="flex flex-col h-full py-6 justify-between">
        <nav role="navigation" aria-label="Main navigation" className="space-y-4 px-3 select-none">
          <div className="space-y-2.5 flex flex-col items-center">
            {modules
              .flatMap(m => m.items)
              .map(item => {
                const isActive = activeKey === item.key;
                const cls = `p-2.5 rounded-xl transition-all duration-200 text-center ${
                  isActive
                    ? 'bg-m3-primary-container text-m3-on-primary-container font-semibold'
                    : 'text-m3-secondary hover:bg-m3-primary/10 hover:text-m3-primary'
                }`;
                if (item.href) {
                  return (
                    <a
                      key={item.key}
                      href={item.href}
                      target="_blank"
                      rel="noopener noreferrer"
                      className={cls}
                      title={item.label}
                    >
                      <span>{item.icon}</span>
                    </a>
                  );
                }
                return (
                  <button
                    key={item.key}
                    onClick={() => go(item)}
                    onMouseOver={() => prefetch(item)}
                    onFocus={() => prefetch(item)}
                    className={cls}
                    title={item.label}
                  >
                    <span className={isActive ? 'text-m3-primary' : ''}>{item.icon}</span>
                  </button>
                );
              })}
          </div>
        </nav>
      </div>
    </aside>
  ) : (
    <aside
      data-testid="nav-rail"
      className="bg-m3-surface border-r border-m3-outline/25 select-none transition-all duration-300 w-64 hidden lg:block"
    >
      <div className="flex flex-col h-full py-6 justify-between">
        {accordion}
        {footer}
      </div>
    </aside>
  );

  // ── Drawer móvil (oculto en ≥ lg) ──
  const mobileDrawer = (
    <div
      className={`lg:hidden fixed inset-0 z-50 ${mobileOpen ? '' : 'pointer-events-none'}`}
      aria-hidden={!mobileOpen}
    >
      <div
        onClick={onCloseMobile}
        className={`absolute inset-0 bg-black/40 backdrop-blur-sm transition-opacity duration-300 ${
          mobileOpen ? 'opacity-100' : 'opacity-0'
        }`}
      />
      <aside
        data-testid="nav-rail-mobile"
        className={`absolute inset-y-0 left-0 w-72 max-w-[85vw] bg-m3-surface border-r border-m3-outline/25 shadow-2xl flex flex-col transform transition-transform duration-300 ease-in-out ${
          mobileOpen ? 'translate-x-0' : '-translate-x-full'
        }`}
      >
        <div className="flex items-center justify-between h-16 px-4 border-b border-m3-outline/20 shrink-0">
          <span className="text-sm font-extrabold tracking-wider text-m3-primary">{t.appName}</span>
          <button
            onClick={onCloseMobile}
            aria-label="Cerrar menú"
            className="p-2 rounded-full hover:bg-m3-primary/10 text-m3-secondary"
          >
            <X className="w-5 h-5" />
          </button>
        </div>
        <div className="flex-1 overflow-y-auto py-4 flex flex-col justify-between">
          {accordion}
          {footer}
        </div>
      </aside>
    </div>
  );

  // Render condicional por JS (no solo CSS) para no duplicar el árbol de
  // navegación en el DOM: rail en escritorio, drawer en móvil.
  return isMobile ? mobileDrawer : desktopAside;
};
