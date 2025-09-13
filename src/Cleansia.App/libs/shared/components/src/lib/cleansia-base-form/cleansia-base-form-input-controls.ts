import { InputSignal } from '@angular/core';
import { NgControl } from '@angular/forms';

export abstract class CleansiaBaseInputControls<T> {
  value?: T | null;

  readonly ngControl!: NgControl | null;

  readonly label!: InputSignal<string | undefined>;

  readonly required!: InputSignal<boolean>;

  readonly disabled!: boolean;
}
