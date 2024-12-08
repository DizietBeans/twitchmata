using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Core.Exceptions;
using TwitchLib.Api.Core.Interfaces;
using TwitchLib.Api.Helix;
using TwitchLib.Api.Helix.Models.EventSub;
using Newtonsoft.Json;

namespace Twitchmata.Adapters
{
    public class HelixEventSub : EventSub
    {
        public HelixEventSub(IApiSettings settings, IRateLimiter rateLimiter, IHttpCallHandler http) : base(settings, rateLimiter, http)
        {
        }

        public Task<CreateEventSubSubscriptionResponse> CreateEventSubSubscriptionAsync(string type, string version, Dictionary<string, string> condition, string websocketSessionId = null, string clientId = null, string accessToken = null, string customBase = null)
        {
            if (string.IsNullOrEmpty(type))
            {
                throw new BadParameterException("type must be set");
            }

            if (string.IsNullOrEmpty(version))
            {
                throw new BadParameterException("version must be set");
            }

            if (condition == null || condition.Count == 0)
            {
                throw new BadParameterException("condition must be set");
            }

              
            if (string.IsNullOrWhiteSpace(websocketSessionId))
            {
                throw new BadParameterException("websocketSessionId must be set");
            }

            var value = new
            {
                type = type,
                version = version,
                condition = condition,
                transport = new
                {
                    method = "websocket",
                    session_id = websocketSessionId
                }
            };
            return TwitchPostGenericAsync<CreateEventSubSubscriptionResponse>("/eventsub/subscriptions", ApiVersion.Helix, JsonConvert.SerializeObject(value), null, accessToken, clientId, customBase);
        }
    }
}