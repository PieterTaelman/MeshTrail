import { definePreset } from '@openng/optimus-ui-themes';
import Aura from '@openng/optimus-ui-themes/aura';

// Near-black neutral with a slight cool tint (see Moodboard/): the dark scheme uses the high numbers.
const graphite = {
  0: '#ffffff',
  50: '#f4f5f5',
  100: '#e4e6e7',
  200: '#c9cdcf',
  300: '#a3a9ac',
  400: '#7b8285',
  500: '#5a6064',
  600: '#41464a',
  700: '#2e3236',
  800: '#222528',
  900: '#191b1d',
  950: '#111213',
};

/** Aura with a teal accent and graphite surfaces. Every component picks this up through design tokens. */
export const MeshtrailPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '{teal.50}',
      100: '{teal.100}',
      200: '{teal.200}',
      300: '{teal.300}',
      400: '{teal.400}',
      500: '{teal.500}',
      600: '{teal.600}',
      700: '{teal.700}',
      800: '{teal.800}',
      900: '{teal.900}',
      950: '{teal.950}',
    },
    colorScheme: {
      light: {
        surface: graphite,
      },
      dark: {
        surface: graphite,
        // Softer panel borders than Aura's default, closer to the moodboard.
        content: { borderColor: '{surface.800}' },
        formField: { borderColor: '{surface.700}', background: '{surface.950}' },
      },
    },
  },
});
