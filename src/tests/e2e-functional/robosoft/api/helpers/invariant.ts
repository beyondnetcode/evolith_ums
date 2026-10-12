// Declaración y aserción de invariantes del PRD (INV-*) para el arnés RoboSoft (carril B).
//
// Cada invariante es un test de Playwright cuyo título canónico es `INV-XXX: <descripción>`.
// El id INV-* y su evidencia se adjuntan como anotaciones para trazabilidad (aparecen en el
// reporte y en test-results/robosoft-api.json), cerrando el ciclo con la matriz de certificación.
import { test, expect, type APIResponse } from '@playwright/test';

export interface InvariantMeta {
  /** Identificador de la invariante del PRD, p. ej. "INV-AR3". */
  id: string;
  /** Descripción funcional (español). */
  descripcion: string;
  /** Contexto acotado al que pertenece, p. ej. "approvals-compliance". */
  contexto: string;
  /** Referencia de código o requisito funcional que la respalda (SD-05: evidencia). */
  referencia?: string;
}

type CuerpoTest = Parameters<typeof test>[2];

function anotaciones(meta: InvariantMeta, extra: Array<{ type: string; description: string }> = []) {
  const base = [
    { type: 'invariante', description: meta.id },
    { type: 'contexto', description: meta.contexto },
  ];
  if (meta.referencia) base.push({ type: 'referencia', description: meta.referencia });
  return [...base, ...extra];
}

/**
 * Declara una invariante VIGENTE (se espera que el sistema actual la cumpla → verde).
 * El cuerpo aprovisiona su propia data, ejerce el API y asevera la regla. El id INV-* y su
 * referencia quedan como anotaciones (firma `test(title, details, body)` de Playwright).
 */
export function invariante(meta: InvariantMeta, cuerpo: CuerpoTest): void {
  test(`${meta.id}: ${meta.descripcion}`, { annotation: anotaciones(meta) }, cuerpo);
}

/**
 * Declara una invariante con BUG REAL pendiente en producción (re-verificado FAIL).
 * Se marca `fixme` (no se ejecuta, no rompe el carril) y se registra el hallazgo con
 * archivo:línea + evidencia. NO se corrige producción desde el arnés.
 */
export function invarianteFixme(meta: InvariantMeta & { hallazgo: string }, cuerpo: CuerpoTest): void {
  test.fixme(
    `${meta.id}: ${meta.descripcion}`,
    { annotation: anotaciones(meta, [{ type: 'hallazgo', description: meta.hallazgo }]) },
    cuerpo,
  );
}

/**
 * Declara una invariante NO verificable de forma determinista en caja negra (p. ej. requiere
 * data no sembrada o provisión destructiva sobre el clúster compartido). Se marca `skip` con el
 * motivo. NO es un bug: es una brecha de cobertura documentada (equivale a PENDING en la auditoría).
 */
export function invariantePendiente(meta: InvariantMeta & { motivo: string }, cuerpo: CuerpoTest): void {
  test.skip(
    `${meta.id}: ${meta.descripcion}`,
    { annotation: anotaciones(meta, [{ type: 'pendiente', description: meta.motivo }]) },
    cuerpo,
  );
}

/** Asevera el código HTTP con un mensaje que incluye el cuerpo de respuesta como evidencia. */
export async function esperarEstado(res: APIResponse, esperado: number | number[]): Promise<void> {
  const codigos = Array.isArray(esperado) ? esperado : [esperado];
  if (!codigos.includes(res.status())) {
    const cuerpo = await res.text();
    expect(
      codigos,
      `Esperado ${codigos.join('|')} pero fue ${res.status()} en ${res.url()} :: ${cuerpo}`,
    ).toContain(res.status());
  }
  expect(codigos).toContain(res.status());
}

/** Extrae el errorCode/brokenRule del ProblemDetails para aseverar la causa de dominio. */
export async function codigoDeError(res: APIResponse): Promise<string> {
  try {
    const body = await res.json();
    // `errorCode`/`brokenRule`: ProblemDetails de dominio. `code`: errores de auth (AUTH_00x).
    return String(body.errorCode ?? body.brokenRule ?? body.code ?? body.detail ?? '');
  } catch {
    return '';
  }
}
