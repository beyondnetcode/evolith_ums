import React, { useEffect, useRef, useState } from 'react';
import { Check, ChevronDown, IdCard } from 'lucide-react';
import { useAuthStore } from '@app/stores/auth.store';
import type { GraphProfileOption } from '@domain/authorization/schemas/authorization-graph.schema';

/**
 * Selector del perfil vigente.
 *
 * Un perfil ata un usuario, un rol y —por el rol— un sistema, opcionalmente acotado a una
 * sucursal. Un usuario con varios tenía que cerrar sesión y volver a entrar para cambiarse de
 * sombrero, y ni siquiera sabía que existían: el grafo elegía uno y descartaba el resto.
 *
 * Dos decisiones de interfaz que conviene no revertir sin pensarlo:
 *
 *  1. **Con un solo perfil no se pinta nada.** Un desplegable de un elemento es ruido que sugiere
 *     una elección inexistente. El dato viaja igual, y es el componente quien decide.
 *  2. **La sucursal se muestra siempre que exista.** Dos perfiles con el mismo rol en Callao y en
 *     Paita son indistinguibles sin ella, y elegir mal significa operar sobre la sucursal
 *     equivocada.
 */
export const ProfileSelector: React.FC<{ className?: string }> = ({ className }) => {
  const { user, switchProfile } = useAuthStore();
  const [abierto, setAbierto] = useState(false);
  const [cambiando, setCambiando] = useState(false);
  const contenedor = useRef<HTMLDivElement>(null);

  const perfiles: GraphProfileOption[] = user?.authorizationGraph?.profiles ?? [];
  const vigente = perfiles.find(p => p.isCurrent);

  useEffect(() => {
    function alPulsarFuera(evento: MouseEvent) {
      if (contenedor.current && !contenedor.current.contains(evento.target as Node)) {
        setAbierto(false);
      }
    }
    document.addEventListener('mousedown', alPulsarFuera);
    return () => document.removeEventListener('mousedown', alPulsarFuera);
  }, []);

  // Con un único perfil no hay nada que elegir.
  if (perfiles.length < 2) return null;

  const rotulo = (p: GraphProfileOption) =>
    p.branch ? `${p.role.value} · ${p.system.code} · ${p.branch.value}` : `${p.role.value} · ${p.system.code}`;

  const cambiar = async (p: GraphProfileOption) => {
    if (p.isCurrent || cambiando || !p.id) return;

    setCambiando(true);
    const ok = await switchProfile(p.id);
    setCambiando(false);
    setAbierto(false);

    // El grafo nuevo llega entero: el árbol de navegación, los permisos y el rol cambian a la vez.
    // Se recarga la vista en vez de reconciliar pantalla por pantalla, porque una pantalla abierta
    // puede haber dejado de estar concedida y reconciliar eso a mano es una fuente de fugas.
    if (ok) window.location.reload();
  };

  return (
    <div ref={contenedor} className={`relative select-none ${className || ''}`}>
      <button
        type="button"
        onClick={() => setAbierto(!abierto)}
        disabled={cambiando}
        aria-haspopup="listbox"
        aria-expanded={abierto}
        aria-label="Cambiar de perfil"
        className="flex items-center gap-2 px-3 py-1.5 rounded-lg border border-m3-outline/30 bg-m3-surface hover:bg-m3-primary/5 transition-all text-sm text-m3-on-surface focus:outline-none focus:ring-2 focus:ring-m3-primary disabled:opacity-60 cursor-pointer"
      >
        <IdCard className="w-4 h-4 text-m3-primary shrink-0" aria-hidden="true" />
        <span className="font-medium truncate max-w-[14rem]">
          {vigente ? rotulo(vigente) : 'Seleccionar perfil'}
        </span>
        <ChevronDown
          className={`w-4 h-4 shrink-0 transition-transform ${abierto ? 'rotate-180' : ''}`}
          aria-hidden="true"
        />
      </button>

      {abierto && (
        <ul
          role="listbox"
          aria-label="Perfiles disponibles"
          className="absolute right-0 z-50 mt-1 w-80 max-h-80 overflow-auto rounded-lg border border-m3-outline/30 bg-m3-surface shadow-lg py-1"
        >
          {perfiles.map(p => (
            <li key={p.id ?? `${p.system.code}-${p.role.code}-${p.branch?.code ?? ''}`}>
              <button
                type="button"
                role="option"
                aria-selected={p.isCurrent}
                onClick={() => cambiar(p)}
                disabled={cambiando}
                className="w-full flex items-start gap-2 px-3 py-2 text-left hover:bg-m3-primary/5 transition-colors disabled:opacity-60 cursor-pointer"
              >
                <Check
                  className={`w-4 h-4 mt-0.5 shrink-0 text-m3-primary ${p.isCurrent ? '' : 'invisible'}`}
                  aria-hidden="true"
                />
                <span className="min-w-0">
                  <span className="block text-sm font-medium text-m3-on-surface truncate">
                    {p.role.value}
                  </span>
                  <span className="block text-xs text-m3-on-surface/70 truncate">
                    {p.system.value}
                    {p.branch ? ` · ${p.branch.value}` : ''}
                  </span>
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
};
