import contextlib
import http.client
import importlib.util
import io
import json
import subprocess
import tempfile
import threading
import unittest
import urllib.parse
import zipfile
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from unittest.mock import patch


SCRIPT = Path(__file__).with_name('deploy_package.py')
SPEC = importlib.util.spec_from_file_location('deploy_package', SCRIPT)
deploy = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(deploy)
APP = 'api-cleansia-partner-weu-dev'
ORIGIN = f'https://{APP}.scm.azurewebsites.net'
TOKEN = 'test-bearer-never-print'
ENTRY = 'Cleansia.Web.Partner.dll'


class ScmFixture:
    def __init__(self):
        self.requests = []
        self.upload = b''
        self.deployer = ''
        self.discovery_count = 0
        self.mode = 'success'
        self.pointer = '20261015120000.zip'
        self.clock = 0
        self.app = APP
        self.entry = ENTRY
        self.package_setting = '1'
        self.token = TOKEN

    def respond(self, method, path, headers, body):
        self.requests.append((method, path, dict(headers), body))
        if headers.get('Authorization') != f'Bearer {TOKEN}':
            return 401, {}, b''
        parsed = urllib.parse.urlsplit(path)
        if method == 'POST' and parsed.path == '/api/zipdeploy':
            query = urllib.parse.parse_qs(parsed.query)
            if query.get('isAsync') != ['true'] or not query.get('deployer'):
                return 400, {}, b''
            self.deployer = query['deployer'][0]
            self.upload = body
            if self.mode == 'redirect':
                return 302, {'Location': 'https://other.invalid/?token=must-not-print'}, b''
            location = f'https://{self.app}.scm.azurewebsites.net/api/deployments/latest?deployer={self.deployer}&time=2026-10-15_12-00-00Z'
            if self.mode == 'foreign-location':
                location = 'https://other.invalid/api/deployments/latest?token=must-not-print'
            return 202, {'Location': location}, b''
        if parsed.path == '/api/deployments':
            self.discovery_count += 1
            other = {'id': 'other', 'deployer': 'other-run', 'status': 4, 'complete': True, 'is_temp': False}
            current = {'id': 'temp-01234567', 'deployer': self.deployer, 'status': 0, 'complete': False, 'is_temp': True}
            if self.mode == 'foreign-success':
                return 200, {}, json.dumps([other]).encode()
            if self.mode == 'pending':
                return 200, {}, json.dumps([current]).encode()
            if self.mode == 'temp-failure':
                current['status'] = 3
            elif self.discovery_count > 1:
                current.update(id='exact-id', is_temp=False, status=1)
            return 200, {}, json.dumps([other, current]).encode()
        if parsed.path == '/api/deployments/exact-id':
            result = {'id': 'exact-id', 'deployer': self.deployer, 'status': 4, 'complete': True, 'is_temp': False}
            if self.mode == 'failed':
                result['status'] = 3
            if self.mode == 'wrong-id':
                result['id'] = 'other'
            if self.mode == 'wrong-deployer':
                result['deployer'] = 'other-run'
            if self.mode == 'unknown-state':
                result['status'] = 9
            return 200, {}, json.dumps(result).encode()
        if parsed.path == '/api/vfs/data/SitePackages/packagename.txt':
            pointer = '../escape.zip' if self.mode == 'unsafe-pointer' else self.pointer
            return 200, {}, pointer.encode()
        if parsed.path == f'/api/vfs/data/SitePackages/{self.pointer}':
            return 200, {}, b'wrong-archive' if self.mode == 'wrong-archive' else self.upload
        if parsed.path == f'/api/vfs/site/wwwroot/{self.entry}':
            return 200, {}, b'wrong-entry' if self.mode == 'wrong-entry' else b'application-entry'
        return 404, {}, b''


