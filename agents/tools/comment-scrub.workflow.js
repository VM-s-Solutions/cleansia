export const meta = {
  name: 'comment-scrub',
  description: 'Strip rot-prone tracker references from source comments repo-wide, keep the WHY + stable refs',
  phases: [
    { title: 'Scrub' },
    { title: 'Verify' },
  ],
}

// The curated file list (code only, no migrations/generated/bin/obj) lives here; each agent reads it
// and operates on its assigned line-range slice so the slices are disjoint.
const LIST = '/tmp/scrub_final.txt'
const BATCH = 6 // files per agent — small enough that each edit is careful, big enough to bound agent count

// args.fileCount is the number of lines in LIST (passed in by the caller); fall back to a safe upper bound.
const fileCount = (args && args.fileCount) || 200
const batches = []
for (let start = 1; start <= fileCount; start += BATCH) {
  batches.push({ start, end: Math.min(start + BATCH - 1, fileCount) })
}

const RULE = [
  'You are scrubbing rot-prone TRACKER REFERENCES out of source comments, per the new',
  'agents/knowledge/conventions.md -> "Comments — write almost none" rule. Operate ONLY on the files',
  `at line range [start,end] (1-based, inclusive) of the list file ${LIST}. Read those exact files.`,
  '',
  'For EACH comment in those files:',
  '1. STRIP these ephemeral tracker tokens wherever they appear in a comment (they point into a backlog',
  '   that moves and rot into dangling pointers): T-0NNN, "PR review #N", "AC<n>" / "ACn", and the audit',
  '   finding codes IDA-SEC-NN, PERF-IDA-NN, LG-SEC-NN, SEC-DSP-NN, EMP-GAP-NN, BSP-N, FISCAL-RECON,',
  '   FUNC-CORE, LOY-NNN, IMP-N, BUG-NN. Remove the token and any now-dangling punctuation/parens/leading',
  '   "X — " label it introduced, leaving clean prose.',
  '2. KEEP the explanatory WHY text, and KEEP these STABLE references (they resolve to permanent repo',
  '   docs): ADR-NNNN (e.g. ADR-0002) and its sub-ids like D3.3/C-B; the security laws S1..S10 and',
  '   S7a/S7b. Do NOT strip those.',
  '3. If, after stripping the token, the comment is pure WHAT/noise (restates the code, a section banner,',
  '   e.g. "// update the user", "// ─── helpers ───"), DELETE the whole comment line(s).',
  '4. If the comment carried genuine non-obvious WHY (a race/ordering/atomicity/fiscal subtlety), keep',
  '   that prose — just without the tracker token.',
  '',
  'HARD CONSTRAINTS:',
  '- Change ONLY comments. NEVER change code, identifiers, strings, attributes, or test assertions.',
  '- Do not reflow/rename anything. Keep the same comment style (// vs ///  vs /* */).',
  '- A test name or a string literal that happens to contain one of these tokens is CODE — do NOT touch it.',
  '- Preserve XML doc structure (/// <summary> etc.); only edit the prose inside.',
  'Return a short list of files you edited and, per file, how many comment lines you stripped vs deleted.',
].join('\n')

const VERIFY = [
  'Adversarially verify a comment-scrub edit. Read the same files (range given) and confirm via',
  'git diff: (a) ONLY comment lines changed — no code/identifier/string/attribute/test-assertion change;',
  '(b) no remaining ephemeral tracker token (T-0NNN, PR review #N, AC<n>, IDA-SEC/PERF-IDA/LG-SEC/',
  'SEC-DSP/EMP-GAP/BSP/FISCAL-RECON/FUNC-CORE) in a comment in those files; (c) stable refs (ADR-NNNN,',
  'S1..S10/S7a) and genuine WHY prose were PRESERVED, not stripped; (d) nothing of value was lost.',
  'Report ok=false with specifics if any check fails.',
].join('\n')

const SCRUB_SCHEMA = {
  type: 'object',
  properties: {
    editedFiles: { type: 'array', items: { type: 'string' } },
    notes: { type: 'string' },
  },
  required: ['editedFiles'],
}
const VERIFY_SCHEMA = {
  type: 'object',
  properties: {
    ok: { type: 'boolean' },
    problems: { type: 'array', items: { type: 'string' } },
  },
  required: ['ok'],
}

log(`Scrubbing ${fileCount} files in ${batches.length} batches of ${BATCH}.`)

const results = await pipeline(
  batches,
  (b) =>
    agent(`${RULE}\n\nYour assigned range: lines ${b.start}..${b.end} of ${LIST}.`,
      { label: `scrub:${b.start}-${b.end}`, phase: 'Scrub', agentType: 'backend', schema: SCRUB_SCHEMA })
      .then((r) => ({ batch: b, edited: (r && r.editedFiles) || [] })),
  (scrub) =>
    agent(`${VERIFY}\n\nRange: lines ${scrub.batch.start}..${scrub.batch.end} of ${LIST}. Files reportedly edited: ${JSON.stringify(scrub.edited)}`,
      { label: `verify:${scrub.batch.start}-${scrub.batch.end}`, phase: 'Verify', agentType: 'reviewer', schema: VERIFY_SCHEMA })
      .then((v) => ({ ...scrub, verdict: v }))
)

const clean = results.filter(Boolean)
const failures = clean.filter((r) => r.verdict && r.verdict.ok === false)
return {
  batches: clean.length,
  editedFileCount: clean.reduce((n, r) => n + r.edited.length, 0),
  failures: failures.map((f) => ({ range: `${f.batch.start}-${f.batch.end}`, problems: f.verdict.problems })),
}
