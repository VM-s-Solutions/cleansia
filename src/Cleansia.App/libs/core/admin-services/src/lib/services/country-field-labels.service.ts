import { Injectable, inject, signal } from '@angular/core';
import { catchError, of } from 'rxjs';
import { AdminClient } from '../client/admin-base-client';
import { GetCountryFieldLabelsCountryFieldLabelsDto } from '../client/admin-client';

/**
 * What a country calls its business identifiers.
 *
 * The admin console showed the Czech "IČO" to an admin looking at a Polish or Ukrainian company,
 * because the label was baked into the translation files — it described the app's language rather
 * than the company's country, which is the one thing it is not.
 *
 * Two callers today, which is why this is a service rather than the inline loader the partner
 * facade uses: employee detail resolves the labels for the employee's country, and the company-info
 * form re-resolves them every time the admin changes the country select.
 *
 * The API method is `fieldLabels`, not the partner client's `getFieldLabels` — the admin route is
 * kebab-case to match its sibling actions on that controller and NSwag names the method after the
 * route. Cosmetic, and not worth a second owner-run regeneration to align.
 */
@Injectable({ providedIn: 'root' })
export class CountryFieldLabelsService {
  private readonly adminClient = inject(AdminClient);

  /** `null` means "no configuration" AND "we could not ask" — both fall back to neutral wording. */
  readonly labels = signal<GetCountryFieldLabelsCountryFieldLabelsDto | null>(null);

  /** The country the current value describes, so a re-entry for the same country costs nothing. */
  private loadedFor: string | null = null;

  /**
   * Silent on failure, deliberately. A country with no configuration row is a 404, which is a
   * normal answer and not an error worth a banner: the caller falls back to its own neutral label,
   * and reporting a missing configuration as a broken form would be worse than saying nothing.
   */
  load(countryId: string | null | undefined): void {
    if (!countryId) {
      this.loadedFor = null;
      this.labels.set(null);
      return;
    }
    if (this.loadedFor === countryId) {
      return;
    }

    this.loadedFor = countryId;
    this.adminClient.adminCountryClient
      .fieldLabels(countryId)
      .pipe(catchError(() => of(null)))
      .subscribe((labels) => {
        // Only publish if the caller has not moved on to another country since this went out.
        if (this.loadedFor === countryId) {
          this.labels.set(labels);
        }
      });
  }
}
