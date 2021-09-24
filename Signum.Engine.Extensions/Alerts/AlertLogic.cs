using System;
using System.Collections.Generic;
using System.Linq;
using Signum.Engine.Operations;
using Signum.Entities.Authorization;
using Signum.Entities;
using Signum.Engine.Maps;
using Signum.Utilities.Reflection;
using Signum.Engine.DynamicQuery;
using System.Reflection;
using Signum.Utilities;
using Signum.Entities.Basics;
using Signum.Entities.Alerts;
using System.Linq.Expressions;
using Signum.Engine.Extensions.Basics;
using Signum.Engine.Basics;
using Signum.Engine.Authorization;
using Signum.Engine.Mailing;
using Signum.Entities.Mailing;
using Signum.Engine.Templating;
using Signum.Engine.Scheduler;
using Signum.Entities.UserAssets;
using Microsoft.AspNetCore.Html;
using Signum.Engine;
using Signum.Entities.Scheduler;
using System.Text.RegularExpressions;

namespace Signum.Engine.Alerts
{
    public static class AlertLogic
    {
        [AutoExpressionField]
        public static IQueryable<AlertEntity> Alerts(this Entity e) => 
            As.Expression(() => Database.Query<AlertEntity>().Where(a => a.Target.Is(e)));

        [AutoExpressionField]
        public static IQueryable<AlertEntity> MyActiveAlerts(this Entity e) => 
            As.Expression(() => e.Alerts().Where(a => a.Recipient == UserHolder.Current.ToLite() && a.CurrentState == AlertCurrentState.Alerted));

        public static Func<IUserEntity?> DefaultRecipient = () => null;

        public static Dictionary<AlertTypeSymbol, AlertTypeOptions> SystemAlertTypes = new Dictionary<AlertTypeSymbol, AlertTypeOptions>();

        public static string? GetText(this AlertTypeSymbol? alertType)
        {
            if (alertType == null)
                return null;

            var options = SystemAlertTypes.GetOrThrow(alertType);

            return options.GetText?.Invoke();
        }

        public static bool Started = false;

        public static void AssertStarted(SchemaBuilder sb)
        {
            sb.AssertDefined(ReflectionTools.GetMethodInfo(() => Start(null!, null!)));
        }


        public static void Start(SchemaBuilder sb, params Type[] registerExpressionsFor)
        {
            if (sb.NotDefined(MethodInfo.GetCurrentMethod()))
            {
                sb.Include<AlertEntity>()
                    .WithQuery(() => a => new
                    {
                        Entity = a,
                        a.Id,
                        a.AlertDate,
                        a.AlertType,
                        a.State,
                        a.Title,
                        Text = a.Text!.Etc(100),
                        a.Target,
                        a.Recipient,
                        a.CreationDate,
                        a.CreatedBy,
                        a.AttendedDate,
                        a.AttendedBy,
                    });

                AlertGraph.Register();

                As.ReplaceExpression((AlertEntity a) => a.Text, a => a.TextField.HasText() ? a.TextField : a.AlertType.GetText());

                Schema.Current.EntityEvents<AlertEntity>().Retrieved += (a, ctx) =>
                {
                    a.TextFromAlertType = a.AlertType?.GetText();
                };

                sb.Include<AlertTypeSymbol>()
                    .WithSave(AlertTypeOperation.Save)
                    .WithDelete(AlertTypeOperation.Delete)
                    .WithQuery(() => t => new
                    {
                        Entity = t,
                        t.Id,
                        t.Name,
                        t.Key,
                    });

                SemiSymbolLogic<AlertTypeSymbol>.Start(sb, () => SystemAlertTypes.Keys);

                if (registerExpressionsFor != null)
                {
                    var alerts = Signum.Utilities.ExpressionTrees.Linq.Expr((Entity ident) => ident.Alerts());
                    var myActiveAlerts = Signum.Utilities.ExpressionTrees.Linq.Expr((Entity ident) => ident.MyActiveAlerts());
                    foreach (var type in registerExpressionsFor)
                    {
                        QueryLogic.Expressions.Register(new ExtensionInfo(type, alerts, alerts.Body.Type, "Alerts", () => typeof(AlertEntity).NicePluralName()));
                        QueryLogic.Expressions.Register(new ExtensionInfo(type, myActiveAlerts, myActiveAlerts.Body.Type, "MyActiveAlerts", () => AlertMessage.MyActiveAlerts.NiceToString()));
                    }
                }
                

                Started = true;
            }
        }

