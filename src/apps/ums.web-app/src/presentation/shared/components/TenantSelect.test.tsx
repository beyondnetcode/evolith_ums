import React, { useState } from 'react';
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TenantSelect } from './TenantSelect';
import type { InquilinoSugerido } from '@domain/identity/constants/tenant.constants';

const SUGERENCIAS: InquilinoSugerido[] = [
  { code: 'EVOLITH', name: 'Evolith UMS — Inquilino raíz' },
  { code: 'COMEX_ANDINA', name: 'Comercializadora Andina S.A.C.' },
];

/** Envoltura controlada: el campo es controlado y sin estado no se ve lo que se teclea. */
function CampoControlado({
  inicial = '',
  sugerencias,
  onChange,
}: {
  inicial?: string;
  sugerencias?: InquilinoSugerido[];
  onChange?: (codigo: string) => void;
}): React.JSX.Element {
  const [valor, setValor] = useState(inicial);
  return (
    <TenantSelect
      label="Inquilino"
      value={valor}
      onChange={codigo => {
        setValor(codigo);
        onChange?.(codigo);
      }}
      {...(sugerencias ? { sugerencias } : {})}
    />
  );
}

describe('TenantSelect', () => {
  it('es una caja combinada cuyo nombre accesible es el rótulo', () => {
    render(<CampoControlado sugerencias={SUGERENCIAS} />);

    const campo = screen.getByRole('combobox', { name: /inquilino/i });
    expect(campo).toHaveAttribute('aria-expanded', 'false');
    expect(campo).toHaveAttribute('aria-autocomplete', 'list');
  });

  it('admite cualquier código tecleado y lo normaliza a mayúsculas', async () => {
    // G-263: el ingreso no puede quedar limitado a una lista compilada; un inquilino que el
    // bundle no conoce tiene que poder entrar.
    const usuario = userEvent.setup();
    const alCambiar = vi.fn();
    render(<CampoControlado sugerencias={SUGERENCIAS} onChange={alCambiar} />);

    await usuario.type(screen.getByRole('combobox', { name: /inquilino/i }), 'impo_andina_sub');

    expect(screen.getByRole('combobox', { name: /inquilino/i })).toHaveValue('IMPO_ANDINA_SUB');
    expect(alCambiar).toHaveBeenLastCalledWith('IMPO_ANDINA_SUB');
  });

  it('sin sugerencias no ofrece desplegable alguno', () => {
    render(<CampoControlado />);

    expect(screen.queryByRole('button', { name: /inquilinos sugeridos/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('despliega las sugerencias y adopta la elegida', async () => {
    const usuario = userEvent.setup();
    render(<CampoControlado sugerencias={SUGERENCIAS} />);

    await usuario.click(screen.getByRole('button', { name: /ver inquilinos sugeridos/i }));

    const opciones = screen.getAllByRole('option');
    expect(opciones).toHaveLength(2);

    await usuario.click(screen.getByRole('option', { name: /COMEX_ANDINA/ }));

    expect(screen.getByRole('combobox', { name: /inquilino/i })).toHaveValue('COMEX_ANDINA');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('filtra las sugerencias por lo que se teclea', async () => {
    const usuario = userEvent.setup();
    render(<CampoControlado sugerencias={SUGERENCIAS} />);

    await usuario.type(screen.getByRole('combobox', { name: /inquilino/i }), 'comex');

    const opciones = screen.getAllByRole('option');
    expect(opciones).toHaveLength(1);
    expect(opciones[0]).toHaveTextContent('COMEX_ANDINA');
  });

  it('desplegado a mano muestra todas las sugerencias, no solo la ya elegida', async () => {
    // Filtrar por el valor vigente dejaba el desplegable con una sola fila y sin salida:
    // había que borrar el campo para volver a ver el resto.
    const usuario = userEvent.setup();
    render(<CampoControlado inicial="EVOLITH" sugerencias={SUGERENCIAS} />);

    await usuario.click(screen.getByRole('button', { name: /ver inquilinos sugeridos/i }));

    expect(screen.getAllByRole('option')).toHaveLength(2);
  });

  it('se recorre con el teclado y Escape cierra la lista sin enviar nada', async () => {
    const usuario = userEvent.setup();
    const alEnviar = vi.fn(event => event.preventDefault());
    render(
      <form onSubmit={alEnviar}>
        <CampoControlado sugerencias={SUGERENCIAS} />
        <button type="submit">Ingresar</button>
      </form>
    );

    const campo = screen.getByRole('combobox', { name: /inquilino/i });
    campo.focus();

    await usuario.keyboard('{ArrowDown}{ArrowDown}');
    expect(screen.getByRole('listbox')).toBeInTheDocument();

    await usuario.keyboard('{Enter}');
    expect(campo).toHaveValue('COMEX_ANDINA');
    // Enter eligió la sugerencia; no envió el formulario.
    expect(alEnviar).not.toHaveBeenCalled();

    await usuario.keyboard('{ArrowDown}');
    expect(screen.getByRole('listbox')).toBeInTheDocument();

    await usuario.keyboard('{Escape}');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    expect(alEnviar).not.toHaveBeenCalled();
  });
});
