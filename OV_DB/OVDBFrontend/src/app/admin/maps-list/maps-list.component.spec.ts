import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { MapsListComponent } from './maps-list.component';

describe('MapsComponent', () => {
  let component: MapsListComponent;
  let fixture: ComponentFixture<MapsListComponent>;

  beforeEach(async(() => {
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
