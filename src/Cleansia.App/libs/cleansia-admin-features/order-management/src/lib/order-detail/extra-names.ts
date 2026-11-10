import { AdminClient } from '@cleansia/admin-services';
import { Observable, catchError, map, of } from 'rxjs';

export type ExtraNames = Record<string, string>;

const EXTRAS_CATALOGUE_LIMIT = 100;

export function loadExtraNames(client: AdminClient): Observable<ExtraNames> {
  return client.adminExtraClient
    .getPaged(undefined, undefined, undefined, 0, EXTRAS_CATALOGUE_LIMIT)
    .pipe(
      map((page) =>
        Object.fromEntries(
          (page.data ?? []).filter((e) => e.slug && e.name).map((e) => [e.slug!, e.name!])
        )
      ),
      catchError(() => of({}))
    );
}

export function extraLabel(slug: string, names: ExtraNames): string {
  return names[slug] ?? slug;
}
