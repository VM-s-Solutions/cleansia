import { SendSitewidePromoCommand } from '@cleansia/admin-services';

export interface SitewidePushFormData {
  titleEn: string;
  titleCs: string;
  titleSk: string;
  titleUk: string;
  titleRu: string;
  bodyEn: string;
  bodyCs: string;
  bodySk: string;
  bodyUk: string;
  bodyRu: string;
}

export function buildSendSitewidePromoCommand(
  data: SitewidePushFormData
): SendSitewidePromoCommand {
  const command = new SendSitewidePromoCommand();
  command.titleEn = data.titleEn;
  command.titleCs = data.titleCs;
  command.titleSk = data.titleSk;
  command.titleUk = data.titleUk;
  command.titleRu = data.titleRu;
  command.bodyEn = data.bodyEn;
  command.bodyCs = data.bodyCs;
  command.bodySk = data.bodySk;
  command.bodyUk = data.bodyUk;
  command.bodyRu = data.bodyRu;
  return command;
}
