import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { ProfileSelector } from './ProfileSelector';

const switchProfile = vi.fn();
let perfiles: unknown[] = [];

vi.mock('@app/stores/auth.store', () => ({
  useAuthStore: () => ({
    user: { authorizationGraph: { profiles: perfiles } },
    switchProfile,
  }),
}));

const perfil = (
  id: string,
  systemCode: string,
  roleCode: string,
  isCurrent: boolean,
  branch: { code: string; value: string } | null = null
) => ({
  id,
  system: { code: systemCode, value: `Sistema ${systemCode}` },
  role: { code: roleCode, value: `Rol ${roleCode}`, hierarchyLevel: 1 },
  branch,
  scope: branch ? 'BranchScoped' : 'OrgWide',
  isCurrent,
});

describe('ProfileSelector', () => {
  beforeEach(() => {
    switchProfile.mockReset().mockResolvedValue(true);
    perfiles = [];
  });

  it('no se pinta cuando el usuario tiene un solo perfil', () => {
    perfiles = [perfil('p1', 'SDLC', 'PMO', true)];
    const { container } = render(<ProfileSelector />);
    // Un desplegable de un elemento sugiere una elección que no existe.
    expect(container).toBeEmptyDOMElement();
  });

  it('no se pinta cuando el grafo no trae perfiles', () => {
    const { container } = render(<ProfileSelector />);
    expect(container).toBeEmptyDOMElement();
  });

  it('muestra el perfil vigente y lista los disponibles', () => {
    perfiles = [perfil('p1', 'SDLC', 'PMO', true), perfil('p2', 'TMS', 'OPERADOR', false)];
    render(<ProfileSelector />);

    expect(screen.getByRole('button', { name: /cambiar de perfil/i })).toHaveTextContent('Rol PMO');

    fireEvent.click(screen.getByRole('button', { name: /cambiar de perfil/i }));
    expect(screen.getAllByRole('option')).toHaveLength(2);
    expect(screen.getByRole('option', { selected: true })).toHaveTextContent('Rol PMO');
  });

  it('distingue dos perfiles del mismo rol por su sucursal', () => {
    perfiles = [
      perfil('p1', 'TMS', 'OPERADOR', true, { code: 'CALLAO', value: 'Callao' }),
      perfil('p2', 'TMS', 'OPERADOR', false, { code: 'PAITA', value: 'Paita' }),
    ];
    render(<ProfileSelector />);
    fireEvent.click(screen.getByRole('button', { name: /cambiar de perfil/i }));

    // Sin la sucursal serían indistinguibles y elegir mal significa operar sobre la equivocada.
    // Se busca dentro de la lista: el botón que la abre también rotula la sucursal vigente.
    const opciones = screen.getAllByRole('option').map(o => o.textContent ?? '');
    expect(opciones.some(t => t.includes('Callao'))).toBe(true);
    expect(opciones.some(t => t.includes('Paita'))).toBe(true);
  });

  it('cambia de perfil al elegir otro', async () => {
    perfiles = [perfil('p1', 'SDLC', 'PMO', true), perfil('p2', 'TMS', 'OPERADOR', false)];
    render(<ProfileSelector />);
    fireEvent.click(screen.getByRole('button', { name: /cambiar de perfil/i }));

    fireEvent.click(screen.getByRole('option', { name: /OPERADOR/i }));

    await waitFor(() => expect(switchProfile).toHaveBeenCalledWith('p2'));
  });

  it('no llama al servidor si se elige el perfil que ya está vigente', () => {
    perfiles = [perfil('p1', 'SDLC', 'PMO', true), perfil('p2', 'TMS', 'OPERADOR', false)];
    render(<ProfileSelector />);
    fireEvent.click(screen.getByRole('button', { name: /cambiar de perfil/i }));

    fireEvent.click(screen.getByRole('option', { selected: true }));

    expect(switchProfile).not.toHaveBeenCalled();
  });
});
