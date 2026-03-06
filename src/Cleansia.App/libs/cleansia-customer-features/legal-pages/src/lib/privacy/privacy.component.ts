import { Component } from '@angular/core';
import { CleansiaTitleComponent } from '@cleansia/components';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'cleansia-customer-privacy',
  standalone: true,
  imports: [TranslateModule, CleansiaTitleComponent],
  template: `
    <div class="legal-page max-w-4xl mx-auto px-4 py-8 md:px-6 lg:px-8">
      <cleansia-title [title]="'privacy_page.title' | translate" />
      <p class="text-600 mb-5">{{ 'privacy_page.intro' | translate }}</p>

      @for (i of sections; track i) {
        <div class="mb-5">
          <h2 class="text-xl font-semibold text-900 mb-2">{{ 'privacy_page.section' + i + '_title' | translate }}</h2>
          <p class="text-700 line-height-3">{{ 'privacy_page.section' + i + '_text' | translate }}</p>
        </div>
      }
    </div>
  `,
})
export class PrivacyComponent {
  sections = [1, 2, 3, 4, 5, 6];
}
