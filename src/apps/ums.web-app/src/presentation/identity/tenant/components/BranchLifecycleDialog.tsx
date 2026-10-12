/**
 * BranchLifecycleDialog — la bitácora de una sucursal (ADR-0164).
 *
 * El estado solo dice cómo está la sucursal HOY. La pregunta de auditoría de un operador aduanero
 * es otra: cómo estaba cuando salió un despacho de hace tres años, quién la desactivó y por qué.
 * Por eso cada asiento muestra qué pasó, cuándo, quién lo hizo y —si lo hay— el motivo.
 */
import React from 'react';
import { History, Lock, EyeOff, ShieldCheck, MapPin, Store } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { M3Dialog } from '@shared/components/M3Dialog';
import { useI18n } from '@app/i18n/use-i18n';
import { useDateFormat } from '@app/formatting/use-date-format';
import { useGetBranchLifecycle } from '@app/identity/hooks/use-branch';
import type { Branch, BranchLifecycleEntry } from '@domain/identity/models/branch.model';

interface BranchLifecycleDialogProps {
  tenantId: string;
  /** Sucursal cuya bitácora se consulta; `null` mantiene el diálogo cerrado y la consulta parada. */
  branch: Branch | null;
  onClose: () => void;
}

type BranchTranslations = ReturnType<typeof useI18n>;

/**
 * Los episodios llegan por nombre desde el backend. Se traducen aquí; si algún día aparece uno
 * nuevo se muestra su nombre crudo en vez de romper la bitácora, que es lo último que puede fallar
 * cuando alguien la abre precisamente para auditar.
 */
const EPISODE_VIEW: Record<
  string,
  { icon: LucideIcon; color: string; label: keyof BranchTranslations }
> = {
  Opened: { icon: Store, color: 'text-emerald-500', label: 'branchEpisodeOpened' },
  Deactivated: { icon: EyeOff, color: 'text-amber-500', label: 'branchEpisodeDeactivated' },
  Reactivated: { icon: ShieldCheck, color: 'text-emerald-500', label: 'branchEpisodeReactivated' },
  Closed: { icon: Lock, color: 'text-m3-error', label: 'branchEpisodeClosed' },
};

export const BranchLifecycleDialog: React.FC<BranchLifecycleDialogProps> = ({
  tenantId,
  branch,
  onClose,
}) => {
  const t = useI18n();
  const { formatDateTime } = useDateFormat();
  const { data: entries = [], isLoading } = useGetBranchLifecycle(
    tenantId,
    branch?.branchId ?? null
  );

  if (!branch) return null;

  const renderEntry = (entry: BranchLifecycleEntry) => {
    const view = EPISODE_VIEW[entry.episode];
    const EpisodeIcon = view?.icon ?? History;
    const label = view ? (t[view.label] as string) : entry.episode;

    return (
      <li
        key={entry.entryId}
        className="flex gap-2.5 py-2 border-b border-m3-outline/10 last:border-b-0"
      >
        <EpisodeIcon
          className={`w-3.5 h-3.5 mt-0.5 shrink-0 ${view?.color ?? 'text-m3-secondary'}`}
        />
        <div className="min-w-0 flex-1">
          <div className="flex items-baseline justify-between gap-2">
            <span className="text-[11px] font-semibold text-m3-on-surface">{label}</span>
            <span className="text-[10px] text-m3-secondary shrink-0">
              {formatDateTime(entry.occurredAtUtc)}
            </span>
          </div>
          <p className="text-[10px] text-m3-secondary">{t.branchEpisodeActor(entry.actorId)}</p>
          {entry.reason && (
            <p className="text-[10px] text-m3-secondary/80 italic">
              {t.branchEpisodeReason(entry.reason)}
            </p>
          )}
          {/* Foto de la época: el nombre y la geocerca de entonces, no los de hoy. */}
          <p className="text-[10px] text-m3-secondary/70 truncate" title={entry.nameSnapshot}>
            {entry.nameSnapshot}
            {entry.geofencingSnapshot && (
              <span className="text-indigo-400 inline-flex items-center gap-0.5 ml-1">
                <MapPin className="w-2.5 h-2.5" />
                {entry.geofencingSnapshot}
              </span>
            )}
          </p>
        </div>
      </li>
    );
  };

  const renderContenido = () => {
    if (isLoading) {
      return (
        <p className="text-[11px] text-m3-secondary py-4 text-center">{t.branchLifecycleLoading}</p>
      );
    }
    if (entries.length === 0) {
      return (
        <p className="text-[11px] text-m3-secondary py-4 text-center">{t.branchLifecycleEmpty}</p>
      );
    }
    return <ul className="space-y-0">{entries.map(renderEntry)}</ul>;
  };

  return (
    <M3Dialog
      open={!!branch}
      title={t.branchLifecycleTitle(branch.name)}
      icon={
        <div className="p-2 rounded-lg flex-shrink-0 bg-m3-primary/15 text-m3-primary">
          <History className="w-5 h-5" aria-hidden="true" />
        </div>
      }
      onScrimClick={onClose}
      actions={[{ label: t.branchLifecycleClose, variant: 'outlined', onClick: onClose }]}
    >
      <div className="max-h-[50vh] overflow-y-auto pr-1">{renderContenido()}</div>
    </M3Dialog>
  );
};
