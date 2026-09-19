import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaSectionComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import { TimelineComponent } from '../timeline/timeline.component';

@Component({
  selector: 'cleansia-admin-audit-resource-history',
  standalone: true,
  imports: [
    CommonModule,
    CleansiaButtonComponent,
    TranslatePipe,
    CleansiaTitleComponent,
    CleansiaSectionComponent,
    TimelineComponent,
  ],
  templateUrl: './resource-history.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourceHistoryComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly resourceType = signal<string>('');
  readonly resourceId = signal<string>('');

  private readonly destroy$ = new Subject<void>();

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe((params) => {
      this.resourceType.set(params.get('resourceType') ?? '');
      this.resourceId.set(params.get('resourceId') ?? '');
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  goBack(): void {
    this.router.navigate([CleansiaAdminRoute.AUDIT_LOG]);
  }
}
