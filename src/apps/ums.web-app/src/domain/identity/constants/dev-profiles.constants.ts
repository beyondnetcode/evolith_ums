/**
 * dev-profiles.constants.ts — Development profile presets for local testing.
 *
 * M-5: Moved hardcoded GUIDs from LoginScreen.tsx.
 */

export interface DevProfile {
  nameKey: string;
  id: string;
  role: string;
  email: string;
  username: string;
  tenantId: string;
}

export const DEV_PROFILES: DevProfile[] = [
  {
    nameKey: 'devProfile1',
    id: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    role: 'admin',
    email: 'admin@ransa.pe',
    username: 'admin_root',
    tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
  },
  {
    nameKey: 'devProfile2',
    id: '8a7b6c5d-4e3f-2a1b-0c9d-8e7f6a5b4c3d',
    role: 'moderator',
    email: 'operaciones@ransa.pe',
    username: 'gerente_ops',
    tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
  },
  {
    nameKey: 'devProfile3',
    id: '9f8e7d6c-5b4a-3f2e-1d0c-9b8a7f6e5d4c',
    role: 'user',
    email: 'auditoria@ransa.pe',
    username: 'auditor_est',
    tenantId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
  },
];
