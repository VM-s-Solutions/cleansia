import { Injector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, UrlTree } from '@angular/router';
import { PermissionService, Policy } from '@cleansia/services';
import { permissionGuard } from './permission.guard';

describe('permissionGuard', () => {
  let hasPolicy: jest.Mock;

  function run(data: Record<string, unknown>) {
    TestBed.configureTestingModule({
      providers: [
        { provide: PermissionService, useValue: { hasPolicy } },
        {
          provide: Router,
          useValue: {
            createUrlTree: jest.fn((commands: unknown[]) => ({ commands }) as unknown as UrlTree),
          },
        },
      ],
    });
    return runInInjectionContext(TestBed.inject(Injector), () =>
      permissionGuard({ data } as unknown as ActivatedRouteSnapshot, null as never)
    );
  }

  beforeEach(() => {
    hasPolicy = jest.fn();
  });

  it('admits a route whose permission the session holds', () => {
    hasPolicy.mockReturnValue(true);

    expect(run({ permission: Policy.CanViewRevenueReport })).toBe(true);
    expect(hasPolicy).toHaveBeenCalledWith(Policy.CanViewRevenueReport);
  });

  it('lands a route whose permission the session lacks on /unauthorized', () => {
    hasPolicy.mockReturnValue(false);

    expect(run({ permission: Policy.CanViewRevenueReport })).toEqual({
      commands: ['/unauthorized'],
    });
  });

  it('passes a route that names no permission without asking', () => {
    expect(run({})).toBe(true);
    expect(hasPolicy).not.toHaveBeenCalled();
  });
});
