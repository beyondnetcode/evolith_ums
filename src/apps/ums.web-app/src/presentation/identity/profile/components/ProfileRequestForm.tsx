import React, { useState } from 'react';
import { ArrowLeft, Shield, ShieldCheck, LogIn, UserPlus, Info } from 'lucide-react';
import { M3Button } from '@shared/components/M3Button';
import { TenantSelect } from '@shared/components/TenantSelect';
import {
  CODIGO_INQUILINO_POR_OMISION,
  INQUILINOS_SUGERIDOS,
} from '@domain/identity/constants/tenant.constants';

interface ProfileRequestFormProps {
  onBack: () => void;
  onGoToLogin?: () => void;
  onGoToSignup?: () => void;
}

export const ProfileRequestForm: React.FC<ProfileRequestFormProps> = ({
  onBack,
  onGoToLogin,
  onGoToSignup,
}) => {
  const [tenantCode, setTenantCode] = useState(CODIGO_INQUILINO_POR_OMISION);
  const [step, setStep] = useState<'info' | 'has-account' | 'no-account'>('info');

  if (step === 'has-account') {
    const sugerida = INQUILINOS_SUGERIDOS.find(s => s.code === tenantCode);
    // El nombre solo se conoce si el código está entre las sugerencias; si no, se muestra el
    // código, que es lo único que el usuario tecleó y lo único que la API entiende.
    const nombreInquilino = sugerida?.name ?? tenantCode;
    return (
      <div className="space-y-4 animate-fadeIn">
        <div className="flex items-center gap-2 text-m3-primary mb-2">
          <Shield className="w-5 h-5" />
          <h2 className="text-sm font-extrabold uppercase tracking-widest text-m3-on-surface">
            Solicitar perfil
          </h2>
        </div>

        <div className="p-4 rounded-xl border border-m3-primary/20 bg-m3-primary/5 space-y-2">
          <div className="flex items-start gap-2">
            <Info className="w-4 h-4 text-m3-primary shrink-0 mt-0.5" />
            <div className="space-y-1">
              <p className="text-xs font-semibold text-m3-primary">¿Cómo solicitar un perfil?</p>
              <ol className="text-xs text-m3-secondary space-y-1 list-decimal list-inside">
                <li>
                  Inicia sesión en tu inquilino <strong>{tenantCode}</strong>
                </li>
                <li>
                  Ve a <strong>Autorización → Perfiles</strong>
                </li>
                <li>
                  Haz clic en <strong>"Solicitar acceso"</strong> y elige el sistema y rol deseados
                </li>
              </ol>
            </div>
          </div>
        </div>

        <div className="rounded-xl border border-m3-outline/20 bg-m3-surface-container/30 p-3">
          <p className="text-[11px] text-m3-secondary mb-3">
            Tu inquilino seleccionado:{' '}
            <span className="font-semibold text-m3-on-surface">{nombreInquilino}</span>
          </p>
          <M3Button variant="filled" className="w-full" type="button" onClick={onGoToLogin}>
            <LogIn className="w-4 h-4 mr-2" />
            Iniciar sesión para continuar
          </M3Button>
        </div>

        <button
          type="button"
          onClick={() => setStep('info')}
          className="w-full flex items-center justify-center gap-1.5 text-xs text-m3-secondary hover:text-m3-primary transition-colors py-1"
        >
          <ArrowLeft className="w-3.5 h-3.5" />
          Volver
        </button>
      </div>
    );
  }

  if (step === 'no-account') {
    return (
      <div className="space-y-4 animate-fadeIn">
        <div className="flex items-center gap-2 text-m3-primary mb-2">
          <Shield className="w-5 h-5" />
          <h2 className="text-sm font-extrabold uppercase tracking-widest text-m3-on-surface">
            Primero crea tu cuenta
          </h2>
        </div>

        <div className="p-4 rounded-xl border border-amber-500/20 bg-amber-500/5 space-y-2">
          <div className="flex items-start gap-2">
            <Info className="w-4 h-4 text-amber-500 shrink-0 mt-0.5" />
            <div className="space-y-1">
              <p className="text-xs font-semibold text-amber-600">Necesitas una cuenta primero</p>
              <p className="text-xs text-m3-secondary leading-relaxed">
                Para solicitar un perfil, primero debes crear una cuenta de usuario en el tenant
                deseado. Una vez aprobada, podrás solicitar el perfil o rol que necesitas.
              </p>
            </div>
          </div>
        </div>

        <M3Button variant="filled" className="w-full" type="button" onClick={onGoToSignup}>
          <UserPlus className="w-4 h-4 mr-2" />
          Crear cuenta de usuario
        </M3Button>

        <button
          type="button"
          onClick={() => setStep('info')}
          className="w-full flex items-center justify-center gap-1.5 text-xs text-m3-secondary hover:text-m3-primary transition-colors py-1"
        >
          <ArrowLeft className="w-3.5 h-3.5" />
          Volver
        </button>
      </div>
    );
  }

  // Step: info
  return (
    <div className="space-y-4 animate-fadeIn">
      <div className="flex items-center gap-2 text-m3-primary mb-2">
        <ShieldCheck className="w-5 h-5" />
        <h2 className="text-sm font-extrabold uppercase tracking-widest text-m3-on-surface">
          Solicitar perfil
        </h2>
      </div>

      <p className="text-xs text-m3-secondary leading-relaxed">
        Un <strong>perfil</strong> te asigna permisos y acceso a módulos dentro del inquilino.
        Escribe el código de tu empresa y elige tu situación.
      </p>

      <TenantSelect
        label="Inquilino / Empresa"
        value={tenantCode}
        onChange={setTenantCode}
        sugerencias={INQUILINOS_SUGERIDOS}
        placeholder="Ej.: EVOLITH"
        helperText="Código de su organización, tal como se lo entregó su administrador"
      />

      <div className="grid grid-cols-1 gap-2 pt-1">
        <button
          type="button"
          onClick={() => setStep('has-account')}
          className="flex items-center gap-3 p-3 rounded-xl border border-m3-primary/25 bg-m3-primary/5 hover:bg-m3-primary/10 hover:border-m3-primary/40 transition-colors text-left"
        >
          <LogIn className="w-5 h-5 text-m3-primary shrink-0" />
          <div>
            <p className="text-xs font-semibold text-m3-on-surface">Ya tengo cuenta de usuario</p>
            <p className="text-[11px] text-m3-secondary">
              Quiero solicitar acceso a un perfil o rol
            </p>
          </div>
        </button>

        <button
          type="button"
          onClick={() => setStep('no-account')}
          className="flex items-center gap-3 p-3 rounded-xl border border-m3-outline/20 bg-m3-surface/40 hover:border-m3-primary/30 hover:bg-m3-primary/5 transition-colors text-left"
        >
          <UserPlus className="w-5 h-5 text-m3-secondary shrink-0" />
          <div>
            <p className="text-xs font-semibold text-m3-on-surface">No tengo cuenta aún</p>
            <p className="text-[11px] text-m3-secondary">
              Primero necesito crear una cuenta de usuario
            </p>
          </div>
        </button>
      </div>

      <button
        type="button"
        onClick={onBack}
        className="w-full flex items-center justify-center gap-1.5 text-xs text-m3-secondary hover:text-m3-primary transition-colors py-1"
      >
        <ArrowLeft className="w-3.5 h-3.5" />
        Volver al inicio de sesión
      </button>
    </div>
  );
};
