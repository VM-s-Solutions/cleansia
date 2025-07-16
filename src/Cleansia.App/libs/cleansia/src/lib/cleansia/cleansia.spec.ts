import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Cleansia } from './cleansia';

describe('Cleansia', () => {
  let component: Cleansia;
  let fixture: ComponentFixture<Cleansia>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Cleansia],
    }).compileComponents();

    fixture = TestBed.createComponent(Cleansia);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
