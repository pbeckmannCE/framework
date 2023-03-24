//////////////////////////////////
//Auto-generated. Do NOT modify!//
//////////////////////////////////

import { MessageKey, QueryKey, Type, EnumType, registerSymbol } from '../../Signum.React/Reflection'
import * as Entities from '../../Signum.React/Signum.Entities'
import * as DynamicQuery from '../../Signum.React/Signum.DynamicQuery'
import * as Basics from '../../Signum.React/Signum.Basics'
import * as Operations from '../../Signum.React/Signum.Operations'
import * as UserAssets from '../Signum.UserAssets.React/Signum.Entities.UserAssets'
import * as Queries from '../Signum.UserAssets.React/Signum.UserAssets.Queries'
import * as Chart from './Signum.Chart'


export const UserChartEntity = new Type<UserChartEntity>("UserChart");
export interface UserChartEntity extends Entities.Entity, UserAssets.IUserAssetEntity {
  Type: "UserChart";
  query: DynamicQuery.QueryEntity;
  entityType: Entities.Lite<Basics.TypeEntity> | null;
  hideQuickLink: boolean;
  owner: Entities.Lite<Entities.Entity> | null;
  displayName: string;
  includeDefaultFilters: boolean | null;
  maxRows: number | null;
  chartScript: Chart.ChartScriptSymbol;
  parameters: Entities.MList<Chart.ChartParameterEmbedded>;
  columns: Entities.MList<Chart.ChartColumnEmbedded>;
  filters: Entities.MList<Queries.QueryFilterEmbedded>;
  customDrilldowns: Entities.MList<Entities.Lite<Entities.Entity>>;
  guid: string /*Guid*/;
}

export module UserChartOperation {
  export const Save : Operations.ExecuteSymbol<UserChartEntity> = registerSymbol("Operation", "UserChartOperation.Save");
  export const Delete : Operations.DeleteSymbol<UserChartEntity> = registerSymbol("Operation", "UserChartOperation.Delete");
}