        public static void RegisterAlertNotificationMail(SchemaBuilder sb)
        {
            EmailModelLogic.RegisterEmailModel<AlertNotificationMail>(() => new EmailTemplateEntity
            {
                Messages = CultureInfoLogic.ForEachCulture(culture => new EmailTemplateMessageEmbedded(culture)
                {
                    Text = $@"
<p>{AlertMessage.Hi0.NiceToString("@[m:Entity]")},</p>
<p>{AlertMessage.YouHaveSomePendingAlerts.NiceToString()}</p>
<ul>
@foreach[m:Alerts] as $a
    <li>
        <strong>@[$a.Title]:</strong><br/>
        @[m:TextFormatted]<br/>
        <small>@[$a.AlertDate] @[$a.CreatedBy]</small>
    </li>
 @endforeach
</ul>
<p>{AlertMessage.PleaseVisit0.NiceToString(@"<a href=""@[g:UrlLeft]"">@[g:UrlLeft]</a>")}</p>",
                    Subject = AlertMessage.NewUnreadNotifications.NiceToString(),
                }).ToMList()
            });

            sb.Include<SendNotificationEmailTaskEntity>()
                  .WithSave(SendNotificationEmailTaskOperation.Save)
                  .WithQuery(() => e => new
                  {
                      Entity = e,
                      e.Id,
                      e.SendNotificationsOlderThan,
                      e.SendBehavior,
                  });

            SchedulerLogic.ExecuteTask.Register((SendNotificationEmailTaskEntity task, ScheduledTaskContext ctx) =>
            {
                var limit = DateTime.Now.AddMinutes(-task.SendNotificationsOlderThan);

                var query = Database.Query<AlertEntity>()
                .Where(a => a.State == AlertState.Saved && a.EmailNotificationsSent == false && a.Recipient != null && a.CreationDate < limit)
                .Where(a => task.SendBehavior == SendAlertTypeBehavior.All ||
                            task.SendBehavior == SendAlertTypeBehavior.Include && task.AlertTypes.Contains(a.AlertType!) ||
                            task.SendBehavior == SendAlertTypeBehavior.Exclude && !task.AlertTypes.Contains(a.AlertType!));

                if (!query.Any())
                    return null;

                var alerts = query
                .Select(a => new { Alert = a, Recipient = a.Recipient!.Entity })
                .ToList();

                EmailPackageEntity emailPackage = new EmailPackageEntity().Save();

                var emails = alerts.GroupBy(a => a.Recipient, a => a.Alert).SelectMany(gr => new AlertNotificationMail((UserEntity)gr.Key, gr.ToList()).CreateEmailMessage()).ToList();

                emails.ForEach(a =>
                {
                    a.State = EmailMessageState.ReadyToSend;
                    a.Package = emailPackage.ToLite();
                });

                emails.BulkInsertQueryIds(a => a.Target!);

                query.UnsafeUpdate().Set(a => a.EmailNotificationsSent, a => true).Execute();

                return emailPackage.ToLite();
            });
        }

        public class AlertNotificationMail : EmailModel<UserEntity>
        {
            public List<AlertEntity> Alerts { get; set; }

            public AlertNotificationMail(UserEntity recipient, List<AlertEntity> alerts) : base(recipient)
            {
                this.Alerts = alerts;
            }

            static Regex LinkPlaceholder = new Regex(@"\[(?<prop>(\w|\d|\.)+)(\:(?<text>.+))?\](\((?<url>.+)\))?");

            public static HtmlString? TextFormatted(TemplateParameters tp)
            {
                if (!tp.RuntimeVariables.TryGetValue("$a", out object? alertObject))
                    return null;

                var alert = (AlertEntity)alertObject;
                var text = alert.Text ?? "";

                var newText = LinkPlaceholder.Replace(text, m =>
                {
                    var propEx = m.Groups["prop"].Value;

                    var prop = GetPropertyValue(alert, propEx);

                    var lite = prop is Entity e ? e.ToLite() :
                                prop is Lite<Entity> l ? l : null;

                    var url = ReplacePlaceHolders(m.Groups["url"].Value.DefaultToNull(), alert)?.Replace("~", EmailLogic.Configuration.UrlLeft) ?? (lite != null ? EntityUrl(lite) : "#");

                    var text = ReplacePlaceHolders(m.Groups["text"].Value.DefaultToNull(), alert) ?? (lite?.ToString());

                    return @$"<a href=""{url}"">{text}</a>";

                });

                if (text != newText)
                    return new HtmlString(newText);

                if (alert.Target != null)
                    return new HtmlString(@$"{text}<br/><a href=""{EntityUrl(alert.Target)}"">{alert.Target}</a>");

                return new HtmlString(text);
            }

       

