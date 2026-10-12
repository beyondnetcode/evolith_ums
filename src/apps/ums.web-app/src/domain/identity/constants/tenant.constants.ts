/**
 * tenant.constants.ts — Constantes de dominio del agregado Inquilino (Tenant).
 *
 * Centraliza valores que referencian varios componentes (formularios, fichas, selectores)
 * para que un cambio se propague desde un solo sitio.
 */

/** Valores de tipo de inquilino que acepta la API. */
export const TENANT_TYPES = ['INTERNAL', 'SUPPLIER', 'CLIENT'] as const;
export type TenantType = (typeof TENANT_TYPES)[number];

/** Tamaño de página por omisión en las vistas de listado de inquilinos. */
export const TENANT_PAGE_SIZE = 9 as const;

/**
 * Longitud máxima del código de inquilino. Espeja `CreateTenantCommandValidator`
 * (`MaximumLength(50)`): el cliente no inventa una regla más estricta que la del servidor,
 * porque quien decide si el código existe es el servidor.
 */
export const LONGITUD_MAXIMA_CODIGO_INQUILINO = 50;

/**
 * Inquilino raíz del ecosistema (ADR-0071 / FS-26). Es el único código que este portal puede
 * dar por existente sin preguntar: es la organización que opera el propio UMS.
 */
export const CODIGO_INQUILINO_RAIZ = 'EVOLITH';

/**
 * Sugerencia de inquilino para el campo de ingreso. NO es un catálogo: es una ayuda para
 * teclear menos. Lo que viaja a la API es siempre el **código**, nunca un identificador.
 */
export interface InquilinoSugerido {
  code: string;
  name: string;
}

/**
 * Normaliza lo que el usuario teclea al formato que espera el servidor: sin espacios sobrantes
 * y en mayúsculas, que es exactamente lo que hace `auth.service` antes de enviarlo.
 */
export function normalizarCodigoInquilino(valor: string): string {
  return valor.trim().toUpperCase().slice(0, LONGITUD_MAXIMA_CODIGO_INQUILINO);
}

/**
 * Lee las sugerencias que declara el despliegue en `VITE_TENANT_CODES`.
 *
 * Formato: `CODIGO|Nombre visible` separados por comas; el nombre es opcional.
 *   VITE_TENANT_CODES="EVOLITH|Evolith UMS,COMEX_ANDINA|Comercializadora Andina S.A.C."
 *
 * Es una FRONTERA (P-FE-06): una variable de entorno es texto arbitrario que llega al bundle,
 * así que se valida en tiempo de ejecución antes de tiparla. Lo que no case se descarta en
 * silencio —una sugerencia mal escrita no puede impedir que nadie inicie sesión— y el campo
 * sigue admitiendo cualquier código tecleado.
 */
export function parsearInquilinosDeclarados(crudo: unknown): InquilinoSugerido[] {
  if (typeof crudo !== 'string' || crudo.trim().length === 0) {
    return [];
  }

  const vistos = new Set<string>();
  const sugerencias: InquilinoSugerido[] = [];

  for (const entrada of crudo.split(',')) {
    const [codigoCrudo = '', ...restoNombre] = entrada.split('|');
    const code = normalizarCodigoInquilino(codigoCrudo);
    if (code.length === 0 || vistos.has(code)) {
      continue;
    }

    const name = restoNombre.join('|').trim();
    vistos.add(code);
    sugerencias.push({ code, name: name.length > 0 ? name : code });
  }

  return sugerencias;
}

/**
 * Inquilinos que siembra el dataset de desarrollo (FS-25 / FS-26). Solo se ofrecen cuando el
 * bundle se sirve en modo desarrollo: fuera de ahí no existen, y ofrecerlos era el defecto
 * G-263 —medido sobre UAT el 2026-08-15, el único inquilino real era `EVOLITH` y el desplegable
 * seguía mostrando otros tres—. Ya no llevan identificador: el que había estaba cableado al
 * sembrador y no correspondía en ninguna base creada de otro modo.
 */
