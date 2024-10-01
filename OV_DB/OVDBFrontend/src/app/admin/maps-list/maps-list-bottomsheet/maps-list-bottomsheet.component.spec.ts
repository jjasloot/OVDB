import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { MapsListBottomsheetComponent } from './maps-list-bottomsheet.component';

describe('MapsListBottomsheetComponent', () => {
  let component: MapsListBottomsheetComponent;
  let fixture: ComponentFixture<MapsListBottomsheetComponent>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
    imports: [MapsListBottomsheetComponent]
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
