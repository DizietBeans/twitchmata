using System.Collections.Generic;
using System.Threading.Tasks;
using TwitchLib.EventSub.Websockets;
using TwitchLib.PubSub.Events;
using TwitchLib.Unity;

namespace Twitchmata {
    /// <summary>
    /// Used to hook into follower events in your overlay
    /// </summary>
    /// <remarks>
    /// To utilise FollowManager create a subclass and add to a GameObject (either the
    /// GameObject holding TwitchManager or a child GameObject).
    ///
    /// Then override <code>UserFollowed()</code> and add your follow-handling code.
    /// </remarks>
    public class FollowManager : FeatureManager {
        #region Notifications
        /// <summary>
        /// Fired when a user follows the channel
        /// </summary>
        /// <param name="follower">A User object with details on the follower</param>
        public virtual void UserFollowed(Models.User follower) {
            Logger.LogInfo($"User followed: {follower.DisplayName}");
        }
        #endregion

        #region Stats
        /// <summary>
        /// List of users who have followed while the overlay has been open
        /// </summary>
        public List<Models.User> FollowsThisStream { get; private set; } = new List<Models.User>() { };
        #endregion


        #region Debug
        public void Debug_NewFollow(string displayName = "JWP", string username = "jwp", string userID = "95546976") {
            this.Connection.PubSub_SendTestMessage("following.000", new {
                display_name = displayName,
                username = username,
                user_id = userID
            });
        }
        #endregion


        /**************************************************
         * INTERNAL CODE. NO NEED TO READ BELOW THIS LINE *
         **************************************************/

        #region Internal

        internal override void InitializeEventSub(EventSubWebsocketClient eventSub)
        {
            Logger.LogInfo("Initializing Follow Manager with EventSub");
            eventSub.ChannelFollow -= EventSub_ChannelFollow;
            eventSub.ChannelFollow += EventSub_ChannelFollow;

            Logger.LogInfo("Creating EventSub subscriptions for FollowManager");
            var createSub = this.HelixEventSub.CreateEventSubSubscriptionAsync(
                "channel.follow",
                "2",
                new Dictionary<string, string> {
                    { "broadcaster_user_id", this.Manager.ConnectionManager.ChannelID },
                    { "moderator_user_id", this.Manager.ConnectionManager.ChannelID },
                },
                this.Connection.EventSub.SessionId,
                this.Connection.ConnectionConfig.ClientID,
                this.Manager.ConnectionManager.Secrets.AccountAccessToken
            );
            TwitchManager.RunTask(createSub, (response) =>
            {
                Logger.LogInfo("channel.follow subscription created.");
            }, (ex) =>
            {
                Logger.LogError(ex.ToString());
            });
        }

        private System.Threading.Tasks.Task EventSub_ChannelFollow(object sender, TwitchLib.EventSub.Websockets.Core.EventArgs.Channel.ChannelFollowArgs args)
        {
            var user = this.UserManager.UserForEventSubFollowNotification(args.Notification.Payload.Event);
            this.FollowsThisStream.Add(user);
            this.UserFollowed(user);
            return Task.CompletedTask;
        }

        #endregion
    }
}
