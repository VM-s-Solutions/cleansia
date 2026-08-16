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
  CleansiaFormSkeletonComponent,
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
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaSectionComponent,
    CleansiaBankAccountComponent,
    CleansiaTextInputComponent,
    CleansiaSelectComponent,
    CleansiaButtonComponent,
    CleansiaFormSkeletonComponent,
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