class DeploymentTests(unittest.TestCase):
    def setUp(self):
        self.fixture = ScmFixture()
        fixture = self.fixture

        class Handler(BaseHTTPRequestHandler):
            def dispatch(self):
                body = self.rfile.read(int(self.headers.get('Content-Length', 0)))
                code, headers, response = fixture.respond(self.command, self.path, self.headers, body)
                self.send_response(code)
                for key, value in headers.items():
                    self.send_header(key, value)
                self.send_header('Content-Length', str(len(response)))
                self.end_headers()
                self.wfile.write(response)

            do_GET = dispatch
            do_POST = dispatch

            def log_message(self, *_):
                pass

        self.server = ThreadingHTTPServer(('127.0.0.1', 0), Handler)
        self.server_thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.server_thread.start()
        port = self.server.server_address[1]
        test = self

        class LoopbackConnection(http.client.HTTPConnection):
            def __init__(self, host, **kwargs):
                test.assertEqual(host, f'{fixture.app}.scm.azurewebsites.net')
                kwargs.pop('context', None)
                kwargs.pop('check_hostname', None)
                super().__init__('127.0.0.1', port, **kwargs)

        self.connection_patch = patch('http.client.HTTPSConnection', LoopbackConnection)
        self.connection_patch.start()
        self.tmp = tempfile.TemporaryDirectory()
        self.package = Path(self.tmp.name) / 'artifact'
        self.package.mkdir()
        (self.package / ENTRY).write_bytes(b'application-entry')
        (self.package / 'appsettings.json').write_text('{}')
        (self.package / 'nested').mkdir()
        (self.package / 'nested' / 'dependency.dll').write_bytes(b'dependency')
        self.output = io.StringIO()
        self.az_calls = []

    def tearDown(self):
        self.connection_patch.stop()
        self.server.shutdown()
        self.server.server_close()
        self.server_thread.join()
        self.tmp.cleanup()

    def run_deploy(self):
        def az_run(args, **kwargs):
            self.az_calls.append(args)
            output = self.fixture.token if args[1] == 'account' else self.fixture.package_setting
            return subprocess.CompletedProcess(args, 0, output + '\n', '')

        def sleep(seconds):
            self.fixture.clock += seconds

        with patch.object(deploy.subprocess, 'run', side_effect=az_run), \
             patch.object(deploy.time, 'monotonic', side_effect=lambda: self.fixture.clock), \
             patch.object(deploy.time, 'sleep', side_effect=sleep), \
             contextlib.redirect_stdout(self.output), contextlib.redirect_stderr(self.output):
            return deploy.main([self.fixture.app, str(self.package), self.fixture.entry, '--timeout-seconds', '10'])

    def assert_failed_without_secret(self):
        self.assertEqual(self.run_deploy(), 1)
        self.assertNotIn(TOKEN, self.output.getvalue())
        self.assertNotIn('must-not-print', self.output.getvalue())

    def test_uploads_right_root_binary_zip_and_pins_own_final_deployment(self):
        self.assertEqual(self.run_deploy(), 0)
        requests = self.fixture.requests
        self.assertEqual(len([r for r in requests if r[0] == 'POST']), 1)
        post = requests[0]
        self.assertTrue(post[1].startswith('/api/zipdeploy?isAsync=true&deployer='))
        self.assertEqual(post[2]['Content-Type'], 'application/zip')
        with zipfile.ZipFile(io.BytesIO(post[3])) as archive:
            self.assertEqual(set(archive.namelist()), {ENTRY, 'appsettings.json', 'nested/dependency.dll'})
            self.assertEqual(archive.read(ENTRY), b'application-entry')
        exact = [r for r in requests if r[1] == '/api/deployments/exact-id']
        self.assertEqual(len(exact), 1)
        self.assertFalse(any('/latest' in r[1] for r in requests))
        self.assertEqual(self.az_calls[0], ['az', 'account', 'get-access-token', '--query', 'accessToken', '-o', 'tsv'])
        self.assertNotIn(TOKEN, self.output.getvalue())
        self.assertIn('package_sha256', self.output.getvalue())
        self.assertIn('exact-id', self.output.getvalue())

    def test_terminal_failure_is_not_retried_or_reported_success(self):
        self.fixture.mode = 'failed'
        self.assert_failed_without_secret()
        self.assertEqual(len([r for r in self.fixture.requests if r[0] == 'POST']), 1)

    def test_matching_temporary_failure_is_terminal(self):
        self.fixture.mode = 'temp-failure'
        self.assert_failed_without_secret()

    def test_foreign_success_cannot_satisfy_this_deployment(self):
        self.fixture.mode = 'foreign-success'
        self.assert_failed_without_secret()
        self.assertLessEqual(self.fixture.clock, 10)
        self.assertFalse(any('/api/vfs/' in r[1] for r in self.fixture.requests))

    def test_pending_exhausts_one_bounded_deadline(self):
        self.fixture.mode = 'pending'
        self.assert_failed_without_secret()
        self.assertEqual(self.fixture.clock, 10)
        self.assertEqual(len([r for r in self.fixture.requests if r[0] == 'POST']), 1)

    def test_refuses_redirect_without_following_or_printing_url(self):
        self.fixture.mode = 'redirect'
        self.assert_failed_without_secret()
        self.assertEqual(len(self.fixture.requests), 1)

    def test_refuses_foreign_location_before_an_authenticated_get(self):
        self.fixture.mode = 'foreign-location'
        self.assert_failed_without_secret()
        self.assertEqual(len(self.fixture.requests), 1)

    def test_exact_status_response_must_keep_id_and_deployer(self):
        for mode in ['wrong-id', 'wrong-deployer', 'unknown-state']:
            with self.subTest(mode=mode):
                self.fixture.mode = mode
                self.assert_failed_without_secret()

    def test_archive_and_live_entry_hash_must_match_uploaded_artifact(self):
        for mode in ['wrong-archive', 'wrong-entry']:
            with self.subTest(mode=mode):
                self.fixture.mode = mode
                self.assert_failed_without_secret()

    def test_pointer_cannot_escape_sitepackages(self):
        self.fixture.mode = 'unsafe-pointer'
        self.assert_failed_without_secret()
        self.assertFalse(any('escape' in r[1] for r in self.fixture.requests))

    def test_missing_entry_fails_before_requesting_token_or_uploading(self):
        (self.package / ENTRY).unlink()
        self.assert_failed_without_secret()
        self.assertEqual(self.az_calls, [])
        self.assertEqual(self.fixture.requests, [])

    def test_symlink_cannot_include_outside_artifact_content(self):
        (self.package / 'outside').symlink_to(Path(self.tmp.name))
        self.assert_failed_without_secret()
        self.assertEqual(self.fixture.requests, [])

    def test_customer_ssr_uses_the_same_contract_and_keeps_server_at_package_root(self):
        self.fixture.app = 'web-cleansia-customer-weu-dev'
        self.fixture.entry = 'server/server.mjs'
        (self.package / 'server').mkdir()
        (self.package / self.fixture.entry).write_bytes(b'application-entry')
        self.assertEqual(self.run_deploy(), 0)
        with zipfile.ZipFile(io.BytesIO(self.fixture.upload)) as archive:
            self.assertIn('server/server.mjs', archive.namelist())

    def test_effective_local_package_mode_is_required_before_upload(self):
        self.fixture.package_setting = ''
        self.assert_failed_without_secret()
        self.assertEqual(self.fixture.requests, [])

    def test_missing_bearer_fails_without_authentication_fallback(self):
        self.fixture.token = ''
        self.assert_failed_without_secret()
        self.assertEqual(self.fixture.requests, [])
        self.assertEqual(len(self.az_calls), 1)

    def test_prod_and_unknown_apps_are_rejected_before_azure_access(self):
        for app in ['api-cleansia-partner-weu-prod', 'other-site-dev']:
            with self.subTest(app=app):
                self.fixture.app = app
                self.assert_failed_without_secret()
        self.assertEqual(self.az_calls, [])
        self.assertEqual(self.fixture.requests, [])


