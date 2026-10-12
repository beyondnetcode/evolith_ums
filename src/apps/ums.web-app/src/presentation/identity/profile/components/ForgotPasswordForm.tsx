import React, { useState } from 'react';
import { KeyRound, Mail, ArrowLeft, ShieldAlert, Lock, CheckCircle2 } from 'lucide-react';
import { M3Button } from '@shared/components/M3Button';
import { M3TextField } from '@shared/components/M3TextField';
import { TenantSelect } from '@shared/components/TenantSelect';
import {
  CODIGO_INQUILINO_POR_OMISION,
  INQUILINOS_SUGERIDOS,
  normalizarCodigoInquilino,
} from '@domain/identity/constants/tenant.constants';
import authService from '@infra/identity/services/auth.service';

interface ForgotPasswordFormProps {
  onBack: () => void;
  /**
   * Valores que llegan por el enlace del correo de restablecimiento (G-270). Con el código
   * presente el formulario ARRANCA en el canje: quien viene del correo ya pidió el código y
   * hacerle repetir la solicitud lo invalidaría, porque solo puede haber uno vivo por cuenta.
   */
  codigoInicial?: string;
  correoInicial?: string;
  inquilinoInicial?: string;
}

/**
 * Recuperación de contraseña en dos pasos (G-188).
 *
 * La solicitud ya no devuelve credencial alguna: el código de restablecimiento llega al buzón
 * del titular y se canjea aquí. Mientras no se canjee, la contraseña anterior sigue sirviendo,
 * de modo que una solicitud ajena no deja a nadie fuera.
 */
type Paso = 'solicitud' | 'canje' | 'listo';

/** Espejo de `MinPasswordLength` del servidor (`ResetPasswordCommandValidator`). */
const LONGITUD_MINIMA_CLAVE = 12;

