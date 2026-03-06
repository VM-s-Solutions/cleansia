import {
  AfterViewInit,
  Component,
  ElementRef,
  inject,
  OnDestroy,
  OnInit,
} from '@angular/core';
import {
  loadCustomerPackages,
  loadCustomerServices,
} from '@cleansia/customer-stores';
import { Store } from '@ngrx/store';
import { CleansiaScrollTopComponent } from '@cleansia/components';

import { FloatingBgComponent } from './components/floating-bg/floating-bg.component';
import { HeroComponent } from './components/hero/hero.component';
import { FeaturesComponent } from './components/features/features.component';
import { ProcessComponent } from './components/process/process.component';
import { BenefitsComponent } from './components/benefits/benefits.component';
import { ServicesComponent } from './components/services/services.component';
import { GalleryComponent } from './components/gallery/gallery.component';
import { TestimonialsComponent } from './components/testimonials/testimonials.component';
import { FaqComponent } from './components/faq/faq.component';
import { CtaComponent } from './components/cta/cta.component';
import { LandingFooterComponent } from './components/landing-footer/landing-footer.component';

@Component({
  selector: 'cleansia-home',
  templateUrl: './home.component.html',
  standalone: true,
  imports: [
    FloatingBgComponent,
    HeroComponent,
    FeaturesComponent,
    ProcessComponent,
    BenefitsComponent,
    ServicesComponent,
    GalleryComponent,
    TestimonialsComponent,
    FaqComponent,
    CtaComponent,
    LandingFooterComponent,
    CleansiaScrollTopComponent,
  ],
})
export class HomeComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly el = inject(ElementRef);
  private readonly store = inject(Store);

  private observer?: IntersectionObserver;
  private mutationObserver?: MutationObserver;

  ngOnInit(): void {
    this.store.dispatch(loadCustomerServices());
    this.store.dispatch(loadCustomerPackages());
  }

  ngAfterViewInit(): void {
    this.setupScrollAnimations();
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
    const elements = this.el.nativeElement.querySelectorAll('.animate-on-scroll:not(.section-visible)');
    elements.forEach((el: Element) => this.observer!.observe(el));
  }
}
