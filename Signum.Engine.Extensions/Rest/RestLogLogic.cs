﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Signum.Engine.Basics;
using Signum.Engine.DynamicQuery;
using Signum.Engine.Maps;
using Signum.Entities.Basics;
using Signum.Entities.Rest;
using System.Threading;
using Signum.Utilities;

namespace Signum.Engine.Rest
{
    public class RestLogLogic
    {
        public static void Start(SchemaBuilder sb, DynamicQueryManager dqm)
        {
            if (sb.NotDefined(MethodInfo.GetCurrentMethod()))
            {
                sb.Include<RestLogEntity>()
                    .WithIndex(a => a.StartDate)
                    .WithIndex(a => a.EndDate)
                    .WithIndex(a => a.Controller)
                    .WithIndex(a => a.Action)
                    .WithQuery(dqm, () => e => new
                    {
                        Entity = e,
                        e.Id,
                        e.StartDate,
                        e.Duration,
                        e.Url,
                        e.User,
                        e.Exception,
                    });

                ExceptionLogic.DeleteLogs += ExceptionLogic_DeleteRestLogs;
            }
        }

        private static void ExceptionLogic_DeleteRestLogs(DeleteLogParametersEmbedded parameters, StringBuilder sb, CancellationToken token)
        {
            var dateLimit = parameters.GetDateLimit(typeof(RestLogEntity).ToTypeEntity());

            Database.Query<RestLogEntity>().Where(a => a.StartDate < dateLimit).UnsafeDeleteChunksLog(parameters, sb, token);
        }

        public static async Task<RestDiffResult> GetRestDiffResult(HttpMethod httpMethod, string url, string apiKey, string oldRequestBody, string oldResponseBody)
        {
            var result = new RestDiffResult { previous = oldResponseBody };

            //create the new Request
            var restClient = new HttpClient { BaseAddress = new Uri(url) };
            restClient.DefaultRequestHeaders.Add("X-ApiKey", apiKey);
            var request = new HttpRequestMessage(httpMethod, url);

            if (!string.IsNullOrWhiteSpace(oldRequestBody))
            {
                var response = await restClient.PostAsync("",
                    new StringContent(oldRequestBody, Encoding.UTF8, "application/json"));
                result.current = await response.Content.ReadAsStringAsync();
            }
            else
            {
                var requestUriAbsoluteUri = request.RequestUri.AbsoluteUri;
                if (requestUriAbsoluteUri.Contains("apiKey"))
                {
                    request.RequestUri = requestUriAbsoluteUri.After("apiKey=").Contains("&")
                        ? new Uri(requestUriAbsoluteUri.Before("apiKey=") + requestUriAbsoluteUri.After("apiKey=").After('&'))
                        : new Uri(requestUriAbsoluteUri.Before("apiKey="));
                }

                result.current = await restClient.SendAsync(request).Result.Content.ReadAsStringAsync();
            }
            return RestDiffLog(result);
        }

        public static RestDiffResult RestDiffLog(RestDiffResult result)
        {
            StringDistance sd = new StringDistance();
            long? size = (long?)result.current?.Length * result.previous?.Length;
            if (size != null && size<=int.MaxValue)
            {
                var diff = sd.DiffText(result.previous, result.current);
                result.diff = diff;
            }

            
            
            return result;
        }

    }
}