class SourceContractTests(unittest.TestCase):
    root = Path(__file__).resolve().parents[2]

    def test_dev_package_and_api_startup_settings_preserve_prod(self):
        bicep = (self.root / 'deploy/bicep/main.bicep').read_text()
        self.assertRegex(bicep, r"(?s)var devPackageSettings = env == 'prod' \? \{\} : \{\s*WEBSITE_RUN_FROM_PACKAGE: '1'\s*SCM_DO_BUILD_DURING_DEPLOYMENT: 'false'\s*\}")
        self.assertRegex(bicep, r"(?s)var apiStartupSettings = env == 'prod' \? \{\} : \{\s*WEBSITE_WARMUP_PATH: '/health'\s*WEBSITE_WARMUP_STATUSES: '200'\s*\}")
        api = bicep.split('var apiBaseSettings = ', 1)[1].split('\n// FCM', 1)[0]
        self.assertIn('devPackageSettings, apiStartupSettings', api)
        ssr = bicep.split('module ssr ', 1)[1].split('\nmodule ', 1)[0]
        self.assertIn('devPackageSettings', ssr)
        module = (self.root / 'deploy/bicep/modules/appService.bicep').read_text()
        self.assertIn("param healthCheckPath string = '/alive'", module)

    def test_workflow_uses_package_helper_only_on_dev_and_keeps_prod_action(self):
        workflow = (self.root / '.github/workflows/deploy-azure.yml').read_text()
        api = workflow.split('\n  deploy-api:', 1)[1].split('\n  build-and-deploy-functions:', 1)[0]
        ssr = workflow.split('\n  deploy-customer-ssr:', 1)[1].split('\n  warm-dev-sites:', 1)[0]
        for job in [api, ssr]:
            self.assertRegex(job, r"Deploy to Azure App Service\s+if: inputs.env == 'prod'\s+uses: azure/webapps-deploy@02a81bead70021f5284939794bcec79c271ab383")
            self.assertRegex(job, r"(?s)Deploy immutable DEV package\s+if: inputs.env != 'prod'.*?timeout 600s python3 .github/scripts/deploy_package.py")
            self.assertIn("slot-name: ${{ inputs.env == 'prod' && 'staging' || 'production' }}", job)
        self.assertIn('server/server.mjs', ssr)
        for entry in ['Cleansia.Web.Partner.dll', 'Cleansia.Web.Admin.dll', 'Cleansia.Web.Customer.dll',
                      'Cleansia.Web.Mobile.Partner.dll', 'Cleansia.Web.Mobile.Customer.dll']:
            self.assertIn(entry, api)


if __name__ == '__main__':
    unittest.main()
