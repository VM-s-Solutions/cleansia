import type { Config } from 'jest';
import { getJestProjectsAsync } from '@nx/jest';

export default async (): Promise<Config> => ({
  projects: await getJestProjectsAsync(),
  // On a cold transform cache every worker builds one TypeScript program per project it happens
  // to be handed, and by the fourth or fifth project the heap passes 4 GB and the worker dies with
  // whichever suite it was loading. Recycling a worker past this mark keeps it well under that.
  workerIdleMemoryLimit: '1.5GB',
});
