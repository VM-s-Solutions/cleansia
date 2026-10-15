#!/usr/bin/env python3
import argparse
import hashlib
import http.client
import json
import os
import re
import signal
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
import zipfile
from pathlib import Path, PurePosixPath
from datetime import datetime, timezone


class DeploymentError(Exception):
    pass


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *_args, **_kwargs):
        return None


def digest(stream):
    return hashlib.file_digest(stream, 'sha256').hexdigest()


MAX_PACKAGE_BYTES = 1_000_000_000

DEV_ENTRIES = {
    'api-cleansia-partner-weu-dev': 'Cleansia.Web.Partner.dll',
    'api-cleansia-admin-weu-dev': 'Cleansia.Web.Admin.dll',
    'api-cleansia-customer-weu-dev': 'Cleansia.Web.Customer.dll',
    'api-cleansia-partner-mobile-weu-dev': 'Cleansia.Web.Mobile.Partner.dll',
    'api-cleansia-customer-mobile-weu-dev': 'Cleansia.Web.Mobile.Customer.dll',
    'web-cleansia-customer-weu-dev': 'server/server.mjs',
}


def safe_error(error):
    reason = getattr(error, 'reason', None)
    cause = reason if isinstance(reason, BaseException) else error
    result = {'exception_class': type(error).__name__}
    if isinstance(reason, BaseException):
        result['reason_class'] = type(reason).__name__
    for field, name in (('errno', 'errno'), ('verify_code', 'ssl_verify_code')):
        value = getattr(cause, field, None)
        if type(value) is int:
            result[name] = value
    if isinstance(error, urllib.error.HTTPError):
        result['http_status'] = error.code
    return result


def az_value(arguments, remaining, phase):
    started = time.monotonic()
    try:
        result = subprocess.run(['az', *arguments, '-o', 'tsv'], capture_output=True,
                                text=True, timeout=remaining(), check=False)
    except (OSError, subprocess.SubprocessError) as error:
        facts = {'phase': phase, 'method': 'CLI', **safe_error(error),
                 'elapsed_seconds': round(time.monotonic() - started, 3)}
        raise DeploymentError('Azure read failed ' + json.dumps(facts, sort_keys=True)) from None
    if result.returncode:
        raise DeploymentError('Azure read failed ' + json.dumps({'phase': phase, 'method': 'CLI',
                              'exit_code': result.returncode,
                              'elapsed_seconds': round(time.monotonic() - started, 3)}, sort_keys=True))
    value = result.stdout.strip()
    if not value or '\n' in value or '\r' in value:
        raise DeploymentError('Azure read returned no valid scalar for ' + phase)
    remaining()
    return value


def app_service_resource(remaining):
    if az_value(['cloud', 'show', '--query', 'name'], remaining, 'azure-cloud') != 'AzureCloud':
        raise DeploymentError('Only the AzureCloud App Service audience is accepted')
    return 'https://appservice.azure.com'


