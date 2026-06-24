import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { DisputeStatus } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTextInputComponent,
  CleansiaTextareaComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import {
  AuditResourceType,
  buildAuditResourceHistoryRoute,
  CleansiaAdminRoute,
  Policy,
} from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DisputeDetailFacade } from './dispute-detail.facade';
import {
  buildDisputeStatusOptions,
  DISPUTE_STATUS_LABEL_KEYS,
  DisputeStatusOption,
  getDisputeStatusClass,
} from '../disputes-management/disputes-management.models';

@Component({
  selector: 'cleansia-admin-dispute-detail',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaSelectComponent,
    CleansiaTextInputComponent,
    CleansiaTextareaComponent,
    CleansiaTitleComponent,
    CleansiaPermissionDirective,
  ],
  templateUrl: './dispute-detail.component.html',
  providers: [DisputeDetailFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DisputeDetailComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(DisputeDetailFacade);

  protected readonly Policy = Policy;

  private disputeId = '';
  statusOptions: DisputeStatusOption[] = [];
  readonly selectedStatus = signal<DisputeStatus | null>(null);

  readonly resolveForm = this.fb.nonNullable.group({
    refundAmount: [null as number | null],
    resolutionNotes: [''],
  });

  readonly messageForm = this.fb.nonNullable.group({
    message: [''],
  });

  ngOnInit(): void {
    this.disputeId = this.route.snapshot.paramMap.get('disputeId') ?? '';
    this.statusOptions = buildDisputeStatusOptions(this.translate);
    if (this.disputeId) {
      this.facade.loadDispute(this.disputeId);
    }
  }

  goBack(): void {
    this.router.navigate([CleansiaAdminRoute.DISPUTE_MANAGEMENT]);
  }

  viewAuditHistory(): void {
    if (!this.disputeId) return;
    this.router.navigate(
      buildAuditResourceHistoryRoute(AuditResourceType.Dispute, this.disputeId)
    );
  }

  hasRefundAmount(): boolean {
    return this.facade.dispute()?.refundAmount != null;
  }

  getStatusClass(value: number | undefined | null): string {
    return getDisputeStatusClass(value);
  }

  getStatusLabel(value: number | undefined | null): string {
    if (value == null) return '';
    const key = DISPUTE_STATUS_LABEL_KEYS[value];
    return key ? this.translate.instant(key) : '';
  }

  onStatusSelected(value: DisputeStatus | null): void {
    this.selectedStatus.set(value);
  }

  submitStatusUpdate(): void {
    const status = this.selectedStatus();
    if (status == null) return;
    this.facade.updateStatus(this.disputeId, status);
  }

  submitResolve(): void {
    const { refundAmount, resolutionNotes } = this.resolveForm.getRawValue();
    this.facade.resolve(this.disputeId, refundAmount, resolutionNotes);
  }

  submitMessage(): void {
    const message = this.messageForm.getRawValue().message;
    this.facade.addMessage(this.disputeId, message, () =>
      this.messageForm.reset({ message: '' })
    );
  }
}
