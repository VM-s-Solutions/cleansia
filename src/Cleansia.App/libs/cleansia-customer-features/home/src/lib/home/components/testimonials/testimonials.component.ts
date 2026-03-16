import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'cleansia-testimonials',
  templateUrl: './testimonials.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
})
export class TestimonialsComponent {
  testimonials = [
    { text: 'pages.home.testimonials.t1.text', author: 'pages.home.testimonials.t1.author', role: 'pages.home.testimonials.t1.role', avatar: 'https://randomuser.me/api/portraits/women/44.jpg' },
    { text: 'pages.home.testimonials.t2.text', author: 'pages.home.testimonials.t2.author', role: 'pages.home.testimonials.t2.role', avatar: 'https://randomuser.me/api/portraits/men/32.jpg' },
    { text: 'pages.home.testimonials.t3.text', author: 'pages.home.testimonials.t3.author', role: 'pages.home.testimonials.t3.role', avatar: 'https://randomuser.me/api/portraits/women/68.jpg' },
  ];
}
