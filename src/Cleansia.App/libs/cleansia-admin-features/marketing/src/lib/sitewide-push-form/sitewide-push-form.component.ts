import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { SendSitewidePromoCommand } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaSectionComponent,
  CleansiaTextInputComponent,
  CleansiaTextareaComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { SnackbarService } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { SitewidePushFormFacade } from './sitewide-push-form.facade';

@Component({
  selector: 'cleansia-admin-sitewide-push-form',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [SitewidePushFormFacade],
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
  private readonly snackbar = inject(SnackbarService);
  protected readonly facade = inject(SitewidePushFormFacade);

  private static readonly TITLE_MAX = 120;
  private static readonly BODY_MAX = 500;

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

  get submitting() {
    return this.facade.submitting;
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.snackbar.showErrorTranslated('pages.sitewide_push.validation_error');
      return;
    }

    const command = new SendSitewidePromoCommand(this.form.getRawValue());
    this.facade.submit(command, () => this.form.reset());
  }
}
