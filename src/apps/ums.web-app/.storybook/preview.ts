import type { Preview } from '@storybook/react';
import '../src/application/i18n/translations';
import '../src/presentation/shared/theme/globals.css';

const preview: Preview = {
  parameters: {
    controls: {
      matchers: {
        color: /(background|color)$/i,
        date: /Date$/i,
      },
    },
    backgrounds: {
      default: 'light',
      values: [
        { name: 'light', value: '#FAFAFA' },
        { name: 'dark', value: '#121212' },
      ],
    },
    docs: {
      toc: true,
    },
  },
  globalTypes: {
    locale: {
      name: 'Locale',
      description: 'Application locale',
      defaultValue: 'en',
      toolbar: {
        icon: 'globe',
        items: [
          { value: 'en', title: 'English' },
          { value: 'es', title: 'Spanish' },
        ],
      },
    },
  },
};

export default preview;
