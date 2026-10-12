import React, { useState } from 'react';
import { ArrowLeft, Building2, Check, Mail, ShieldPlus } from 'lucide-react';
import { M3Button } from '@shared/components/M3Button';
import { M3TextField } from '@shared/components/M3TextField';
import authService from '@infra/identity/services/auth.service';

interface TenantSignupFormProps {
  onBack: () => void;
}

export const TenantSignupForm: React.FC<TenantSignupFormProps> = ({ onBack }) => {
  const [companyName, setCompanyName] = useState('');
  const [companyReference, setCompanyReference] = useState('');
  const [contactName, setContactName] = useState('');
  const [contactEmail, setContactEmail] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [result, setResult] = useState<{
    message: string;
    tenantSignupRequestId: string | null;
  } | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (!companyName.trim()) {
      setError('Ingrese el nombre de la empresa');
      return;
    }
    if (!companyReference.trim()) {
      setError('Ingrese el RUC o código');
      return;
    }
    if (!contactName.trim()) {
      setError('Ingrese el nombre de contacto');
      return;
    }
    if (!contactEmail.trim()) {
      setError('Ingrese el correo de contacto');
      return;
    }

    setIsLoading(true);
    try {
      const response = await authService.signupTenant({
        companyName: companyName.trim(),
        companyReference: companyReference.trim(),
        contactName: contactName.trim(),
        contactEmail: contactEmail.trim().toLowerCase(),
      });

      setResult({
        message: response.message,
        tenantSignupRequestId: response.tenantSignupRequestId,
      });
    } catch {
      setError('No se pudo registrar el tenant. Intente nuevamente.');
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
            El equipo de BeyondNet Code revisará la solicitud y le enviará las credenciales de
            administración cuando sea aprobada.
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
        <ShieldPlus className="w-5 h-5" />
        <h2 className="text-sm font-extrabold uppercase tracking-widest text-m3-on-surface">
          Registro de empresa
        </h2>
      </div>

      <p className="text-xs text-m3-secondary leading-relaxed">
        Registre su empresa para iniciar el proceso de onboarding en UMS.
      </p>

      <M3TextField
        label="Nombre de la empresa"
        required
        value={companyName}
        onChange={e => {
          setCompanyName(e.target.value);
          setError('');
        }}
        placeholder="Empresa S.A.C."
        autoComplete="organization"
        disabled={isLoading}
        icon={<Building2 className="w-4 h-4" />}
      />

      <M3TextField
        label="RUC / Código"
        required
        value={companyReference}
        onChange={e => {
          setCompanyReference(e.target.value);
          setError('');
        }}
        placeholder="RUC o código único"
        disabled={isLoading}
      />

      <M3TextField
        label="Nombre de contacto"
        required
        value={contactName}
        onChange={e => {
          setContactName(e.target.value);
          setError('');
        }}
        placeholder="Nombre y apellido"
        autoComplete="name"
        disabled={isLoading}
      />

      <M3TextField
        label="Correo de contacto"
        type="email"
        required
        value={contactEmail}
        onChange={e => {
          setContactEmail(e.target.value);
          setError('');
        }}
        placeholder="contacto@empresa.com"
        autoComplete="email"
        disabled={isLoading}
        icon={<Mail className="w-4 h-4" />}
      />

      {error && <p className="text-xs text-m3-error px-1">{error}</p>}

      <M3Button variant="filled" className="w-full" type="submit" disabled={isLoading}>
        {isLoading ? 'Enviando...' : 'Registrar empresa'}
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
