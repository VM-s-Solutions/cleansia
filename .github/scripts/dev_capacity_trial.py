#!/usr/bin/env python3
import argparse
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone
import hashlib
from html.parser import HTMLParser
import json
import math
import os
from pathlib import Path
import re
import signal
import subprocess
import tempfile
import time
import urllib.parse
import urllib.request

import deploy_package as package


REPOSITORY = 'VM-s-Solutions/cleansia'
GROUP = 'rg-cleansia-weu-dev'
PLAN = 'plan-cleansia-weu-dev'
API = '2024-04-01'
APPS = tuple(package.DEV_ENTRIES)
OCCUPANTS = (*APPS, 'func-cleansia-weu-dev')
ACCEPTED_RUN = '38830931038'
DIRECTORY = Path('dev-capacity-evidence')
EVENTS = []
SUBSCRIPTION = ''
UUID = r'[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}'
PROBES = ('mobile-alive', 'mobile-health', 'ssr-health', 'ssr-root')
FAILURE_REASONS = frozenset((
    'fixed-context-guard', 'reviewed-cli-receipt-required', 'expected-identity-guard', 'azure-identity-guard',
    'bounded-command-failed', 'cli-version-guard', 'hosted-cli-behavior-unverified', 'fixed-plan-state-guard',
    'occupancy-response-guard', 'seven-occupant-guard', 'occupancy-pagination-guard', 'occupancy-pagination-bound',
    'github-token-unavailable', 'master-moved-guard', 'complete-queue-guard', 'concurrent-manual-work-guard',
    'site-config-identity-unavailable', 'functions-image-unverified', 'local-package-mode-guard',
    'stable-deployment-list-guard', 'idle-deployment-guard', 'accepted-package-pointer-guard',
    'functional-readiness-failed', 'resize-arguments-guard'))
URLS = {
    'mobile-alive': f'https://{APPS[4]}.azurewebsites.net/alive',
    'mobile-health': f'https://{APPS[4]}.azurewebsites.net/health',
    'ssr-health': f'https://{APPS[5]}.azurewebsites.net/health',
    'ssr-root': f'https://{APPS[5]}.azurewebsites.net/',
    **{app: f'https://{app}.azurewebsites.net/health' for app in APPS[:5]},
}


def utc():
    return datetime.now(timezone.utc).isoformat()


def encoded(value):
    return json.dumps(value, sort_keys=True, ensure_ascii=True, allow_nan=False).encode()


def fingerprint(value):
    return hashlib.sha256(encoded(value)).hexdigest()


def remaining(deadline, maximum):
    value = maximum if deadline is None else min(maximum, deadline - time.monotonic())
    if value <= 0:
        raise TimeoutError('phase-deadline-exhausted')
    return value


def save(name, value):
    DIRECTORY.mkdir(exist_ok=True)
    target = DIRECTORY / name
    with target.open('wb') as stream:
        stream.write(encoded(value) + b'\n')
        stream.flush()
        os.fsync(stream.fileno())


def event(kind, **facts):
    row = {'kind': kind, 'utc': utc(), **facts}
    EVENTS.append(row)
    DIRECTORY.mkdir(exist_ok=True)
    with (DIRECTORY / 'events.jsonl').open('ab') as stream:
        stream.write(encoded(row) + b'\n')
        stream.flush()
        os.fsync(stream.fileno())
    print(encoded(row).decode(), flush=True)


def validate_context(mode, capacity):
    if (mode not in ('observe', 'trial', 'restore-b2') or capacity not in ('1', '2', '3')
            or os.environ.get('GITHUB_REPOSITORY') != REPOSITORY
            or os.environ.get('GITHUB_REF') != 'refs/heads/master'
            or os.environ.get('GITHUB_EVENT_NAME') != 'workflow_dispatch'
            or re.fullmatch(r'[0-9a-f]{40}', os.environ.get('GITHUB_SHA', '')) is None
            or re.fullmatch(r'[1-9][0-9]{0,18}', os.environ.get('GITHUB_RUN_ID', '')) is None
            or os.environ.get('GITHUB_RUN_ATTEMPT') != '1'):
        raise ValueError('fixed-context-guard')
    if mode == 'trial' and re.fullmatch(r'[0-9a-f]{64}', os.environ.get('EXPECTED_CLI_RECEIPT', '')) is None:
        raise ValueError('reviewed-cli-receipt-required')
    return int(capacity)


def command(arguments, timeout=30, limit=1_048_576, *, stdin=None):
    with tempfile.TemporaryFile() as output:
        result = subprocess.run(arguments, input=stdin, stdout=output,
                                stderr=subprocess.DEVNULL, timeout=timeout, check=False)
        output.seek(0)
        raw = output.read(limit + 1)
    if result.returncode or len(raw) > limit:
        raise ValueError('bounded-command-failed')
    return raw


def azure(arguments, timeout=30, deadline=None):
    return json.loads(command(['az', *arguments, '--subscription', SUBSCRIPTION,
                               '--only-show-errors', '--output', 'json'], remaining(deadline, timeout)))


def base():
    return f'/subscriptions/{SUBSCRIPTION}/resourceGroups/{GROUP}/providers/Microsoft.Web'


def plan_id():
    return base() + '/serverfarms/' + PLAN


def arm(path, query, deadline=None):
    return azure(['rest', '--method', 'get', '--url',
                  'https://management.azure.com' + path + '?api-version=' + API, '--query', query], deadline=deadline)


def authenticate():
    global SUBSCRIPTION
    SUBSCRIPTION = os.environ.get('EXPECTED_SUBSCRIPTION_ID', '')
    tenant = os.environ.get('EXPECTED_TENANT_ID', '')
    if re.fullmatch(UUID, SUBSCRIPTION) is None or re.fullmatch(UUID, tenant) is None:
        raise ValueError('expected-identity-guard')
    account = azure(['account', 'show', '--query', '{id:id,tenant:tenantId,cloud:environmentName}'])
    if (not isinstance(account, dict) or account.get('id', '').lower() != SUBSCRIPTION.lower()
            or account.get('tenant', '').lower() != tenant.lower() or account.get('cloud') != 'AzureCloud'):
        raise ValueError('azure-identity-guard')


