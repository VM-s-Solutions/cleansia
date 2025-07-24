import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CleansiaSectionComponent } from './cleansia-section.component';

describe('CleansiaSectionComponent', () => {
  let component: CleansiaSectionComponent;
  let fixture: ComponentFixture<CleansiaSectionComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CleansiaSectionComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(CleansiaSectionComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
