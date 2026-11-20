#!/usr/bin/env python3
"""Read DEV's Azure Monitor metrics, Resource Health, Activity Log and App Insights over given UTC windows.

Read-only: every Azure call goes through az(), which refuses anything that is not a read.
"""
import argparse
import json
import os
import re
import subprocess
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path

ARM = 'https://management.azure.com'
LOG_QUERY = re.compile(r'https://api\.loganalytics\.io/v1/workspaces/[0-9a-f-]{36}/query')
WINDOW = re.compile(r'(\d{4}-\d{2}-\d{2}T\d{2}:\d{2})Z/(\d{4}-\d{2}-\d{2}T\d{2}:\d{2})Z')
MAX_WINDOWS = 12
MAX_SPAN = timedelta(hours=6)
# Azure Monitor keeps platform metrics 93 days and Resource Health about 30; older windows read empty.
MAX_AGE = timedelta(days=90)
HEALTH_MARGIN = timedelta(minutes=30)

NAME_SOURCES = {
    'plan': ('modules/appServicePlan.bicep', r"var planName = '([^']+)'"),
    'postgres': ('modules/postgres.bicep', r"var postgresServerName = '([^']+)'"),
    'functions': ('modules/functionApp.bicep', r"var functionAppName = '([^']+)'"),
    'workspace': ('modules/appInsights.bicep', r"var workspaceName = '([^']+)'"),
    'ssr': ('main.bicep', r"var ssrSiteName = '([^']+)'"),
    'api': ('main.bicep', r"var apiSiteNames = \[for host in apiHosts: '([^']+)'\]"),
}

METRICS = {
    'serverfarms': ('Microsoft.Web/serverfarms',
                    ['CpuPercentage', 'MemoryPercentage', 'HttpQueueLength', 'DiskQueueLength']),
    'flexibleServers': ('Microsoft.DBforPostgreSQL/flexibleServers',
                        ['cpu_credits_remaining', 'cpu_credits_consumed', 'cpu_percent', 'memory_percent',
                         'active_connections', 'connections_failed', 'disk_iops_consumed_percentage', 'is_db_alive']),
    'sites': ('Microsoft.Web/sites', ['HttpResponseTime', 'Http5xx', 'PrivateBytes', 'Threads']),
}

QUERIES = {
    'slow_requests': """AppRequests
| where TimeGenerated between (datetime({start}) .. datetime({end})) and DurationMs > 20000
| project TimeGenerated, AppRoleName, Name, DurationMs, ResultCode, Success, OperationId
| order by TimeGenerated asc | take 200""",
    'postgres_dependencies': """let slow = AppRequests
| where TimeGenerated between (datetime({start}) .. datetime({end})) and DurationMs > 20000 | project OperationId;
AppDependencies
| where TimeGenerated between (datetime({start}) .. datetime({end}))
| where DependencyType has 'postgres' or Target has 'postgres'
| where OperationId in (slow) or DurationMs > 5000
| project TimeGenerated, AppRoleName, OperationId, Name, Target, DurationMs, Success, ResultCode
| order by TimeGenerated asc | take 500""",
    'npgsql_exceptions': """AppExceptions
| where TimeGenerated between (datetime({start}) .. datetime({end}))
| where ExceptionType has 'Npgsql' or OuterType has 'Npgsql' or InnermostType has 'Npgsql' or InnermostType has 'Timeout'
| summarize Count = count(), First = min(TimeGenerated), Last = max(TimeGenerated)
    by AppRoleName, ExceptionType, InnermostType, InnermostMessage = substring(InnermostMessage, 0, 160)
| order by Count desc | take 50""",
}


def stamp(moment):
    return moment.strftime('%Y-%m-%dT%H:%M:%SZ')


def instant(text):
    return datetime.fromisoformat(text.replace('Z', '+00:00')).astimezone(timezone.utc)


