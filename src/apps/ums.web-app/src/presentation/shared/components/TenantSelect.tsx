import React, { useEffect, useId, useMemo, useRef, useState } from 'react';
import { ChevronDown, Building2 } from 'lucide-react';
import { M3TextField } from './M3TextField';
import {
  LONGITUD_MAXIMA_CODIGO_INQUILINO,
  normalizarCodigoInquilino,
  type InquilinoSugerido,
} from '@domain/identity/constants/tenant.constants';

export type TenantOption = InquilinoSugerido;

export interface TenantSelectProps {
  /** Rótulo visible del campo; es también su nombre accesible. */
  label: string;
  /** Código del inquilino. Es lo que viaja a la API: no hay identificador de por medio. */
  value: string;
  onChange: (codigo: string) => void;
  /**
   * Sugerencias para teclear menos. Puede venir vacía —y lo estará en cualquier despliegue que
   * no las declare—: entonces el campo es texto libre y nada más.
   */
  sugerencias?: readonly InquilinoSugerido[];
  placeholder?: string;
  error?: string;
  helperText?: string;
  disabled?: boolean;
  required?: boolean;
}

/**
 * Campo del inquilino de la pantalla de ingreso.
 *
 * ERA un desplegable cerrado alimentado por una lista compilada en el bundle (`DEV_TENANTS`), y
 * eso es el defecto G-263: fuera de desarrollo ofrecía inquilinos que no existen, escondía los
 * que sí, y cada opción llevaba un GUID cableado al sembrador que ningún otro entorno comparte.
 *
 * AHORA es una caja combinada (ARIA 1.2 `combobox` con autocompletado de lista): el usuario
 * **teclea su código** —que es lo único que el contrato de ingreso consume— y las sugerencias,
 * si el despliegue declara alguna, solo ahorran tecleo. Nunca limitan.
 *
 * Lo que sigue abierto de G-263 no se puede cerrar desde aquí: que la lista la sirva la API.
 * Hoy no existe endpoint anónimo que publique los inquilinos que el ingreso admite.
 */
