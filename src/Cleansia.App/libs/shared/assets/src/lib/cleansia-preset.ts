import { definePreset } from '@primeng/themes';
import Aura from '@primeng/themes/aura';

export const CleansiaPreset = definePreset(Aura, {
  // No `primitive` block: PrimeNG 20 dropped fontFamily from the Primitive token type, and it
  // was never doing any work here — all three apps set `font-family: 'Nunito', sans-serif`
  // in their own index.html, which is what the pages actually render with.
  semantic: {
    primary: {
      0: '#ffffff',
      50: '#f0f9ff',
      100: '#e0f2fe',
      200: '#bae6fd',
      300: '#7dd3fc',
      400: '#38bdf8',
      500: '#0ea5e9',
      600: '#0284c7',
      700: '#0369a1',
      800: '#075985',
      900: '#0c4a6e',
      950: '#082f49',
    },
    colorScheme: {
      dark: {
        surface: {
          0: '#ffffff',
          50: '#f8fafc',
          100: '#f1f5f9',
          200: '#e2e8f0',
          300: '#cbd5e1',
          400: '#94a3b8',
          500: '#64748b',
          600: '#475569',
          700: '#334155',
          800: '#1e293b',
          900: '#0f172a',
          950: '#020617',
        },
        primary: {
          color: '#38bdf8',
          contrastColor: '#0c4a6e',
          hoverColor: '#7dd3fc',
          activeColor: '#bae6fd',
        },
      },
    },
  },
  components: {
    button: {
      colorScheme: {
        // A text, outlined or link button is blue TEXT on a light ground, so it takes the text ink,
        // Sky700 (5.9 on white), not the light theme's primary, Sky500 (2.8). Under the pointer it
        // goes a step darker, Sky800: the link's own token here, the other two in
        // cleansia-button.component.scss. Filled buttons keep the primary; dark keeps its light blue.
        light: {
          text: { primary: { color: '{primary.700}' } },
          outlined: { primary: { color: '{primary.700}' } },
          link: { color: '{primary.700}', hoverColor: '{primary.800}', activeColor: '{primary.800}' },
        },
      },
    },
  },
});
