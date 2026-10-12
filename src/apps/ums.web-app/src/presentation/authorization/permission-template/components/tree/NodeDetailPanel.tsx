import React from 'react';
import { UITreeNodeData, computeEffectiveState } from './TreeNode';
import { Shield, CheckCircle2, XCircle, MinusCircle, Info } from 'lucide-react';
import { useApplyTemplateItemEffect } from '@app/authorization/hooks/use-permission-template';
import type { SystemSuite } from '@domain/authorization/models/system-suite.model';
import type {
  ExclusiveArcTarget,
  PermissionEffect,
} from '@domain/authorization/models/permission-template.model';
import { itemEffect, primaryItem } from '@domain/authorization/models/permission-template.model';

interface NodeDetailPanelProps {
  node: UITreeNodeData | null;
  suite: SystemSuite | undefined | null;
  templateId: string;
  isDraft: boolean;
}

export const NodeDetailPanel: React.FC<NodeDetailPanelProps> = ({
  node,
  suite,
  templateId,
  isDraft,
}) => {
  const { applyEffect, isPending } = useApplyTemplateItemEffect(templateId);

  if (!node || !suite) {
    return (
      <div className="flex flex-col items-center justify-center h-full text-m3-secondary/50 p-6 text-center">
        <Shield className="w-12 h-12 mb-3 opacity-20" />
        <p className="text-sm font-medium">Seleccione un elemento del árbol</p>
        <p className="text-xs mt-1 max-w-[250px]">
          Explore la jerarquía a la izquierda para visualizar o configurar permisos específicos.
        </p>
      </div>
    );
  }

  const effectiveState = computeEffectiveState(node);
  // Si hay varias filas sobre el mismo nodo manda la vigente; la retirada solo sirve para reactivar.
  const selfItem = primaryItem(node.items);
  // Lo que la regla directa concede HOY: una fila retirada no concede nada, luego es Neutral.
  const selfEffect: PermissionEffect = selfItem ? itemEffect(selfItem) : 'Neutral';

  const StateIcon =
    effectiveState === 'Allow'
      ? CheckCircle2
      : effectiveState === 'Deny'
        ? XCircle
        : effectiveState === 'Partial'
          ? CheckCircle2
          : MinusCircle;

  const stateColor =
    effectiveState === 'Allow'
      ? 'text-emerald-500 bg-emerald-500/10'
      : effectiveState === 'Deny'
        ? 'text-rose-500 bg-rose-500/10'
        : effectiveState === 'Partial'
          ? 'text-amber-500 bg-amber-500/10'
          : 'text-m3-secondary bg-m3-surface-variant';

  const stateLabel =
    effectiveState === 'Allow'
      ? 'Permitido'
      : effectiveState === 'Deny'
        ? 'Denegado'
        : effectiveState === 'Partial'
          ? 'Permitido parcialmente'
          : 'No configurado (Heredado)';

  const mapTypeToExclusiveArcTarget = (type: string): ExclusiveArcTarget => {
    if (type === 'Module') return 'Module';
    if (type === 'Menu') return 'Submodule';
    if (type === 'Aggregate') return 'Aggregate';
    if (type === 'Entity') return 'Entity';
    return 'Option'; // Option for SubMenu and Option
  };

  const resolveActionId = (): string => {
    // Si el nodo es una opción trae su actionCode; si no, el backend exige una acción cualquiera.
    const byCode = node.actionCode
      ? suite.actions.find(a => a.code === node.actionCode)?.id
      : undefined;
    return byCode ?? suite.actions[0]?.id ?? '00000000-0000-0000-0000-000000000000';
  };

  const handleApplyEffect = async (effect: PermissionEffect) => {
    if (!isDraft) return;
    // El adaptador decide el verbo: alta, efecto o reactivación del ítem retirado (ADR-0164).
    await applyEffect({
      effect,
      item: selfItem,
      target: {
        targetType: mapTypeToExclusiveArcTarget(node.type),
        targetId: node.id,
        actionId: resolveActionId(),
      },
    });
  };

  return (
    <div className="p-5">
      <div className="mb-6">
        <h3 className="text-sm font-bold text-m3-on-surface flex items-center gap-2">
          {node.label}
          <span
            className={`text-[10px] font-semibold uppercase px-2.5 py-0.5 rounded-full border border-current ${stateColor}`}
          >
            {node.type}
          </span>
        </h3>
        {node.description && <p className="text-xs text-m3-secondary mt-1.5">{node.description}</p>}
      </div>

      <div className="p-4 rounded-xl border border-m3-outline/20 bg-m3-surface-container/30 mb-6 flex items-start gap-3">
        <StateIcon className={`w-6 h-6 shrink-0 mt-0.5 ${stateColor.split(' ')[0]}`} />
        <div>
          <p className="text-xs font-bold text-m3-on-surface">Estado efectivo: {stateLabel}</p>
          <p className="text-[11px] text-m3-secondary mt-1 leading-relaxed">
            {effectiveState === 'Partial'
              ? 'Algunos elementos secundarios de este nodo tienen configuraciones de permisos diferentes.'
              : effectiveState === 'Neutral'
                ? 'No hay reglas directas asignadas a este nodo. Su acceso dependerá de las reglas aplicadas en sus contenedores superiores.'
                : `El acceso a esta entidad y sus elementos dependientes está ${effectiveState === 'Allow' ? 'permitido' : 'denegado'}.`}
          </p>
        </div>
      </div>

      <div className="space-y-3">
        <h4 className="text-xs font-bold uppercase tracking-wider text-m3-secondary">
          Regla Directa
        </h4>

        {!isDraft && (
          <div className="flex items-center gap-2 text-[11px] text-m3-secondary/70 bg-m3-surface-variant/30 p-2 rounded">
            <Info className="w-3.5 h-3.5" />
            Solo puedes modificar reglas en plantillas en estado Borrador (Draft).
          </div>
        )}

        <div className="flex gap-2">
          <button
            onClick={() => handleApplyEffect('Allow')}
            disabled={!isDraft || isPending}
            className={`flex-1 py-2 px-3 rounded-lg border text-xs font-semibold flex items-center justify-center gap-2 transition-colors disabled:opacity-50
              ${
                selfEffect === 'Allow'
                  ? 'bg-emerald-500/15 border-emerald-500/30 text-emerald-600'
                  : 'bg-transparent border-m3-outline/20 text-m3-secondary hover:border-emerald-500/30 hover:text-emerald-600'
              }`}
          >
            <CheckCircle2 className="w-4 h-4" />
            Permitir explícitamente
          </button>

          <button
            onClick={() => handleApplyEffect('Deny')}
            disabled={!isDraft || isPending}
            className={`flex-1 py-2 px-3 rounded-lg border text-xs font-semibold flex items-center justify-center gap-2 transition-colors disabled:opacity-50
              ${
                selfEffect === 'Deny'
                  ? 'bg-rose-500/15 border-rose-500/30 text-rose-600'
                  : 'bg-transparent border-m3-outline/20 text-m3-secondary hover:border-rose-500/30 hover:text-rose-600'
              }`}
          >
            <XCircle className="w-4 h-4" />
            Denegar explícitamente
          </button>

          <button
            onClick={() => handleApplyEffect('Neutral')}
            disabled={!isDraft || selfEffect === 'Neutral' || isPending}
            className={`flex-1 py-2 px-3 rounded-lg border text-xs font-semibold flex items-center justify-center gap-2 transition-colors disabled:opacity-50
              ${
                selfEffect === 'Neutral'
                  ? 'bg-m3-surface-variant border-m3-outline/20 text-m3-on-surface/80'
                  : 'bg-transparent border-m3-outline/20 text-m3-secondary hover:border-m3-outline/40 hover:text-m3-on-surface'
              }`}
          >
            <MinusCircle className="w-4 h-4" />
            Heredar
          </button>
        </div>
      </div>
    </div>
  );
};
