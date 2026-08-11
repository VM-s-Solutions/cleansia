/**
 * Pins the workers' zone west of UTC before they are forked. A date-only member arrives parsed as
 * UTC midnight and is serialized back from LOCAL calendar components, so on a UTC runner — which is
 * what CI is — the two readings coincide and a replayed birth date shifting a day is undetectable.
 * V8 caches the zone at isolate startup, so this cannot be done from inside a spec.
 */
module.exports = async () => {
  process.env.TZ = 'America/Los_Angeles';
};
