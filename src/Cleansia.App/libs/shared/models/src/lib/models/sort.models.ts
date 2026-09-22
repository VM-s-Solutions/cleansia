import { Icon } from '@cleansia/types';

export interface ICleansiaSortDefinition {
  field: string;
  title?: string;
  titleTranslationKey?: string;
  isAscending?: boolean;
  isSelected?: boolean;
  icon?: Icon;
  order?: number;
}

export class BaseSortDefinition implements ICleansiaSortDefinition {
  field: string;
  title?: string;
  titleTranslationKey?: string;
  isAscending?: boolean;
  isSelected?: boolean;
  icon?: Icon;
  order?: number;

  constructor(data: ICleansiaSortDefinition) {
    this.field = data.field;
    this.title = data.title;
    this.titleTranslationKey = data.titleTranslationKey;
    this.isAscending = data.isAscending;
    this.isSelected = data.isSelected ?? false;
    this.icon = data.icon;
    this.order = data.order;
  }
}
