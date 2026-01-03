import { CommonModule } from '@angular/common';
import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Code, EmployeeDocumentItem } from '@cleansia/admin-services';
import { selectDayOfWeekCodes } from '@cleansia/admin-stores';
import {
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
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
    CleansiaButtonComponent,
    TranslatePipe,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaLanguageSwitcherComponent,
    ToastModule,
  ],
  templateUrl: './employee-detail.component.html',
  styleUrl: './employee-detail.component.scss',
  providers: [EmployeeDetailFacade, DialogService],
})
export class EmployeeDetailComponent implements OnInit, OnDestroy {
  protected readonly facade = inject(EmployeeDetailFacade);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly store = inject(Store);

  readonly daysOfWeek = signal<Code[]>([]);

  ngOnInit(): void {
    const employeeId = this.route.snapshot.paramMap.get('employeeId');
    if (employeeId) {
      this.facade.loadEmployeeDetail(employeeId);
    } else {
      this.router.navigate(['/employee-management']);
    }

    this.store.select(selectDayOfWeekCodes).subscribe((codes: Code[]) => {
      this.daysOfWeek.set(codes);
    });
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
  }

  goBack(): void {
    this.router.navigate(['/employee-management']);
  }

  getContractStatusClass(status: string): string {
    const statusName = status?.toLowerCase().replace(/\s+/g, '-') || 'pending';
    return `contract-status-badge status-${statusName}`;
  }

  formatDate(date: string | Date | null | undefined): string {
    if (!date) return '-';
    const dateObj = date instanceof Date ? date : new Date(date);
    return dateObj.toLocaleDateString('cs-CZ');
  }

  formatDateTime(date: string | Date | null | undefined): string {
    if (!date) return '-';
    const dateObj = date instanceof Date ? date : new Date(date);
    return dateObj.toLocaleString('cs-CZ');
  }

  formatTimeRange(start: any, end: any): string {
    if (!start || !end) return '-';
    return `${start.toString()} - ${end.toString()}`;
  }

  onRejectDocument(document: EmployeeDocumentItem): void {
    this.facade.openRejectDocumentDialog(document);
  }
}
