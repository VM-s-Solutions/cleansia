import { WorkContractDto } from '@cleansia/admin-services';
import { formatDate, languageDisplayName, WorkContractFactRow } from '@cleansia/utils';

export interface AdminWorkContractDialogData {
  acceptanceId: string;
}

export interface WorkContractRow extends WorkContractFactRow {
  valueKey?: string;
}

const DIALOG_KEY = 'pages.order_detail.work_contract.dialog';
export const WORK_CONTRACT_FACTS_KEY = `${DIALOG_KEY}.facts`;
const ACCEPTANCE_KEY = `${DIALOG_KEY}.acceptance`;

export const WORK_CONTRACT_HASH_ROW = `${ACCEPTANCE_KEY}.hash`;
const HASH_UNAVAILABLE_KEY = `${ACCEPTANCE_KEY}.hash_unavailable`;
const HASH_NOT_VISIBLE_KEY = `${ACCEPTANCE_KEY}.hash_not_visible`;

function buildHashRow(acceptedTextHash: string | null, hashReadable: boolean): WorkContractRow {
  if (!hashReadable) return { labelKey: WORK_CONTRACT_HASH_ROW, value: '', valueKey: HASH_NOT_VISIBLE_KEY };
  if (!acceptedTextHash) return { labelKey: WORK_CONTRACT_HASH_ROW, value: '', valueKey: HASH_UNAVAILABLE_KEY };
  return { labelKey: WORK_CONTRACT_HASH_ROW, value: acceptedTextHash };
}

export function buildWorkContractAcceptanceRows(
  contract: WorkContractDto | null,
  acceptedTextHash: string | null,
  lang: string,
  hashReadable: boolean
): WorkContractRow[] {
  const acceptance = contract?.acceptance;
  if (!acceptance) return [];
  return [
    { labelKey: `${ACCEPTANCE_KEY}.accepted_on`, value: formatDate(acceptance.acceptedOn, lang, 'dateTime') },
    { labelKey: `${ACCEPTANCE_KEY}.version`, value: acceptance.documentVersion ?? '' },
    {
      labelKey: `${ACCEPTANCE_KEY}.language`,
      value: languageDisplayName(acceptance.acceptedLanguage ?? '', lang),
    },
    buildHashRow(acceptedTextHash, hashReadable),
  ];
}
