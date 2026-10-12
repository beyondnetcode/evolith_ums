import React, { useEffect, useState } from 'react';
import { ClipboardList } from 'lucide-react';
import { M3FormDialog } from '@shared/components/M3FormDialog';
import { M3TextField } from '@shared/components/M3TextField';
import { M3Button } from '@shared/components/M3Button';
import type {
  SystemSuiteNode,
  SystemSuiteNodeMetadata,
} from '@domain/authorization/schemas/system-suite.schema';

interface NodeMetadataDialogProps {
  open: boolean;
  node: SystemSuiteNode;
  isSaving: boolean;
  onClose: () => void;
  onSave: (metadata: SystemSuiteNodeMetadata) => void;
}

type MetaState = {
  responsable: string;
  criticidad: string;
  productoImpactado: string;
  componenteTecnico: string;
  dependencias: string;
  evidencias: string;
  trazabilidadSdlc: string;
};

const fromNode = (m: SystemSuiteNodeMetadata | null | undefined): MetaState => ({
  responsable: m?.responsable ?? '',
  criticidad: m?.criticidad ?? '',
  productoImpactado: m?.productoImpactado ?? '',
  componenteTecnico: m?.componenteTecnico ?? '',
  dependencias: m?.dependencias ?? '',
  evidencias: m?.evidencias ?? '',
  trazabilidadSdlc: m?.trazabilidadSdlc ?? '',
});

/**
 * Editor de metadatos de gobernanza SDLC por nodo (ADR-0090).
 * Todos los campos son opcionales; se envía el conjunto completo (reemplazo).
 */
export const NodeMetadataDialog: React.FC<NodeMetadataDialogProps> = ({
  open,
  node,
  isSaving,
  onClose,
  onSave,
}) => {
  const [state, setState] = useState<MetaState>(fromNode(node.metadata));

  useEffect(() => {
    if (open) setState(fromNode(node.metadata));
  }, [open, node.metadata]);

  const set = (key: keyof MetaState) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setState(s => ({ ...s, [key]: e.target.value }));

  const handleSave = () => {
    const trimmed = (v: string) => (v.trim() === '' ? null : v.trim());
    onSave({
      responsable: trimmed(state.responsable),
      criticidad: trimmed(state.criticidad),
      productoImpactado: trimmed(state.productoImpactado),
      componenteTecnico: trimmed(state.componenteTecnico),
      dependencias: trimmed(state.dependencias),
      evidencias: trimmed(state.evidencias),
      trazabilidadSdlc: trimmed(state.trazabilidadSdlc),
    });
  };

  return (
    <M3FormDialog
      open={open}
      onClose={onClose}
      title={`Metadatos SDLC — ${node.label}`}
      icon={<ClipboardList className="w-4 h-4" />}
      maxWidth="max-w-xl"
      footer={
        <>
          <M3Button variant="text" onClick={onClose} disabled={isSaving}>
            Cancelar
          </M3Button>
          <M3Button variant="filled" onClick={handleSave} disabled={isSaving}>
            {isSaving ? 'Guardando…' : 'Guardar Metadatos'}
          </M3Button>
        </>
      }
    >
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-4">
        <M3TextField
          label="Responsable"
          value={state.responsable}
          onChange={set('responsable')}
          placeholder="e.g. Equipo Aduanas"
        />
        <M3TextField
          label="Criticidad"
          value={state.criticidad}
          onChange={set('criticidad')}
          placeholder="e.g. Alta / Media / Baja"
        />
        <M3TextField
          label="Producto Impactado"
          value={state.productoImpactado}
          onChange={set('productoImpactado')}
          placeholder="e.g. Portal de Clientes"
        />
        <M3TextField
          label="Componente Técnico"
          value={state.componenteTecnico}
          onChange={set('componenteTecnico')}
          placeholder="e.g. ums.api / Authorization"
        />
        <M3TextField
          label="Dependencias"
          value={state.dependencias}
          onChange={set('dependencias')}
          placeholder="e.g. SUNAT, servicio X"
        />
        <M3TextField
          label="Evidencias"
          value={state.evidencias}
          onChange={set('evidencias')}
          placeholder="e.g. enlace a PR / doc"
        />
        <div className="sm:col-span-2">
          <M3TextField
            label="Trazabilidad SDLC"
            value={state.trazabilidadSdlc}
            onChange={set('trazabilidadSdlc')}
            placeholder="e.g. FS-04 / ADR-0090 / G-029"
          />
        </div>
      </div>
    </M3FormDialog>
  );
};
