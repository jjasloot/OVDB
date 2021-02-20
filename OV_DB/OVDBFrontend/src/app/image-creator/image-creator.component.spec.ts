import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { ImageCreatorComponent } from './image-creator.component';

describe('ImageCreatorComponent', () => {
  let component: ImageCreatorComponent;
  let fixture: ComponentFixture<ImageCreatorComponent>;

  beforeEach(waitForAsync(() => {
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
