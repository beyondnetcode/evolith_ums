import React, { useState, useRef, useEffect } from 'react';
import { Search, ChevronDown, Check } from 'lucide-react';
import { Tenant } from '@domain/identity/models/tenant.model';

export interface TenantSelectorProps {
  tenants: Tenant[];
  selectedTenantId: string;
  onTenantChange: (tenantId: string) => void;
  label?: string;
  className?: string;
}

export const TenantSelector: React.FC<TenantSelectorProps> = ({
  tenants,
  selectedTenantId,
  onTenantChange,
  label,
  className,
}) => {
  const [isOpen, setIsOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const containerRef = useRef<HTMLDivElement>(null);

  const selectedTenant = tenants.find(t => t.tenantId === selectedTenantId);

  // Close dropdown on click outside
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const filteredTenants = tenants.filter(
    tenant =>
      tenant.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
      tenant.code.toLowerCase().includes(searchQuery.toLowerCase())
  );

  return (
    <div ref={containerRef} className={`relative select-none ${className || ''}`}>
      {label && (
        <span className="block text-[11px] font-semibold text-m3-secondary uppercase tracking-wider mb-1">
          {label}
        </span>
      )}

      {/* Selector Trigger Button */}
      <button
        type="button"
        onClick={() => setIsOpen(!isOpen)}
        className="w-full flex items-center justify-between px-3 py-2 rounded-lg border border-m3-outline/30 bg-m3-surface hover:bg-m3-primary/5 transition-all text-sm text-m3-on-surface focus:outline-none focus:ring-2 focus:ring-m3-primary focus:border-m3-primary text-left cursor-pointer"
      >
        <span className="font-medium truncate">
          {selectedTenant ? `${selectedTenant.name} (${selectedTenant.code})` : 'Select Tenant'}
        </span>
        <ChevronDown
          className={`w-4 h-4 text-m3-secondary transition-transform duration-200 ${isOpen ? 'rotate-180' : ''}`}
        />
      </button>

      {/* Dropdown Menu */}
      {isOpen && (
        <div className="absolute right-0 top-full mt-1.5 w-full min-w-[280px] bg-m3-surface border border-m3-outline/25 rounded-xl shadow-lg z-50 p-2 overflow-hidden flex flex-col max-h-[300px]">
          {/* Search Box */}
          <div className="relative mb-2 flex-shrink-0">
            <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-m3-secondary" />
            <input
              type="text"
              placeholder="Search tenants..."
              value={searchQuery}
              onChange={e => setSearchQuery(e.target.value)}
              className="w-full pl-8 pr-3 py-1.5 bg-m3-surface-container/40 hover:bg-m3-surface-container/70 border border-m3-outline/20 rounded-lg text-xs text-m3-on-surface focus:outline-none focus:border-m3-primary transition-all"
              onClick={e => e.stopPropagation()}
            />
          </div>

          {/* Tenants List */}
          <div className="flex-1 overflow-y-auto space-y-0.5 custom-scrollbar max-h-[220px]">
            {filteredTenants.length === 0 ? (
              <div className="text-center py-4 text-xs text-m3-secondary">No tenants found</div>
            ) : (
              filteredTenants.map(tenant => {
                const isSelected = tenant.tenantId === selectedTenantId;
                return (
                  <button
                    key={tenant.tenantId}
                    type="button"
                    onClick={() => {
                      onTenantChange(tenant.tenantId);
                      setIsOpen(false);
                      setSearchQuery('');
                    }}
                    className={`w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs transition-colors text-left cursor-pointer ${
                      isSelected
                        ? 'bg-m3-primary/10 text-m3-primary font-semibold'
                        : 'text-m3-on-surface hover:bg-m3-surface-container/50'
                    }`}
                  >
                    <span className="truncate">
                      {tenant.name} <span className="opacity-60">({tenant.code})</span>
                    </span>
                    {isSelected && <Check className="w-3.5 h-3.5 flex-shrink-0" />}
                  </button>
                );
              })
            )}
          </div>
        </div>
      )}
    </div>
  );
};
