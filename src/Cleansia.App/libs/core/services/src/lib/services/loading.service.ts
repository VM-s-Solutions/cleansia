import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class LoadingService {
  private readonly _isLoading = new BehaviorSubject<boolean>(false);
  private loadingCounter = 0;

  isLoading$ = this._isLoading.asObservable();

  show(): void {
    this.loadingCounter++;
    if (this.loadingCounter === 1) {
      this._isLoading.next(true);
    }
  }

  hide(): void {
    this.loadingCounter = Math.max(0, this.loadingCounter - 1);
    if (this.loadingCounter === 0) {
      this._isLoading.next(false);
    }
  }

  get isLoading(): boolean {
    return this._isLoading.value;
  }
}