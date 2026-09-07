import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  OnDestroy,
  OnInit,
  PLATFORM_ID,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import {
  loadCustomerPackages,
  loadCustomerServices,
} from '@cleansia/customer-stores';
import { Store } from '@ngrx/store';
import { CleansiaScrollTopComponent } from '@cleansia/components/cleansia-scroll-top';
import { TranslatePipe } from '@ngx-translate/core';

import { HeroComponent } from './components/hero/hero.component';
import { FeaturesComponent } from './components/features/features.component';
import { ServicesComponent } from './components/services/services.component';
import { GalleryComponent } from './components/gallery/gallery.component';
import { RulesComponent } from './components/rules/rules.component';
import { PlusComponent } from './components/plus/plus.component';
import { FaqComponent } from './components/faq/faq.component';
import { CtaComponent } from './components/cta/cta.component';

@Component({
  selector: 'cleansia-home',
  templateUrl: './home.component.html',
  standalone: true,
  imports: [HeroComponent, FeaturesComponent, ServicesComponent, GalleryComponent, RulesComponent, PlusComponent, FaqComponent, CtaComponent, CleansiaScrollTopComponent, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomeComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly el = inject(ElementRef);
  private readonly store = inject(Store);
  private readonly platformId = inject(PLATFORM_ID);

  private observer?: IntersectionObserver;
  private mutationObserver?: MutationObserver;

  ngOnInit(): void {
    this.store.dispatch(loadCustomerServices());
    this.store.dispatch(loadCustomerPackages());
  }

  ngAfterViewInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.setupScrollAnimations();
    }
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
    this.mutationObserver?.disconnect();
  }

  private setupScrollAnimations(): void {
    this.observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add('section-visible');
          }
        });
      },
      { threshold: 0.08, rootMargin: '0px 0px -40px 0px' }
    );

    this.observeAll();

    this.mutationObserver = new MutationObserver(() => {
      this.observeAll();
    });
    this.mutationObserver.observe(this.el.nativeElement, {
      childList: true,
      subtree: true,
    });
  }

  private observeAll(): void {
    const elements = this.el.nativeElement.querySelectorAll(
      '.animate-on-scroll:not(.section-visible):not(.anim-pending)'
    );
    const viewportBottom = window.innerHeight;
    elements.forEach((el: Element) => {
      // Only elements below the fold start hidden — above-the-fold content
      // stays visible so the server-rendered paint is never blanked out.
      if (el.getBoundingClientRect().top > viewportBottom) {
        el.classList.add('anim-pending');
        this.observer!.observe(el);
      } else {
        el.classList.add('section-visible');
      }
    });
  }
}
