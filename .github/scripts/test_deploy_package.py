import contextlib
import http.client
import hashlib
import importlib.util
import io
import json
import os
import socket
import ssl
import urllib.error
import urllib.request
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
            output = ('AzureCloud' if args[1] == 'cloud' else
                      self.fixture.token if args[1] == 'account' else self.fixture.package_setting)
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
        post = next(r for r in requests if r[0] == 'POST')
        self.assertTrue(post[1].startswith('/api/zipdeploy?isAsync=true&deployer='))
        self.assertEqual(post[2]['Content-Type'], 'application/zip')
        with zipfile.ZipFile(io.BytesIO(post[3])) as archive:
            self.assertEqual(set(archive.namelist()), {ENTRY, 'appsettings.json', 'nested/dependency.dll'})
            self.assertEqual(archive.read(ENTRY), b'application-entry')
        exact = [r for r in requests if r[1] == '/api/deployments/exact-id']
        self.assertEqual(len(exact), 1)
        self.assertFalse(any('/latest' in r[1] for r in requests))
        self.assertIn(['az', 'account', 'get-access-token', '--resource', 'https://appservice.azure.com', '--query', 'accessToken', '-o', 'tsv'], self.az_calls)
        self.assertEqual(requests[0][:2], ('GET', '/api/deployments'))
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
        self.assertEqual([r[0] for r in self.fixture.requests], ['GET', 'POST'])

    def test_refuses_foreign_location_before_an_authenticated_get(self):
        self.fixture.mode = 'foreign-location'
        self.assert_failed_without_secret()
        self.assertEqual([r[0] for r in self.fixture.requests], ['GET', 'POST'])

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
        self.assertEqual(len(self.az_calls), 2)
        self.assertFalse(any(args[1] == 'webapp' for args in self.az_calls))

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


    def test_inspection_workflow_is_fixed_dev_only_and_covered_by_both_ci_filters(self):
        workflow = (self.root / '.github/workflows/diagnose-dev.yml').read_text()
        self.assertIn("github.repository == 'VM-s-Solutions/cleansia' && github.ref == 'refs/heads/master'", workflow)
        self.assertIn('environment: dev-weu', workflow)
        self.assertIn('group: deploy-dev', workflow)
        self.assertIn('cancel-in-progress: false', workflow)
        self.assertEqual(workflow.count('id-token: write'), 1)
        self.assertIn('ref: ${{ github.sha }}', workflow)
        self.assertIn('persist-credentials: false', workflow)
        self.assertIn('retention-days: 1', workflow)
        self.assertIn('if: always()', workflow)
        self.assertIn('actions/checkout@fbc6f3992d24b796d5a048ff273f7fcc4a7b6c09', workflow)
        self.assertIn('azure/login@a641126d1b8aa4d1fa005f4f92df94a3a4c4c906', workflow)
        self.assertIn('actions/upload-artifact@b7c566a772e6b6bfb58ed0dc250532a479d7789f', workflow)
        self.assertEqual([line.strip() for line in workflow.splitlines() if line.startswith('          - ')], [
            '- api-cleansia-partner-weu-dev', '- api-cleansia-admin-weu-dev',
            '- api-cleansia-customer-weu-dev', '- api-cleansia-partner-mobile-weu-dev',
            '- api-cleansia-customer-mobile-weu-dev', '- web-cleansia-customer-weu-dev'])
        inputs = workflow.split('    inputs:\n', 1)[1].split('\npermissions:', 1)[0]
        self.assertEqual([line.strip() for line in inputs.splitlines() if line.startswith('      ') and not line.startswith('       ')], ['prior_run_id:'])
        backend = (self.root / '.github/workflows/backend-ci.yml').read_text()
        for path in ('.github/scripts/deploy_package.py', '.github/scripts/test_deploy_package.py',
                     '.github/workflows/diagnose-dev.yml'):
            self.assertEqual(backend.count("- '" + path + "'"), 2)
        self.assertIn("python3 -m unittest discover -s .github/scripts -p 'test_deploy_package.py' -v", backend)

    def test_workflow_rejects_invalid_run_input_before_authentication(self):
        workflow = (self.root / '.github/workflows/diagnose-dev.yml').read_text()
        validation = workflow.split("python3 - <<'PYVALIDATE'\n", 1)[1].split('          PYVALIDATE', 1)[0]
        validation = '\n'.join(line[10:] for line in validation.splitlines())
        for value, expected in (('1', 0), ('9223372036854775807', 0), ('', 1), ('0', 1),
                                ('１２３', 1), ('1; echo secret', 1), ('1\n2', 1),
                                ('9223372036854775808', 1)):
            with self.subTest(value=value):
                result = subprocess.run([os.sys.executable, '-c', validation],
                                        env={'PRIOR_RUN_ID': value}, capture_output=True, text=True)
                self.assertEqual(result.returncode, expected, result.stderr)
        self.assertLess(workflow.index('Validate prior run identity'), workflow.index('Login to Azure'))



class TransportBoundaryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tls_tmp = tempfile.TemporaryDirectory()
        cls.cert = Path(cls.tls_tmp.name) / 'cert.pem'
        cls.key = Path(cls.tls_tmp.name) / 'key.pem'
        result = subprocess.run(['openssl', 'req', '-x509', '-newkey', 'rsa:2048', '-nodes',
                                 '-keyout', str(cls.key), '-out', str(cls.cert), '-days', '1',
                                 '-subj', '/CN=' + APP + '.scm.azurewebsites.net',
                                 '-addext', 'subjectAltName=DNS:' + APP + '.scm.azurewebsites.net'],
                                capture_output=True)
        if result.returncode:
            raise RuntimeError('Loopback TLS fixture certificate generation failed')

    @classmethod
    def tearDownClass(cls):
        cls.tls_tmp.cleanup()

    def exercise(self, mode, status=401, large=False):
        fixture = ScmFixture()
        observed = []
        class FlushedOutput(io.StringIO):
            flushed = ''
            def flush(self):
                self.flushed = self.getvalue()
                super().flush()
        output = FlushedOutput()
        flushed_before_post = []
        class Handler(BaseHTTPRequestHandler):
            def dispatch(self):
                observed.append((self.command, urllib.parse.urlsplit(self.path).path))
                if self.command == 'POST':
                    flushed_before_post.append(output.flushed)
                reject = (mode == 'preflight' and self.command == 'GET') or (mode == 'early-post' and self.command == 'POST')
                if reject:
                    self.send_response(status)
                    self.send_header('Content-Length', '0')
                    self.send_header('Connection', 'close')
                    self.end_headers()
                    self.close_connection = True
                    return
                body = self.rfile.read(int(self.headers.get('Content-Length', 0)))
                code, headers, body = fixture.respond(self.command, self.path, self.headers, body)
                if mode == 'later-get' and self.command == 'GET' and fixture.upload:
                    self.close_connection = True
                    return
                self.send_response(code)
                for k, v in headers.items():
                    self.send_header(k, v)
                self.send_header('Content-Length', str(len(body)))
                self.end_headers()
                self.wfile.write(body)
            do_GET = dispatch
            do_POST = dispatch
            def log_message(self, *_):
                pass
        server = ThreadingHTTPServer(('127.0.0.1', 0), Handler)
        server.handle_error = lambda *_: None
        context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
        context.load_cert_chain(self.cert, self.key)
        server.socket = context.wrap_socket(server.socket, server_side=True)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        original_connect = socket.create_connection
        original_build = urllib.request.build_opener
        trusted = ssl.create_default_context(cafile=self.cert)
        def connect(address, *args, **kwargs):
            self.assertEqual(address, (APP + '.scm.azurewebsites.net', 443))
            return original_connect(('127.0.0.1', server.server_address[1]), *args, **kwargs)
        def opener(*handlers):
            return original_build(*handlers) if mode == 'untrusted-tls' else original_build(*handlers, urllib.request.HTTPSHandler(context=trusted))
        def az(args, **kwargs):
            value = 'AzureCloud' if args[1] == 'cloud' else TOKEN if args[1] == 'account' else '1'
            return subprocess.CompletedProcess(args, 0, value, 'secret-cli-stderr')
        try:
            with tempfile.TemporaryDirectory() as tmp:
                package = Path(tmp)
                (package / ENTRY).write_bytes(b'application-entry')
                (package / 'dependency.dll').write_bytes(os.urandom(54_000_000 if large else 4096))
                with patch.object(socket, 'create_connection', side_effect=connect), \
                     patch.object(urllib.request, 'build_opener', side_effect=opener), \
                     patch.object(deploy.subprocess, 'run', side_effect=az), \
                     patch.object(deploy.time, 'sleep', return_value=None), \
                     patch.dict(os.environ, {'GITHUB_RUN_ID': '123', 'GITHUB_RUN_ATTEMPT': '1',
                                             'HTTPS_PROXY': 'http://127.0.0.1:1'}), \
                     contextlib.redirect_stdout(output), contextlib.redirect_stderr(output):
                    result = deploy.main([APP, str(package), ENTRY, '--timeout-seconds', '30'])
            self.assertEqual(result, 0 if mode == 'success-large' else 1)
            for intent in flushed_before_post:
                self.assertIn('deployer', intent)
                self.assertIn('entry_sha256', intent)
                self.assertIn('package_sha256', intent)
            if mode == 'success-large':
                rows = [json.loads(line) for line in output.getvalue().splitlines() if line.startswith('{')]
                self.assertEqual(rows[-1]['package_sha256'], hashlib.sha256(fixture.upload).hexdigest())
                self.assertEqual(rows[-1]['package_bytes'], len(fixture.upload))
            self.assertNotIn(TOKEN, output.getvalue())
            self.assertNotIn('secret-cli-stderr', output.getvalue())
            return observed, output.getvalue()
        finally:
            server.shutdown()
            server.server_close()
            thread.join()

    def test_verified_tls_large_upload_still_streams_and_verifies_exact_bytes(self):
        observed, output = self.exercise('success-large', large=True)
        self.assertEqual(sum(method == 'POST' for method, _ in observed), 1)
        self.assertIn('deployment_id', output)

    def test_untrusted_tls_fails_before_any_http_request(self):
        observed, output = self.exercise('untrusted-tls')
        self.assertEqual(observed, [])
        self.assertIn('SSLCertVerificationError', output)
        self.assertIn('ssl_verify_code', output)

    def test_verified_tls_401_preflight_prevents_large_post(self):
        observed, output = self.exercise('preflight', 401, True)
        self.assertEqual(observed, [('GET', '/api/deployments')])
        self.assertIn('scm-auth-preflight', output)
        self.assertIn('not-started', output)

    def test_verified_tls_403_preflight_prevents_large_post(self):
        observed, output = self.exercise('preflight', 403, True)
        self.assertEqual(observed, [('GET', '/api/deployments')])
        self.assertIn('scm-auth-preflight', output)

    def test_verified_tls_early_large_post_rejection_keeps_phase_and_intent(self):
        for status in (401, 403):
            with self.subTest(status=status):
                observed, output = self.exercise('early-post', status, True)
                self.assertEqual(sum(method == 'POST' for method, _ in observed), 1)
                intent = next(json.loads(line) for line in output.splitlines() if line.startswith('{'))
                self.assertRegex(intent['deployer'], r'^cleansia-123-1-' + APP + r'-[0-9a-f]{32}$')
                self.assertIn('entry_sha256', intent)
                self.assertIn('upload-post', output)
                self.assertIn('exception_class', output)
                failure = json.loads(next(line.split('SCM request failed ', 1)[1] for line in output.splitlines()
                                          if line.startswith('::error::SCM request failed ')))
                self.assertEqual(failure['phase'], 'upload-post')
                self.assertEqual(failure['method'], 'POST')
                if failure['exception_class'] == 'URLError':
                    self.assertIn(failure['reason_class'], ('BrokenPipeError', 'ConnectionResetError', 'SSLEOFError'))
                    self.assertIsInstance(failure['errno'], int)
                else:
                    self.assertEqual(failure.get('http_status'), status)
                self.assertIn('elapsed_seconds', output)
                self.assertIn('unknown', output)

    def test_post_ack_later_get_error_is_distinct_from_upload(self):
        observed, output = self.exercise('later-get')
        self.assertEqual(sum(method == 'POST' for method, _ in observed), 1)
        self.assertIn('deployment-list', output)
        self.assertIn('acknowledged', output)
        self.assertIn('RemoteDisconnected', output)


