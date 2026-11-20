import importlib.util
import json
import os
from datetime import datetime, timezone
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch


spec = importlib.util.spec_from_file_location('dev_metrics', Path(__file__).with_name('dev_metrics.py'))
metrics = importlib.util.module_from_spec(spec)
spec.loader.exec_module(metrics)

NOW = datetime(2026, 11, 21, 12, 0, tzinfo=timezone.utc)
SUBSCRIPTION = '11111111-1111-1111-1111-111111111111'
WORKSPACE_ID = '22222222-2222-2222-2222-222222222222'

# Shaped after `az monitor metrics list --output json` for a plan read at 1-minute grain.
PLAN_METRICS = {
    'cost': 59, 'interval': 'PT1M', 'namespace': 'Microsoft.Web/serverfarms', 'resourceregion': 'westeurope',
    'timespan': '2026-11-14T09:10:00Z/2026-11-14T09:13:00Z',
    'value': [
        {'name': {'value': 'CpuPercentage', 'localizedValue': 'CPU Percentage'}, 'unit': 'Percent',
         'errorCode': 'Success', 'type': 'Microsoft.Insights/metrics',
         'timeseries': [{'metadatavalues': [], 'data': [
             {'timeStamp': '2026-11-14T09:10:00Z', 'average': 40.0, 'minimum': 31.0, 'maximum': 52.0, 'total': 80.0},
             {'timeStamp': '2026-11-14T09:11:00Z', 'average': 98.5, 'minimum': 97.0, 'maximum': 100.0, 'total': 197.0},
             {'timeStamp': '2026-11-14T09:12:00Z'}]}]},
        {'name': {'value': 'HttpQueueLength', 'localizedValue': 'Http Queue Length'}, 'unit': 'Count',
         'errorCode': 'Success', 'type': 'Microsoft.Insights/metrics', 'timeseries': []},
    ]}

# Shaped after GET .../providers/Microsoft.ResourceHealth/availabilityStatuses?api-version=2022-10-01.
HEALTH = {'value': [
    {'id': 'current', 'properties': {'availabilityState': 'Available', 'title': 'Available',
     'summary': 'There are no known problems', 'occuredTime': '2026-11-19T10:00:00Z',
     'reportedTime': '2026-11-21T11:00:00Z'}},
    {'id': 'restart', 'properties': {'availabilityState': 'Unavailable', 'title': 'Server restarting',
     'summary': 'The server is restarting', 'reasonType': 'Unplanned', 'occuredTime': '2026-11-14T09:20:00Z',
     'reportedTime': '2026-11-14T09:21:00Z'}},
]}

# Shaped after `az monitor activity-log list --query <the script's projection>`.
ACTIVITY = [{'time': '2026-11-14T09:15:02Z',
             'resource': '/subscriptions/x/resourceGroups/rg-cleansia-weu-dev/providers/Microsoft.Web/sites/api-cleansia-admin-weu-dev',
             'operation': 'Microsoft.Web/sites/restart/action', 'status': 'Succeeded', 'substatus': None,
             'level': 'Informational', 'category': 'Administrative'}]

# Shaped after POST https://api.loganalytics.io/v1/workspaces/<id>/query.
SLOW_REQUESTS = {'tables': [{'name': 'PrimaryResult',
    'columns': [{'name': 'TimeGenerated', 'type': 'datetime'}, {'name': 'AppRoleName', 'type': 'string'},
                {'name': 'Name', 'type': 'string'}, {'name': 'DurationMs', 'type': 'real'},
                {'name': 'ResultCode', 'type': 'string'}, {'name': 'Success', 'type': 'bool'},
                {'name': 'OperationId', 'type': 'string'}],
    'rows': [['2026-11-14T09:22:10Z', 'Cleansia.Web.Admin', 'POST /api/auth/login', 31234.5, '500', False, 'op1']]}]}

WORKSPACE = {'properties': {'customerId': WORKSPACE_ID}}


def log_table(columns, *rows):
    return {'tables': [{'name': 'PrimaryResult', 'columns': [{'name': name, 'type': 'dynamic'} for name in columns],
                        'rows': list(rows)}]}


POSTGRES_DEPENDENCIES = log_table(
    ['TimeGenerated', 'AppRoleName', 'OperationId', 'Name', 'Target', 'DurationMs', 'Success', 'ResultCode'],
    ['2026-11-14T09:22:09Z', 'Cleansia.Web.Admin', 'op1', 'Cleansia', 'pg-cleansia-weu-dev.postgres.database.azure.com|Cleansia',
     30012.0, False, ''])
