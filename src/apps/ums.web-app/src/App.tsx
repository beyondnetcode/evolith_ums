import { useEffect, lazy, Suspense } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router';
import { MainLayout } from './presentation/shared/layouts/MainLayout';
import { AppErrorBoundary } from './presentation/shared/components/ErrorBoundary';
import { RouteLoader } from './presentation/shared/components/RouteLoader';
import { ProtectedRoute } from './presentation/shared/components/ProtectedRoute';
import { AccessGuard } from './presentation/shared/components/AccessGuard';
import { useThemeStore } from './application/stores/theme.store';
import { ShieldCheck } from 'lucide-react';

const UnauthorizedFallback = () => (
  <div className="flex flex-col items-center justify-center h-full p-8 text-m3-secondary animate-fadeIn">
    <ShieldCheck className="w-16 h-16 mb-4 opacity-30" />
    <h2 className="text-xl font-bold mb-2">Acceso Denegado</h2>
    <p className="text-sm opacity-70">
      No tienes permisos para visualizar este módulo o la función ha sido deshabilitada.
    </p>
  </div>
);

const TenantDashboardScreen = lazy(
  () => import('./presentation/identity/tenant/screens/TenantDashboardScreen')
);
const UserAccountDashboardScreen = lazy(
  () => import('./presentation/identity/user-account/screens/UserAccountDashboardScreen')
);
const DelegationDashboardScreen = lazy(
  () => import('./presentation/identity/delegation/screens/DelegationDashboardScreen')
);
const SystemSuiteDashboardScreen = lazy(
  () => import('./presentation/authorization/system-suite/screens/SystemSuiteDashboardScreen')
);
const PermissionTemplateDashboardScreen = lazy(
  () =>
    import(
      './presentation/authorization/permission-template/screens/PermissionTemplateDashboardScreen'
    )
);
const ProfileDashboardScreen = lazy(
  () => import('./presentation/authorization/profile/screens/ProfileDashboardScreen')
);
const FeatureFlagDashboardScreen = lazy(
  () => import('./presentation/configuration/feature-flag/screens/FeatureFlagDashboardScreen')
);
const AppConfigurationDashboardScreen = lazy(
  () =>
    import('./presentation/configuration/app-configuration/screens/AppConfigurationDashboardScreen')
);
const GlobalAppConfigurationDashboardScreen = lazy(
  () =>
    import(
      './presentation/configuration/app-configuration/screens/GlobalAppConfigurationDashboardScreen'
    )
);
const ParameterCatalogScreen = lazy(
  () => import('./presentation/configuration/parameter-catalog/screens/ParameterCatalogScreen')
);
const ProfileScreen = lazy(() => import('./presentation/identity/profile/screens/ProfileScreen'));
const LoginScreen = lazy(() => import('./presentation/identity/profile/screens/LoginScreen'));

