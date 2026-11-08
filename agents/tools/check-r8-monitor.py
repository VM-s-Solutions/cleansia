"""Find methods in a dexdump -d listing that ART would reject with
'expected to be within a catch-all for an instruction where a monitor is held':
a throwing instruction, executed while a monitor is held, inside a try range with no catch-all handler.
Mirrors ART MethodVerifier: the check applies only to instructions covered by a try item.
A minified build can carry that shape (a return inside synchronized once did), and ART then fails the class with a VerifyError.

Usage: dexdump -d classes*.dex > dump.txt && python3 check-r8-monitor.py dump.txt [method-name-prefix]
Exits 1 when a method would be rejected, or when no method takes a monitor: that dump was not read, not passed.
The last line also counts the methods with switch targets unchecked: code reached only through a switch is not walked."""
import re, sys
from collections import deque

# ART clears kThrow on monitor-exit (method_verifier.cc), so it has no exception edge.
THROW_PREFIXES = ('invoke-', 'iget', 'iput', 'sget', 'sput', 'aget', 'aput', 'new-instance', 'new-array',
                  'filled-new-array', 'check-cast', 'instance-of', 'const-class', 'const-string', 'array-length',
                  'throw', 'monitor-enter', 'div-int', 'rem-int', 'div-long', 'rem-long', 'const-method')
INSN = re.compile(r'\|([0-9a-f]{4,8}): ([a-z0-9/\-]+)(.*)$')

def can_throw(op):
    return op.startswith(THROW_PREFIXES)

def parse(path):
    methods = []; cur = None; in_catches = False; rng = None
    for line in open(path, errors='replace'):
        m = re.match(r'^[0-9a-f]{6,8}:\s+\|\[[0-9a-f]+\] (\S+)', line)
        if m:
            cur = {'name': m.group(1), 'insns': [], 'tries': []}; methods.append(cur); in_catches = False; continue
        if cur is None: continue
        mi = INSN.search(line)
        if mi and not in_catches:
            addr = int(mi.group(1), 16); op = mi.group(2); rest = mi.group(3)
            raw = line.split('|')[0].split(':', 1)[1].strip().split() if ':' in line.split('|')[0] else []
            cur['insns'].append((addr, op, rest, raw)); continue
        s = line.strip()
        if s.startswith('catches'):
            in_catches = True; continue
        if in_catches:
            mr = re.match(r'^0x([0-9a-f]+) - 0x([0-9a-f]+)$', s)
            if mr:
                rng = {'start': int(mr.group(1), 16), 'end': int(mr.group(2), 16), 'handlers': []}; cur['tries'].append(rng); continue
            mh = re.match(r'^(<any>|\S+) -> 0x([0-9a-f]+)$', s)
            if mh and rng is not None:
                rng['handlers'].append((mh.group(1), int(mh.group(2), 16))); continue
            if s.startswith('positions') or s.startswith('locals') or s == '' or s.startswith('#') or s.startswith('source_file_idx'):
                in_catches = False; rng = None
    return methods

def check(meth):
    ins = meth['insns']
    if not any(op == 'monitor-enter' for _, op, _, _ in ins): return None
    idx = {a: i for i, (a, _, _, _) in enumerate(ins)}
    def try_of(a):
        for t in meth['tries']:
            if t['start'] <= a < t['end']: return t
        return None
    def succ(i):
        a, op, rest, raw = ins[i]; out = []
        if op.startswith('goto'):
            out.append(int(rest.split()[0], 16))
        elif op.startswith('if-'):
            out.append(int(rest.split(',')[-1].split('//')[0].strip(), 16))
            if i + 1 < len(ins): out.append(ins[i + 1][0])
        elif op.startswith('return') or op == 'throw':
            pass
        elif op.endswith('switch'):
            return None  # shortcut: dexdump truncates switch payloads, so a block reached only through a switch goes unwalked; read the targets from the dex if one ever matters
        else:
            if i + 1 < len(ins): out.append(ins[i + 1][0])
        return out
    depth_in = {ins[0][0]: 0}; work = deque([ins[0][0]]); problems = []; unknown = False
    # handler entries reachable via exceptions too
    while work:
        a = work.popleft(); i = idx.get(a)
        if i is None: continue
        d = depth_in[a]; op = ins[i][1]
        if op.endswith('-data'):
            continue
        after = d + 1 if op == 'monitor-enter' else (max(d - 1, 0) if op == 'monitor-exit' else d)
        if can_throw(op):
            t = try_of(a)
            if t is not None:
                has_any = any(h[0] == '<any>' for h in t['handlers'])
                if after > 0 and not has_any and not (op == 'monitor-enter' and after == 1):
                    problems.append((a, op, ins[i][2].strip()[:90]))
                for _, h in t['handlers']:
                    if depth_in.get(h, -1) < d: depth_in[h] = d; work.append(h)
        s = succ(i)
        if s is None: unknown = True; s = [ins[i + 1][0]] if i + 1 < len(ins) else []
        for b in s:
            if depth_in.get(b, -1) < after: depth_in[b] = after; work.append(b)
    return problems, unknown

if __name__ == '__main__':
    filt = sys.argv[2] if len(sys.argv) > 2 else None
    ms = parse(sys.argv[1]); n_mon = 0; n_bad = 0; n_part = 0
    for m in ms:
        if filt and not m['name'].startswith(filt): continue
        r = check(m)
        if r is None: continue
        n_mon += 1; probs, unk = r; n_part += unk
        if probs:
            n_bad += 1; print('REJECT', m['name'], 'switch-approx' if unk else '', probs[:3])
        elif filt:
            print('ok    ', m['name'], 'switch-approx' if unk else '', 'tries=%d' % len(m['tries']))
    print(f'methods parsed {len(ms)}, with monitor-enter {n_mon}, would be rejected {n_bad}, with switch targets unchecked {n_part}')
    sys.exit(1 if n_bad or not n_mon else 0)