class InspectionTests(unittest.TestCase):
    setUp = DeploymentTests.setUp
    tearDown = DeploymentTests.tearDown

    def inspect(self, mode='success', via_cli=False):
        self.fixture.mode = 'success'
        self.fixture.pointer = '20261015120000.zip'
        run_deployer = 'cleansia-123-1-' + APP + '-' + 'a' * 32
        own = {'id': 'old-exact', 'deployer': run_deployer, 'status': 4,
               'complete': True, 'is_temp': False, 'received_time': '2026-10-15T12:00:00Z'}
        rows = [own]
        if mode == 'absent':
            rows = []
        if mode == 'foreign-pending':
            rows = [{'id': 'foreign-pending', 'deployer': 'secret-foreign-deployer', 'status': 1,
                     'complete': False, 'is_temp': True}]
        if mode == 'ambiguous':
            rows.append(dict(own, id='another-final'))
        if mode == 'prefix-imposter':
            rows = [dict(own, deployer=run_deployer + '-extra')]
        if mode == 'unsafe-id':
            rows = [dict(own, id='../secret-unsafe-id')]
        if mode == 'unsafe-pointer':
            self.fixture.mode = mode
        if mode == 'credential-id':
            rows = [dict(own, id=TOKEN)]
        if mode == 'credential-pointer':
            self.fixture.pointer = TOKEN + '.zip'
        original = lambda *args: ScmFixture.respond(self.fixture, *args)
        pointers = []
        def respond(method, path, headers, body):
            if headers.get('Authorization') == 'Bearer secret-default-token':
                self.fixture.requests.append((method, path, dict(headers), body))
                return 401, {}, b'secret-response-body'
            parsed = urllib.parse.urlsplit(path)
            if parsed.path == '/api/deployments' or parsed.path.startswith('/api/deployments/'):
                self.fixture.requests.append((method, path, dict(headers), body))
                if mode == 'redirect':
                    return 302, {'Location': 'https://other.invalid/?secret-redirect'}, b''
                result = rows if parsed.path == '/api/deployments' else next((row for row in rows if row['id'] == parsed.path.rsplit('/', 1)[1]), {})
                if mode == 'changed-identity' and isinstance(result, dict):
                    result = dict(result, deployer='secret-changed-deployer')
                return 200, {}, json.dumps(result).encode()
            if mode == 'pointer-race' and parsed.path.endswith('/packagename.txt'):
                pointers.append(1)
                if len(pointers) > 1:
                    self.fixture.requests.append((method, path, dict(headers), body))
                    return 200, {}, b'changed.zip'
            return original(method, path, headers, body)
        self.fixture.respond = respond
        self.fixture.upload = b'active-archive'
        def az(args, **kwargs):
            self.az_calls.append(args)
            output = 'AzureCloud' if args[1] == 'cloud' else TOKEN if '--resource' in args else 'secret-default-token'
            return subprocess.CompletedProcess(args, 0, output, 'secret-cli-stderr')
        with patch.object(deploy.subprocess, 'run', side_effect=az), \
             contextlib.redirect_stdout(self.output), contextlib.redirect_stderr(self.output):
            if via_cli:
                target = Path(self.tmp.name) / 'evidence.json'
                code = deploy.main([APP, '--inspect-prior-run-id', '123', '--output', str(target),
                                    '--timeout-seconds', '10'])
                result = json.loads(target.read_text())
                self.assertEqual(code, 0 if result['collection_result'] == 'complete' else 1)
            else:
                result = deploy.inspect_dev(APP, '123', 10)
        rendered = json.dumps(result) + self.output.getvalue()
        for secret in (TOKEN, 'secret-default-token', 'secret-cli-stderr', 'secret-response-body',
                       'secret-foreign-deployer', 'secret-changed-deployer', 'secret-unsafe-id', 'secret-redirect'):
            self.assertNotIn(secret, rendered)
        self.assertTrue(self.fixture.requests)
        self.assertTrue(all(method == 'GET' and not body for method, _, _, body in self.fixture.requests))
        return result

    def test_compares_audiences_then_collects_exact_history_and_separate_uncompared_hashes(self):
        result = self.inspect()
        self.assertEqual([event.get('http_status') for event in result['requests'][:2]], [401, 200])
        self.assertEqual(result['historical_state'], 'matched-terminal-success')
        self.assertEqual(len(result['matches']), 1)
        self.assertEqual(result['matches'][0]['deployer'], 'cleansia-123-1-' + APP + '-' + 'a' * 32)
        self.assertEqual(result['details'][0]['identity_matches'], True)
        self.assertEqual(result['active']['observation'], 'stable-uncompared')
        self.assertEqual(result['active']['archive']['bytes'], len(b'active-archive'))
        self.assertEqual(result['active']['entry']['bytes'], len(b'application-entry'))
        self.assertNotIn('ready', json.dumps(result))
        self.assertNotIn('safe_to_upload', result)

    def test_cli_inspection_writes_actual_sanitized_collection_artifact(self):
        result = self.inspect(via_cli=True)
        self.assertEqual(result['historical_state'], 'matched-terminal-success')
        self.assertEqual(result['collection_result'], 'complete')

    def test_cli_auth_failure_still_writes_partial_artifact(self):
        target = Path(self.tmp.name) / 'evidence.json'
        with patch.object(deploy.subprocess, 'run', return_value=subprocess.CompletedProcess([], 9, 'secret-cli-stdout', 'secret-cli-stderr')), \
             contextlib.redirect_stdout(self.output), contextlib.redirect_stderr(self.output):
            code = deploy.main([APP, '--inspect-prior-run-id', '123', '--output', str(target)])
        self.assertEqual(code, 1)
        result = json.loads(target.read_text())
        self.assertEqual(result['collection_result'], 'partial')
        self.assertNotIn('secret-cli', target.read_text() + self.output.getvalue())
        self.assertEqual(self.fixture.requests, [])

    def test_absent_or_imposter_prefix_never_proves_no_prior_upload(self):
        for mode in ('absent', 'prefix-imposter'):
            with self.subTest(mode=mode):
                result = self.inspect(mode)
                self.assertEqual(result['historical_state'], 'unverified')
                self.assertEqual(result['matches'], [])

    def test_foreign_pending_is_collected_even_without_prior_prefix(self):
        result = self.inspect('foreign-pending')
        self.assertEqual(result['historical_state'], 'pending-or-temporary')
        self.assertEqual(result['pending_or_temporary'][0]['id'], 'foreign-pending')
        self.assertEqual(result['pending_or_temporary'][0]['source'], 'foreign')
        self.assertIsNone(result['pending_or_temporary'][0]['deployer'])

    def test_ambiguous_or_changed_exact_identity_is_unverified(self):
        self.assertEqual(self.inspect('ambiguous')['historical_state'], 'ambiguous')
        self.assertEqual(self.inspect('changed-identity')['historical_state'], 'unverified')

    def test_unsafe_ids_pointers_redirects_and_pointer_race_fail_closed(self):
        for mode in ('unsafe-id', 'unsafe-pointer', 'redirect', 'pointer-race'):
            with self.subTest(mode=mode):
                result = self.inspect(mode)
                self.assertEqual(result['collection_result'], 'partial')
                if mode == 'pointer-race':
                    self.assertFalse(result['active']['pointer_stable'])
                self.assertFalse(any('/../' in path or 'other.invalid' in path for _, path, _, _ in self.fixture.requests))

    def test_credential_needle_in_otherwise_valid_id_or_pointer_is_never_persisted(self):
        for mode in ('credential-id', 'credential-pointer'):
            with self.subTest(mode=mode):
                self.assertEqual(self.inspect(mode)['collection_result'], 'partial')

    def test_streamed_inspection_hashes_enforce_package_size_limit(self):
        with patch.object(deploy, 'MAX_PACKAGE_BYTES', 8):
            result = self.inspect()
        self.assertEqual(result['collection_result'], 'partial')
        self.assertNotIn('archive', result['active'])
        self.assertEqual(result['active']['observation'], 'unverified')

    def test_unknown_cloud_fails_before_scm_requests(self):
        with patch.object(deploy.subprocess, 'run', return_value=subprocess.CompletedProcess([], 0, 'UnknownCloud', '')):
            result = deploy.inspect_dev(APP, '123', 10)
        self.assertEqual(result['collection_result'], 'partial')
        self.assertEqual(self.fixture.requests, [])

    def test_rejects_non_numeric_run_or_non_fixed_dev_app_before_cli_or_network(self):
        with patch.object(deploy.subprocess, 'run') as az:
            for app, run in ((APP, '123;echo secret'), (APP, '１２３'), (APP, '0'),
                             (APP, '9223372036854775808'), ('api-cleansia-partner-weu-pro', '123'),
                             ('api-cleansia-partner-neu-dev', '123')):
                with self.subTest(app=app, run=run), self.assertRaises(deploy.DeploymentError):
                    deploy.inspect_dev(app, run, 10)
            az.assert_not_called()
        self.assertEqual(self.fixture.requests, [])

    def test_auth_cli_failure_is_credential_free_and_never_uses_scm(self):
        with patch.object(deploy.subprocess, 'run', return_value=subprocess.CompletedProcess([], 9, 'secret-cli-stdout', 'secret-cli-stderr')):
            result = deploy.inspect_dev(APP, '123', 10)
        self.assertEqual(result['collection_result'], 'partial')
        self.assertEqual(self.fixture.requests, [])
        self.assertNotIn('secret-cli', json.dumps(result))

    def test_transport_reason_text_is_omitted_without_mutation(self):
        opener = unittest.mock.Mock()
        opener.open.side_effect = urllib.error.URLError(OSError(32, 'secret-error-url-and-token'))
        with patch.object(deploy.subprocess, 'run', return_value=subprocess.CompletedProcess([], 0, 'AzureCloud', '')), \
             patch.object(deploy.urllib.request, 'build_opener', return_value=opener):
            result = deploy.inspect_dev(APP, '123', 10)
        self.assertEqual(result['collection_result'], 'partial')
        self.assertTrue(all(call.args[0].get_method() == 'GET' for call in opener.open.call_args_list))
        self.assertNotIn('secret-error', json.dumps(result))
        self.assertEqual(result['requests'][0]['errno'], 32)


