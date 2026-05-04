import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  OnDestroy,
  OnInit,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  MapboxAddressSuggestion,
  MapboxAutocompleteService,
} from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { AutoCompleteCompleteEvent, AutoCompleteModule } from 'primeng/autocomplete';
import { catchError, of, Subject, takeUntil } from 'rxjs';

/**
 * Text-only Mapbox autocomplete. NO map canvas — search box + dropdown.
 * Emits the picked suggestion so the parent can populate street/city/zip + lat/lng.
 *
 * Renders nothing if Mapbox isn't configured (no token), letting the parent
 * fall back to plain manual inputs without any visual noise.
 */
@Component({
  selector: 'cleansia-address-autocomplete',
  standalone: true,
  imports: [CommonModule, FormsModule, AutoCompleteModule, TranslatePipe],
  templateUrl: './cleansia-address-autocomplete.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaAddressAutocompleteComponent implements OnInit, OnDestroy {
  private readonly mapbox = inject(MapboxAutocompleteService);
  private readonly destroy$ = new Subject<void>();

  /** Optional initial input value (e.g., when editing an existing address). */
  initialQuery = input<string>('');

  /** Emitted once the user picks a suggestion from the dropdown. */
  readonly picked = output<MapboxAddressSuggestion>();

  /** Emitted on every error (network / 4xx / parse) so the parent can warn. */
  readonly searchFailed = output<void>();

  readonly suggestions = signal<MapboxAddressSuggestion[]>([]);
  readonly searching = signal(false);
  readonly errored = signal(false);
  readonly query = signal<string>('');

  // p-autoComplete uses two-way `[(ngModel)]`. We keep an internal mirror of
  // whatever the user typed (string) OR the picked object.
  model: string | MapboxAddressSuggestion = '';

  get isMapboxConfigured(): boolean {
    return this.mapbox.isConfigured;
  }

  ngOnInit(): void {
    const initial = this.initialQuery();
    if (initial) this.model = initial;
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /** PrimeNG fires this on every keystroke (after its own internal debounce). */
  onComplete(event: AutoCompleteCompleteEvent): void {
    const q = (event.query ?? '').trim();
    this.query.set(q);
    if (!q) {
      this.suggestions.set([]);
      return;
    }
    this.searching.set(true);
    this.errored.set(false);
    this.mapbox
      .search(q)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => {
          this.errored.set(true);
          this.searchFailed.emit();
          return of([] as MapboxAddressSuggestion[]);
        })
      )
      .subscribe((results) => {
        this.suggestions.set(results);
        this.searching.set(false);
      });
  }

  /** Fired when the user clicks/keys into a suggestion. */
  onSelect(event: { value: MapboxAddressSuggestion | string }): void {
    const v = event.value;
    if (typeof v === 'string') return;
    this.picked.emit(v);
    // Show the formatted place name in the input so it's clear what was picked.
    this.model = v.placeName;
  }

}
