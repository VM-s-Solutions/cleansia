import importlib.util
import os
from pathlib import Path
import unittest
from unittest.mock import patch


spec = importlib.util.spec_from_file_location('capacity_trial', Path(__file__).with_name('dev_capacity_trial.py'))
trial = importlib.util.module_from_spec(spec)
spec.loader.exec_module(trial)

CONTEXT = {'GITHUB_REPOSITORY': 'VM-s-Solutions/cleansia', 'GITHUB_REF': 'refs/heads/master',
           'GITHUB_EVENT_NAME': 'workflow_dispatch', 'GITHUB_SHA': 'a' * 40,
           'GITHUB_RUN_ID': '123', 'GITHUB_RUN_ATTEMPT': '1', 'EXPECTED_CLI_RECEIPT': 'a' * 64}


CLI_RECEIPT = {'version': '2.99.0', 'update_sha256': '1' * 64, 'setter_sha256': '2' * 64,
               'registration_sha256': '3' * 64, 'getter_sha256': '4' * 64, 'getter_name': 'get',
               'registered_callbacks': True, 'existing_model': True, 'explicit_sku_count': True,
               'put_poller': True, 'waits': True, 'provider_api_version': 'unreported',
               'http_retry_count': 'SDK defaults'}


class AdmissionTests(unittest.TestCase):
    def test_wrong_scope_is_rejected_before_any_subprocess(self):
        for key, value in (('GITHUB_REPOSITORY', 'other/repo'), ('GITHUB_REF', 'refs/heads/test'),
                           ('GITHUB_EVENT_NAME', 'push'), ('GITHUB_SHA', 'not-a-sha')):
            with self.subTest(key=key), patch.dict(os.environ, {**CONTEXT, key: value}, clear=True):
                with self.assertRaises(ValueError):
                    trial.validate_context('trial', '1')

    def test_count_is_only_a_guard_between_one_and_three(self):
        with patch.dict(os.environ, CONTEXT, clear=True):
            for capacity in ('0', '4', '-1', '1; az', '01', 'true'):
                with self.subTest(capacity=capacity), self.assertRaises(ValueError):
                    trial.validate_context('trial', capacity)

import copy
from datetime import datetime, timezone
import io
import json
import socket
import subprocess
import tempfile