def hosted_cli():
    version = json.loads(command(['az', 'version', '--query', '"azure-cli"', '--only-show-errors', '--output', 'json']))
    if not isinstance(version, str) or re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+', version) is None:
        raise ValueError('cli-version-guard')
    receipt_source = r"""
import ast
import hashlib
import inspect
import json
from azure.cli.command_modules.appservice import custom, commands, _client_factory
from azure.cli.core.commands import AzCommandGroup
from azure.mgmt.web.operations import AppServicePlansOperations

source = inspect.getsource(commands.load_command_table)
tree = ast.parse(source)
groups = [node for node in ast.walk(tree) if isinstance(node, ast.With) and any(
    isinstance(item.context_expr, ast.Call) and getattr(item.context_expr.func, 'attr', '') == 'command_group'
    and len(item.context_expr.args) == 2 and isinstance(item.context_expr.args[1], ast.Name)
    and item.context_expr.args[1].id == 'appservice_plan_sdk'
    and isinstance(item.context_expr.args[0], ast.Constant)
    and item.context_expr.args[0].value == 'appservice plan'
    for item in node.items)]
updates = [node.value for group in groups for node in group.body if isinstance(node, ast.Expr)
    and isinstance(node.value, ast.Call) and getattr(node.value.func, 'attr', '') == 'generic_update_command'
    and node.value.args and isinstance(node.value.args[0], ast.Constant) and node.value.args[0].value == 'update']
sdks = [node for node in ast.walk(tree) if isinstance(node, ast.Assign)
    and any(isinstance(target, ast.Name) and target.id == 'appservice_plan_sdk' for target in node.targets)]
customs = [node for node in ast.walk(tree) if isinstance(node, ast.Assign)
    and any(isinstance(target, ast.Name) and target.id == 'appservice_custom' for target in node.targets)]
if len(groups) != 1 or len(updates) != 1 or len(sdks) != 1 or len(customs) != 1:
    raise ValueError('plan-registration-unverified')
keywords = {item.arg: item.value for item in updates[0].keywords}
sdk_keywords = {item.arg: item.value for item in sdks[0].value.keywords}
custom_keywords = {item.arg: item.value for item in customs[0].value.keywords}
def literal(name):
    return ast.literal_eval(keywords[name])
getter = literal('getter_name') if 'getter_name' in keywords else inspect.signature(AzCommandGroup.generic_update_command).parameters['getter_name'].default
registered = (getter == 'get' and literal('setter_name') == 'update_app_service_plan_with_progress'
    and literal('custom_func_name') == 'update_app_service_plan' and literal('setter_arg_name') == 'app_service_plan'
    and isinstance(keywords.get('setter_type'), ast.Name) and keywords['setter_type'].id == 'appservice_custom'
    and 'getter_type' not in keywords
    and ast.literal_eval(sdk_keywords['operations_tmpl']) == 'azure.mgmt.web.operations#AppServicePlansOperations.{}'
    and isinstance(sdk_keywords.get('client_factory'), ast.Name) and sdk_keywords['client_factory'].id == 'cf_plans'
    and ast.literal_eval(custom_keywords['operations_tmpl']) == 'azure.cli.command_modules.appservice.custom#{}')
if not registered:
    raise ValueError('plan-callbacks-unverified')
registration = '\n'.join((ast.get_source_segment(source, groups[0]), ast.get_source_segment(source, sdks[0]),
    ast.get_source_segment(source, customs[0]), inspect.getsource(AzCommandGroup.generic_update_command), inspect.getsource(_client_factory.cf_plans)))
a = inspect.getsource(custom.update_app_service_plan)
b = inspect.getsource(custom.update_app_service_plan_with_progress)
print(json.dumps({'update_sha256': hashlib.sha256(a.encode()).hexdigest(),
    'setter_sha256': hashlib.sha256(b.encode()).hexdigest(),
    'registration_sha256': hashlib.sha256(registration.encode()).hexdigest(),
    'getter_sha256': hashlib.sha256(inspect.getsource(AppServicePlansOperations.get).encode()).hexdigest(),
    'getter_name': getter, 'registered_callbacks': registered,
    'existing_model': 'instance.sku' in a and 'return instance' in a,
    'explicit_sku_count': 'sku_def.name = sku' in a and 'sku_def.capacity = number_of_workers' in a,
    'put_poller': 'begin_create_or_update(resource_group_name, name, app_service_plan)' in b,
    'waits': 'poller.result()' in b or 'LongRunningOperation' in b}))
"""
    proof = json.loads(command(['/opt/az/bin/python3', '-c', receipt_source], limit=4096))
    if (set(proof) != {'update_sha256', 'setter_sha256', 'registration_sha256', 'getter_sha256', 'getter_name',
                      'registered_callbacks', 'existing_model', 'explicit_sku_count', 'put_poller', 'waits'}
            or proof['getter_name'] != 'get'
            or any(proof[key] is not True for key in ('registered_callbacks', 'existing_model', 'explicit_sku_count', 'put_poller', 'waits'))
            or any(re.fullmatch(r'[0-9a-f]{64}', proof[key]) is None
                   for key in ('update_sha256', 'setter_sha256', 'registration_sha256', 'getter_sha256'))):
        raise ValueError('hosted-cli-behavior-unverified')
    return {'version': version, **proof, 'provider_api_version': 'Not exposed by this bounded receipt; no version inferred from CLI version.',
            'http_retry_count': 'SDK defaults; one logical CLI invocation is not one HTTP request'}


def read_plan(capacity, allowed=('B2', 'B3'), require_ready=True, deadline=None):
    event('snapshot-phase', phase='plan-read')
    data = arm(plan_id(), '{id:id,name:name,type:type,location:location,sku:sku.name,tier:sku.tier,'
               'capacity:sku.capacity,workers:properties.numberOfWorkers,reserved:properties.reserved,'
               'perSiteScaling:properties.perSiteScaling,status:properties.status,provisioning:properties.provisioningState,'
               'targetWorkerCount:properties.targetWorkerCount,targetWorkerSizeId:properties.targetWorkerSizeId,'
               'configuration:{kind:kind,tags:tags,identity:identity,location:location,reserved:properties.reserved,'
               'perSiteScaling:properties.perSiteScaling,asyncScalingEnabled:properties.asyncScalingEnabled,'
               'elasticScaleEnabled:properties.elasticScaleEnabled,maximumElasticWorkerCount:properties.maximumElasticWorkerCount,'
               'zoneRedundant:properties.zoneRedundant,hostingEnvironmentProfile:properties.hostingEnvironmentProfile,'
               'workerTierName:properties.workerTierName,isSpot:properties.isSpot,spotExpirationTime:properties.spotExpirationTime,'
               'freeOfferExpirationTime:properties.freeOfferExpirationTime,isXenon:properties.isXenon,hyperV:properties.hyperV,'
               'kubeEnvironmentProfile:properties.kubeEnvironmentProfile,extendedLocation:extendedLocation}}', deadline)
    selected = data if type(data) is dict else {}
    configuration = selected.get('configuration')
    event('plan-state-selected', response_shape='object' if type(data) is dict else 'null' if data is None else 'unsupported',
          identity_matches=type(selected.get('id')) is str and selected['id'].lower() == plan_id().lower(),
          name_matches=type(selected.get('name')) is str and selected['name'] == PLAN,
          resource_type_matches=type(selected.get('type')) is str and selected['type'].lower() == 'microsoft.web/serverfarms',
          location_matches=type(selected.get('location')) is str and selected['location'] in ('westeurope', 'West Europe'),
          location=selected.get('location') if type(selected.get('location')) is str and selected['location'] in ('westeurope', 'West Europe') else 'unrecognized',
          configuration_shape='object' if type(configuration) is dict else 'null' if configuration is None else 'unsupported',
          configuration_nonempty=type(configuration) is dict and bool(configuration),
          capacity_matches=type(selected.get('capacity')) is int and selected['capacity'] == capacity,
          workers_match=type(selected.get('workers')) is int and selected['workers'] == capacity,
          **{key: selected.get(key) if type(selected.get(key)) is int and 0 <= selected[key] <= 2_147_483_647 else None
             for key in ('capacity', 'workers', 'targetWorkerCount', 'targetWorkerSizeId')},
          **{key: selected.get(key) if type(selected.get(key)) is bool else None for key in ('reserved', 'perSiteScaling')},
          **{key: selected.get(key) if selected.get(key) is None or type(selected.get(key)) is str and selected[key] in values
             else 'unrecognized' for key, values in (
                 ('sku', ('B1', 'B2', 'B3')), ('tier', ('Basic',)), ('status', ('Ready', 'Scaling', 'NotReady')),
                 ('provisioning', ('Succeeded', 'Updating', 'Failed', 'Canceled', 'Accepted', 'InProgress', 'Creating', 'Deleting')))})
    if (not isinstance(data, dict) or str(data.get('id', '')).lower() != plan_id().lower()
            or data.get('name') != PLAN or str(data.get('type', '')).lower() != 'microsoft.web/serverfarms'
            or type(data.get('location')) is not str or data.get('location') not in ('westeurope', 'West Europe')
            or data.get('reserved') is not True
            or data.get('perSiteScaling') is not False or data.get('tier') != 'Basic'
            or data.get('sku') not in allowed or type(data.get('capacity')) is not int
            or type(data.get('workers')) is not int or data['capacity'] != capacity or data['workers'] != capacity
            or not isinstance(data.get('configuration'), dict) or not data['configuration']
            or data.get('targetWorkerCount') is not None and (type(data['targetWorkerCount']) is not int
                or data['targetWorkerCount'] not in (0, capacity))
            or data.get('targetWorkerSizeId') is not None and (type(data['targetWorkerSizeId']) is not int
                or not 0 <= data['targetWorkerSizeId'] <= 2_147_483_647)
            or require_ready and (data.get('status') != 'Ready' or data.get('provisioning') not in (None, 'Succeeded'))):
        raise ValueError('fixed-plan-state-guard')
    return {key: data[key] for key in ('name', 'sku', 'tier', 'capacity', 'workers', 'reserved', 'perSiteScaling', 'status', 'provisioning')} | {
        'targetWorkerCount': data.get('targetWorkerCount'), 'targetWorkerSizeId': data.get('targetWorkerSizeId'),
        'derived_scaling_qualification': 'Observed provider state; no numeric SKU mapping inferred and neither target field explicitly assigned.',
        'configuration_sha256': fingerprint(data['configuration'])}


