import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { MapsAddComponent } from './maps-add.component';

describe('MapsAddComponent', () => {
  let component: MapsAddComponent;
  let fixture: ComponentFixture<MapsAddComponent>;

  beforeEach(waitForAsync(() => {
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
