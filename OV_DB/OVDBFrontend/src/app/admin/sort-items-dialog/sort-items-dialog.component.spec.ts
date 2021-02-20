import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { SortItemsDialogComponent } from './sort-items-dialog.component';

describe('SortItemsDialogComponent', () => {
  let component: SortItemsDialogComponent;
  let fixture: ComponentFixture<SortItemsDialogComponent>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
      declarations: [ SortItemsDialogComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(SortItemsDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
