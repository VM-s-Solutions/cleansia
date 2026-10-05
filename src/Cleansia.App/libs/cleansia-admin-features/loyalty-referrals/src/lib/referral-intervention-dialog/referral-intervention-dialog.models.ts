export type ReferralInterventionMode = 'reverse' | 'forceQualify' | 'release' | 'reject';

export interface ReferralInterventionSubmit {
  mode: ReferralInterventionMode;
  reason: string;
}

export interface ReferralInterventionCopy {
  readonly titleKey: string;
  readonly hintKey: string;
  readonly submitKey: string;
  readonly destructive: boolean;
}

const INTERVENTION = 'pages.loyalty_referrals.intervention';

export const REFERRAL_INTERVENTION_COPY: Readonly<Record<ReferralInterventionMode, ReferralInterventionCopy>> = {
  reverse: {
    titleKey: `${INTERVENTION}.title_reverse`,
    hintKey: `${INTERVENTION}.hint_reverse`,
    submitKey: `${INTERVENTION}.submit_reverse`,
    destructive: true,
  },
  forceQualify: {
    titleKey: `${INTERVENTION}.title_force_qualify`,
    hintKey: `${INTERVENTION}.hint_force_qualify`,
    submitKey: `${INTERVENTION}.submit_force_qualify`,
    destructive: false,
  },
  release: {
    titleKey: `${INTERVENTION}.title_release`,
    hintKey: `${INTERVENTION}.hint_release`,
    submitKey: `${INTERVENTION}.submit_release`,
    destructive: false,
  },
  reject: {
    titleKey: `${INTERVENTION}.title_reject`,
    hintKey: `${INTERVENTION}.hint_reject`,
    submitKey: `${INTERVENTION}.submit_reject`,
    destructive: true,
  },
};
