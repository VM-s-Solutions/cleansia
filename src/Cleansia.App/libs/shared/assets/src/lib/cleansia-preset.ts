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
      // The light primary is the brand blue, Sky600, not Aura's Sky500 (white on it read 2.77): a
      // filled button, a checkbox, a selected date, a focus border. Under the pointer and pressed it
      // goes a step darker each time. The customer site already painted its filled buttons Sky600
      // and Sky700 on hover in its own stylesheet; the partner and admin sites only had this.
      // A focused field's floating label is blue TEXT, so it takes the text ink, Sky700 (5.9 on
      // white), as the tab label and the text buttons below do.
      light: {
        primary: {
          color: '{primary.600}',
          contrastColor: '#ffffff',
          hoverColor: '{primary.700}',
          activeColor: '{primary.800}',
        },
        formField: {
          floatLabelFocusColor: '{primary.700}',
        },
      },
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
        // Sky700 (5.9 on white), not the light theme's primary, Sky600 (4.1). Under the pointer it
        // goes a step darker, Sky800: the link's own token here, the other two in
        // cleansia-button.component.scss. Filled buttons keep the primary; dark keeps its light blue.
        light: {
          text: { primary: { color: '{primary.700}' } },
          outlined: { primary: { color: '{primary.700}' } },
          link: { color: '{primary.700}', hoverColor: '{primary.800}', activeColor: '{primary.800}' },
        },
      },
    },
    // The open tab's label is blue text too (the admin's tab strips); its underline keeps the primary.
    // The dark entry is not optional: primeuix writes the light scheme into `:root`, and Aura's dark
    // tabs scheme sets no label ink, so without it Sky700 would carry into the customer site's dark
    // theme (about 3:1 on its dark surface). It restores Aura's own dark ink, the primary.
    tabs: {
      colorScheme: {
        light: {
          tab: { activeColor: '{primary.700}' },
        },
        dark: {
          tab: { activeColor: '{primary.color}' },
        },
      },
    },
  },
});
