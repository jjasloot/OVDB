import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { EditMultipleComponent } from './edit-multiple.component';

describe('EditMultipleComponent', () => {
  let component: EditMultipleComponent;
  let fixture: ComponentFixture<EditMultipleComponent>;

  beforeEach(async(() => {
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
