﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Signum.Engine.Maps;
using Signum.Entities.Authorization;
using Signum.Entities.Basics;
using Signum.Engine.DynamicQuery;
using Signum.Engine.Basics;
using Signum.Utilities;
using Signum.Utilities.DataStructures;
using System.Threading;
using Signum.Entities;
using Signum.Engine.Operations;
using System.Reflection;
using Signum.Entities.DynamicQuery;

namespace Signum.Engine.Authorization
{
    public static class OperationAuthLogic
    {
        static AuthCache<RuleOperationDN, OperationAllowedRule, OperationDN, Enum, OperationAllowed> cache;

        public static IManualAuth<Enum, OperationAllowed> Manual { get { return cache; } }

        public static bool IsStarted { get { return cache != null; } }

        public static void Start(SchemaBuilder sb)
        {
            if (sb.NotDefined(MethodInfo.GetCurrentMethod()))
            {
                AuthLogic.AssertStarted(sb);
                OperationLogic.AssertStarted(sb);

                OperationLogic.AllowOperation += new AllowOperationHandler(OperationLogic_AllowOperation);

                cache = new AuthCache<RuleOperationDN, OperationAllowedRule, OperationDN, Enum, OperationAllowed>(sb,
                     MultiEnumLogic<OperationDN>.ToEnum,
                     MultiEnumLogic<OperationDN>.ToEntity,
                     merger: new OperationMerger(),
                     invalidateWithTypes: true,
                     coercer:  OperationCoercer.Instance);

                AuthLogic.SuggestRuleChanges += SuggestOperationRules;
                AuthLogic.ExportToXml += () => cache.ExportXml("Operations", "Operation", OperationDN.UniqueKey, b => b.ToString());
                AuthLogic.ImportFromXml += (x, roles, replacements) =>
                {
                    string replacementKey = typeof(OperationDN).Name;

                    replacements.AskForReplacements(
                        x.Element("Operations").Elements("Role").SelectMany(r => r.Elements("Operation")).Select(p => p.Attribute("Resource").Value).ToHashSet(),
                        MultiEnumLogic<OperationDN>.AllUniqueKeys().ToHashSet(),
                        replacementKey);

                    return cache.ImportXml(x, "Operations", "Operation", roles,
                        s => MultiEnumLogic<OperationDN>.TryToEntity(replacements.Apply(replacementKey, s)), EnumExtensions.ToEnum<OperationAllowed>);
                };
            }
        }

        static Action<Lite<RoleDN>> SuggestOperationRules()
        {
            var operations = (from type in Schema.Current.Tables.Keys
                              let ops = OperationLogic.ServiceGetOperationInfos(type).Where(oi => oi.OperationType == OperationType.Execute && oi.Lite == false).ToList()
                              where ops.Any()
                              select KVP.Create(type, ops.ToList())).ToDictionary();

            return r =>
            {
                bool? warnings = null;

                foreach (var type in operations.Keys)
                {
                    var ta = TypeAuthLogic.GetAllowed(r, type);
                    var max = ta.MaxCombined();


                    if (max.GetUI() == TypeAllowedBasic.None)
                    {
                        OperationAllowed typeAllowed =
                             max.GetUI() >= TypeAllowedBasic.Modify ? OperationAllowed.Allow :
                             max.GetDB() >= TypeAllowedBasic.Modify ? OperationAllowed.DBOnly :
                             OperationAllowed.None;

                        var ops = operations[type];
                        foreach (var oi in ops.Where(o => GetOperationAllowed(r, o.Key) > typeAllowed))
                        {
                            bool isError = ta.MaxDB() == TypeAllowedBasic.None;

                            SafeConsole.WriteLineColor(ConsoleColor.DarkGray, "{0}: Operation {1} is {2} but type {3} is [{4}]".Formato(
                                  isError ? "Error" : "Warning",
                                OperationDN.UniqueKey(oi.Key),
                                GetOperationAllowed(r, oi.Key),
                                type.Name,
                                ta));


                            SafeConsole.WriteColor(ConsoleColor.DarkRed, "Disallow ");
                            string message = "{0} to {1} for {2}?".Formato(OperationDN.UniqueKey(oi.Key), typeAllowed, r);
                            if (isError ? SafeConsole.Ask(message) : SafeConsole.Ask(ref warnings, message))
                            {
                                Manual.SetAllowed(r, oi.Key, typeAllowed);
                                SafeConsole.WriteLineColor(ConsoleColor.Red, "Disallowed");
                            }
                            else
                            {
                                SafeConsole.WriteLineColor(ConsoleColor.White, "Skipped");
                            }
                        }
                    }
                    else
                    {
                        var ops = operations[type];
                        if (ta.MaxUI() > TypeAllowedBasic.Modify && ops.Any() && !ops.Any(oi => GetOperationAllowed(r, oi.Key, inUserInterface: true)))
                        {
                            SafeConsole.WriteLineColor(ConsoleColor.DarkGray, "Warning: Type {0} is [{1}] but no save operation is allowed".Formato(type.Name, ta));
                            var only = ops.Only();
                            if (only != null)
                            {
                                SafeConsole.WriteColor(ConsoleColor.DarkGreen, "Allow ");
                                if (SafeConsole.Ask(ref warnings, "{0} to {1}?".Formato(OperationDN.UniqueKey(only.Key), r)))
                                {
                                    Manual.SetAllowed(r, only.Key, OperationAllowed.Allow);
                                    SafeConsole.WriteLineColor(ConsoleColor.Green, "Allowed");
                                }
                                else
                                {
                                    SafeConsole.WriteLineColor(ConsoleColor.White, "Skipped");
                                }
                            }
                        }
                    }
                }
            };
        }
        static bool OperationLogic_AllowOperation(Enum operationKey, bool inUserInterface)
        {
            return GetOperationAllowed(operationKey, inUserInterface);
        }

