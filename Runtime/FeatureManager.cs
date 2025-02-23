using UnityEngine;
using TwitchLib.Unity;
using TwitchLib.EventSub.Websockets;
using Twitchmata.Adapters;
using TwitchLib.Api.Helix.Models.Chat.ChatSettings;

namespace Twitchmata {

    public class FeatureManager : MonoBehaviour {
        public ConnectionManager Connection;
        public TwitchManager Manager;

        #region Initialization
        /// <summary>
        /// Override this in a subclass to perform any initialisation
        /// </summary>
        /// <remarks>
        /// This is where you would setup things such as chat commands and channel point rewards
        /// </remarks>
        public virtual void InitializeFeatureManager() {}
        #endregion


        #region Convenience Properties & Methods
        /// <summary>
        /// The current channel ID
        /// </summary>
        public string ChannelID {
            get { return this.Connection.ChannelID; }
        }

        /// <summary>
        /// The current twitch API
        /// </summary>
        public TwitchLib.Api.Helix.Helix HelixAPI {
            get { return this.Connection.API.Helix; }
        }
        public HelixEventSub HelixEventSub
        {
            get { return this.Connection.HelixEventSub; }
        }

        internal UserManager UserManager {
            get { return this.Connection.UserManager; }
        }

        public void SendChatMessage(string message) {
            this.Connection.Client.SendMessage(this.Connection.ConnectionConfig.ChannelName, message);
        }

        public void SetEmoteOnly(bool emoteOnly)
        {
            var settingsRequest = this.Connection.API.Helix.Chat.GetChatSettingsAsync(this.ChannelID, this.ChannelID);
            TwitchManager.RunTask(settingsRequest, (settings) =>
            {
                if (settings == null || settings.Data.Length == 0)
                {
                    Debug.LogWarning("Couldn't get initial chat settings to set emote mode.");
                    return;
                }
                var update = new ChatSettings();
                update.EmoteMode = emoteOnly;
                update.FollowerMode = settings.Data[0].FollowerMode;
                update.FollowerModeDuration = settings.Data[0].FollowerModeDuration;
                update.NonModeratorChatDelay = settings.Data[0].NonModeratorChatDelay;
                update.NonModeratorChatDelayDuration = settings.Data[0].NonModeratorChatDelayDuration;
                update.UniqueChatMode = settings.Data[0].UniqueChatMode;
                update.SlowMode = settings.Data[0].SlowMode;
                update.SlowModeWaitTime = settings.Data[0].SlowModeWaitDuration;

                var result = this.Connection.API.Helix.Chat.UpdateChatSettingsAsync(this.Connection.ChannelID, this.Connection.BotID, update, this.Connection.Secrets.BotAccessToken);
                TwitchManager.RunTask(result, (response) =>
                {
                    if (response == null || response.Data.Length == 0)
                    {
                        Debug.LogWarning("Couldn't update chat settings to set emote mode");
                    }
                    else
                    {
                        Debug.Log("Updated emote only mode");
                    }
                });
            });
        }

        #endregion



        /**************************************************
         * INTERNAL CODE. NO NEED TO READ BELOW THIS LINE *
         **************************************************/

        #region Internal Initialization
        internal void InitializeWithAPIManager(ConnectionManager manager) {
            this.Connection = manager;
            this.InitializeFeatureManager();
            if (this.Connection.EventSub.SessionId != null)
            {
                this.InitializeEventSub(manager.EventSub);
            }
            this.InitializeClient(manager.Client);
        }

        internal virtual void InitializePubSub(PubSub pubSub) { }

        internal virtual void InitializeClient(Client client) { }

        internal virtual void InitializeEventSub(EventSubWebsocketClient eventSub) { }
        
        //All feature managers set up by user are guaranteed to exist when this is called
        internal virtual void PerformPostDiscoverySetup() { }

        #endregion
    }

}