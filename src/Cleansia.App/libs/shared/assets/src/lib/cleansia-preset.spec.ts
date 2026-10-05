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

interface Semantic {
  primary?: Record<string, string>;
  colorScheme?: {
    light?: {
      primary?: { color?: string; contrastColor?: string; hoverColor?: string; activeColor?: string };
      formField?: { floatLabelFocusColor?: string };
    };
    dark?: { primary?: { color?: string; contrastColor?: string } };
  };
}

interface TabsTokens {
  tab?: { activeColor?: string; activeBorderColor?: string };
  activeBar?: { background?: string };
  colorScheme?: { light?: { tab?: { activeColor?: string } }; dark?: { tab?: unknown } };
}

const semanticOf = (preset: unknown): Semantic | undefined => (preset as { semantic?: Semantic }).semantic;
const tabsOf = (preset: unknown): TabsTokens | undefined =>
  (preset as { components?: { tabs?: TabsTokens } }).components?.tabs;

/** WCAG contrast of a `{primary.N}` reference from the preset's own ramp against white. */
const onWhite = (ref: string | undefined): number => {
  const step = /^\{primary\.(\d+)\}$/.exec(ref ?? '')?.[1];
  const hex = step ? semanticOf(CleansiaPreset)?.primary?.[step] : undefined;
  if (!hex) throw new Error(`not a primary step: ${ref}`);
  const lum = [1, 3, 5]
    .map((i) => parseInt(hex.slice(i, i + 2), 16) / 255)
    .map((c) => (c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4));
  const l = 0.2126 * lum[0] + 0.7152 * lum[1] + 0.0722 * lum[2];
  return 1.05 / (l + 0.05);
};

// The light theme's primary is the brand blue, Sky600 (Aura's default, Sky500, put white at 2.77).
// Blue TEXT never takes the primary: a text, outlined or link button, a focused field's floating
// label and the open tab's label take the text ink, Sky700 (5.9). All three web apps read this preset.
describe('CleansiaPreset — the light primary', () => {
  const light = semanticOf(CleansiaPreset)?.colorScheme?.light;

  it('fills with Sky600, a step darker under the pointer and another pressed, white on it', () => {
    expect(light?.primary).toEqual({
      color: '{primary.600}',
      contrastColor: '#ffffff',
      hoverColor: '{primary.700}',
      activeColor: '{primary.800}',
    });
  });

  it("inks a focused field's floating label with Sky700", () => {
    expect(light?.formField?.floatLabelFocusColor).toBe('{primary.700}');
  });

  it('keeps the dark theme the customer site draws', () => {
    expect(semanticOf(CleansiaPreset)?.colorScheme?.dark?.primary).toEqual({
      color: '#38bdf8',
      contrastColor: '#0c4a6e',
      hoverColor: '#7dd3fc',
      activeColor: '#bae6fd',
    });
  });
});

describe('CleansiaPreset — the open tab', () => {
  const tabs = tabsOf(CleansiaPreset);
  const aura = tabsOf(Aura);

  it('inks the open tab label with Sky700 in the light theme', () => {
    expect(tabs?.colorScheme?.light?.tab?.activeColor).toBe('{primary.700}');
  });

  it('keeps the underline on the primary and the dark theme as Aura draws it', () => {
    expect(aura?.tab?.activeBorderColor).toBe('{primary.color}');
    expect(aura?.activeBar?.background).toBe('{primary.color}');
    expect(tabs?.colorScheme?.dark).toEqual(aura?.colorScheme?.dark);
  });
});

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

  it('leaves the filled button on the primary and the dark theme as Aura draws them', () => {
    expect(light?.root).toEqual(aura?.colorScheme?.light?.root);
    expect(button?.colorScheme?.dark).toEqual(aura?.colorScheme?.dark);
  });
});

describe('CleansiaPreset — every light blue ink reads on white', () => {
  const semantic = semanticOf(CleansiaPreset)?.colorScheme?.light;
  const button = (CleansiaPreset as { components?: { button?: ButtonInks } }).components?.button
    ?.colorScheme?.light;

  it.each([
    ['a focused floating label', () => semantic?.formField?.floatLabelFocusColor],
    ['the open tab label', () => tabsOf(CleansiaPreset)?.colorScheme?.light?.tab?.activeColor],
    ['a text button', () => button?.text?.primary?.color],
    ['an outlined button', () => button?.outlined?.primary?.color],
    ['a link button', () => button?.link?.color],
  ])('%s at 4.5 or better', (_, ink) => {
    expect(onWhite(ink())).toBeGreaterThanOrEqual(4.5);
  });

  // A fill carries white text; the owner chose the brand blue for it (2026-10-05), 4.1 on white,
  // where Aura's Sky500 read 2.77. Its hover and pressed steps clear 4.5.
  it('puts white on the fill at 4.1, and at 4.5 or better under the pointer', () => {
    expect(onWhite(semantic?.primary?.color)).toBeCloseTo(4.1, 1);
    expect(onWhite(semantic?.primary?.hoverColor)).toBeGreaterThanOrEqual(4.5);
    expect(onWhite(semantic?.primary?.activeColor)).toBeGreaterThanOrEqual(4.5);
  });
});