            private static string EntityUrl(Lite<Entity> lite)
            {
                return $"{EmailLogic.Configuration.UrlLeft}/view/{TypeLogic.GetCleanName(lite.EntityType)}/{lite.Id}";
            }

            static Regex TextPlaceHolder = new Regex(@"({(?<prop>(\w|\d|\.)+)})");

            private static string? ReplacePlaceHolders(string? value, AlertEntity alert)
            {
                if (value == null)
                    return null;

                return TextPlaceHolder.Replace(value, g =>
                {
                    return GetPropertyValue(alert, g.Groups["prop"].Value)?.ToString()!;
                });
            }

            private static object? GetPropertyValue(AlertEntity alert, string expresion)
            {
                var parts = expresion.SplitNoEmpty('.');

                var result = SimpleMemberEvaluator.EvaluateExpression(alert, parts);

                if (result is Result<object?>.Error e)
                    throw new InvalidOperationException(e.ErrorText);

                if (result is Result<object?>.Success s)
                    return s.Value;

                throw new UnexpectedValueException(result);
            }

            public override List<EmailOwnerRecipientData> GetRecipients()
            {
                return new List<EmailOwnerRecipientData>
                {
                    new EmailOwnerRecipientData(this.Entity.EmailOwnerData)
                    {
                        Kind = EmailRecipientKind.To,
                    }
                };
            }
        }

        public static void RegisterAlertType(AlertTypeSymbol alertType, Enum localizableTextMessage) => RegisterAlertType(alertType, new AlertTypeOptions { GetText = () => localizableTextMessage.NiceToString() });
        public static void RegisterAlertType(AlertTypeSymbol alertType, AlertTypeOptions? options = null)
        {
            if (!alertType.Key.HasText())
                throw new InvalidOperationException("alertType must have a key, use MakeSymbol method after the constructor when declaring it");

            SystemAlertTypes.Add(alertType, options ?? new AlertTypeOptions());
        }

        public static AlertEntity? CreateAlert(this IEntity entity, AlertTypeSymbol alertType, string? text = null, string?[]? textArguments = null, DateTime? alertDate = null, Lite<IUserEntity>? createdBy = null, string? title = null, Lite<IUserEntity>? recipient = null)
        {
            return CreateAlert(entity.ToLiteFat(), alertType, text, textArguments, alertDate, createdBy, title, recipient);
        }

        public static AlertEntity? CreateAlert(this Lite<IEntity> entity, AlertTypeSymbol alertType, string? text = null, string?[]? textArguments = null, DateTime? alertDate = null, Lite<IUserEntity>? createdBy = null, string? title = null, Lite<IUserEntity>? recipient = null)
        {
            if (Started == false)
                return null;

            using (AuthLogic.Disable())
            {
                var result = new AlertEntity
                {
                    AlertDate = alertDate ?? TimeZoneManager.Now,
                    CreatedBy = createdBy ?? UserHolder.Current?.ToLite(),
                    TitleField = title,
                    TextArguments = textArguments?.ToString("\n###\n"),
                    TextField = text,
                    Target = (Lite<Entity>)entity,
                    AlertType = alertType,
                    Recipient = recipient
                };

                return result.Execute(AlertOperation.Save);
            }
        }

        public static AlertEntity? CreateAlertForceNew(this IEntity entity, AlertTypeSymbol alertType, string? text = null, string?[]? textArguments = null, DateTime? alertDate = null, Lite<IUserEntity>? createdBy = null, string? title = null, Lite<IUserEntity>? recipient = null)
        {
            return CreateAlertForceNew(entity.ToLite(), alertType, text, textArguments, alertDate, createdBy, title, recipient);
        }

        public static AlertEntity? CreateAlertForceNew(this Lite<IEntity> entity, AlertTypeSymbol alertType, string? text = null, string?[]? textArguments = null, DateTime? alertDate = null, Lite<IUserEntity>? createdBy = null, string? title = null, Lite<IUserEntity>? recipient = null)
        {
            if (Started == false)
                return null;

            using (Transaction tr = Transaction.ForceNew())
            {
                var alert = entity.CreateAlert(alertType, text, textArguments, alertDate, createdBy, title, recipient);

                return tr.Commit(alert);
            }
        }

