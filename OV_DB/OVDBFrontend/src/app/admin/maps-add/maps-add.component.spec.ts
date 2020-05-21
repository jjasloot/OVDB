import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { MapsAddComponent } from './maps-add.component';

describe('MapsAddComponent', () => {
  let component: MapsAddComponent;
  let fixture: ComponentFixture<MapsAddComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ MapsAddComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(MapsAddComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
