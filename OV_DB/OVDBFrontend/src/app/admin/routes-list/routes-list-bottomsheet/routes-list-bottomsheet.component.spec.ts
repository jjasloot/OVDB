import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { RoutesListBottomsheetComponent } from './routes-list-bottomsheet.component';

describe('RoutesListBottomsheetComponent', () => {
  let component: RoutesListBottomsheetComponent;
  let fixture: ComponentFixture<RoutesListBottomsheetComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ RoutesListBottomsheetComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(RoutesListBottomsheetComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
