import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { ImageCreatorComponent } from './image-creator.component';

describe('ImageCreatorComponent', () => {
  let component: ImageCreatorComponent;
  let fixture: ComponentFixture<ImageCreatorComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ ImageCreatorComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(ImageCreatorComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