def inventory(deadline=None):
    event('snapshot-phase', phase='occupant-inventory')
    path = plan_id() + '/sites'
    url = 'https://management.azure.com' + path + '?api-version=' + API
    seen = {}
    for _ in range(10):
        data = azure(['rest', '--method', 'get', '--url', url, '--query',
                      '{value:value[].{id:id,name:name,plan:properties.serverFarmId,state:properties.state},nextLink:nextLink}'], deadline=deadline)
        event('inventory-page-selected', page_index=_,
              response_shape={dict: 'object', list: 'array', str: 'string', bool: 'boolean', int: 'integer',
                              float: 'number', type(None): 'null'}.get(type(data), 'unsupported'),
              value_shape='missing' if type(data) is not dict or 'value' not in data else
                  {dict: 'object', list: 'array', str: 'string', bool: 'boolean', int: 'integer',
                   float: 'number', type(None): 'null'}.get(type(data['value']), 'unsupported'),
              value_count=len(data['value']) if type(data) is dict and type(data.get('value')) is list
                  and len(data['value']) <= 100 else None,
              value_within_bound=type(data) is dict and type(data.get('value')) is list and len(data['value']) <= 100,
              next_link_present=type(data) is dict and 'nextLink' in data,
              current_page_known_counts={name: sum(type(row) is dict and type(row.get('name')) is str and row['name'] == name
                  for row in data['value']) if type(data) is dict and type(data.get('value')) is list
                  and len(data['value']) <= 100 else None for name in OCCUPANTS},
              current_page_unrecognized_name_count=sum(type(row) is dict and not
                  (type(row.get('name')) is str and row['name'] in OCCUPANTS) for row in data['value'])
                  if type(data) is dict and type(data.get('value')) is list and len(data['value']) <= 100 else None,
              current_page_nonobject_count=sum(type(row) is not dict for row in data['value'])
                  if type(data) is dict and type(data.get('value')) is list and len(data['value']) <= 100 else None,
              validated_prefix_counts={name: int(name in seen) for name in OCCUPANTS},
              coverage='received-current-page-and-validated-prefix-before-current-page')
        if not isinstance(data, dict) or not isinstance(data.get('value'), list) or len(data['value']) > 100:
            raise ValueError('occupancy-response-guard')
        page_prefix_count = len(seen)
        for item in data['value']:
            if (isinstance(item, dict) and item.get('name') in OCCUPANTS and item['name'] not in seen
                    and str(item.get('id', '')).lower() == (base() + '/sites/' + item['name']).lower()
                    and item.get('plan') is None):
                known_name = OCCUPANTS[OCCUPANTS.index(item['name'])]
                event('inventory-binding-detail-read', known_name=known_name)
                detail = arm(base() + '/sites/' + known_name, '{id:id,name:name,plan:properties.serverFarmId}', deadline)
                event('inventory-binding-detail-selected', known_name=known_name, response_object=type(detail) is dict,
                      name_matches=type(detail) is dict and type(detail.get('name')) is str and detail['name'] == known_name,
                      id_matches=type(detail) is dict and type(detail.get('id')) is str
                          and detail['id'].lower() == (base() + '/sites/' + known_name).lower(),
                      plan_present=type(detail) is dict and 'plan' in detail,
                      plan_shape='missing' if type(detail) is not dict or 'plan' not in detail else
                          'null' if detail['plan'] is None else 'string' if type(detail['plan']) is str else 'unsupported',
                      plan_matches=type(detail) is dict and type(detail.get('plan')) is str and detail['plan'].lower() == plan_id().lower())
                if (type(detail) is not dict or type(detail.get('name')) is not str or detail['name'] != known_name
                        or type(detail.get('id')) is not str or detail['id'].lower() != (base() + '/sites/' + known_name).lower()
                        or type(detail.get('plan')) is not str or detail['plan'].lower() != plan_id().lower()):
                    raise ValueError('seven-occupant-guard')
                item['plan'] = detail['plan']
            if (not isinstance(item, dict) or item.get('name') not in OCCUPANTS or item['name'] in seen
                    or str(item.get('id', '')).lower() != (base() + '/sites/' + item['name']).lower()
                    or str(item.get('plan', '')).lower() != plan_id().lower()):
                event('inventory-row-rejected', page_index=_, row_index=len(seen) - page_prefix_count,
                      row_shape={dict: 'object', list: 'array', str: 'string', bool: 'boolean', int: 'integer',
                                 float: 'number', type(None): 'null'}.get(type(item), 'unsupported'),
                      known_name=item['name'] if type(item) is dict and type(item.get('name')) is str
                          and item['name'] in OCCUPANTS else 'unrecognized',
                      name_expected=type(item) is dict and type(item.get('name')) is str and item['name'] in OCCUPANTS,
                      field_presence={key: type(item) is dict and key in item for key in ('name', 'id', 'plan')},
                      field_shapes={key: 'missing' if type(item) is not dict or key not in item else
                          {dict: 'object', list: 'array', str: 'string', bool: 'boolean', int: 'integer',
                           float: 'number', type(None): 'null'}.get(type(item[key]), 'unsupported')
                          for key in ('name', 'id', 'plan')},
                      id_matches=type(item) is dict and type(item.get('name')) is str and item['name'] in OCCUPANTS
                          and type(item.get('id')) is str and item['id'].lower() == (base() + '/sites/' + item['name']).lower(),
                      plan_matches=type(item) is dict and type(item.get('plan')) is str and item['plan'].lower() == plan_id().lower(),
                      duplicate_known_name=type(item) is dict and type(item.get('name')) is str
                          and item['name'] in OCCUPANTS and item['name'] in seen,
                      validated_prefix_counts={name: int(name in seen) for name in OCCUPANTS},
                      coverage='validated-prefix-before-first-rejected-row')
                raise ValueError('seven-occupant-guard')
            seen[item['name']] = {'name': item['name'], 'bound_to_fixed_plan': True, 'running': item.get('state') == 'Running'}
        next_link = data.get('nextLink')
        if not next_link:
            event('inventory-terminal-selected', page_index=_,
                  validated_prefix_counts={name: int(name in seen) for name in OCCUPANTS},
                  missing_known_names=[name for name in OCCUPANTS if name not in seen],
                  coverage='terminal-by-existing-next-link-condition')
            if set(seen) != set(OCCUPANTS):
                raise ValueError('seven-occupant-guard')
            return [seen[app] for app in sorted(seen)]
        parsed = urllib.parse.urlsplit(next_link)
        query = urllib.parse.parse_qs(parsed.query, strict_parsing=True)
        if (parsed.scheme != 'https' or parsed.netloc != 'management.azure.com' or parsed.path.lower() != path.lower()
                or parsed.fragment or set(query) - {'api-version', '$skiptoken', 'skipToken'}
                or query.get('api-version') != [API] or len(next_link) > 4096):
            raise ValueError('occupancy-pagination-guard')
        url = next_link
    raise ValueError('occupancy-pagination-bound')


def queues(restoring=False, deadline=None):
    token = os.environ.get('GH_TOKEN', '')
    if not token or '\n' in token or '\r' in token:
        raise ValueError('github-token-unavailable')
    headers = ('Authorization: Bearer ' + token + '\nAccept: application/vnd.github+json\n'
               'X-GitHub-Api-Version: 2026-03-10\n').encode()
    def get(suffix):
        return json.loads(command(['curl', '--disable', '--silent', '--fail', '--request', 'GET', '--proto', '=https',
            '--proxy', '', '--noproxy', '*', '--max-redirs', '0', '--retry', '0', '--max-time', '15',
            '--connect-timeout', '3', '--max-filesize', '1048576', '--header', '@-',
            'https://api.github.com/repos/' + REPOSITORY + suffix], timeout=remaining(deadline, 20), stdin=headers))
    branch = get('/branches/master')
    if not restoring and branch.get('commit', {}).get('sha') != os.environ['GITHUB_SHA']:
        raise ValueError('master-moved-guard')
    for status in ('queued', 'in_progress', 'waiting', 'pending', 'requested'):
        data = get('/actions/runs?status=' + status + '&per_page=100')
        if (not isinstance(data, dict) or type(data.get('total_count')) is not int
                or data['total_count'] > 100 or not isinstance(data.get('workflow_runs'), list)
                or len(data['workflow_runs']) != data['total_count']):
            raise ValueError('complete-queue-guard')
        for run in data['workflow_runs']:
            if str(run.get('id')) == os.environ['GITHUB_RUN_ID']:
                continue
            relevant = run.get('event') == 'workflow_dispatch' or run.get('path') in (
                    '.github/workflows/deploy-dev.yml', '.github/workflows/deploy-azure.yml',
                    '.github/workflows/execute-sql.yml', '.github/workflows/deploy-docs.yml')
            plan_writer = run.get('path') in ('.github/workflows/deploy-dev.yml', '.github/workflows/deploy-azure.yml',
                                              '.github/workflows/dev-capacity-trial.yml')
            if relevant and restoring and (status != 'in_progress' or not plan_writer):
                event('interference-during-restore', status=status, active_plan_writer=False)
            elif relevant:
                raise ValueError('concurrent-manual-work-guard')
    event('manual-queues-idle', master_sha=os.environ['GITHUB_SHA'])


