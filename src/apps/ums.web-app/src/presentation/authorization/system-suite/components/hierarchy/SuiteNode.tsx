import React, { useState } from 'react';
import {
  ChevronDown,
  ChevronRight,
  FolderTree,
  Folder,
  FileText,
  EyeOff,
  ShieldCheck,
  Trash2,
  Plus,
  Pencil,
  ClipboardList,
  Key,
} from 'lucide-react';
import { IconButton } from '@shared/components/Tooltip';
import { CodeBadge } from '@shared/components/CodeBadge';
import { StatusBadge } from '@shared/components/StatusBadge';
import { M3TextField } from '@shared/components/M3TextField';
import { M3Select } from '@shared/components/M3Select';
import { InlineAddForm } from '@shared/components/InlineAddForm';
import { formatSystemCode } from '@app/utils/security';
import type {
  SystemSuiteNode,
  SystemSuiteNodeMetadata,
} from '@domain/authorization/schemas/system-suite.schema';
import { NodeMetadataDialog } from './NodeMetadataDialog';
import { NodeActionsPanel, type SuiteActionOption } from './NodeActionsPanel';

// ── Handlers compartidos por todo el subárbol (instanciados a nivel de módulo) ──
export interface NodeHandlers {
  onAddChild: (
    parentNodeId: string,
    payload: { kind: string; code: string; label: string; description: string; sortOrder: number }
  ) => void;
  onUpdate: (
    nodeId: string,
    payload: { label: string; description: string; sortOrder: number }
  ) => void;
  onRemove: (nodeId: string) => void;
  onSetStatus: (nodeId: string, active: boolean) => void;
  onLinkAction: (nodeId: string, actionCode: string) => void;
  onUnlinkAction: (nodeId: string, actionCode: string) => void;
  onSetMetadata: (nodeId: string, metadata: SystemSuiteNodeMetadata) => void;
  /**
   * Si es `false`, el módulo contenedor está inactivo: el backend rechaza `AddNode`
   * con 400 sobre un módulo inactivo, así que la UI deshabilita el alta de nodos
   * y guía a activar el módulo primero (G-107).
   */
  canAdd: boolean;
  pending: {
    add: boolean;
    update: boolean;
    remove: boolean;
    status: boolean;
    link: boolean;
    unlink: boolean;
    metadata: boolean;
  };
}

interface SuiteNodeProps {
  node: SystemSuiteNode;
  level: number;
  availableActions: SuiteActionOption[];
  handlers: NodeHandlers;
  isNodeExpanded: (id: string) => boolean;
  onToggleNode: (id: string) => void;
}

const KIND_LABEL: Record<string, string> = {
  Menu: 'Menú',
  SubMenu: 'Submenú',
  Option: 'Opción',
};

const KIND_STYLE: Record<string, string> = {
  Menu: 'bg-blue-500/10 text-blue-500',
  SubMenu: 'bg-sky-500/10 text-sky-500',
  Option: 'bg-slate-500/10 text-slate-500',
};

const kindIcon = (kind: string) => {
  if (kind === 'Menu') return <FolderTree className="w-3.5 h-3.5 text-blue-500" />;
  if (kind === 'SubMenu') return <Folder className="w-3.5 h-3.5 text-sky-500" />;
  return <FileText className="w-3.5 h-3.5 text-slate-500" />;
};

const emptyChild = () => ({
  kind: 'Option',
  code: '',
  label: '',
  desc: '',
  sort: '1',
  error: '',
});

