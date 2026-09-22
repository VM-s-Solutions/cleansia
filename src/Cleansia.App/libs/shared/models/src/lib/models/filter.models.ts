export interface IBaseFilter {
  isFilterChanged?: boolean;
}

export class BaseFilter implements IBaseFilter {
  isFilterChanged?: boolean;

  constructor(data?: IBaseFilter) {
    this.isFilterChanged = data?.isFilterChanged ?? false;
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