        public static OperationRulePack GetOperationRules(Lite<RoleDN> role, TypeDN typeDN)
        {
            var resources = OperationLogic.GetAllOperationInfos(TypeLogic.DnToType[typeDN]).Select(a => MultiEnumLogic<OperationDN>.ToEntity(a.Key));
            var result = new OperationRulePack { Role = role, Type = typeDN, };

            cache.GetRules(result, resources);

            var coercer = OperationCoercer.Instance.GetCoerceValue(role);
            result.Rules.ForEach(r => r.CoercedValues = EnumExtensions.GetValues<OperationAllowed>().Where(a => !coercer(MultiEnumLogic<OperationDN>.ToEnum(r.Resource), a).Equals(a)).ToArray());
            
            return result;
        }

        public static void SetOperationRules(OperationRulePack rules)
        {
            var keys = OperationLogic.GetAllOperationInfos(TypeLogic.DnToType[rules.Type])
                .Select(a => OperationDN.UniqueKey(a.Key)).ToArray();

            cache.SetRules(rules, r => keys.Contains(r.Key));
        }

        public static bool GetOperationAllowed(Lite<RoleDN> role, Enum operationKey, bool inUserInterface)
        {
            OperationAllowed allowed = GetOperationAllowed(role, operationKey);

            return allowed == OperationAllowed.Allow || allowed == OperationAllowed.DBOnly && !inUserInterface;
        }

        public static OperationAllowed GetOperationAllowed(Lite<RoleDN> role, Enum operationKey)
        {
            return cache.GetAllowed(role, operationKey);
        }

        public static bool GetOperationAllowed(Enum operationKey, bool inUserInterface)
        {
            if (!AuthLogic.IsEnabled || ExecutionMode.InGlobal)
                return true;

            if (GetTemporallyAllowed(operationKey))
                return true;

            OperationAllowed allowed =cache.GetAllowed(RoleDN.Current.ToLite(), operationKey);

            return allowed == OperationAllowed.Allow || allowed == OperationAllowed.DBOnly && !inUserInterface;
        }

        public static AuthThumbnail? GetAllowedThumbnail(Lite<RoleDN> role, Type entityType)
        {
            return OperationLogic.GetAllOperationInfos(entityType).Select(oi => cache.GetAllowed(role, oi.Key)).Collapse();
        }

        public static Dictionary<Enum, OperationAllowed> AllowedOperations()
        {
            return OperationLogic.AllKeys().ToDictionary(k => k, k => cache.GetAllowed(RoleDN.Current.ToLite(), k));
        }

        static readonly Variable<ImmutableStack<Enum>> tempAllowed = Statics.ThreadVariable<ImmutableStack<Enum>>("authTempOperationsAllowed");

