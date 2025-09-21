import { AfterViewInit, Component, ElementRef, ViewChild } from '@angular/core';
import { RouterModule } from '@angular/router';
import { SnackbarService } from '@cleansia/services';
import { MenubarModule } from 'primeng/menubar'; // p-menubar
import { ButtonModule } from 'primeng/button'; // pButton
import { CardModule, Card } from 'primeng/card'; // p-card
import { StepsModule } from 'primeng/steps'; // p-steps
import { CarouselModule, Carousel } from 'primeng/carousel'; // p-carousel
import { AccordionModule, Accordion, AccordionTab } from 'primeng/accordion'; // p-accordion / p-accordionTab
import { InputTextModule } from 'primeng/inputtext'; // pInputText
import { AvatarModule, Avatar } from 'primeng/avatar';
import { NgFor } from '@angular/common';
import { fromEvent, debounceTime } from 'rxjs';

@Component({
  selector: 'cleansia-home',
  templateUrl: './cleansia.html',
  styleUrls: ['./cleansia.scss'],
  standalone: true,
  providers: [],
  imports: [Accordion, Avatar, Carousel, Card,
    RouterModule,
    ButtonModule,
    CardModule,
    MenubarModule,
    StepsModule,
    CarouselModule,
    AccordionModule,
    InputTextModule,
    NgFor,
    AvatarModule]
})
export class CleansiaComponent implements AfterViewInit
{
  navItems = [
    { label: 'Úvod', routerLink: '/home' },
    { label: 'Služby', routerLink: '/services' },
    { label: 'FAQ', routerLink: '/faq' },
  ];

  processSteps = [
    { label: 'Objednávka', icon: 'pi pi-check' },
    { label: 'Příprava', icon: 'pi pi-cog' },
    { label: 'Čištění', icon: 'pi pi-brush' },
    { label: 'Kontrola', icon: 'pi pi-eye' },
    { label: 'Dokončení', icon: 'pi pi-thumbs-up' },
  ];

  beforeAfterImages = [1, 2, 3, 4, 5, 6]; // Replace with image URLs if needed

  testimonials = [
    { text: 'Skvělá práce! Nábytek jako nový...', author: 'Jan Novák' },
    { text: 'Rychlé a profesionální služby.', author: 'Eva Svobodová' },
    { text: 'Doporučuji všem!', author: 'Petr Dvořák' },
  ];

  faqs = [
    {
      question: 'Jak dlouho trvá čištění?',
      answer: 'Čištění trvá 2-4 hodiny.',
    },
    {
      question: 'Používáte bezpečné prostředky?',
      answer: 'Ano, pouze certifikované prostředky.',
    },
  ];
  @ViewChild('counter', { static: true }) counterRef!: ElementRef;

  targetCount = 2733;
  displayedCount = 0;
  isVisible = false;
  buttonDownVisible = true;
  buttonUpVisible = false;

  sections: HTMLElement[] = [];
  currentSectionIndex = 0;

  constructor(private snackbarService: SnackbarService) {
    // Lazy loading for images AND background images
document.addEventListener('DOMContentLoaded', () => {
  let lazyloadElements: NodeListOf<HTMLElement>;

  if ('IntersectionObserver' in window) {
    lazyloadElements = document.querySelectorAll<HTMLElement>('.lazy');

    const observer = new IntersectionObserver((entries, obs) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          const el = entry.target as HTMLElement;

          // Handle <img> tags
          if (el.tagName.toLowerCase() === 'img') {
            const img = el as HTMLImageElement;
            const dataSrc = img.dataset['src'];
            if (dataSrc) {
              img.src = dataSrc;
            }
          }

          // Handle elements with background images
          const dataBg = el.dataset['bg'];
          if (dataBg) {
            el.style.backgroundImage = `url('${dataBg}')`;
          }

          el.classList.remove('lazy');
          obs.unobserve(el);
        }
      });
    });

    lazyloadElements.forEach((el) => {
      observer.observe(el);
    });

  } else {
    // Fallback for older browsers
    lazyloadElements = document.querySelectorAll<HTMLElement>('.lazy');
    let lazyloadThrottleTimeout: number;

    function lazyload() {
      if (lazyloadThrottleTimeout) {
        clearTimeout(lazyloadThrottleTimeout);
      }

      lazyloadThrottleTimeout = window.setTimeout(() => {
        const scrollTop = window.pageYOffset;

        lazyloadElements.forEach((el) => {
          if (el.offsetTop < (window.innerHeight + scrollTop)) {
            // Handle <img> tags
            if (el.tagName.toLowerCase() === 'img') {
              const img = el as HTMLImageElement;
              const dataSrc = img.dataset['src'];
              if (dataSrc) {
                img.src = dataSrc;
              }
            }

            // Handle background images
            const dataBg = el.dataset['bg'];
            if (dataBg) {
              el.style.backgroundImage = `url('${dataBg}')`;
            }

            el.classList.remove('lazy');
          }
        });

        // Stop observing if everything is loaded
        if (document.querySelectorAll('.lazy').length === 0) {
          document.removeEventListener('scroll', lazyload);
          window.removeEventListener('resize', lazyload);
          window.removeEventListener('orientationChange', lazyload);
        }
      }, 20);
    }

    document.addEventListener('scroll', lazyload);
    (window as Window & typeof globalThis).addEventListener('resize', lazyload);
    (window as Window & typeof globalThis).addEventListener('orientationChange', lazyload);
  }
});

  }

  ngAfterViewInit(): void {
    this.sections = Array.from(
      document.querySelectorAll('.fullscreen-section')
    ) as HTMLElement[];

    fromEvent(window, 'scroll')
      .pipe(debounceTime(100))
      .subscribe(() => {
        this.checkVisibility();
      });
  }

  checkVisibility(): void {
    if (!this.isVisible) {
      this.isVisible = true;
      this.startCounter();
    } else {
      this.isVisible = false;
      this.displayedCount = 0;
    }
  }

  startCounter(): void {
    const stepTime = 10;
    const increment = Math.ceil(this.targetCount / 100);

    const interval = setInterval(() => {
      this.displayedCount += increment;
      if (this.displayedCount >= this.targetCount) {
        this.displayedCount = this.targetCount;
        clearInterval(interval);
      }
    }, stepTime);
  }

  submitRequest(form: any) {
    this.snackbarService.showSuccessTranslated('global.messages.form.request_sent');
    form.reset();
  }

  scrollToNext() {
    const nextIndex = Math.min(
      this.currentSectionIndex + 1,
      this.sections.length - 1
    );
    if (this.sections[nextIndex]) {
      this.sections[nextIndex].scrollIntoView({ behavior: 'smooth' });
      this.currentSectionIndex = nextIndex; // Update index after scrolling
      console.log('Scrolled to next section:', nextIndex); // Debug log
      this.buttonUpVisible = true;
    }
    if (this.currentSectionIndex + 1 > this.sections.length - 1) {
      this.buttonDownVisible = false;
    }
  }

  scrollToPrev() {
    const prevIndex = Math.max(this.currentSectionIndex - 1, 0);
    if (this.sections[prevIndex]) {
      this.sections[prevIndex].scrollIntoView({ behavior: 'smooth' });
      this.currentSectionIndex = prevIndex; // Update index after scrolling
      console.log('Scrolled to previous section:', prevIndex); // Debug log
      this.buttonDownVisible = true;
    }
    if (this.currentSectionIndex - 1 < 0) {
      this.buttonUpVisible = false;
    }
  }
}
