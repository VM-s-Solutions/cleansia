export const meta = {
  name: 'wave2-2c-t0168b',
  description: 'T-0168b backend: expose per-package included service IDs on the order-detail DTO so the admin refund UI can build valid bundled-service lines (additive, no break to the string[] consumers)',
  phases: [
    { title: 'Build', detail: 'backend adds IncludedServiceItems [{Id,Name}] to PackageDetails + mapper' },
    { title: 'Review', detail: 'reviewer audits additive-not-breaking + tests' },
  ],
}

const CONTEXT = `
WHY: The admin partial-refund command (IssuePartialRefund, src/Cleansia.Core.AppServices/Features/Refunds/
IssuePartialRefund.cs) takes lines as RefundLineSelection(string ServiceId, string? PackageId):
  - standalone service line = { ServiceId, PackageId: null }
  - bundled service line    = { ServiceId, PackageId }   (a service inside a package)
EVERY line MUST carry a non-empty ServiceId (validator line 53). There is NO "whole package, no service"
shape. To refund a single service bundled inside a package (ADR-0009 D5 — the owner's long-term win), the
admin UI must build { ServiceId, PackageId } — so it needs the SERVICE IDs of each package's included
services. Today the order-detail DTO exposes them only as display-name strings (no IDs), so the UI cannot
build a valid bundled line. This ticket closes that gap on the backend.

THE GAP (read these exact spots):
- DTO: src/Cleansia.Core.AppServices/Features/Packages/DTOs/PackageDetails.cs — IncludedServices is
  IEnumerable<string> (NAMES only).
- Mapper: src/Cleansia.Core.AppServices/Mappers/PackageMappers.cs:29 — MapToDetails populates
  IncludedServices: package.IncludedServices.Select(s => s.Service.Name).
- This PackageDetails is part of the order-detail OrderItem DTO (OrderItem.SelectedPackages, OrderItem.cs:42),
  loaded via LookupOrder (which already does .ThenInclude(p => p.IncludedServices) so Service is materialized
  — VERIFY it ThenIncludes the Service too; if it only includes IncludedServices but not .Service, add the
  ThenInclude so s.Service.Id is loaded without a lazy/null hit).

CRITICAL CONSTRAINT — ADDITIVE, DO NOT BREAK EXISTING CONSUMERS:
PackageDetails.IncludedServices (string[] of names) is consumed by the CUSTOMER app
(services-catalog.component.ts:181-183) and the admin package-form. DO NOT change its type or remove it.
ADD a parallel field instead:
  IncludedServiceItems: IEnumerable<{ string Id, string Name }>   (a small record, e.g. PackageServiceRef)
populated from s.Service.Id + s.Service.Name. Leave IncludedServices exactly as-is. This is a purely additive
DTO change → no existing consumer breaks; the refund UI reads the new IncludedServiceItems for IDs.
`

const RULES = `
RULES (non-negotiable): DTOs are positional record types. Mapper is an extension method in
Cleansia.Core.AppServices/Mappers. No CommitAsync anywhere here (read-path only). No 'any'/dynamic. Comments:
almost none, no task-number refs, keep only load-bearing ADR refs. TEST-FIRST. Do NOT run npm generate /
hand-edit NSwag clients — flag manual_step: nswag-regen (the order-detail/PackageDetails DTO surface changes
for the admin client). NO ef-migration (no schema change — Service.Id already exists). Build src/Cleansia.Api.sln
+ run src/Cleansia.Tests green. Backend only. Evidence fields are POINTERS not artifacts — terse counts +
one-line verdict + key file:line; full logs live in the ticket status log, never in the report.
`

phase('Build')
const dev = await agent(
  `You are the BACKEND developer. Implement T-0168b — expose per-package included service IDs on the
order-detail DTO so the admin refund UI can build valid bundled-service refund lines.

${CONTEXT}
${RULES}

DELIVERABLES:
1. A small record for an included-service reference with { string Id, string Name } (name it sensibly, e.g.
   PackageServiceRef, beside PackageDetails). Reuse an existing equivalent if one already fits — but do NOT
   reuse PackageServiceSummary (it carries Name + Translations, no Id) unless you ADD an Id to it without
   breaking its other consumers; a new tiny record is cleaner.
2. Add IncludedServiceItems: IEnumerable<PackageServiceRef> to PackageDetails (ADDITIVE — keep
   IncludedServices: IEnumerable<string> exactly as-is).
3. PackageMappers.MapToDetails populates IncludedServiceItems from package.IncludedServices.Select(s =>
   new PackageServiceRef(s.Service.Id, s.Service.Name)). Confirm LookupOrder's query .ThenInclude(...).Service
   so s.Service.Id is loaded (add the ThenInclude if missing — check LookupOrder.cs and LookupOrderBatch.cs).
4. TEST-FIRST: a mapper test asserting MapToDetails returns IncludedServiceItems with the correct {Id,Name}
   pairs for a package's included services, AND that IncludedServices (names) is unchanged (so the additive
   change didn't alter the existing field). If there's an existing LookupOrder integration/handler test for
   order detail, extend it to assert the new field is populated end-to-end.
5. Build + run src/Cleansia.Tests green.

Return: files changed, the new record shape, the exact PackageDetails delta (proving it's additive), whether
you had to add a .ThenInclude(...).Service, test names + result, build result, and the manual_step flags
(nswag-regen: admin order-detail DTO; NO ef-migration). State plainly that the customer-app string[]
consumer is untouched.`,
  { label: 'dev:T-0168b', phase: 'Build', agentType: 'backend' },
)

phase('Review')
const review = await agent(
  `You are the REVIEWER for T-0168b (backend — expose bundled service IDs on the order-detail DTO). Audit:
- ADDITIVE-NOT-BREAKING (the critical property): PackageDetails.IncludedServices (IEnumerable<string> names)
  is UNCHANGED in type and still populated; a NEW IncludedServiceItems field carries {Id,Name}. Verify the
  customer services-catalog consumer (services-catalog.component.ts:181-183) and the admin package-form
  consumer still compile against the unchanged string[] field. A type change to IncludedServices is a BLOCKER.
- correctness: IncludedServiceItems IDs are real Service.Id values (not package-service join ids, not names),
  so the refund UI can build RefundLineSelection { ServiceId, PackageId }. The query loads .Service (no null
  ref / lazy load). Trace one package end-to-end.
- the test is non-vacuous: it asserts the {Id,Name} pairs AND that the names field is preserved.
- conventions: record DTO, mapper extension, no CommitAsync, no 'any', comment discipline (no task-number refs).
- manual_step: nswag-regen flagged; NO ef-migration (verify the dev added no migration / entity / EF change).
Read the real files, run the gate (build + the relevant test filter). Verdict: APPROVE / APPROVE-WITH-NITS /
REQUEST-CHANGES with file:line findings.`,
  { label: 'review:T-0168b', phase: 'Review', agentType: 'reviewer' },
)

return { ticket: 'T-0168b', dev, review }