def configurations():
    results = {}
    for app in OCCUPANTS:
        config = azure(['webapp', 'config', 'show', '--name', app, '--resource-group', GROUP,
            '--query', '{linuxFxVersion:linuxFxVersion,alwaysOn:alwaysOn,appCommandLine:appCommandLine,'
            'healthCheckPath:healthCheckPath,http20Enabled:http20Enabled,ftpsState:ftpsState,'
            'minTlsVersion:minTlsVersion,numberOfWorkers:numberOfWorkers}'])
        if not isinstance(config, dict) or not isinstance(config.get('linuxFxVersion'), str):
            raise ValueError('site-config-identity-unavailable')
        if app == OCCUPANTS[-1] and not config['linuxFxVersion'].startswith('DOCKER|'):
            raise ValueError('functions-image-unverified')
        results[app] = fingerprint(config)
    return results


def packages():
    deadline = time.monotonic() + 180
    def remaining():
        left = deadline - time.monotonic()
        if left <= 0:
            raise TimeoutError('package-snapshot-deadline')
        return left
    token = package.az_value(['account', 'get-access-token', '--subscription', SUBSCRIPTION,
        '--resource', package.app_service_resource(remaining), '--query', 'accessToken'], remaining, 'trial-read-token')
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), package.NoRedirect())
    result = {}
    for app in APPS:
        mode = azure(['webapp', 'config', 'appsettings', 'list', '--name', app, '--resource-group', GROUP,
                      '--query', "[?name=='WEBSITE_RUN_FROM_PACKAGE'] | length(@) == `1` && [0].value == '1'"])
        if mode is not True:
            raise ValueError('local-package-mode-guard')
        def read(path, kind='json'):
            requests = []
            try:
                return package.scm_request(opener, app, token, path, 'trial-package-read', remaining,
                                           kind=kind, events=requests)
            except package.DeploymentError:
                event('package-read-failed', app=app, requests=requests)
                raise
        before = read('/api/deployments')
        pointer = read('/api/vfs/data/SitePackages/packagename.txt', 'pointer')
        after = read('/api/deployments')
        if not isinstance(before, list) or not 1 <= len(before) <= 100 or before != after:
            raise ValueError('stable-deployment-list-guard')
        active = []
        for row in before:
            if (not isinstance(row, dict) or row.get('complete') is not True or row.get('is_temp') is not False
                    or row.get('status') not in (3, 4) or type(row.get('active')) is not bool):
                raise ValueError('idle-deployment-guard')
            if row['active']:
                active.append(row)
        prefix = f'cleansia-{ACCEPTED_RUN}-1-{app}-'
        if (len(active) != 1 or active[0].get('status') != 4
                or re.fullmatch(re.escape(prefix) + r'[0-9a-f]{32}', str(active[0].get('deployer', ''))) is None
                or re.fullmatch(r'[A-Za-z0-9_-]{1,128}', str(active[0].get('id', ''))) is None
                or active[0]['id'] == 'latest' or token in active[0]['id'] or token in pointer
                or read('/api/vfs/data/SitePackages/packagename.txt', 'pointer') != pointer):
            raise ValueError('accepted-package-pointer-guard')
        result[app] = {'deployment_id': active[0]['id'], 'deployer': active[0]['deployer'], 'pointer': pointer,
                       'verification_scope': 'stable accepted-run deployment and selected pointer; no archive download or executing-worker proof'}
        event('package-pointer-snapshot', app=app, observation=result[app])
    return result


class SSRPage(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.stack = []
        self.named_cards = []
        self.ssr_root_seen = False
        self.ssr_home_seen = False
        self.marker_error = False
    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        active = not any(node['tag'] in ('script', 'style', 'template') for node in self.stack)
        node = {'tag': tag, 'root': active and tag == 'app-root' and attrs.get('ng-server-context') == 'ssr',
                'home': False, 'home_closed': False, 'card': None, 'capture': None}
        node['home'] = active and tag == 'cleansia-home' and any(item['root'] for item in self.stack)
        if active and tag == 'cleansia-home' and not node['home']:
            self.marker_error = True
        if active and tag == 'a' and any(item['home'] for item in self.stack) and 'cl-services__card' in attrs.get('class', '').split():
            href = urllib.parse.urlsplit(attrs.get('href', ''))
            ids = urllib.parse.parse_qs(href.query, keep_blank_values=True).get('serviceId', [])
            if not href.scheme and not href.netloc and href.path == '/order' and len(ids) == 1 and ids[0].strip():
                node['card'] = []
        if active and tag == 'h3' and 'cl-services__name' in attrs.get('class', '').split():
            node['capture'] = next((item['card'] for item in reversed(self.stack) if item['card'] is not None), None)
        if tag not in ('area', 'base', 'br', 'col', 'embed', 'hr', 'img', 'input', 'link', 'meta', 'param', 'source', 'track', 'wbr'):
            self.stack.append(node)
    def handle_startendtag(self, tag, attrs):
        self.handle_starttag(tag, attrs)
        if tag in ('app-root', 'cleansia-home'):
            self.marker_error = True
        self.handle_endtag(tag)
    def handle_data(self, data):
        if not any(node['tag'] in ('script', 'style', 'template') for node in self.stack):
            capture = next((item['capture'] for item in reversed(self.stack) if item['capture'] is not None), None)
            if capture is not None:
                capture.append(data)
    def handle_endtag(self, tag):
        index = next((i for i in range(len(self.stack) - 1, -1, -1) if self.stack[i]['tag'] == tag), None)
        if index is not None:
            removed = self.stack[index:]
            if any(item['root'] or item['home'] for item in removed) and len(removed) != 1:
                self.marker_error = True
            if len(removed) == 1 and removed[0]['home']:
                next(item for item in reversed(self.stack[:index]) if item['root'])['home_closed'] = True
            if len(removed) == 1 and removed[0]['root']:
                self.ssr_root_seen = True
                self.ssr_home_seen |= removed[0]['home_closed']
            del self.stack[index:]
            self.named_cards.extend(bool(''.join(item['card']).strip()) for item in removed if item['card'] is not None)
        elif tag in ('app-root', 'cleansia-home'):
            self.marker_error = True


def semantic(label, body):
    if label == 'ssr-root':
        page = SSRPage()
        page.feed(body.decode('utf-8'))
        page.close()
        markers_complete = not page.marker_error and not any(node['root'] or node['home'] for node in page.stack)
        return {'healthy': bool(page.named_cards) and all(page.named_cards), 'named_card_count': sum(page.named_cards),
                'ssr_root_seen': page.ssr_root_seen and markers_complete,
                'ssr_home_seen': page.ssr_home_seen and markers_complete}
    if label == 'ssr-health':
        data = json.loads(body)
        memory = {'rss', 'heapTotal', 'heapUsed', 'external', 'arrayBuffers'}
        valid = (isinstance(data, dict) and set(data) == {'status', 'timestamp', 'uptime', 'memory'}
            and data['status'] == 'healthy' and isinstance(data['timestamp'], str)
            and re.fullmatch(r'\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z', data['timestamp']) is not None
            and type(data['uptime']) in (int, float) and math.isfinite(data['uptime']) and data['uptime'] >= 0
            and isinstance(data['memory'], dict) and set(data['memory']) == memory
            and all(type(value) is int and value >= 0 for value in data['memory'].values()))
        return {'healthy': valid, **({'uptime_seconds': data['uptime']} if valid else {})}
    return {'healthy': body == b'Healthy'}


def probe(label, deadline=None, maximum=5):
    limit = 524_288 if label == 'ssr-root' else 65_536
    row = {'probe': label, 'started_utc': utc(), 'healthy': False}
    started = time.monotonic()
    with tempfile.TemporaryDirectory() as directory:
        body_path = Path(directory) / 'body'
        try:
            arguments = ['curl', '--disable', '--silent', '--request', 'GET', '--proto', '=https',
                '--proxy', '', '--noproxy', '*', '--max-redirs', '0', '--retry', '0', '--max-time', str(maximum),
                '--connect-timeout', '3', '--max-filesize', str(limit), '--header', 'Accept-Language: en',
                '--header', 'Cookie: cleansia_ssr_probe=1', '--output', str(body_path), '--write-out',
                '{"status":%{http_code},"ttfb":%{time_starttransfer},"total":%{time_total},"redirects":%{num_redirects}}', URLS[label]]
            with tempfile.TemporaryFile() as output:
                response = subprocess.run(arguments, stdout=output, stderr=subprocess.DEVNULL,
                                          timeout=remaining(deadline, maximum + 2), check=False)
                row['curl_exit_code'] = response.returncode
                output.seek(0)
                raw = output.read(4097)
            if len(raw) > 4096:
                raise ValueError('public-timing-bound')
            timings = json.loads(raw)
            if (set(timings) != {'status', 'ttfb', 'total', 'redirects'} or type(timings['status']) is not int
                    or not 100 <= timings['status'] <= 599 or timings['redirects'] != 0
                    or any(type(timings[key]) not in (int, float) or not math.isfinite(timings[key]) or timings[key] < 0
                           for key in ('ttfb', 'total'))):
                raise ValueError('public-timing-guard')
            body = body_path.read_bytes() if body_path.stat().st_size <= limit else b''
            row.update(timings, body_bytes=body_path.stat().st_size, body_sha256=hashlib.sha256(body).hexdigest())
            if response.returncode == 0 and timings['status'] == 200 and body_path.stat().st_size <= limit:
                row.update(semantic(label, body))
        except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError, UnicodeError) as error:
            row['error_kind'] = type(error).__name__
    row.update(finished_utc=utc(), elapsed_seconds=round(time.monotonic() - started, 3), _started_monotonic=started)
    return row


