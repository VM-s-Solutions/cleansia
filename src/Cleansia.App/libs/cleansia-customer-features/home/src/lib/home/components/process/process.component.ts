import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

interface ProcessStep {
  number: string;
  nameKey: string;
  descKey: string;
  // Which side of the roadmap the text block sits on — alternates down the page.
  side: 'right' | 'left';
  // 1-based index used by the .cl-roadmap__step--N modifier class.
  modifierIndex: number;
  delayClass: string;
}

@Component({
  selector: 'cleansia-process',
  templateUrl: './process.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
})
export class ProcessComponent {
  readonly steps: ProcessStep[] = [
    {
      number: '01',
      nameKey: 'pages.home.process.step1',
      descKey: 'pages.home.process.step1_desc',
      side: 'right',
      modifierIndex: 1,
      delayClass: 'delay-1',
    },
    {
      number: '02',
      nameKey: 'pages.home.process.step2',
      descKey: 'pages.home.process.step2_desc',
      side: 'left',
      modifierIndex: 2,
      delayClass: 'delay-2',
    },
    {
      number: '03',
      nameKey: 'pages.home.process.step3',
      descKey: 'pages.home.process.step3_desc',
      side: 'right',
      modifierIndex: 3,
      delayClass: 'delay-3',
    },
    {
      number: '04',
      nameKey: 'pages.home.process.step4',
      descKey: 'pages.home.process.step4_desc',
      side: 'left',
      modifierIndex: 4,
      delayClass: 'delay-4',
    },
    {
      number: '05',
      nameKey: 'pages.home.process.step5',
      descKey: 'pages.home.process.step5_desc',
      side: 'right',
      modifierIndex: 5,
      delayClass: 'delay-5',
    },
    {
      number: '06',
      nameKey: 'pages.home.process.step6',
      descKey: 'pages.home.process.step6_desc',
      side: 'left',
      modifierIndex: 6,
      delayClass: 'delay-6',
    },
  ];
}