export const ForgotPasswordForm: React.FC<ForgotPasswordFormProps> = ({
  onBack,
  codigoInicial,
  correoInicial,
  inquilinoInicial,
}) => {
  const vieneDelCorreo = Boolean(codigoInicial);

  const [paso, setPaso] = useState<Paso>(vieneDelCorreo ? 'canje' : 'solicitud');
  const [tenantCode, setTenantCode] = useState(inquilinoInicial || CODIGO_INQUILINO_POR_OMISION);
  const [email, setEmail] = useState(correoInicial ?? '');
  const [token, setToken] = useState(codigoInicial ?? '');
  const [nuevaClave, setNuevaClave] = useState('');
  const [confirmacion, setConfirmacion] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [mensaje, setMensaje] = useState(
    vieneDelCorreo ? 'Su código ya está puesto. Defina la contraseña nueva.' : ''
  );

  const handleSolicitud = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (!email.trim()) {
      setError('Ingrese su correo electrónico');
      return;
    }

    const codigoInquilino = normalizarCodigoInquilino(tenantCode);
    if (!codigoInquilino) {
      setError('Ingrese el código de su inquilino');
      return;
    }

    setIsLoading(true);
    try {
      const response = await authService.forgotPassword({
        tenantCode: codigoInquilino,
        email: email.trim().toLowerCase(),
      });
      setMensaje(response.message);
      setPaso('canje');
    } catch {
      setError('Error al procesar la solicitud. Intente nuevamente.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleCanje = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (!token.trim()) {
      setError('Ingrese el código que recibió por correo');
      return;
    }
    if (nuevaClave !== confirmacion) {
      setError('Las contraseñas no coinciden');
      return;
    }
    // El servidor exige el mismo mínimo (`ResetPasswordCommandValidator`). Comprobarlo aquí no lo
    // sustituye —la validación que cuenta es la del servidor—, pero evita gastar el código de un
    // solo uso en un intento que iba a rechazarse: si el canje falla, el código ya no sirve.
    if (nuevaClave.length < LONGITUD_MINIMA_CLAVE) {
      setError(`La contraseña debe tener al menos ${LONGITUD_MINIMA_CLAVE} caracteres`);
      return;
    }

    setIsLoading(true);
    try {
      const response = await authService.resetPassword({
        token: token.trim(),
        newPassword: nuevaClave,
      });
      setMensaje(response.message);
      setPaso('listo');
    } catch {
      // El servidor responde igual ante código inexistente, vencido o ya usado: no hay más
      // detalle que dar, y darlo revelaría qué códigos existen.
      setError('El código no es válido o ha expirado. Solicite uno nuevo.');
    } finally {
      setIsLoading(false);
    }
  };

  const spinner = (
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
  );

  const botonVolver = (
    <button
      type="button"
      onClick={onBack}
      className="w-full flex items-center justify-center gap-1.5 text-xs text-m3-secondary hover:text-m3-primary transition-colors py-1"
    >
      <ArrowLeft className="w-3.5 h-3.5" />
      Volver al inicio de sesión
    </button>
  );

  // ── Contraseña restablecida ──────────────────────────────────────────────
  if (paso === 'listo') {
    return (
      <div className="space-y-4 animate-fadeIn">
        <div className="p-4 rounded-xl border border-emerald-500/30 bg-emerald-500/5 space-y-3">
          <div className="flex items-center gap-2">
            <CheckCircle2 className="w-4 h-4 text-emerald-500" />
            <span className="text-sm font-semibold text-emerald-600 dark:text-emerald-400">
              Contraseña actualizada
            </span>
          </div>
          <p className="text-xs text-m3-secondary leading-relaxed">{mensaje}</p>
        </div>

        <M3Button variant="outlined" className="w-full" onClick={onBack}>
          <ArrowLeft className="w-4 h-4 mr-2" />
          Volver al inicio de sesión
        </M3Button>
      </div>
    );
  }

  // ── Canje del código ─────────────────────────────────────────────────────
  if (paso === 'canje') {
    return (
      <form onSubmit={handleCanje} className="space-y-4 animate-fadeIn">
        <div className="p-4 rounded-xl border border-emerald-500/30 bg-emerald-500/5 space-y-2">
          <div className="flex items-center gap-2">
            <ShieldAlert className="w-4 h-4 text-emerald-500" />
            <span className="text-sm font-semibold text-emerald-600 dark:text-emerald-400">
              {vieneDelCorreo ? 'Código recibido' : 'Solicitud procesada'}
            </span>
          </div>
          <p className="text-xs text-m3-secondary leading-relaxed">{mensaje}</p>
        </div>

        <M3TextField
          label="Código de restablecimiento"
          required
          value={token}
          onChange={e => {
            setToken(e.target.value);
            setError('');
          }}
          placeholder="Péguelo tal como llegó a su correo"
          autoComplete="one-time-code"
          disabled={isLoading}
          icon={<KeyRound className="w-4 h-4" />}
        />

        <M3TextField
          label="Nueva contraseña"
          type="password"
          required
          value={nuevaClave}
          onChange={e => {
            setNuevaClave(e.target.value);
            setError('');
          }}
          autoComplete="new-password"
          disabled={isLoading}
          icon={<Lock className="w-4 h-4" />}
        />

        <M3TextField
          label="Confirme la nueva contraseña"
          type="password"
          required
          value={confirmacion}
          onChange={e => {
            setConfirmacion(e.target.value);
            setError('');
          }}
          autoComplete="new-password"
          disabled={isLoading}
          icon={<Lock className="w-4 h-4" />}
        />

        {error && <p className="text-xs text-m3-error px-1">{error}</p>}

        <M3Button variant="filled" className="w-full" type="submit" disabled={isLoading}>
          {isLoading ? (
            <span className="flex items-center gap-2">
              {spinner}
              Procesando...
            </span>
          ) : (
            'Restablecer contraseña'
          )}
        </M3Button>

        {botonVolver}
      </form>
    );
  }

  // ── Solicitud ────────────────────────────────────────────────────────────
  return (
    <form onSubmit={handleSolicitud} className="space-y-4 animate-fadeIn">
      <div className="flex items-center gap-2 text-m3-primary mb-2">
        <KeyRound className="w-5 h-5" />
        <h2 className="text-sm font-extrabold uppercase tracking-widest text-m3-on-surface">
          Recuperar Contraseña
        </h2>
      </div>

      <p className="text-xs text-m3-secondary leading-relaxed">
        Ingrese el código de su inquilino y su correo electrónico. Si existe una cuenta asociada,
        recibirá un código de un solo uso para definir una contraseña nueva. Su contraseña actual
        seguirá siendo válida hasta que lo use.
      </p>

      <TenantSelect
        label="Inquilino"
        value={tenantCode}
        onChange={setTenantCode}
        sugerencias={INQUILINOS_SUGERIDOS}
        placeholder="Ej.: EVOLITH"
        helperText="Código de su organización, tal como se lo entregó su administrador"
        disabled={isLoading}
      />

      <M3TextField
        label="Correo electrónico"
        type="email"
        required
        value={email}
        onChange={e => {
          setEmail(e.target.value);
          setError('');
        }}
        placeholder="usuario@empresa.com"
        autoComplete="email"
        disabled={isLoading}
        icon={<Mail className="w-4 h-4" />}
      />

      {error && <p className="text-xs text-m3-error px-1">{error}</p>}

      <M3Button variant="filled" className="w-full" type="submit" disabled={isLoading}>
        {isLoading ? (
          <span className="flex items-center gap-2">
            {spinner}
            Procesando...
          </span>
        ) : (
          'Enviar instrucciones'
        )}
      </M3Button>

      {/* Quien YA tiene un código no debe pasar por aquí: pedir uno nuevo invalida el anterior
          —solo puede haber un secreto vivo por usuario—. Sin esta salida, el administrador de un
          despliegue recién arrancado destruiría su propio código de arranque al intentar usarlo. */}
      <button
        type="button"
        onClick={() => {
          setError('');
          setMensaje('');
          setPaso('canje');
        }}
        disabled={isLoading}
        className="w-full text-xs text-m3-primary hover:underline disabled:opacity-50"
      >
        Ya tengo un código
      </button>

      {botonVolver}
    </form>
  );
};
