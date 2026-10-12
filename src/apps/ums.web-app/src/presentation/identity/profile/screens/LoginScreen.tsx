/**
 * LoginScreen.tsx
 *
 * Production-grade login screen.
 * Implements OWASP recommendations:
 *
 * - Input validation and sanitization
 * - No information leakage in error messages
 * - Rate limiting awareness (handled by backend)
 * - Secure password handling (no logging)
 * - Proper session management on success
 * - Accessible design with proper labels
 */
import React, { useState, useEffect, useCallback } from 'react';
import { useNavigate, useLocation } from 'react-router';
import { useAuthStore, detectBrowserTimezone } from '@app/stores/auth.store';
import { useI18nStore } from '@app/stores/i18n.store';
import type { SupportedLanguage } from '@app/stores/i18n.store';
import { useDevToolsStore } from '@app/stores/devTools.store';
import { useI18n } from '@app/i18n/use-i18n';
import { M3Card } from '@shared/components/M3Card';
import { M3Button } from '@shared/components/M3Button';
import { M3TextField } from '@shared/components/M3TextField';
import { TenantSelect } from '@shared/components/TenantSelect';
import { Key, Building2, ShieldCheck, AlertCircle, Info } from 'lucide-react';
import { useNotificationStore } from '@app/stores/notification.store';
import {
  CODIGO_INQUILINO_POR_OMISION,
  DEV_PERSONAS,
  INQUILINOS_SUGERIDOS,
  normalizarCodigoInquilino,
} from '@domain/identity/constants/tenant.constants';

/**
 * Accesos rápidos de la pantalla de ingreso.
 *
 * En DESARROLLO son las personas sembradas por FS-25, con su contraseña: sirven para comparar qué
 * ve cada rol sin teclear nada.
 *
 * Fuera de desarrollo el panel está APAGADO salvo que el despliegue lo pida con
 * `VITE_SHOW_TEST_CREDENTIALS=true`, y aun entonces ofrece solo el administrador y **sin
 * contraseña**. La razón es concreta y medida: lo que se escribe aquí acaba dentro del JavaScript
 * servido, legible por cualquiera que abra el bundle. Rellenar inquilino y usuario es casi toda la
 * comodidad; la contraseña, que es lo único que protege, se teclea.
 *
 * El correo sale de `VITE_ADMIN_EMAIL` para que no quede clavado a un entorno.
 */
const MOSTRAR_ACCESOS_RAPIDOS =
  import.meta.env.DEV || import.meta.env.VITE_SHOW_TEST_CREDENTIALS === 'true';

// Se resuelve al RENDERIZAR el panel, no al importar el módulo. Referenciar `DEV_PERSONAS` en el
// ámbito superior la convertía en dependencia de carga: cualquier prueba que sustituya el módulo
// de constantes sin exponer ese símbolo falla al importar la pantalla, aunque no abra el panel.
const accesosRapidos = () =>
  import.meta.env.DEV
    ? DEV_PERSONAS
    : [
        {
          label: 'Administrador de UMS',
          tenantCode: 'EVOLITH',
          email: (import.meta.env.VITE_ADMIN_EMAIL as string | undefined) ?? 'admin@evolith.example',
          // Llega por `VITE_ADMIN_PASSWORD` en el build, nunca escrita aquí: una contraseña en el
          // fuente se commitea, y este repositorio pasa gitleaks en el gancho de pre-commit.
          //
          // Con la variable puesta, la contraseña VIAJA EN EL BUNDLE y es legible para cualquiera
          // que alcance el sitio y abra el JavaScript. Es una decisión consciente para un UAT
          // interno; sin la variable el panel no la muestra y solo rellena inquilino y usuario.
          password: (import.meta.env.VITE_ADMIN_PASSWORD as string | undefined) ?? '',
        },
      ];
