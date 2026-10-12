/**
 * system-suites.mock.ts — Datos de prueba del árbol de nodos recursivo (ADR-0090)
 *
 * Refleja el modelo flexible Suite→Módulo→nodos (kind Menu/SubMenu/Option con
 * profundidad variable y funcionalidad N:M por `actionCodes`). Los IDs son
 * marcadores; el frontend depende de la respuesta REST, no de IDs fijos.
 * Se usa principalmente para pruebas unitarias y Storybook.
 */
import type {
  SystemSuite,
  SystemSuiteDomainResource,
} from '@domain/authorization/models/system-suite.model';
import type { SystemSuiteNode } from '@domain/authorization/schemas/system-suite.schema';

// ── Constants matching CoreDevDataSeeder ────────────────────────────────────
const RANSA_TENANT_ID = '3fa85f64-5717-4562-b3fc-2c963f66afa6';

// Helper para construir nodos hoja (Opción) con su vínculo N:M.
const optionNode = (
  id: string,
  code: string,
  label: string,
  description: string,
  actionCode: string,
  sortOrder: number
): SystemSuiteNode => ({
  id,
  parentNodeId: null,
  kind: 'Option',
  code,
  label,
  description,
  status: 'Active',
  sortOrder,
  actionCodes: [actionCode],
  metadata: null,
  children: [],
});

const branchNode = (
  id: string,
  kind: 'Menu' | 'SubMenu',
  code: string,
  label: string,
  description: string,
  sortOrder: number,
  children: SystemSuiteNode[]
): SystemSuiteNode => ({
  id,
  parentNodeId: null,
  kind,
  code,
  label,
  description,
  status: 'Active',
  sortOrder,
  actionCodes: [],
  metadata: null,
  children,
});

// ── Suite 1: LOGISTICS_CORE ──────────────────────────────────────────────────
const logisticsCoreModules = [
  {
    id: 'sec-module-id',
    code: 'SEC',
    name: 'Security & Auditing',
    description: 'Security, user management, and audit trailing modules',
    status: 'Active',
    sortOrder: 1,
    nodes: [
      branchNode('users-menu-id', 'Menu', 'USERS', 'Users Administration', 'Manage user accounts', 1, [
        branchNode('list-submenu-id', 'SubMenu', 'LIST', 'User Directory', 'View and search users', 1, [
          optionNode('view-users-opt-id', 'VIEW_USERS', 'View Users List', 'View users list', 'VIEW', 1),
          optionNode('edit-users-opt-id', 'EDIT_USERS', 'Edit User Profiles', 'Edit user profiles', 'MANAGE', 2),
        ]),
        branchNode('roles-submenu-id', 'SubMenu', 'ROLES', 'Roles & Permissions', 'Manage roles', 2, [
          optionNode('view-roles-opt-id', 'VIEW_ROLES', 'View Security Roles', 'View roles', 'VIEW', 1),
          optionNode('manage-roles-opt-id', 'MANAGE_ROLES', 'Configure Permissions', 'Edit access rights', 'MANAGE', 2),
        ]),
      ]),
      branchNode('audit-menu-id', 'Menu', 'AUDIT', 'Audit Trails', 'System operations logging', 2, [
        branchNode('logs-submenu-id', 'SubMenu', 'LOGS', 'System Logs', 'View telemetry', 1, [
          optionNode('view-logs-opt-id', 'VIEW_LOGS', 'Search Audit Trail', 'Query audit logs', 'VIEW', 1),
          optionNode('purge-logs-opt-id', 'PURGE_LOGS', 'Purge Historical Data', 'Clear log records', 'APPROVE', 2),
        ]),
      ]),
    ],
  },
  {
    id: 'config-module-id',
    code: 'CONFIG',
    name: 'System Configuration',
    description: 'Global properties, settings and email setup',
    status: 'Active',
    sortOrder: 2,
    nodes: [
      branchNode('settings-menu-id', 'Menu', 'SETTINGS', 'Global Setup', 'Configure global variables', 1, [
        branchNode('params-submenu-id', 'SubMenu', 'PARAMS', 'App Parameters', 'Timeouts and limits', 1, [
          optionNode('view-params-opt-id', 'VIEW_PARAMS', 'View Parameters', 'View options', 'VIEW', 1),
          optionNode('edit-params-opt-id', 'EDIT_PARAMS', 'Update Global Config', 'Edit global values', 'MANAGE', 2),
        ]),
        branchNode('smtp-submenu-id', 'SubMenu', 'SMTP', 'SMTP Server Setup', 'Email gateway', 2, [
          optionNode('test-smtp-opt-id', 'TEST_SMTP', 'Test SMTP Gateway', 'Trigger email test', 'APPROVE', 1),
        ]),
      ]),
    ],
  },
];

