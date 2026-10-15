import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { resolve, join } from 'node:path';
import { performance } from 'node:perf_hooks';
import { pathToFileURL } from 'node:url';

const options = Object.fromEntries(process.argv.slice(2).map((arg) => {
  const split = arg.indexOf('=');
  return [arg.slice(2, split), arg.slice(split + 1)];
}));
assert(options.artifact && options.output, 'Pass --artifact=<optimized app directory> --output=<evidence directory>');
const artifact = resolve(options.artifact);
const output = resolve(options.output);
mkdirSync(output, { recursive: true });
const marker = 'SSR_RECOVERED_SERVICE';
const apiOrigin = options['api-origin'] ?? 'https://customer-api.dev.cleansia.cz';
const paths = {
  market: '/api/Market/GetOverview', service: '/api/Service/GetOverview',
  package: '/api/Package/GetOverview', currency: '/api/Currency/GetOverview',
  plans: '/api/Membership/GetPlans',
};
const fixtures = {
  [paths.market]: [{ countryId: '11111111-1111-1111-1111-111111111111', isoCode: 'CZ', isoAlpha2: 'CZ', name: 'Czechia', isDefault: true, currencyCode: 'CZK', currencySymbol: 'Kč', translations: {} }],
  [paths.service]: [{ id: '22222222-2222-2222-2222-222222222222', name: marker, description: 'Local smoke fixture', category: { id: '33333333-3333-3333-3333-333333333333', name: 'Cleaning' }, basePrice: 900, perRoomPrice: 10, currencyCode: 'CZK', translations: {} }],
  [paths.package]: [],
  [paths.currency]: [{ id: '44444444-4444-4444-4444-444444444444', code: 'CZK', symbol: 'Kč', name: 'Czech koruna', isDefault: true }],
  [paths.plans]: [{ code: 'monthly', name: 'Monthly', price: 199, monthlyEquivalentPrice: 199, billingInterval: 1, discountPercentage: 5, freeCancellationWindowHours: 4, allowsExpressUpgrade: false, expressUpgradesPerMonth: 0, trialPeriodDays: 14, savingsPercentVsMonthly: 0, currencyCode: 'CZK' }],
};
const preload = join(output, 'fetch-preload.mjs');
writeFileSync(preload, `import { appendFileSync } from 'node:fs';
const nativeFetch = globalThis.fetch;
const expected = new URL(process.env.SSR_TEST_API_ORIGIN);
const stub = new URL(process.env.SSR_TEST_STUB_ORIGIN);
const record = (event) => appendFileSync(process.env.SSR_TEST_EVENTS, JSON.stringify({ ...event, t: performance.now() }) + '\\n');
globalThis.fetch = (input, init) => {
  const url = new URL(typeof input === 'string' || input instanceof URL ? input : input.url);
  if (url.origin !== expected.origin) throw new Error('Unexpected smoke outbound origin: ' + url.origin);
  const signal = init?.signal;
  record({ type: 'fetch', path: url.pathname });
  signal?.addEventListener('abort', () => record({ type: 'abort', path: url.pathname, reason: signal.reason?.name }), { once: true });
  url.protocol = stub.protocol;
  url.host = stub.host;
  return nativeFetch(url, init);
};
`);

function sleep(ms) { return new Promise((resolve) => setTimeout(resolve, ms)); }
async function listen(server) {
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  return server.address().port;
}
async function unusedPort() {
  const server = createServer();
  const port = await listen(server);
  await new Promise((resolve) => server.close(resolve));
  return port;
}
function hasSsr(html) {
  assert.match(html, /ng-server-context="ssr"/, 'SSR marker missing');
  assert.match(html, /<app-root[^>]*>[\s\S]*<cleansia-home/, 'populated home root missing');
}
function hasService(html) {
  assert.match(html, /<h3[^>]*class="[^"]*cl-services__name[^"]*"[^>]*>\s*SSR_RECOVERED_SERVICE\s*<\/h3>/, 'recovered service missing from rendered card');
}