import { ForgotPasswordForm } from '../components/ForgotPasswordForm';
import { authService } from '@app/identity/services/auth.service';
import { getHttpErrorMessage, getSupportReferenceId } from '@app/errors/http-error';
import { SignupForm } from '../components/SignupForm';
import { TenantSignupForm } from '../components/TenantSignupForm';
import { ProfileRequestForm } from '../components/ProfileRequestForm';
import { assetUrl } from '@shared/utils/asset-url';

type VistaDeAutenticacion = 'login' | 'forgot' | 'signup' | 'tenant-signup' | 'profile-request';

/** Los parámetros que trae el enlace del correo de restablecimiento (G-270). */
const PARAMETROS_DE_RECUPERACION = ['recuperar', 'codigo', 'correo', 'inquilino'] as const;

interface RecuperacionPorEnlace {
  codigo: string;
  correo: string;
  inquilino: string;
}

/**
 * Lee el enlace de restablecimiento que llega por correo: `?recuperar=1&inquilino=..&correo=..&codigo=..`.
 *
 * <p>Devuelve `null` si no es ese enlace, y también si viene sin código: sin código no hay nada
 * que canjear, y abrir el paso de canje vacío solo confundiría. Quien llegue así ve el ingreso
 * normal, desde donde puede pedir un código nuevo.</p>
 */
/** Quien llega por el enlace del correo entra directamente a recuperar; el resto, al ingreso. */
function vistaInicial(recuperacion: RecuperacionPorEnlace | null): VistaDeAutenticacion {
  return recuperacion ? 'forgot' : 'login';
}

function leerEnlaceDeRecuperacion(search: string): RecuperacionPorEnlace | null {
  const params = new URLSearchParams(search);
  if (params.get('recuperar') !== '1') return null;

  const codigo = params.get('codigo') ?? '';
  if (!codigo) return null;

  return {
    codigo,
    correo: params.get('correo') ?? '',
    inquilino: params.get('inquilino') || CODIGO_INQUILINO_POR_OMISION,
  };
}

/**
 * Borra de la barra de direcciones los parámetros del enlace de restablecimiento (G-270).
 *
 * <p>El código es credencial viva durante 15 minutos: no tiene por qué quedarse a la vista, en el
 * historial ni en lo que alguien copie de la barra. Se borran SOLO los nuestros; `redirect` y
 * `showSessionExpired` se conservan, que no son nuestros.</p>
 */
function useLimpiarEnlaceDeRecuperacion(
  activo: boolean,
  search: string,
  pathname: string,
  navigate: (destino: string, opciones: { replace: boolean }) => void
): void {
  useEffect(() => {
    if (!activo) return;

    const params = new URLSearchParams(search);
    if (params.get('recuperar') !== '1') return;

    PARAMETROS_DE_RECUPERACION.forEach(clave => params.delete(clave));
    const resto = params.toString();
    navigate(resto ? `${pathname}?${resto}` : pathname, { replace: true });
  }, [activo, search, pathname, navigate]);
}