export default function App() {
  const isDarkMode = useThemeStore(state => state.isDarkMode);

  // C-2: DOM manipulation moved from store to presentation layer
  //
  // La marca va en <html>, NO en <body>. El bloque de tokens se declara como
  // `.dark, :root[data-theme="dark"]`, es decir, escrito para que lo lleve la raiz. Con la clase
  // en <body> ocurria que el propio <body> es a la vez quien DEFINE los tokens (por casar con
  // `.dark`) y quien los CONSUME —`body { background-color: hsl(var(--m3-surface)) }`—, y en la
  // carga inicial el fondo se quedaba con el valor claro: medido el 2026-09-01 sobre el UAT,
  // `rgb(252,250,255)` con `body.class="dark"` y `--m3-surface` ya en `214 17% 8%`. Se veia como
  // una franja clara en el borde de la pagina, detras de una interfaz por lo demas oscura.
  //
  // En <html> el token se define un nivel por encima de quien lo usa y <body> lo recibe heredado,
  // que es la via que si se invalida al cambiar la clase.
  useEffect(() => {
    document.documentElement.classList.toggle('dark', isDarkMode);
  }, [isDarkMode]);

  return (
    <div className={isDarkMode ? 'dark' : ''}>
      <AppErrorBoundary>
        {/* `import.meta.env.BASE_URL` es el `base` con el que Vite construyó el bundle.
            Vale '/' en el despliegue de contenedor (nginx sirve en la raíz) y '/ums_web/'
            cuando el build se destina al directorio virtual de IIS. Sin pasarlo, bajo un
            subpath el router interpretaría el prefijo como parte de la ruta y ninguna
            pantalla resolvería. */}
        <BrowserRouter basename={import.meta.env.BASE_URL}>
          <MainLayout>
            <Suspense fallback={<RouteLoader />}>
              <Routes>
                <Route path="/" element={<Navigate to="/login" replace />} />
                <Route path="/login" element={<LoginScreen />} />
                <Route
                  path="/tenants"
                  element={
                    <ProtectedRoute>
                      <AccessGuard
                        moduleCode="IDM"
                        menuCode="TENANTS"
                        fallback={<UnauthorizedFallback />}
                      >
                        <TenantDashboardScreen />
                      </AccessGuard>
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="/users"
                  element={
                    <ProtectedRoute>
                      <AccessGuard
                        moduleCode="IDM"
                        menuCode="USERS"
                        fallback={<UnauthorizedFallback />}
                      >
                        <UserAccountDashboardScreen />
                      </AccessGuard>
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="/delegations"
                  element={
                    <ProtectedRoute>
                      <AccessGuard
                        moduleCode="IDM"
                        menuCode="DELEGATIONS"
                        fallback={<UnauthorizedFallback />}
                      >
                        <DelegationDashboardScreen />
                      </AccessGuard>
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="/system-suites"
                  element={
                    <ProtectedRoute>
                      <AccessGuard
                        moduleCode="AUTH"
                        menuCode="SYSTEM_SUITES"
                        fallback={<UnauthorizedFallback />}
                      >
                        <SystemSuiteDashboardScreen />
                      </AccessGuard>
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="/permission-templates"
                  element={
                    <ProtectedRoute>
                      <AccessGuard
                        moduleCode="AUTH"
                        menuCode="PERMISSION_TEMPLATES"
                        fallback={<UnauthorizedFallback />}
                      >
                        <PermissionTemplateDashboardScreen />
                      </AccessGuard>
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="/profiles"
                  element={
                    <ProtectedRoute>
                      <AccessGuard
                        moduleCode="AUTH"
                        menuCode="PROFILES"
                        fallback={<UnauthorizedFallback />}
                      >
                        <ProfileDashboardScreen />
                      </AccessGuard>
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="/feature-flags"
                  element={
                    <ProtectedRoute>
                      <AccessGuard
                        moduleCode="SYS"
                        menuCode="FEATURE_FLAGS"
                        fallback={<UnauthorizedFallback />}
                      >
                        <FeatureFlagDashboardScreen />
                      </AccessGuard>
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="/app-configurations"
                  element={
                    <ProtectedRoute>
                      <AccessGuard
                        moduleCode="SYS"
                        menuCode="APP_CONFIG"
                        fallback={<UnauthorizedFallback />}
                      >
                        <AppConfigurationDashboardScreen />
                      </AccessGuard>
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="/app-configurations/global"
                  element={
                    <ProtectedRoute>
                      <AccessGuard
                        moduleCode="SYS"
                        menuCode="APP_CONFIG"
                        fallback={<UnauthorizedFallback />}
                      >
                        <GlobalAppConfigurationDashboardScreen />
                      </AccessGuard>
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="/parameter-catalog"
                  element={
                    <ProtectedRoute>
                      <AccessGuard
                        moduleCode="SYS"
                        menuCode="PARAM_CATALOG"
                        fallback={<UnauthorizedFallback />}
                      >
                        <ParameterCatalogScreen />
                      </AccessGuard>
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="/profile"
                  element={
                    <ProtectedRoute>
                      <ProfileScreen />
                    </ProtectedRoute>
                  }
                />
                <Route path="*" element={<Navigate to="/login" replace />} />
              </Routes>
            </Suspense>
          </MainLayout>
        </BrowserRouter>
      </AppErrorBoundary>
    </div>
  );
}
