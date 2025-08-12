import { Injectable } from '@angular/core';
import { TableState, TableStateConfig } from '../models/table-state.model';

@Injectable({
  providedIn: 'root'
})
export class TableStateService {
  private readonly STORAGE_PREFIX = 'ovdb_table_state_';

  /**
   * Save table state to localStorage
   * @param tableId Unique identifier for the table
   * @param state Current table state to save
   */
  saveTableState(tableId: string, state: TableState): void {
    try {
      const storageKey = this.getStorageKey(tableId);
      localStorage.setItem(storageKey, JSON.stringify(state));
    } catch (error) {
      console.warn('Failed to save table state to localStorage:', error);
    }
  }

  /**
   * Get table state from localStorage
   * @param tableId Unique identifier for the table
   * @param config Optional configuration for default values
   * @returns Saved table state or default values
   */
  getTableState(tableId: string, config?: TableStateConfig): TableState {
    try {
      const storageKey = this.getStorageKey(tableId);
      const savedState = localStorage.getItem(storageKey);
      
      if (savedState) {
        const parsedState = JSON.parse(savedState) as TableState;
        // Validate the parsed state has all required properties
        if (this.isValidTableState(parsedState)) {
          return parsedState;
        }
      }
    } catch (error) {
      console.warn('Failed to load table state from localStorage:', error);
    }

    // Return default state if no valid saved state exists
    return this.getDefaultState(config);
  }

  /**
   * Clear saved table state
   * @param tableId Unique identifier for the table
   */
  clearTableState(tableId: string): void {
    try {
      const storageKey = this.getStorageKey(tableId);
      localStorage.removeItem(storageKey);
    } catch (error) {
      console.warn('Failed to clear table state from localStorage:', error);
    }
  }

  /**
   * Get current table state from table components
   * @param pageIndex Current page index
   * @param pageSize Current page size
   * @param sortActive Current sort column
   * @param sortDirection Current sort direction
   * @param filter Current filter value
   * @returns TableState object
   */
  getCurrentState(
    pageIndex: number,
    pageSize: number,
    sortActive: string,
    sortDirection: 'asc' | 'desc' | '',
    filter: string
  ): TableState {
    return {
      pageIndex,
      pageSize,
      sortActive,
      sortDirection,
      filter
    };
  }

  private getStorageKey(tableId: string): string {
    return `${this.STORAGE_PREFIX}${tableId}`;
  }

  private isValidTableState(state: unknown): state is TableState {
    return (
      typeof state === 'object' &&
      state !== null &&
      typeof (state as TableState).pageIndex === 'number' &&
      typeof (state as TableState).pageSize === 'number' &&
      typeof (state as TableState).sortActive === 'string' &&
      ((state as TableState).sortDirection === 'asc' || 
       (state as TableState).sortDirection === 'desc' || 
       (state as TableState).sortDirection === '') &&
      typeof (state as TableState).filter === 'string'
    );
  }

  private getDefaultState(config?: TableStateConfig): TableState {
    return {
      pageIndex: 0,
      pageSize: config?.defaultPageSize || 10,
      sortActive: config?.defaultSortActive || '',
      sortDirection: config?.defaultSortDirection || '',
      filter: ''
    };
  }
}