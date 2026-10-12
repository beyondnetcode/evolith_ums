import React, { useState, useRef, useEffect, useMemo } from 'react';
import { ChevronDown, Search, X, Check, AlertCircle, Loader2 } from 'lucide-react';

export type SearchCriteria = 'contains' | 'startsWith' | 'endsWith' | 'equals' | 'notEquals';

export interface SearchableSelectOption<T = string> {
  value: T;
  label: string;
  description?: string;
  disabled?: boolean;
  group?: string;
}

export interface SearchableSelectProps<T = string> {
  label: string;
  value: T | null;
  onChange: (value: T | null) => void;
  options: SearchableSelectOption<T>[];
  placeholder?: string;
  searchPlaceholder?: string;
  error?: string;
  helperText?: string;
  disabled?: boolean;
  required?: boolean;
  loading?: boolean;
  emptyMessage?: string;
  enableAdvancedSearch?: boolean;
  enableGrouping?: boolean;
  compact?: boolean;
  className?: string;
}

const CRITERIA_LABELS: Record<SearchCriteria, string> = {
  contains: 'Contiene',
  startsWith: 'Empieza con',
  endsWith: 'Termina con',
  equals: 'Es igual a',
  notEquals: 'No es igual a',
};

export function SearchableSelect<T extends string = string>({
  label,
  value,
  onChange,
  options,
  placeholder = 'Seleccionar...',
  searchPlaceholder = 'Buscar...',
  error,
  helperText,
  disabled = false,
  required = false,
  loading = false,
  emptyMessage = 'No se encontraron resultados',
  enableAdvancedSearch = true,
  enableGrouping = false,
  compact = false,
  className = '',
}: SearchableSelectProps<T>): React.JSX.Element {
  const [isOpen, setIsOpen] = useState(false);
  const [search, setSearch] = useState('');
  const [criteria, setCriteria] = useState<SearchCriteria>('contains');
  const [showAdvanced, setShowAdvanced] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  const selectedOption = useMemo(() => options.find(opt => opt.value === value), [options, value]);

  const filteredOptions = useMemo(() => {
    if (!search.trim()) return options;

    return options.filter(option => {
      if (option.disabled) return false;

      const labelLower = option.label.toLowerCase();
      const searchLower = search.toLowerCase();

      switch (criteria) {
        case 'contains':
          return labelLower.includes(searchLower);
        case 'startsWith':
          return labelLower.startsWith(searchLower);
        case 'endsWith':
          return labelLower.endsWith(searchLower);
        case 'equals':
          return labelLower === searchLower;
        case 'notEquals':
          return labelLower !== searchLower;
        default:
          return true;
      }
    });
  }, [options, search, criteria]);

  const groupedOptions = useMemo(() => {
    if (!enableGrouping) return null;

    const groups: Record<string, SearchableSelectOption<T>[]> = {};
    filteredOptions.forEach(opt => {
      const group = opt.group || 'Sin grupo';
      if (!groups[group]) groups[group] = [];
      groups[group].push(opt);
    });
    return groups;
  }, [filteredOptions, enableGrouping]);

  useEffect(() => {
    if (isOpen && inputRef.current) {
      inputRef.current.focus();
    }
  }, [isOpen]);

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setIsOpen(false);
        setSearch('');
        setShowAdvanced(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const handleSelect = (optionValue: T) => {
    onChange(optionValue);
    setIsOpen(false);
    setSearch('');
    setShowAdvanced(false);
  };

  const handleClear = (e: React.MouseEvent) => {
    e.stopPropagation();
    onChange(null);
  };

  const toggleAdvanced = () => {
    setShowAdvanced(!showAdvanced);
    if (!showAdvanced) {
      setCriteria('contains');
    }
  };

  const hasError = !!error;
  const heightClass = compact ? 'h-12' : 'h-14';

  const borderClass = isOpen
    ? `border-2 ${hasError ? 'border-m3-error' : 'border-m3-primary'}`
    : hasError
      ? 'border border-m3-error'
      : 'border border-m3-outline hover:border-m3-on-surface';

  const labelColorClass = isOpen
    ? hasError
      ? 'text-m3-error'
      : 'text-m3-primary'
    : hasError
      ? 'text-m3-error'
      : 'text-m3-secondary';

  return (
    <div ref={containerRef} className={`relative w-full ${className}`}>
      <div
        onClick={() => !disabled && setIsOpen(!isOpen)}
        className={[
          `relative w-full ${heightClass} rounded-[4px] cursor-pointer`,
          'bg-m3-surface-container/30 dark:bg-m3-surface-container/20',
          'transition-colors duration-150',
          borderClass,
          disabled ? 'opacity-50 cursor-not-allowed' : '',
        ].join(' ')}
      >
        <fieldset
          className={[
            `absolute inset-0 w-full m-0 p-0 min-w-0 rounded-[4px] pointer-events-none`,
          ].join(' ')}
        >
          <legend
            aria-hidden="true"
            className="ml-3 h-0 overflow-hidden whitespace-nowrap text-xs opacity-0 pointer-events-none select-none"
            style={{ maxWidth: '1000px', padding: '0 4px' }}
          >
            {label}
          </legend>
        </fieldset>

        <div className="flex items-center justify-between h-full px-4">
          <div className="flex items-center gap-2 min-w-0 flex-1">
            {selectedOption ? (
              <div className="flex items-center gap-2 min-w-0 flex-1">
                <span className="text-sm text-m3-on-surface truncate">{selectedOption.label}</span>
                {selectedOption.description && (
                  <span className="text-xs text-m3-secondary truncate hidden sm:inline">
                    - {selectedOption.description}
                  </span>
                )}
              </div>
            ) : (
              <span className="text-sm text-m3-secondary">{placeholder}</span>
            )}
          </div>

          <div className="flex items-center gap-1 flex-shrink-0">
            {selectedOption && !disabled && (
              <button
                type="button"
                onClick={handleClear}
                className="p-1 rounded-full hover:bg-m3-surface-container/50 text-m3-secondary"
              >
                <X className="w-4 h-4" />
              </button>
            )}
            {loading ? (
              <Loader2 className="w-4 h-4 text-m3-secondary animate-spin" />
            ) : (
              <ChevronDown
                className={`w-4 h-4 text-m3-secondary transition-transform ${isOpen ? 'rotate-180' : ''}`}
              />
            )}
          </div>
        </div>
      </div>

      <label
        className={[
          'absolute left-3 px-1 top-0 -translate-y-1/2 pointer-events-none text-xs font-normal',
          'transition-colors duration-150',
          labelColorClass,
        ].join(' ')}
      >
        {label}
        {required && <span className="text-m3-error ml-0.5">*</span>}
      </label>

      {isOpen && !disabled && (
        <div className="absolute z-50 w-full mt-1 bg-m3-surface-container/95 backdrop-blur-sm border border-m3-outline/50 rounded-xl shadow-lg overflow-hidden">
          <div className="p-2 border-b border-m3-outline/25 space-y-2">
            <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-m3-surface-container/30">
              <Search className="w-4 h-4 text-m3-secondary flex-shrink-0" />
              <input
                ref={inputRef}
                type="text"
                value={search}
                onChange={e => setSearch(e.target.value)}
                placeholder={searchPlaceholder}
                className="flex-1 bg-transparent text-sm text-m3-on-surface outline-none placeholder:text-m3-secondary"
              />
            </div>

            {enableAdvancedSearch && (
              <div className="flex items-center gap-2 px-1">
                <button
                  type="button"
                  onClick={toggleAdvanced}
                  className={`text-[10px] font-medium px-2.5 py-0.5 rounded-full transition-colors ${
                    showAdvanced
                      ? 'bg-m3-primary text-m3-on-primary'
                      : 'bg-m3-surface-container/50 text-m3-secondary hover:bg-m3-primary/10'
                  }`}
                >
                  Búsqueda avanzada
                </button>
                {showAdvanced && (
                  <select
                    value={criteria}
                    onChange={e => setCriteria(e.target.value as SearchCriteria)}
                    className="text-xs bg-transparent text-m3-secondary outline-none cursor-pointer"
                  >
                    {(Object.keys(CRITERIA_LABELS) as SearchCriteria[]).map(key => (
                      <option key={key} value={key}>
                        {CRITERIA_LABELS[key]}
                      </option>
                    ))}
                  </select>
                )}
              </div>
            )}
          </div>

          <div className="max-h-64 overflow-y-auto">
            {loading ? (
              <div className="px-4 py-6 text-sm text-m3-secondary text-center flex items-center justify-center gap-2">
                <Loader2 className="w-4 h-4 animate-spin" />
                Cargando...
              </div>
            ) : filteredOptions.length === 0 ? (
              <div className="px-4 py-3 text-sm text-m3-secondary text-center flex items-center justify-center gap-2">
                <AlertCircle className="w-4 h-4" />
                {emptyMessage}
              </div>
            ) : groupedOptions ? (
              Object.entries(groupedOptions).map(([groupName, groupOptions]) => (
                <div key={groupName}>
                  <div className="px-4 py-1.5 text-[10px] font-bold text-m3-secondary uppercase tracking-wider bg-m3-surface-container/30">
                    {groupName}
                  </div>
                  {groupOptions.map(option => (
                    <OptionButton
                      key={String(option.value)}
                      option={option}
                      isSelected={option.value === value}
                      onSelect={handleSelect}
                    />
                  ))}
                </div>
              ))
            ) : (
              filteredOptions.map(option => (
                <OptionButton
                  key={String(option.value)}
                  option={option}
                  isSelected={option.value === value}
                  onSelect={handleSelect}
                />
              ))
            )}
          </div>

          <div className="p-2 border-t border-m3-outline/25 text-[10px] text-m3-secondary text-center">
            {filteredOptions.length} resultado{filteredOptions.length !== 1 ? 's' : ''}
            {search && ` para "${search}"`}
          </div>
        </div>
      )}

      {(error || helperText) && (
        <span
          className={`block text-xs mt-1 ml-4 ${
            hasError ? 'text-m3-error' : 'text-m3-secondary/75'
          }`}
        >
          {error || helperText}
        </span>
      )}
    </div>
  );
}

function OptionButton<T>({
  option,
  isSelected,
  onSelect,
}: {
  option: SearchableSelectOption<T>;
  isSelected: boolean;
  onSelect: (value: T) => void;
}) {
  return (
    <button
      type="button"
      onClick={() => onSelect(option.value)}
      disabled={option.disabled}
      className={[
        'w-full px-4 py-3 flex items-center gap-3 text-left transition-colors',
        isSelected ? 'bg-m3-primary-container/50' : 'hover:bg-m3-surface-container/50',
        option.disabled ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer',
      ].join(' ')}
    >
      <div className="flex items-center gap-2 min-w-0 flex-1">
        <span className="text-sm font-medium text-m3-on-surface block truncate">
          {option.label}
        </span>
        {option.description && (
          <span className="text-xs text-m3-secondary block truncate hidden sm:inline">
            - {option.description}
          </span>
        )}
      </div>
      {isSelected && <Check className="w-4 h-4 text-m3-primary flex-shrink-0" />}
    </button>
  );
}
