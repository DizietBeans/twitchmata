using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using TwitchLib.EventSub.Core;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.Models;
using Twitchmata.Adapters.Args;
using Twitchmata.Adapters.Models;

namespace Twitchmata.Adapters
{
    public class ExtendedEventSubWebsocketClient
    {
        private readonly EventSubWebsocketClient _client;

        public event AsyncEventHandler<PowerUpRedemptionArgs> PowerUpRedemption;

        public string SessionId => _client.SessionId;

        public EventSubWebsocketClient RawClient => _client;

        public ExtendedEventSubWebsocketClient(EventSubWebsocketClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));

            RegisterCustomHandlers();
        }

        private void RegisterCustomHandlers()
        {
            Logger.LogInfo("Registering custom handlers");
            FieldInfo field = typeof(EventSubWebsocketClient)
                .GetField("_handlers", BindingFlags.NonPublic | BindingFlags.Instance);

            var handlers = (Dictionary<string, Action<EventSubWebsocketClient, string>>)field.GetValue(_client);

            if (handlers == null)
            {
                handlers = new Dictionary<string, Action<EventSubWebsocketClient, string>>();
                field.SetValue(_client, handlers);
            }

            handlers["channel.custom_power_up_redemption.add"] = HandlePowerUpRedemption;
        }

        private void HandlePowerUpRedemption(EventSubWebsocketClient client, string jsonString)
        {
            EventSubNotification<PowerUpRedemption> eventSubNotification = JsonConvert.DeserializeObject<EventSubNotification<PowerUpRedemption>>(jsonString);
            if (eventSubNotification == null)
            {
                throw new InvalidOperationException("Parsed JSON cannot be null!");
            }
            PowerUpRedemption?.Invoke(this, new PowerUpRedemptionArgs { Notification = eventSubNotification });
        }
    }
}