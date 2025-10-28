import { Code } from '@cleansia/services';

export const CODE_FEATURE_KEY = 'code';

export interface CodeState {
  data: Code[];
  loading: boolean;
  error: string | null;
}

export const codeInitialState: CodeState = {
  data: [],
  loading: false,
  error: null,
};
