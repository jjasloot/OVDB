import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { MapsListBottomsheetComponent } from './maps-list-bottomsheet.component';

describe('MapsListBottomsheetComponent', () => {
  let component: MapsListBottomsheetComponent;
  let fixture: ComponentFixture<MapsListBottomsheetComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ MapsListBottomsheetComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(MapsListBottomsheetComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
