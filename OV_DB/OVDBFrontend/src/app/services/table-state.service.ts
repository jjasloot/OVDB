import { Injectable, inject } from '@angular/core';
import { NavigationStart, Router } from '@angular/router';
import { TableState, TableStateConfig } from '../models/table-state.model';
import { filter } from 'rxjs/operators';

@Injectable({
  providedIn: 'root'
})
export class TableStateService {
  private readonly HISTORY_STATE_KEY = 'ovdbTableStates';


  /**
   * Save table state to the current browser history entry.
   * @param tableId Unique identifier for the table
   * @param state Current table state to save
   */
  saveTableState(tableId: string, state: TableState): void {
    try {
      const historyState = this.getCurrentHistoryState();
      const tableStates = this.getHistoryTableStates(historyState);

      this.replaceCurrentHistoryState({
        ...historyState,
        [this.HISTORY_STATE_KEY]: {
          ...tableStates,
          [tableId]: state
        }
      });
    } catch (error) {
      console.warn('Failed to save table state to browser history:', error);
    }
  }

  /**
   * Get table state from the current browser history entry.
   * @param tableId Unique identifier for the table
   * @param config Optional configuration for default values
   * @returns Saved table state or default values
   */
  getTableState(tableId: string, config?: TableStateConfig): TableState {
    try {
      const historyState = this.getCurrentHistoryState();
      const tableStates = this.getHistoryTableStates(historyState);
      const savedState = tableStates[tableId];

      if (savedState) {
        if (this.isValidTableState(savedState)) {
          return savedState;
        }
      }
    } catch (error) {
      console.warn('Failed to load table state from browser history:', error);
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
      const historyState = this.getCurrentHistoryState();
      const tableStates = this.getHistoryTableStates(historyState);

      if (!(tableId in tableStates)) {
        return;
      }

      const { [tableId]: _, ...remainingTableStates } = tableStates;
      const nextHistoryState = { ...historyState };

      if (Object.keys(remainingTableStates).length === 0) {
        delete nextHistoryState[this.HISTORY_STATE_KEY];
      } else {
        nextHistoryState[this.HISTORY_STATE_KEY] = remainingTableStates;
      }

      this.replaceCurrentHistoryState(nextHistoryState);
    } catch (error) {
      console.warn('Failed to clear table state from browser history:', error);
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

  private getCurrentHistoryState(): Record<string, unknown> {
    const historyState = globalThis.history?.state;

    if (typeof historyState !== 'object' || historyState === null) {
      return {};
    }

    return historyState as Record<string, unknown>;
  }

  private getHistoryTableStates(historyState: Record<string, unknown>): Record<string, unknown> {
    const tableStates = historyState[this.HISTORY_STATE_KEY];

    if (typeof tableStates !== 'object' || tableStates === null) {
      return {};
    }

    return tableStates as Record<string, unknown>;
  }

  private replaceCurrentHistoryState(state: Record<string, unknown>): void {
    if (!globalThis.history || !globalThis.location) {
      return;
    }

    globalThis.history.replaceState(state, globalThis.document?.title ?? '', globalThis.location.href);
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