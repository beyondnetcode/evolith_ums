import { UmsErrorCodes, isSchemaVersionSupported, SchemaVersion } from '@evolith-ums/ums-sdk-contracts';
import type { AuthorizationGraph } from '@evolith-ums/ums-sdk-contracts';
import type {
  ClientAuthEnvelope,
  ClientAuthOutcome,
  ClientAuthRequest,
  ClientAuthResult,
  UmsAuthClientOptions
} from './types.js';

const DEFAULT_PATH = '/api/v1/client/authenticate';
const DEFAULT_TIMEOUT_MS = 30_000;

/**
 * Cliente tipado del endpoint de autenticación de UMS.
 *
 * Deserializa el grafo —que viaja como cadena, en el formato que eligió el inquilino— y comprueba
 * su `schemaVersion` contra la ventana de compatibilidad del SDK antes de devolverlo; un MAJOR
 * incompatible da `AUTH_205` sin exponer el contenido.
 */
export class UmsAuthClient {
  private readonly baseUrl: string;
  private readonly path: string;
  private readonly fetchImpl: typeof fetch;
  private readonly timeoutMs: number;

  constructor(options: UmsAuthClientOptions) {
    if (!options.baseUrl) throw new Error('UmsAuthClient requires baseUrl');
    this.baseUrl = options.baseUrl.replace(/\/$/, '');
    this.path = options.authenticatePath ?? DEFAULT_PATH;
    this.fetchImpl = options.fetchImpl ?? globalThis.fetch;
    if (!this.fetchImpl) {
      throw new Error('No fetch implementation available — provide one via options.fetchImpl.');
    }
    this.timeoutMs = options.timeoutMs ?? DEFAULT_TIMEOUT_MS;
  }

  async authenticate(request: ClientAuthRequest, init?: { signal?: AbortSignal }): Promise<ClientAuthOutcome> {
    const url = `${this.baseUrl}${this.path}`;
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), this.timeoutMs);
    const signal = init?.signal ?? controller.signal;

    let response: Response;
    try {
      response = await this.fetchImpl(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
        signal
      });
    } catch (err) {
      return failure(UmsErrorCodes.IdpCallFailed, (err as Error).message);
    } finally {
      clearTimeout(timeout);
    }

    if (!response.ok) {
      const body = await safeText(response);
      const code = mapStatus(response.status);
      return failure(code, body || response.statusText);
    }

    let sobre: ClientAuthEnvelope;
    try {
      sobre = (await response.json()) as ClientAuthEnvelope;
    } catch (err) {
      return failure(UmsErrorCodes.AuthGraphMalformed, (err as Error).message);
    }

    // El grafo viene serializado en el formato que eligió el inquilino. Hay que deserializarlo
    // ANTES de mirar nada dentro: sobre una cadena, `.schemaVersion` es `undefined`, y esa era la
    // causa de que el SDK contestara «falta schemaVersion» a una respuesta perfectamente válida.
    const formato = (sobre.format ?? 'JSON').toUpperCase();
    if (formato !== 'JSON') {
      return failure(
        UmsErrorCodes.AuthGraphMalformed,
        `El inquilino sirve el grafo en '${formato}' y este SDK solo interpreta JSON. ` +
          `Pide 'JSON' en la petición (campo 'format') o configura el inquilino para emitirlo así.`
      );
    }

    let graph: AuthorizationGraph;
    try {
      graph = JSON.parse(sobre.graph) as AuthorizationGraph;
    } catch (err) {
      return failure(
        UmsErrorCodes.AuthGraphMalformed,
        `El campo 'graph' no es JSON deserializable: ${(err as Error).message}`
      );
    }

    if (!graph?.schemaVersion) {
      return failure(
        UmsErrorCodes.AuthGraphSchemaMissing,
        'Server response does not carry a schemaVersion field.'
      );
    }
    if (!isSchemaVersionSupported(graph.schemaVersion)) {
      return failure(
        UmsErrorCodes.AuthGraphSchemaUnsupported,
        `Server emitted schemaVersion '${graph.schemaVersion}' outside SDK range ` +
          `(${SchemaVersion.CompatibilityMinInclusive} ≤ x < ${SchemaVersion.CompatibilityMaxExclusive}).`
      );
    }

    return { ok: true, value: { ...sobre, graph } };
  }
}

function failure(code: string, message: string): ClientAuthOutcome {
  return { ok: false, error: { code, message } };
}

async function safeText(response: Response): Promise<string> {
  try {
    return await response.text();
  } catch {
    return '';
  }
}

function mapStatus(status: number): string {
  switch (status) {
    case 400: return UmsErrorCodes.ValidationError;
    case 401: return UmsErrorCodes.InvalidCredentials;
    case 403: return UmsErrorCodes.UserNotActive;
    case 404: return UmsErrorCodes.TenantNotFound;
    case 423: return UmsErrorCodes.AccountLocked;
    default:  return UmsErrorCodes.IdpCallFailed;
  }
}
