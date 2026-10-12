import React from 'react';
import { Info } from 'lucide-react';

interface RequiresFilterPromptProps {
  title?: string;
  message?: string;
  className?: string;
}

export const RequiresFilterPrompt: React.FC<RequiresFilterPromptProps> = ({
  title = 'Aplica un filtro para cargar',
  message = 'Selecciona un estado o ingresa un término de búsqueda para visualizar los elementos.',
  className = '',
}) => (
  <div
    className={`flex flex-col items-center justify-center h-full text-center py-16 ${className}`}
  >
    <div className="p-4 rounded-2xl bg-m3-primary/5 border border-m3-primary/10 mb-4">
      <Info className="w-8 h-8 text-m3-primary/60" />
    </div>
    <h3 className="text-sm font-semibold text-m3-on-surface mb-1">{title}</h3>
    <p className="text-xs text-m3-secondary/70 max-w-xs">{message}</p>
  </div>
);
