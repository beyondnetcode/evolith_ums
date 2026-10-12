import type { AuthorizationGraph } from '@evolith-ums/ums-sdk-contracts';

export interface ClientAuthRequest {
  readonly tenantCode: string;
  readonly username: string;
  readonly password: string;
  readonly format?: 'JSON' | 'XML' | 'YAML' | 'CSV';
}

/**
 * El sobre TAL COMO VIAJA por el cable.
 *
 * `graph` es una **cadena serializada**, no un objeto: el formato lo elige el inquilino (JSON, XML,
 * YAML o CSV) y un objeto JSON no puede transportar XML. El SDK lo tipaba como objeto y nunca había
 * ejercido el endpoint real, así que `parsed.graph?.schemaVersion` daba `undefined` y **cualquier
 * login a través del SDK fallaba** con `AUTH_204` (G-207).
 *
 * `requestId` es el identificador de traza de ASP.NET Core (`0HN...:00000001`), no un GUID.
 *
 * Se distingue del resultado que recibe quien llama: este es el cable, aquel es el grafo ya
 * interpretado. Mezclarlos fue lo que permitió que la forma real y la esperada divergieran sin que
 * nada fallara.
 */
export interface ClientAuthEnvelope {
  readonly token: string;
  readonly tokenType: string;
  readonly expiresIn: number;
  readonly issuedAt: string;
  readonly format: string;
  readonly graph: string;
  readonly requestId: string;
}

/** Lo que recibe quien llama: el mismo sobre, con el grafo ya interpretado. */
export interface ClientAuthResult {
  readonly token: string;
  readonly tokenType: string;
  readonly expiresIn: number;
  readonly issuedAt: string;
  readonly format: string;
  readonly graph: AuthorizationGraph;
  readonly requestId: string;
}

export interface UmsAuthClientOptions {
  readonly baseUrl: string;
  readonly authenticatePath?: string;   // default: '/api/v1/client/authenticate'
  readonly fetchImpl?: typeof fetch;    // for testing or environments without global fetch
  readonly timeoutMs?: number;
}

export interface ClientAuthFailure {
  readonly code: string;
  readonly message: string;
}

export type ClientAuthOutcome =
  | { readonly ok: true; readonly value: ClientAuthResult }
  | { readonly ok: false; readonly error: ClientAuthFailure };
