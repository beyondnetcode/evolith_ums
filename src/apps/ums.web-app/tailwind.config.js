/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        // EVOLITH corporate navy ramp (anchored on brand #0f3e67 / footer #042139)
        brand: {
          50: '#f0f5fa',
          100: '#dbe7f2',
          200: '#b8cfe4',
          300: '#8db0d1',
          400: '#5d8bb8',
          500: '#3a6a9c',
          600: '#1f5080',
          700: '#0f3e67',
          800: '#0c3253',
          900: '#0a2843',
          950: '#042139',
        },
        // EVOLITH green accent (#41a62a) as a standalone Tailwind color
        accent: {
          50: '#eff8ec',
          100: '#d6eecd',
          200: '#b0dfa0',
          300: '#83cc6d',
          400: '#5cb844',
          500: '#41a62a',
          600: '#33851f',
          700: '#29661b',
          800: '#22511a',
          900: '#1d4318',
          950: '#0c2609',
        },
        dark: {
          50: '#f8fafc',
          100: '#f1f5f9',
          900: '#0f172a',
          950: '#020617',
        },
        /**
         * Full MD3 colour role set.
         * Each token uses hsl(var(--token)) so Tailwind opacity modifiers
         * such as bg-m3-primary/10 work correctly.
         */
        m3: {
          // Primary
          primary: 'hsl(var(--m3-primary))',
          'on-primary': 'hsl(var(--m3-on-primary))',
          'primary-container': 'hsl(var(--m3-primary-container))',
          'on-primary-container': 'hsl(var(--m3-on-primary-container))',
          // Secondary
          secondary: 'hsl(var(--m3-secondary))',
          'on-secondary': 'hsl(var(--m3-on-secondary))',
          'secondary-container': 'hsl(var(--m3-secondary-container))',
          'on-secondary-container': 'hsl(var(--m3-on-secondary-container))',
          // Tertiary
          tertiary: 'hsl(var(--m3-tertiary))',
          'on-tertiary': 'hsl(var(--m3-on-tertiary))',
          'tertiary-container': 'hsl(var(--m3-tertiary-container))',
          'on-tertiary-container': 'hsl(var(--m3-on-tertiary-container))',
          // Error
          // Roles que el generador del nucleo declara y la aplicacion no mapeaba: `success` y
          // `warning` son de primera clase en ADR-0228 §2.6, y la familia de superficie completa la
          // exige P-UX-06. Sin mapearlos, quedaban tokens que nadie podia usar.
          success: 'hsl(var(--m3-success))',
          'on-success': 'hsl(var(--m3-on-success))',
          'success-container': 'hsl(var(--m3-success-container))',
          'on-success-container': 'hsl(var(--m3-on-success-container))',

          warning: 'hsl(var(--m3-warning))',
          'on-warning': 'hsl(var(--m3-on-warning))',
          'warning-container': 'hsl(var(--m3-warning-container))',
          'on-warning-container': 'hsl(var(--m3-on-warning-container))',

          scrim: 'hsl(var(--m3-scrim))',
          shadow: 'hsl(var(--m3-shadow))',

          error: 'hsl(var(--m3-error))',
          'on-error': 'hsl(var(--m3-on-error))',
          'error-container': 'hsl(var(--m3-error-container))',
          'on-error-container': 'hsl(var(--m3-on-error-container))',
          // Surface
          surface: 'hsl(var(--m3-surface))',
          'surface-container-lowest': 'hsl(var(--m3-surface-container-lowest))',
          'surface-container-highest': 'hsl(var(--m3-surface-container-highest))',
          'on-surface': 'hsl(var(--m3-on-surface))',
          'surface-variant': 'hsl(var(--m3-surface-variant))',
          'on-surface-variant': 'hsl(var(--m3-on-surface-variant))',
          'surface-container': 'hsl(var(--m3-surface-container))',
          'surface-container-low': 'hsl(var(--m3-surface-container-low))',
          'surface-container-high': 'hsl(var(--m3-surface-container-high))',
          // Outline
          outline: 'hsl(var(--m3-outline))',
          'outline-variant': 'hsl(var(--m3-outline-variant))',
          // Inverse
          'inverse-surface': 'hsl(var(--m3-inverse-surface))',
          'inverse-on-surface': 'hsl(var(--m3-inverse-on-surface))',
          'inverse-primary': 'hsl(var(--m3-inverse-primary))',
        },
      },
      // M3 fija el estado deshabilitado en 38 % y el boton lo pedia asi, pero `opacity-38` NO existe
      // en la escala de Tailwind —va de cinco en cinco—, de modo que la clase no generaba NADA: un
      // boton deshabilitado se veia identico a uno habilitado en toda la aplicacion. Se declara el
      // valor en vez de redondearlo, que seria cambiar el sistema por culpa de una errata.
      opacity: {
        38: '0.38',
      },
      fontFamily: {
        // La pila sale del token, no de una lista escrita aqui: la marca es Montserrat
        // (ADR-0228 §2.7) y su respaldo esta declarado para que el primer render no salte de
        // metrica. Duplicarla en esta configuracion la desincronizaria del generador.
        sans: ['var(--md-sys-typescale-font)'],
      },
      keyframes: {
        tooltipIn: {
          '0%': { opacity: '0', transform: 'scale(0.92) translateY(3px)' },
          '100%': { opacity: '1', transform: 'scale(1) translateY(0)' },
        },
        fadeIn: {
          '0%': { opacity: '0' },
          '100%': { opacity: '1' },
        },
        slideDown: {
          '0%': { opacity: '0', transform: 'translateY(-6px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
      },
      animation: {
        tooltipIn: 'tooltipIn 0.12s ease-out forwards',
        fadeIn: 'fadeIn 0.18s ease-out forwards',
        slideDown: 'slideDown 0.18s ease-out forwards',
      },
    },
  },
  plugins: [],
};
