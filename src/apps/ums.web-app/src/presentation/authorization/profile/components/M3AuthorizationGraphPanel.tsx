import React, { useState, useEffect } from 'react';
import {
  Share2,
  FileCode,
  Clipboard,
  Download,
  Check,
  Loader2,
  ShieldCheck,
  Info,
} from 'lucide-react';
import { M3Button } from '@shared/components/M3Button';
import { M3FormDialog } from '@shared/components/M3FormDialog';
import { M3SegmentedButton } from '@shared/components/M3SegmentedButton';
import { getHttpErrorMessage, getSupportReferenceId } from '@app/errors/http-error';
import {
  profileService,
  type PreviewAuthGraphResponse,
} from '@infra/authorization/services/profile.service';

interface Props {
  isOpen: boolean;
  onClose: () => void;
  profileId: string;
}

type ExportFormat = 'JSON' | 'XML' | 'YAML' | 'CSV';

export const M3AuthorizationGraphPanel: React.FC<Props> = ({ isOpen, onClose, profileId }) => {
  const [format, setFormat] = useState<ExportFormat>('JSON');
  const [content, setContent] = useState('');
  const [meta, setMeta] = useState<Omit<PreviewAuthGraphResponse, 'graph'> | null>(null);
  const [loading, setLoading] = useState(false);
  const [copied, setCopied] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!profileId || !isOpen) return;

    const fetchGraph = async () => {
      setLoading(true);
      setError('');
      setMeta(null);
      try {
        // Calls the same pipeline as POST /api/v1/client/authenticate.
        // This is the exact graph an external client system would receive.
        const res = await profileService.previewAuthGraph(profileId, format);

        const { graph, ...rest } = res;
        setMeta(rest);

        if (format === 'JSON') {
          try {
            const parsed = JSON.parse(graph);
            setContent(JSON.stringify(parsed, null, 2));
          } catch {
            setContent(graph);
          }
        } else {
          setContent(graph);
        }
      } catch (err) {
        const errorMessage = getHttpErrorMessage(err, 'Error al generar el grafo en el servidor.');
        const supportReferenceId = getSupportReferenceId(err);
        setError(
          supportReferenceId ? `${errorMessage} Referencia: ${supportReferenceId}` : errorMessage
        );
      } finally {
        setLoading(false);
      }
    };

    fetchGraph();
  }, [profileId, format, isOpen]);

  const handleCopy = () => {
    if (!content) return;
    navigator.clipboard.writeText(content);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleDownload = () => {
    if (!content) return;
    const mimeTypes: Record<string, string> = {
      JSON: 'application/json',
      XML: 'application/xml',
      YAML: 'application/x-yaml',
      CSV: 'text/csv',
    };
    const mimeType = mimeTypes[format] || 'text/plain';
    const blob = new Blob([content], { type: `${mimeType};charset=utf-8` });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `profile_${profileId}.${format.toLowerCase()}`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  };

  return (
    <M3FormDialog
      open={isOpen}
      onClose={onClose}
      title="Grafo de Autorización Efectiva"
      icon={<Share2 className="w-4 h-4 text-m3-primary" />}
      maxWidth="max-w-4xl"
      footer={
        <>
          <M3Button type="button" variant="text" onClick={onClose}>
            Cerrar
          </M3Button>
          <M3Button
            type="button"
            variant="tonal"
            onClick={handleCopy}
            disabled={loading || !content}
            className="mr-2"
          >
            {copied ? (
              <>
                <Check className="w-3.5 h-3.5 mr-1.5 text-emerald-500" />
                Copiado
              </>
            ) : (
              <>
                <Clipboard className="w-3.5 h-3.5 mr-1.5" />
                Copiar
              </>
            )}
          </M3Button>
          <M3Button
            type="button"
            variant="filled"
            onClick={handleDownload}
            disabled={loading || !content}
          >
            <Download className="w-3.5 h-3.5 mr-1.5" />
            Descargar .{format.toLowerCase()}
          </M3Button>
        </>
      }
    >
      <div className="space-y-4">
        {/* Preview mode notice */}
        <div className="flex items-start gap-2 rounded-lg border border-m3-primary/20 bg-m3-primary/5 px-3 py-2">
          <ShieldCheck className="w-3.5 h-3.5 text-m3-primary mt-0.5 shrink-0" />
          <p className="text-[11px] text-m3-on-surface/70 leading-relaxed">
            Este grafo es{' '}
            <span className="font-semibold text-m3-primary">exactamente el mismo</span> que recibirá
            un sistema cliente al consumir{' '}
            <code className="bg-m3-surface-variant/50 px-1 rounded text-[10px]">
              POST /api/v1/client/authenticate
            </code>
            . Usa el mismo pipeline de generación, formato y parámetros del tenant. Solo se omite la
            validación de credenciales externas.
          </p>
        </div>

        <div className="flex justify-center">
          <M3SegmentedButton
            options={[
              { value: 'JSON', label: 'JSON' },
              { value: 'XML', label: 'XML' },
              { value: 'YAML', label: 'YAML' },
              { value: 'CSV', label: 'CSV' },
            ]}
            value={format}
            onChange={v => setFormat(v as ExportFormat)}
          />
        </div>

        {/* Metadata strip */}
        {meta && !loading && (
          <div className="flex flex-wrap items-center gap-2 text-[10px] text-m3-secondary">
            <span className="flex items-center gap-1">
              <Info className="w-3 h-3" />
              <span className="font-medium">Modo:</span>
              <span className="bg-emerald-500/10 text-emerald-600 px-1.5 py-0.5 rounded-full font-semibold">
                {meta.previewMode}
              </span>
            </span>
            <span className="text-m3-outline/30">·</span>
            <span>
              <span className="font-medium">Tenant:</span> {meta.tenantCode}
            </span>
            <span className="text-m3-outline/30">·</span>
            <span>
              <span className="font-medium">Auth:</span> {meta.authMethodUsed}
            </span>
            <span className="text-m3-outline/30">·</span>
            <span>
              <span className="font-medium">Formato:</span> {meta.format}
            </span>
            <span className="text-m3-outline/30">·</span>
            <span
              className="font-mono text-[9px] text-m3-secondary/60 truncate max-w-[140px]"
              title={meta.requestId}
            >
              req: {meta.requestId.slice(0, 8)}…
            </span>
          </div>
        )}

        <div className="relative rounded-xl border border-m3-outline/20 bg-zinc-950 p-4 font-mono text-[11px] text-zinc-300 min-h-[250px] max-h-[400px] overflow-auto shadow-inner">
          {loading ? (
            <div className="absolute inset-0 flex flex-col items-center justify-center gap-2 bg-zinc-950/80">
              <Loader2 className="w-7 h-7 animate-spin text-m3-primary" />
              <span className="text-zinc-400 text-[11px]">Generando grafo de autorización...</span>
            </div>
          ) : error ? (
            <div className="absolute inset-0 flex flex-col items-center justify-center gap-2 p-4 text-center text-rose-500 bg-zinc-950/80">
              <FileCode className="w-8 h-8 opacity-40" />
              <span className="text-[11px]">{error}</span>
            </div>
          ) : !content ? (
            <div className="absolute inset-0 flex flex-col items-center justify-center text-zinc-500 bg-zinc-950/80">
              No hay datos para visualizar.
            </div>
          ) : (
            <pre className="whitespace-pre overflow-x-auto text-[11px] leading-relaxed">
              {content}
            </pre>
          )}
        </div>
      </div>
    </M3FormDialog>
  );
};
