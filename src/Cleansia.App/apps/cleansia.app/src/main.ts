import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app';
import { appConfig } from './app/app.config';

bootstrapApplication(AppComponent, appConfig).catch((err) =>
  console.error(err)
);

// Scroll between fullscreen sections
let isScrolling = false;

function scrollToSection(next: boolean = true): void {
  if (isScrolling) return;
  isScrolling = true;

  const sections: HTMLElement[] = Array.from(document.querySelectorAll('.fullscreen-section'));
  const offsetTops = sections.map((s) => Math.abs(s.getBoundingClientRect().top));
  const currentIdx = offsetTops.indexOf(Math.min(...offsetTops));

  let targetIdx = next ? currentIdx + 1 : currentIdx - 1;
  targetIdx = Math.max(0, Math.min(targetIdx, sections.length - 1));

  if (targetIdx !== currentIdx) {
    sections[targetIdx].scrollIntoView({ behavior: 'smooth' });
    setTimeout(() => { isScrolling = false; }, 400);
  } else {
    isScrolling = false;
  }
}

document.addEventListener('wheel', (event: WheelEvent) => {
  if (Math.abs(event.deltaY) < 10) return;
  event.preventDefault();
  scrollToSection(event.deltaY > 0);
}, { passive: false });