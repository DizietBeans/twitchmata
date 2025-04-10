using System.Collections.Generic;
using UnityEngine;
using TwitchLib.Unity;
using TwitchLib.Client.Models;
using TwitchLib.Client.Events;
using System;
using TwitchLib.EventSub.Websockets;
using System.Threading.Tasks;
using TwitchLib.Api.Core.RateLimiter;
using TwitchLib.Api.Core.HttpCallHandlers;
using Twitchmata.Adapters;

namespace Twitchmata {
    public class ConnectionManager {
        public PubSub PubSub { get; private set; }
        public Api API { get; private set; }
        public Client Client { get; private set; }
        public HelixEventSub HelixEventSub { get; private set; }

        public EventSubWebsocketClient EventSub { get; private set; }

        public ConnectionConfig ConnectionConfig { get; private set; }

        private bool manualDisconnectFlag = false;

        public bool ChannelModerateSubscribed { get; set; } = false;

        public string ChannelID {
            get { return this.UserManager.BroadcasterID; }
        }

        public string BotID {
            get { return this.UserManager.BotID; }
        }

        /// <summary>
        /// Connect to EventSub and Chat Bot
        /// </summary>
        public void Connect() {
            if (this.ChannelID == null) {
                Logger.LogError("Channel ID not set, did you forget to call PerformSetup()?");
                return;
            }
            this.ConnectClient();
            TwitchManager.RunTask(this.EventSub.ConnectAsync(), (response) =>
            {
                Logger.LogInfo("EventSub websocket connection request complete: " + response.ToString());
            }, (ex) =>
            {
                Logger.LogError("EventSub websocket connection error: " + ex.ToString());
            });
        }

        /// <summary>
        /// Disconnect from EventSub and Chat Bot
        /// </summary>
        public void Disconnect() {
            this.manualDisconnectFlag = true;
            this.Client.Disconnect();
            TwitchManager.RunTask(this.EventSub.DisconnectAsync(), (response) =>
            {
                Logger.LogInfo("EventSub websocket disconnect request complete: " + response.ToString());
            }, (ex) =>
            {
                Logger.LogError("EventSub websocket disconnect error: " + ex.ToString());
            });
        }

        /// <summary>
        /// This must be called after initialising a connection manager but before calling any APIs or connecting
        /// </summary>
        /// <param name="callback">Action that is called when it is safe to use the ConnectionManager</param>
        public void PerformSetup(Action callback) {
            this.UserManager.PerformSetup(callback);
        }



        /**************************************************
         * INTERNAL CODE. NO NEED TO READ BELOW THIS LINE *
         **************************************************/

        internal UserManager UserManager { get; private set; }
        internal Persistence Secrets { get; private set; }
        public bool UseDebugServer { get; }

        internal ConnectionManager(ConnectionConfig connectionConfig, Persistence secrets, bool useDebugServer) {
            this.ConnectionConfig = connectionConfig;
            this.Secrets = secrets;
            this.UseDebugServer = useDebugServer;
            this.SetupAPI();
            this.SetupEventSub();
            this.SetupClient();
            this.UserManager = new UserManager(this);
            
        }

        private void SetupClient() {
            this.Client = new Client();
            this.Client.OnIncorrectLogin += Client_OnIncorrectLogin;
            this.Client.OnJoinedChannel += ClientOnJoinedChannel;
        }
        private void SetupEventSub()
        {
            if (this.UseDebugServer)
            {
                this.EventSub = new EventSubWebsocketClient("ws://localhost:8080/ws");
            }
            else
            {
                this.EventSub = new EventSubWebsocketClient();
            }
            this.EventSub.WebsocketConnected += EventSub_WebsocketConnected;
            this.EventSub.WebsocketDisconnected += EventSub_WebsocketDisconnected;
            this.EventSub.WebsocketReconnected += EventSub_WebsocketReconnected;
            this.EventSub.ErrorOccurred += EventSub_ErrorOccurred;
            this.HelixEventSub = new HelixEventSub(this.API.Settings, BypassLimiter.CreateLimiterBypassInstance(), new TwitchHttpClient());

        }

