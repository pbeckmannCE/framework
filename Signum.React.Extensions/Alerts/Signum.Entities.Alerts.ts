//////////////////////////////////
//Auto-generated. Do NOT modify!//
//////////////////////////////////
import { MessageKey, QueryKey, Type, EnumType, registerSymbol } from 'Framework/Signum.React/Scripts/Reflection' 

import * as Entities from 'Framework/Signum.React/Scripts/Signum.Entities' 
export const AlertEntity_Type = new Type<AlertEntity>("AlertEntity");
export interface AlertEntity extends Entities.Entity {
    target?: Entities.Lite<Entities.Entity>;
    creationDate?: string;
    alertDate?: string;
    attendedDate?: string;
    title?: string;
    text?: string;
    createdBy?: Entities.Lite<Entities.Basics.IUserEntity>;
    attendedBy?: Entities.Lite<Entities.Basics.IUserEntity>;
    alertType?: AlertTypeEntity;
    state?: AlertState;
}

export module AlertMessage {
    export const Alert = new MessageKey("AlertMessage", "Alert");
    export const NewAlert = new MessageKey("AlertMessage", "NewAlert");
    export const Alerts = new MessageKey("AlertMessage", "Alerts");
    export const Alerts_Attended = new MessageKey("AlertMessage", "Alerts_Attended");
    export const Alerts_Future = new MessageKey("AlertMessage", "Alerts_Future");
    export const Alerts_NotAttended = new MessageKey("AlertMessage", "Alerts_NotAttended");
    export const CheckedAlerts = new MessageKey("AlertMessage", "CheckedAlerts");
    export const CreateAlert = new MessageKey("AlertMessage", "CreateAlert");
    export const FutureAlerts = new MessageKey("AlertMessage", "FutureAlerts");
    export const WarnedAlerts = new MessageKey("AlertMessage", "WarnedAlerts");
}

export module AlertOperation {
    export const CreateAlertFromEntity : Entities.ConstructSymbol_From<AlertEntity, Entities.Entity> = registerSymbol({ key: "AlertOperation.CreateAlertFromEntity" });
    export const SaveNew : Entities.ExecuteSymbol<AlertEntity> = registerSymbol({ key: "AlertOperation.SaveNew" });
    export const Save : Entities.ExecuteSymbol<AlertEntity> = registerSymbol({ key: "AlertOperation.Save" });
    export const Attend : Entities.ExecuteSymbol<AlertEntity> = registerSymbol({ key: "AlertOperation.Attend" });
    export const Unattend : Entities.ExecuteSymbol<AlertEntity> = registerSymbol({ key: "AlertOperation.Unattend" });
}

export enum AlertState {
    New,
    Saved,
    Attended,
}
export const AlertState_Type = new EnumType<AlertState>("AlertState", AlertState);

export const AlertTypeEntity_Type = new Type<AlertTypeEntity>("AlertTypeEntity");
export interface AlertTypeEntity extends Entities.Basics.SemiSymbol {
}

export module AlertTypeOperation {
    export const Save : Entities.ExecuteSymbol<AlertTypeEntity> = registerSymbol({ key: "AlertTypeOperation.Save" });
}

