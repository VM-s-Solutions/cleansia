import { Code } from '@cleansia/partner-services';
import { createFeatureSelector, createSelector } from '@ngrx/store';
import { CodeTypes } from './code-types';
import { CODE_FEATURE_KEY, CodeState } from './code.state';

export const selectCodeState =
  createFeatureSelector<CodeState>(CODE_FEATURE_KEY);

export const selectCodeData = createSelector(
  selectCodeState,
  (state: CodeState) => state.data
);

export const selectCodeLoading = createSelector(
  selectCodeState,
  (state: CodeState) => state.loading
);

export const selectCodeError = createSelector(
  selectCodeState,
  (state: CodeState) => state.error
);

const createCodeSelector = (codeType: string) =>
  createSelector(selectCodeData, (data: Code[]) =>
    data ? extractCodesByType(data, codeType) : []
  );

export const selectOrderStatusCodes = createCodeSelector(
  CodeTypes.ORDER_STATUS
);

export const selectPaymentStatusCodes = createCodeSelector(
  CodeTypes.PAYMENT_STATUS
);

export const selectPaymentTypeCodes = createCodeSelector(
  CodeTypes.PAYMENT_TYPE
);

export const selectInvoiceStatusCodes = createCodeSelector(
  CodeTypes.INVOICE_STATUS
);

export const selectDayOfWeekCodes = createCodeSelector(CodeTypes.DAY_OF_WEEK);

export function extractCodesByType(codes: Code[], type: string): Code[] {
  return codes.filter((x) => x.type?.toLowerCase() === type.toLowerCase());
}

// Helper selector to get code by type and value
export const selectCodeByTypeAndValue = (type: string, value: number) =>
  createSelector(selectCodeData, (codes: Code[]) => {
    const typedCodes = extractCodesByType(codes, type);
    return typedCodes.find((code) => code.value === value);
  });
