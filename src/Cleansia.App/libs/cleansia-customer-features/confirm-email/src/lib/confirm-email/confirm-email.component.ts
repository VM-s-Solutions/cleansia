import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  CleansiaBrandNameComponent,
  CleansiaButtonComponent,
  CleansiaCodeInputComponent,
  CleansiaDynamicBackgroundComponent,
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
  ],
  providers: [ConfirmEmailFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmEmailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  protected readonly facade = inject(ConfirmEmailFacade);

  protected readonly loading = toSignal(this.store.select(selectCustomerLoading));

  protected readonly resendCodeTimeout = computed(() => {
    const t = this.facade.resendCodeTimeout();
    return `00:${t > 9 ? t : '0' + t}`;
  });

  email!: string;

  ngOnInit(): void {
    if (!this.route.snapshot.queryParamMap.get('email')) {
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
