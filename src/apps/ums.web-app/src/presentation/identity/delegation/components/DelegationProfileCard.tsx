import React from 'react';
import {
  useActivateDelegation,
  useRevokeDelegation,
  useSubmitDelegationForApproval,
  useApproveDelegation,
  useRejectDelegation,
} from '@app/identity/hooks/use-delegation';
import { useI18n } from '@app/i18n/use-i18n';
import { useStatusLabel } from '@app/hooks/use-status-label';
import { Delegation } from '@domain/identity/models/delegation.model';
import { Shield, Sliders, ShieldAlert, CheckCircle2, Send, XCircle } from 'lucide-react';
import { M3Card } from '@shared/components/M3Card';
import { M3Button } from '@shared/components/M3Button';
import { StatusBadge } from '@shared/components/StatusBadge';
import { CodeBadge } from '@shared/components/CodeBadge';

// ─── Props ───────────────────────────────────────────────────────────────────

interface DelegationProfileCardProps {
  delegation: Delegation;
}

// ─── Component ───────────────────────────────────────────────────────────────

const EVOLITH_USERS: Record<string, string> = {
  '5f4e3d01-1b0a-9f8e-7d6c-543210987654': 'Gerente',
  '5f4e3d02-1b0a-9f8e-7d6c-543210987654': 'Analista',
  '5f4e3d05-1b0a-9f8e-7d6c-543210987654': 'Socio',
};

const formatUserId = (id: string) => {
  return EVOLITH_USERS[id] || 'Usuario';
};

export const DelegationProfileCard: React.FC<DelegationProfileCardProps> = ({ delegation }) => {
  const t = useI18n();
  const statusLabel = useStatusLabel();

  const activateMutation = useActivateDelegation(delegation.delegationId);
  const revokeMutation = useRevokeDelegation(delegation.delegationId);
  const submitMutation = useSubmitDelegationForApproval(delegation.delegationId);
  const approveMutation = useApproveDelegation(delegation.delegationId);
  const rejectMutation = useRejectDelegation(delegation.delegationId);
  const isPendingMutation =
    activateMutation.isPending ||
    revokeMutation.isPending ||
    submitMutation.isPending ||
    approveMutation.isPending ||
    rejectMutation.isPending;

  // G-131 / G-132: las acciones se cablean por estado real de la máquina
  // (Draft/PendingApproval/Active/Revoked/Expired), no contra un 'Pending' inexistente.
  // La mutación (useNotifiedMutation) es dueña de la notificación success/error e invalida
  // el query, que refresca el estado del botón. Sin aviso prematuro ni error tragado: si el
  // backend rechaza, sale el error real y el botón no cambia (patrón de G-129).
  const canActivateDirectly = delegation.status === 'Draft' && !delegation.requiresApproval;
  const canSubmitForApproval = delegation.status === 'Draft' && delegation.requiresApproval;
  const isPendingApproval = delegation.status === 'PendingApproval';
  const isActive = delegation.status === 'Active';

  return (
    <M3Card
      variant="elevated"
      className="p-5 border border-m3-outline/25 bg-m3-surface-container/20 shadow-sm"
    >
      <div className="flex justify-between items-start gap-4 pb-3.5 border-b border-m3-outline/15 mb-4">
        <div className="flex gap-3 flex-1 min-w-0">
          <div className="p-2 bg-m3-primary/10 rounded-lg text-m3-primary border border-m3-primary/10 self-start flex-shrink-0">
            <Shield className="w-5 h-5" />
          </div>
          <div className="min-w-0">
            <h3 className="text-sm font-semibold text-m3-on-surface flex items-center gap-1.5 flex-wrap">
              Delegación de Acceso
              <CodeBadge code={delegation.scopeType} />
            </h3>
            <p className="text-xs text-m3-secondary mt-0.5">
              De: {formatUserId(delegation.delegatingAdminId)} → Para:{' '}
              {formatUserId(delegation.delegatedAdminId)}
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2 flex-shrink-0">
          <StatusBadge status={delegation.status} label={statusLabel(delegation.status)} />
        </div>
      </div>

      <div className="flex items-center justify-between text-[11px]">
        <div className="flex items-center gap-1.5 text-m3-secondary font-medium">
          <Sliders className="w-3.5 h-3.5" />
          <span>{t.stateControls}</span>
        </div>
        <div className="flex items-center gap-2">
          {isActive ? (
            <button
              type="button"
              onClick={() => revokeMutation.mutate('User requested revocation')}
              disabled={isPendingMutation}
              className="h-7 px-3 rounded-full border border-rose-500/30 text-rose-500 text-[11px] font-medium flex items-center gap-1.5 hover:bg-rose-500/10 transition-colors disabled:opacity-50"
            >
              <ShieldAlert className="w-3 h-3" /> Revoke
            </button>
          ) : null}

          {canActivateDirectly ? (
            <button
              type="button"
              onClick={() => activateMutation.mutate(undefined)}
              disabled={isPendingMutation}
              className="h-7 px-3 rounded-full bg-emerald-600 hover:bg-emerald-500 text-white text-[11px] font-medium flex items-center gap-1.5 transition-colors disabled:opacity-50"
            >
              <CheckCircle2 className="w-3 h-3" /> {t.activateBtn}
            </button>
          ) : null}

          {canSubmitForApproval ? (
            <button
              type="button"
              onClick={() => submitMutation.mutate(undefined)}
              disabled={isPendingMutation}
              className="h-7 px-3 rounded-full bg-m3-primary hover:bg-m3-primary/90 text-white text-[11px] font-medium flex items-center gap-1.5 transition-colors disabled:opacity-50"
            >
              <Send className="w-3 h-3" /> Enviar a aprobación
            </button>
          ) : null}

          {isPendingApproval ? (
            <>
              <button
                type="button"
                onClick={() => approveMutation.mutate(undefined)}
                disabled={isPendingMutation}
                className="h-7 px-3 rounded-full bg-emerald-600 hover:bg-emerald-500 text-white text-[11px] font-medium flex items-center gap-1.5 transition-colors disabled:opacity-50"
              >
                <CheckCircle2 className="w-3 h-3" /> Aprobar
              </button>
              <button
                type="button"
                onClick={() => rejectMutation.mutate('Rechazada por el revisor')}
                disabled={isPendingMutation}
                className="h-7 px-3 rounded-full border border-rose-500/30 text-rose-500 text-[11px] font-medium flex items-center gap-1.5 hover:bg-rose-500/10 transition-colors disabled:opacity-50"
              >
                <XCircle className="w-3 h-3" /> Rechazar
              </button>
            </>
          ) : null}
        </div>
      </div>
    </M3Card>
  );
};
