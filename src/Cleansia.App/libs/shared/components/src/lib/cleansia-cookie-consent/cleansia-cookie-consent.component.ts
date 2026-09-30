import { isPlatformBrowser } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  OnInit,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

/**
 * A notice, not a consent form: the apps store only what they need to work, so there is nothing
 * to accept or refuse, and categories that guard nothing would themselves mislead.
 */
@Component({
  selector: 'cleansia-cookie-consent',
  standalone: true,
  imports: [TranslateModule],
  templateUrl: './cleansia-cookie-consent.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaCookieConsentComponent implements OnInit {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  storageKey = input<string>('cleansia-cookie-consent');
  policyUrl = input<string>('');
  position = input<'bottom' | 'top'>('bottom');
  // Still bound by the admin and partner shells; it changes nothing and goes once they drop it.
  showDeclineButton = input<boolean>(false);

  readonly isVisible = signal(false);

  ngOnInit(): void {
    if (!this.isBrowser) return;
    // Any stored value counts as seen, including the accept and decline answers of the old banner.
    this.isVisible.set(localStorage.getItem(this.storageKey()) === null);
  }

  acknowledge(): void {
    if (this.isBrowser) localStorage.setItem(this.storageKey(), 'acknowledged');
    this.isVisible.set(false);
  }
}
