import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'cleansia-partner-gdpr',
  standalone: true,
  imports: [
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaTitleComponent,
    RouterModule,
  ],
  template: `
    <div class="cleansia-gdpr">
      <div class="cleansia-gdpr__card page-wrapper">
        <cleansia-title [title]="'pages.gdpr.title' | translate" />
        <p class="cleansia-gdpr__subtitle">
          {{ 'pages.gdpr.subtitle' | translate }}
        </p>

        <div class="cleansia-gdpr__section">
          <h3>{{ 'pages.gdpr.data_collection_title' | translate }}</h3>
          <p>{{ 'pages.gdpr.data_collection_text' | translate }}</p>
        </div>

        <div class="cleansia-gdpr__section">
          <h3>{{ 'pages.gdpr.data_usage_title' | translate }}</h3>
          <p>{{ 'pages.gdpr.data_usage_text' | translate }}</p>
        </div>

        <div class="cleansia-gdpr__section">
          <h3>{{ 'pages.gdpr.data_rights_title' | translate }}</h3>
          <p>{{ 'pages.gdpr.data_rights_text' | translate }}</p>
        </div>

        <div class="cleansia-gdpr__section">
          <h3>{{ 'pages.gdpr.data_retention_title' | translate }}</h3>
          <p>{{ 'pages.gdpr.data_retention_text' | translate }}</p>
        </div>

        <div class="cleansia-gdpr__section">
          <h3>{{ 'pages.gdpr.contact_title' | translate }}</h3>
          <p>{{ 'pages.gdpr.contact_text' | translate }}</p>
        </div>

        <div class="cleansia-gdpr__actions">
          <cleansia-button
            [title]="'global.actions.go_back' | translate"
            severity="secondary"
            [outlined]="true"
            routerLink="/profile"
          />
        </div>
      </div>
    </div>
  `,
  styles: [
    `
      .cleansia-gdpr {
        display: flex;
        justify-content: center;
        padding: 1rem;

        &__card {
          width: 100%;
          max-width: 1200px;
        }
        &__subtitle {
          color: var(--text-color-secondary);
          margin-bottom: 2rem;
          font-size: 0.95rem;
        }
        &__section {
          margin-bottom: 2rem;
          padding-bottom: 1.5rem;
          border-bottom: 1px solid var(--surface-border, #e5e7eb);
          &:last-of-type {
            border-bottom: none;
          }
          h3 {
            font-size: 1.1rem;
            font-weight: 600;
            margin-bottom: 0.75rem;
            color: var(--text-color);
          }
          p {
            color: var(--text-color-secondary);
            line-height: 1.75;
            margin: 0;
          }
        }
        &__actions {
          margin-top: 2rem;
        }
      }
    `,
  ],
})
export class PartnerGdprComponent {}
