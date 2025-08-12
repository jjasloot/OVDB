# Table State Persistence

This document explains how the table state persistence feature works and how to apply it to other mat-tables in the application.

## Overview

The `TableStateService` automatically preserves pagination, sorting, and filtering state for mat-tables when users navigate away and return. State is persisted in localStorage across browser sessions.

## Features

- **Pagination**: Preserves current page index and page size
- **Sorting**: Preserves active sort column and direction (asc/desc)
- **Filtering**: Preserves search filter text
- **Cross-session persistence**: State survives browser refreshes and sessions
- **Error handling**: Graceful fallback when localStorage is unavailable

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
  - Saves table state to localStorage
  
- `getTableState(tableId: string, config?: TableStateConfig): TableState`
  - Retrieves table state from localStorage with default fallbacks
  
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

State is stored in localStorage with keys like `ovdb_table_state_{tableId}`.

Example stored value:
```json
{
  "pageIndex": 9,
  "pageSize": 10,
  "sortActive": "date",
  "sortDirection": "desc",
  "filter": "test route"
}
```

## Notes

- State is automatically validated when loaded
- Invalid or corrupted state falls back to defaults
- Service handles localStorage unavailability gracefully
- Each table requires a unique TABLE_ID to avoid conflicts