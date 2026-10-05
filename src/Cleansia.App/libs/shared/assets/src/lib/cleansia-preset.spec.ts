import Aura from '@primeng/themes/aura';
import { CleansiaPreset } from './cleansia-preset';

interface ButtonInks {
  colorScheme?: {
    light?: {
      root?: unknown;
      text?: { primary?: { color?: string } };
      outlined?: { primary?: { color?: string } };
      link?: { color?: string; hoverColor?: string; activeColor?: string };
    };
    dark?: unknown;
  };
}

// The light theme's primary is Sky500, 2.8 on white. A text, outlined or link button draws its label
// in that colour unless the preset says otherwise, so the three take Sky700 (5.9) and the link goes
// to Sky800 under the pointer. All three web apps read this preset.
describe('CleansiaPreset — blue text on a button', () => {
  const buttonOf = (preset: unknown): ButtonInks | undefined =>
    (preset as { components?: { button?: ButtonInks } }).components?.button;
  const button = buttonOf(CleansiaPreset);
  const aura = buttonOf(Aura);
  const light = button?.colorScheme?.light;

  it('inks text, outlined and link buttons with Sky700', () => {
    expect(light?.text?.primary?.color).toBe('{primary.700}');
    expect(light?.outlined?.primary?.color).toBe('{primary.700}');
    expect(light?.link?.color).toBe('{primary.700}');
  });

  it('takes the link a step darker, Sky800, under the pointer and pressed', () => {
    expect(light?.link?.hoverColor).toBe('{primary.800}');
    expect(light?.link?.activeColor).toBe('{primary.800}');
  });

  it('leaves the filled button and the dark theme as Aura draws them', () => {
    expect(light?.root).toEqual(aura?.colorScheme?.light?.root);
    expect(button?.colorScheme?.dark).toEqual(aura?.colorScheme?.dark);
  });
});
