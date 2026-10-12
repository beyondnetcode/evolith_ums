import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AccessGuard, withAccessGuard } from './AccessGuard';

const hasModuleAccess = vi.fn();
const hasMenuAccess = vi.fn();
const hasOptionAccess = vi.fn();
const hasFeatureFlag = vi.fn();

vi.mock('@app/authorization/hooks/use-access-resolution', () => ({
  useAccessResolution: () => ({
    hasModuleAccess,
    hasMenuAccess,
    hasOptionAccess,
    hasFeatureFlag,
  }),
}));

const Child = () => <div data-testid="child">contenido protegido</div>;
const Fallback = () => <div data-testid="fallback">sin acceso</div>;

describe('AccessGuard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    hasModuleAccess.mockReturnValue(true);
    hasMenuAccess.mockReturnValue(true);
    hasOptionAccess.mockReturnValue(true);
    hasFeatureFlag.mockReturnValue(true);
  });

  it('muestra los hijos cuando no se especifica ningún requisito', () => {
    render(
      <AccessGuard>
        <Child />
      </AccessGuard>
    );
    expect(screen.getByTestId('child')).toBeInTheDocument();
  });

  it('oculta los hijos y muestra fallback cuando falta el feature flag', () => {
    hasFeatureFlag.mockReturnValue(false);
    render(
      <AccessGuard featureCode="NUEVO_TABLERO" fallback={<Fallback />}>
        <Child />
      </AccessGuard>
    );
    expect(screen.queryByTestId('child')).not.toBeInTheDocument();
    expect(screen.getByTestId('fallback')).toBeInTheDocument();
  });

  it('renderiza null (nada) por defecto cuando no hay acceso', () => {
    hasModuleAccess.mockReturnValue(false);
    const { container } = render(
      <AccessGuard moduleCode="DESPACHO">
        <Child />
      </AccessGuard>
    );
    expect(screen.queryByTestId('child')).not.toBeInTheDocument();
    expect(container).toBeEmptyDOMElement();
  });

  it('permite acceso cuando el módulo está autorizado', () => {
    render(
      <AccessGuard moduleCode="DESPACHO">
        <Child />
      </AccessGuard>
    );
    expect(hasModuleAccess).toHaveBeenCalledWith('DESPACHO');
    expect(screen.getByTestId('child')).toBeInTheDocument();
  });

  it('deniega acceso cuando la opción del menú no está autorizada', () => {
    hasOptionAccess.mockReturnValue(false);
    render(
      <AccessGuard menuCode="USUARIOS" optionCode="CREAR" fallback={<Fallback />}>
        <Child />
      </AccessGuard>
    );
    expect(hasOptionAccess).toHaveBeenCalledWith('USUARIOS', 'CREAR');
    expect(screen.getByTestId('fallback')).toBeInTheDocument();
  });

  it('deniega acceso a nivel de menú cuando hay módulo y menú pero no opción', () => {
    hasMenuAccess.mockReturnValue(false);
    render(
      <AccessGuard moduleCode="DESPACHO" menuCode="USUARIOS" fallback={<Fallback />}>
        <Child />
      </AccessGuard>
    );
    expect(hasMenuAccess).toHaveBeenCalledWith('DESPACHO', 'USUARIOS');
    expect(screen.getByTestId('fallback')).toBeInTheDocument();
  });

  it('cortocircuita: si el feature flag falla no consulta el módulo', () => {
    hasFeatureFlag.mockReturnValue(false);
    render(
      <AccessGuard featureCode="X" moduleCode="DESPACHO">
        <Child />
      </AccessGuard>
    );
    expect(hasModuleAccess).not.toHaveBeenCalled();
    expect(screen.queryByTestId('child')).not.toBeInTheDocument();
  });
});

describe('withAccessGuard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    hasModuleAccess.mockReturnValue(true);
    hasMenuAccess.mockReturnValue(true);
    hasOptionAccess.mockReturnValue(true);
    hasFeatureFlag.mockReturnValue(true);
  });

  it('envuelve un componente y respeta el acceso concedido', () => {
    const Wrapped = withAccessGuard(
      (props: { label: string }) => <div data-testid="wrapped">{props.label}</div>,
      { moduleCode: 'DESPACHO' }
    );
    render(<Wrapped label="hola" />);
    expect(screen.getByTestId('wrapped')).toHaveTextContent('hola');
  });

  it('oculta el componente envuelto cuando se deniega el acceso', () => {
    hasModuleAccess.mockReturnValue(false);
    const Wrapped = withAccessGuard(
      (props: { label: string }) => <div data-testid="wrapped">{props.label}</div>,
      { moduleCode: 'DESPACHO' }
    );
    render(<Wrapped label="hola" />);
    expect(screen.queryByTestId('wrapped')).not.toBeInTheDocument();
  });
});
