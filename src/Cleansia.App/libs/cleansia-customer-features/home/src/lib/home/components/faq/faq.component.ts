import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AccordionModule } from 'primeng/accordion';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'cleansia-faq',
  templateUrl: './faq.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  // AccordionModule re-exports Accordion / AccordionPanel / AccordionHeader /
  // AccordionContent — keep the umbrella import. Importing the standalone
  // sub-components directly broke HMR replaceMetadata in PrimeNG 19 (saw
  // "Cannot read properties of undefined (reading 'ButtonDirective')" on
  // hot-reload because of an evaluation-order issue with sibling modules).
  imports: [RouterModule, TranslatePipe, AccordionModule, ButtonModule],
})
export class FaqComponent {
  faqs = [
    { question: 'pages.home.faq.q1.question', answer: 'pages.home.faq.q1.answer' },
    { question: 'pages.home.faq.q2.question', answer: 'pages.home.faq.q2.answer' },
    { question: 'pages.home.faq.q3.question', answer: 'pages.home.faq.q3.answer' },
    { question: 'pages.home.faq.q4.question', answer: 'pages.home.faq.q4.answer' },
    { question: 'pages.home.faq.q5.question', answer: 'pages.home.faq.q5.answer' },
    { question: 'pages.home.faq.q6.question', answer: 'pages.home.faq.q6.answer' },
  ];
}
