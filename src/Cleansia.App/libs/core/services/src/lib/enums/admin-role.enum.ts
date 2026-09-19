/**
 * The administrator's role as the token response names it (`JwtTokenResponse.adminRole`, the
 * enum member's name). A second axis beside {@link Role}: the profile answers "which audience",
 * this answers "which administrator". Meaningful only when the profile is Administrator.
 */
export enum AdminRoleName {
  ADMINISTRATOR = 'Administrator',
  MANAGER = 'Manager',
  SUPPORT = 'Support',
  ACCOUNTANT = 'Accountant',
}
