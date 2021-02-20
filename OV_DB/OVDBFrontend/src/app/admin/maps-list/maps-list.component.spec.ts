import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { MapsListComponent } from './maps-list.component';

describe('MapsComponent', () => {
  let component: MapsListComponent;
  let fixture: ComponentFixture<MapsListComponent>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
      declarations: [ MapsListComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(MapsListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
