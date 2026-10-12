/**
 * pluralize.ts — pluralización en español para los contadores de las barras de
 * herramientas (ListToolbar, ChildEntityToolbar, PermissionSectionToolbar).
 *
 * Reglas cubiertas:
 *  - Termina en vocal (incluidas acentuadas) → +s     (módulo → módulos)
 *  - Termina en «-ión»                        → «-iones» (acción → acciones)
 *  - Termina en «z»                           → «-ces»   (voz → voces)
 *  - Termina en otra consonante               → +es      (rol → roles, perfil → perfiles)
 *
 * Los préstamos que no siguen la regla (tenant → tenants, flag → flags) deben
 * pasar su plural explícito; el helper lo respeta si se provee.
 */
export function pluralizeEs(word: string): string {
  if (!word) return word;
  if (/[aeiouáéíóú]$/i.test(word)) return `${word}s`;
  if (/ión$/i.test(word)) return word.replace(/ión$/i, 'iones');
  if (/z$/i.test(word)) return word.replace(/z$/i, 'ces');
  return `${word}es`;
}

/**
 * Devuelve «N etiqueta» con la etiqueta en singular o plural según el conteo.
 * @param count   número de elementos.
 * @param singular etiqueta en singular (p.ej. «Acción»).
 * @param plural  plural explícito para préstamos/irregulares; si se omite se
 *                deriva con {@link pluralizeEs}.
 */
export function formatItemCountLabel(count: number, singular: string, plural?: string): string {
  const label = count === 1 ? singular : (plural ?? pluralizeEs(singular));
  return `${count} ${label}`;
}
