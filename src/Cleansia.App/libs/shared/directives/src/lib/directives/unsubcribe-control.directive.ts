import { Directive, OnDestroy } from '@angular/core';
import { Subject } from 'rxjs';

@Directive()
export abstract class UnsubscribeControlDirective implements OnDestroy {
  destroyed$ = new Subject<void>();

  ngOnDestroy() {
    this.destroyed$.next();
    this.destroyed$.complete();
  }
}