const logisticsCoreActions = [
  { id: 'view-action-id', code: 'VIEW', name: 'View Logistics Core' },
  { id: 'manage-action-id', code: 'MANAGE', name: 'Manage Logistics Core' },
  { id: 'approve-action-id', code: 'APPROVE', name: 'Approve Operations' },
  { id: 'create-action-id', code: 'CREATE', name: 'Create Record' },
  { id: 'read-action-id', code: 'READ', name: 'Read Record' },
  { id: 'update-action-id', code: 'UPDATE', name: 'Update Record' },
  { id: 'delete-action-id', code: 'DELETE', name: 'Delete Record' },
  { id: 'search-action-id', code: 'SEARCH', name: 'Search Records' },
];

const logisticsCoreDomainResources: SystemSuiteDomainResource[] = [
  {
    id: 'users-agg-id',
    type: 'Aggregate',
    code: 'USERS',
    name: 'Users Aggregate',
    description: 'User Management',
    moduleId: null,
    parentResourceId: null,
  },
  {
    id: 'inventory-agg-id',
    type: 'Aggregate',
    code: 'INVENTORY',
    name: 'Inventory Aggregate',
    description: 'Inventory Management',
    moduleId: null,
    parentResourceId: null,
  },
  {
    id: 'audit-log-ent-id',
    type: 'Entity',
    code: 'AUDIT_LOG',
    name: 'Audit Log Entity',
    description: 'Audit Logs',
    moduleId: null,
    parentResourceId: null,
  },
  {
    id: 'stock-level-ent-id',
    type: 'Entity',
    code: 'STOCK_LEVEL',
    name: 'Stock Level Entity',
    description: 'Stock Levels',
    moduleId: null,
    parentResourceId: null,
  },
];

// ── Suite 2: WMS ────────────────────────────────────────────────────────────
const wmsModules = [
  {
    id: 'inv-module-id',
    code: 'INV',
    name: 'Inventory Control',
    description: 'Inventory management and levels',
    status: 'Active',
    sortOrder: 1,
    nodes: [
      branchNode('stock-menu-id', 'Menu', 'STOCK', 'Stock Administration', 'Stock levels and status', 1, [
        branchNode('levels-submenu-id', 'SubMenu', 'LEVELS', 'Real-time Levels', 'Current stock status', 1, [
          optionNode('view-stock-opt-id', 'VIEW_STOCK', 'View Stock Levels', 'View inventory counts', 'INVENTORY_VIEW', 1),
          optionNode('adjust-stock-opt-id', 'ADJUST_STOCK', 'Adjust Inventory Counts', 'Physical adjustments', 'INVENTORY_EDIT', 2),
        ]),
      ]),
      branchNode('ops-menu-id', 'Menu', 'OPS', 'Warehouse Operations', 'Stock movements', 2, [
        branchNode('transfers-submenu-id', 'SubMenu', 'TRANSFERS', 'Warehouse Transfers', 'Move stock', 1, [
          optionNode('initiate-transfer-opt-id', 'INITIATE_TRANSFER', 'Initiate Stock Transfer', 'Start a transfer', 'INVENTORY_EDIT', 1),
          optionNode('approve-transfer-opt-id', 'APPROVE_TRANSFER', 'Approve Location Transfer', 'Authorize relocation', 'INVENTORY_EDIT', 2),
        ]),
      ]),
    ],
  },
];

const wmsActions = [
  { id: 'inv-view-action-id', code: 'INVENTORY_VIEW', name: 'View Inventory' },
  { id: 'inv-edit-action-id', code: 'INVENTORY_EDIT', name: 'Edit Inventory' },
];

const wmsDomainResources: SystemSuiteDomainResource[] = [];

// ── Exported mock array ──────────────────────────────────────────────────────
export const mockSystemSuites: SystemSuite[] = [
  {
    systemSuiteId: 'logistics-core-id',
    tenantId: RANSA_TENANT_ID,
    code: 'LOGISTICS_CORE',
    name: 'Logistics Core',
    description: 'Core operations and user management',
    status: 'Active',
    modules: logisticsCoreModules,
    actions: logisticsCoreActions,
    domainResources: logisticsCoreDomainResources,
  },
  {
    systemSuiteId: 'wms-id',
    tenantId: RANSA_TENANT_ID,
    code: 'WMS',
    name: 'Warehouse Management',
    description: 'Warehouse inventory management',
    status: 'Active',
    modules: wmsModules,
    actions: wmsActions,
    domainResources: wmsDomainResources,
  },
];
