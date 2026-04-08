import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { LanguageFormData, LanguageFormFacade } from './language-form.facade';

@Component({
  selector: 'cleansia-admin-language-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaTextInputComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './language-form.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [LanguageFormFacade],
})
export class LanguageFormComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(LanguageFormFacade);

  private readonly mode = signal<'create' | 'edit'>('create');

  readonly isEditMode = computed(() => this.mode() === 'edit');
  readonly pageTitle = computed(() =>
    this.isEditMode()
      ? this.translate.instant('pages.language_form.edit_title')
      : this.translate.instant('pages.language_form.create_title')
  );

  readonly form = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(10)]],
    name: ['', [Validators.required, Validators.maxLength(50)]],
  });

  private languageLoadEffect = effect(() => {
    const language = this.facade.language();
    if (language && this.isEditMode()) {
      this.populateForm(language);
    }
  });

  ngOnInit(): void {
    const routeMode = this.route.snapshot.data['mode'] as 'create' | 'edit';
    if (routeMode) {
      this.mode.set(routeMode);
    }

    if (this.isEditMode()) {
      const languageId = this.route.snapshot.paramMap.get('languageId');
      if (languageId) {
        this.facade.loadLanguage(languageId);
      } else {
        this.router.navigate([CleansiaAdminRoute.LANGUAGE_MANAGEMENT]);
      }
    }
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
  }

  private populateForm(language: { code?: string; name?: string }): void {
    this.form.patchValue({
      code: language.code ?? '',
      name: language.name ?? '',
    });

    // Disable code field in edit mode (code should not be editable)
    if (this.isEditMode()) {
      this.form.get('code')?.disable();
    }
  }

  onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const formValue = this.form.getRawValue();

    const data: LanguageFormData = {
      code: formValue.code,
      name: formValue.name,
    };

    if (this.isEditMode()) {
      const languageId = this.route.snapshot.paramMap.get('languageId');
      if (languageId) {
        this.facade.updateLanguage(languageId, data);
      }
    } else {
      this.facade.createLanguage(data);
    }
  }

  onCancel(): void {
    this.facade.navigateBack();
  }
}