def parse_windows(text, now):
    parts = [part for part in re.split(r'[,\s]+', text.strip()) if part]
    if not 1 <= len(parts) <= MAX_WINDOWS:
        raise ValueError(f'give 1 to {MAX_WINDOWS} windows')
    windows = []
    for part in parts:
        match = WINDOW.fullmatch(part)
        if match is None:
            raise ValueError(f'{part!r} is not YYYY-MM-DDTHH:MMZ/YYYY-MM-DDTHH:MMZ')
        start, end = (datetime.strptime(value, '%Y-%m-%dT%H:%M').replace(tzinfo=timezone.utc)
                      for value in match.groups())
        if not (start < end <= now and end - start <= MAX_SPAN and now - start <= MAX_AGE):
            raise ValueError(f'{part} must end in the past, start within {MAX_AGE.days} days and span at most 6 hours')
        windows.append((start, end))
    return windows


def dev_names(param_file):
    params = dict(re.findall(r"^param (region|env) = '([a-z]+)'", Path(param_file).read_text(), re.M))
    if params.get('env') != 'dev' or 'region' not in params:
        raise ValueError(f'{param_file} is not a DEV parameter file; this workflow reads DEV only')
    bicep = Path(param_file).parent
    main = (bicep / 'main.bicep').read_text()
    block = re.search(r'^var apiHosts = \[\n(.*?)^\]', main, re.M | re.S)
    audiences = re.findall(r"audience: '([a-z-]+)'", block.group(1)) if block else []

    def fill(key, audience=None):
        path, pattern = NAME_SOURCES[key]
        template = re.search(pattern, (bicep / path).read_text()).group(1)
        name = (template.replace('${region}', params['region']).replace('${env}', 'dev')
                .replace('${stage}', 'dev').replace('${host.audience}', audience or ''))
        if '${' in name or not name.endswith('-dev'):
            raise ValueError(f'cannot resolve {template}')
        return name

    if not audiences:
        raise ValueError('main.bicep lists no apiHosts')
    return {'group': f"rg-cleansia-{params['region']}-dev", 'plan': fill('plan'), 'postgres': fill('postgres'),
            'workspace': fill('workspace'),
            'sites': [fill('api', audience) for audience in audiences] + [fill('ssr'), fill('functions')]}


def read_only(args):
    args = list(args)
    if args[:3] in (['monitor', 'metrics', 'list'], ['monitor', 'activity-log', 'list']):
        return True
    if args[:1] != ['rest'] or args.count('--method') != 1 or args.count('--url') != 1:
        return False
    method, url = args[args.index('--method') + 1], args[args.index('--url') + 1]
    return method == 'get' or (method == 'post' and LOG_QUERY.fullmatch(url) is not None)


def az(args):
    if not read_only(args):
        raise ValueError(f'refusing a non-read Azure call: az {" ".join(args[:3])}')
    result = subprocess.run(['az', *args, '--only-show-errors', '--output', 'json'],
                            capture_output=True, text=True, timeout=180, check=False)
    if result.returncode:
        raise RuntimeError(result.stderr.strip()[-400:] or f'az exited {result.returncode}')
    return json.loads(result.stdout or 'null')


def attempt(args):
    try:
        return {'data': az(args)}
    except (RuntimeError, ValueError, OSError, subprocess.TimeoutExpired) as error:
        return {'error': str(error) or type(error).__name__}


def metric_rows(response):
    rows = []
    for metric in response.get('value', []):
        points = [point for series in metric.get('timeseries', []) for point in series.get('data', [])]
        values = {key: [p[key] for p in points if p.get(key) is not None]
                  for key in ('minimum', 'average', 'maximum', 'total')}
        peak = max((p for p in points if p.get('maximum') is not None), key=lambda p: p['maximum'], default=None)
        rows.append({'metric': metric['name']['value'], 'unit': metric.get('unit'),
                     'min': min(values['minimum'], default=None),
                     'mean': sum(values['average']) / len(values['average']) if values['average'] else None,
                     'max': peak['maximum'] if peak else None, 'max_at': peak['timeStamp'] if peak else None,
                     'total': sum(values['total']) if values['total'] else None, 'points': len(values['average'])})
    return rows


def table_rows(response):
    table = response['tables'][0]
    columns = [column['name'] for column in table['columns']]
    return [dict(zip(columns, row)) for row in table['rows']]


