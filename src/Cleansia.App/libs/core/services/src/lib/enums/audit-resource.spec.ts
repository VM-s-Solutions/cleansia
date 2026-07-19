import { AuditResourceType, buildAuditResourceHistoryRoute } from './audit-resource';
import { CleansiaAdminRoute } from './routes.enum';

describe('buildAuditResourceHistoryRoute', () => {
  it('builds the per-resource history route for the User resource type', () => {
    expect(
      buildAuditResourceHistoryRoute(AuditResourceType.User, 'user-1')
    ).toEqual([CleansiaAdminRoute.AUDIT_LOG, 'resource', 'User', 'user-1']);
  });

  it('keeps the resource type literal aligned with what the backend records', () => {
    expect(AuditResourceType.User).toBe('User');
  });
});
