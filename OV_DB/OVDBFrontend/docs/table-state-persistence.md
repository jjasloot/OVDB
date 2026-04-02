# Table State Persistence

This document explains how the table state persistence feature works and how to apply it to other mat-tables in the application.

## Overview

The `TableStateService` preserves pagination, sorting, and filtering state per browser history entry. The service restores that state only when Angular reports a `popstate` navigation, so table state returns on browser back or forward, while fresh router navigations and page reloads start with defaults.

## Features

- **Pagination**: Preserves current page index and page size
- **Sorting**: Preserves active sort column and direction (asc/desc)
- **Filtering**: Preserves search filter text
- **History-aware restore**: State comes back only when revisiting the same history entry
- **Fresh navigation reset**: New navigations to the route start from defaults unless the new entry also stores state
- **Reload-safe behavior**: A page refresh does not restore the previous table state
- **Error handling**: Graceful fallback when the History API state is unavailable

## Usage

### 1. Import Required Dependencies

```typescript
import { TableStateService } from 'src/app/services/table-state.service';
import { TableState } from 'src/app/models/table-state.model';
```

### 2. Inject Service in Component

```typescript
constructor(
  // ... other dependencies
  private tableStateService: TableStateService
) {}
```

### 3. Define Table ID

```typescript
export class YourTableComponent {
  private readonly TABLE_ID = 'your-table-name'; // Unique identifier
  
  // ... rest of component
}
```

### 4. Restore State on Component Init

```typescript
ngOnInit() {
  // Restore saved table state
  this.restoreTableState();
  
  // ... other initialization
}

private restoreTableState(): void {
  const savedState = this.tableStateService.getTableState(this.TABLE_ID, {
    defaultPageSize: 10,
    defaultSortActive: 'date',
    defaultSortDirection: 'desc'
  });

  this.restoredState = savedState;
}
```

### 5. Apply Restored State After View Init

```typescript
ngAfterViewInit() {
  // Apply restored state to components after they're initialized
  this.applyRestoredState();

  // ... existing event subscriptions
}

private applyRestoredState(): void {
  if (this.restoredState) {
    // Apply pagination state
    if (this.paginator()) {
      this.paginator().pageIndex = this.restoredState.pageIndex;
      this.paginator().pageSize = this.restoredState.pageSize;
    }

    // Apply sorting state
    if (this.sort()) {
      this.sort().active = this.restoredState.sortActive;
      this.sort().direction = this.restoredState.sortDirection;
    }

    // Apply filter state
    if (this.input() && this.restoredState.filter) {
      this.input().nativeElement.value = this.restoredState.filter;
    }

    this.restoredState = null;
  }
}
```

### 6. Save State on Changes

```typescript
ngAfterViewInit() {
  // Save state when sorting changes
  this.sort().sortChange.subscribe(() => {
    this.paginator().pageIndex = 0;
    this.saveCurrentTableState();
  });

  // Save state when pagination changes
  this.paginator().page.subscribe(() => {
    this.saveCurrentTableState();
  });

  // Save state when filtering changes
  fromEvent(this.input().nativeElement, "keyup")
    .pipe(
      debounceTime(150),
      distinctUntilChanged(),
      tap(() => {
        this.paginator().pageIndex = 0;
        this.saveCurrentTableState();
        this.filter$.next();
      })
    )
    .subscribe();
}

private saveCurrentTableState(): void {
  if (!this.paginator() || !this.sort()) return;

  const currentState = this.tableStateService.getCurrentState(
    this.paginator().pageIndex,
    this.paginator().pageSize,
    this.sort().active,
    this.sort().direction,
    this.filterValue
  );

  this.tableStateService.saveTableState(this.TABLE_ID, currentState);
}
```

## Example Implementation

See `src/app/admin/routes-list/routes-list.component.ts` for a complete working example.

## API Reference

### TableStateService

#### Methods

- `saveTableState(tableId: string, state: TableState): void`
  - Saves table state to the current history entry
  
- `getTableState(tableId: string, config?: TableStateConfig): TableState`
  - Retrieves table state from the current history entry when navigation was triggered by browser back or forward
  
- `clearTableState(tableId: string): void`
  - Removes saved table state
  
- `getCurrentState(...): TableState`
  - Helper method to create TableState from current component values

### TableState Interface

```typescript
interface TableState {
  pageIndex: number;
  pageSize: number;
  sortActive: string;
  sortDirection: 'asc' | 'desc' | '';
  filter: string;
}
```

### TableStateConfig Interface

```typescript
interface TableStateConfig {
  defaultPageSize?: number;
  defaultSortActive?: string;
  defaultSortDirection?: 'asc' | 'desc' | '';
}
```

## Storage Format

State is stored in `history.state.ovdbTableStates` under the current browser history entry.

Example history state:
```json
{
  "navigationId": 42,
  "ovdbTableStates": {
    "routes-list": {
      "pageIndex": 9,
      "pageSize": 10,
      "sortActive": "date",
      "sortDirection": "desc",
      "filter": "test route"
    }
  }
}
```

## Notes

- State is automatically validated when loaded
- Invalid or corrupted state falls back to defaults
- Service preserves Angular router metadata already present in `history.state`
- State is intentionally not shared across browser sessions or fresh route navigations
- Each table requires a unique TABLE_ID to avoid conflicts