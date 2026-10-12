import React, { useState } from 'react';
import { ArrowLeft, Check, Mail, User, UserPlus } from 'lucide-react';
import { M3Button } from '@shared/components/M3Button';
import { M3TextField } from '@shared/components/M3TextField';
import { TenantSelect } from '@shared/components/TenantSelect';
import {
  CODIGO_INQUILINO_POR_OMISION,
  INQUILINOS_SUGERIDOS,
  normalizarCodigoInquilino,
} from '@domain/identity/constants/tenant.constants';
import authService from '@infra/identity/services/auth.service';

interface SignupFormProps {
  onBack: () => void;
}

export const SignupForm: React.FC<SignupFormProps> = ({ onBack }) => {
  const [tenantCode, setTenantCode] = useState(CODIGO_INQUILINO_POR_OMISION);
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [result, setResult] = useState<{ message: string; userAccountId: string | null } | null>(
    null
  );

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (!displayName.trim()) {
      setError('Ingrese su nombre');
      return;
    }
    if (!email.trim()) {
      setError('Ingrese su correo electrónico');
      return;
    }
    if (password.length < 12) {
      setError('La contraseña debe tener al menos 12 caracteres');
      return;
    }
    if (password !== confirmPassword) {
      setError('Las contraseñas no coinciden');
      return;
    }

    const codigoInquilino = normalizarCodigoInquilino(tenantCode);
    if (!codigoInquilino) {
      setError('Ingrese el código de su inquilino');
      return;
    }

    setIsLoading(true);
    try {
      const response = await authService.signupUser({
        tenantCode: codigoInquilino,
        displayName: displayName.trim(),
        email: email.trim().toLowerCase(),
        password,
      });

      setResult({
        message: response.message,
        userAccountId: response.userAccountId,
      });
    } catch {
      setError('No se pudo registrar la solicitud. Intente nuevamente.');
    } finally {
      setIsLoading(false);
    }
  };

  if (result) {
    return (
      <div className="space-y-4 animate-fadeIn">
        <div className="p-4 rounded-xl border border-emerald-500/30 bg-emerald-500/5 space-y-3">
          <div className="flex items-center gap-2">
            <Check className="w-4 h-4 text-emerald-500" />
            <span className="text-sm font-semibold text-emerald-600 dark:text-emerald-400">
              Solicitud enviada
            </span>
          </div>
          <p className="text-xs text-m3-secondary leading-relaxed">{result.message}</p>
          <p className="text-[10px] text-m3-secondary/70">
            El tenant seleccionado revisará su solicitud y le notificará cuando sea aprobada.
          </p>
        </div>

        <M3Button variant="outlined" className="w-full" onClick={onBack}>
          <ArrowLeft className="w-4 h-4 mr-2" />
          Volver al inicio de sesión
        </M3Button>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4 animate-fadeIn">
      <div className="flex items-center gap-2 text-m3-primary mb-2">
        <UserPlus className="w-5 h-5" />
        <h2 className="text-sm font-extrabold uppercase tracking-widest text-m3-on-surface">
          Solicitar acceso
        </h2>
      </div>

      <p className="text-xs text-m3-secondary leading-relaxed">
        Si ya existe su empresa en UMS, escriba su código de inquilino y envíe la solicitud de
        acceso.
      </p>

      <TenantSelect
        label="Inquilino"
        value={tenantCode}
        onChange={setTenantCode}
        sugerencias={INQUILINOS_SUGERIDOS}
        placeholder="Ej.: EVOLITH"
        helperText="Código de la organización a la que solicita acceso"
        disabled={isLoading}
      />

      <M3TextField
        label="Nombre"
        required
        value={displayName}
        onChange={e => {
          setDisplayName(e.target.value);
          setError('');
        }}
        placeholder="Nombre y apellido"
        autoComplete="name"
        disabled={isLoading}
        icon={<User className="w-4 h-4" />}
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

      <M3TextField
        label="Contraseña"
        type="password"
        required
        value={password}
        onChange={e => {
          setPassword(e.target.value);
          setError('');
        }}
        placeholder="Mínimo 12 caracteres"
        autoComplete="new-password"
        disabled={isLoading}
      />

      <M3TextField
        label="Confirmar contraseña"
        type="password"
        required
        value={confirmPassword}
        onChange={e => {
          setConfirmPassword(e.target.value);
          setError('');
        }}
        placeholder="Repita la contraseña"
        autoComplete="new-password"
        disabled={isLoading}
      />

      {error && <p className="text-xs text-m3-error px-1">{error}</p>}

      <M3Button variant="filled" className="w-full" type="submit" disabled={isLoading}>
        {isLoading ? 'Enviando...' : 'Solicitar acceso'}
      </M3Button>

      <button
        type="button"
        onClick={onBack}
        className="w-full flex items-center justify-center gap-1.5 text-xs text-m3-secondary hover:text-m3-primary transition-colors py-1"
      >
        <ArrowLeft className="w-3.5 h-3.5" />
        Volver al inicio de sesión
      </button>
    </form>
  );
};
