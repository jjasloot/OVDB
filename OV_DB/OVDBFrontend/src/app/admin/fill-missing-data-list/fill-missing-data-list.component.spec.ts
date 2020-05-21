import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { FillMissingDataListComponent } from './fill-missing-data-list.component';

describe('FillMissingDataListComponent', () => {
  let component: FillMissingDataListComponent;
  let fixture: ComponentFixture<FillMissingDataListComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ FillMissingDataListComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(FillMissingDataListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