def scm_request(opener, app, token, path, phase, remaining, *, kind='json', data=None,
                length=0, events=None, audience='app-service', upload_state='not-started'):
    started = time.monotonic()
    facts = {'app': app, 'phase': phase, 'method': 'GET' if data is None else 'POST',
             'audience': audience, 'upload_state': upload_state,
             'started_utc': datetime.now(timezone.utc).isoformat()}
    headers = {'Authorization': f'Bearer {token}'}
    if data is not None:
        headers.update({'Content-Type': 'application/zip', 'Content-Length': str(length)})
    request = urllib.request.Request(f'https://{app}.scm.azurewebsites.net' + path,
                                     data=data, headers=headers)
    def expired(_signum, _frame):
        raise TimeoutError('SCM operation deadline exhausted')

    previous_handler = signal.getsignal(signal.SIGALRM)
    try:
        # An inactivity timeout cannot stop a continuously streaming peer. → /architecture/infrastructure#deploys
        signal.signal(signal.SIGALRM, expired)
        signal.setitimer(signal.ITIMER_REAL, remaining())
        with opener.open(request, timeout=min(60, remaining())) as response:
            facts['http_status'] = response.status
            if kind == 'upload':
                if response.status != 202:
                    raise DeploymentError('SCM did not acknowledge an asynchronous ZIP deployment')
                result = response.headers.get('Location', '')
            else:
                if response.status != 200:
                    raise DeploymentError('Unexpected SCM read response')
                if kind == 'hash':
                    count = 0
                    hasher = hashlib.sha256()
                    while True:
                        remaining()
                        block = response.read(min(65_536, MAX_PACKAGE_BYTES + 1 - count))
                        if not block:
                            break
                        count += len(block)
                        if count > MAX_PACKAGE_BYTES:
                            raise DeploymentError('SCM artifact response exceeds the limit')
                        hasher.update(block)
                    result = {'sha256': hasher.hexdigest(), 'bytes': count}
                    facts['bytes'] = count
                else:
                    limit = 256 if kind == 'pointer' else 1_048_576
                    body = response.read(limit + 1)
                    facts['bytes'] = len(body)
                    if len(body) > limit:
                        raise DeploymentError('SCM read response exceeds the limit')
                    result = body.decode('utf-8').strip() if kind == 'pointer' else json.loads(body)
                    if kind == 'pointer' and (not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_.-]{0,127}\.zip', result)
                                              or token in result):
                        raise DeploymentError('SCM package pointer is invalid')
            remaining()
    except (DeploymentError, OSError, ValueError, urllib.error.URLError, http.client.HTTPException) as error:
        signal.setitimer(signal.ITIMER_REAL, 0)
        if isinstance(error, urllib.error.HTTPError):
            error.close()
        facts.update(safe_error(error), outcome='failed',
                     elapsed_seconds=round(time.monotonic() - started, 3))
        if events is not None:
            events.append(facts)
        raise DeploymentError('SCM request failed ' + json.dumps(facts, sort_keys=True)) from None
    finally:
        signal.setitimer(signal.ITIMER_REAL, 0)
        signal.signal(signal.SIGALRM, previous_handler)
    facts.update(outcome='received', elapsed_seconds=round(time.monotonic() - started, 3))
    if events is not None:
        events.append(facts)
    return result


