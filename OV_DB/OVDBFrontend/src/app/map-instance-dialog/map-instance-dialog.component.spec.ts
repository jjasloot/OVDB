import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { MapInstanceDialogComponent } from './map-instance-dialog.component';

describe('MapInstanceDialogComponent', () => {
  let component: MapInstanceDialogComponent;
  let fixture: ComponentFixture<MapInstanceDialogComponent>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
      declarations: [ MapInstanceDialogComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(MapInstanceDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
