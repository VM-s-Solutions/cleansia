import { TemplateRef } from '@angular/core';

export type StroytorgFilterType =
  | 'boolean'
  | 'select'
  | 'range'
  | 'text'
  | 'date'
  | 'multiselect'
  | 'checkbox-select';

export interface IStroytorgFilterOption<T = unknown> {
  label: string;
  value: T;
}

export class StroytorgFilterOption<T = unknown>
  implements IStroytorgFilterOption<T>
{
  label: string;
  value: T;

  constructor(data: IStroytorgFilterOption<T>) {
    this.label = data.label;
    this.value = data.value;
  }
}

export interface FilterDefinition<T = unknown> {
  type: StroytorgFilterType;
  name: string;
  title: string;
  description?: string;
  initialValue?: T;
  options?: StroytorgFilterOption[];
  rangeMinValue?: number;
  rangeMaxValue?: number;
  rangeStep?: number;
  twoSidesRange?: boolean;
  template?: TemplateRef<unknown>;
}

export interface IBaseFilter {
  isFilterChanged?: boolean;
}

export class BaseFilter implements IBaseFilter {
  isFilterChanged?: boolean;

  constructor(data?: IBaseFilter) {
    this.isFilterChanged = data?.isFilterChanged ?? false;
  }

  setFilterChanged(): void {
    this.isFilterChanged = true;
  }

  setFilterNotChanged(): void {
    this.isFilterChanged = false;
  }

  resetFilter(): void {
    const self = this as unknown as Record<string, unknown>;
    Object.keys(this).forEach((key) => {
      if (key !== 'isFilterChanged') {
        self[key] = undefined;
      }
    });
  }

  equals(other: BaseFilter): boolean {
    return JSON.stringify(this) === JSON.stringify(other);
  }

  deepCopy(): BaseFilter {
    return Object.assign({}, this);
  }
}

export interface ICategoryFilter {
  id?: string;
  name?: string;
}

export class CategoryFilter extends BaseFilter implements ICategoryFilter {
  id?: string;
  name?: string;

  constructor(filter: ICategoryFilter) {
    super();
    this.id = filter.id;
    this.name = filter.name;
  }
}

export interface IMaterialFilter {
  id?: string;
  name?: string;
  article?: string;
  searchTerm?: string;
  units?: string[];
  categories?: string[];
  minPrice?: number;
  maxPrice?: number;
  minStockAmount?: number;
  maxStockAmount?: number;
  exceptIds?: string[];
  isActive?: boolean;
}

export class MaterialFilter extends BaseFilter implements IMaterialFilter {
  id?: string;
  name?: string;
  article?: string;
  searchTerm?: string;
  units?: string[];
  categories?: string[];
  minPrice?: number;
  maxPrice?: number;
  minStockAmount?: number;
  maxStockAmount?: number;
  exceptIds?: string[];
  isActive?: boolean;

  constructor(filter: IMaterialFilter) {
    super();
    this.id = filter.id;
    this.name = filter.name;
    this.article = filter.article;
    this.searchTerm = filter.searchTerm;
    this.units = filter.units;
    this.categories = filter.categories;
    this.minPrice = filter.minPrice;
    this.maxPrice = filter.maxPrice;
    this.minStockAmount = filter.minStockAmount;
    this.maxStockAmount = filter.maxStockAmount;
    this.exceptIds = filter.exceptIds;
    this.isActive = filter.isActive;
  }
}

export interface IUserFilter {
  id?: string;
  firstName?: string;
  lastName?: string;
  email?: string;
  phoneNumber?: string;
  userProfiles?: number[];
  authenticationTypes?: number[];
  isActive?: boolean;
}

export class UserFilter extends BaseFilter implements IUserFilter {
  id?: string;
  firstName?: string;
  lastName?: string;
  email?: string;
  phoneNumber?: string;
  userProfiles?: number[];
  authenticationTypes?: number[];
  isActive?: boolean;

  constructor(filter: IUserFilter) {
    super();
    this.id = filter.id;
    this.firstName = filter.firstName;
    this.lastName = filter.lastName;
    this.email = filter.email;
    this.phoneNumber = filter.phoneNumber;
    this.userProfiles = filter.userProfiles;
    this.authenticationTypes = filter.authenticationTypes;
    this.isActive = filter.isActive;
  }
}

export interface IOrderFilter {
  id?: string;
  customerName?: string;
  customerEmail?: string;
  customerPhone?: string;
  displayOrderNumber?: string;
  employeeId?: string;
  cleaningDateFrom?: Date;
  cleaningDateTo?: Date;
  paymentStatuses?: number[];
  orderStatuses?: number[];
  paymentTypes?: number[];
  minTotalPrice?: number;
  maxTotalPrice?: number;
  hasAvailableSpots?: boolean;
  isUnassigned?: boolean;
  excludeEmployeeId?: string;
  isActive?: boolean;
}