def health_events(response, start, end):
    events = []
    for status in response.get('value', []):
        properties = status.get('properties', {})
        moment = properties.get('occuredTime') or properties.get('reportedTime')
        if moment and start - HEALTH_MARGIN <= instant(moment) <= end + HEALTH_MARGIN:
            events.append({key: properties.get(key) for key in
                           ('occuredTime', 'availabilityState', 'title', 'summary', 'reasonType', 'reasonChronicity')})
    return events


def number(value):
    if value is None:
        return ''
    return f'{value:,.0f}' if abs(value) >= 1e6 else f'{value:.6g}'


def run(windows, names, subscription, output):
    group = f'/subscriptions/{subscription}/resourceGroups/{names["group"]}/providers'
    resources = {names['plan']: f'{group}/Microsoft.Web/serverfarms/{names["plan"]}',
                 names['postgres']: f'{group}/Microsoft.DBforPostgreSQL/flexibleServers/{names["postgres"]}',
                 **{site: f'{group}/Microsoft.Web/sites/{site}' for site in names['sites']}}
    health = {name: attempt(['rest', '--method', 'get', '--url',
                             f'{ARM}{rid}/providers/Microsoft.ResourceHealth/availabilityStatuses?api-version=2022-10-01'])
              for name, rid in resources.items()}
    workspace = attempt(['rest', '--method', 'get', '--url',
                         f'{ARM}{group}/Microsoft.OperationalInsights/workspaces/{names["workspace"]}?api-version=2022-10-01'])
    output.mkdir(parents=True, exist_ok=True)
    (output / 'resource-health.json').write_text(json.dumps(health, indent=2))
    lines = ['# DEV metrics', '', f'Resource group `{names["group"]}`, read-only, 1-minute grain. '
             'The raw JSON of every read is in the run artifact.', '']
    lines += [f'- Resource Health of {name} could not be read: {read["error"]}' for name, read in health.items()
              if 'error' in read]
    any_metric = False
    for number_, (start, end) in enumerate(windows, 1):
        window = {'start': stamp(start), 'end': stamp(end), 'metrics': {}, 'errors': {}}
        for name, rid in resources.items():
            namespace, metric_names = METRICS[rid.split('/')[-2]]
            read = attempt(['monitor', 'metrics', 'list', '--resource', rid, '--namespace', namespace,
                            '--metrics', *metric_names, '--aggregation', 'Average', 'Minimum', 'Maximum', 'Total',
                            '--interval', '1m', '--start-time', stamp(start), '--end-time', stamp(end)])
            window['metrics'][name] = read.get('data')
            if 'error' in read:
                window['errors'][f'{name} metrics'] = read['error']
            any_metric |= 'data' in read
        activity = attempt(['monitor', 'activity-log', 'list', '--resource-group', names['group'],
                            '--start-time', stamp(start - HEALTH_MARGIN), '--end-time', stamp(end + HEALTH_MARGIN),
                            '--max-events', '1000', '--query',
                            '[].{time:eventTimestamp,resource:resourceId,operation:operationName.value,'
                            'status:status.value,substatus:subStatus.value,level:level,category:category.value}'])
        window['activity_log'] = activity.get('data')
        if 'error' in activity:
            window['errors']['activity log'] = activity['error']
        window['app_insights'] = {}
        if 'data' in workspace:
            url = f'https://api.loganalytics.io/v1/workspaces/{workspace["data"]["properties"]["customerId"]}/query'
            for key, query in QUERIES.items():
                read = attempt(['rest', '--method', 'post', '--url', url, '--resource', 'https://api.loganalytics.io',
                                '--body', json.dumps({'query': query.format(start=stamp(start), end=stamp(end))})])
                window['app_insights'][key] = read.get('data')
                if 'error' in read:
                    window['errors'][f'App Insights {key}'] = read['error']
        else:
            window['errors']['App Insights'] = f'workspace not reachable: {workspace["error"]}'
        (output / f'window-{number_}.json').write_text(json.dumps(window, indent=2))
        lines += render(number_, start, end, window, health)
    return '\n'.join(lines) + '\n', any_metric


