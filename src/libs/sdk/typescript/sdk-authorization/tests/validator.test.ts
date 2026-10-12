import { describe, it, expect, beforeAll } from 'vitest';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';
import type { AuthorizationGraph } from '@evolith-ums/ums-sdk-contracts';
import { AuthorizationValidator, isGranted } from '../src/index.js';

const __dirname = dirname(fileURLToPath(import.meta.url));
const FIXTURES_DIR = resolve(__dirname, '../../../contracts/fixtures');

function loadFixture(name: string): AuthorizationGraph {
  const raw = readFileSync(resolve(FIXTURES_DIR, `${name}.json`), 'utf-8');
  return JSON.parse(raw) as AuthorizationGraph;
}

/** Patch validUntil so the fixture is in the future at test-run time. */
function makeValid(graph: AuthorizationGraph): AuthorizationGraph {
  return { ...graph, validUntil: new Date(Date.now() + 60 * 60 * 1000).toISOString() };
}

describe('AuthorizationValidator — golden fixture contract', () => {
  let validator: AuthorizationValidator;

  beforeAll(() => {
    validator = new AuthorizationValidator();
  });

  it('local-auth-success: grants view scopes, denies not-granted scopes', () => {
    const graph = makeValid(loadFixture('local-auth-success'));
    expect(isGranted(validator.requireScope(graph, 'STOCK_VIEW.VIEW'))).toBe(true);
    expect(isGranted(validator.requireScope(graph, 'PURCHASE_ORDER.VIEW'))).toBe(true);
    const notGranted = validator.requireScope(graph, 'PURCHASE_ORDER.APPROVE');
    expect(isGranted(notGranted)).toBe(false);
    expect(notGranted.errorCode).toBe('AUTH_101');
  });

  it('idp-auth-success: branch-scoped profile + IDP provider, Override Allow grants APPROVE', () => {
    const graph = makeValid(loadFixture('idp-auth-success'));
    expect(graph.context.branch).not.toBeNull();
    expect(graph.context.profile.scope).toBe('BranchScoped');
    expect(graph.authentication.method).toBe('IDP');
    expect(graph.authentication.provider?.value).toBe('AZURE_AD');
    expect(isGranted(validator.requireDomainAccess(graph, 'BRANCH_INVENTORY', 'APPROVE'))).toBe(true);
    expect(isGranted(validator.requireScope(graph, 'BRANCH_INVENTORY.VIEW'))).toBe(true);
  });

  it('deny-wins: explicit Deny beats any Allow on the same target', () => {
    const graph = makeValid(loadFixture('deny-wins'));
    const decision = validator.requireDomainAccess(graph, 'PURCHASE_ORDER', 'DELETE');
    expect(decision.status).toBe('Denied');
    expect(decision.errorCode).toBe('AUTH_106');

    const menuDecision = validator.requireMenuOption(graph, 'STOCK_DELETE');
    expect(menuDecision.status).toBe('Denied');
    expect(menuDecision.errorCode).toBe('AUTH_104');
  });

  it('override-allow: Override source promotes effect to Allow', () => {
    const graph = makeValid(loadFixture('override-allow'));
    expect(isGranted(validator.requireDomainAccess(graph, 'PURCHASE_ORDER', 'APPROVE'))).toBe(true);
  });

  it('empty-permissions: all probes return NotGranted variants', () => {
    const graph = makeValid(loadFixture('empty-permissions'));
    expect(validator.requireScope(graph, 'ANY.SCOPE').errorCode).toBe('AUTH_101');
    expect(validator.requireMenuOption(graph, 'ANY_MENU').errorCode).toBe('AUTH_103');
    expect(validator.requireDomainAccess(graph, 'ANY', 'ACTION').errorCode).toBe('AUTH_105');
    expect(validator.requireFeatureFlag(graph, 'ANY_FLAG').errorCode).toBe('AUTH_108');
  });

  it('expired-graph: fails with AUTH_201 regardless of contents', () => {
    const graph = loadFixture('expired-graph');
    const decision = validator.requireScope(graph, 'PURCHASE_ORDER.VIEW');
    expect(decision.status).toBe('Expired');
    expect(decision.errorCode).toBe('AUTH_201');
  });

  it('feature-flag-matched: enabled flags resolve to Granted', () => {
    const graph = makeValid(loadFixture('feature-flag-matched'));
    expect(isGranted(validator.requireFeatureFlag(graph, 'WMS_NEW_PICKING_UI'))).toBe(true);
    expect(isGranted(validator.requireFeatureFlag(graph, 'WMS_EXPRESS_CHECKOUT'))).toBe(true);
  });

  it('feature-flag-missed-context: present but disabled returns AUTH_107', () => {
    const graph = makeValid(loadFixture('feature-flag-missed-context'));
    const decision = validator.requireFeatureFlag(graph, 'WMS_NEW_PICKING_UI');
    expect(decision.errorCode).toBe('AUTH_107');
  });

  it('multi-tenant-rejection: assertTenant fails with AUTH_109', () => {
    const graph = makeValid(loadFixture('multi-tenant-rejection'));
    const decision = validator.assertTenant(graph, 'LOGISTICS_CORE');
    expect(decision.status).toBe('TenantMismatch');
    expect(decision.errorCode).toBe('AUTH_109');
    expect(isGranted(validator.assertTenant(graph, 'ACME_RETAIL'))).toBe(true);
  });

  it('schema-unsupported-major: rejects with AUTH_205', () => {
    const raw = loadFixture('schema-unsupported-major');
    const graph: AuthorizationGraph = { ...raw, validUntil: new Date(Date.now() + 60 * 60 * 1000).toISOString() };
    const decision = validator.requireScope(graph, 'ANY.SCOPE');
    expect(decision.status).toBe('SchemaUnsupported');
    expect(decision.errorCode).toBe('AUTH_205');
  });

  it('missing graph: rejects with AUTH_202', () => {
    const decision = validator.requireScope(null, 'ANY.SCOPE');
    expect(decision.status).toBe('GraphMissing');
    expect(decision.errorCode).toBe('AUTH_202');
  });
});
