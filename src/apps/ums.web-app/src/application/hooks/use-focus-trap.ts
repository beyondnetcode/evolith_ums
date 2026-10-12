import { useEffect, useRef, useCallback } from 'react';

const FOCUSABLE_SELECTORS = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
  'area[href]',
  'iframe',
  'object',
  'embed',
  '[contenteditable]',
  'audio[controls]',
  'video[controls]',
  'summary',
].join(', ');

/**
 * useFocusTrap — traps focus inside a container and restores it on unmount.
 *
 * Usage:
 *   const { containerRef } = useFocusTrap({ active: isOpen, onEscape: onClose });
 *   <div ref={containerRef}>...</div>
 */
export function useFocusTrap({
  active = true,
  onEscape,
}: {
  active?: boolean;
  onEscape?: () => void;
} = {}) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const previousActiveElementRef = useRef<Element | null>(null);

  const trapFocus = useCallback(
    (e: KeyboardEvent) => {
      if (!containerRef.current || !active) return;

      if (e.key === 'Escape') {
        onEscape?.();
        return;
      }

      if (e.key !== 'Tab') return;

      const focusable = Array.from(
        containerRef.current.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTORS)
      ).filter(el => {
        return !el.hasAttribute('disabled') && el.offsetParent !== null;
      });

      if (focusable.length === 0) return;

      const firstFocusable = focusable[0];
      const lastFocusable = focusable[focusable.length - 1];

      if (e.shiftKey) {
        if (document.activeElement === firstFocusable) {
          e.preventDefault();
          lastFocusable.focus();
        }
      } else {
        if (document.activeElement === lastFocusable) {
          e.preventDefault();
          firstFocusable.focus();
        }
      }
    },
    [active, onEscape]
  );

  useEffect(() => {
    if (!active) return;

    // El contenedor se lee AQUÍ, no en el render. Al leerlo en el render, la primera pasada lo veía
    // en `null` (las refs se asignan después) y el efecto volvía a dispararse en el siguiente
    // render —el que provoca la primera tecla escrita en un campo del diálogo—, robándole el foco
    // al campo y llevándolo al primer botón. Con la barra espaciadora, ese botón acababa pulsado.
    const container = containerRef.current;

    previousActiveElementRef.current = document.activeElement;

    if (!container) return;

    const focusable = container.querySelector<HTMLElement>(FOCUSABLE_SELECTORS);
    if (focusable) {
      focusable.focus();
    } else {
      container.setAttribute('tabindex', '-1');
      container.focus();
    }

    document.addEventListener('keydown', trapFocus, true);

    // El resto del documento se vuelve INERTE mientras el overlay está abierto (P-UX-08 de
    // ADR-0110). Atrapar el tabulador no basta: sin `inert`, el contenido de detrás sigue siendo
    // alcanzable por el lector de pantalla y por el gesto de exploración táctil, así que la persona
    // que no ve el scrim no tiene forma de saber que hay un diálogo delante.
    //
    // Se marcan los hermanos del ancestro de primer nivel del contenedor, no el `body`: marcar el
    // padre del propio diálogo lo dejaría inerte a él también.
    const raizDelOverlay = (() => {
      let nodo: HTMLElement | null = container;
      while (nodo?.parentElement && nodo.parentElement !== document.body) {
        nodo = nodo.parentElement;
      }
      return nodo;
    })();

    const inertados = Array.from(document.body.children).filter(
      hermano => hermano !== raizDelOverlay && !hermano.contains(container)
    ) as HTMLElement[];
    inertados.forEach(hermano => hermano.setAttribute('inert', ''));

    return () => {
      inertados.forEach(hermano => hermano.removeAttribute('inert'));
      document.removeEventListener('keydown', trapFocus, true);
      if (
        previousActiveElementRef.current &&
        previousActiveElementRef.current instanceof HTMLElement
      ) {
        previousActiveElementRef.current.focus();
      }
    };
  }, [active, trapFocus]);

  return { containerRef };
}