NPGSQL_EXCEPTIONS = log_table(
    ['AppRoleName', 'ExceptionType', 'InnermostType', 'InnermostMessage', 'Count', 'First', 'Last'],
    ['Cleansia.Web.Admin', 'Npgsql.NpgsqlException', 'System.TimeoutException', 'Timeout during reading attempt', 3,
     '2026-11-14T09:21:00Z', '2026-11-14T09:23:00Z'])


def bicep_root():
    return Path(__file__).resolve().parents[2] / 'deploy' / 'bicep'


class WindowTests(unittest.TestCase):
    def test_reads_several_windows_separated_by_commas_or_spaces(self):
        windows = metrics.parse_windows(
            '2026-11-14T09:10Z/2026-11-14T09:45Z, 2026-11-15T15:55Z/2026-11-15T17:15Z 2026-11-20T06:55Z/2026-11-20T07:20Z',
            NOW)
        self.assertEqual(len(windows), 3)
        self.assertEqual(windows[0], (datetime(2026, 11, 14, 9, 10, tzinfo=timezone.utc),
                                      datetime(2026, 11, 14, 9, 45, tzinfo=timezone.utc)))

    def test_refuses_what_is_not_a_bounded_past_window(self):
        for text in ('', 'yesterday', '2026-11-14T09:45Z/2026-11-14T09:10Z', '2026-11-14T09:10Z/2026-11-14T16:11Z',
                     '2026-11-21T11:00Z/2026-11-21T13:00Z', '2026-08-01T09:10Z/2026-08-01T09:45Z',
                     '2026-11-14T09:10Z/2026-11-14T09:45Z; az rest --method delete',
                     '2026-11-14T09:10/2026-11-14T09:45', '2026-11-14T25:10Z/2026-11-14T25:45Z',
                     ' '.join(['2026-11-14T09:10Z/2026-11-14T09:45Z'] * 13)):
            with self.subTest(text=text), self.assertRaises(ValueError):
                metrics.parse_windows(text, NOW)


class NameTests(unittest.TestCase):
    def test_names_come_from_the_dev_bicep(self):
        names = metrics.dev_names(bicep_root() / 'weu.dev.bicepparam')
        self.assertEqual(names['group'], 'rg-cleansia-weu-dev')
        self.assertEqual(names['plan'], 'plan-cleansia-weu-dev')
        self.assertEqual(names['postgres'], 'pg-cleansia-weu-dev')
        self.assertEqual(names['workspace'], 'log-cleansia-weu-dev')
        self.assertEqual(names['sites'], [
            'api-cleansia-partner-weu-dev', 'api-cleansia-admin-weu-dev', 'api-cleansia-customer-weu-dev',
            'api-cleansia-partner-mobile-weu-dev', 'api-cleansia-customer-mobile-weu-dev',
            'web-cleansia-customer-weu-dev', 'func-cleansia-weu-dev'])

    def test_refuses_any_stage_but_dev(self):
        with self.assertRaises(ValueError):
            metrics.dev_names(bicep_root() / 'weu.prod.bicepparam')


class ReadOnlyTests(unittest.TestCase):
    def test_allows_only_reads(self):
        query = f'https://api.loganalytics.io/v1/workspaces/{WORKSPACE_ID}/query'
        for args in (['monitor', 'metrics', 'list', '--resource', 'x'],
                     ['monitor', 'activity-log', 'list', '--resource-group', 'x'],
                     ['rest', '--method', 'get', '--url', 'https://management.azure.com/x'],
                     ['rest', '--method', 'post', '--url', query, '--resource', 'https://api.loganalytics.io']):
            with self.subTest(args=args):
                self.assertTrue(metrics.read_only(args))
        for args in (['rest', '--method', 'put', '--url', 'https://management.azure.com/x'],
                     ['rest', '--method', 'delete', '--url', 'https://management.azure.com/x'],
                     ['rest', '--method', 'patch', '--url', 'https://management.azure.com/x'],
                     ['rest', '--url', 'https://management.azure.com/x'],
                     ['rest', '--method', 'post', '--url', 'https://management.azure.com/x/restart'],
                     ['rest', '--method', 'post', '--url', query + '/../x'],
                     ['rest', '--method', 'get', '--method', 'put', '--url', 'https://management.azure.com/x'],
                     ['postgres', 'flexible-server', 'firewall-rule', 'create'],
                     ['webapp', 'restart'], ['monitor', 'metrics', 'alert', 'create']):
            with self.subTest(args=args):
                self.assertFalse(metrics.read_only(args))
                with self.assertRaises(ValueError):
                    metrics.az(args)


