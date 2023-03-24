//////////////////////////////////
//Auto-generated. Do NOT modify!//
//////////////////////////////////

import { MessageKey, QueryKey, Type, EnumType, registerSymbol } from '../../Signum.React/Reflection'
import * as Entities from '../../Signum.React/Signum.Entities'
import * as Operations from '../../Signum.React/Signum.Operations'
import * as Processes from '../Signum.Processes.React/Signum.Processes'
import * as Scheduler from '../Signum.Scheduler.React/Signum.Scheduler'
import * as Templates from '../Signum.Mailing.React/Signum.Mailing.Templates'
import * as Mailing from '../Signum.Mailing.React/Signum.Mailing'
import * as UserQueries from '../Signum.UserQueries.React/Signum.UserQueries'
import * as Templating from '../Signum.Templating.React/Signum.Templating'


export module EmailMessagePackageOperation {
  export const ReSendEmails : Operations.ConstructSymbol_FromMany<Processes.ProcessEntity, Mailing.EmailMessageEntity> = registerSymbol("Operation", "EmailMessagePackageOperation.ReSendEmails");
}

export module EmailMessageProcess {
  export const CreateEmailsSendAsync : Processes.ProcessAlgorithmSymbol = registerSymbol("ProcessAlgorithm", "EmailMessageProcess.CreateEmailsSendAsync");
  export const SendEmails : Processes.ProcessAlgorithmSymbol = registerSymbol("ProcessAlgorithm", "EmailMessageProcess.SendEmails");
}

export const EmailPackageEntity = new Type<EmailPackageEntity>("EmailPackage");
export interface EmailPackageEntity extends Entities.Entity, Processes.IProcessDataEntity {
  Type: "EmailPackage";
  name: string | null;
}

export const SendEmailTaskEntity = new Type<SendEmailTaskEntity>("SendEmailTask");
export interface SendEmailTaskEntity extends Entities.Entity, Scheduler.ITaskEntity {
  Type: "SendEmailTask";
  name: string;
  emailTemplate: Entities.Lite<Templates.EmailTemplateEntity>;
  uniqueTarget: Entities.Lite<Entities.Entity> | null;
  targetsFromUserQuery: Entities.Lite<UserQueries.UserQueryEntity> | null;
  modelConverter: Templating.ModelConverterSymbol | null;
}

export module SendEmailTaskOperation {
  export const Save : Operations.ExecuteSymbol<SendEmailTaskEntity> = registerSymbol("Operation", "SendEmailTaskOperation.Save");
}

