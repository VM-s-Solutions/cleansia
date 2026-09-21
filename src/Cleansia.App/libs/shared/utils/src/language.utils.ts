import { Signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslateService } from '@ngx-translate/core';
import { map } from 'rxjs';

/**
 * The session's language as a signal, so a table definition or a chip label computed from
 * `translate.instant(...)` re-derives when the user switches language. Call it from a field
 * initializer or a constructor: it subscribes for the owner's lifetime.
 */
export function currentLanguage(translate: TranslateService): Signal<string> {
  return toSignal(translate.onLangChange.pipe(map((event) => event.lang)), {
    initialValue: translate.currentLang,
  });
}
