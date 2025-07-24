import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CleansiaMenuComponent } from './cleansia-menu.component';

describe('CleansiaMenuComponent', () => {
  let component: CleansiaMenuComponent;
  let fixture: ComponentFixture<CleansiaMenuComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CleansiaMenuComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(CleansiaMenuComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
