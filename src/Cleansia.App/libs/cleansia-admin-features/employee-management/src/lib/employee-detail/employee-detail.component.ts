import { CommonModule } from '@angular/common';
import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Code, EmployeeDocumentItem, TimeRange } from '@cleansia/admin-services';
import { selectDayOfWeekCodes } from '@cleansia/admin-stores';
import {
  CleansiaAvailabilityComponent,
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslatePipe } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { ToastModule } from 'primeng/toast';
import { EmployeeDetailFacade } from './employee-detail.facade';

@Component({
  selector: 'cleansia-admin-employee-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    CleansiaButtonComponent,
    CleansiaAvailabilityComponent,
    TranslatePipe,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaLanguageSwitcherComponent,
    ToastModule,
  ],
  templateUrl: './employee-detail.component.html',
  providers: [EmployeeDetailFacade, DialogService],
})
export class EmployeeDetailComponent implements OnInit, OnDestroy {
  protected readonly facade = inject(EmployeeDetailFacade);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly store = inject(Store);

  readonly daysOfWeek = signal<Code[]>([]);
  availabilityValue: { [key: string]: TimeRange[] } = {};

  ngOnInit(): void {
    const employeeId = this.route.snapshot.paramMap.get('employeeId');
    if (employeeId) {
      this.facade.loadEmployeeDetail(employeeId);
    } else {
      this.router.navigate([CleansiaAdminRoute.EMPLOYEE_MANAGEMENT]);
    }

    this.store.select(selectDayOfWeekCodes).subscribe((codes: Code[]) => {
      this.daysOfWeek.set(codes);
    });
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
  }

  goBack(): void {
    this.router.navigate([CleansiaAdminRoute.EMPLOYEE_MANAGEMENT]);
  }

  getContractStatusClass(status: string): string {
    const statusName = status?.toLowerCase().replace(/\s+/g, '-') || 'pending';
    return `contract-status-badge status-${statusName}`;
  }

  formatDate(date: string | Date | null | undefined): string {
    if (!date) return '-';
    const dateObj = date instanceof Date ? date : new Date(date);
    return dateObj.toLocaleDateString('en-GB');
  }

  formatDateTime(date: string | Date | null | undefined): string {
    if (!date) return '-';
    const dateObj = date instanceof Date ? date : new Date(date);
    return dateObj.toLocaleString('en-GB');
  }

  formatTimeRange(start: any, end: any): string {
    if (!start || !end) return '-';
    return `${start.toString()} - ${end.toString()}`;
  }

  onRejectDocument(document: EmployeeDocumentItem): void {
    this.facade.openRejectDocumentDialog(document);
  }

  onEditAvailability(): void {
    const employee = this.facade.employee();
    this.availabilityValue = employee?.availability
      ? { ...employee.availability }
      : {};
    this.facade.startEditingAvailability();
  }

  onSaveAvailability(): void {
    this.facade.saveAvailability(this.availabilityValue);
  }

  onCancelEditAvailability(): void {
    this.facade.cancelEditingAvailability();
  }
}