def deploy(app, directory, entry, budget):
    started = time.monotonic()
    deadline = started + budget

    def remaining():
        value = deadline - time.monotonic()
        if value <= 0:
            raise DeploymentError('Package deployment deadline exhausted')
        return value

    def wait():
        time.sleep(min(3, remaining()))

    if not re.fullmatch(r'(?:api-cleansia-(?:partner|admin|customer|partner-mobile|customer-mobile)|web-cleansia-customer)-[a-z0-9]+-dev', app):
        raise DeploymentError('Only the existing DEV API and customer SSR app names are accepted')
    relative = PurePosixPath(entry)
    if relative.is_absolute() or '..' in relative.parts or not re.fullmatch(r'[A-Za-z0-9_./-]+', entry):
        raise DeploymentError('Invalid package entry path')
    directory = Path(directory)
    if not directory.is_dir() or directory.is_symlink() or not (directory / entry).is_file():
        raise DeploymentError('Package entry is missing')
    files = []
    for path in sorted(directory.rglob('*')):
        if path.is_symlink() or not (path.is_file() or path.is_dir()):
            raise DeploymentError('Package contains a link or nonregular file')
        if path.is_file():
            files.append(path)
    with (directory / entry).open('rb') as source:
        entry_hash = digest(source)
    origin = f'https://{app}.scm.azurewebsites.net'
    deployer = f"cleansia-{os.environ.get('GITHUB_RUN_ID', 'local')}-{os.environ.get('GITHUB_RUN_ATTEMPT', '1')}-{app}-{uuid.uuid4().hex}"
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect())
    with tempfile.TemporaryFile() as archive:
        with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as package:
            for path in files:
                package.write(path, path.relative_to(directory).as_posix())
        length = archive.tell()
        if length > MAX_PACKAGE_BYTES:
            raise DeploymentError('Package exceeds the App Service ZIP size limit')
        archive.seek(0)
        package_hash = digest(archive)
        archive.seek(0)
        resource = app_service_resource(remaining)
        token = az_value(['account', 'get-access-token', '--resource', resource,
                          '--query', 'accessToken'], remaining, 'app-service-token')
        region = app.rsplit('-', 2)[1]
        mode = az_value(['webapp', 'config', 'appsettings', 'list', '--name', app,
                         '--resource-group', f'rg-cleansia-{region}-dev', '--query',
                         "[?name=='WEBSITE_RUN_FROM_PACKAGE'].value | [0]"], remaining, 'package-mode')
        if mode != '1':
            raise DeploymentError('DEV app is not configured for local immutable package deployment')
        upload_state = 'not-started'

        def request(path, phase, **kwargs):
            return scm_request(opener, app, token, path, phase, remaining,
                               upload_state=upload_state, **kwargs)

        preflight = request('/api/deployments', 'scm-auth-preflight')
        if not isinstance(preflight, list):
            raise DeploymentError('SCM preflight deployment list is invalid; upload not started')
        upload_started = time.monotonic()
        print(json.dumps({'app': app, 'phase': 'upload', 'package_sha256': package_hash,
                          'package_bytes': length, 'file_count': len(files), 'deployer': deployer,
                          'entry_sha256': entry_hash}, sort_keys=True), flush=True)
        upload_state = 'unknown'
        location = urllib.parse.urlsplit(request('/api/zipdeploy?isAsync=true&deployer=' + deployer,
                                               'upload-post', kind='upload', data=archive, length=length))
        upload_state = 'acknowledged'
        query = urllib.parse.parse_qs(location.query, keep_blank_values=True)
        if (location.scheme != 'https' or location.netloc != f'{app}.scm.azurewebsites.net'
                or location.fragment or not re.fullmatch(r'/api/deployments/[A-Za-z0-9_-]+', location.path)
                or set(query) - {'deployer', 'time'}
                or ('deployer' in query and query['deployer'] != [deployer])):
            raise DeploymentError('SCM returned an unexpected deployment Location')
        uploaded = time.monotonic()
        deployment_id = None
        while deployment_id is None:
            results = request('/api/deployments', 'deployment-list')
            if not isinstance(results, list):
                raise DeploymentError('SCM deployment list is invalid')
            matching = [item for item in results if isinstance(item, dict) and item.get('deployer') == deployer]
            if any(item.get('status') == 3 for item in matching):
                raise DeploymentError('SCM ZIP deployment failed')
            final = [item for item in matching if item.get('is_temp') is False]
            if len(final) > 1:
                raise DeploymentError('SCM deployment identity is ambiguous')
            if final:
                deployment_id = final[0].get('id')
                if not isinstance(deployment_id, str) or not re.fullmatch(r'[A-Za-z0-9_-]+', deployment_id) or deployment_id == 'latest':
                    raise DeploymentError('SCM deployment identity is invalid')
            else:
                wait()
        while True:
            result = request('/api/deployments/' + deployment_id, 'deployment-detail')
            if not isinstance(result, dict) or result.get('id') != deployment_id or result.get('deployer') != deployer:
                raise DeploymentError('SCM deployment identity changed')
            status = result.get('status')
            if type(status) is not int or status not in (0, 1, 2, 3, 4):
                raise DeploymentError('SCM deployment status is invalid')
            if status == 3:
                raise DeploymentError('SCM ZIP deployment failed')
            if status == 4 and result.get('complete') is True:
                break
            wait()
        deployed = time.monotonic()
        pointer_path = '/api/vfs/data/SitePackages/packagename.txt'
        pointer = request(pointer_path, 'package-pointer-before', kind='pointer')
        if request('/api/vfs/data/SitePackages/' + pointer, 'active-package-hash', kind='hash')['sha256'] != package_hash:
            raise DeploymentError('SCM active package hash differs from the uploaded ZIP')
        if request('/api/vfs/site/wwwroot/' + entry, 'live-entry-hash', kind='hash')['sha256'] != entry_hash:
            raise DeploymentError('SCM live entry hash differs from the uploaded artifact')
        if request(pointer_path, 'package-pointer-after', kind='pointer') != pointer:
            raise DeploymentError('SCM active package changed during verification')
        remaining()
        return {'app': app, 'deployment_id': deployment_id, 'package_pointer': pointer,
                'run_from_package': '1', 'deployer': deployer,
                'package_sha256': package_hash, 'entry_sha256': entry_hash, 'file_count': len(files),
                'package_bytes': length, 'upload_seconds': round(uploaded - upload_started, 3),
                'deployment_seconds': round(deployed - uploaded, 3),
                'verification_seconds': round(time.monotonic() - deployed, 3),
                'elapsed_seconds': round(time.monotonic() - started, 3)}


