import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { EditMultipleComponent } from './edit-multiple.component';

describe('EditMultipleComponent', () => {
  let component: EditMultipleComponent;
  let fixture: ComponentFixture<EditMultipleComponent>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
      declarations: [ EditMultipleComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(EditMultipleComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
