export interface TableState {
  pageIndex: number;
  pageSize: number;
  sortActive: string;
  sortDirection: 'asc' | 'desc' | '';
  filter: string;
}

export interface TableStateConfig {
  defaultPageSize?: number;
  defaultSortActive?: string;
  defaultSortDirection?: 'asc' | 'desc' | '';
}