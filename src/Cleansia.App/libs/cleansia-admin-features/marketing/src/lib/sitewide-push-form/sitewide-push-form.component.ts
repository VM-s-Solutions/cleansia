import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ADMINAPIBASEURL } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaSectionComponent,
  CleansiaTextInputComponent,
  CleansiaTextareaComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { DialogService, SnackbarService } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Admin form for the `promo.new_sitewide` push event. Posts a 5-locale
 * title+body payload to the backend; the server enqueues a fan-out
 * message and returns immediately. Actual per-user delivery runs async
 * in the SendSitewidePromoFanout Function.
 *
 * Why raw HttpClient instead of the typed AdminClient: this endpoint
 * is new, NSwag hasn't been regenerated yet (manual step the owner
 * runs). Once regen lands, swap the http.post to
 * `adminClient.adminMarketingClient.sendSitewidePromo(...)` — the body
 * shape is identical.
 */
@Component({
  selector: 'cleansia-admin-sitewide-push-form',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaSectionComponent,
    CleansiaTextInputComponent,
    CleansiaTextareaComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './sitewide-push-form.component.html',
})
export class SitewidePushFormComponent {
  private readonly fb = inject(FormBuilder);
  private readonly http = inject(HttpClient);
  private readonly snackbar = inject(SnackbarService);
  private readonly dialog = inject(DialogService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly apiBaseUrl =
    inject(ADMINAPIBASEURL, { optional: true }) ?? 'http://localhost:5001';

  // Mirror the FluentValidation rules on the backend SendSitewidePromo.Validator.
  // Backend rejects with 400 if these limits are exceeded; the client-side
  // validator just lights up the field early so the admin doesn't submit a
  // doomed payload.
  private static readonly TITLE_MAX = 120;
  private static readonly BODY_MAX = 500;

  readonly submitting = signal(false);

  readonly form = this.fb.group({
    titleEn: this.fb.control<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(SitewidePushFormComponent.TITLE_MAX)],
    }),
    titleCs: this.fb.control<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(SitewidePushFormComponent.TITLE_MAX)],
    }),
    titleSk: this.fb.control<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(SitewidePushFormComponent.TITLE_MAX)],
    }),
    titleUk: this.fb.control<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(SitewidePushFormComponent.TITLE_MAX)],
    }),
    titleRu: this.fb.control<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(SitewidePushFormComponent.TITLE_MAX)],
    }),
    bodyEn: this.fb.control<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(SitewidePushFormComponent.BODY_MAX)],
    }),
    bodyCs: this.fb.control<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(SitewidePushFormComponent.BODY_MAX)],
    }),
    bodySk: this.fb.control<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(SitewidePushFormComponent.BODY_MAX)],
    }),
    bodyUk: this.fb.control<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(SitewidePushFormComponent.BODY_MAX)],
    }),
    bodyRu: this.fb.control<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(SitewidePushFormComponent.BODY_MAX)],
    }),
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.snackbar.showErrorTranslated('pages.sitewide_push.validation_error');
      return;
    }

    // Confirm dialog — once enqueued the fan-out runs unstoppable.
    this.dialog
      .confirmTranslated(
        'pages.sitewide_push.confirm_send',
        'pages.sitewide_push.confirm_title',
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed) => {
        if (confirmed) this.send();
      });
  }

  private send(): void {
    this.submitting.set(true);

    // POST /api/AdminMarketing/send-sitewide-promo — matches the backend
    // route in AdminMarketingController. Body shape mirrors
    // SendSitewidePromo.Command exactly.
    this.http
      .post(
        `${this.apiBaseUrl}/api/AdminMarketing/send-sitewide-promo`,
        this.form.getRawValue(),
        { withCredentials: true },
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.snackbar.showSuccessTranslated('pages.sitewide_push.send_success');
          this.form.reset();
        },
        error: () => {
          this.submitting.set(false);
          this.snackbar.showErrorTranslated('pages.sitewide_push.send_error');
        },
      });
  }
}
