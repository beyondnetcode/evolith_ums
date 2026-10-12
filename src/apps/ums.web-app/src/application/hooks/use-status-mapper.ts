/**
 * useStatusMapper.ts — Generic status-to-label/color mapper.
 *
 * Accepts a configuration object mapping status values to i18n keys
 * and CSS color classes. Returns a function that resolves status
 * strings to display labels and style classes.
 *
 * Usage:
 *   const statusMapper = useStatusMapper({
 *     Active: { labelKey: 'active', colors: { bg: 'bg-emerald-500/10', ... } },
 *     Suspended: { labelKey: 'suspended', colors: { bg: 'bg-rose-500/10', ... } },
 *   });
 *   const { label, colors } = statusMapper('Active');
 */
import { useI18n } from '@app/i18n/use-i18n';

export interface StatusColorSet {
  bg: string;
  border: string;
  text: string;
}

export interface StatusConfig {
  labelKey: string;
  colors: StatusColorSet;
}

export type StatusMapperConfig = Record<string, StatusConfig>;

export function useStatusMapper(config: StatusMapperConfig) {
  const t = useI18n();

  return (status: string): { label: string; colors: StatusColorSet } => {
    const entry = config[status];
    if (!entry) {
      return {
        label: status,
        colors: {
          bg: 'bg-m3-outline/10',
          border: 'border-m3-outline/20',
          text: 'text-m3-secondary',
        },
      };
    }
    return {
      label: (t[entry.labelKey as keyof typeof t] as string) ?? status,
      colors: entry.colors,
    };
  };
}