def functionality(strict=True):
    labels = (*APPS[:5], 'ssr-health', 'ssr-root')
    with ThreadPoolExecutor(max_workers=4) as pool:
        rows = list(pool.map(lambda label: probe(label, maximum=20 if label == 'ssr-root' else 5), labels))
    for row in rows:
        row.pop('_started_monotonic', None)
    strict_ready = all(row['healthy'] for row in rows)
    event('functional-readiness', samples=rows, strict_ready=strict_ready)
    if strict and not strict_ready:
        raise ValueError('functional-readiness-failed')
    return rows


def snapshot(capacity, allowed=('B2', 'B3'), deadline=None):
    return {'plan': read_plan(capacity, allowed, deadline=deadline), 'occupants': inventory(deadline)}


def same_bindings(left, right):
    return [row['name'] for row in left] == [row['name'] for row in right]


def preserved(baseline, observed):
    if (baseline['plan']['configuration_sha256'] != observed['plan']['configuration_sha256']
            or not same_bindings(baseline['occupants'], observed['occupants'])):
        raise ValueError('plan-or-binding-drift')


def resize_arguments(sku, capacity):
    if sku not in ('B2', 'B3') or type(capacity) is not int or capacity not in (1, 2, 3):
        raise ValueError('resize-arguments-guard')
    return ['appservice', 'plan', 'update', '--name', PLAN, '--resource-group', GROUP,
            '--sku', sku, '--number-of-workers', str(capacity), '--query', '{name:name,sku:sku.name,capacity:sku.capacity}']


def settle(capacity, sku, baseline, unknown=False, stop_at=None):
    started = time.monotonic()
    deadline = min(started + 600, stop_at) if stop_at is not None else started + 600
    consistent = 0
    last = None
    for _ in range(61):
        try:
            remaining(deadline, 30)
            observed = snapshot(capacity, deadline=deadline)
            preserved(baseline, observed)
            last = observed
            consistent = consistent + 1 if observed['plan']['sku'] == sku else 0
            if consistent >= (7 if unknown else 2):
                event('resize-state-verified', requested_sku=sku, elapsed_seconds=round(time.monotonic() - started, 3), observed=observed)
                return observed
        except (ValueError, OSError, subprocess.SubprocessError):
            consistent = 0
        if time.monotonic() >= deadline:
            break
        time.sleep(max(0, min(10, deadline - time.monotonic())))
    event('resize-state-unverified', requested_sku=sku, last_observed=last)
    raise ValueError('resize-state-unverified')


def resize(sku, capacity, baseline, stop_at=None):
    if sku == 'B2':
        restore_queues(stop_at)
    else:
        queues(deadline=stop_at)
    current = snapshot(capacity, deadline=stop_at)
    preserved(baseline, current)
    if sku == 'B3' and current['plan']['sku'] != 'B2':
        raise ValueError('unexpected-pre-upscale-sku')
    if sku == 'B2' and current['plan']['sku'] == 'B2':
        return current
    arguments = resize_arguments(sku, capacity)
    event('resize-intent', sku=sku, preserved_capacity=capacity, argv=['az', *arguments,
          '--subscription', '<dev-weu-subscription>', '--only-show-errors', '--output', 'json'])
    if sku == 'B3':
        save('b3-armed.json', {'source_sha': os.environ['GITHUB_SHA'], 'capacity': capacity,
            'intent_utc': utc(), 'operation_state': 'unresolved',
            'restore_attempt_by_utc': (datetime.now(timezone.utc) + timedelta(
                seconds=max(0, stop_at + 180 - time.monotonic()) if stop_at is not None else 1500)).isoformat()})
    else:
        save('b2-operation.json', {'operation_state': 'unresolved', 'intent_utc': utc(), 'capacity': capacity})
    started = time.monotonic()
    unknown = False
    try:
        azure(arguments, timeout=600, deadline=stop_at)
        if sku == 'B3':
            armed = json.loads((DIRECTORY / 'b3-armed.json').read_bytes())
            save('b3-armed.json', armed | {'operation_state': 'cli-poller-completed'})
        else:
            save('b2-operation.json', {'operation_state': 'cli-poller-completed', 'capacity': capacity})
        event('resize-command-return', sku=sku, outcome='completed-cli-poller', elapsed_seconds=round(time.monotonic() - started, 3))
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        unknown = True
        event('resize-command-return', sku=sku, outcome='unknown', error_kind=type(error).__name__, elapsed_seconds=round(time.monotonic() - started, 3))
    observed = settle(capacity, sku, baseline, unknown, stop_at)
    if unknown:
        if sku == 'B3':
            armed = json.loads((DIRECTORY / 'b3-armed.json').read_bytes())
            save('b3-armed.json', armed | {'operation_state': 'reconciled-b3-ready'})
        else:
            save('b2-operation.json', {'operation_state': 'reconciled-b2-ready', 'capacity': capacity})
        raise ValueError('ambiguous-resize-reconciled-stop-trial')
    return observed


def restore_queues(deadline):
    try:
        queues(restoring=True, deadline=deadline)
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
        if isinstance(error, ValueError) and error.args == ('concurrent-manual-work-guard',):
            raise
        event('restore-queue-evidence-unavailable', error_kind=type(error).__name__)