class SummaryTests(unittest.TestCase):
    def test_metric_rows_skip_empty_minutes(self):
        rows = metrics.metric_rows(PLAN_METRICS)
        self.assertEqual(rows[0], {'metric': 'CpuPercentage', 'unit': 'Percent', 'min': 31.0, 'mean': 69.25,
                                   'max': 100.0, 'max_at': '2026-11-14T09:11:00Z', 'total': 277.0, 'points': 2})
        self.assertEqual(rows[1]['points'], 0)
        self.assertIsNone(rows[1]['max'])

    def test_table_rows_name_their_columns(self):
        self.assertEqual(metrics.table_rows(SLOW_REQUESTS)[0]['DurationMs'], 31234.5)

    def test_health_keeps_events_near_the_window(self):
        start, end = datetime(2026, 11, 14, 9, 10, tzinfo=timezone.utc), datetime(2026, 11, 14, 9, 45, tzinfo=timezone.utc)
        events = metrics.health_events(HEALTH, start, end)
        self.assertEqual([event['title'] for event in events], ['Server restarting'])


class RunTests(unittest.TestCase):
    def setUp(self):
        self.calls = []
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)

    def fake(self, failing=()):
        def run(argv, **_):
            self.assertEqual(argv[0], 'az')
            self.assertTrue(metrics.read_only(argv[1:]), argv)
            self.calls.append(argv)
            url = argv[argv.index('--url') + 1] if '--url' in argv else ''
            if any(part in ' '.join(argv) for part in failing):
                return subprocess.CompletedProcess(argv, 1, '', 'ERROR: (AuthorizationFailed) no access')
            if argv[1:4] == ['monitor', 'metrics', 'list']:
                body = PLAN_METRICS
            elif argv[1:4] == ['monitor', 'activity-log', 'list']:
                body = ACTIVITY
            elif 'availabilityStatuses' in url:
                body = HEALTH
            elif 'Microsoft.OperationalInsights/workspaces' in url:
                body = WORKSPACE
            elif 'AppExceptions' in ' '.join(argv):
                body = NPGSQL_EXCEPTIONS
            elif 'AppDependencies' in ' '.join(argv):
                body = POSTGRES_DEPENDENCIES
            else:
                body = SLOW_REQUESTS
            return subprocess.CompletedProcess(argv, 0, json.dumps(body), '')
        return run

    def read(self, failing=()):
        names = metrics.dev_names(bicep_root() / 'weu.dev.bicepparam')
        windows = metrics.parse_windows('2026-11-14T09:10Z/2026-11-14T09:45Z', NOW)
        with patch.object(metrics.subprocess, 'run', side_effect=self.fake(failing)):
            return metrics.run(windows, names, SUBSCRIPTION, Path(self.tmp.name))

    def test_reads_every_resource_and_writes_summary_and_raw_json(self):
        summary, complete = self.read()
        self.assertTrue(complete)
        resources = {argv[argv.index('--resource') + 1].rsplit('/', 1)[1]
                     for argv in self.calls if argv[1:4] == ['monitor', 'metrics', 'list']}
        self.assertEqual(len(resources), 9)
        self.assertIn('| plan-cleansia-weu-dev | CpuPercentage | Percent | 31 | 69.25 | 100 | 09:11 |', summary)
        self.assertIn('Server restarting', summary)
        self.assertIn('Microsoft.Web/sites/restart/action', summary)
        self.assertIn('POST /api/auth/login', summary)
        self.assertIn('PostgreSQL dependencies of those requests, or over 5 s: 1, longest 30012 ms', summary)
        self.assertIn('| 3 | Cleansia.Web.Admin | Npgsql.NpgsqlException | System.TimeoutException: Timeout during reading attempt', summary)
        raw = json.loads((Path(self.tmp.name) / 'window-1.json').read_text())
        self.assertEqual(raw['start'], '2026-11-14T09:10:00Z')
        self.assertEqual(raw['metrics']['pg-cleansia-weu-dev']['value'][0]['name']['value'], 'CpuPercentage')

    def test_a_failed_read_is_reported_and_the_rest_still_read(self):
        summary, complete = self.read(failing=('pg-cleansia-weu-dev', 'Microsoft.OperationalInsights'))
        self.assertTrue(complete)
        self.assertIn('pg-cleansia-weu-dev metrics: ERROR: (AuthorizationFailed) no access', summary)
        self.assertIn('App Insights: workspace not reachable', summary)
        self.assertIn('| plan-cleansia-weu-dev | CpuPercentage |', summary)

    def test_no_metric_at_all_is_not_complete(self):
        _, complete = self.read(failing=('metrics',))
        self.assertFalse(complete)


if __name__ == '__main__':
    unittest.main()
