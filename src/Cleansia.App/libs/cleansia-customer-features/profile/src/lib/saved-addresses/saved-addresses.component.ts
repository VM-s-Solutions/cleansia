import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
} from '@angular/core';
import { Router } from '@angular/router';
import { CleansiaButtonComponent } from '@cleansia/components';
import { SavedAddressDto } from '@cleansia/customer-services';
import { CleansiaCustomerRoute, DialogService } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { SkeletonModule } from 'primeng/skeleton';
import { take } from 'rxjs';
import { SavedAddressesFacade } from './saved-addresses.facade';

@Component({
  selector: 'cleansia-customer-saved-addresses',
  standalone: true,
  imports: [CommonModule, TranslatePipe, SkeletonModule, CleansiaButtonComponent],
  templateUrl: './saved-addresses.component.html',
  providers: [SavedAddressesFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SavedAddressesComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly dialogService = inject(DialogService);
  protected readonly facade = inject(SavedAddressesFacade);

  ngOnInit(): void {
    this.facade.load();
  }

  setDefault(address: SavedAddressDto): void {
    if (!address.id) return;
    void this.facade.setDefault(address.id);
  }

  delete(address: SavedAddressDto): void {
    if (!address.id) return;
    const id = address.id;
    this.dialogService
      .confirmTranslated('pages.saved_addresses.delete_confirm', undefined, {
        label: address.label ?? '',
      })
      .pipe(take(1))
      .subscribe((confirmed) => {
        if (confirmed) {
          void this.facade.delete(id);
        }
      });
  }

  goToProfileAddresses(): void {
    void this.router.navigate([CleansiaCustomerRoute.PROFILE]);
  }
}