def restore(capacity, baseline=None):
    deadline = time.monotonic() + 18 * 60
    event('restore-attempt-started', preserved_capacity=capacity)
    restore_queues(deadline)
    observed = snapshot(capacity, deadline=deadline)
    standalone = baseline is None
    if baseline is not None:
        preserved(baseline, observed)
    else:
        baseline = observed
    event('restore-admission', observed=observed)
    if (DIRECTORY / 'b2-operation.json').exists():
        pending_b2 = json.loads((DIRECTORY / 'b2-operation.json').read_bytes())
        if pending_b2.get('operation_state') == 'unresolved':
            if observed['plan']['sku'] != 'B2':
                event('restore-unverified-pending-original-b2', observed=observed)
                raise ValueError('original-b2-operation-unresolved')
            settle(capacity, 'B2', baseline, unknown=True, stop_at=deadline)
            save('b2-operation.json', {'operation_state': 'reconciled-b2-ready', 'capacity': capacity})
    if standalone:
        event('standalone-restore-qualification', prior_operation_terminal='unknown',
              qualification='Separated Ready observations are admission only and cannot prove all older asynchronous operations ended.')
        if observed['plan']['sku'] == 'B3':
            if remaining(deadline, 11) < 10:
                raise TimeoutError('separated-restore-read-budget')
            time.sleep(10)
            confirmed = snapshot(capacity, ('B3',), deadline)
            preserved(observed, confirmed)
            event('standalone-b3-separated-read-confirmed', observed=confirmed, minimum_separation_seconds=10)
    if (DIRECTORY / 'b3-armed.json').exists():
        armed = json.loads((DIRECTORY / 'b3-armed.json').read_bytes())
        if armed.get('operation_state') == 'unresolved':
            if observed['plan']['sku'] != 'B3':
                event('restore-unverified-pending-original-b3', observed=observed)
                raise ValueError('original-b3-operation-unresolved')
            settle(capacity, 'B3', baseline, unknown=True, stop_at=deadline)
            save('b3-armed.json', armed | {'operation_state': 'reconciled-b3-ready'})
    if observed['plan']['sku'] == 'B3':
        resize('B2', capacity, baseline, deadline)
    final = snapshot(capacity, ('B2',), deadline)
    preserved(baseline, final)
    event('b2-restoration-verified', observed=final,
          qualification='Current fixed-plan state; a separate run cannot prove an earlier unknown operation terminal without its receipt.')
    return final


def restart_sites(deadline=None):
    deadline = min(time.monotonic() + 60, deadline) if deadline is not None else time.monotonic() + 60
    token = package.az_value(['account', 'get-access-token', '--subscription', SUBSCRIPTION,
        '--resource', 'https://management.azure.com/', '--query', 'accessToken'],
        lambda: remaining(deadline, 60), 'trial-restart-arm-token')
    headers = ('Authorization: Bearer ' + token + '\nContent-Length: 0\n').encode()
    for app in OCCUPANTS:
        started = time.monotonic()
        facts = {}
        failure = None
        with tempfile.TemporaryDirectory() as directory, tempfile.TemporaryFile() as output:
            try:
                timeout = remaining(deadline, 30)
                event('restart-site-intent', app=app)
                response = subprocess.run(['curl', '--disable', '--silent', '--request', 'POST', '--proto', '=https',
                    '--proxy', '', '--noproxy', '*', '--max-redirs', '0', '--retry', '0', '--max-time', '30',
                    '--connect-timeout', '3', '--max-filesize', '65536', '--header', '@-',
                    '--output', str(Path(directory) / 'body'), '--write-out', '%{http_code}',
                    'https://management.azure.com' + base() + '/sites/' + app + '/restart?softRestart=false&api-version=' + API],
                    input=headers, stdout=output, stderr=subprocess.DEVNULL,
                    timeout=timeout, check=False)
                facts['curl_exit_code'] = response.returncode
            except (OSError, subprocess.SubprocessError, TimeoutError) as error:
                failure = error
            output.seek(0)
            status = output.read(17)
        if re.fullmatch(rb'[1-5][0-9]{2}', status) is not None:
            facts['http_status'] = int(status)
        category = ('curl-timeout' if isinstance(failure, subprocess.TimeoutExpired) else
            'deadline-exhausted' if isinstance(failure, TimeoutError) else
            'curl-process-error' if failure is not None else
            'curl-exit' if facts['curl_exit_code'] else
            'status-output-bound' if len(status) > 16 else
            'http-status-invalid' if 'http_status' not in facts else
            'http-status-not-200' if facts['http_status'] != 200 else None)
        if category is not None:
            event('restart-control-plane-failed', app=app, failure_category=category, **facts,
                  elapsed_seconds=round(time.monotonic() - started, 3))
            if failure is not None:
                raise failure
            if facts['curl_exit_code'] or len(status) > 16:
                raise ValueError('bounded-command-failed')
            raise ValueError('restart-not-200')
        event('restart-control-plane-return', app=app, http_status=200, elapsed_seconds=round(time.monotonic() - started, 3))
    return 200


def restart_leg(name, capacity, baseline, deadline=None):
    deadline = min(time.monotonic() + 600, deadline) if deadline is not None else time.monotonic() + 600
    queues(deadline=deadline)
    observed = snapshot(capacity, ('B3',) if name == 'B3' else ('B2',), deadline)
    preserved(baseline, observed)
    with ThreadPoolExecutor(max_workers=4) as pool:
        before = {row['probe']: row for row in pool.map(
            lambda label: probe(label, deadline, 20 if label == 'ssr-root' else 5), PROBES)}
    for row in before.values():
        row.pop('_started_monotonic', None)
    root = before['ssr-root']
    strict_ready = all(row['healthy'] for row in before.values())
    experimental_admission = (os.environ.get('TRIAL_MODE') == 'trial'
        and all(row['healthy'] for label, row in before.items() if label != 'ssr-root')
        and root['healthy'] is False and root.get('curl_exit_code') == 0 and root.get('status') == 200
        and root.get('redirects') == 0 and type(root.get('body_bytes')) is int and 0 < root['body_bytes'] <= 524_288
        and 'error_kind' not in root and root.get('named_card_count') == 0
        and root.get('ssr_root_seen') is True and root.get('ssr_home_seen') is True)
    save(name + '-precheck.json', {'leg': name, 'samples': list(before.values()),
        'strict_ready': strict_ready, 'experimental_admission': experimental_admission})
    if not strict_ready and not experimental_admission:
        raise ValueError('pre-restart-readiness-failed')
    required = tuple(label for label in PROBES if label != 'ssr-root'
                     or root['healthy'] and root.get('elapsed_seconds', 0) <= 5)
    started = time.monotonic()
    intent = utc()
    event('restart-intent', leg=name, method='POST', path='fixed-sites/restart', api_version=API,
          softRestart=False, all_seven_occupants=True, sequential=True)
    boundary = {label: None for label in PROBES}
    confirmations = dict.fromkeys(PROBES, 0)
    consecutive = dict.fromkeys(PROBES, 0)
    first_success = dict.fromkeys(PROBES, None)
    first_recovery = dict.fromkeys(PROBES, None)
    confirmed_recovery = dict.fromkeys(PROBES, None)
    samples = []
    rows = []
    deadline_exhausted = False
    with ThreadPoolExecutor(max_workers=5) as pool:
        first_round = pool.map(lambda label: probe(label, deadline), PROBES)
        event('restart-observer-submitted', leg=name, fixed_endpoint_count=4)
        operation = pool.submit(restart_sites, deadline)
        for round_number in range(60):
            if time.monotonic() >= deadline:
                deadline_exhausted = True
                break
            round_started = time.monotonic()
            rows = list(first_round if round_number == 0 else pool.map(lambda label: probe(label, deadline), PROBES))
            for row in rows:
                label = row['probe']
                row.update(round=round_number, since_intent_seconds=round(row.pop('_started_monotonic', round_started) - started, 3))
                samples.append(row)
                consecutive[label] = consecutive[label] + 1 if row['healthy'] else 0
                if not row['healthy']:
                    if before[label]['healthy']:
                        boundary[label] = 'observed-negative-transition'
                    confirmations[label] = 0
                    first_recovery[label] = confirmed_recovery[label] = None
                if (label == 'ssr-health' and before[label]['healthy'] and row['healthy']
                        and before[label].get('uptime_seconds', 0) > row.get('uptime_seconds', float('inf'))):
                    boundary['ssr-health'] = 'responding-SSR-process-uptime-reset'
                    if before['ssr-root']['healthy']:
                        boundary['ssr-root'] = 'responding-SSR-process-uptime-reset'
                if row['healthy'] and boundary[label] is not None:
                    confirmations[label] += 1
                    if first_recovery[label] is None:
                        first_recovery[label] = row['since_intent_seconds'] + row['elapsed_seconds']
                    if confirmations[label] == 2:
                        confirmed_recovery[label] = row['since_intent_seconds'] + row['elapsed_seconds']
                if row['healthy'] and first_success[label] is None:
                    first_success[label] = row['since_intent_seconds'] + row['elapsed_seconds']
            save(name + '-restart.json', {'leg': name, 'intent_utc': intent,
                'collection_result': 'partial', 'control_plane_acknowledged': None, 'samples': samples})
            elapsed = time.monotonic() - started
            if all(confirmations[label] >= 2 for label in required) or elapsed >= 180 and all(consecutive[label] >= 2 for label in required):
                break
            if time.monotonic() >= deadline:
                deadline_exhausted = True
                break
            time.sleep(max(0, min(10 - (time.monotonic() - round_started), deadline - time.monotonic())))
        try:
            acknowledged = operation.result(timeout=1) == 200
        except (OSError, ValueError, subprocess.SubprocessError, TimeoutError) as error:
            acknowledged = False
            event('restart-command-failed', leg=name, error_kind=type(error).__name__)
    result = {'leg': name, 'intent_utc': intent, 'control_plane_acknowledged': acknowledged,
              'deadline_exhausted': deadline_exhausted,
              'right_censored': not rows or any(consecutive[label] < 2 for label in PROBES),
              'elapsed_seconds': round(time.monotonic() - started, 3), 'samples': samples,
              'measurements': {label: {'boundary': boundary[label], 'confirmations': confirmations[label],
                  'precheck_healthy': before[label]['healthy'],
                  'cold_comparison_eligible': bool(acknowledged and label in required and boundary[label] is not None
                                                   and confirmations[label] >= 2),
                  'consecutive_healthy_samples': consecutive[label],
                  'first_semantic_success_seconds': first_success[label],
                  'first_post_boundary_success_seconds': first_recovery[label],
                  'second_post_boundary_success_seconds': confirmed_recovery[label],
                  'classification': 'pre-existing-unhealthy-excluded' if not before[label]['healthy'] else
                    'slow-precheck-excluded' if label not in required else
                    'observed-recovery' if boundary[label] and confirmations[label] >= 2 else
                    'right-censored-not-ready' if not rows or not next(row['healthy'] for row in rows if row['probe'] == label) else
                    'right-censored-unconfirmed' if boundary[label] else
                    'warm-or-unobserved-inconclusive'} for label in PROBES}}
    save(name + '-restart.json', result)
    event('restart-leg-complete', leg=name, measurements=result['measurements'], control_plane_acknowledged=acknowledged)
    if (not acknowledged or not rows or any(consecutive[label] < 2 for label in required)
            or deadline_exhausted and not all(confirmations[label] >= 2 for label in required)):
        raise ValueError('restart-leg-not-ready')
    return result


