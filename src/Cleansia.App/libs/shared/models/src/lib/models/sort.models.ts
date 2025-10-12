import { Icon } from '@cleansia/types';
import { SortDefinition, SortDirection } from './sort-types.models';

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

  static create(data: ICleansiaSortDefinition): BaseSortDefinition {
    return new BaseSortDefinition(data);
  }

  static init(): BaseSortDefinition[] {
    return [];
  }

  toggle() {
    return BaseSortDefinition.create({
      field: this.field,
      title: this.title,
      titleTranslationKey: this.titleTranslationKey,
      isAscending: !this.isAscending,
      isSelected: this.isSelected,
    });
  }

  select() {
    return BaseSortDefinition.create({
      field: this.field,
      title: this.title,
      titleTranslationKey: this.titleTranslationKey,
      isAscending: this.isAscending,
      isSelected: true,
    });
  }

  deselect(newIcon?: Icon) {
    return BaseSortDefinition.create({
      field: this.field,
      title: this.title,
      titleTranslationKey: this.titleTranslationKey,
      isAscending: undefined,
      isSelected: false,
      icon: newIcon,
    });
  }

  setIcon(icon: Icon) {
    return BaseSortDefinition.create({
      field: this.field,
      title: this.title,
      titleTranslationKey: this.titleTranslationKey,
      isAscending: this.isAscending,
      isSelected: this.isSelected,
      icon,
    });
  }

  update(data: ICleansiaSortDefinition) {
    return BaseSortDefinition.create({
      field: data.field,
      title: data.title,
      titleTranslationKey: data.titleTranslationKey,
      isAscending: data.isAscending,
      isSelected: data.isSelected,
      icon: data.icon,
    });
  }

  updateIsAscendingIsSelectedIconOrder(
    isAscending: boolean,
    isSelected: boolean,
    icon: Icon,
    order?: number
  ) {
    return BaseSortDefinition.create({
      field: this.field,
      title: this.title,
      titleTranslationKey: this.titleTranslationKey,
      isAscending,
      isSelected,
      icon,
      order,
    });
  }

  updateOrder(order: number) {
    return BaseSortDefinition.create({
      field: this.field,
      title: this.title,
      titleTranslationKey: this.titleTranslationKey,
      isAscending: this.isAscending,
      isSelected: this.isSelected,
      icon: this.icon,
      order,
    });
  }
}

export class MaterialSortDefinition extends BaseSortDefinition {
  constructor(data: ICleansiaSortDefinition) {
    super(data);
  }

  static override init(): MaterialSortDefinition[] {
    return [
      BaseSortDefinition.create({
        field: 'name',
        titleTranslationKey: 'global.sort.material.name',
      }),
      BaseSortDefinition.create({
        field: 'price',
        titleTranslationKey: 'global.sort.material.price',
      }),
      BaseSortDefinition.create({
        field: 'category',
        titleTranslationKey: 'global.sort.material.category',
      }),
    ];
  }
}

export class CategorySortDefinition extends BaseSortDefinition {
  constructor(data: ICleansiaSortDefinition) {
    super(data);
  }

  static override init(): CategorySortDefinition[] {
    return [
      BaseSortDefinition.create({
        field: 'name',
        titleTranslationKey: 'global.sort.category.name',
      }),
    ];
  }
}

export class UnitSortDefinition extends BaseSortDefinition {
  constructor(data: ICleansiaSortDefinition) {
    super(data);
  }

  static override init(): UnitSortDefinition[] {
    return [
      BaseSortDefinition.create({
        field: 'name',
        titleTranslationKey: 'global.sort.unit.name',
      }),
      BaseSortDefinition.create({
        field: 'shortName',
        titleTranslationKey: 'global.sort.unit.short_name',
      }),
    ];
  }
}

export class SupplierSortDefinition extends BaseSortDefinition {
  constructor(data: ICleansiaSortDefinition) {
    super(data);
  }

  static override init(): SupplierSortDefinition[] {
    return [
      BaseSortDefinition.create({
        field: 'name',
        titleTranslationKey: 'global.sort.supplier.name',
      }),
      BaseSortDefinition.create({
        field: 'description',
        titleTranslationKey: 'global.sort.supplier.description',
      }),
      BaseSortDefinition.create({
        field: 'address',
        titleTranslationKey: 'global.sort.supplier.address',
      }),
      BaseSortDefinition.create({
        field: 'city',
        titleTranslationKey: 'global.sort.supplier.city',
      }),
      BaseSortDefinition.create({
        field: 'zipCode',
        titleTranslationKey: 'global.sort.supplier.zip_code',
      }),
      BaseSortDefinition.create({
        field: 'phoneNumber',
        titleTranslationKey: 'global.sort.supplier.phone_number',
      }),
      BaseSortDefinition.create({
        field: 'email',
        titleTranslationKey: 'global.sort.supplier.email',
      }),
      BaseSortDefinition.create({
        field: 'contactPerson',
        titleTranslationKey: 'global.sort.supplier.contact_person',
      }),
      BaseSortDefinition.create({
        field: 'website',
        titleTranslationKey: 'global.sort.supplier.website',
      }),
      BaseSortDefinition.create({
        field: 'taxId',
        titleTranslationKey: 'global.sort.supplier.tax_id',
      }),
      BaseSortDefinition.create({
        field: 'isActive',
        titleTranslationKey: 'global.sort.supplier.is_active',
      }),
    ];
  }
}

export function transformToApiSortDefinition(
  sortDefinition: BaseSortDefinition[]
): SortDefinition[] {
  return sortDefinition
    .filter((s) => s.isSelected)
    .sort((a, b) => (a.order ?? 0) - (b.order ?? 0))
    .map(
      (s) =>
        new SortDefinition({
          direction: s.isAscending
            ? SortDirection.Ascending
            : SortDirection.Descending,
          field: s.field,
        })
    );
}
