import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import type { AuthorizationGraph } from '@evolith-ums/ums-sdk-contracts';
import { AuthGraphBuilder } from '@evolith-ums/ums-sdk-testing';
import { AuthorizationValidator } from '../src/authorization-validator.js';
import {
  GraphSections,
  GraphSectionMissingError,
  onGraphSectionAbsent,
  setStrictGraphSections,
  resetGraphSectionDiagnostics
} from '../src/graph-diagnostics.js';

/**
 * Gemela de `GraphSectionDiagnosticsTests` del SDK .NET. Una sección AUSENTE no es una sección
 * VACÍA, y el índice tiene que poder decir cuál de las dos recibió.
 *
 * La guarda de nulos que introdujo la proyección arreglaba media cosa: evitaba el `TypeError` en
 * la primera comprobación de permiso, pero colapsaba `undefined` a `[]` y con eso volvía
 * indistinguibles «no me la mandaron» y «está vacía» justo en la estructura que decide permisos. El
 * modo de fallo pasaba de ruidoso a MUDO, que en autorización es peor.
 *
 * El grafo se fabrica BORRANDO CLAVES de uno completo, que es lo que hace `JSON.parse` sobre una
 * respuesta proyectada: la interfaz no valida nada en ejecución, así que la clave ausente llega
 * como `undefined` por su cuenta.
 */
describe('diagnóstico de secciones ausentes', () => {
  const validator = new AuthorizationValidator();
  let avisos: string[];

  beforeEach(() => {
    resetGraphSectionDiagnostics();
    avisos = [];
    onGraphSectionAbsent(s => avisos.push(s));
  });

  afterEach(() => {
    resetGraphSectionDiagnostics();
  });

  function grafoCompleto(): AuthorizationGraph {
    return AuthGraphBuilder.forTenant('EVOLITH')
      .withSystemSuite('WMS')
      .withScope('stock_view.view')
      .withMenuOption('STOCK_VIEW', 'VIEW')
      .withDomainPermission('PURCHASE_ORDER', 'VIEW')
      .withFeatureFlag('WMS_BULK', true)
      .build();
  }

  /** Lo que llega con `?include=navigation`: sin permisos, sin scopes y sin banderas. */
  function grafoProyectado(): AuthorizationGraph {
    const crudo = JSON.parse(JSON.stringify(grafoCompleto())) as Record<string, unknown>;

    delete crudo['domainPermissions'];
    delete crudo['scopes'];
    delete crudo['featureFlags'];
    crudo['projection'] = ['navigation'];

    return crudo as unknown as AuthorizationGraph;
  }

  /** El mismo grafo, COMPLETO, con esas secciones legítimamente vacías. */
  function grafoCompletoYVacio(): AuthorizationGraph {
    const crudo = JSON.parse(JSON.stringify(grafoCompleto())) as Record<string, unknown>;

    crudo['domainPermissions'] = [];
    crudo['scopes'] = [];
    crudo['featureFlags'] = [];

    return crudo as unknown as AuthorizationGraph;
  }

  it('tolera la ausencia: no revienta, y no concede', () => {
    // La mitad que ya funcionaba y hay que conservar: un SDK que reviente ante un grafo recortado
    // convierte un `curl` con `?include=` contra un entorno compartido en una caída ajena.
    const decision = validator.requireDomainAccess(grafoProyectado(), 'PURCHASE_ORDER', 'VIEW');

    expect(decision.status).toBe('NotGranted');
  });

  it('registra la ausencia con el nombre de la sección', () => {
    // EL defecto que esta prueba cierra: tolerar en silencio hacía que «no me la mandaron» y «está
    // vacía» fueran el mismo estado observable, y solo uno de los dos justifica concluir que no hay
    // ninguna denegación explícita.
    validator.requireDomainAccess(grafoProyectado(), 'PURCHASE_ORDER', 'VIEW');

    expect(avisos).toContain(GraphSections.domainPermissions);
    expect(avisos).toContain(GraphSections.scopes);
    expect(avisos).toContain(GraphSections.featureFlags);
    expect(avisos).not.toContain(GraphSections.menuAccess);
  });

  it('una sección legítimamente VACÍA no se reporta', () => {
    // El falso positivo que dejaría el aviso inservible: un grafo lobby trae todas las secciones
    // vacías y es normal. Si vacío avisara, nadie leería los avisos.
    validator.requireDomainAccess(grafoCompletoYVacio(), 'PURCHASE_ORDER', 'VIEW');

    expect(avisos).toEqual([]);
  });

  it('sin oyentes cae a console.warn, porque tolerar no puede ser callar', () => {
    // Un aviso que solo existe si alguien lo cableó de antemano sigue siendo silencio para quien no
    // sabe que hay algo que cablear — que es exactamente el caso del integrador que acaba de
    // recibir su primer grafo proyectado.
    resetGraphSectionDiagnostics();
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});

    try {
      validator.requireDomainAccess(grafoProyectado(), 'PURCHASE_ORDER', 'VIEW');

      expect(warn).toHaveBeenCalled();
      expect(warn.mock.calls.flat().join(' ')).toContain(GraphSections.domainPermissions);
    } finally {
      warn.mockRestore();
    }
  });

  it('quien prefiera caerse a autorizar a ciegas puede pedirlo', () => {
    // La salida dura, opt-in: un satélite que NECESITA `domainPermissions` prefiere un error
    // tipado a un «no concedido» que parece una decisión legítima.
    const grafo = grafoProyectado();
    setStrictGraphSections(true);

    try {
      validator.requireDomainAccess(grafo, 'PURCHASE_ORDER', 'VIEW');
      expect.unreachable('debería haber lanzado GraphSectionMissingError');
    } catch (e) {
      expect(e).toBeInstanceOf(GraphSectionMissingError);
      // El nombre viaja en el TIPO, no solo en el texto: es lo que permite decidir
      // programáticamente si esa ausencia concreta importa para esta comprobación.
      expect((e as GraphSectionMissingError).sectionName).toBe(GraphSections.domainPermissions);
    }
  });
});