async function runCase(name, fault = 'healthy') {
  const started = performance.now();
  const active = new Set();
  const requests = [];
  const closes = [];
  let mode = fault;
  const fixture = createServer((req, res) => {
    const url = new URL(req.url, 'http://fixture');
    const path = url.pathname;
    const record = { path, startedMs: performance.now() - started };
    requests.push(record);
    active.add(res);
    res.once('close', () => {
      active.delete(res);
      closes.push({ path, ended: res.writableEnded, elapsedMs: performance.now() - started });
    });
    const hung = mode === 'all' || mode === path || (mode === 'body' && path === paths.market);
    if (hung) {
      if (mode === 'body') {
        res.writeHead(200, { 'content-type': 'application/json' });
        res.write('[');
      }
      return;
    }
    if (mode === 'reset') { req.socket.destroy(); return; }
    setTimeout(() => {
      if (res.destroyed) return;
      res.writeHead(mode === '500' ? 500 : mode === '429' ? 429 : 200, {
        'content-type': 'application/json', ...(mode === '429' ? { 'retry-after': '60' } : {}),
      });
      const legal = path === '/api/Legal/GetDocument' ? {
        type: Number(url.searchParams.get('type')), countryId: fixtures[paths.market][0].countryId,
        version: '2026-10-15', effectiveFrom: '2026-10-15T00:00:00Z', language: url.searchParams.get('language') ?? 'en',
        title: url.searchParams.get('type') === '1' ? 'SSR_PRIVACY_DOCUMENT' : 'SSR_TERMS_DOCUMENT',
        contentHtml: '<h2>1. Local legal fixture</h2><p>SSR_LEGAL_BODY</p>', contentHash: 'local-fixture',
      } : undefined;
      res.end(JSON.stringify(mode === 'empty' || mode === '500' || mode === '429' ? [] : legal ?? fixtures[path] ?? []));
    }, 25);
  });
  const fixturePort = await listen(fixture);
  const port = await unusedPort();
  const eventsFile = join(output, `${name}.events.jsonl`);
  writeFileSync(eventsFile, '');
  const child = spawn(process.execPath, ['--import', pathToFileURL(preload).href, join(artifact, 'server/server.mjs')], {
    cwd: artifact,
    env: { ...process.env, PORT: String(port), NG_ALLOWED_HOSTS: 'localhost,127.0.0.1', SSR_TEST_API_ORIGIN: apiOrigin, SSR_TEST_STUB_ORIGIN: `http://127.0.0.1:${fixturePort}`, SSR_TEST_EVENTS: eventsFile },
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  let logs = '';
  let listenMs;
  const collect = (chunk) => {
    logs += chunk.toString();
    if (listenMs === undefined && logs.includes('Node Express server listening')) listenMs = performance.now() - started;
  };
  child.stdout.on('data', collect);
  child.stderr.on('data', collect);
  const metrics = { name, fault, node: process.version };
  const request = async (path, cookie = false, timeout = 4000, language = 'en') => {
    const begin = performance.now();
    const response = await fetch(`http://127.0.0.1:${port}${path}`, {
      headers: { 'accept-language': language, ...(cookie ? { cookie: 'ssr-smoke=1' } : {}) },
      signal: AbortSignal.timeout(timeout),
    });
    const headerMs = performance.now() - begin;
    const body = await response.text();
    return { status: response.status, body, cacheControl: response.headers.get('cache-control'),
      frameOptions: response.headers.get('x-frame-options'), csp: response.headers.get('content-security-policy'),
      headerMs, completeMs: performance.now() - begin };
  };
  try {
    const deadline = performance.now() + 2000;
    while (listenMs === undefined && performance.now() < deadline) {
      if (child.exitCode !== null) throw new Error(`SSR child exited ${child.exitCode}: ${logs}`);
      await sleep(10);
    }
    assert(listenMs !== undefined, 'SSR child did not listen within 2000ms');
    metrics.listenMs = listenMs;
    const health = await request('/health', false, 1000);
    assert.equal(health.status, 200);
    metrics.healthMs = health.completeMs;
    assert(health.completeMs < 250, 'health exceeded 250ms after listen');
    const rootPromise = request('/');
    await sleep(100);
    const duringHealth = await request('/health', false, 1000);
    assert(duringHealth.completeMs < 250, 'health stalled during SSR');
    metrics.duringHealthMs = duringHealth.completeMs;
    const root = await rootPromise;
    let lastFallbackCompletedAt = performance.now();
    metrics.rootMs = root.completeMs;
    metrics.rootHeaderMs = root.headerMs;
    metrics.cacheControl = root.cacheControl;
    metrics.bodySha256 = createHash('sha256').update(root.body).digest('hex');
    assert.equal(root.status, 200);
    hasSsr(root.body);
    assert(root.completeMs < 4000, 'root exceeded 4000ms');
    if (fault === 'healthy') {
      hasService(root.body);
      const before = requests.length;
      const cached = await request('/');
      hasService(cached.body);
      assert.equal(requests.length, before, 'healthy anonymous root missed landing cache');
      metrics.cachedMs = cached.completeMs;
      const warm = await request('/', true);
      hasSsr(warm.body);
      hasService(warm.body);
      assert(requests.length > before, 'cookie root did not bypass landing cache');
      metrics.warmMs = warm.completeMs;
    }
    if (['all', 'body', ...Object.values(paths)].includes(fault)) {
      assert.match(root.cacheControl ?? '', /no-store/i, 'timeout root must be no-store');
      await sleep(50);
      assert.equal(active.size, 0, 'outbound fixture request remains active after deadline');
      if (name === 'repeated') {
        metrics.repeatedMs = [];
        for (let attempt = 0; attempt < 2; attempt++) {
          const next = await request('/', true);
          lastFallbackCompletedAt = performance.now();
          hasSsr(next.body);
          assert.match(next.cacheControl ?? '', /no-store/i);
          metrics.repeatedMs.push(next.completeMs);
          await sleep(50);
          assert.equal(active.size, 0, 'repeated render left fixture transport active');
          const health = await request('/health', false, 1000);
          assert(health.completeMs < 250, 'health stalled after repeated deadlines');
        }
      }
      const events = readFileSync(eventsFile, 'utf8').trim().split('\n').filter(Boolean).map(JSON.parse);
      const aborts = events.filter((event) => event.type === 'abort' && event.reason === 'TimeoutError');
      const stalled = requests.filter(({ path }) => fault === 'all' || path === (fault === 'body' ? paths.market : fault));
      assert.equal(aborts.length, stalled.length, 'deadline abort count differs from stalled requests');
      assert(aborts.length > 0, 'native fetch signal never received TimeoutError');
      for (const abort of aborts) {
        const request = events.findLast((event) => event.type === 'fetch' && event.path === abort.path && event.t < abort.t);
        assert(request, 'deadline abort has no matching fetch');
        abort.elapsedMs = abort.t - request.t;
        assert(abort.elapsedMs >= 1400 && abort.elapsedMs < 2500, 'native deadline missed the calibrated timer window');
      }
      metrics.timeoutAborts = aborts;
      mode = 'healthy';
      const before = requests.length;
      metrics.recoveryInitiationGapMs = performance.now() - lastFallbackCompletedAt;
      assert(metrics.recoveryInitiationGapMs < 1000, 'recovery request was not issued within one second');
      const recovered = await request('/');
      hasSsr(recovered.body);
      hasService(recovered.body);
      assert(requests.length > before, 'recovery root replayed cached fallback');
      assert(!/no-store/i.test(recovered.cacheControl ?? ''), 'timeout state leaked into later render');
      metrics.recoveryMs = recovered.completeMs;
      assert(metrics.recoveryInitiationGapMs + recovered.completeMs < 60000, 'recovery waited for the landing cache TTL');
      const afterRecovery = requests.length;
      const cached = await request('/');
      hasService(cached.body);
      assert.equal(requests.length, afterRecovery, 'healthy recovery was not re-admitted to cache');
      const uncached = await request('/', true);
      hasSsr(uncached.body);
      hasService(uncached.body);
      assert(requests.length > afterRecovery, 'cookie recovery did not bypass the cache');
      metrics.recoveryCachedMs = cached.completeMs;
      metrics.recoveryCookieMs = uncached.completeMs;
    }
    if (fault === 'empty') {
      assert(!/no-store/i.test(root.cacheControl ?? ''), 'valid [] was marked as timeout');
      const before = requests.length;
      await request('/');
      assert.equal(requests.length, before, 'valid [] render was not cached');
    }
    if (['500', '429', 'reset'].includes(fault)) {
      const events = readFileSync(eventsFile, 'utf8').trim().split('\n').filter(Boolean).map(JSON.parse);
      assert.equal(events.filter((event) => event.type === 'abort' && event.reason === 'TimeoutError').length, 0,
        'ordinary failure was classified as a native timeout');
      metrics.ordinaryFailureCacheControl = root.cacheControl;
    }
    if (name === 'deep-links') {
      metrics.deepLinks = [];
      for (const path of ['/terms', '/privacy', '/services']) {
        const page = await request(path, false, 4000, 'cs');
        assert.equal(page.status, 200);
        assert.equal(page.frameOptions, 'DENY');
        assert.equal(page.csp, "frame-ancestors 'none'");
        assert(!page.body.includes('<cleansia-home'), 'deep link rendered the home page');
        assert.match(page.body, /ng-server-context="ssr"/);
        if (path === '/services') {
          assert.match(page.body, /<h3[^>]*class="[^"]*cl-cat-svc__name[^"]*"[^>]*>\s*SSR_RECOVERED_SERVICE\s*<\/h3>/);
        } else {
          const title = path === '/privacy' ? 'SSR_PRIVACY_DOCUMENT' : 'SSR_TERMS_DOCUMENT';
          assert.match(page.body, new RegExp('<h1[^>]*>\\s*' + title + '\\s*</h1>'));
          assert(page.body.includes('SSR_LEGAL_BODY'));
        }
        metrics.deepLinks.push({ path, status: page.status, completeMs: page.completeMs });
      }
      const health = await request('/health', false, 1000);
      assert(health.completeMs < 250, 'health stalled after deep links');
      assert.equal(active.size, 0, 'deep link left fixture transport active');
      metrics.deepHealthMs = health.completeMs;
    }
    metrics.pass = true;
  } catch (error) {
    metrics.pass = false;
    metrics.error = error.stack;
  } finally {
    metrics.activeBeforeChildStop = active.size;
    metrics.requests = requests;
    metrics.closes = closes;
    child.kill('SIGTERM');
    await Promise.race([new Promise((resolve) => child.once('exit', resolve)), sleep(1000)]);
    if (child.exitCode === null && child.signalCode === null) child.kill('SIGKILL');
    fixture.closeAllConnections();
    await new Promise((resolve) => fixture.close(resolve));
    metrics.activeAfterCleanup = active.size;
    metrics.totalMs = performance.now() - started;
    writeFileSync(join(output, `${name}.child.log`), logs);
    writeFileSync(join(output, `${name}.json`), JSON.stringify(metrics, null, 2) + '\n');
  }
  console.log(JSON.stringify({ name, pass: metrics.pass, listenMs: metrics.listenMs, rootMs: metrics.rootMs, error: metrics.error?.split('\n')[0] }));
  return metrics;
}

const selected = options.cases?.split(',') ?? ['healthy-1', 'market', 'service'];
const results = [];
for (const name of selected) {
  const fault = name.startsWith('healthy') ? 'healthy' : name === 'repeated' ? paths.service : paths[name] ?? name;
  results.push(await runCase(name, fault));
}
writeFileSync(join(output, 'summary.json'), JSON.stringify({ artifact, apiOrigin, node: process.version, results }, null, 2) + '\n');
process.exitCode = results.every((result) => result.pass) ? 0 : 1;