export class OrderFilter extends BaseFilter implements IOrderFilter {
  id?: string;
  customerName?: string;
  customerEmail?: string;
  customerPhone?: string;
  displayOrderNumber?: string;
  employeeId?: string;
  cleaningDateFrom?: Date;
  cleaningDateTo?: Date;
  paymentStatuses?: number[];
  paymentTypes?: number[];
  orderStatuses?: number[];
  minTotalPrice?: number;
  maxTotalPrice?: number;
  hasAvailableSpots?: boolean | undefined;
  isUnassigned?: boolean | undefined;
  excludeEmployeeId?: string;
  isActive?: boolean;

  constructor(filter: IOrderFilter) {
    super();
    this.id = filter.id;
    this.customerName = filter.customerName;
    this.customerEmail = filter.customerEmail;
    this.customerPhone = filter.customerPhone;
    this.displayOrderNumber = filter.displayOrderNumber;
    this.employeeId = filter.employeeId;
    this.cleaningDateFrom = filter.cleaningDateFrom;
    this.cleaningDateTo = filter.cleaningDateTo;
    this.paymentStatuses = filter.paymentStatuses;
    this.paymentTypes = filter.paymentTypes;
    this.minTotalPrice = filter.minTotalPrice;
    this.maxTotalPrice = filter.maxTotalPrice;
    this.orderStatuses = filter.orderStatuses;
    this.hasAvailableSpots = filter.hasAvailableSpots;
    this.isUnassigned = filter.isUnassigned;
    this.excludeEmployeeId = filter.excludeEmployeeId;
    this.isActive = filter.isActive;
  }
}

export interface IUnitFilter {
  id?: string;
  name?: string;
  shortName?: string;
}

export class UnitFilter extends BaseFilter implements IUnitFilter {
  id?: string;
  name?: string;
  shortName?: string;

  constructor(filter: IUnitFilter) {
    super();
    this.id = filter.id;
    this.name = filter.name;
    this.shortName = filter.shortName;
  }
}

export interface IMaterialAnalyticsFilter {
  name?: string;
  article?: string;
  startDate?: Date;
  endDate?: Date;
  units?: string[];
  categories?: string[];
}

export class MaterialAnalyticsFilter
  extends BaseFilter
  implements IMaterialAnalyticsFilter
{
  name?: string;
  article?: string;
  startDate?: Date;
  endDate?: Date;
  units?: string[];
  categories?: string[];

  constructor(filter: IMaterialAnalyticsFilter) {
    super();
    this.name = filter?.name;
    this.article = filter?.article;
    this.startDate = filter?.startDate;
    this.endDate = filter?.endDate;
    this.units = filter?.units;
    this.categories = filter?.categories;
  }
}

export interface ISupplierFilter {
  id?: string;
  name?: string;
  address?: string;
  city?: string;
  zipCode?: string;
  phoneNumber?: string;
  email?: string;
  contactPerson?: string;
  website?: string;
  taxId?: string;
  isActive?: boolean;
}

export class SupplierFilter extends BaseFilter implements ISupplierFilter {
  id?: string;
  name?: string;
  address?: string;
  city?: string;
  zipCode?: string;
  phoneNumber?: string;
  email?: string;
  contactPerson?: string;
  website?: string;
  taxId?: string;
  isActive?: boolean;

  constructor(filter: ISupplierFilter) {
    super();
    this.id = filter.id;
    this.name = filter.name;
    this.address = filter.address;
    this.city = filter.city;
    this.zipCode = filter.zipCode;
    this.phoneNumber = filter.phoneNumber;
    this.email = filter.email;
    this.contactPerson = filter.contactPerson;
    this.website = filter.website;
    this.taxId = filter.taxId;
    this.isActive = filter.isActive;
  }
}

export interface IDisputeFilter {
  id?: string;
  orderId?: string;
  userId?: string;
  customerName?: string;
  customerEmail?: string;
  statuses?: number[];
  reasons?: number[];
  createdFrom?: Date;
  createdTo?: Date;
  resolvedFrom?: Date;
  resolvedTo?: Date;
  minRefundAmount?: number;
  maxRefundAmount?: number;
  isActive?: boolean;
}

export class DisputeFilter extends BaseFilter implements IDisputeFilter {
  id?: string;
  orderId?: string;
  userId?: string;
  customerName?: string;
  customerEmail?: string;
  statuses?: number[];
  reasons?: number[];
  createdFrom?: Date;
  createdTo?: Date;
  resolvedFrom?: Date;
  resolvedTo?: Date;
  minRefundAmount?: number;
  maxRefundAmount?: number;
  isActive?: boolean;

  constructor(filter: IDisputeFilter) {
    super();
    this.id = filter.id;
    this.orderId = filter.orderId;
    this.userId = filter.userId;
    this.customerName = filter.customerName;
    this.customerEmail = filter.customerEmail;
    this.statuses = filter.statuses;
    this.reasons = filter.reasons;
    this.createdFrom = filter.createdFrom;
    this.createdTo = filter.createdTo;
    this.resolvedFrom = filter.resolvedFrom;
    this.resolvedTo = filter.resolvedTo;
    this.minRefundAmount = filter.minRefundAmount;
    this.maxRefundAmount = filter.maxRefundAmount;
    this.isActive = filter.isActive;
  }
}
