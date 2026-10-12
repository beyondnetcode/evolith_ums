import React, { useMemo, useState } from 'react';
import { ChevronDown, ChevronRight, Layers, EyeOff, ShieldCheck, Trash2, Plus } from 'lucide-react';
import {
  useAddNode,
  useUpdateNode,
  useRemoveNode,
  useSetNodeStatus,
  useLinkNodeAction,
  useUnlinkNodeAction,
  useSetNodeMetadata,
} from '@app/authorization/hooks/use-system-suite';
import { InlineAddForm } from '@shared/components/InlineAddForm';
import { IconButton } from '@shared/components/Tooltip';
import { CodeBadge } from '@shared/components/CodeBadge';
import { StatusBadge } from '@shared/components/StatusBadge';
import { M3TextField } from '@shared/components/M3TextField';
import { M3Select } from '@shared/components/M3Select';
import { formatSystemCode } from '@app/utils/security';
import type { SystemSuiteNode } from '@domain/authorization/schemas/system-suite.schema';
import { SuiteNode, type NodeHandlers } from './SuiteNode';
import type { SuiteActionOption } from './NodeActionsPanel';

interface ModuleWithNodes {
  id: string;
  code: string;
  name: string;
  description?: string;
  status: string;
  sortOrder?: number;
  nodes?: SystemSuiteNode[];
}

interface SuiteNodeTreeProps {
  suiteId: string;
  module: ModuleWithNodes;
  availableActions: SuiteActionOption[];
  isExpanded: boolean;
  isNodeExpanded: (id: string) => boolean;
  onToggle: () => void;
  onToggleNode: (id: string) => void;
  onDeactivate: () => void;
  onActivate: () => void;
  onRemove: () => void;
  isDeactivating: boolean;
  isActivating: boolean;
  isRemoving: boolean;
}

const emptyRoot = () => ({ kind: 'Menu', code: '', label: '', desc: '', sort: '1', error: '' });

/**
 * Árbol de nodos recursivo de un módulo (ADR-0090). Reemplaza la cadena rígida
 * ModuleCard→MenuRow→SubMenuRow→OptionRow: renderiza `module.nodes` con el
 * componente recursivo <SuiteNode/> y expone la escritura por nodeId.
 */
