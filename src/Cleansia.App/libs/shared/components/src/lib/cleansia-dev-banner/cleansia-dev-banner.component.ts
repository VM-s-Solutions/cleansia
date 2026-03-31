import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'cleansia-dev-banner',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (bugReportUrl) {
      <div class="cleansia-dev-banner">
        <span class="cleansia-dev-banner__env">DEV</span>
        <span class="cleansia-dev-banner__text">Testing environment</span>
        <a
          class="cleansia-dev-banner__link"
          [href]="bugReportUrl"
          target="_blank"
          rel="noopener noreferrer"
        >
          <i class="pi pi-file-excel"></i>
          Report a Bug
        </a>
      </div>
    }
  `,
  styles: `
    .cleansia-dev-banner {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.75rem;
      padding: 0.35rem 1rem;
      background: linear-gradient(90deg, #f59e0b, #d97706);
      color: #fff;
      font-size: 0.8rem;
      font-weight: 600;
      z-index: 9999;
      position: relative;
    }

    .cleansia-dev-banner__env {
      background: rgba(0, 0, 0, 0.25);
      padding: 0.1rem 0.5rem;
      border-radius: 4px;
      font-size: 0.7rem;
      letter-spacing: 1px;
      text-transform: uppercase;
    }

    .cleansia-dev-banner__text {
      opacity: 0.9;
    }

    .cleansia-dev-banner__link {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      color: #fff;
      text-decoration: underline;
      cursor: pointer;
      transition: opacity 0.2s;
    }

    .cleansia-dev-banner__link:hover {
      opacity: 0.8;
    }

    .cleansia-dev-banner__link i {
      font-size: 0.85rem;
    }
  `,
})
export class CleansiaDevBannerComponent {
  @Input() bugReportUrl: string | undefined;
}
