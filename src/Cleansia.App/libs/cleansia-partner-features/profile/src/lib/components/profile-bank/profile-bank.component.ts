import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  Input,
  OnInit,
} from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import {
  CleansiaBankAccountComponent,
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTextInputComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { ProfileBankFacade } from '../../profile/profile-bank.facade';

@Component({
  selector: 'cleansia-partner-profile-bank',
  standalone: true,
  imports: [
    CleansiaLoaderComponent,
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaSectionComponent,
    CleansiaBankAccountComponent,
    CleansiaTextInputComponent,
    CleansiaSelectComponent,
    CleansiaButtonComponent,
  ],
  templateUrl: './profile-bank.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileBankComponent implements OnInit {
  @Input({ required: true }) facade!: ProfileBankFacade;

  ngOnInit(): void {
    this.facade.load();
  }
}