const INQUILINOS_SEMBRADOS: InquilinoSugerido[] = [
  { code: CODIGO_INQUILINO_RAIZ, name: 'Evolith UMS — Inquilino raíz / Admin Root' },
  { code: 'INTERNAL_ADMIN', name: 'Internal Admin (recuperación / break-glass)' },
  { code: 'COMEX_ANDINA', name: 'Comercializadora Andina S.A.C. (cliente)' },
  { code: 'IMPO_ANDINA_SUB', name: 'Importadora Sub-Cliente (cliente del cliente)' },
];

const declarados = parsearInquilinosDeclarados(import.meta.env.VITE_TENANT_CODES);

/**
 * Sugerencias vigentes, por orden de precedencia: las que declara el despliegue, las del
 * sembrador si estamos en desarrollo, y ninguna en cualquier otro caso. La lista vacía es un
 * estado legítimo y previsto: el campo de inquilino se teclea.
 *
 * Mientras la API no publique un endpoint anónimo con los inquilinos que el ingreso admite,
 * esto es lo más lejos que puede llegar el cliente sin inventarse un catálogo (G-263).
 */
export const INQUILINOS_SUGERIDOS: readonly InquilinoSugerido[] = resolverSugerencias();

function resolverSugerencias(): readonly InquilinoSugerido[] {
  if (declarados.length > 0) return declarados;
  return import.meta.env.DEV ? INQUILINOS_SEMBRADOS : [];
}

/**
 * Código con el que arranca el campo. Se puede sobrescribir tecleando: no oculta ningún
 * inquilino ni impide entrar a uno que no esté sugerido.
 */
export function resolverCodigoPorOmision(
  declarado: unknown,
  sugerencias: readonly InquilinoSugerido[]
): string {
  if (typeof declarado === 'string' && declarado.trim().length > 0) {
    return normalizarCodigoInquilino(declarado);
  }
  const primera = sugerencias[0];
  return primera ? primera.code : CODIGO_INQUILINO_RAIZ;
}

export const CODIGO_INQUILINO_POR_OMISION = resolverCodigoPorOmision(
  import.meta.env.VITE_DEFAULT_TENANT_CODE,
  INQUILINOS_SUGERIDOS
);

/** Persona de acceso sembrada (seed FS-25). Usada por el popup "Datos de prueba". */
export interface DevPersona {
  label: string;
  tenantCode: string;
  email: string;
  password: string;
}

/** Personas de prueba materializadas por el seed de EVOLITH (contraseña dev uniforme). */
export const DEV_PERSONAS: DevPersona[] = [
  {
    // ADR-0071 / FS-26: EVOLITH es el Admin Root — gobierna el ecosistema y administra su propia casa.
    label: 'EVOLITH — Admin Root (plataforma + su organización)',
    tenantCode: 'EVOLITH',
    email: 'admin@evolith.example',
    password: 'Evolith.Dev.2026',
  },
  {
    label: 'EVOLITH — Callao · Jefe de Almacén',
    tenantCode: 'EVOLITH',
    email: 'jefe.almacen.callao@evolith.example',
    password: 'Evolith.Dev.2026',
  },
  {
    label: 'EVOLITH — Callao · Agente de Aduanas',
    tenantCode: 'EVOLITH',
    email: 'agente.aduanas.callao@evolith.example',
    password: 'Evolith.Dev.2026',
  },
  {
    label: 'EVOLITH — Paita · Jefe de Almacén',
    tenantCode: 'EVOLITH',
    email: 'jefe.almacen.paita@evolith.example',
    password: 'Evolith.Dev.2026',
  },
  {
    label: 'Cliente externo (Comex Andina)',
    tenantCode: 'COMEX_ANDINA',
    email: 'usuario.impo@comexandina.com.pe',
    password: 'Evolith.Dev.2026',
  },
  {
    label: 'Cliente de mi cliente (sub-cliente)',
    tenantCode: 'IMPO_ANDINA_SUB',
    email: 'usuario@impo-subcliente.com.pe',
    password: 'Evolith.Dev.2026',
  },
  {
    // ADR-0071 / FS-26: cuenta de emergencia (break-glass), degradada — sin gobierno global.
    label: 'Plataforma — break-glass (recuperación)',
    tenantCode: 'INTERNAL_ADMIN',
    email: 'admin@ums.local',
    password: 'root',
  },
];