        private Task EventSub_ErrorOccurred(object sender, TwitchLib.EventSub.Websockets.Core.EventArgs.ErrorOccuredArgs args)
        {
            Logger.LogError("EventSub Error Occurred: " + args.Message + " \n " + args.Exception.Message + " \n " + args.Exception.InnerException.Message + "\n" + args.Exception.InnerException.StackTrace);
            return Task.CompletedTask;
        }


        #region Connection

        private void ConnectClient() {
            ConnectionCredentials credentials = new ConnectionCredentials(this.ConnectionConfig.BotName, this.Secrets.BotAccessToken);
            this.Client.Initialize(credentials, this.ConnectionConfig.ChannelName);
            foreach (FeatureManager manager in this.FeatureManagers) {
                manager.InitializeClient(this.Client);
            }
            this.Client.Connect();
        }

        #endregion


        #region Client Management

        private void Client_OnIncorrectLogin(object sender, OnIncorrectLoginArgs args) {
            Logger.LogError("Invalid bot login, need to re-authenticate");
        }

        private void ClientOnJoinedChannel(object sender, OnJoinedChannelArgs e) {
            Logger.LogInfo($"Joined Channel {e.Channel} with User {e.BotUsername}");
            if (ConnectionConfig.PostConnectMessage)
            {
                this.Client.SendMessage(ConnectionConfig.ChannelName, ConnectionConfig.ConnectMessage);
            }
        }
        
        #endregion

        private Task EventSub_WebsocketReconnected(object sender, EventArgs args)
        {
            Logger.LogInfo("EventSub reconnected.");
            /*foreach (FeatureManager manager in this.FeatureManagers)
            {
                manager.InitializeEventSub(this.EventSub);
            }*/
            return Task.CompletedTask;
        }

        private Task EventSub_WebsocketDisconnected(object sender, EventArgs args)
        {
            if (!manualDisconnectFlag)
            {
                Logger.LogWarning("EventSub disconnected, requires reconnect");
                TwitchManager.RunTask(this.EventSub.ReconnectAsync(), (response) =>
                {
                    Logger.LogInfo("EventSub websocket reconnection request complete: " + response.ToString());
                }, (ex) =>
                {
                    Logger.LogError("EventSub websocket reconnection error: " + ex.ToString());
                });
            }
            return Task.CompletedTask;
        }

        private Task EventSub_WebsocketConnected(object sender, TwitchLib.EventSub.Websockets.Core.EventArgs.WebsocketConnectedArgs args)
        {
            this.manualDisconnectFlag = false;
            Logger.LogInfo("EventSub connected.");
            if (!args.IsRequestedReconnect)
            {
                this.ChannelModerateSubscribed = false;
                foreach (FeatureManager manager in this.FeatureManagers)
                {
                    manager.InitializeEventSub(this.EventSub);
                }
            }
            return Task.CompletedTask;
        }

        private void SetupAPI()
        {
            this.API = new Api();
            this.API.Settings.ClientId = this.ConnectionConfig.ClientID;
            this.API.Settings.AccessToken = this.Secrets.AccountAccessToken;
        }

        #region Feature Managers
        public List<FeatureManager> FeatureManagers { get; private set; } = new List<FeatureManager>();
        /// <summary>
        /// Register a feature manager with the connectino manager.
        /// </summary>
        /// <remarks>
        /// Usually this would be handled by TwitchManager but is provided in case you want to programtically register a feature manager
        /// </remarks>
        /// <param name="manager">The manager to register</param>
        public void RegisterFeatureManager(FeatureManager manager) {
            this.FeatureManagers.Add(manager);
            manager.InitializeWithAPIManager(this);
        }
        
        public void PerformPostDiscoverySetup() {
            var featureManagers = new List<FeatureManager>(this.FeatureManagers);
            foreach (var featureManager in featureManagers) {
                featureManager.PerformPostDiscoverySetup();
            }
        }

        #endregion


        internal void PubSub_SendTestMessage(string topicName, System.Object messageObject)
        {
            var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                type = "MESSAGE",
                data = new
                {
                    topic = topicName,
                    message = messageObject
                }
            });

            this.PubSub.TestMessageParser(jsonString);
        }
    }
}
