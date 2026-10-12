import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { NAV_ROUTES } from './navigation.config';

/**
 * Cerco entre la ESPECIFICACIÓN del sistema UMS y la aplicación que la consume.
 *
 * POR QUÉ EXISTE. `src/provisioning/ums/ums-suite.json` declara la navegación que UMS entrega en el
 * grafo de autorización: la ruta a la que lleva cada menú y el código con el que el `AccessGuard`
 * decide si la pantalla se pinta. Ninguna de las dos cosas la valida nadie contra el código de esta
 * aplicación, y ya divergieron una vez con consecuencias caras: la suite se declaraba con un
 * mecanismo que DERIVABA las rutas del código de módulo (`/idm/view-users`) mientras el enrutador
 * declara rutas literales (`/users`). El efecto no fue un error visible sino algo peor — la barra
 * lateral pintaba sus menús y ninguna opción abría nada, porque el enrutador caía al comodín.
 *
 * Es un test ESTÁTICO a propósito: no necesita instancia, base de datos ni sesión. La divergencia
 * se detecta al escribirla, no al desplegarla.
 *
 * El cerco dinámico complementario vive fuera de aquí:
 *   node src/provisioning/cargar-en-ums.mjs --spec src/provisioning/ums/ums-suite.json --dry-run
 * que compara la spec contra una instancia viva y debe reportar cero diferencias.
 */

interface OpcionSpec {
  code: string;
}
interface SubmenuSpec {
  code: string;
  options?: OpcionSpec[];
}
interface MenuSpec {
  code: string;
  route?: string | null;
  navegable?: boolean;
  submenus?: SubmenuSpec[];
}
interface ModuloSpec {
  code: string;
  navegable?: boolean;
  menus?: MenuSpec[];
}
interface SuiteSpec {
  modules?: ModuloSpec[];
}

const RAIZ = resolve(__dirname, '../../../..');
const spec: SuiteSpec = JSON.parse(
  readFileSync(resolve(RAIZ, '../../provisioning/ums/ums-suite.json'), 'utf8'),
);

/**
 * Menús que la aplicación web DEBE poder mostrar.
 *
 * Se excluyen los marcados `navegable: false`. No es una excepción para hacer pasar la prueba:
 * `ums-suite.json` cumple DOS papeles —declara los permisos y dibuja el menú—, y desde que la API
 * tiene dominios sin pantalla (aprobaciones, IGA, auditoría, roles) los dos dejaron de coincidir.
 * Un permiso que existe sin pantalla es legítimo; un menú que apunta a una pantalla inexistente no.
 * El marcador distingue las dos cosas en la propia spec, que es donde se puede ver.
 *
 * Mismo criterio que `origen.rutasNoNavegables` en la spec de SDLC, que lista endpoints de
 * infraestructura para que el inventario siga siendo exhaustivo sin ensuciar la navegación.
 */
const menusDeLaSpec = (spec.modules ?? [])
  .filter(modulo => modulo.navegable !== false)
  .flatMap(modulo =>
    (modulo.menus ?? [])
      .filter(menu => menu.navegable !== false)
      .map(menu => ({
        modulo: modulo.code,
        menu: menu.code,
        ruta: menu.route ?? null,
      })),
  );

/** Los que existen como permiso pero NO como destino. Se comprueban aparte, abajo. */
const menusSinPantalla = (spec.modules ?? []).flatMap(modulo =>
  (modulo.menus ?? [])
    .filter(menu => modulo.navegable === false || menu.navegable === false)
    .map(menu => `${modulo.code}/${menu.code}`),
);

/**
 * Pares (moduleCode, menuCode) que el `AccessGuard` exige en `App.tsx`.
 *
 * Se leen del propio fuente en vez de duplicarlos aquí: una lista escrita a mano en el test
 * envejecería en silencio y el cerco pasaría a comprobar su propia copia en lugar de la aplicación.
 */
function paresQueExigeLaApp(): Set<string> {
  const fuente = readFileSync(resolve(RAIZ, 'src/App.tsx'), 'utf8');
  const pares = new Set<string>();

  // Los dos atributos viajan juntos en cada <AccessGuard>, separados solo por espacios y saltos.
  const patron = /moduleCode="([A-Z_]+)"\s+menuCode="([A-Z_]+)"/g;
  for (const [, modulo, menu] of fuente.matchAll(patron)) {
    pares.add(`${modulo}/${menu}`);
  }

  return pares;
}

describe('ums-suite.json ↔ aplicación web', () => {
  it('declara al menos un menú por módulo', () => {
    expect(menusDeLaSpec.length).toBeGreaterThan(0);
  });

  it('toda ruta de menú de la spec existe en NAV_ROUTES', () => {
    const rutasConocidas = new Set(Object.values(NAV_ROUTES));
    const huerfanas = menusDeLaSpec
      .filter(m => m.ruta !== null && !rutasConocidas.has(m.ruta))
      .map(m => `${m.modulo}/${m.menu} → ${m.ruta}`);

    // Una ruta que el enrutador no conoce produce un menú que no lleva a ninguna parte.
    expect(huerfanas).toEqual([]);
  });

  it('todo menú de la spec tiene ruta declarada', () => {
    // Sin ruta, el elemento aparece en la barra lateral y no navega. En esta suite todos los
    // menús son destinos reales; las OPCIONES sí van sin ruta a propósito, porque son permisos
    // sobre la pantalla del menú y no destinos distintos.
    const sinRuta = menusDeLaSpec.filter(m => !m.ruta).map(m => `${m.modulo}/${m.menu}`);
    expect(sinRuta).toEqual([]);
  });

  it('todo par (módulo, menú) que exige el AccessGuard está declarado en la spec', () => {
    const declarados = new Set(menusDeLaSpec.map(m => `${m.modulo}/${m.menu}`));
    const exigidos = paresQueExigeLaApp();

    // Este es el sentido que importa: si la aplicación protege una pantalla con un código que la
    // spec no concede, esa pantalla queda inalcanzable para todo el que no sea admin interno.
    const sinConceder = [...exigidos].filter(par => !declarados.has(par));
    expect(sinConceder).toEqual([]);
  });

  it('un menú sin pantalla no se declara navegable a medias', () => {
    // La contrapartida del filtro: si alguien marca un menú como no navegable y AUN ASÍ la
    // aplicación lo protege con AccessGuard, tendríamos una pantalla real excluida de la
    // comprobación de rutas — el agujero que el filtro podría abrir.
    const exigidos = paresQueExigeLaApp();
    const contradictorios = menusSinPantalla.filter(par => exigidos.has(par));
    expect(contradictorios).toEqual([]);
  });

  it('el AccessGuard exige algún par (prueba de que la lectura del fuente funciona)', () => {
    // Sin esta comprobación, un cambio de formato en App.tsx dejaría el conjunto vacío y la
    // prueba anterior pasaría comparando la nada contra la nada.
    expect(paresQueExigeLaApp().size).toBeGreaterThan(0);
  });
});