class OfflineCase(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.addCleanup(patch.stopall)
        patch.dict(os.environ, {**CONTEXT, 'BASELINE_ARTIFACT_ID': '456', 'GH_TOKEN': 'github-credential-needle',
            'EXPECTED_SUBSCRIPTION_ID': '11111111-1111-1111-1111-111111111111',
            'EXPECTED_TENANT_ID': '22222222-2222-2222-2222-222222222222',
            'EXPECTED_CLI_RECEIPT': trial.fingerprint(CLI_RECEIPT)}, clear=True).start()
        patch.object(trial, 'DIRECTORY', Path(self.tmp.name) / 'evidence').start()
        patch.object(trial, 'EVENTS', []).start()
        patch.object(trial, 'SUBSCRIPTION', os.environ['EXPECTED_SUBSCRIPTION_ID']).start()
        self.process = patch.object(trial.subprocess, 'run', side_effect=AssertionError('unmocked subprocess')).start()
        patch.object(socket, 'create_connection', side_effect=AssertionError('unmocked network')).start()
        patch.object(trial.time, 'sleep').start()
        self.stdout = io.StringIO()
        patch('sys.stdout', self.stdout).start()

    def raw_plan(self, **changes):
        return {'id': trial.plan_id(), 'name': trial.PLAN, 'type': 'Microsoft.Web/serverfarms',
            'location': 'westeurope', 'sku': 'B2', 'tier': 'Basic', 'capacity': 1, 'workers': 1,
            'reserved': True, 'perSiteScaling': False, 'status': 'Ready', 'provisioning': 'Succeeded',
            'configuration': {'tags': {'owner': 'opaque-value'}}} | changes

    def snapshot(self, sku='B2'):
        return {'plan': {'name': trial.PLAN, 'sku': sku, 'capacity': 1, 'workers': 1,
                         'configuration_sha256': 'f' * 64},
                'occupants': [{'name': app, 'bound_to_fixed_plan': True, 'running': True}
                              for app in sorted(trial.OCCUPANTS)]}

    def listing(self, names=None):
        return {'value': [{'id': trial.base() + '/sites/' + app, 'name': app,
                           'plan': trial.plan_id(), 'state': 'Running'}
                          for app in (trial.OCCUPANTS if names is None else names)], 'nextLink': None}

    def baseline(self):
        value = {**self.snapshot(), 'source_sha': CONTEXT['GITHUB_SHA'], 'run_id': CONTEXT['GITHUB_RUN_ID'],
            'run_attempt': 1, 'recorded_utc': datetime.now(timezone.utc).isoformat(),
            'packages': {'same': 'packages'}, 'configurations': {'same': 'configs'},
            'cli': CLI_RECEIPT, 'cli_receipt_sha256': trial.fingerprint(CLI_RECEIPT)}
        trial.save('baseline.json', value)
        return value


class SafetyTests(OfflineCase):
    def test_invalid_scope_main_performs_zero_subprocesses(self):
        for key, value in (('GITHUB_REPOSITORY', 'foreign/repo'), ('GITHUB_REF', 'refs/heads/topic'),
                           ('GITHUB_RUN_ATTEMPT', '2'), ('GITHUB_EVENT_NAME', 'push')):
            with patch.dict(os.environ, {key: value}):
                self.assertEqual(trial.main(['trial']), 1)
        self.process.assert_not_called()

    def test_observe_has_no_mutator_and_restore_does_not_prepare(self):
        with patch.object(trial, 'authenticate'), patch.object(trial, 'prepare') as prepare, \
             patch.object(trial, 'resize') as resize, patch.object(trial, 'restart_sites') as restart:
            self.assertEqual(trial.main(['observe']), 0)
            prepare.assert_called_once_with(1)
            resize.assert_not_called()
            restart.assert_not_called()

    def test_invalid_mode_count_and_subscription_reject_zero_writes(self):
        for mode in ('trial; az', 'prod', '', 'restore'):
            with self.assertRaises(ValueError):
                trial.validate_context(mode, '1')
        with patch.dict(os.environ, {'EXPECTED_SUBSCRIPTION_ID': 'wrong'}):
            self.assertEqual(trial.main(['restore-b2']), 1)
        self.process.assert_not_called()

    def test_account_requires_expected_subscription_tenant_and_cloud(self):
        good = {'id': os.environ['EXPECTED_SUBSCRIPTION_ID'], 'tenant': os.environ['EXPECTED_TENANT_ID'], 'cloud': 'AzureCloud'}
        for change in ({'id': 'other'}, {'tenant': 'other'}, {'cloud': 'AzureChinaCloud'}):
            with patch.object(trial, 'azure', return_value=good | change), self.assertRaises(ValueError):
                trial.authenticate()

    def test_plan_scope_capacity_and_readiness_are_admission_guards(self):
        bad = ({'id': trial.plan_id() + '-foreign'}, {'name': 'other'}, {'type': 'Microsoft.Web/sites'},
            {'location': 'eastus'}, {'sku': 'P1v3'}, {'tier': 'PremiumV3'}, {'workers': 2}, {'capacity': True},
            {'reserved': False}, {'perSiteScaling': True}, {'status': 'Scaling'}, {'provisioning': 'Updating'})
        for change in bad:
            with self.subTest(change=change), patch.object(trial, 'arm', return_value=self.raw_plan(**change)), self.assertRaises(ValueError):
                trial.read_plan(1)
        with patch.object(trial, 'arm', return_value=self.raw_plan()):
            observed = trial.read_plan(1)
        self.assertNotIn('opaque-value', json.dumps(observed))
        self.assertEqual(observed['sku'], 'B2')
        self.process.assert_not_called()

    def test_exact_seven_occupants_and_pagination_are_required(self):
        for names in (trial.OCCUPANTS[:-1], (*trial.OCCUPANTS, 'foreign'), (*trial.OCCUPANTS, trial.OCCUPANTS[0])):
            with self.subTest(names=names), patch.object(trial, 'azure', return_value=self.listing(names)), self.assertRaises(ValueError):
                trial.inventory()
        first = self.listing(trial.OCCUPANTS[:3])
        first['nextLink'] = 'https://management.azure.com' + trial.plan_id() + '/sites?api-version=2024-04-01&$skiptoken=x'
        with patch.object(trial, 'azure', side_effect=[first, self.listing(trial.OCCUPANTS[3:])]):
            self.assertEqual(len(trial.inventory()), 7)

    def test_incomplete_foreign_redirect_and_wrong_binding_never_admit(self):
        for url in ('https://foreign.example/secret', 'https://management.azure.com@foreign.example/a',
                    'http://management.azure.com/a', 'https://management.azure.com' + trial.plan_id() + '/sites?api-version=wrong'):
            rows = self.listing()
            rows['nextLink'] = url
            with patch.object(trial, 'azure', return_value=rows), self.assertRaises(ValueError):
                trial.inventory()
        rows = self.listing()
        rows['value'][-1]['plan'] += '-other'
        with patch.object(trial, 'azure', return_value=rows), self.assertRaises(ValueError):
            trial.inventory()
        self.process.assert_not_called()

    def test_resize_arguments_are_exact_fixed_sku_and_preserved_capacity(self):
        for capacity in (1, 2, 3):
            for sku in ('B2', 'B3'):
                args = trial.resize_arguments(sku, capacity)
                self.assertEqual(args[0:3], ['appservice', 'plan', 'update'])
                self.assertEqual(args[args.index('--number-of-workers') + 1], str(capacity))
                self.assertEqual(args[args.index('--sku') + 1], sku)
                self.assertEqual(args[args.index('--name') + 1], trial.PLAN)
                self.assertEqual(args[args.index('--resource-group') + 1], trial.GROUP)
        for sku, count in (('P1v3', 1), ('B3', 4), ('B3', True)):
            with self.assertRaises(ValueError):
                trial.resize_arguments(sku, count)

    def test_bounded_subprocess_omits_raw_failure_output(self):
        def failed(arguments, **kwargs):
            kwargs['stdout'].write(b'credential-needle secret-body')
            return subprocess.CompletedProcess(arguments, 1)
        self.process.side_effect = failed
        with self.assertRaises(ValueError):
            trial.command(['az', 'fixed'])
        self.assertNotIn('credential-needle', self.stdout.getvalue())
        self.assertIs(self.process.call_args.kwargs['stderr'], subprocess.DEVNULL)
        self.assertFalse(self.process.call_args.kwargs['check'])

    def queue_response(self, status, path, moved=False):
        def answer(arguments, **kwargs):
            url = arguments[-1]
            if '/branches/master' in url:
                return json.dumps({'commit': {'sha': 'b' * 40 if moved else CONTEXT['GITHUB_SHA']}}).encode()
            selected = 'status=' + status + '&' in url
            runs = [{'id': 999, 'event': 'workflow_dispatch', 'path': path}] if selected else []
            return json.dumps({'total_count': len(runs), 'workflow_runs': runs}).encode()
        return answer

    def test_manual_interference_blocks_trial(self):
        with patch.object(trial, 'command', side_effect=self.queue_response('queued', '.github/workflows/execute-sql.yml')), self.assertRaises(ValueError):
            trial.queues()

    def test_recovery_survives_master_advance_and_unrelated_manual_work(self):
        with patch.object(trial, 'command', side_effect=self.queue_response('in_progress', '.github/workflows/execute-sql.yml', True)):
            trial.queues(restoring=True)
        with patch.object(trial, 'command', side_effect=self.queue_response('in_progress', '.github/workflows/deploy-dev.yml')), self.assertRaises(ValueError):
            trial.queues(restoring=True)


class PackageAndProbeTests(OfflineCase):
    def test_package_snapshot_reads_only_lists_and_stable_pointers(self):
        calls = []
        def read(_opener, app, _token, path, _phase, _remaining, **kwargs):
            calls.append(path)
            if path.endswith('packagename.txt'):
                return 'selected.zip'
            return [{'id': 'deployment1', 'deployer': f'cleansia-{trial.ACCEPTED_RUN}-1-{app}-' + 'a' * 32,
                     'status': 4, 'complete': True, 'is_temp': False, 'active': True}]
        with patch.object(trial.package, 'app_service_resource', return_value='https://appservice.azure.com'), \
             patch.object(trial.package, 'az_value', return_value='credential-needle'), \
             patch.object(trial.package, 'scm_request', side_effect=read), patch.object(trial, 'azure', return_value=True):
            observed = trial.packages()
        self.assertEqual(len(observed), 6)
        self.assertEqual(len(calls), 24)
        self.assertTrue(all(path in ('/api/deployments', '/api/vfs/data/SitePackages/packagename.txt') for path in calls))
        self.assertNotIn('credential-needle', json.dumps(observed))

    def test_changed_pointer_or_pending_deployment_blocks_snapshot(self):
        for pending in (False, True):
            pointers = iter(('one.zip', 'two.zip'))
            def read(_opener, app, _token, path, _phase, _remaining, **kwargs):
                if path.endswith('packagename.txt'):
                    return next(pointers)
                return [{'id': 'd1', 'deployer': f'cleansia-{trial.ACCEPTED_RUN}-1-{app}-' + 'a' * 32,
                         'status': 1 if pending else 4, 'complete': not pending, 'is_temp': False, 'active': True}]
            with patch.object(trial.package, 'app_service_resource', return_value='https://appservice.azure.com'), \
                 patch.object(trial.package, 'az_value', return_value='credential-needle'), \
                 patch.object(trial.package, 'scm_request', side_effect=read), patch.object(trial, 'azure', return_value=True), self.assertRaises(ValueError):
                trial.packages()

    def test_ssr_requires_real_named_card_inside_rendered_home(self):
        card = '<a class="other cl-services__card" href="/order?serviceId=1"><h3 class="cl-services__name">&amp; Home</h3></a>'
        valid = '<app-root ng-server-context="ssr"><cleansia-home>' + card + '</cleansia-home></app-root>'
        self.assertTrue(trial.semantic('ssr-root', valid.encode())['healthy'])
        for body in ('<!--' + valid + '-->', '<script>' + valid + '</script>', '<template>' + valid + '</template>',
                     valid.replace('ng-server-context="ssr"', ''), valid.replace('&amp; Home', '  '), card,
                     valid.replace('/order?serviceId=1', 'https://foreign.example/order?serviceId=1')):
            with self.subTest(body=body):
                self.assertFalse(trial.semantic('ssr-root', body.encode())['healthy'])

    def test_health_contract_rejects_500_body_shell_and_nonfinite_uptime(self):
        self.assertTrue(trial.semantic('mobile-health', b'Healthy')['healthy'])
        for body in (b'Healthy\n', b'<html>Healthy</html>', b'Unhealthy'):
            self.assertFalse(trial.semantic('mobile-health', body)['healthy'])
        data = {'status': 'healthy', 'timestamp': '2026-10-16T16:00:00.000Z', 'uptime': 1,
                'memory': dict.fromkeys(('rss', 'heapTotal', 'heapUsed', 'external', 'arrayBuffers'), 1)}
        self.assertTrue(trial.semantic('ssr-health', json.dumps(data).encode())['healthy'])
        data['uptime'] = float('nan')
        self.assertFalse(trial.semantic('ssr-health', json.dumps(data).encode())['healthy'])

    def test_fixed_restart_requires_204_and_never_exposes_token(self):
        def response(arguments, **kwargs):
            self.assertIn('--max-redirs', arguments)
            self.assertIn('--proxy', arguments)
            self.assertEqual(arguments[-1], 'https://management.azure.com' + trial.plan_id() + '/restartSites?softRestart=false&api-version=2024-04-01')
            self.assertNotIn('credential-needle', repr(arguments))
            self.assertEqual(kwargs['stdin'], b'Authorization: Bearer credential-needle\n')
            return b'204'
        with patch.object(trial.package, 'az_value', return_value='credential-needle'), patch.object(trial, 'command', side_effect=response):
            self.assertEqual(trial.restart_sites(), 204)
        with patch.object(trial.package, 'az_value', return_value='credential-needle'), patch.object(trial, 'command', return_value=b'202'), self.assertRaises(ValueError):
            trial.restart_sites()
        self.assertNotIn('credential-needle', self.stdout.getvalue())

    def test_warm_200_is_inconclusive_and_negative_transition_recovers(self):
        baseline = self.snapshot()
        for negative in (False, True):
            counts = dict.fromkeys(trial.PROBES, 0)
            def probe(label, deadline=None):
                counts[label] += 1
                healthy = not negative or counts[label] != 2
                return {'probe': label, 'healthy': healthy, 'elapsed_seconds': 0.1,
                        'started_utc': trial.utc(), 'finished_utc': trial.utc()}
            ticks = iter(range(0, 10000, 5))
            with patch.object(trial, 'queues'), patch.object(trial, 'snapshot', return_value=baseline), \
                 patch.object(trial, 'probe', side_effect=probe), patch.object(trial, 'restart_sites', return_value=204), \
                 patch.object(trial.time, 'monotonic', side_effect=lambda: next(ticks)):
                result = trial.restart_leg('B2-before', 1, baseline)
            expected = 'observed-recovery' if negative else 'warm-or-unobserved-inconclusive'
            self.assertTrue(all(row['classification'] == expected for row in result['measurements'].values()))
            self.assertTrue(all(row['first_semantic_success_seconds'] > 0 for row in result['measurements'].values()))
            self.assertLessEqual(len(result['samples']), 240)


class TrialAndRestoreTests(OfflineCase):
    def flow(self, fault=None, package_fault=False):
        baseline = self.baseline()
        state = {'sku': 'B2'}
        writes = []
        legs = []
        def snapshot(_capacity, allowed=('B2', 'B3'), deadline=None):
            if state['sku'] not in allowed:
                raise ValueError('wrong-sku')
            return self.snapshot(state['sku'])
        def azure(arguments, timeout=30, deadline=None):
            self.assertEqual(arguments[:3], ['appservice', 'plan', 'update'])
            sku = arguments[arguments.index('--sku') + 1]
            self.assertTrue((trial.DIRECTORY / 'baseline.json').exists())
            if sku == 'B3':
                self.assertTrue((trial.DIRECTORY / 'b3-armed.json').exists())
            writes.append((sku, arguments[arguments.index('--number-of-workers') + 1]))
            state['sku'] = sku
            if sku == 'B3' and fault in ('unknown', 'unknown-stays-b2'):
                if fault == 'unknown-stays-b2':
                    state['sku'] = 'B2'
                raise subprocess.TimeoutExpired('az', 600)
            return {'name': trial.PLAN, 'sku': sku, 'capacity': 1}
        def leg(name, *_args):
            legs.append(name)
            if name == 'B3' and fault == 'health':
                raise ValueError('health-needle-secret')
            if name == 'B3' and fault == 'signal':
                raise InterruptedError('signal-secret')
            return {'leg': name}
        with patch.object(trial, 'queues'), patch.object(trial, 'snapshot', side_effect=snapshot), \
             patch.object(trial, 'azure', side_effect=azure), patch.object(trial, 'packages', return_value=baseline['packages']), \
             patch.object(trial, 'configurations', return_value=baseline['configurations']), \
             patch.object(trial, 'functionality', return_value=[]), patch.object(trial, 'restart_leg', side_effect=leg), \
             patch.object(trial, 'telemetry', side_effect=ValueError('metric-secret') if package_fault else None), \
             patch.object(trial, 'activity'):
            if fault or package_fault:
                with self.assertRaises(ValueError):
                    trial.run_trial(1)
            else:
                trial.run_trial(1)
        return state, writes, legs, json.loads((trial.DIRECTORY / 'result.json').read_bytes())

    def test_success_is_exactly_two_preserved_count_writes_and_three_matched_legs(self):
        state, writes, legs, result = self.flow()
        self.assertEqual(writes, [('B3', '1'), ('B2', '1')])
        self.assertEqual(legs, ['B2-before', 'B3', 'B2-after'])
        self.assertEqual(state['sku'], 'B2')
        self.assertIsNone(result['restore_error'])

    def test_unknown_upscale_is_not_retried_and_reconciled_b3_is_restored(self):
        state, writes, legs, result = self.flow('unknown')
        self.assertEqual(writes, [('B3', '1'), ('B2', '1')])
        self.assertEqual(legs, ['B2-before'])
        self.assertEqual(state['sku'], 'B2')
        self.assertEqual(result['trial_error'], 'ValueError')
        self.assertNotIn('secret', json.dumps(result))

    def test_unknown_pending_b3_cannot_be_closed_by_an_old_b2_snapshot(self):
        _state, writes, _legs, result = self.flow('unknown-stays-b2')
        self.assertEqual(writes, [('B3', '1')])
        self.assertIsNotNone(result['restore_error'])
        self.assertFalse(any(row['kind'] == 'b2-restoration-verified' for row in trial.EVENTS))

    def test_health_failure_or_signal_stops_more_rounds_and_restores(self):
        for fault in ('health', 'signal'):
            with self.subTest(fault=fault):
                _state, writes, legs, result = self.flow(fault)
                self.assertEqual(writes, [('B3', '1'), ('B2', '1')])
                self.assertEqual(legs, ['B2-before', 'B3'])
                self.assertIsNone(result['restore_error'])

    def test_missing_baseline_artifact_rejects_before_any_write(self):
        self.baseline()
        with patch.dict(os.environ, {'BASELINE_ARTIFACT_ID': ''}), patch.object(trial, 'resize') as resize:
            with self.assertRaises(ValueError):
                trial.run_trial(1)
            resize.assert_not_called()
        self.process.assert_not_called()

    def test_restore_is_independent_of_packages_metrics_health_and_baseline_file(self):
        state = {'sku': 'B3'}
        def snapshot(_count, allowed=('B2', 'B3'), deadline=None):
            return self.snapshot(state['sku'])
        def resize(sku, *_args, **_kwargs):
            state['sku'] = sku
        with patch.object(trial, 'queues'), patch.object(trial, 'snapshot', side_effect=snapshot), \
             patch.object(trial, 'resize', side_effect=resize), patch.object(trial, 'packages', side_effect=AssertionError('packages must not gate restore')), \
             patch.object(trial, 'telemetry', side_effect=AssertionError('metrics must not gate restore')), \
             patch.object(trial, 'functionality', side_effect=AssertionError('health must not gate restore')):
            self.assertEqual(trial.restore(1)['plan']['sku'], 'B2')

    def test_already_b2_restore_has_no_extra_update_or_restart(self):
        with patch.object(trial, 'queues'), patch.object(trial, 'snapshot', return_value=self.snapshot()), \
             patch.object(trial, 'resize') as resize, patch.object(trial, 'restart_sites') as restart:
            self.assertEqual(trial.restore(1)['plan']['sku'], 'B2')
            resize.assert_not_called()
            restart.assert_not_called()

    def test_optional_metrics_failure_occurs_after_verified_b2(self):
        state, writes, _legs, result = self.flow(package_fault=True)
        self.assertEqual(state['sku'], 'B2')
        self.assertEqual(writes[-1], ('B2', '1'))
        self.assertIsNone(result['restore_error'])
        self.assertEqual(result['verification_errors'][0]['phase'], 'metrics')

    def test_configuration_drift_blocks_restore_overwrite(self):
        baseline = self.snapshot()
        drifted = copy.deepcopy(baseline)
        drifted['plan']['configuration_sha256'] = '0' * 64
        with patch.object(trial, 'queues'), patch.object(trial, 'snapshot', return_value=drifted), \
             patch.object(trial, 'resize') as resize, self.assertRaises(ValueError):
            trial.restore(1, baseline)
        resize.assert_not_called()


class EvidenceTests(OfflineCase):
    def test_probe_uses_fixed_bounded_get_and_withholds_unexpected_body(self):
        def respond(arguments, **kwargs):
            self.assertEqual(arguments[0], 'curl')
            self.assertIn('--disable', arguments)
            self.assertEqual(arguments[arguments.index('--max-redirs') + 1], '0')
            self.assertEqual(arguments[arguments.index('--retry') + 1], '0')
            self.assertEqual(arguments[arguments.index('--max-time') + 1], '5')
            self.assertEqual(arguments[-1], trial.URLS['mobile-health'])
            Path(arguments[arguments.index('--output') + 1]).write_bytes(b'credential-needle traceback')
            kwargs['stdout'].write(b'{"status":500,"ttfb":0.1,"total":0.2,"redirects":0}')
            return subprocess.CompletedProcess(arguments, 0)
        self.process.side_effect = respond
        result = trial.probe('mobile-health')
        self.assertFalse(result['healthy'])
        self.assertEqual(result['status'], 500)
        self.assertEqual(result['curl_exit_code'], 0)
        self.assertNotIn('credential-needle', json.dumps(result))
        self.assertEqual(len(result['body_sha256']), 64)

    def test_metrics_preserve_nulls_and_hash_instance_identity(self):
        started = trial.utc()
        metrics = [{'name': name, 'unit': unit, 'errorCode': None,
            'series': [{'dimensions': [{'name': 'Instance', 'value': 'opaque-worker-needle'}],
                        'data': [{'timestamp': started, 'average': None, 'maximum': None}]}]}
            for name, unit in (('CpuPercentage', 'Percent'), ('MemoryPercentage', 'Percent'), ('HttpQueueLength', 'Count'))]
        with patch.object(trial, 'azure', return_value=metrics):
            trial.telemetry(started)
        raw = (trial.DIRECTORY / 'metrics.json').read_text()
        data = json.loads(raw)
        self.assertIsNone(data['metrics'][0]['series'][0]['data'][0]['average'])
        self.assertNotIn('opaque-worker-needle', raw)
        metrics[0]['series'][0]['data'] *= 124
        with patch.object(trial, 'azure', return_value=metrics), self.assertRaises(ValueError):
            trial.telemetry(started)

    def test_activity_is_fixed_plan_only_and_omits_caller_properties(self):
        started = trial.utc()
        rows = [{'timestamp': started, 'operation': 'Microsoft.Web/serverfarms/write', 'status': 'Succeeded',
                 'resource': trial.plan_id(), 'correlation': '33333333-3333-3333-3333-333333333333',
                 'caller': 'credential-needle', 'properties': {'secret': 'credential-needle'}, 'substatus': 'OK'}]
        with patch.object(trial, 'azure', return_value=rows):
            trial.activity(started)
        raw = (trial.DIRECTORY / 'activity.json').read_text()
        self.assertNotIn('credential-needle', raw)
        self.assertNotIn('"caller"', raw)
        rows[0]['resource'] += '-other'
        with patch.object(trial, 'azure', return_value=rows), self.assertRaises(ValueError):
            trial.activity(started)

    def test_shared_deadline_prevents_another_subprocess(self):
        with patch.object(trial.time, 'monotonic', return_value=100):
            with self.assertRaises(TimeoutError):
                trial.azure(['rest', '--method', 'get'], deadline=99)
        self.process.assert_not_called()

    def test_settle_threads_one_deadline_through_every_snapshot(self):
        baseline = self.snapshot()
        ticks = iter((0, 0, 0, 590, 590, 601, 601, 601, 601, 601))
        deadlines = []
        def snapshot(_count, **kwargs):
            deadlines.append(kwargs['deadline'])
            raise ValueError('still-pending')
        with patch.object(trial.time, 'monotonic', side_effect=lambda: next(ticks)), \
             patch.object(trial, 'snapshot', side_effect=snapshot), self.assertRaises(ValueError):
            trial.settle(1, 'B3', baseline, stop_at=600)
        self.assertTrue(deadlines)
        self.assertTrue(all(deadline == 600 for deadline in deadlines))
        self.process.assert_not_called()

    def test_workflow_keeps_offline_identity_and_recovery_independent(self):
        workflow = (Path(__file__).parents[1] / 'workflows/dev-capacity-trial.yml').read_text()
        offline, manual = workflow.split('  capacity:\n', 1)
        self.assertNotIn('id-token:', offline)
        self.assertNotIn('needs:', manual)
        self.assertLess(manual.index('Validate fixed context'), manual.index('Login to existing DEV identity'))
        self.assertLess(manual.index('Publish immutable baseline'), manual.index('Run one bounded B2 B3 B2 cycle'))
        self.assertIn("if: always() && (inputs.mode == 'restore-b2'", manual)
        self.assertEqual(workflow.count('retention-days: 1'), 2)
        self.assertNotIn('overwrite:', workflow)
        self.assertIn("'deploy-dev' || format('capacity-controls-", workflow)
        for action in __import__('re').findall(r'uses: ([^\s]+)', workflow):
            self.assertRegex(action, r'@[0-9a-f]{40}$')


class FinalBoundaryTests(OfflineCase):
    def test_derived_size_is_recorded_but_does_not_change_invariant_hash(self):
        with patch.object(trial, 'arm', return_value=self.raw_plan(targetWorkerCount=0, targetWorkerSizeId=1)):
            before = trial.read_plan(1)
        with patch.object(trial, 'arm', return_value=self.raw_plan(sku='B3', targetWorkerCount=1, targetWorkerSizeId=2)):
            after = trial.read_plan(1)
        self.assertEqual(before['configuration_sha256'], after['configuration_sha256'])
        self.assertEqual((before['targetWorkerSizeId'], after['targetWorkerSizeId']), (1, 2))
        for value in (2, True, -1, 1.0):
            with patch.object(trial, 'arm', return_value=self.raw_plan(targetWorkerCount=value)), self.assertRaises(ValueError):
                trial.read_plan(1)

    def test_recovery_queue_read_failure_does_not_gate_guarded_b2(self):
        with patch.object(trial, 'queues', side_effect=ValueError('github-unavailable')), \
             patch.object(trial, 'snapshot', return_value=self.snapshot()), patch.object(trial, 'resize') as resize:
            self.assertEqual(trial.restore(1)['plan']['sku'], 'B2')
            resize.assert_not_called()
        self.assertTrue(any(row['kind'] == 'restore-queue-evidence-unavailable' for row in trial.EVENTS))

    def test_already_unhealthy_leg_refuses_restart(self):
        with patch.object(trial, 'queues'), patch.object(trial, 'snapshot', return_value=self.snapshot()), \
             patch.object(trial, 'probe', side_effect=lambda label, *_args: {'probe': label, 'healthy': False}), \
             patch.object(trial, 'restart_sites') as restart, self.assertRaises(ValueError):
            trial.restart_leg('B2-before', 1, self.snapshot())
        restart.assert_not_called()

    def test_pre_boundary_warm_success_is_separate_from_confirmed_recovery(self):
        counts = dict.fromkeys(trial.PROBES, 0)
        now = {'value': 0}
        def probe(label, *_args):
            counts[label] += 1
            if label == 'ssr-root' and counts[label] > 1:
                now['value'] += 20
            return {'probe': label, 'healthy': counts[label] != 3, 'elapsed_seconds': 0.1}
        with patch.object(trial, 'queues'), patch.object(trial, 'snapshot', return_value=self.snapshot()), \
             patch.object(trial, 'probe', side_effect=probe), patch.object(trial, 'restart_sites', return_value=204), \
             patch.object(trial.time, 'monotonic', side_effect=lambda: now['value']):
            result = trial.restart_leg('B2-before', 1, self.snapshot())
        for row in result['measurements'].values():
            self.assertGreater(row['first_post_boundary_success_seconds'], row['first_semantic_success_seconds'])
            self.assertGreater(row['second_post_boundary_success_seconds'], row['first_post_boundary_success_seconds'])

    def test_deadline_retains_negative_samples_and_one_late_healthy_is_unconfirmed(self):
        counts = dict.fromkeys(trial.PROBES, 0)
        now = {'value': 0}
        def probe(label, *_args):
            counts[label] += 1
            count = counts[label]
            if label == 'ssr-root' and count > 1:
                now['value'] += 400 if count == 4 else 100
            return {'probe': label, 'healthy': count == 1 or count >= 4, 'elapsed_seconds': 0.1}
        with patch.object(trial, 'queues'), patch.object(trial, 'snapshot', return_value=self.snapshot()), \
             patch.object(trial, 'probe', side_effect=probe), patch.object(trial, 'restart_sites', return_value=204), \
             patch.object(trial.time, 'monotonic', side_effect=lambda: now['value']), self.assertRaises(ValueError):
            trial.restart_leg('B2-before', 1, self.snapshot())
        result = json.loads((trial.DIRECTORY / 'B2-before-restart.json').read_bytes())
        self.assertTrue(result['right_censored'])
        self.assertTrue(result['deadline_exhausted'])
        self.assertEqual(len(result['samples']), 12)
        self.assertTrue(all(row['confirmations'] == 1 and row['classification'] == 'right-censored-unconfirmed'
                            for row in result['measurements'].values()))


class PendingRecoveryTests(OfflineCase):
    def test_unresolved_original_b2_cannot_be_blindly_reissued_from_old_b3(self):
        trial.save('b2-operation.json', {'operation_state': 'unresolved', 'capacity': 1})
        with patch.object(trial, 'queues'), patch.object(trial, 'snapshot', return_value=self.snapshot('B3')), \
             patch.object(trial, 'resize') as resize, self.assertRaises(ValueError):
            trial.restore(1, self.snapshot())
        resize.assert_not_called()
        self.assertFalse(any(row['kind'] == 'b2-restoration-verified' for row in trial.EVENTS))

    def test_unknown_b2_is_reconciled_by_reads_before_noop_verification(self):
        trial.save('b2-operation.json', {'operation_state': 'unresolved', 'capacity': 1})
        with patch.object(trial, 'queues'), patch.object(trial, 'snapshot', return_value=self.snapshot()), \
             patch.object(trial, 'settle', return_value=self.snapshot()) as settle, patch.object(trial, 'resize') as resize:
            self.assertEqual(trial.restore(1, self.snapshot())['plan']['sku'], 'B2')
        self.assertTrue(settle.call_args.kwargs['unknown'])
        resize.assert_not_called()
        self.assertEqual(json.loads((trial.DIRECTORY / 'b2-operation.json').read_bytes())['operation_state'], 'reconciled-b2-ready')


class HostedReceiptTests(OfflineCase):
    def runtime(self, *, getter='get', setter='update_app_service_plan_with_progress', default_getter='get', getter_source=None):
        import contextlib
        import inspect
        import sys
        import types
        names = ('azure', 'azure.cli', 'azure.cli.command_modules', 'azure.cli.command_modules.appservice',
                 'azure.cli.command_modules.appservice.custom', 'azure.cli.command_modules.appservice.commands',
                 'azure.cli.command_modules.appservice._client_factory', 'azure.cli.core', 'azure.cli.core.commands',
                 'azure.cli.core.commands.arm', 'azure.mgmt', 'azure.mgmt.web', 'azure.mgmt.web.operations')
        modules = {name: types.ModuleType(name) for name in names}
        for name, module in modules.items():
            module.__path__ = []
            if '.' in name:
                parent, child = name.rsplit('.', 1)
                setattr(modules[parent], child, module)
        custom = modules['azure.cli.command_modules.appservice.custom']
        custom.update_app_service_plan = object()
        custom.update_app_service_plan_with_progress = object()
        commands = modules['azure.cli.command_modules.appservice.commands']
        commands.load_command_table = object()
        factory = modules['azure.cli.command_modules.appservice._client_factory']
        factory.cf_plans = object()
        class AzCommandGroup:
            def generic_update_command(self, name, getter_name=default_getter):
                return getter_name
        modules['azure.cli.core.commands'].AzCommandGroup = AzCommandGroup
        getter_callback = object()
        modules['azure.mgmt.web.operations'].AppServicePlansOperations = types.SimpleNamespace(get=getter_callback)
        sources = {
            custom.update_app_service_plan: 'instance.sku; sku_def.name = sku; sku_def.capacity = number_of_workers; return instance',
            custom.update_app_service_plan_with_progress: 'begin_create_or_update(resource_group_name, name, app_service_plan); poller.result()',
            commands.load_command_table: "def load_command_table(self, _):\n    appservice_plan_sdk = CliCommandType(operations_tmpl='azure.mgmt.web.operations#AppServicePlansOperations.{}', client_factory=cf_plans)\n    appservice_custom = CliCommandType(operations_tmpl='azure.cli.command_modules.appservice.custom#{}')\n    with self.command_group('appservice plan', appservice_plan_sdk) as g:\n        g.generic_update_command('update', setter_name='" + setter + "', custom_func_name='update_app_service_plan', setter_arg_name='app_service_plan', setter_type=appservice_custom" + (", getter_name='" + getter + "'" if getter != 'get' else '') + ")\n",
            AzCommandGroup.generic_update_command: 'def generic_update_command(self, name, getter_name=' + repr(default_getter) + '): return getter_name',
            factory.cf_plans: 'def cf_plans(cli_ctx, _): return web_client_factory(cli_ctx).app_service_plans',
            getter_callback: getter_source or 'def get(resource_group_name, name): return existing_plan',
        }
        def command(arguments, **_kwargs):
            if arguments[0] == 'az':
                return b'"2.99.0"'
            self.assertEqual(arguments[:2], ['/opt/az/bin/python3', '-c'])
            output = io.StringIO()
            try:
                with patch.dict(sys.modules, modules), patch.object(inspect, 'getsource', side_effect=sources.__getitem__), contextlib.redirect_stdout(output):
                    exec(arguments[2], {})
            except Exception:
                raise ValueError('bounded-command-failed') from None
            return output.getvalue().encode()
        with patch.object(trial, 'command', side_effect=command):
            return trial.hosted_cli()

    def test_receipt_uses_primary_command_group_method_without_arm_export(self):
        proof = self.runtime()
        self.assertEqual(proof['getter_name'], 'get')
        self.assertTrue(proof['registered_callbacks'])
        for key in ('registration_sha256', 'getter_sha256', 'update_sha256', 'setter_sha256'):
            self.assertRegex(proof[key], r'^[0-9a-f]{64}$')
        self.assertIn('Not exposed', proof['provider_api_version'])

    def test_changed_getter_source_changes_its_receipt(self):
        before = self.runtime()
        after = self.runtime(getter_source='def get(resource_group_name, name): return existing_plan\n')
        self.assertNotEqual(before['getter_sha256'], after['getter_sha256'])
        self.assertEqual(before['registration_sha256'], after['registration_sha256'])

    def test_registered_foreign_getter_or_setter_is_rejected(self):
        for changes in ({'getter': 'list'}, {'setter': 'unexpected_setter'}):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                self.runtime(**changes)
        self.process.assert_not_called()

    def test_changed_default_getter_is_rejected_without_authentication(self):
        with self.assertRaises(ValueError):
            self.runtime(default_getter='list')
        self.process.assert_not_called()


class ReceiptBindingTests(OfflineCase):
    def test_missing_malformed_trial_receipt_rejects_before_authentication(self):
        for expected in ('', 'a' * 63, 'A' * 64, 'a' * 64 + '; az'):
            with self.subTest(expected=expected), patch.dict(os.environ, {'EXPECTED_CLI_RECEIPT': expected}), \
                 patch.object(trial, 'authenticate') as authenticate:
                self.assertEqual(trial.main(['trial', '--validate-only']), 1)
                authenticate.assert_not_called()
        self.process.assert_not_called()

    def test_missing_or_mismatched_receipt_rejects_before_first_restart_or_write(self):
        baseline = self.baseline()
        for expected in ('', 'A' * 64, '0' * 64):
            with self.subTest(expected=expected), patch.dict(os.environ, {'EXPECTED_CLI_RECEIPT': expected}), \
                 patch.object(trial, 'snapshot', return_value=self.snapshot()), \
                 patch.object(trial, 'packages', return_value=baseline['packages']), \
                 patch.object(trial, 'configurations', return_value=baseline['configurations']), \
                 patch.object(trial, 'functionality', return_value=[]), patch.object(trial, 'restart_leg', return_value={'leg': 'synthetic'}) as restart, \
                 patch.object(trial, 'resize') as resize, patch.object(trial, 'telemetry'), patch.object(trial, 'activity'):
                with self.assertRaises(ValueError):
                    trial.run_trial(1)
                restart.assert_not_called()
                resize.assert_not_called()

    def test_matching_complete_receipt_is_logged_before_first_restart(self):
        baseline = self.baseline()
        def restart(name, *_args):
            self.assertTrue(any(row['kind'] == 'cli-receipt-admitted' for row in trial.EVENTS))
            return {'leg': name}
        with patch.object(trial, 'snapshot', return_value=self.snapshot()), \
             patch.object(trial, 'packages', return_value=baseline['packages']), \
             patch.object(trial, 'configurations', return_value=baseline['configurations']), \
             patch.object(trial, 'functionality', return_value=[]), patch.object(trial, 'restart_leg', side_effect=restart), \
             patch.object(trial, 'resize'), patch.object(trial, 'telemetry'), patch.object(trial, 'activity'):
            trial.run_trial(1)
        admitted = next(row for row in trial.EVENTS if row['kind'] == 'cli-receipt-admitted')
        self.assertEqual(admitted['sha256'], trial.fingerprint(CLI_RECEIPT))

    def test_observe_and_restore_ignore_optional_receipt(self):
        with patch.dict(os.environ, {'EXPECTED_CLI_RECEIPT': 'not-a-hash'}):
            self.assertEqual(trial.validate_context('observe', '1'), 1)
            self.assertEqual(trial.validate_context('restore-b2', '1'), 1)
        self.process.assert_not_called()


class ObservationDiagnosticTests(OfflineCase):
    def test_observe_guard_failure_keeps_selected_plan_and_phase_without_mutation(self):
        with patch.object(trial, 'authenticate'), patch.object(trial, 'queues'), \
             patch.object(trial, 'arm', return_value=self.raw_plan(capacity=2)), \
             patch.object(trial, 'inventory') as inventory, patch.object(trial, 'resize') as resize, \
             patch.object(trial, 'restart_sites') as restart:
            self.assertEqual(trial.main(['observe']), 1)
        failure = next(row for row in trial.EVENTS if row['kind'] == 'operation-failed')
        self.assertEqual(failure.get('reason'), 'fixed-plan-state-guard')
        selected = next(row for row in trial.EVENTS if row['kind'] == 'plan-state-selected')
        self.assertEqual((selected['capacity'], selected['workers']), (2, 1))
        self.assertFalse(selected['capacity_matches'])
        self.assertTrue(selected['identity_matches'])
        self.assertEqual([row['phase'] for row in trial.EVENTS if row['kind'] == 'snapshot-phase'], ['plan-read'])
        self.assertNotIn('opaque-value', self.stdout.getvalue())
        self.assertFalse((trial.DIRECTORY / 'baseline.json').exists())
        inventory.assert_not_called()
        resize.assert_not_called()
        restart.assert_not_called()
        self.process.assert_not_called()

    def test_inventory_failure_is_distinguished_after_plan_guard_passes(self):
        with patch.object(trial, 'authenticate'), patch.object(trial, 'queues'), \
             patch.object(trial, 'arm', return_value=self.raw_plan()), \
             patch.object(trial, 'azure', return_value=self.listing(trial.OCCUPANTS[:-1])):
            self.assertEqual(trial.main(['observe']), 1)
        self.assertEqual([row['phase'] for row in trial.EVENTS if row['kind'] == 'snapshot-phase'],
                         ['plan-read', 'occupant-inventory'])
        self.assertEqual(trial.EVENTS[-1].get('reason'), 'seven-occupant-guard')
        self.process.assert_not_called()

    def test_unknown_error_args_are_withheld_even_when_stringification_is_hostile(self):
        class ForeignError(ValueError):
            def __str__(self):
                raise AssertionError('exception must not be stringified')
        for error in (ValueError('credential-needle https://secret.example/config'),
                      ValueError({'credential-needle': 'secret'}), ForeignError('fixed-plan-state-guard')):
            with self.subTest(kind=type(error).__name__), patch.object(trial, 'authenticate'), \
                 patch.object(trial, 'prepare', side_effect=error):
                self.assertEqual(trial.main(['observe']), 1)
            self.assertEqual(trial.EVENTS[-1].get('reason'), 'unclassified')
        self.assertNotIn('credential-needle', self.stdout.getvalue())
        self.assertNotIn('secret.example', self.stdout.getvalue())
        self.process.assert_not_called()

    def test_plan_projection_withholds_unknown_strings_nested_data_and_unsupported_types(self):
        changes = {'id': 'https://credential-needle.example', 'location': 'credential-needle',
                   'sku': 'credential-needle', 'status': ['credential-needle'], 'capacity': True,
                   'workers': 2 ** 64, 'reserved': {'credential-needle': 'secret'},
                   'configuration': {'tags': {'credential-needle': 'secret'}}}
        for response in (self.raw_plan(**changes), ['credential-needle'], 'credential-needle', None):
            with self.subTest(shape=type(response).__name__), patch.object(trial, 'authenticate'), \
                 patch.object(trial, 'queues'), patch.object(trial, 'arm', return_value=response):
                self.assertEqual(trial.main(['observe']), 1)
            selected = next(row for row in reversed(trial.EVENTS) if row['kind'] == 'plan-state-selected')
            self.assertIsNone(selected['capacity'])
            self.assertIsNone(selected['workers'])
            self.assertFalse(selected['identity_matches'])
        self.assertNotIn('credential-needle', self.stdout.getvalue())
        self.assertNotIn('secret', self.stdout.getvalue())
        self.process.assert_not_called()


    def test_location_projection_accepts_only_two_exact_west_europe_strings(self):
        class StringSubclass(str):
            pass
        rejected = ('northeurope', 'WESTEUROPE', 'west europe', 'WestEurope', 'westeurope ', ' West Europe',
                    'West Europe\n', '', 'credential-needle.example', None, True, 1, ['credential-needle'],
                    {'credential-needle': 'secret'}, StringSubclass('westeurope'), StringSubclass('West Europe'))
        for location in rejected:
            with self.subTest(kind=type(location).__name__), patch.object(trial, 'arm', return_value=self.raw_plan(location=location)):
                with self.assertRaisesRegex(ValueError, '^fixed-plan-state-guard$'):
                    trial.read_plan(1)
                observation = trial.EVENTS[-1]
                self.assertEqual(observation.get('location'), 'unrecognized')
                self.assertFalse(observation['location_matches'])
        for location in ('westeurope', 'West Europe'):
            with self.subTest(location=location), patch.object(trial, 'arm', return_value=self.raw_plan(location=location)):
                try:
                    observed = trial.read_plan(1)
                except ValueError:
                    self.fail('documented fixed West Europe representation unexpectedly rejected')
                self.assertEqual(observed['sku'], 'B2')
                self.assertEqual(trial.EVENTS[-1]['location'], location)
                self.assertTrue(trial.EVENTS[-1]['location_matches'])
        self.assertNotIn('northeurope', self.stdout.getvalue())
        self.assertNotIn('credential-needle', self.stdout.getvalue())
        self.process.assert_not_called()

    def test_display_region_does_not_relax_other_fixed_plan_guards(self):
        invalid = ({'id': trial.plan_id() + '-foreign'}, {'name': 'other'}, {'reserved': False},
                   {'capacity': 2}, {'workers': 2}, {'perSiteScaling': True}, {'status': 'Scaling'},
                   {'provisioning': 'Updating'}, {'configuration': {}})
        for change in invalid:
            with self.subTest(field=next(iter(change))), patch.object(trial, 'arm', return_value=self.raw_plan(location='West Europe', **change)):
                with self.assertRaisesRegex(ValueError, '^fixed-plan-state-guard$'):
                    trial.read_plan(1)
        self.process.assert_not_called()


if __name__ == '__main__':
    unittest.main()
