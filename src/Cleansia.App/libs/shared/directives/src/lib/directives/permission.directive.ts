import {
  Directive,
  Input,
  TemplateRef,
  ViewContainerRef,
  inject,
} from '@angular/core';
import { PermissionService, PolicyName } from '@cleansia/services';

/**
 * Structural directive that hides its host element when the current user
 * does NOT satisfy the given Policy. Defense-in-depth UI gating that
 * mirrors the backend `[Permission]` attribute.
 *
 * Usage:
 *   <button *cleansiaPermission="Policy.CanApproveEmployee" (click)="approve()">
 *     Approve
 *   </button>
 *
 *   <!-- Multiple policies: passes if ANY of them passes -->
 *   <button *cleansiaPermission="[Policy.CanApproveInvoice, Policy.CanCancelInvoice]">…</button>
 *
 *   <!-- Custom else-template (e.g. show a tooltip placeholder) -->
 *   <div *cleansiaPermission="Policy.CanResolveDispute; else readOnly">…</div>
 *   <ng-template #readOnly>Read-only view</ng-template>
 *
 * Note: this is a render-time gate. It does NOT subscribe to login state;
 * Angular re-evaluates structural inputs on change detection, which is
 * sufficient for typical SPA flows (login/logout always trigger CD via
 * router navigation or auth-service signals).
 */
@Directive({
  selector: '[cleansiaPermission]',
  standalone: true,
})
export class CleansiaPermissionDirective {
  private readonly templateRef = inject(TemplateRef<unknown>);
  private readonly viewContainer = inject(ViewContainerRef);
  private readonly permissions = inject(PermissionService);

  private elseTemplate: TemplateRef<unknown> | null = null;
  private hasView = false;

  @Input()
  set cleansiaPermission(policy: PolicyName | string | (PolicyName | string)[]) {
    const policies = Array.isArray(policy) ? policy : [policy];
    const allowed = policies.some((p) => this.permissions.hasPolicy(p));
    this.render(allowed);
  }

  @Input()
  set cleansiaPermissionElse(template: TemplateRef<unknown> | null) {
    this.elseTemplate = template;
    // Re-render to reflect the new else branch when the gate is currently denying.
    if (!this.hasView && this.elseTemplate) {
      this.viewContainer.clear();
      this.viewContainer.createEmbeddedView(this.elseTemplate);
    }
  }

  private render(allowed: boolean): void {
    if (allowed && !this.hasView) {
      this.viewContainer.clear();
      this.viewContainer.createEmbeddedView(this.templateRef);
      this.hasView = true;
    } else if (!allowed && this.hasView) {
      this.viewContainer.clear();
      this.hasView = false;
      if (this.elseTemplate) {
        this.viewContainer.createEmbeddedView(this.elseTemplate);
      }
    } else if (!allowed && !this.hasView && this.elseTemplate) {
      // Initial render path with an else template
      this.viewContainer.createEmbeddedView(this.elseTemplate);
    }
  }
}
