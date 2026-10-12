import React from 'react';
import ReactDOM from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import App from './App.tsx';
import './index.css';
import { useDevToolsStore } from './application/stores/devTools.store';
import { useI18nStore } from './application/stores/i18n.store';
import { useAuthStore } from './application/stores/auth.store';
import { configureRequestContext } from './infrastructure/http/request-context';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      refetchOnWindowFocus: false,
      retry: 1,
    },
  },
});

const requestContextProvider = () => {
  const { user } = useAuthStore.getState();

  if (import.meta.env.DEV) {
    const { devUserId, devLanguage } = useDevToolsStore.getState();
    useI18nStore.getState().setLanguage(devLanguage);
    return { userId: devUserId, language: devLanguage, tenantId: user?.tenantId };
  }

  // Producción (G-026): el contexto de tenant proviene de la sesión autenticada
  // (`user.tenantId`), no de un tenant por defecto. La identidad del usuario la
  // resuelve el backend desde el JWT; no se inyecta un X-User-Id de desarrollo.
  return { language: useI18nStore.getState().language, tenantId: user?.tenantId };
};

configureRequestContext(requestContextProvider);

function LocaleSync() {
  const lang = useI18nStore(s => s.language);
  React.useEffect(() => {
    document.documentElement.lang = lang;
  }, [lang]);
  return null;
}

window.addEventListener('unhandledrejection', event => {
  if (import.meta.env.DEV) {
    console.error('Unhandled promise rejection:', event.reason);
  }
  event.preventDefault();
});

async function enableMocking() {
  if (import.meta.env.DEV && import.meta.env.VITE_USE_MOCKS === 'true') {
    const { worker } = await import('./test/mocks/browser');
    return worker.start({
      onUnhandledRequest: 'bypass',
    });
  }
}

const rootEl = document.getElementById('root');
if (!rootEl) {
  throw new Error(
    '[UMS] Root element #root not found in index.html. ' +
      'Check that public/index.html contains <div id="root"></div>.'
  );
}

enableMocking().then(() => {
  ReactDOM.createRoot(rootEl).render(
    <React.StrictMode>
      <QueryClientProvider client={queryClient}>
        <LocaleSync />
        <App />
      </QueryClientProvider>
    </React.StrictMode>
  );
});
