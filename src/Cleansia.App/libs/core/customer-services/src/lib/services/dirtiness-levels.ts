import { HEAVY_DIRTINESS_SURCHARGE_RATE, INCREASED_DIRTINESS_SURCHARGE_RATE } from '@cleansia/models';
import { DirtinessLevel } from '../client/customer-client';

export interface DirtinessLevelOption {
  level: DirtinessLevel;
  /** The segment under `pages.order.dirtiness.` that holds this level's copy. */
  key: 'normal' | 'increased' | 'heavy';
  ratePercent: number;
}

/**
 * The three levels a customer picks from, mildest first, shared by the booking wizard and the home
 * calculator so the two cannot offer them differently.
 */
export const DIRTINESS_LEVELS: readonly DirtinessLevelOption[] = [
  { level: DirtinessLevel.Normal, key: 'normal', ratePercent: 0 },
  {
    level: DirtinessLevel.Increased,
    key: 'increased',
    ratePercent: Math.round(INCREASED_DIRTINESS_SURCHARGE_RATE * 100),
  },
  {
    level: DirtinessLevel.Heavy,
    key: 'heavy',
    ratePercent: Math.round(HEAVY_DIRTINESS_SURCHARGE_RATE * 100),
  },
];

export function dirtinessLevelOption(level: DirtinessLevel | null | undefined): DirtinessLevelOption | null {
  return DIRTINESS_LEVELS.find((option) => option.level === level) ?? null;
}
