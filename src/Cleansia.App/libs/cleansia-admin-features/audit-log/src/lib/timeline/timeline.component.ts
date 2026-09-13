import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  effect,
  inject,
  input,
  OnDestroy,
  TemplateRef,
  untracked,
  viewChild,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TimelineEntryDto } from '@cleansia/admin-services';
import {
  CleansiaLoaderComponent,
  CleansiaTableComponent,
  PaginationState,
  TableAction,
  TableColumn,
} from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import {
  formatResource,
  getOutcomeClass,
  getOutcomeLabelKey,
} from '../audit-log/audit-log.models';
import { TimelineFacade } from './timeline.facade';
import {
  buildTimelineActorRoute,
  buildTimelineEntryRoute,
  getTimelineSourceClass,
  getTimelineSourceLabelKey,
  getTimelineTableDefinition,
} from './timeline.models';

@Component({
  selector: 'cleansia-admin-audit-timeline',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaLoaderComponent,
  ],
  templateUrl: './timeline.component.html',
  providers: [TimelineFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TimelineComponent implements AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(TimelineFacade);

  readonly userId = input<string | null>(null);
  readonly resourceType = input<string | null>(null);
  readonly resourceId = input<string | null>(null);
  readonly resourceLinks = input<boolean>(true);

  readonly sourceTemplate = viewChild<TemplateRef<TimelineEntryDto>>('sourceTemplate');
  readonly actorTemplate = viewChild<TemplateRef<TimelineEntryDto>>('actorTemplate');
  readonly resourceTemplate = viewChild<TemplateRef<TimelineEntryDto>>('resourceTemplate');
  readonly outcomeTemplate = viewChild<TemplateRef<TimelineEntryDto>>('outcomeTemplate');

  timelineColumns!: TableColumn<TimelineEntryDto>[];
  timelineActions!: TableAction<TimelineEntryDto>[];

  private readonly destroy$ = new Subject<void>();

  constructor() {
    effect(() => {
      const userId = this.userId();
      const resourceType = this.resourceType();
      const resourceId = this.resourceId();
      // Only the three inputs may re-run this: the facade reads its own paging signals while it
      // loads, and tracking those would restart from page one on every page click.
      untracked(() => {
        if (userId) {
          this.facade.loadForUser(userId);
        } else if (resourceType && resourceId) {
          this.facade.loadForResource(resourceType, resourceId);
        }
      });
    });
  }

  ngAfterViewInit(): void {
    this.rebuildTableDefinitions();
    this.cd.detectChanges();

    this.translate.onLangChange.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.rebuildTableDefinitions();
      this.cd.detectChanges();
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private rebuildTableDefinitions(): void {
    const definition = getTimelineTableDefinition(
      { onView: (entry) => this.viewEntry(entry) },
      this.translate,
      {
        source: this.sourceTemplate(),
        // A customer's own page names the person in its title; only a resource history needs the column.
        actor: this.userId() ? undefined : this.actorTemplate(),
        resource: this.resourceTemplate(),
        outcome: this.outcomeTemplate(),
      }
    );
    this.timelineColumns = definition.columns;
    this.timelineActions = definition.actions;
  }

  viewEntry(entry: TimelineEntryDto): void {
    const route = buildTimelineEntryRoute(entry);
    if (route) {
      this.router.navigate(route);
    }
  }

  onPageChange(event: PaginationState): void {
    this.facade.onPageChange(event.first, event.rows);
  }

  getSourceClass(entry: TimelineEntryDto): string {
    return getTimelineSourceClass(entry.source);
  }

  getSourceLabelKey(entry: TimelineEntryDto): string {
    return getTimelineSourceLabelKey(entry.source);
  }

  getOutcomeClass(entry: TimelineEntryDto): string {
    return getOutcomeClass(entry.success);
  }

  getOutcomeLabelKey(entry: TimelineEntryDto): string {
    return getOutcomeLabelKey(entry.success);
  }

  formatResource(entry: TimelineEntryDto): string {
    return formatResource(entry);
  }

  actorRoute(entry: TimelineEntryDto): string[] | null {
    return buildTimelineActorRoute(entry);
  }

  resourceHistoryRoute(entry: TimelineEntryDto): (string | CleansiaAdminRoute)[] | null {
    if (!this.resourceLinks() || !entry.resourceType || !entry.resourceId) return null;
    return [CleansiaAdminRoute.AUDIT_LOG, 'resource', entry.resourceType, entry.resourceId];
  }
}