def telemetry(started):
    end = datetime.now(timezone.utc)
    start = datetime.fromisoformat(started) - timedelta(minutes=3)
    if end - start > timedelta(hours=2):
        raise ValueError('telemetry-window-bound')
    query = 'value[].{name:name.value,unit:unit,errorCode:errorCode,series:timeseries[].{dimensions:metadatavalues[].{name:name.value,value:value},data:data[].{timestamp:timeStamp,average:average,maximum:maximum}}}'
    data = azure(['monitor', 'metrics', 'list', '--resource', plan_id(), '--namespace', 'Microsoft.Web/serverfarms',
        '--metrics', 'CpuPercentage', 'MemoryPercentage', 'HttpQueueLength', '--aggregation', 'Average', 'Maximum',
        '--interval', '1m', '--filter', "Instance eq '*'", '--start-time', start.isoformat(), '--end-time', end.isoformat(), '--query', query], 60)
    expected = {'CpuPercentage': 'Percent', 'MemoryPercentage': 'Percent', 'HttpQueueLength': 'Count'}
    if not isinstance(data, list) or len(data) != 3 or {row.get('name') for row in data} != set(expected):
        raise ValueError('metric-set-guard')
    safe = []
    for metric in data:
        if metric.get('unit') != expected[metric['name']] or metric.get('errorCode') not in (None, 'Success') or not isinstance(metric.get('series'), list) or len(metric['series']) > 10:
            raise ValueError('metric-series-guard')
        series = []
        for row in metric['series']:
            if not isinstance(row.get('data'), list) or len(row['data']) > 123 or not isinstance(row.get('dimensions'), list) or len(row['dimensions']) > 4:
                raise ValueError('metric-bound')
            dimensions = [{'instance_sha256': fingerprint(item.get('value'))} for item in row['dimensions'] if item.get('name') == 'Instance']
            points = []
            for point in row['data']:
                stamp = datetime.fromisoformat(point['timestamp'].replace('Z', '+00:00'))
                if stamp.tzinfo is None or not start - timedelta(minutes=1) <= stamp <= end + timedelta(minutes=1):
                    raise ValueError('metric-timestamp-guard')
                if any(point.get(key) is not None and (type(point[key]) not in (int, float) or not math.isfinite(point[key]) or point[key] < 0) for key in ('average', 'maximum')):
                    raise ValueError('metric-value-guard')
                points.append({key: point.get(key) for key in ('timestamp', 'average', 'maximum')})
            series.append({'dimensions': dimensions, 'data': points})
        safe.append({'name': metric['name'], 'unit': metric['unit'], 'series': series})
    save('metrics.json', {'start_utc': start.isoformat(), 'end_utc': end.isoformat(), 'metrics': safe,
                          'qualification': 'Minute telemetry may lag or be missing; memory percentages have different SKU denominators.'})


def activity(started):
    start = datetime.fromisoformat(started) - timedelta(minutes=3)
    end = datetime.now(timezone.utc)
    if end - start > timedelta(hours=2):
        raise ValueError('activity-window-bound')
    rows = azure(['monitor', 'activity-log', 'list', '--resource-id', plan_id(), '--start-time', start.isoformat(),
        '--end-time', end.isoformat(), '--max-events', '1000', '--query',
        '[].{timestamp:eventTimestamp,operation:operationName.value,status:status.value,substatus:subStatus.value,'
        'resource:resourceId,correlation:correlationId,operationId:operationId}'], 60)
    if not isinstance(rows, list) or len(rows) > 1000:
        raise ValueError('activity-response-bound')
    safe = []
    for row in rows:
        stamp = datetime.fromisoformat(row['timestamp'].replace('Z', '+00:00'))
        if (stamp.tzinfo is None or not start <= stamp <= end or str(row.get('resource', '')).lower() != plan_id().lower()
                or not isinstance(row.get('operation'), str)
                or re.fullmatch(r'Microsoft\.Web/serverfarms/[A-Za-z/]{1,128}', row['operation'], re.I) is None):
            raise ValueError('activity-resource-guard')
        safe.append({'timestamp': row['timestamp'], 'operation': row['operation'],
            'status': row.get('status') if row.get('status') in ('Started', 'Succeeded', 'Failed', 'Accepted', 'Running', 'Canceled') else 'unrecognized',
            'substatus_sha256': fingerprint(row.get('substatus')),
            **{key: row[key] for key in ('correlation', 'operationId')
               if isinstance(row.get(key), str) and re.fullmatch(UUID, row[key]) is not None}})
    save('activity.json', {'rows': safe, 'possibly_truncated': len(rows) == 1000,
        'qualification': 'Bounded lagging plan Activity Log only; caller/claims/IP/properties omitted. Container lifecycle unavailable; logging was not enabled.'})


