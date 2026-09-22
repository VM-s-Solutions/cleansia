import { BaseFilter } from './filter.models';
import { BaseSortDefinition } from './sort.models';

export class Page<T extends object> {
  data: T[];
  total: number;
  size: number;
  page: number;
  filter: BaseFilter;
  sort: BaseSortDefinition[];

  constructor(
    data: T[] = [],
    total = 0,
    size = 50,
    page = 1,
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
}
