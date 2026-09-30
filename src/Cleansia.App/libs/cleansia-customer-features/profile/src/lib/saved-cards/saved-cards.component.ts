import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { CleansiaButtonComponent } from '@cleansia/components';
import { DialogService } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { SkeletonModule } from 'primeng/skeleton';
import { take } from 'rxjs';
import { SavedCardsFacade } from './saved-cards.facade';
import { CARD_SETUP_QUERY_PARAM, SavedCardRow } from './saved-cards.models';

@Component({
  selector: 'cleansia-customer-saved-cards',
  standalone: true,
  imports: [TranslatePipe, SkeletonModule, CleansiaButtonComponent],
  templateUrl: './saved-cards.component.html',
  providers: [SavedCardsFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SavedCardsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly dialogService = inject(DialogService);
  protected readonly facade = inject(SavedCardsFacade);

  ngOnInit(): void {
    this.facade.init(this.route.snapshot.queryParamMap.get(CARD_SETUP_QUERY_PARAM));
  }

  remove(card: SavedCardRow): void {
    this.dialogService
      .confirmTranslated(
        'pages.profile.saved_cards.remove_confirm',
        undefined,
        { last4: card.last4 },
        { danger: true },
      )
      .pipe(take(1))
      .subscribe((confirmed) => {
        if (confirmed) this.facade.remove(card.id);
      });
  }
}
