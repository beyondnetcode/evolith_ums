import React, { useState } from 'react';
import { Key, Plus, X } from 'lucide-react';
import { M3Select } from '@shared/components/M3Select';
import { IconButton } from '@shared/components/Tooltip';

export interface SuiteActionOption {
  code: string;
  name: string;
}

interface NodeActionsPanelProps {
  actionCodes: string[];
  availableActions: SuiteActionOption[];
  isLinking: boolean;
  isUnlinking: boolean;
  onLink: (actionCode: string) => void;
  onUnlink: (actionCode: string) => void;
}

/**
 * Panel del vínculo N:M funcionalidad↔nodo (ADR-0090).
 * Muestra las funcionalidades vinculadas como chips y permite añadir
 * desde el registro de acciones de la suite (multi-select acumulativo).
 */
export const NodeActionsPanel: React.FC<NodeActionsPanelProps> = ({
  actionCodes,
  availableActions,
  isLinking,
  isUnlinking,
  onLink,
  onUnlink,
}) => {
  const [selected, setSelected] = useState('');

  const unlinked = availableActions.filter(a => !actionCodes.includes(a.code));

  const nameOf = (code: string) => availableActions.find(a => a.code === code)?.name ?? code;

  const handleLink = () => {
    if (!selected) return;
    onLink(selected);
    setSelected('');
  };

  return (
    <div className="mt-1.5 rounded-md border border-m3-outline/15 bg-m3-surface-container/10 p-2 space-y-2">
      <div className="flex items-center gap-1.5 text-[10px] font-semibold uppercase tracking-wider text-m3-secondary">
        <Key className="w-3 h-3" /> Funcionalidades vinculadas (N:M)
      </div>

      {actionCodes.length === 0 ? (
        <p className="text-[11px] text-m3-secondary/70 italic">Sin funcionalidades vinculadas.</p>
      ) : (
        <div className="flex flex-wrap gap-1.5">
          {actionCodes.map(code => (
            <span
              key={code}
              className="group/chip inline-flex items-center gap-1 pl-2 pr-1 py-0.5 rounded-full bg-m3-primary/10 text-m3-primary text-[10px] font-medium"
              title={nameOf(code)}
            >
              <span className="font-mono">{code}</span>
              <button
                type="button"
                onClick={() => onUnlink(code)}
                disabled={isUnlinking}
                className="rounded-full p-0.5 hover:bg-m3-error/15 hover:text-m3-error transition-colors"
                title="Desvincular"
              >
                <X className="w-3 h-3" />
              </button>
            </span>
          ))}
        </div>
      )}

      {unlinked.length > 0 && (
        <div className="flex items-end gap-2">
          <div className="flex-1">
            <M3Select
              label="Vincular funcionalidad"
              compact
              value={selected}
              onChange={e => setSelected(e.target.value)}
              className="mb-0"
            >
              <option value="">Seleccionar…</option>
              {unlinked.map(a => (
                <option key={a.code} value={a.code}>
                  {a.name} ({a.code})
                </option>
              ))}
            </M3Select>
          </div>
          <IconButton
            tooltip="Vincular"
            onClick={handleLink}
            disabled={!selected || isLinking}
            className="mb-1 hover:text-m3-primary hover:bg-m3-primary/10"
          >
            <Plus className="w-3.5 h-3.5" />
          </IconButton>
        </div>
      )}
    </div>
  );
};