        public static IDisposable AllowTemporally(Enum operationKey)
        {
            tempAllowed.Value = (tempAllowed.Value ?? ImmutableStack<Enum>.Empty).Push(operationKey);

            return new Disposable(() => tempAllowed.Value = tempAllowed.Value.Pop());
        }

        internal static bool GetTemporallyAllowed(Enum operationKey)
        {
            var ta = tempAllowed.Value;
            if (ta == null || ta.IsEmpty)
                return false;

            return ta.Contains(operationKey);
        }

        public static OperationAllowed MaxTypePermission(Enum operationKey, TypeAllowedBasic minimum,  Func<Type, TypeAllowedAndConditions> allowed)
        {
            return OperationLogic.FindTypes(operationKey).Max(t =>
            {
                if (!TypeLogic.TypeToDN.ContainsKey(t))
                    return OperationAllowed.Allow;

                var typeAllowed = allowed(t);

                return minimum <= typeAllowed.MaxUI() ? OperationAllowed.Allow :
                    minimum <= typeAllowed.MaxDB() ? OperationAllowed.DBOnly :
                    OperationAllowed.None;
            });
        }
    }

    class OperationMerger : Merger<Enum, OperationAllowed>
    {
        protected override OperationAllowed Union(Enum key, Lite<RoleDN> role, IEnumerable<OperationAllowed> baseValues)
        {
            var result = Max(baseValues);

            if (key != null && result == OperationAuthLogic.MaxTypePermission(key, TypeAllowedBasic.Modify, t => TypeAuthLogic.GetAllowedBase(role, t)))
                return OperationAuthLogic.MaxTypePermission(key, TypeAllowedBasic.Modify, t => TypeAuthLogic.GetAllowed(role, t));

            return result;
        }

        static OperationAllowed Max(IEnumerable<OperationAllowed> baseValues)
        {
            OperationAllowed result = OperationAllowed.None;

            foreach (var item in baseValues)
            {
                if (item > result)
                    result = item;

                if (result == OperationAllowed.Allow)
                    return result;

            }
            return result;
        }

        protected override OperationAllowed Intersection(Enum key, Lite<RoleDN> role, IEnumerable<OperationAllowed> baseValues)
        {
            var result = Min(baseValues);

            if (key != null && result == OperationAuthLogic.MaxTypePermission(key, TypeAllowedBasic.Modify, t => TypeAuthLogic.GetAllowedBase(role, t)))
                return OperationAuthLogic.MaxTypePermission(key, TypeAllowedBasic.Modify, t => TypeAuthLogic.GetAllowed(role, t));

            return result;
        }

        private static OperationAllowed Min(IEnumerable<OperationAllowed> baseValues)
        {
            OperationAllowed result = OperationAllowed.Allow;

            foreach (var item in baseValues)
            {
                if (item < result)
                    result = item;

                if (result == OperationAllowed.None)
                    return result;

            }
            return result;
        }

        public override Func<Enum, OperationAllowed> MergeDefault(Lite<RoleDN> role, IEnumerable<Func<Enum, OperationAllowed>> baseDefaultValues)
        {
            return key => OperationAuthLogic.MaxTypePermission(key, TypeAllowedBasic.Modify, t => TypeAuthLogic.GetAllowed(role, t));
        }
    }

    class OperationCoercer : Coercer<OperationAllowed, Enum>
    {
        public static readonly OperationCoercer Instance = new OperationCoercer();

        private OperationCoercer()
        {
        }

        public override Func<Enum, OperationAllowed, OperationAllowed> GetCoerceValue(Lite<RoleDN> role)
        {
            return (operationKey, allowed) =>
            {
                var required = OperationAuthLogic.MaxTypePermission(operationKey, TypeAllowedBasic.Read, t => TypeAuthLogic.GetAllowed(role, t));

                return allowed < required ? allowed : required;
            };
        }

        public override Func<Lite<RoleDN>, OperationAllowed, OperationAllowed> GetCoerceValueManual(Enum operationKey)
        {
            return (role, allowed) =>
            {
                var required = OperationAuthLogic.MaxTypePermission(operationKey, TypeAllowedBasic.Read, t => TypeAuthLogic.Manual.GetAllowed(role, t));

                return allowed < required ? allowed : required;
            };
        }
    }
}