export default function LoginScreen(): React.JSX.Element {
  const navigate = useNavigate();
  const location = useLocation();
  const { login, isAuthenticated } = useAuthStore();
  const { setLanguage } = useI18nStore();
  const { setDevUserId } = useDevToolsStore();
  const addNotification = useNotificationStore(state => state.addNotification);
  const t = useI18n();

  const [tenantCode, setTenantCode] = useState(CODIGO_INQUILINO_POR_OMISION);
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [attempts, setAttempts] = useState(0);
  const [lockoutUntil, setLockoutUntil] = useState<number | null>(null);
  const [showInfoPopup, setShowInfoPopup] = useState(false);
  const [recuperacionPorEnlace, setRecuperacionPorEnlace] = useState(() =>
    leerEnlaceDeRecuperacion(location.search)
  );
  const [authView, setAuthView] = useState<VistaDeAutenticacion>(
    vistaInicial(recuperacionPorEnlace)
  );

  const showSessionExpired =
    new URLSearchParams(location.search).get('showSessionExpired') === 'true' ||
    location.state?.showSessionExpired === true;
  const redirectTo = new URLSearchParams(location.search).get('redirect') || location.state?.from;

  /**
   * A dónde llevar al usuario tras autenticar, por orden de precedencia:
   *
   *   1. La ruta que pidió antes de que lo mandáramos a iniciar sesión. Es SUYA: mandarle a la
   *      página inicial del sistema le haría perder lo que estaba haciendo.
   *   2. `settings.ui.home_route`, la que publica el sistema (G-178).
   *   3. `/tenants`, el respaldo del producto.
   */
  const destinoTrasEntrar = () =>
    redirectTo ||
    useAuthStore.getState().user?.authorizationGraph?.settings?.ui?.home_route ||
    '/tenants';

  useEffect(() => {
    if (isAuthenticated) {
      navigate(destinoTrasEntrar(), { replace: true });
    }
    // `destinoTrasEntrar` lee el store en el momento de navegar, no en el del render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated, navigate, redirectTo]);

  useLimpiarEnlaceDeRecuperacion(
    Boolean(recuperacionPorEnlace),
    location.search,
    location.pathname,
    navigate
  );

  useEffect(() => {
    if (lockoutUntil && Date.now() < lockoutUntil) {
      const remaining = Math.ceil((lockoutUntil - Date.now()) / 1000);
      setError(`Demasiados intentos. Espere ${remaining} segundos`);
    } else if (lockoutUntil) {
      setLockoutUntil(null);
      setError('');
    }
  }, [lockoutUntil]);

  const handleLogin = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault();
      setError('');

      if (lockoutUntil && Date.now() < lockoutUntil) {
        return;
      }

      const codigoInquilino = normalizarCodigoInquilino(tenantCode);
      const trimmedUsername = username.trim();
      const trimmedPassword = password.trim();

      if (!codigoInquilino) {
        setError('Ingrese el código de su inquilino');
        return;
      }
      if (!trimmedUsername) {
        setError('Ingrese su correo electrónico');
        return;
      }
      if (!trimmedPassword) {
        setError('Ingrese su contraseña');
        return;
      }

      if (trimmedUsername.length < 2 || trimmedUsername.length > 100) {
        setError('Correo electrónico inválido');
        return;
      }

      setIsLoading(true);

      try {
        const data = await authService.login({
          tenantCode: codigoInquilino,
          username: trimmedUsername,
          password: trimmedPassword,
          rememberMe: false,
        });

        setAttempts(0);
        setDevUserId(data.userId);

        // ADR-0076: Detect browser timezone (D2) and resolve language (D3).
        // Browser timezone wins over tenant default; browser Accept-Language (already applied
        // server-side) is reflected in data.language which wins over tenant parameter default.
        const browserTimezone = detectBrowserTimezone(
          data.sessionParameters?.defaultTimezone ?? 'America/Lima'
        );
        const resolvedParams = data.sessionParameters
          ? { ...data.sessionParameters, defaultTimezone: browserTimezone }
          : null;

        login(
          {
            id: data.userId,
            username: data.username,
            email: data.email,
            role: data.role ?? '',
            tenantId: data.tenantId,
            tenantCode: data.tenantCode,
            tenantName: data.tenantName,
            profileId: data.profileId ?? undefined,
            sessionTrackingId: data.sessionTrackingId,
            permissions: data.permissions,
            token: data.token,
            isInternalAdmin: data.isInternalAdmin,
            crossTenantAccessEnabled: false,
            sessionParameters: resolvedParams,
            // El grafo viaja en la respuesta del login y hasta ahora se descartaba aquí: el usuario
            // se componía campo a campo y este quedaba fuera. Sin él, todo lo que el sistema publica
            // —menú, permisos, marca, tema, idioma, ajustes— no existía hasta el primer refresco de
            // token, que es el único sitio que sí lo guardaba.
            authorizationGraph: data.authorizationGraph ?? null,
          },
          data.expiresIn * 1000,
          data.refreshExpiresIn * 1000
        );

        // ADR-0076 D3: Initialize UI language from server-resolved language
        // (backend already applied Accept-Language > tenant param > default chain).
        const supportedLangs: SupportedLanguage[] = ['es', 'en'];
        const lang = data.language as SupportedLanguage;
        if (supportedLangs.includes(lang)) {
          setLanguage(lang);
        }

        addNotification({
          title: 'Bienvenido',
          message: `Sesión iniciada en ${data.tenantCode}`,
          type: 'success',
        });

        navigate(destinoTrasEntrar(), { replace: true });
      } catch (err) {
        const newAttempts = attempts + 1;
        setAttempts(newAttempts);

        if (newAttempts >= 5) {
          const lockDuration = Math.min(
            30 * 1000,
            10 * 1000 * Math.pow(2, Math.floor(newAttempts / 5))
          );
          setLockoutUntil(Date.now() + lockDuration);
          setError(`Demasiados intentos fallidos. Bloqueado por ${lockDuration / 1000} segundos`);
        } else {
          const supportReferenceId = getSupportReferenceId(err);
          const message = getHttpErrorMessage(
            err,
            'No pudimos iniciar sesión. Verifique sus credenciales.'
          );
          setError(supportReferenceId ? `${message} Referencia: ${supportReferenceId}` : message);
        }
      } finally {
        setIsLoading(false);
      }
    },
    [
      username,
      password,
      tenantCode,
      attempts,
      lockoutUntil,
      login,
      navigate,
      addNotification,
      redirectTo,
      setDevUserId,
    ]
  );

  if (isAuthenticated) {
    return null;
  }

  const isLocked = lockoutUntil !== null && Date.now() < lockoutUntil;

  // ── Registration options shown on the right panel ──────────────────────────
  const REGISTRATION_OPTIONS = [
    {
      id: 'tenant-signup' as const,
      icon: <Building2 className="w-6 h-6" />,
      color: 'text-m3-primary',
      bg: 'bg-m3-primary/10 border-m3-primary/20',
      activeBg: 'bg-m3-primary/15 border-m3-primary/40',
      title: 'Registrar empresa',
      description:
        'Registra tu empresa en UMS para iniciar el proceso de onboarding y obtener acceso al sistema.',
      cta: 'Registrar mi empresa',
    },
    {
      id: 'signup' as const,
      icon: <Key className="w-6 h-6" />,
      color: 'text-emerald-600',
      bg: 'bg-emerald-500/10 border-emerald-500/20',
      activeBg: 'bg-emerald-500/15 border-emerald-500/40',
      title: 'Crear usuario',
      description:
        'Solicita una cuenta de usuario en un tenant existente. El administrador aprobará tu acceso.',
      cta: 'Solicitar acceso',
    },
    {
      id: 'profile-request' as const,
      icon: <ShieldCheck className="w-6 h-6" />,
      color: 'text-sky-600',
      bg: 'bg-sky-500/10 border-sky-500/20',
      activeBg: 'bg-sky-500/15 border-sky-500/40',
      title: 'Solicitar perfil',
      description:
        'Solicita acceso a un perfil o rol dentro de tu tenant para obtener permisos en el sistema.',
      cta: 'Solicitar perfil',
    },
  ] as const;

  const isRegistrationView =
    authView === 'signup' || authView === 'tenant-signup' || authView === 'profile-request';
  const isForgotPasswordView = authView === 'forgot';
  const showLoginForm = authView === 'login';

  return (
    <div className="min-h-screen flex items-center justify-center p-4 bg-gradient-to-br from-m3-surface via-m3-surface-container/30 to-m3-surface-container/50">
      <div className="w-full max-w-4xl">
        {/* ── Brand header ──────────────────────────────────────────────────── */}
        <div className="text-center mb-6">
          <img
            src={assetUrl('brand/logo-evolith.png')}
            alt="EVOLITH"
            className="h-16 w-auto mx-auto mb-3 dark:hidden"
          />
          <img
            src={assetUrl('brand/logo-evolith-white.png')}
            alt="EVOLITH"
            className="h-16 w-auto mx-auto mb-3 hidden dark:block"
          />
          <p className="text-sm text-m3-secondary mt-0.5">User Management System</p>
        </div>

        {showSessionExpired && (
          <div className="mb-4 p-3 rounded-lg bg-amber-500/10 border border-amber-500/30 flex items-center gap-2 max-w-md mx-auto">
            <AlertCircle className="w-4 h-4 text-amber-500 flex-shrink-0" />
            <p className="text-xs text-amber-500">
              Su sesión ha expirado. Inicie sesión nuevamente.
            </p>
          </div>
        )}

        {/* ── Main two-column layout ─────────────────────────────────────────── */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 items-start">
          {/* ── LEFT: Login form (or forgot password) ─────────────────────── */}
          <M3Card
            variant="elevated"
            className="p-6 border border-m3-outline/25 bg-m3-surface-container/30"
          >
            {isForgotPasswordView ? (
              <ForgotPasswordForm
                onBack={() => {
                  setRecuperacionPorEnlace(null);
                  setAuthView('login');
                }}
                codigoInicial={recuperacionPorEnlace?.codigo}
                correoInicial={recuperacionPorEnlace?.correo}
                inquilinoInicial={recuperacionPorEnlace?.inquilino}
              />
            ) : isRegistrationView ? (
              // On mobile: show the selected form in the left card too
              <div className="lg:hidden">
                {authView === 'signup' && <SignupForm onBack={() => setAuthView('login')} />}
                {authView === 'tenant-signup' && (
                  <TenantSignupForm onBack={() => setAuthView('login')} />
                )}
                {authView === 'profile-request' && (
                  <ProfileRequestForm
                    onBack={() => setAuthView('login')}
                    onGoToLogin={() => setAuthView('login')}
                    onGoToSignup={() => setAuthView('signup')}
                  />
                )}
              </div>
            ) : null}

            {/* Show the login form only in the explicit login state */}
            {showLoginForm && (
              <div>
                <div className="flex items-center gap-2 mb-5">
                  <Key className="w-5 h-5 text-m3-primary" />
                  <h2 className="text-sm font-extrabold uppercase tracking-widest text-m3-on-surface">
                    Iniciar Sesión
                  </h2>
                </div>

                <form onSubmit={handleLogin} className="space-y-4">
                  <TenantSelect
                    label="Inquilino"
                    value={tenantCode}
                    onChange={setTenantCode}
                    sugerencias={INQUILINOS_SUGERIDOS}
                    placeholder="Ej.: EVOLITH"
                    helperText="Código de su organización, tal como se lo entregó su administrador"
                    disabled={isLoading || isLocked}
                    error={error && error.includes('inquilino') ? error : undefined}
                  />

                  <M3TextField
                    label="Correo electrónico"
                    required
                    value={username}
                    onChange={e => setUsername(e.target.value)}
                    placeholder="usuario@empresa.com"
                    autoComplete="email"
                    disabled={isLoading || isLocked}
                    error={
                      error &&
                      (error.includes('correo') ||
                        error.includes('usuario') ||
                        error.includes('email'))
                        ? error
                        : undefined
                    }
                  />

                  <M3TextField
                    label="Contraseña"
                    type="password"
                    required
                    value={password}
                    onChange={e => setPassword(e.target.value)}
                    placeholder="••••••••"
                    autoComplete="current-password"
                    disabled={isLoading || isLocked}
                    error={error && error.includes('contraseña') ? error : undefined}
                  />

                  {error && !error.includes('usuario') && !error.includes('contraseña') && (
                    <div className="p-3 rounded-lg bg-m3-error-container/30 border border-m3-error/20 flex items-center gap-2">
                      <AlertCircle className="w-4 h-4 text-m3-error flex-shrink-0" />
                      <p className="text-xs text-m3-error">{error}</p>
                    </div>
                  )}

                  <M3Button
                    variant="filled"
                    className="w-full"
                    type="submit"
                    disabled={isLoading || isLocked}
                  >
                    {isLoading ? (
                      <span className="flex items-center gap-2">
                        <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24">
                          <circle
                            className="opacity-25"
                            cx="12"
                            cy="12"
                            r="10"
                            stroke="currentColor"
                            strokeWidth="4"
                            fill="none"
                          />
                          <path
                            className="opacity-75"
                            fill="currentColor"
                            d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
                          />
                        </svg>
                        Verificando...
                      </span>
                    ) : isLocked ? (
                      'Bloqueado'
                    ) : (
                      'Ingresar'
                    )}
                  </M3Button>

                  <div className="text-center pt-1">
                    <button
                      type="button"
                      onClick={() => setAuthView('forgot')}
                      className="text-xs text-m3-primary/70 hover:text-m3-primary transition-colors"
                    >
                      ¿Olvidaste tu contraseña?
                    </button>
                  </div>
                </form>
              </div>
            )}
          </M3Card>

          {/* ── RIGHT: Registration options or selected form ───────────────── */}
          <div className="space-y-3">
            {/* On desktop: show the active form in the right column */}
            {isRegistrationView && (
              <M3Card
                variant="elevated"
                className="p-6 border border-m3-outline/25 bg-m3-surface-container/30 hidden lg:block animate-fadeIn"
              >
                {authView === 'signup' && <SignupForm onBack={() => setAuthView('login')} />}
                {authView === 'tenant-signup' && (
                  <TenantSignupForm onBack={() => setAuthView('login')} />
                )}
                {authView === 'profile-request' && (
                  <ProfileRequestForm
                    onBack={() => setAuthView('login')}
                    onGoToLogin={() => setAuthView('login')}
                    onGoToSignup={() => setAuthView('signup')}
                  />
                )}
              </M3Card>
            )}

            {/* Registration options — always shown or collapsed */}
            <div>
              {!isRegistrationView && (
                <p className="text-[11px] font-semibold uppercase tracking-wider text-m3-secondary mb-2 px-1">
                  ¿Qué necesitas?
                </p>
              )}
              <div className="space-y-2">
                {REGISTRATION_OPTIONS.map(opt => {
                  const isActive = authView === opt.id;
                  return (
                    <button
                      key={opt.id}
                      type="button"
                      onClick={() => setAuthView(isActive ? 'login' : opt.id)}
                      className={[
                        'w-full flex items-start gap-4 p-4 rounded-2xl border text-left transition-all duration-200',
                        isActive
                          ? opt.activeBg
                          : 'border-m3-outline/20 bg-m3-surface-container/30 hover:border-m3-outline/40 hover:bg-m3-surface-container/50',
                      ].join(' ')}
                    >
                      <div
                        className={`p-2.5 rounded-xl border ${isActive ? opt.activeBg : opt.bg} ${opt.color} shrink-0`}
                      >
                        {opt.icon}
                      </div>
                      <div className="min-w-0 flex-1">
                        <p
                          className={`text-sm font-semibold ${isActive ? opt.color : 'text-m3-on-surface'}`}
                        >
                          {opt.title}
                        </p>
                        <p className="text-xs text-m3-secondary mt-0.5 leading-relaxed">
                          {opt.description}
                        </p>
                        <span
                          className={`inline-flex items-center gap-1 mt-2 text-[11px] font-medium ${isActive ? opt.color : 'text-m3-secondary/70'}`}
                        >
                          {isActive ? '← Volver al login' : `→ ${opt.cta}`}
                        </span>
                      </div>
                    </button>
                  );
                })}
              </div>
            </div>
          </div>
        </div>

        {/* ── Footer ────────────────────────────────────────────────────────── */}
        <div className="mt-5 flex items-center justify-center gap-4">
          {/* Solo en desarrollo. El popup expone credenciales ESCRITAS EN EL CÓDIGO
              (`DEV_PERSONAS`), así que fuera de desarrollo hace dos daños a la vez: anuncia
              usuarios y contraseñas en la puerta de entrada, y —como no salen de la base— miente
              en cuanto el entorno deja de usar el sembrador de desarrollo. Medido el 2026-08-15
              en UAT: el autorrelleno metía `Evolith.Dev.2026` contra una base cuyo administrador
              tiene otra contraseña, y el login fallaba con «verifique sus credenciales» sobre una
              credencial que el usuario no había escrito. */}
          {MOSTRAR_ACCESOS_RAPIDOS && (
            <>
              <button
                type="button"
                onClick={() => setShowInfoPopup(true)}
                className="flex items-center gap-1 text-[10px] text-m3-secondary/60 hover:text-m3-secondary transition-colors"
              >
                <Info className="w-3 h-3" />
                <span>{import.meta.env.DEV ? 'Datos de prueba' : 'Acceso rápido'}</span>
              </button>
              <span className="text-[10px] text-m3-secondary/30">·</span>
            </>
          )}
          <p className="text-[10px] text-m3-secondary/50">
            Sistema protegido contra accesos no autorizados
          </p>
        </div>

        {/* ── Dev credentials popup ──────────────────────────────────────────── */}
        {/* La guarda de entorno va también AQUÍ, y no solo en el botón que lo abre. Ocultar el
            disparador basta para la interfaz, pero deja el bloque en el código y con él la
            referencia a `DEV_PERSONAS`: las contraseñas seguían viajando dentro del JavaScript
            servido, legibles para cualquiera que abriese el bundle. Con `import.meta.env.DEV`
            en la condición, el empaquetador sustituye la constante por `false`, elimina la rama
            muerta y la lista deja de exportarse al artefacto de producción. */}
        {MOSTRAR_ACCESOS_RAPIDOS && showInfoPopup && (
          <>
            <div
              className="fixed inset-0 z-50 bg-black/50 backdrop-blur-sm"
              onClick={() => setShowInfoPopup(false)}
            />
            <div className="fixed z-50 left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 w-full max-w-sm p-4 rounded-xl border border-m3-outline/30 bg-m3-surface-container/95 shadow-2xl">
              <div className="flex items-center justify-between mb-3 pb-2 border-b border-m3-outline/20">
                <div className="flex items-center gap-2">
                  <ShieldCheck className="w-4 h-4 text-m3-primary" />
                  <p className="text-sm font-bold text-m3-on-surface">Credenciales de Prueba</p>
                </div>
                <button
                  type="button"
                  onClick={() => setShowInfoPopup(false)}
                  className="text-m3-secondary hover:text-m3-on-surface transition-colors"
                >
                  ✕
                </button>
              </div>
              <div className="space-y-2 text-xs max-h-80 overflow-y-auto">
                <p className="text-[11px] text-m3-secondary mb-1">
                  Usuarios de prueba sembrados. Haz clic en uno para autocompletar el formulario.
                </p>
                {accesosRapidos().map(p => {
                  return (
                    <button
                      key={p.email}
                      type="button"
                      onClick={() => {
                        setTenantCode(p.tenantCode);
                        setUsername(p.email);
                        setPassword(p.password);
                        setShowInfoPopup(false);
                      }}
                      className="w-full text-left p-2 rounded-lg bg-m3-surface/50 border border-m3-outline/10 hover:border-m3-primary/40 hover:bg-m3-primary/5 transition-colors"
                    >
                      <p className="font-bold text-m3-primary">{p.label}</p>
                      <p className="text-m3-secondary font-mono">Tenant: {p.tenantCode}</p>
                      <p className="text-m3-secondary font-mono break-all">Usuario: {p.email}</p>
                      {p.password ? (
                        <p className="text-m3-secondary font-mono">Password: {p.password}</p>
                      ) : (
                        <p className="text-m3-secondary/70 italic">
                          La contraseña depende del entorno: escríbela tú
                        </p>
                      )}
                    </button>
                  );
                })}
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
