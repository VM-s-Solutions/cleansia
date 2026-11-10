import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DisputeEvidenceDto, DisputeStatus } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaCheckboxComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTextInputComponent,
  CleansiaTextareaComponent,
  CleansiaTitleComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import {
  AuditResourceType,
  buildAuditResourceHistoryRoute,
  CleansiaAdminRoute,
  Policy,
  SnackbarService,
} from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DisputeDetailFacade } from './dispute-detail.facade';
import {
  buildDisputeStatusOptions,
  DisputeStatusOption,
} from '../disputes-management/disputes-management.models';

const CLEANER_CHARGE_REASON_MAX = 500;
const RESOLUTION_NOTES_MAX = 2000;
const IMAGE_PATH = /\.(jpe?g|png|gif|webp)$/i;

@Component({
  selector: 'cleansia-admin-dispute-detail',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaCheckboxComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaSelectComponent,
    CleansiaStatusBadgeComponent,
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
  private readonly snackbar = inject(SnackbarService);
  protected readonly facade = inject(DisputeDetailFacade);

  protected readonly Policy = Policy;
  protected readonly resolutionNotesMax = RESOLUTION_NOTES_MAX;
  protected readonly orderManagementRoute = CleansiaAdminRoute.ORDER_MANAGEMENT;

  private disputeId = '';
  statusOptions: DisputeStatusOption[] = [];
  readonly selectedStatus = signal<DisputeStatus | null>(null);

  readonly chargingCleaner = signal<boolean>(false);

  readonly crewOptions = computed<ICleansiaSelectOption[]>(() =>
    this.facade
      .crew()
      .filter((employee) => !!employee.employeeId)
      .map((employee) => ({
        label:
          employee.fullName ||
          this.translate.instant('pages.disputes_management.resolve.charge.unnamed'),
        value: employee.employeeId,
      }))
  );

  readonly canChargeCleaner = computed(() => this.crewOptions().length > 0);

  readonly resolveForm = this.fb.nonNullable.group({
    refundAmount: [null as number | null],
    resolutionNotes: ['', [Validators.required, Validators.maxLength(RESOLUTION_NOTES_MAX)]],
    charge: this.fb.nonNullable.group({
      employeeId: [null as string | null, Validators.required],
      amount: [null as number | null, [Validators.required, Validators.min(0.01)]],
      reason: ['', [Validators.required, Validators.maxLength(CLEANER_CHARGE_REASON_MAX)]],
    }),
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

  isImageEvidence(item: DisputeEvidenceDto): boolean {
    return IMAGE_PATH.test(item.filePath ?? '');
  }

  onStatusSelected(value: DisputeStatus | null): void {
    this.selectedStatus.set(value);
  }

  submitStatusUpdate(): void {
    const status = this.selectedStatus();
    if (status == null) return;
    this.facade.updateStatus(this.disputeId, status);
  }

  onChargeCleanerToggled(value: boolean): void {
    this.chargingCleaner.set(value);
  }

  submitResolve(): void {
    const { refundAmount, resolutionNotes, charge } = this.resolveForm.getRawValue();
    if (!resolutionNotes.trim()) {
      this.resolveForm.controls.resolutionNotes.markAsTouched();
      this.snackbar.showErrorTranslated('pages.disputes_management.resolve.notes_required');
      return;
    }
    if (!this.chargingCleaner() || !this.canChargeCleaner()) {
      this.facade.resolve(this.disputeId, refundAmount, resolutionNotes);
      return;
    }

    const chargeGroup = this.resolveForm.controls.charge;
    if (chargeGroup.invalid || !charge.employeeId || charge.amount == null) {
      chargeGroup.markAllAsTouched();
      this.snackbar.showErrorTranslated('pages.disputes_management.resolve.charge.incomplete');
      return;
    }
    this.facade.resolve(this.disputeId, refundAmount, resolutionNotes, {
      employeeId: charge.employeeId,
      amount: charge.amount,
      reason: charge.reason,
    });
  }

  submitMessage(): void {
    const message = this.messageForm.getRawValue().message;
    this.facade.addMessage(this.disputeId, message, () =>
      this.messageForm.reset({ message: '' })
    );
  }
}
