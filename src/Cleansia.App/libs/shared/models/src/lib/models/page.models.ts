import { BaseFilter } from './filter.models';
import { BaseSortDefinition } from './sort.models';

export const DEFAULT_PAGE_SIZE = 50;
export const DEFAULT_PAGE_NUMBER = 1;
export const EMPTY_DATA = [];
export const EMPTY_TOTAL = 0;

export interface IRequestParams {
  id?: string;
  filter?: BaseFilter;
  limit?: number;
  offset?: number;
  sort?: BaseSortDefinition[];
}

export interface IBaseEntity {
  id: string;
}

export interface IPage<T> {
  data: T[];
  total: number;
  size: number;
  page: number;
  filter: BaseFilter;
  sort: BaseSortDefinition[];
}

export class Page<T extends object> implements IPage<T> {
  data: T[];
  total: number;
  size: number;
  page: number;
  filter: BaseFilter;
  sort: BaseSortDefinition[];

  constructor(
    data: T[] = EMPTY_DATA,
    total: number = EMPTY_TOTAL,
    size: number = DEFAULT_PAGE_SIZE,
    page: number = DEFAULT_PAGE_NUMBER,
    filter: BaseFilter = new BaseFilter(),
    sort: BaseSortDefinition[] = [],
  ) {
    this.data = data;
    this.total = total;
    this.size = size;
    this.page = page;
    this.filter = filter;
    this.sort = sort;
  }

  static create<T extends object>(): Page<T> {
    return new Page<T>();
  }

  static createWith<T extends object>(data: T[]): Page<T> {
    return new Page<T>(data, data.length);
  }

  static createWithTotal<T extends object>(data: T[], total: number): Page<T> {
    return new Page<T>(data, total);
  }

  static createWithTotalAndSize<T extends object>(
    data: T[],
    total: number,
    size: number,
  ): Page<T> {
    return new Page<T>(data, total, size);
  }

  static createWithTotalSizeAndPage<T extends object>(
    data: T[],
    total: number,
    size: number,
    pageNumber: number,
  ): Page<T> {
    return new Page<T>(data, total, size, pageNumber);
  }

  static createWithTotalSizeAndPageAndFilter<T extends object>(
    data: T[],
    total: number,
    size: number,
    pageNumber: number,
    filter: BaseFilter = new BaseFilter(),
  ): Page<T> {
    return new Page<T>(data, total, size, pageNumber, filter);
  }

  static createDefaultWithSpecifiedSort<T extends object>(
    sort: BaseSortDefinition[],
  ): Page<T> {
    return new Page<T>(
      EMPTY_DATA,
      EMPTY_TOTAL,
      DEFAULT_PAGE_SIZE,
      DEFAULT_PAGE_NUMBER,
      new BaseFilter(),
      sort,
    );
  }

  updateData(data: T[]): Page<T> {
    return new Page<T>(
      data,
      this.total,
      this.size,
      this.page,
      this.filter,
      this.sort,
    );
  }

  updateTotal(total: number): Page<T> {
    return new Page<T>(
      this.data,
      total,
      this.size,
      this.page,
      this.filter,
      this.sort,
    );
  }

  updateSize(size: number): Page<T> {
    return new Page<T>(
      this.data,
      this.total,
      size,
      this.page,
      this.filter,
      this.sort,
    );
  }

  updatePage(page: number): Page<T> {
    return new Page<T>(
      this.data,
      this.total,
      this.size,
      page,
      this.filter,
      this.sort,
    );
  }

  updatePageAndSize(page: number, size: number): Page<T> {
    return new Page<T>(
      this.data,
      this.total,
      size,
      page,
      this.filter,
      this.sort,
    );
  }

  updateFilter(filter: BaseFilter): Page<T> {
    filter.isFilterChanged = true;
    return new Page<T>(
      this.data,
      this.total,
      this.size,
      DEFAULT_PAGE_NUMBER,
      filter,
      this.sort,
    );
  }

  updateSort(sort: BaseSortDefinition[]): Page<T> {
    return new Page<T>(
      this.data,
      this.total,
      this.size,
      this.page,
      this.filter,
      sort,
    );
  }

  updateDataAndTotal(data: T[], total: number): Page<T> {
    return new Page<T>(
      data,
      total,
      this.size,
      this.page,
      this.filter,
      this.sort,
    );
  }

  updateDataAndTotalAndPageNumberAndPageSize(
    data: T[],
    total: number,
    pageNumber: number,
    pageSize: number,
  ): Page<T> {
    return new Page<T>(
      data,
      total,
      pageSize,
      pageNumber,
      this.filter,
      this.sort,
    );
  }

  updateDataPageNumberPageSize(
    data: T[],
    pageNumber: number,
    pageSize: number,
  ): Page<T> {
    const newData = [...this.data, ...data];
    const uniqueData = newData.filter(
      (item: T & { id?: string }, index, self) =>
        index ===
        self.findIndex(
          (t: T & { id?: string }) =>
            'id' in t && 'id' in item && t.id === item.id,
        ),
    );

    return new Page<T>(
      uniqueData,
      this.total,
      pageSize,
      pageNumber,
      this.filter,
      this.sort,
    );
  }

  transformToRequestParams(requestParams?: IRequestParams): IRequestParams {
    return {
      filter: requestParams?.filter ?? this.filter,
      limit: requestParams?.limit ?? this.size,
      offset: requestParams?.offset ?? (this.page - 1) * this.size,
      sort: requestParams?.sort ?? this.sort,
    };
  }

  deepClone(): Page<T> {
    return new Page<T>(
      [...this.data],
      this.total,
      this.size,
      this.page,
      new BaseFilter(this.filter),
      [...this.sort],
    );
  }
}

export function getDefaultRequestParams<T extends BaseFilter>(
  filter?: T,
): IRequestParams {
  return {
    filter: filter || new BaseFilter(),
    limit: DEFAULT_PAGE_SIZE,
    offset: 0,
    sort: [],
  };
}
