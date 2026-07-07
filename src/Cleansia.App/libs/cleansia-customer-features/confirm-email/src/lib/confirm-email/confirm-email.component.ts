import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import {
  CleansiaBrandNameComponent,
  CleansiaButtonComponent,
  CleansiaCodeInputComponent,
  CleansiaDynamicBackgroundComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { selectCustomerLoading } from '@cleansia/customer-stores';
import { Store } from '@ngrx/store';
import { TranslatePipe } from '@ngx-translate/core';
import { ConfirmEmailFacade } from './confirm-email.facade';

@Component({
  selector: 'cleansia-customer-confirm-email',
  templateUrl: './confirm-email.component.html',
  standalone: true,
  imports: [
    CommonModule,
    TranslatePipe,
    ReactiveFormsModule,
    CleansiaTitleComponent,
    CleansiaButtonComponent,
    CleansiaBrandNameComponent,
    CleansiaCodeInputComponent,
    CleansiaDynamicBackgroundComponent,
    CleansiaTextInputComponent,
  ],
  providers: [ConfirmEmailFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmEmailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly store = inject(Store);
  protected readonly facade = inject(ConfirmEmailFacade);

  protected readonly loading = toSignal(this.store.select(selectCustomerLoading));

  protected readonly resendCodeTimeout = computed(() => {
    const t = this.facade.resendCodeTimeout();
    return `00:${t > 9 ? t : '0' + t}`;
  });

  ngOnInit(): void {
    const email = this.route.snapshot.queryParamMap.get('email');
    if (email) {
      this.facade.setEmail(email);
    }
  }

  onResendCode(): void {
    this.facade.resendCode();
  }

  onVerifyCode(): void {
    this.facade.confirmEmail();
  }
}
