using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.Models;
using TwitchLib.EventSub.Websockets.Handler.Channel.Follows;
using TwitchLib.PubSub.Events;
using TwitchLib.PubSub.Models.Responses.Messages.Redemption;
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
            var follow = new
            {
                user_id = userID,
                user_name = displayName,
                user_login = username,
                broadcaster_user_id = "",
                broadcaster_user_name = "",
                broadcaster_user_login = "",
                followed_at = "2020-12-09T16:09:53+00:00"
            };
            var arg = new
            {
                metadata = new
                {
                    message_id = "test",
                    message_type = "",
                    message_timestamp = "",
                },
                payload = new {
                    @event = follow,
                }
            };
            var argString = Newtonsoft.Json.JsonConvert.SerializeObject(arg);
            var test = Newtonsoft.Json.JsonConvert.DeserializeObject<EventSubNotification<ChannelFollow>>(argString);
            var handler = new ChannelFollowHandler();
            handler.Handle(this.Connection.EventSub, argString);
        }
        #endregion


        /**************************************************
         * INTERNAL CODE. NO NEED TO READ BELOW THIS LINE *
         **************************************************/

        #region Internal

        internal override void InitializeEventSub(EventSubWebsocketClient eventSub)
        {
            eventSub.ChannelFollow -= EventSub_ChannelFollow;
            eventSub.ChannelFollow += EventSub_ChannelFollow;
            if (this.Connection.UseDebugServer)
            {
                return;
            }
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
            ThreadDispatcher.Enqueue(() =>
            {
                try { 
                    var user = this.UserManager.UserForEventSubFollowNotification(args.Notification.Payload.Event);
                    this.FollowsThisStream.Add(user);
                    try
                    {
                        this.UserFollowed(user);
                    }
                    catch (Exception ex2)
                    {
                        Logger.LogError("Error in Userspace: " + ex2.Message + "\n" + ex2.StackTrace);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in Twitchmata: " + ex.Message + "\n" + ex.StackTrace);
                }
            });
            return Task.CompletedTask;
        }

        #endregion
    }
}
