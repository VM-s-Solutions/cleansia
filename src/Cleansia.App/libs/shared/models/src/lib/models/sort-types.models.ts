export interface ISortDefinition {
  field: string | undefined;
  direction: SortDirection;
}

export class SortDefinition implements ISortDefinition {
  field: string | undefined;
  direction: SortDirection;

  constructor(data?: ISortDefinition) {
    this.field = data?.field;
    this.direction = data?.direction ?? SortDirection.Ascending;
  }
}

export enum SortDirection {
  Ascending = 0,
  Descending = 1,
}