export const SuiteNodeTree: React.FC<SuiteNodeTreeProps> = ({
  suiteId,
  module,
  availableActions,
  isExpanded,
  isNodeExpanded,
  onToggle,
  onToggleNode,
  onDeactivate,
  onActivate,
  onRemove,
  isDeactivating,
  isActivating,
  isRemoving,
}) => {
  const addNode = useAddNode(suiteId, module.id);
  const updateNode = useUpdateNode(suiteId, module.id);
  const removeNode = useRemoveNode(suiteId, module.id);
  const setNodeStatus = useSetNodeStatus(suiteId, module.id);
  const linkAction = useLinkNodeAction(suiteId, module.id);
  const unlinkAction = useUnlinkNodeAction(suiteId, module.id);
  const setMetadata = useSetNodeMetadata(suiteId, module.id);

  const [isAddingRoot, setIsAddingRoot] = useState(false);
  const [root, setRoot] = useState(emptyRoot());

  // Un módulo nace `Inactive`; el backend rechaza `AddNode` con 400 sobre un módulo
  // inactivo. La UI deshabilita el alta de nodos y guía a activarlo primero (G-107).
  const isModuleActive = module.status === 'Active';

  const handlers: NodeHandlers = useMemo(
    () => ({
      onAddChild: (parentNodeId, payload) =>
        addNode.mutate({ parentNodeId, ...payload }),
      onUpdate: (nodeId, payload) => updateNode.mutate({ nodeId, ...payload }),
      onRemove: nodeId => removeNode.mutate(nodeId),
      onSetStatus: (nodeId, active) => setNodeStatus.mutate({ nodeId, active }),
      onLinkAction: (nodeId, actionCode) => linkAction.mutate({ nodeId, actionCode }),
      onUnlinkAction: (nodeId, actionCode) => unlinkAction.mutate({ nodeId, actionCode }),
      onSetMetadata: (nodeId, metadata) => setMetadata.mutate({ nodeId, ...metadata }),
      canAdd: isModuleActive,
      pending: {
        add: addNode.isPending,
        update: updateNode.isPending,
        remove: removeNode.isPending,
        status: setNodeStatus.isPending,
        link: linkAction.isPending,
        unlink: unlinkAction.isPending,
        metadata: setMetadata.isPending,
      },
    }),
    [
      addNode,
      updateNode,
      removeNode,
      setNodeStatus,
      linkAction,
      unlinkAction,
      setMetadata,
      isModuleActive,
    ]
  );

  const nodes = useMemo(
    () => [...(module.nodes ?? [])].sort((a, b) => a.sortOrder - b.sortOrder),
    [module.nodes]
  );

  const submitRoot = (e: React.FormEvent) => {
    e.preventDefault();
    if (!root.code.trim()) {
      setRoot(s => ({ ...s, error: 'Código requerido' }));
      return;
    }
    if (!root.label.trim()) {
      setRoot(s => ({ ...s, error: 'Etiqueta requerida' }));
      return;
    }
    addNode.mutate(
      {
        parentNodeId: null,
        kind: root.kind,
        code: formatSystemCode(root.code),
        label: root.label.trim(),
        description: root.desc.trim(),
        sortOrder: parseInt(root.sort) || 1,
      },
      {
        onSuccess: () => {
          setRoot(emptyRoot());
          setIsAddingRoot(false);
        },
      }
    );
  };

  // Empty-state del árbol: si el módulo está inactivo, el backend rechaza `AddNode`
  // con 400 (G-107), así que se muestra un CTA deshabilitado que guía a activarlo.
  const renderRootCta = () => {
    if (isAddingRoot) return null;
    if (!isModuleActive) {
      return (
        <button
          type="button"
          disabled
          title="Activa el módulo para agregar nodos"
          className="w-full flex items-center justify-center gap-1.5 py-1.5 rounded-md border border-dashed border-m3-outline/25 text-[11px] font-medium text-m3-secondary/50 cursor-not-allowed"
        >
          <Plus className="w-3.5 h-3.5" /> Activa el módulo para agregar nodos
        </button>
      );
    }
    return (
      <button
        type="button"
        onClick={() => setIsAddingRoot(true)}
        className="w-full flex items-center justify-center gap-1.5 py-1.5 rounded-md border border-dashed border-m3-outline/25 text-[11px] font-medium text-m3-secondary/60 hover:text-m3-primary hover:border-m3-primary/40 hover:bg-m3-primary/5 transition-colors"
      >
        <Plus className="w-3.5 h-3.5" /> Agregar nodo raíz
      </button>
    );
  };

  return (
    <div className="border border-m3-outline/20 rounded-lg overflow-hidden bg-m3-surface-container/5 transition-all hover:border-m3-outline/40 animate-fadeIn">
      {/* ── Cabecera del módulo ── */}
      <div className="flex items-center justify-between px-3 py-2 bg-m3-surface-container/10 select-none border-b border-m3-outline/10 hover:bg-m3-surface-container/20 transition-colors group/module">
        <div onClick={onToggle} className="flex items-center gap-2 cursor-pointer flex-1">
          {isExpanded ? (
            <ChevronDown className="w-4 h-4 text-m3-secondary" />
          ) : (
            <ChevronRight className="w-4 h-4 text-m3-secondary" />
          )}
          <Layers className="w-4 h-4 text-m3-primary" />
          <div className="flex items-center gap-2">
            <span className="font-semibold text-sm text-m3-on-surface">{module.name}</span>
            <CodeBadge code={module.code} size="xs" />
          </div>
        </div>
        <div className="flex items-center gap-3">
          <StatusBadge status={module.status} label={module.status} />
          <span className="text-xs text-m3-secondary">Ord: {module.sortOrder}</span>
          <div className="flex items-center gap-0.5 opacity-0 group-hover/module:opacity-100 transition-opacity">
            {isExpanded && (
              <IconButton
                tooltip={
                  isModuleActive ? 'Agregar nodo raíz' : 'Activa el módulo para agregar nodos'
                }
                aria-label={
                  isModuleActive ? 'Agregar nodo raíz' : 'Activa el módulo para agregar nodos'
                }
                disabled={!isModuleActive}
                onClick={e => {
                  e.stopPropagation();
                  setIsAddingRoot(true);
                }}
                className="hover:text-m3-primary hover:bg-m3-primary/10 disabled:opacity-40 disabled:cursor-not-allowed disabled:hover:bg-transparent disabled:hover:text-m3-secondary"
              >
                <Plus className="w-3.5 h-3.5" />
              </IconButton>
            )}
            {module.status === 'Active' ? (
              <IconButton
                tooltip="Desactivar"
                onClick={e => {
                  e.stopPropagation();
                  onDeactivate();
                }}
                disabled={isDeactivating}
              >
                <EyeOff className="w-3.5 h-3.5" />
              </IconButton>
            ) : (
              <IconButton
                tooltip="Activar"
                onClick={e => {
                  e.stopPropagation();
                  onActivate();
                }}
                disabled={isActivating}
              >
                <ShieldCheck className="w-3.5 h-3.5" />
              </IconButton>
            )}
            <IconButton
              tooltip="Eliminar"
              onClick={e => {
                e.stopPropagation();
                onRemove();
              }}
              disabled={isRemoving}
              className="hover:text-m3-error hover:bg-m3-error/10"
            >
              <Trash2 className="w-3.5 h-3.5" />
            </IconButton>
          </div>
        </div>
      </div>

      {/* ── Cuerpo: árbol de nodos ── */}
      <div className={`p-3 space-y-1 ${isExpanded ? 'block' : 'hidden'}`}>
        {module.description && (
          <p className="text-xs text-m3-secondary italic pl-1 border-l-2 border-m3-outline/20">
            {module.description}
          </p>
        )}

        <InlineAddForm
          isOpen={isAddingRoot}
          onToggle={open => {
            setIsAddingRoot(open);
            if (!open) setRoot(emptyRoot());
          }}
          onSubmit={submitRoot}
          addLabel="Nodo raíz"
          title="Nuevo Nodo Raíz"
          triggerEmphasis="none"
          cancelLabel="Cancelar"
          submitLabel="Guardar Nodo"
          isLoading={addNode.isPending}
          error={root.error || undefined}
        >
          <M3Select
            label="Tipo de Nodo"
            value={root.kind}
            onChange={e => setRoot(s => ({ ...s, kind: e.target.value }))}
          >
            <option value="Menu">Menú</option>
            <option value="SubMenu">Submenú</option>
            <option value="Option">Opción</option>
          </M3Select>
          <M3TextField
            label="Código"
            required
            value={root.code}
            onChange={e => setRoot(s => ({ ...s, code: e.target.value }))}
            placeholder="e.g. ADMIN"
          />
          <M3TextField
            label="Etiqueta"
            required
            value={root.label}
            onChange={e => setRoot(s => ({ ...s, label: e.target.value }))}
            placeholder="e.g. Administración"
          />
          <M3TextField
            label="Descripción"
            value={root.desc}
            onChange={e => setRoot(s => ({ ...s, desc: e.target.value }))}
            placeholder="Opcional"
          />
          <M3TextField
            label="Orden"
            type="number"
            value={root.sort}
            onChange={e => setRoot(s => ({ ...s, sort: e.target.value }))}
            placeholder="1"
          />
        </InlineAddForm>

        {nodes.length === 0 ? (
          renderRootCta()
        ) : (
          <div className="space-y-0.5">
            {nodes.map(n => (
              <SuiteNode
                key={n.id}
                node={n}
                level={0}
                availableActions={availableActions}
                handlers={handlers}
                isNodeExpanded={isNodeExpanded}
                onToggleNode={onToggleNode}
              />
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
