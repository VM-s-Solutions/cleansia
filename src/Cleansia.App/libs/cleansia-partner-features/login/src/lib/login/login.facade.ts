import { Injectable } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';

@Injectable()
export class LoginFacade {
  formGroup = this.createFormGroup();

  login() {}

  private createFormGroup(): FormGroup {
    const formGroup = new FormGroup({
      email: new FormControl('', [Validators.required, Validators.email]),
      password: new FormControl('', [
        Validators.required,
        Validators.minLength(6),
      ]),
      rememberMe: new FormControl(false),
    });
    return formGroup;
  }
}