export const TenantSelect: React.FC<TenantSelectProps> = ({
  label,
  value,
  onChange,
  sugerencias = [],
  placeholder = 'Ej.: EVOLITH',
  error,
  helperText,
  disabled = false,
  required = true,
}) => {
  const [abierta, setAbierta] = useState(false);
  const [activo, setActivo] = useState(-1);
  /**
   * Solo se filtra por lo TECLEADO. Abriendo la lista con el botón o con ↓ se ven todas las
   * sugerencias: si se filtrase siempre por el valor vigente, el desplegable enseñaría una sola
   * fila —la ya elegida— y no habría forma de ver las demás sin borrar antes el campo.
   */
  const [filtrando, setFiltrando] = useState(false);
  const contenedorRef = useRef<HTMLDivElement>(null);

  /**
   * `M3TextField` no reenvía `ref`, y devolverle el foco tras elegir una sugerencia no es un
   * adorno: sin ello el foco se queda en el `<li>` que acaba de desaparecer y el teclado pierde
   * el hilo (WCAG 2.2 §2.4.3). El campo tiene exactamente un `<input>`, así que se busca ahí.
   */
  const enfocarEntrada = (): void => {
    contenedorRef.current?.querySelector('input')?.focus();
  };

  const idBase = useId();
  const idEntrada = `${idBase}-inquilino`;
  const idLista = `${idBase}-sugerencias`;

  const haySugerencias = sugerencias.length > 0;

  const filtradas = useMemo(() => {
    const aguja = value.trim().toLowerCase();
    if (!filtrando || aguja.length === 0) return [...sugerencias];
    return sugerencias.filter(
      s => s.code.toLowerCase().includes(aguja) || s.name.toLowerCase().includes(aguja)
    );
  }, [sugerencias, value, filtrando]);

  const listaVisible = abierta && filtradas.length > 0;

  // Cierre al pulsar fuera. La suscripción al documento se retira al desmontar (P-FE-04).
  useEffect(() => {
    if (!listaVisible) return;
    const alPulsarFuera = (evento: MouseEvent): void => {
      if (contenedorRef.current && !contenedorRef.current.contains(evento.target as Node)) {
        setAbierta(false);
        setActivo(-1);
      }
    };
    document.addEventListener('mousedown', alPulsarFuera);
    return () => document.removeEventListener('mousedown', alPulsarFuera);
  }, [listaVisible]);

  const cerrar = (): void => {
    setAbierta(false);
    setActivo(-1);
    setFiltrando(false);
  };

  const elegir = (codigo: string): void => {
    onChange(codigo);
    cerrar();
    enfocarEntrada();
  };

  /** Recorre las sugerencias; con la lista cerrada, la primera flecha la abre. */
  const mover = (paso: number): void => {
    if (!abierta) {
      setFiltrando(false);
      setAbierta(true);
      setActivo(paso > 0 ? 0 : sugerencias.length - 1);
      return;
    }
    const total = Math.max(filtradas.length, 1);
    setActivo(anterior => (anterior + paso + total) % total);
  };

  const alTeclear = (evento: React.KeyboardEvent<HTMLInputElement>): void => {
    // Escape se atiende aunque no haya sugerencias, y se detiene aquí: con la lista abierta la
    // cierra y no debe alcanzar al diálogo o panel que envuelva al campo (WCAG 2.2, patrón ARIA).
    if (evento.key === 'Escape') {
      if (!abierta) return;
      evento.preventDefault();
      evento.stopPropagation();
      cerrar();
      return;
    }

    if (!haySugerencias) return;

    if (evento.key === 'ArrowDown' || evento.key === 'ArrowUp') {
      evento.preventDefault();
      mover(evento.key === 'ArrowDown' ? 1 : -1);
      return;
    }

    if (evento.key === 'Enter') {
      const elegida = abierta && activo >= 0 ? filtradas[activo] : undefined;
      // Sin `preventDefault` Enter enviaría el formulario con el código a medio escribir.
      if (elegida) {
        evento.preventDefault();
        elegir(elegida.code);
      }
      return;
    }

    if (evento.key === 'Tab' && abierta) cerrar();
  };

  /** Resalte de la fila: primero la que recorre el teclado, luego la ya elegida. */
  const claseFila = (indice: number, seleccionada: boolean): string => {
    if (indice === activo) return 'bg-m3-primary-container/50';
    return seleccionada ? 'bg-m3-primary-container/30' : '';
  };

  const activoId = listaVisible && activo >= 0 ? `${idLista}-${activo}` : undefined;

  return (
    <div ref={contenedorRef} className="relative w-full">
      <M3TextField
        id={idEntrada}
        label={label}
        value={value}
        onChange={evento => {
          onChange(normalizarCodigoInquilino(evento.target.value));
          setActivo(-1);
          setFiltrando(true);
          if (haySugerencias) setAbierta(true);
        }}
        onKeyDown={alTeclear}
        error={error}
        helperText={helperText}
        placeholder={placeholder}
        disabled={disabled}
        required={required}
        maxLength={LONGITUD_MAXIMA_CODIGO_INQUILINO}
        autoComplete="organization"
        autoCorrect="off"
        spellCheck={false}
        icon={<Building2 className="w-4 h-4" aria-hidden="true" />}
        iconPosition="start"
        role="combobox"
        aria-expanded={listaVisible}
        aria-controls={idLista}
        aria-autocomplete="list"
        aria-activedescendant={activoId}
      />

      {haySugerencias && (
        <button
          type="button"
          // Fuera del orden de tabulación a propósito (patrón ARIA de caja combinada): el campo
          // ya abre la lista con ↓, y un tabulador de más entre inquilino y correo estorba.
          tabIndex={-1}
          disabled={disabled}
          onClick={() => {
            setAbierta(anterior => !anterior);
            setActivo(-1);
            setFiltrando(false);
            enfocarEntrada();
          }}
          aria-label={abierta ? 'Ocultar inquilinos sugeridos' : 'Ver inquilinos sugeridos'}
          aria-expanded={listaVisible}
          aria-controls={idLista}
          className="absolute right-1 top-7 -translate-y-1/2 h-11 w-11 flex items-center justify-center rounded-full text-m3-secondary hover:text-m3-on-surface hover:bg-m3-surface-container/50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-m3-primary disabled:opacity-50 disabled:cursor-not-allowed"
        >
          <ChevronDown
            className={`w-4 h-4 transition-transform motion-reduce:transition-none ${abierta ? 'rotate-180' : ''}`}
            aria-hidden="true"
          />
        </button>
      )}

      <ul
        id={idLista}
        role="listbox"
        aria-label={`Inquilinos sugeridos para ${label}`}
        hidden={!listaVisible}
        className="absolute left-0 right-0 top-14 mt-1 z-50 max-h-64 overflow-y-auto rounded-xl border border-m3-outline/50 bg-m3-surface-container shadow-lg"
      >
        {filtradas.map((sugerida, indice) => {
          const seleccionada = sugerida.code === value;
          return (
            <li
              key={sugerida.code}
              id={`${idLista}-${indice}`}
              role="option"
              aria-selected={seleccionada}
              // `mousedown` y no `click`: el pulsado ya quita el foco del campo y la lista se
              // cerraría antes de que llegara el `click`.
              onMouseDown={evento => {
                evento.preventDefault();
                elegir(sugerida.code);
              }}
              onMouseEnter={() => setActivo(indice)}
              className={`cursor-pointer px-4 py-3 flex items-center gap-3 ${claseFila(indice, seleccionada)}`}
            >
              <Building2 className="w-4 h-4 text-m3-primary flex-shrink-0" aria-hidden="true" />
              <span className="min-w-0">
                <span className="block text-sm font-semibold text-m3-on-surface">
                  {sugerida.code}
                </span>
                <span className="block text-xs text-m3-secondary truncate">{sugerida.name}</span>
              </span>
            </li>
          );
        })}
      </ul>
    </div>
  );
};
