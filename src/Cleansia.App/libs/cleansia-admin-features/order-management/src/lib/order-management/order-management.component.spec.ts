import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControlName } from '@angular/forms';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { CleansiaCheckboxComponent } from '@cleansia/components';
import { TranslateModule } from '@ngx-translate/core';
import { MessageService } from 'primeng/api';
import { OrderManagementComponent } from './order-management.component';

describe('OrderManagementComponent', () => {
  let component: OrderManagementComponent;
  let fixture: ComponentFixture<OrderManagementComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OrderManagementComponent, TranslateModule.forRoot()],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        MessageService,
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(OrderManagementComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('offers the staffing filter in the drawer as one checkbox on the unassignedOnly control', () => {
    const boxes = fixture.debugElement
      .queryAll(By.directive(CleansiaCheckboxComponent))
      .filter((box) => box.injector.get(FormControlName, null)?.name === 'unassignedOnly');

    expect(boxes).toHaveLength(1);
    expect((boxes[0].componentInstance as CleansiaCheckboxComponent).label()).toBe(
      'pages.order_management.filters.unassigned_only'
    );
  });
});
