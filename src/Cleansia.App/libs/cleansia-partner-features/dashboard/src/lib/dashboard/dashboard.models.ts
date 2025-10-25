export interface StatCard {
  title: string;
  value: number | string;
  icon: string;
  color: string;
  route?: string;
  trend?: {
    value: number;
    direction: 'up' | 'down' | 'neutral';
  };
}
