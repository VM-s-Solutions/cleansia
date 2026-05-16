import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { DialogModule } from 'primeng/dialog';
import { FloatLabelModule } from 'primeng/floatlabel';
import { InputTextModule } from 'primeng/inputtext';
import { CleansiaButtonComponent } from '../cleansia-button';

/**
 * Discriminated state shape both promo and referral dialogs accept. The
 * dialog is intentionally generic — the parent maps domain-specific error
 * codes (PromoCodeError / ReferralValidationError) into a localized
 * `errorMessage` string before handing it back. Same for `successMessage`,
 * which the dialog renders verbatim on the green path.
 */
export type CodeDialogResult =
  | { kind: 'idle' }
  | { kind: 'validating' }
  | { kind: 'valid'; successMessage: string }
  | { kind: 'invalid'; errorMessage: string };

/**
 * Wolt-style "enter a code" dialog. Reused by promo + referral entries on
 * the booking summary step and the signup-form referral entry. Inputs cover
 * label/copy via i18n keys; the parent owns the actual validate call
 * (emitted via `applyClicked`) and gets a callback (`done`) when the user
 * taps Done.
 *
 * Design notes
 * - No placeholder on the input (floating label only — see project conventions).
 * - Apply is the ONLY trigger; we never validate on input change.
 * - When `kind === 'valid'` we swap Cancel+Apply for a single Done button.
 * - We allow dismissing while validating (X / Esc / outside click) — the
 *   in-flight backend call resolves into a state we just discard.
 * - Reopening with a previously-applied code seeds the input with that code
 *   so the user can edit and re-Apply, or Done to keep it.
 */
@Component({
  selector: 'cleansia-code-input-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    FormsModule,
    DialogModule,
    InputTextModule,
    FloatLabelModule,
    CleansiaButtonComponent,
    TranslatePipe,
  ],
  templateUrl: './cleansia-code-input-dialog.component.html',
})
export class CleansiaCodeInputDialogComponent {
  /** Two-way bound visibility — parent toggles via signal model. */
  readonly visible = input.required<boolean>();
  readonly visibleChange = output<boolean>();

  /** Initial code shown when dialog opens. Empty string for fresh entry. */
  readonly initialCode = input<string>('');

  /** External state — parent's facade signal piped in. Drives the message block. */
  readonly state = input.required<CodeDialogResult>();

  /** i18n keys — kept granular so the same component can serve any code domain. */
  readonly titleKey = input.required<string>();
  readonly inputLabelKey = input.required<string>();
  readonly helperKey = input.required<string>();
  readonly cancelKey = input<string>('pages.order.promo.dialog_cancel');
  readonly applyKey = input<string>('pages.order.promo.dialog_apply');
  readonly doneKey = input<string>('pages.order.promo.dialog_done');
  readonly validatingKey = input<string>('pages.order.promo.validating');

  /** Apply tap — parent runs the actual backend validation. */
  readonly applyClicked = output<string>();

  /** Done tap — parent decides what "applied" means (just close, usually). */
  readonly done = output<void>();

  /** Cancel/dismiss — parent may want to wipe state if the user bails. */
  readonly cancelled = output<void>();

  protected readonly codeInput = signal('');

  /** Local mirror of `visible` so [(visible)] on p-dialog has a writable signal. */
  protected readonly dialogVisible = signal(false);

  constructor() {
    // Sync the parent's `visible` input into our local writable signal. When
    // the dialog opens, also seed the input with the parent's `initialCode`
    // (this is how reopening with an applied code preserves user context).
    effect(() => {
      const v = this.visible();
      this.dialogVisible.set(v);
      if (v) {
        this.codeInput.set(this.initialCode() ?? '');
      }
    });

    // Bubble local visibility back out — covers the dialog's own dismiss
    // controls (X button, Esc, outside click) without forcing the parent
    // to listen to (onHide) too.
    effect(() => {
      const local = this.dialogVisible();
      if (local !== this.visible()) {
        this.visibleChange.emit(local);
      }
    });
  }

  protected readonly canApply = computed(() => {
    const code = this.codeInput().trim();
    if (!code) return false;
    const k = this.state().kind;
    return k !== 'validating' && k !== 'valid';
  });

  /** Type-narrow helper because templates can't `as` the union directly. */
  protected readonly asValid = computed(() => {
    const s = this.state();
    return s.kind === 'valid' ? s : { kind: 'valid' as const, successMessage: '' };
  });
  protected readonly asInvalid = computed(() => {
    const s = this.state();
    return s.kind === 'invalid' ? s : { kind: 'invalid' as const, errorMessage: '' };
  });

  onApply(): void {
    if (!this.canApply()) return;
    this.applyClicked.emit(this.codeInput().trim());
  }

  onDone(): void {
    this.done.emit();
    this.dialogVisible.set(false);
  }

  onCancel(): void {
    this.cancelled.emit();
    this.dialogVisible.set(false);
  }

  /**
   * p-dialog (onHide) catches X/Esc/outside-click. We treat all three the
   * same as Cancel — parent decides whether to clear pending state.
   */
  handleHide(): void {
    if (this.visible()) {
      this.visibleChange.emit(false);
    }
  }
}
