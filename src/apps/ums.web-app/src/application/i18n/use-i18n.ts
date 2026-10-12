import { useI18nStore } from '../stores/i18n.store';
import translations from './translations';

export const useI18n = () => {
  const lang = useI18nStore(state => state.language);
  return translations[lang];
};

export { useI18nStore } from '../stores/i18n.store';
export type { SupportedLanguage } from '../stores/i18n.store';
