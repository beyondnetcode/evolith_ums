import React, { useState } from 'react';
import { Menu, Sun, Moon, Bell, Globe, LogOut } from 'lucide-react';
import { useAuthStore } from '@app/stores/auth.store';
import { ProfileSelector } from '@presentation/shared/components/ProfileSelector';
import { SystemThemeApplier } from '@presentation/shared/components/SystemThemeApplier';
import { useSystemSettings } from '@app/authorization/hooks/use-system-settings';
import { useThemeStore } from '@app/stores/theme.store';
import { useDevToolsStore } from '@app/stores/devTools.store';
import { useI18nStore } from '@app/stores/i18n.store';
import { useNotificationStore } from '@app/stores/notification.store';
import { useI18n } from '@app/i18n/use-i18n';
import { NotificationCenter } from '../components/NotificationCenter';
import { ToastQueue } from '../components/ToastQueue';
import { Tooltip } from '../components/Tooltip';
import { ConnectedUserDrawer } from '../components/ConnectedUserDrawer';
import { assetUrl } from '../utils/asset-url';

export const TopAppBar: React.FC<{ onToggleNav: () => void }> = ({ onToggleNav }) => {
  const { user, logout } = useAuthStore();
  const { brand } = useSystemSettings();
  const { isDarkMode, toggleDarkMode } = useThemeStore();
  useDevToolsStore();
  const { language, setLanguage } = useI18nStore();
  const { notifications, setIsOpen, isOpen } = useNotificationStore();
  const t = useI18n();

  const [isUserDrawerOpen, setIsUserDrawerOpen] = useState(false);

  const unreadCount = notifications.filter(n => !n.read).length;
  const etiquetaNotificaciones =
    unreadCount > 0 ? `Notifications, ${unreadCount} unread` : 'Notifications';

  const handleLanguageToggle = () => {
    setLanguage(language === 'en' ? 'es' : 'en');
  };

  const handleLogout = () => {
    logout();
  };

  const getUserInitials = () => {
    if (!user?.username) return '??';
    return user.username.substring(0, 2).toUpperCase();
  };

  /** Hay marca propia del sistema en cuanto publique cualquiera de los dos recursos. */
  const marcaSistema = brand.iconUrl ?? brand.logoUrl;
  const nombreDeMarca = brand.displayName ?? t.appName;
  // Nombre y descriptor viajan juntos: si el sistema publica el suyo, la barra deja de anunciar
  // el producto. Mezclar el nombre de uno con el subtítulo del otro solo confunde sobre dónde está
  // el usuario, que es justo lo que esta zona de la barra responde.
  const descriptorDeMarca = brand.tagline ?? (brand.displayName ? undefined : t.appSubtitle);
  // Para el ancho de un móvil. El nombre corto existe justo para esto; si el sistema no lo publica
  // se recorta el largo antes que caer al nombre del producto, que sería anunciar otro sistema.
  const nombreCorto = brand.shortName ?? nombreDeMarca;

  return (
    <>
      <header
        role="banner"
        data-testid="top-app-bar"
        className="sticky top-0 z-40 bg-m3-surface/85 backdrop-blur-md border-b border-m3-outline/25 h-16 px-3 sm:px-6 flex items-center justify-between gap-2 select-none"
      >
        <div className="flex items-center gap-2 sm:gap-3 min-w-0">
          <button
            onClick={onToggleNav}
            aria-label="Toggle navigation"
            className="p-2.5 rounded-full hover:bg-m3-primary/10 text-m3-secondary transition-colors shrink-0"
          >
            <Menu className="w-5 h-5" />
          </button>

          <div className="flex items-center gap-2.5 min-w-0">
            {/* La marca del SISTEMA sustituye a la del producto solo si está configurada
                (G-178). Sin ajustes publicados se ve el producto por defecto, no un hueco.

                Icono y logotipo no son el mismo recurso y por eso el sistema publica los dos:
                el icono es la marca compacta —cuadrada— y el logotipo el lockup ancho. En móvil
                manda el icono, donde el lockup competía por el ancho con el título; desde `sm`,
                el logotipo. Cada uno respalda al otro: un sistema que solo configure uno se ve
                completo en ambos tamaños. */}
            {marcaSistema ? (
              <>
                <img
                  src={brand.iconUrl ?? marcaSistema}
                  alt={nombreDeMarca}
                  className="h-9 w-9 object-contain sm:hidden"
                />
                <img
                  src={brand.logoUrl ?? marcaSistema}
                  alt={nombreDeMarca}
                  className="h-9 w-auto hidden sm:block"
                />
              </>
            ) : (
              <>
                <img
                  src={assetUrl('brand/logo-evolith.png')}
                  alt="EVOLITH"
                  className="h-9 w-auto dark:hidden"
                />
                <img
                  src={assetUrl('brand/logo-evolith-white.png')}
                  alt="EVOLITH"
                  className="h-9 w-auto hidden dark:block"
                />
              </>
            )}
            <div className="min-w-0 border-l border-m3-outline/30 pl-2.5">
              {/* Un solo encabezado con dos formas del mismo nombre: el corto cabe en un móvil,
                  donde el largo se recortaría a la mitad de una palabra. El descriptor se reserva
                  para cuando hay sitio; en móvil compite con el nombre y pierde. */}
              <h1 className="text-sm font-extrabold tracking-wider text-m3-primary truncate">
                <span className="sm:hidden">{nombreCorto}</span>
                <span className="hidden sm:inline">{nombreDeMarca}</span>
              </h1>
              {descriptorDeMarca && (
                <p className="hidden sm:block text-[10px] text-m3-secondary/70 font-semibold uppercase tracking-widest">
                  {descriptorDeMarca}
                </p>
              )}
            </div>
          </div>
        </div>

        <div className="flex items-center gap-1.5 sm:gap-3 shrink-0">
          {user && (
            <div className="flex items-center gap-2">
              <div className="hidden md:flex items-center gap-2 px-3 py-1.5 bg-m3-primary-container/20 rounded-xl border border-m3-primary-container/30 text-[11px] font-medium text-m3-primary">
                <span>
                  {t.devUser} {user.username}
                </span>
              </div>
              <div className="hidden md:flex items-center gap-2 px-3 py-1.5 bg-accent-50 dark:bg-accent-950/30 rounded-xl border border-accent-200 dark:border-accent-800 text-[11px] font-semibold text-accent-700 dark:text-accent-400">
                <span>
                  {user.isInternalAdmin ? 'Admin Local' : user.tenantName || 'Tenant N/A'}
                </span>
              </div>
            </div>
          )}

          {user && (
            <Tooltip content={t.logoutBtn} placement="bottom">
              <button
                onClick={handleLogout}
                aria-label={t.logoutBtn}
                className="p-2.5 rounded-full hover:bg-m3-primary/10 text-m3-secondary hover:text-m3-primary transition-all border border-m3-outline/30"
              >
                <LogOut className="w-4 h-4" />
              </button>
            </Tooltip>
          )}

          <Tooltip content={t.toggleLanguage} placement="bottom">
            <button
              onClick={handleLanguageToggle}
              aria-label={language === 'en' ? 'Switch to Spanish' : 'Switch to English'}
              className="p-2.5 rounded-full hover:bg-m3-primary/10 text-m3-secondary hover:text-m3-primary transition-all flex items-center gap-1.5 border border-m3-outline/30"
            >
              <Globe className="w-4 h-4 text-m3-primary" />
              <span className="text-[11px] font-medium">{language.toUpperCase()}</span>
            </button>
          </Tooltip>

          <Tooltip content={t.toggleTheme} placement="bottom">
            <button
              onClick={toggleDarkMode}
              aria-label={isDarkMode ? 'Switch to light mode' : 'Switch to dark mode'}
              className="p-2.5 rounded-full hover:bg-m3-primary/10 text-m3-secondary hover:text-m3-primary transition-all border border-m3-outline/30"
            >
              {isDarkMode ? (
                <Sun className="w-4 h-4 text-amber-400" />
              ) : (
                <Moon className="w-4 h-4 text-m3-primary" />
              )}
            </button>
          </Tooltip>

          <Tooltip content={t.openNotifications} placement="bottom">
            <button
              onClick={() => setIsOpen(!isOpen)}
              aria-label={etiquetaNotificaciones}
              className="p-2.5 rounded-full hover:bg-m3-primary/10 text-m3-secondary hover:text-m3-primary transition-all relative border border-m3-outline/30"
            >
              <Bell className="w-4 h-4" />
              {unreadCount > 0 && (
                <span className="absolute top-1 right-1 bg-m3-primary text-m3-on-primary font-extrabold text-[8px] px-1.5 py-0.5 rounded-full scale-90 animate-pulse">
                  {unreadCount}
                </span>
              )}
            </button>
          </Tooltip>

          {/* Selector de perfil: se pinta solo si el usuario tiene más de uno (G-177). */}
          <ProfileSelector className="hidden md:block" />

          {user && <div className="h-8 w-px bg-m3-outline/30" />}

          {user && (
            <div className="flex items-center gap-2">
              <Tooltip
                content={
                  <div className="text-center">
                    <p className="font-bold">{user.username}</p>
                    <p className="text-xs opacity-75">{user.email}</p>
                    <p className="text-xs opacity-75 mt-1">Tenant: {user.tenantCode || 'N/A'}</p>
                    <p className="text-[10px] mt-2 opacity-60">Click to view session details</p>
                  </div>
                }
                placement="bottom"
              >
                <button
                  onClick={() => setIsUserDrawerOpen(true)}
                  aria-label="View connected user details"
                  className="w-9 h-9 rounded-full bg-m3-primary/15 border-2 border-m3-primary/30 flex items-center justify-center font-bold text-xs text-m3-primary hover:bg-m3-primary/25 hover:border-m3-primary/50 transition-all cursor-pointer"
                >
                  {getUserInitials()}
                </button>
              </Tooltip>
            </div>
          )}
        </div>
      </header>

      <SystemThemeApplier />
      <NotificationCenter />
      <ToastQueue />

      <ConnectedUserDrawer
        isOpen={isUserDrawerOpen}
        onClose={() => setIsUserDrawerOpen(false)}
        onLogout={handleLogout}
      />
    </>
  );
};