def inspect_dev(app, prior_run_id, budget):
    if app not in DEV_ENTRIES:
        raise DeploymentError('Inspection accepts only the six fixed weu DEV apps')
    if not re.fullmatch(r'[1-9][0-9]{0,18}', prior_run_id) or int(prior_run_id) > 9_223_372_036_854_775_807:
        raise DeploymentError('Prior DEV run ID must be a positive decimal 64-bit identity')
    if not 1 <= budget <= 600:
        raise DeploymentError('Inspection budget must be within 1–600 seconds')
    deadline = time.monotonic() + budget

    def remaining():
        value = deadline - time.monotonic()
        if value <= 0:
            raise DeploymentError('Inspection deadline exhausted')
        return value

    result = {'app': app, 'entry': DEV_ENTRIES[app], 'prior_run_id': prior_run_id,
              'prior_run_attempt': 1, 'collection_result': 'partial', 'historical_state': 'unverified',
              'matches': [], 'pending_or_temporary': [], 'details': [], 'requests': [],
              'authentication': {}, 'active': {'observation': 'unverified'}}
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect())
    try:
        resource = app_service_resource(remaining)
    except DeploymentError as error:
        result['authentication']['cloud'] = {'available': False, 'error': str(error)}
        return result
    tokens = {}
    for audience in ('default-arm', 'app-service'):
        try:
            if audience == 'app-service':
                tokens[audience] = az_value(['account', 'get-access-token', '--resource', resource,
                                              '--query', 'accessToken'], remaining, 'app-service-token')
            else:
                tokens[audience] = az_value(['account', 'get-access-token', '--query', 'accessToken'],
                                             remaining, 'default-arm-token')
            result['authentication'][audience] = {'available': True}
        except DeploymentError as error:
            result['authentication'][audience] = {'available': False, 'error': str(error)}
    listings = {}
    for audience, token in tokens.items():
        try:
            listings[audience] = scm_request(opener, app, token, '/api/deployments', 'scm-auth-preflight',
                                             remaining, events=result['requests'], audience=audience)
        except DeploymentError:
            pass
    if 'app-service' not in listings:
        return result
    rows = listings['app-service']
    if not isinstance(rows, list):
        return result
    prefix = f'cleansia-{prior_run_id}-1-{app}-'
    complete = True
    selected = []

    def snapshot(item):
        nonlocal complete
        if not isinstance(item, dict):
            complete = False
            return None
        identity = item.get('id')
        status = item.get('status')
        if (not isinstance(identity, str) or not re.fullmatch(r'[A-Za-z0-9_-]{1,128}', identity)
                or identity == 'latest' or any(token in identity for token in tokens.values()) or type(status) is not int or status not in (0, 1, 2, 3, 4)):
            complete = False
            return None
        deployer = item.get('deployer')
        if isinstance(deployer, str) and any(token in deployer for token in tokens.values()):
            complete = False
            return None
        matched = isinstance(deployer, str) and re.fullmatch(re.escape(prefix) + r'[0-9a-f]{32}', deployer) is not None
        known = isinstance(deployer, str) and re.fullmatch(
            r'cleansia-[1-9][0-9]{0,18}-[1-9][0-9]{0,5}-' + re.escape(app) + r'-[0-9a-f]{32}', deployer) is not None
        record = {'id': identity, 'deployer': deployer if known else None,
                  'source': 'prior-run' if matched else 'foreign', 'status': status}
        for field in ('complete', 'is_temp'):
            record[field] = item.get(field) if type(item.get(field)) is bool else None
            if record[field] is None:
                complete = False
        for field in ('received_time', 'start_time', 'end_time', 'last_success_end_time'):
            value = item.get(field)
            record[field] = None
            if isinstance(value, str) and len(value) <= 40:
                try:
                    parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
                    if parsed.tzinfo is not None:
                        record[field] = parsed.astimezone(timezone.utc).isoformat()
                except ValueError:
                    pass
        return record

    for item in rows:
        record = snapshot(item)
        if record is None:
            continue
        if record['source'] == 'prior-run':
            result['matches'].append(record)
        pending = record['is_temp'] is not False or record['complete'] is not True or record['status'] not in (3, 4)
        if pending:
            result['pending_or_temporary'].append(record)
        if record['source'] == 'prior-run' or pending:
            selected.append((record, item.get('deployer')))
    for record, deployer in selected:
        try:
            raw = scm_request(opener, app, tokens['app-service'], '/api/deployments/' + record['id'],
                              'deployment-detail', remaining, events=result['requests'])
            detail = snapshot(raw)
            if detail is None:
                complete = False
                continue
            detail['identity_matches'] = raw.get('id') == record['id'] and raw.get('deployer') == deployer
            if not detail['identity_matches']:
                complete = False
            result['details'].append(detail)
        except DeploymentError:
            complete = False
    final = [row for row in result['matches'] if row['is_temp'] is False]
    if result['pending_or_temporary']:
        result['historical_state'] = 'pending-or-temporary'
    elif len(final) > 1:
        result['historical_state'] = 'ambiguous'
    elif complete and len(final) == 1:
        detail = next((row for row in result['details'] if row['id'] == final[0]['id']), None)
        if detail and detail['identity_matches'] and detail['is_temp'] is False and detail['complete'] is True:
            if detail['status'] == 4:
                result['historical_state'] = 'matched-terminal-success'
            elif detail['status'] == 3:
                result['historical_state'] = 'matched-terminal-failure'
    pointer_path = '/api/vfs/data/SitePackages/packagename.txt'
    active = result['active']
    for phase, path, kind, field in (
            ('package-pointer-before', pointer_path, 'pointer', 'pointer_before'),
            ('active-package-hash', None, 'hash', 'archive'),
            ('live-entry-hash', '/api/vfs/site/wwwroot/' + DEV_ENTRIES[app], 'hash', 'entry'),
            ('package-pointer-after', pointer_path, 'pointer', 'pointer_after')):
        if path is None:
            if 'pointer_before' not in active:
                complete = False
                continue
            path = '/api/vfs/data/SitePackages/' + active['pointer_before']
        try:
            value = scm_request(opener, app, tokens['app-service'], path, phase, remaining,
                                kind=kind, events=result['requests'])
            if kind == 'pointer' and any(token in value for token in tokens.values()):
                complete = False
                continue
            active[field] = value
        except DeploymentError:
            complete = False
    active['pointer_stable'] = ('pointer_before' in active and 'pointer_after' in active
                                and active['pointer_before'] == active['pointer_after'])
    if active['pointer_stable'] and 'archive' in active and 'entry' in active:
        active['observation'] = 'stable-uncompared'
    else:
        complete = False
    result['collection_result'] = 'complete' if complete else 'partial'
    return result


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument('app')
    parser.add_argument('directory', nargs='?')
    parser.add_argument('entry', nargs='?')
    parser.add_argument('--timeout-seconds', type=int, default=600)
    parser.add_argument('--inspect-prior-run-id')
    parser.add_argument('--output')
    args = parser.parse_args(argv)
    try:
        if args.inspect_prior_run_id is not None:
            if args.directory is not None or args.entry is not None or not args.output:
                raise DeploymentError('Inspection requires an output path and accepts no package arguments')
            result = inspect_dev(args.app, args.inspect_prior_run_id, args.timeout_seconds)
            target = Path(args.output)
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(json.dumps(result, sort_keys=True, indent=2) + '\n')
            print(json.dumps({'app': args.app, 'collection_result': result['collection_result'],
                              'historical_state': result['historical_state']}, sort_keys=True), flush=True)
            return 0 if result['collection_result'] == 'complete' else 1
        if args.output is not None or args.directory is None or args.entry is None:
            raise DeploymentError('Deployment requires app, directory and entry')
        if not 1 <= args.timeout_seconds <= 600:
            raise DeploymentError('Package deployment budget must be within 1–600 seconds')
        print(json.dumps(deploy(args.app, args.directory, args.entry, args.timeout_seconds), sort_keys=True))
        return 0
    except DeploymentError as error:
        print(f'::error::{error}', file=sys.stderr)
    except (OSError, ValueError, subprocess.SubprocessError):
        print('::error::Package deployment failed; outcome is unverified', file=sys.stderr)
    return 1


if __name__ == '__main__':
    sys.exit(main())
