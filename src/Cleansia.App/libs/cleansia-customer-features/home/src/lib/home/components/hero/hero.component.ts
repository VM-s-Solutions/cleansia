import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterModule } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';

const HERO_IMAGE = 'assets/images/mascot/mascot-mopping.webp';
const PRELOAD_ID = 'cl-hero-img-preload';

@Component({
  selector: 'cleansia-hero',
  templateUrl: './hero.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterModule, TranslatePipe, ButtonModule],
})
export class HeroComponent {
  private readonly document = inject(DOCUMENT);

  constructor() {
    // Preload the LCP hero image from <head>. Running this during SSR puts
    // the hint into the served HTML, so the browser fetches the image ahead
    // of the script bundles instead of competing with them.
    if (!this.document.getElementById(PRELOAD_ID)) {
      const link = this.document.createElement('link');
      link.id = PRELOAD_ID;
      link.rel = 'preload';
      link.setAttribute('as', 'image');
      link.href = HERO_IMAGE;
      // Mirror the <img srcset/sizes> so the preload fetches the same
      // variant the responsive image will pick.
      link.setAttribute(
        'imagesrcset',
        'assets/images/mascot/mascot-mopping-480.webp 480w, assets/images/mascot/mascot-mopping.webp 800w'
      );
      link.setAttribute('imagesizes', '(max-width: 768px) 260px, 400px');
      link.setAttribute('fetchpriority', 'high');
      this.document.head.appendChild(link);
    }
  }
}
