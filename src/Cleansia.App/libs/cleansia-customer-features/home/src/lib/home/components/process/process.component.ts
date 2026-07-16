import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

interface ProcessStep {
  number: string;
  nameKey: string;
  descKey: string;
}

@Component({
  selector: 'cleansia-process',
  templateUrl: './process.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
})
export class ProcessComponent {
  // Two horizontal tracks: steps the customer takes, then steps we take.
  readonly youSteps: ProcessStep[] = [
    { number: '1', nameKey: 'pages.home.process.step1', descKey: 'pages.home.process.step1_desc' },
    { number: '2', nameKey: 'pages.home.process.step2', descKey: 'pages.home.process.step2_desc' },
    { number: '3', nameKey: 'pages.home.process.step3', descKey: 'pages.home.process.step3_desc' },
  ];

  readonly weSteps: ProcessStep[] = [
    { number: '4', nameKey: 'pages.home.process.step4', descKey: 'pages.home.process.step4_desc' },
    { number: '5', nameKey: 'pages.home.process.step5', descKey: 'pages.home.process.step5_desc' },
    { number: '6', nameKey: 'pages.home.process.step6', descKey: 'pages.home.process.step6_desc' },
  ];
}