export const SuiteNode: React.FC<SuiteNodeProps> = ({
  node,
  level,
  availableActions,
  handlers,
  isNodeExpanded,
  onToggleNode,
}) => {
  const nodeKey = `node-${node.id}`;
  const expanded = isNodeExpanded(nodeKey);
  const hasChildren = node.children.length > 0;
  const isActive = node.status === 'Active';

  const [isAddingChild, setIsAddingChild] = useState(false);
  const [child, setChild] = useState(emptyChild());

  const [isEditing, setIsEditing] = useState(false);
  const [editState, setEditState] = useState({ label: '', desc: '', sort: '1', error: '' });

  const [showMetadata, setShowMetadata] = useState(false);
  const [showActions, setShowActions] = useState(false);

  const beginEdit = () => {
    setEditState({
      label: node.label,
      desc: node.description ?? '',
      sort: String(node.sortOrder ?? 1),
      error: '',
    });
    setIsEditing(true);
  };

  const submitEdit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!editState.label.trim()) {
      setEditState(s => ({ ...s, error: 'Etiqueta requerida' }));
      return;
    }
    handlers.onUpdate(node.id, {
      label: editState.label.trim(),
      description: editState.desc.trim(),
      sortOrder: parseInt(editState.sort) || 1,
    });
    setIsEditing(false);
  };

  const submitChild = (e: React.FormEvent) => {
    e.preventDefault();
    if (!child.code.trim()) {
      setChild(s => ({ ...s, error: 'Código requerido' }));
      return;
    }
    if (!child.label.trim()) {
      setChild(s => ({ ...s, error: 'Etiqueta requerida' }));
      return;
    }
    handlers.onAddChild(node.id, {
      kind: child.kind,
      code: formatSystemCode(child.code),
      label: child.label.trim(),
      description: child.desc.trim(),
      sortOrder: parseInt(child.sort) || 1,
    });
    setChild(emptyChild());
    setIsAddingChild(false);
  };

  const isLeaf = node.kind === 'Option';

  return (
    <div
      className="border-l border-m3-outline/15"
      style={{ marginLeft: level > 0 ? 8 : 0 }}
    >
      {/* ── Fila del nodo ── */}
      <div className="group/node flex items-center gap-2 pl-2 pr-1 py-1.5 rounded-md hover:bg-m3-surface-container/20 transition-colors">
        <button
          type="button"
          onClick={() => hasChildren && onToggleNode(nodeKey)}
          className={`w-4 h-4 flex items-center justify-center shrink-0 ${
            hasChildren ? 'text-m3-secondary hover:text-m3-on-surface' : 'invisible'
          }`}
        >
          {expanded ? <ChevronDown className="w-4 h-4" /> : <ChevronRight className="w-4 h-4" />}
        </button>

        {kindIcon(node.kind)}

        <span
          className={`text-[9px] font-semibold uppercase tracking-wider px-1.5 py-0.5 rounded-full ${
            KIND_STYLE[node.kind] ?? 'bg-m3-outline/10 text-m3-secondary'
          }`}
        >
          {KIND_LABEL[node.kind] ?? node.kind}
        </span>

        <span className="text-[12px] font-medium text-m3-on-surface truncate" title={node.label}>
          {node.label}
        </span>
        <CodeBadge code={node.code} size="xs" />

        {isLeaf && node.actionCodes.length > 0 && (
          <span className="inline-flex items-center gap-1 text-[10px] text-m3-primary bg-m3-primary/10 px-1.5 rounded-full">
            <Key className="w-2.5 h-2.5" />
            {node.actionCodes.length}
          </span>
        )}
        {node.metadata && (
          <span
            className="inline-flex items-center text-[10px] text-emerald-500 bg-emerald-500/10 px-1.5 rounded-full"
            title="Con metadatos SDLC"
          >
            <ClipboardList className="w-2.5 h-2.5" />
          </span>
        )}

        <span className="flex-1" />

        <StatusBadge status={node.status} label={node.status} />
        <span className="text-[10px] text-m3-secondary tabular-nums">Ord: {node.sortOrder}</span>

        <div className="flex items-center gap-0.5 opacity-0 group-hover/node:opacity-100 transition-opacity">
          <IconButton
            tooltip={handlers.canAdd ? 'Agregar nodo hijo' : 'Activa el módulo para agregar nodos'}
            aria-label={
              handlers.canAdd ? 'Agregar nodo hijo' : 'Activa el módulo para agregar nodos'
            }
            disabled={!handlers.canAdd}
            onClick={() => {
              setIsAddingChild(true);
              if (!expanded && hasChildren) onToggleNode(nodeKey);
            }}
            className="hover:text-m3-primary hover:bg-m3-primary/10 disabled:opacity-40 disabled:cursor-not-allowed disabled:hover:bg-transparent disabled:hover:text-m3-secondary"
          >
            <Plus className="w-3.5 h-3.5" />
          </IconButton>
          {isLeaf && (
            <IconButton
              tooltip="Funcionalidades (N:M)"
              onClick={() => setShowActions(v => !v)}
              className={showActions ? 'text-m3-primary bg-m3-primary/10' : ''}
            >
              <Key className="w-3.5 h-3.5" />
            </IconButton>
          )}
          <IconButton
            tooltip="Metadatos SDLC"
            onClick={() => setShowMetadata(true)}
            className="hover:text-emerald-500 hover:bg-emerald-500/10"
          >
            <ClipboardList className="w-3.5 h-3.5" />
          </IconButton>
          <IconButton
            tooltip="Editar"
            onClick={beginEdit}
            className="hover:text-m3-primary hover:bg-m3-primary/10"
          >
            <Pencil className="w-3.5 h-3.5" />
          </IconButton>
          {isActive ? (
            <IconButton
              tooltip="Desactivar"
              onClick={() => handlers.onSetStatus(node.id, false)}
              disabled={handlers.pending.status}
            >
              <EyeOff className="w-3.5 h-3.5" />
            </IconButton>
          ) : (
            <IconButton
              tooltip="Activar"
              onClick={() => handlers.onSetStatus(node.id, true)}
              disabled={handlers.pending.status}
            >
              <ShieldCheck className="w-3.5 h-3.5" />
            </IconButton>
          )}
          <IconButton
            tooltip="Eliminar"
            onClick={() => handlers.onRemove(node.id)}
            disabled={handlers.pending.remove}
            className="hover:text-m3-error hover:bg-m3-error/10"
          >
            <Trash2 className="w-3.5 h-3.5" />
          </IconButton>
        </div>
      </div>

      {/* ── N:M panel (hoja) ── */}
      {isLeaf && showActions && (
        <div className="pl-8 pr-2 pb-1">
          <NodeActionsPanel
            actionCodes={node.actionCodes}
            availableActions={availableActions}
            isLinking={handlers.pending.link}
            isUnlinking={handlers.pending.unlink}
            onLink={code => handlers.onLinkAction(node.id, code)}
            onUnlink={code => handlers.onUnlinkAction(node.id, code)}
          />
        </div>
      )}

      {/* ── Edición inline ── */}
      {isEditing && (
        <div className="pl-8 pr-2 pb-2">
          <InlineAddForm
            isOpen={isEditing}
            onToggle={open => setIsEditing(open)}
            onSubmit={submitEdit}
            addLabel="Nodo"
            title={`Editar ${KIND_LABEL[node.kind] ?? 'Nodo'}`}
            triggerEmphasis="none"
            cancelLabel="Cancelar"
            submitLabel="Guardar Cambios"
            isLoading={handlers.pending.update}
            error={editState.error || undefined}
          >
            <M3TextField
              label="Etiqueta"
              required
              value={editState.label}
              onChange={e => setEditState(s => ({ ...s, label: e.target.value }))}
            />
            <M3TextField
              label="Descripción"
              value={editState.desc}
              onChange={e => setEditState(s => ({ ...s, desc: e.target.value }))}
            />
            <M3TextField
              label="Orden"
              type="number"
              value={editState.sort}
              onChange={e => setEditState(s => ({ ...s, sort: e.target.value }))}
            />
          </InlineAddForm>
        </div>
      )}

      {/* ── Alta de nodo hijo ── */}
      {isAddingChild && (
        <div className="pl-8 pr-2 pb-2">
          <InlineAddForm
            isOpen={isAddingChild}
            onToggle={open => {
              setIsAddingChild(open);
              if (!open) setChild(emptyChild());
            }}
            onSubmit={submitChild}
            addLabel="Nodo"
            title="Nuevo Nodo Hijo"
            triggerEmphasis="none"
            cancelLabel="Cancelar"
            submitLabel="Guardar Nodo"
            isLoading={handlers.pending.add}
            error={child.error || undefined}
          >
            <M3Select
              label="Tipo de Nodo"
              value={child.kind}
              onChange={e => setChild(s => ({ ...s, kind: e.target.value }))}
            >
              <option value="Menu">Menú</option>
              <option value="SubMenu">Submenú</option>
              <option value="Option">Opción</option>
            </M3Select>
            <M3TextField
              label="Código"
              required
              value={child.code}
              onChange={e => setChild(s => ({ ...s, code: e.target.value }))}
              placeholder="e.g. CONSULTAR"
            />
            <M3TextField
              label="Etiqueta"
              required
              value={child.label}
              onChange={e => setChild(s => ({ ...s, label: e.target.value }))}
              placeholder="e.g. Consultar"
            />
            <M3TextField
              label="Descripción"
              value={child.desc}
              onChange={e => setChild(s => ({ ...s, desc: e.target.value }))}
              placeholder="Opcional"
            />
            <M3TextField
              label="Orden"
              type="number"
              value={child.sort}
              onChange={e => setChild(s => ({ ...s, sort: e.target.value }))}
              placeholder="1"
            />
          </InlineAddForm>
        </div>
      )}

      {/* ── Hijos (recursivo) ── */}
      {expanded && hasChildren && (
        <div className="pl-3">
          {node.children.map(childNode => (
            <SuiteNode
              key={childNode.id}
              node={childNode}
              level={level + 1}
              availableActions={availableActions}
              handlers={handlers}
              isNodeExpanded={isNodeExpanded}
              onToggleNode={onToggleNode}
            />
          ))}
        </div>
      )}

      {/* ── Dialogo de metadatos ── */}
      {showMetadata && (
        <NodeMetadataDialog
          open={showMetadata}
          node={node}
          isSaving={handlers.pending.metadata}
          onClose={() => setShowMetadata(false)}
          onSave={metadata => {
            handlers.onSetMetadata(node.id, metadata);
            setShowMetadata(false);
          }}
        />
      )}
    </div>
  );
};
