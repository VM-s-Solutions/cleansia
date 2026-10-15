#!/usr/bin/env python3
import argparse
import hashlib
import json
import os
import re
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


class DeploymentError(Exception):
    pass


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *_args, **_kwargs):
        return None


def digest(stream):
    return hashlib.file_digest(stream, 'sha256').hexdigest()


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
        if length > 1_000_000_000:
            raise DeploymentError('Package exceeds the App Service ZIP size limit')
        archive.seek(0)
        package_hash = digest(archive)
        archive.seek(0)
        token_result = subprocess.run(
            ['az', 'account', 'get-access-token', '--query', 'accessToken', '-o', 'tsv'],
            capture_output=True, text=True, timeout=remaining(), check=False)
        token = token_result.stdout.strip()
        if token_result.returncode or not token or '\n' in token or '\r' in token:
            raise DeploymentError('Azure OIDC access token is unavailable')
        region = app.rsplit('-', 2)[1]
        mode_result = subprocess.run(
            ['az', 'webapp', 'config', 'appsettings', 'list', '--name', app,
             '--resource-group', f'rg-cleansia-{region}-dev', '--query',
             "[?name=='WEBSITE_RUN_FROM_PACKAGE'].value | [0]", '-o', 'tsv'],
            capture_output=True, text=True, timeout=remaining(), check=False)
        if mode_result.returncode or mode_result.stdout.strip() != '1':
            raise DeploymentError('DEV app is not configured for local immutable package deployment')

        def request(path, data=None):
            headers = {'Authorization': f'Bearer {token}'}
            if data is not None:
                headers.update({'Content-Type': 'application/zip', 'Content-Length': str(length)})
            req = urllib.request.Request(origin + path, data=data, headers=headers)
            try:
                response = opener.open(req, timeout=min(60, remaining()))
            except urllib.error.HTTPError as error:
                raise DeploymentError(f'SCM request failed (HTTP {error.code})') from None
            except (urllib.error.URLError, OSError):
                raise DeploymentError('SCM request failed; deployment outcome is unverified') from None
            remaining()
            return response

        def read_json(path):
            with request(path) as response:
                if response.status != 200:
                    raise DeploymentError('Unexpected SCM status response')
                body = response.read(1_048_577)
            remaining()
            if len(body) > 1_048_576:
                raise DeploymentError('SCM status response exceeds the limit')
            return json.loads(body)

        upload_started = time.monotonic()
        print(json.dumps({'app': app, 'phase': 'upload', 'package_sha256': package_hash,
                          'package_bytes': length, 'file_count': len(files)}, sort_keys=True))
        with request('/api/zipdeploy?isAsync=true&deployer=' + deployer, archive) as response:
            if response.status != 202:
                raise DeploymentError('SCM did not acknowledge an asynchronous ZIP deployment')
            location = urllib.parse.urlsplit(response.headers.get('Location', ''))
        query = urllib.parse.parse_qs(location.query, keep_blank_values=True)
        if (location.scheme != 'https' or location.netloc != f'{app}.scm.azurewebsites.net'
                or location.fragment or not re.fullmatch(r'/api/deployments/[A-Za-z0-9_-]+', location.path)
                or set(query) - {'deployer', 'time'}
                or ('deployer' in query and query['deployer'] != [deployer])):
            raise DeploymentError('SCM returned an unexpected deployment Location')
        uploaded = time.monotonic()
        deployment_id = None
        while deployment_id is None:
            results = read_json('/api/deployments')
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
            result = read_json('/api/deployments/' + deployment_id)
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
        with request(pointer_path) as response:
            pointer = response.read(256).decode('utf-8').strip()
        if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_.-]{0,127}\.zip', pointer):
            raise DeploymentError('SCM package pointer is invalid')
        with request('/api/vfs/data/SitePackages/' + pointer) as response:
            if digest(response) != package_hash:
                raise DeploymentError('SCM active package hash differs from the uploaded ZIP')
        with request('/api/vfs/site/wwwroot/' + entry) as response:
            if digest(response) != entry_hash:
                raise DeploymentError('SCM live entry hash differs from the uploaded artifact')
        with request(pointer_path) as response:
            if response.read(256).decode('utf-8').strip() != pointer:
                raise DeploymentError('SCM active package changed during verification')
        remaining()
        return {'app': app, 'deployment_id': deployment_id, 'package_pointer': pointer,
                'run_from_package': '1', 'deployer': deployer,
                'package_sha256': package_hash, 'entry_sha256': entry_hash, 'file_count': len(files),
                'package_bytes': length, 'upload_seconds': round(uploaded - upload_started, 3),
                'deployment_seconds': round(deployed - uploaded, 3),
                'verification_seconds': round(time.monotonic() - deployed, 3),
                'elapsed_seconds': round(time.monotonic() - started, 3)}


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument('app')
    parser.add_argument('directory')
    parser.add_argument('entry')
    parser.add_argument('--timeout-seconds', type=int, default=600)
    args = parser.parse_args(argv)
    try:
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
