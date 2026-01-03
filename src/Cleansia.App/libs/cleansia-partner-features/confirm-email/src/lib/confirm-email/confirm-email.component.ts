import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  CleansiaBrandNameComponent,
  CleansiaButtonComponent,
  CleansiaDynamicBackgroundComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { selectLoading } from '@cleansia/partner-stores';
import { Store } from '@ngrx/store';
import { TranslateModule } from '@ngx-translate/core';
import { ConfirmEmailFacade } from './confirm-email.facade';

@Component({
  selector: 'cleansia-partner-confirm-email',
  templateUrl: './confirm-email.component.html',
  standalone: true,
  imports: [
    CommonModule,
    TranslateModule,
    ReactiveFormsModule,
    CleansiaTitleComponent,
    CleansiaButtonComponent,
    CleansiaBrandNameComponent,
    CleansiaTextInputComponent,
    CleansiaLanguageSwitcherComponent,
    CleansiaDynamicBackgroundComponent,
  ],
  providers: [ConfirmEmailFacade],
})
export class ConfirmEmailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  protected readonly facade = inject(ConfirmEmailFacade);

  protected readonly loading = toSignal(this.store.select(selectLoading));

  email!: string;

  get resendCodeTimeout(): string {
    return `00:${
      this.facade.resendCodeTimeout > 9
        ? this.facade.resendCodeTimeout
        : '0' + this.facade.resendCodeTimeout
    }`;
  }

  ngOnInit(): void {
    if (!this.route.snapshot.queryParamMap.get('email')) {
      // this.router.navigate([CleansiaPartnerRoute.HOME]);
      return;
    }

    this.email = this.route.snapshot.queryParamMap.get('email') || '';
  }

  onResendCode(): void {
    this.facade.resendCode(this.email);
  }

  onVerifyCode(): void {
    this.facade.confirmEmail();
  }
}
