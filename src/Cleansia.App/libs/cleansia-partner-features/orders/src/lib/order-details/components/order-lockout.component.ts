import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
} from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  CleansiaButtonComponent,
  CleansiaSectionComponent,
  CleansiaTextareaComponent,
} from '@cleansia/components';
import { OrderItem } from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { notBlank } from '../../components/dialog-validators';
import {
  formatDateTime,
  LOCKOUT_WAIT_MINUTES,
  LockoutStanding,
  lockoutOpensAt,
  lockoutStanding,
} from '../order-details.helpers';
import { OrderLockoutFacade } from './order-lockout.facade';
import { OrderPhotosFacade } from './order-photos.facade';
import { validatePhotoFile } from './order-photos.helpers';

// ReportOrderLockout.Validator: the note of the calls is at most 1000 characters.
const CALL_ATTEMPTS_MAX_LENGTH = 1000;

@Component({
  selector: 'cleansia-partner-order-lockout',
  standalone: true,
  imports: [
    TranslatePipe,
    ReactiveFormsModule,
    CleansiaButtonComponent,
    CleansiaSectionComponent,
    CleansiaTextareaComponent,
  ],
  templateUrl: './order-lockout.component.html',
  styleUrls: ['./order-lockout.component.scss'],
  providers: [OrderLockoutFacade, OrderPhotosFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderLockoutComponent {
  private readonly facade = inject(OrderLockoutFacade);
  private readonly snackbarService = inject(SnackbarService);

  readonly order = input.required<OrderItem>();
  readonly employeeId = input.required<string>();
  readonly reported = output<void>();

  protected readonly LockoutStanding = LockoutStanding;
  protected readonly lockoutWaitMinutes = LOCKOUT_WAIT_MINUTES;
  protected readonly callAttemptsMaxLength = CALL_ATTEMPTS_MAX_LENGTH;
  protected readonly callAttemptsControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, notBlank, Validators.maxLength(CALL_ATTEMPTS_MAX_LENGTH)],
  });

  protected readonly entrancePhotos = this.facade.entrancePhotos;
  protected readonly uploading = this.facade.uploading;
  protected readonly reporting = this.facade.reporting;

  protected readonly standing = computed(() =>
    lockoutStanding(this.order(), this.employeeId(), this.facade.now())
  );
  private readonly opensAt = computed(() => lockoutOpensAt(this.order().cleaningDateTime));
  protected readonly opensAtLabel = computed(() =>
    formatDateTime(this.opensAt() ?? undefined, this.facade.lang())
  );
  protected readonly reportedAtLabel = computed(() =>
    formatDateTime(this.order().lockoutReportedAt, this.facade.lang())
  );
  protected readonly hasEntrancePhoto = computed(() => this.entrancePhotos().length > 0);
  protected readonly canReport = computed(() => this.hasEntrancePhoto() && !this.reporting());

  constructor() {
    effect(() => {
      const opensAt = this.opensAt();
      if (this.standing() === LockoutStanding.NotYet && opensAt) {
        this.facade.wakeAt(opensAt);
      }
    });

    effect(() => {
      const orderId = this.order().id;
      if (this.standing() === LockoutStanding.Open && orderId) {
        this.facade.loadEntrancePhotos(orderId);
      }
    });
  }

  protected onEntrancePhotoSelected(event: Event): void {
    const fileInput = event.target as HTMLInputElement;
    const file = fileInput.files?.[0];
    fileInput.value = '';
    const orderId = this.order().id;
    if (!file || !orderId) return;

    const validation = validatePhotoFile(file);
    if (!validation.valid) {
      if (validation.errorKey) this.snackbarService.showErrorTranslated(validation.errorKey);
      return;
    }

    const reader = new FileReader();
    reader.onload = () => this.facade.uploadEntrancePhoto(orderId, reader.result as string, file);
    reader.onerror = () =>
      this.snackbarService.showErrorTranslated('global.messages.orders.photo_read_failed');
    reader.readAsDataURL(file);
  }

  protected report(): void {
    const orderId = this.order().id;
    if (!orderId) return;
    if (this.callAttemptsControl.invalid) {
      this.callAttemptsControl.markAsTouched();
      return;
    }
    this.facade.report(orderId, this.callAttemptsControl.value, () => this.reported.emit());
  }
}
