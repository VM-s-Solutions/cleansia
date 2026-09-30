import { Code, DirtinessLevel } from '@cleansia/partner-services';

export interface StatCard {
  title: string;
  value: number | string;
  icon: string;
  route?: string;
  trend?: {
    value: number;
    direction: 'up' | 'down' | 'neutral';
  };
}

/** An upcoming order as the dashboard card prints it: dates and money already in the session language. */
export interface UpcomingOrderCard {
  id: string;
  displayOrderNumber: string;
  orderStatus: Code;
  customerName: string;
  cleaningDate: string;
  customerAddress: string;
  dirtinessLevelKey: string;
  yourPay: string;
}

const DIRTINESS_LEVEL_LABEL_KEYS: Readonly<Record<DirtinessLevel, string>> = {
  [DirtinessLevel.Normal]: 'enums.dirtiness_level.normal',
  [DirtinessLevel.Increased]: 'enums.dirtiness_level.increased',
  [DirtinessLevel.Heavy]: 'enums.dirtiness_level.heavy',
};

export function dirtinessLevelLabelKey(level: DirtinessLevel | undefined): string {
  return DIRTINESS_LEVEL_LABEL_KEYS[level ?? DirtinessLevel.Normal];
}
