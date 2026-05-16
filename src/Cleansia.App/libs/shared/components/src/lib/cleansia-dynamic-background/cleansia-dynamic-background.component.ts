import { NgStyle } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, OnInit } from '@angular/core';

interface FloatingIcon {
  iconClass: string;
  style: Record<string, string>;
  animationName: string;
}

@Component({
  selector: 'cleansia-dynamic-background',
  templateUrl: './cleansia-dynamic-background.component.html',
  standalone: true,
  imports: [NgStyle],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaDynamicBackgroundComponent implements OnInit {
  showIcons = input(true);
  iconCount = input(20);

  floatingIcons: FloatingIcon[] = [];

  private readonly iconClasses = [
    'pi-sparkles',
    'pi-star',
    'pi-sun',
    'pi-heart',
    'pi-home',
    'pi-bolt',
    'pi-cloud',
    'pi-shield',
    'pi-check-circle',
    'pi-thumbs-up',
  ];

  ngOnInit(): void {
    if (!this.showIcons()) return;
    this.floatingIcons = Array.from({ length: this.iconCount() }, (_, i) => this.createIcon(i));
  }

  private createIcon(index: number): FloatingIcon {
    const icon = this.iconClasses[index % this.iconClasses.length];
    const size = 1 + Math.random() * 1.5; // 1rem – 2.5rem
    const duration = 25 + Math.random() * 35; // 25s – 60s
    const delay = -(Math.random() * duration); // negative delay for staggering
    const startX = Math.random() * 100;
    const startY = Math.random() * 100;
    const opacity = 0.08 + Math.random() * 0.15; // 0.08 – 0.23

    return {
      iconClass: `pi ${icon}`,
      animationName: `float-${index}`,
      style: {
        'font-size': `${size}rem`,
        'left': `${startX}%`,
        'top': `${startY}%`,
        'opacity': `${opacity}`,
        'animation-duration': `${duration}s`,
        'animation-delay': `${delay}s`,
        '--drift-x': `${-50 + Math.random() * 100}vw`,
        '--drift-y': `${-50 + Math.random() * 100}vh`,
        '--rotate': `${-180 + Math.random() * 360}deg`,
      },
    };
  }
}