class AdditionalContracts(unittest.TestCase):
    def exercise(self, transform, expected=1, expected_posts=1):
        test = DeploymentTests('test_uploads_right_root_binary_zip_and_pins_own_final_deployment')
        test.setUp()
        try:
            original = test.fixture.respond
            test.fixture.respond = lambda *args: transform(test, original, *args)
            test.server.handle_error = lambda *_args: None
            code = test.run_deploy()
            self.assertEqual(code, expected, test.output.getvalue())
            self.assertEqual(sum(request[0] == 'POST' for request in test.fixture.requests), expected_posts)
            self.assertNotIn(TOKEN, test.output.getvalue())
            self.assertNotIn('must-not-print', test.output.getvalue())
            return {'exit_code': code, 'posts': sum(request[0] == 'POST' for request in test.fixture.requests),
                    'requests': [request[1].split('?')[0] for request in test.fixture.requests],
                    'safe_output': test.output.getvalue()}
        finally:
            test.tearDown()

    def test_lost_upload_ack_is_failure_with_exactly_one_post(self):
        def transform(test, original, method, path, headers, body):
            response = original(method, path, headers, body)
            if method == 'POST':
                raise ConnectionResetError('upload accepted, acknowledgement lost')
            return response
        result = self.exercise(transform)
        self.assertIn('\"upload_state\": \"unknown\"', result['safe_output'])

    def test_success_status_without_complete_true_cannot_pass(self):
        def transform(test, original, method, path, headers, body):
            status, response_headers, data = original(method, path, headers, body)
            if path == '/api/deployments/exact-id':
                value = json.loads(data)
                value['complete'] = False
                data = json.dumps(value).encode()
            return status, response_headers, data
        result = self.exercise(transform)
        self.assertFalse(any('/api/vfs/' in path for path in result['requests']))

    def test_two_matching_final_records_fail_before_verification(self):
        def transform(test, original, method, path, headers, body):
            status, response_headers, data = original(method, path, headers, body)
            if path == '/api/deployments' and test.fixture.discovery_count > 1:
                value = json.loads(data)
                value.append({**value[-1], 'id': 'second-final'})
                data = json.dumps(value).encode()
            return status, response_headers, data
        result = self.exercise(transform)
        self.assertIn('identity is ambiguous', result['safe_output'])
        self.assertFalse(any('/api/vfs/' in path for path in result['requests']))

    def test_pointer_switch_after_both_hashes_fails(self):
        reads = 0
        def transform(test, original, method, path, headers, body):
            nonlocal reads
            status, response_headers, data = original(method, path, headers, body)
            if path == '/api/vfs/data/SitePackages/packagename.txt':
                reads += 1
                if reads == 2:
                    data = b'other-package.zip'
            return status, response_headers, data
        result = self.exercise(transform)
        self.assertIn('active package changed', result['safe_output'])
        self.assertEqual(reads, 2)

    def test_malformed_status_json_is_red_without_raw_body(self):
        def transform(test, original, method, path, headers, body):
            response = original(method, path, headers, body)
            if path == '/api/deployments':
                return 200, {}, b'must-not-print'
            return response
        self.exercise(transform, expected_posts=0)

    def test_same_origin_redirect_is_rejected_without_second_post(self):
        def transform(test, original, method, path, headers, body):
            response = original(method, path, headers, body)
            if method == 'POST':
                return 307, {'Location': ORIGIN + '/api/zipdeploy?token=must-not-print'}, b''
            return response
        result = self.exercise(transform)
        self.assertEqual(len(result['requests']), 2)


class DeadlineTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tls_tmp = tempfile.TemporaryDirectory()
        cls.cert = Path(cls.tls_tmp.name) / 'cert.pem'
        cls.key = Path(cls.tls_tmp.name) / 'key.pem'
        result = subprocess.run(['openssl', 'req', '-x509', '-newkey', 'rsa:2048', '-nodes',
                                 '-keyout', str(cls.key), '-out', str(cls.cert), '-days', '1',
                                 '-subj', '/CN=' + APP + '.scm.azurewebsites.net',
                                 '-addext', 'subjectAltName=DNS:' + APP + '.scm.azurewebsites.net'],
                                capture_output=True)
        if result.returncode:
            raise RuntimeError('Loopback deadline TLS certificate generation failed')

    @classmethod
    def tearDownClass(cls):
        cls.tls_tmp.cleanup()

    def exercise_deadline(self, kind, headers=False, via_cli=False):
        import signal
        import time
        observed = []
        output = io.StringIO()
        body = b'[                  ]' if kind == 'json' else b'20261015120000.zip' if kind == 'pointer' else b'x' * 20
        gap = 0.15 if via_cli else 0.05
        class Handler(BaseHTTPRequestHandler):
            def do_GET(self):
                observed.append((self.command, urllib.parse.urlsplit(self.path).path))
                try:
                    if headers:
                        self.wfile.write(b'HTTP/1.1 200 OK\r\nX-Delay: ')
                        self.wfile.flush()
                        for _ in range(20):
                            self.wfile.write(b'x')
                            self.wfile.flush()
                            time.sleep(gap)
                        self.wfile.write(b'\r\nContent-Length: ' + str(len(body)).encode() + b'\r\n\r\n' + body)
                        self.wfile.flush()
                    else:
                        self.send_response(200)
                        self.send_header('Content-Length', str(len(body)))
                        self.end_headers()
                        for byte in body:
                            self.wfile.write(bytes([byte]))
                            self.wfile.flush()
                            time.sleep(gap)
                except OSError:
                    pass
            def log_message(self, *_):
                pass
        server = ThreadingHTTPServer(('127.0.0.1', 0), Handler)
        context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
        context.load_cert_chain(self.cert, self.key)
        server.socket = context.wrap_socket(server.socket, server_side=True)
        server_thread = threading.Thread(target=lambda: server.serve_forever(poll_interval=0.01), daemon=True)
        server_thread.start()
        original_connect = socket.create_connection
        original_build = urllib.request.build_opener
        trusted = ssl.create_default_context(cafile=self.cert)
        def connect(address, *args, **kwargs):
            self.assertEqual(address, (APP + '.scm.azurewebsites.net', 443))
            return original_connect(('127.0.0.1', server.server_address[1]), *args, **kwargs)
        def opener(*handlers):
            return original_build(*handlers, urllib.request.HTTPSHandler(context=trusted))
        def az(args, **kwargs):
            return subprocess.CompletedProcess(args, 0, 'AzureCloud' if args[1] == 'cloud' else TOKEN,
                                               'secret-deadline-cli-stderr')
        old_handler = signal.getsignal(signal.SIGALRM)
        events = []
        budget = 1 if via_cli else 0.3
        try:
            with patch.object(socket, 'create_connection', side_effect=connect), \
                 patch.object(urllib.request, 'build_opener', side_effect=opener), \
                 patch.object(deploy.subprocess, 'run', side_effect=az), \
                 contextlib.redirect_stdout(output), contextlib.redirect_stderr(output):
                started = time.monotonic()
                if via_cli:
                    with tempfile.TemporaryDirectory() as tmp:
                        target = Path(tmp) / 'evidence.json'
                        code = deploy.main([APP, '--inspect-prior-run-id', '123', '--output', str(target),
                                            '--timeout-seconds', '1'])
                        report = json.loads(target.read_text())
                    self.assertEqual(code, 1)
                    self.assertEqual(report['collection_result'], 'partial')
                    events = report['requests']
                    self.assertNotIn(TOKEN, json.dumps(report))
                    self.assertNotIn('secret-deadline-cli-stderr', json.dumps(report))
                else:
                    deadline = started + budget
                    def remaining():
                        value = deadline - time.monotonic()
                        if value <= 0:
                            raise deploy.DeploymentError('Fixture deadline exhausted')
                        return value
                    with self.assertRaises(deploy.DeploymentError):
                        deploy.scm_request(opener(urllib.request.ProxyHandler({}), deploy.NoRedirect()),
                                           APP, TOKEN, '/api/deployments', 'scm-auth-preflight', remaining,
                                           kind=kind, events=events)
                elapsed = time.monotonic() - started
            self.deadline_observation = {'kind': kind, 'headers': headers, 'via_cli': via_cli,
                                         'budget_seconds': budget, 'elapsed_seconds': elapsed,
                                         'requests': observed, 'events': events}
            self.assertLess(elapsed, budget + (0.6 if via_cli else 0.45))
            self.assertEqual(signal.getsignal(signal.SIGALRM), old_handler)
            self.assertEqual(signal.getitimer(signal.ITIMER_REAL), (0.0, 0.0))
            self.assertTrue(observed)
            self.assertTrue(all(method == 'GET' for method, _ in observed))
            self.assertTrue(any(event['outcome'] == 'failed' for event in events))
            self.assertNotIn(TOKEN, output.getvalue())
        finally:
            server.shutdown()
            server.server_close()
            server_thread.join()

    def test_continuous_tls_archive_body_obeys_operation_deadline(self):
        self.exercise_deadline('hash')

    def test_continuous_tls_json_body_obeys_operation_deadline(self):
        self.exercise_deadline('json')

    def test_continuous_tls_pointer_body_obeys_operation_deadline(self):
        self.exercise_deadline('pointer')

    def test_continuous_tls_headers_obey_operation_deadline(self):
        self.exercise_deadline('json', headers=True)

    def test_cli_stream_deadline_still_persists_partial_sanitized_json(self):
        self.exercise_deadline('json', via_cli=True)


if __name__ == '__main__':
    unittest.main()