def prepare(capacity):
    event('baseline-phase', phase='queues')
    queues()
    event('baseline-phase', phase='snapshot')
    baseline = {'source_sha': os.environ['GITHUB_SHA'], 'run_id': os.environ['GITHUB_RUN_ID'],
        'run_attempt': 1, 'recorded_utc': utc(), **snapshot(capacity, ('B2',))}
    event('baseline-phase', phase='cli')
    baseline['cli'] = hosted_cli()
    event('baseline-phase', phase='configurations')
    baseline['configurations'] = configurations()
    event('baseline-phase', phase='packages')
    baseline['packages'] = packages()
    event('baseline-phase', phase='readiness')
    baseline['readiness'] = functionality(strict=False)
    root = baseline['readiness'][-1]
    baseline['strict_ready'] = all(row['healthy'] for row in baseline['readiness'])
    baseline['experimental_admission'] = (os.environ.get('TRIAL_MODE') == 'trial'
        and len(baseline['readiness']) == 7 and all(row['healthy'] for row in baseline['readiness'][:-1])
        and root['healthy'] is False and root.get('curl_exit_code') == 0 and root.get('status') == 200
        and root.get('redirects') == 0 and type(root.get('body_bytes')) is int and 0 < root['body_bytes'] <= 524_288
        and 'error_kind' not in root and root.get('named_card_count') == 0
        and root.get('ssr_root_seen') is True and root.get('ssr_home_seen') is True)
    baseline.update({'protocol': {'b3_restore_attempt_deadline_seconds': 1500, 'job_timeout_minutes': 75,
                     'readiness_leg_deadline_seconds': 600, 'maximum_rounds_per_leg': 60,
                     'cadence_seconds': 10, 'matched_restarts': 3, 'logical_sku_updates': 2},
        'restore_argv': ['az', *resize_arguments('B2', capacity), '--subscription', '<dev-weu-subscription>', '--only-show-errors', '--output', 'json']})
    baseline['cli_receipt_sha256'] = fingerprint(baseline['cli'])
    baseline['optional_evidence'] = {}
    for phase, collect in (('metrics', telemetry), ('activity', activity)):
        try:
            collect(baseline['recorded_utc'])
            target = DIRECTORY / ('initial-' + phase + '.json')
            (DIRECTORY / (phase + '.json')).rename(target)
            baseline['optional_evidence'][phase] = {'state': 'received', 'sha256': hashlib.sha256(target.read_bytes()).hexdigest()}
        except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
            baseline['optional_evidence'][phase] = {'state': 'unavailable', 'error_kind': type(error).__name__}
    save('baseline.json', baseline)
    event('baseline-prepared', baseline_sha256=fingerprint(baseline), preserved_capacity=capacity,
          cli_receipt_sha256=baseline['cli_receipt_sha256'],
          strict_ready=baseline['strict_ready'], experimental_admission=baseline['experimental_admission'],
          preview={'before': 'B2', 'trial': 'B3', 'restore': 'B2', 'count_changes': False})
    if not baseline['strict_ready'] and not baseline['experimental_admission']:
        raise ValueError('functional-readiness-failed')
    return baseline


def run_trial(capacity):
    if re.fullmatch(r'[1-9][0-9]{0,18}', os.environ.get('BASELINE_ARTIFACT_ID', '')) is None:
        raise ValueError('durable-baseline-artifact-required')
    raw = (DIRECTORY / 'baseline.json').read_bytes()
    if len(raw) > 1_048_576:
        raise ValueError('baseline-bound')
    baseline = json.loads(raw)
    if (baseline.get('source_sha') != os.environ['GITHUB_SHA'] or baseline.get('run_id') != os.environ['GITHUB_RUN_ID']
            or baseline.get('run_attempt') != 1 or baseline.get('plan', {}).get('capacity') != capacity
            or not timedelta() <= datetime.now(timezone.utc) - datetime.fromisoformat(baseline['recorded_utc']) <= timedelta(minutes=30)):
        raise ValueError('baseline-identity-guard')
    expected_receipt = os.environ.get('EXPECTED_CLI_RECEIPT', '')
    actual_receipt = fingerprint(baseline.get('cli'))
    if (re.fullmatch(r'[0-9a-f]{64}', expected_receipt) is None or expected_receipt != actual_receipt
            or baseline.get('cli_receipt_sha256') != actual_receipt):
        raise ValueError('reviewed-cli-receipt-mismatch')
    event('cli-receipt-admitted', sha256=actual_receipt)
    current = snapshot(capacity, ('B2',))
    preserved(baseline, current)
    if packages() != baseline['packages'] or configurations() != baseline['configurations']:
        raise ValueError('package-or-configuration-drift')
    readiness = functionality(strict=False)
    strict_ready = all(row['healthy'] for row in readiness)
    experimental_admission = False
    if not strict_ready:
        root = readiness[-1]
        experimental_admission = (os.environ.get('TRIAL_MODE') == 'trial'
            and len(readiness) == 7 and all(row['healthy'] for row in readiness[:-1])
            and root['healthy'] is False and root.get('curl_exit_code') == 0 and root.get('status') == 200
            and root.get('redirects') == 0 and type(root.get('body_bytes')) is int and 0 < root['body_bytes'] <= 524_288
            and 'error_kind' not in root and root.get('named_card_count') == 0
            and root.get('ssr_root_seen') is True and root.get('ssr_home_seen') is True)
        if not experimental_admission:
            raise ValueError('functional-readiness-failed')
    event('durable-baseline-admitted', artifact_id=os.environ['BASELINE_ARTIFACT_ID'], baseline_sha256=fingerprint(baseline),
          strict_ready=strict_ready, experimental_admission=experimental_admission)
    result = {'started_utc': utc(), 'legs': [], 'trial_error': None, 'restore_error': None}
    try:
        time.sleep(180)
        result['legs'].append(restart_leg('B2-before', capacity, baseline))
        time.sleep(120)
        b3_deadline = time.monotonic() + 25 * 60
        resize('B3', capacity, baseline, b3_deadline - 180)
        time.sleep(min(120, max(0, b3_deadline - 10 - time.monotonic())))
        result['legs'].append(restart_leg('B3', capacity, baseline, b3_deadline - 10))
        time.sleep(min(120, max(0, b3_deadline - time.monotonic())))
    except (Exception, KeyboardInterrupt) as error:
        result['trial_error'] = type(error).__name__
        event('trial-progression-stopped', error_kind=type(error).__name__)
    finally:
        if (DIRECTORY / 'b3-armed.json').exists():
            try:
                result['final_plan'] = restore(capacity, baseline)
            except (Exception, KeyboardInterrupt) as error:
                result['restore_error'] = type(error).__name__
                event('restore-unverified', error_kind=type(error).__name__)
        save('result.json', result)
    if result['trial_error'] is None and result['restore_error'] is None:
        try:
            time.sleep(120)
            result['legs'].append(restart_leg('B2-after', capacity, baseline))
        except (Exception, KeyboardInterrupt) as error:
            result['trial_error'] = type(error).__name__
    result['verification_errors'] = []
    for phase, collect in (
            ('packages', packages), ('configurations', configurations), ('readiness', functionality)):
        try:
            observed = collect()
            if phase in ('packages', 'configurations') and observed != baseline[phase]:
                raise ValueError('final-input-drift')
            result['final_' + phase] = observed
        except (Exception, KeyboardInterrupt) as error:
            result['verification_errors'].append({'phase': phase, 'error_kind': type(error).__name__})
    time.sleep(180)
    for phase, collect in (('metrics', telemetry), ('activity', activity)):
        try:
            collect(result['started_utc'])
        except (Exception, KeyboardInterrupt) as error:
            result['verification_errors'].append({'phase': phase, 'error_kind': type(error).__name__})
            event('optional-evidence-unavailable', phase=phase, error_kind=type(error).__name__)
    result['finished_utc'] = utc()
    result['qualification'] = 'One finite cycle; placement, caches, background work and time confound causality. Unobserved boundaries are inconclusive. No all-worker or Functions-job proof.'
    save('result.json', result)
    if any(result.get(key) for key in ('trial_error', 'restore_error', 'verification_errors')):
        raise ValueError('trial-or-restoration-unverified')


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument('mode', choices=('observe', 'trial', 'restore-b2'))
    parser.add_argument('--expected-capacity', default='1')
    parser.add_argument('--validate-only', action='store_true')
    args = parser.parse_args(argv)
    try:
        capacity = validate_context(args.mode, args.expected_capacity)
        if args.validate_only:
            return 0
        authenticate()
        if args.mode == 'observe':
            prepare(capacity)
        elif args.mode == 'trial':
            run_trial(capacity)
        else:
            baseline = None
            if (DIRECTORY / 'baseline.json').exists():
                baseline = json.loads((DIRECTORY / 'baseline.json').read_bytes())
            restore(capacity, baseline)
            try:
                functionality()
            except (Exception, KeyboardInterrupt) as error:
                event('post-restore-readiness-unverified', error_kind=type(error).__name__)
                return 1
        return 0
    except (Exception, KeyboardInterrupt) as error:
        reason = error.args[0] if (type(error) is ValueError and len(error.args) == 1
                                  and type(error.args[0]) is str and error.args[0] in FAILURE_REASONS) else 'unclassified'
        event('operation-failed', mode=args.mode, error_kind=type(error).__name__, reason=reason)
        return 1


if __name__ == '__main__':
    def interrupted(_signum, _frame):
        raise InterruptedError('termination-requested')
    signal.signal(signal.SIGTERM, interrupted)
    signal.signal(signal.SIGINT, interrupted)
    raise SystemExit(main())