        public static void RegisterCreatorTypeCondition(SchemaBuilder sb, TypeConditionSymbol typeCondition)
        {
            sb.Schema.Settings.AssertImplementedBy((AlertEntity a) => a.CreatedBy, typeof(UserEntity));

            TypeConditionLogic.RegisterCompile<AlertEntity>(typeCondition,
                a => a.CreatedBy.Is(UserEntity.Current));
        }

        public static void RegisterRecipientTypeCondition(SchemaBuilder sb, TypeConditionSymbol typeCondition)
        {
            sb.Schema.Settings.AssertImplementedBy((AlertEntity a) => a.Recipient, typeof(UserEntity));

            TypeConditionLogic.RegisterCompile<AlertEntity>(typeCondition,
                a => a.Recipient.Is(UserEntity.Current));
        }

        public static void AttendAllAlerts(Lite<Entity> target, AlertTypeSymbol alertType)
        {
            using (AuthLogic.Disable())
            {
                Database.Query<AlertEntity>()
                    .Where(a => a.Target.Is(target) && a.AlertType == alertType && a.State == AlertState.Saved)
                    .ToList()
                    .ForEach(a => a.Execute(AlertOperation.Attend));
            }
        }

        public static void DeleteAllAlerts(Lite<Entity> target)
        {
            using (AuthLogic.Disable())
            {
                Database.Query<AlertEntity>()
                    .Where(a => a.Target.Is(target))
                    .UnsafeDelete();
            }
        }


        public static void DeleteUnattendedAlerts(this Entity target, AlertTypeSymbol alertType, Lite<UserEntity> recipient) =>
            target.ToLite().DeleteUnattendedAlerts(alertType, recipient);
        public static void DeleteUnattendedAlerts(this Lite<Entity> target, AlertTypeSymbol alertType, Lite<UserEntity> recipient)
        {
            using (AuthLogic.Disable())
            {
                Database.Query<AlertEntity>()
                    .Where(a => a.State == AlertState.Saved && a.Target.Is(target) && a.AlertType == alertType && a.Recipient == recipient)
                    .UnsafeDelete();
            }
        }

    }

    public class AlertTypeOptions
    {
        public Func<string>? GetText; 
    }

    public class AlertGraph : Graph<AlertEntity, AlertState>
    {
        public static void Register()
        {
            GetState = a => a.State;

            new ConstructFrom<Entity>(AlertOperation.CreateAlertFromEntity)
            {
                ToStates = { AlertState.New },
                Construct = (a, _) => new AlertEntity
                {
                    AlertDate = TimeZoneManager.Now,
                    CreatedBy = UserHolder.Current.ToLite(),
                    Recipient = AlertLogic.DefaultRecipient()?.ToLite(),
                    TitleField = null,
                    TextField = null,
                    Target = a.ToLite(),
                    AlertType = null
                }
            }.Register();

            new Construct(AlertOperation.Create)
            {
                ToStates = { AlertState.New },
                Construct = (_) => new AlertEntity
                {
                    AlertDate = TimeZoneManager.Now,
                    CreatedBy = UserHolder.Current.ToLite(),
                    Recipient = AlertLogic.DefaultRecipient()?.ToLite(),
                    TitleField = null,
                    TextField = null,
                    Target = null!,
                    AlertType = null
                }
            }.Register();

            new Execute(AlertOperation.Save)
            {
                FromStates = { AlertState.Saved, AlertState.New },
                ToStates = { AlertState.Saved },
                CanBeNew = true,
                CanBeModified = true,
                Execute = (a, _) => { a.State = AlertState.Saved; }
            }.Register();

            new Execute(AlertOperation.Attend)
            {
                FromStates = { AlertState.Saved },
                ToStates = { AlertState.Attended },
                Execute = (a, _) =>
                {
                    a.State = AlertState.Attended;
                    a.AttendedDate = TimeZoneManager.Now;
                    a.AttendedBy = UserEntity.Current.ToLite();
                }
            }.Register();

            new Execute(AlertOperation.Unattend)
            {
                FromStates = { AlertState.Attended },
                ToStates = { AlertState.Saved },
                Execute = (a, _) =>
                {
                    a.State = AlertState.Saved;
                    a.AttendedDate = null;
                    a.AttendedBy = null;
                }
            }.Register();

            new Execute(AlertOperation.Delay)
            {
                FromStates = { AlertState.Saved },
                ToStates = { AlertState.Saved },
                Execute = (a, args) =>
                {
                    a.AlertDate = args.GetArg<DateTime>();
                }
            }.Register();
        }
    }
}