def render(index, start, end, window, health):
    lines = [f'## Window {index}: {start:%Y-%m-%d %H:%M} to {end:%H:%M} UTC', '',
             '| Resource | Metric | Unit | Min | Mean | Max | Max at | Total | Minutes |', '|---|---|---|---|---|---|---|---|---|']
    for name, response in window['metrics'].items():
        for row in metric_rows(response or {}):
            peak = instant(row['max_at']).strftime('%H:%M') if row['max_at'] else ''
            lines.append(f'| {name} | {row["metric"]} | {row["unit"]} | {number(row["min"])} | {number(row["mean"])} '
                         f'| {number(row["max"])} | {peak} | {number(row["total"])} | {row["points"]} |')
    lines += ['', '### Resource Health', '']
    events = [(name, event) for name, read in health.items() if 'data' in read
              for event in health_events(read['data'], start, end)]
    lines += [f'- {event["occuredTime"]} {name}: {event["availabilityState"]}, {event["title"]} '
              f'({event["reasonType"] or "no reason type"}): {event["summary"]}' for name, event in events] \
        or ['No Resource Health event within 30 minutes of the window.']
    lines += ['', '### Activity Log (30 minutes either side)', '']
    rows = window['activity_log'] or []
    lines += ['| Time | Resource | Operation | Status |', '|---|---|---|---|'] + [
        f'| {row["time"]} | {(row["resource"] or "").rsplit("/", 1)[-1]} | {row["operation"]} | {row["status"]} |'
        for row in rows] if rows else ['No Activity Log entry.']
    lines += ['', '### App Insights', '']
    insights = window['app_insights']
    if insights.get('slow_requests'):
        slow = table_rows(insights['slow_requests'])
        lines += [f'Requests over 20 s: {len(slow)}']
        if slow:
            lines += ['', '| Time | Role | Request | ms | Result |', '|---|---|---|---|---|']
            lines += [f'| {r.get("TimeGenerated")} | {r.get("AppRoleName")} | {r.get("Name")} '
                      f'| {number(r.get("DurationMs"))} | {r.get("ResultCode")} |' for r in slow[:25]]
    if insights.get('postgres_dependencies'):
        durations = [d['DurationMs'] for d in table_rows(insights['postgres_dependencies'])
                     if d.get('DurationMs') is not None]
        lines += ['', f'PostgreSQL dependencies of those requests, or over 5 s: {len(durations)}'
                  + (f', longest {number(max(durations))} ms' if durations else '')]
    if insights.get('npgsql_exceptions'):
        groups = table_rows(insights['npgsql_exceptions'])
        lines += ['', f'Npgsql or timeout exception groups: {len(groups)}']
        if groups:
            lines += ['', '| Count | Role | Exception | Innermost | First | Last |', '|---|---|---|---|---|---|']
            lines += [f'| {g.get("Count")} | {g.get("AppRoleName")} | {g.get("ExceptionType")} '
                      f'| {g.get("InnermostType")}: {g.get("InnermostMessage")} | {g.get("First")} | {g.get("Last")} |'
                      for g in groups]
    if window['errors']:
        lines += ['', '### Reads that failed', ''] + [f'- {what}: {error}' for what, error in window['errors'].items()]
    return lines + ['']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--windows', required=True)
    parser.add_argument('--params', default='deploy/bicep/weu.dev.bicepparam')
    parser.add_argument('--output', default='dev-metrics')
    parser.add_argument('--validate-only', action='store_true')
    args = parser.parse_args()
    windows = parse_windows(args.windows, datetime.now(timezone.utc))
    names = dev_names(args.params)
    print(json.dumps({'windows': [[stamp(s), stamp(e)] for s, e in windows], 'names': names}, indent=2))
    if args.validate_only:
        return 0
    subscription = os.environ.get('EXPECTED_SUBSCRIPTION_ID', '')
    if re.fullmatch(r'[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}', subscription) is None:
        raise ValueError('EXPECTED_SUBSCRIPTION_ID is not a subscription id')
    summary, complete = run(windows, names, subscription, Path(args.output))
    (Path(args.output) / 'summary.md').write_text(summary)
    if os.environ.get('GITHUB_STEP_SUMMARY'):
        with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as handle:
            handle.write(summary)
    if not complete:
        print('::error::No metric could be read; check the DEV identity and the resource names above')
    return 0 if complete else 1


if __name__ == '__main__':
    sys.exit(main())
