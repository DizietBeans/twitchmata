using System;
using System.Collections.Generic;
using TwitchLib.Unity;
using TwitchLib.Client.Events;
using TwitchLib.PubSub.Events;
using UnityEngine;
using TwitchLib.EventSub.Websockets;
using System.Threading.Tasks;
using Twitchmata.Models;
using TwitchLib.Api.Helix.Models.Raids;

namespace Twitchmata {
    /// <summary>
    /// Used to manage raids in your overlay
    /// </summary>
    /// <remarks>
    /// To utilise RaidManager create a subclass and add to a GameObject (either the
    /// GameObject holding TwitchManager or a child GameObject).
    ///
    /// Then override <code>RaidReceived()</code> and add your incoming-raid handling code.
    /// </remarks>
    public class RaidManager : FeatureManager {

        protected OutgoingRaidUpdate? CurrentRaid { get; set; } = null;


        #region Notifications
        /// <summary>
        /// Fired when another streamer raids your channel
        /// </summary>
        /// <param name="raid">Details of the incoming raid</param>
        public virtual void RaidReceived(Models.IncomingRaid raid) {
            Logger.LogInfo($"{raid.Raider.DisplayName} raided with {raid.ViewerCount} viewers");
        }

        /// <summary>
        /// Fired when an outgoing raid is started or updated
        /// </summary>
        /// <param name="raid">Details of the outgoing raid</param>
        public virtual void RaidUpdated(Models.OutgoingRaidUpdate raid) {
            Logger.LogInfo($"Preparing to raid {raid.RaidTarget.DisplayName} with {raid.ViewerCount} viewers ({raid.TargetProfileImage})");
        }

        /// <summary>
        /// Fired when an outgoing raid completes
        /// </summary>
        /// <param name="raid">Details of the outgoing raid</param>
        public virtual void RaidGo(Models.OutgoingRaidUpdate raid) {
            Logger.LogInfo($"Raiding {raid.RaidTarget.DisplayName} with {raid.ViewerCount} viewers");
        }

        /// <summary>
        /// Fired when an outgoing raid is cancelled
        /// </summary>
        /// <param name="raid">Details of the cancelled raid</param>
        public virtual void RaidCancelled(Models.OutgoingRaidUpdate raid) {
            Logger.LogInfo($"Cancelled raid of {raid.RaidTarget.DisplayName} ({raid.TargetProfileImage})");
        }
        
        #endregion

        #region Outgoing Raids
        /// <summary>
        /// Starts a raid to another streamer
        /// </summary>
        /// <remarks>
        /// You will likely need to fetch the user to raid from the UserManager first
        /// </remarks>
        /// <param name="userToRaid">The user to raid</param>
        /// <param name="action">An action called with details of the outgoing raid if it was successfully started</param>
        public void StartRaid(Models.User userToRaid, Action<Models.OutgoingRaid> action) {
            var task = this.HelixAPI.Raids.StartRaidAsync(this.ChannelID, userToRaid.UserId);
            TwitchManager.RunTask(task, obj => {
                var raid = obj.Data[0];
                var outgoingRaid = new Models.OutgoingRaid() {
                    RaidTarget = userToRaid,
                    CreatedAt = raid.CreatedAt,
                    IsMature = raid.IsMature,
                };
                action.Invoke(outgoingRaid);
            });
        }

        /// <summary>
        /// Cancel any currently pending raid
        /// </summary>
        /// <param name="action">An action called after the raid has been cancelled</param>
        public void CancelRaid(Action action = null) {
            var task = this.HelixAPI.Raids.CancelRaidAsync(this.ChannelID);
            TwitchManager.RunTask(task, () => {
                if (action != null) {
                    action.Invoke();
                }
            });
        }

        #endregion

        #region Stats
        /// <summary>
        /// List of raids that have come in while the overlay has been open
        /// </summary>
        public List<Models.IncomingRaid> RaidsThisStream { get; private set; } = new List<Models.IncomingRaid>() { };
        #endregion


        #region Debug
        /// <summary>
        /// Simulates an incoming raid
        /// </summary>
        /// <param name="viewerCount">The number of viewers in the raid</param>
        /// <param name="displayName">The display name of the raiding channel</param>
        /// <param name="username">The username of the raiding channel</param>
        /// <param name="userID">The user ID of the raiding channel</param>
        public void Debug_IncomingRaid(int viewerCount = 20, string displayName = "TestChannel", string username = "testchannel", string userID = "123456") {
            Logger.LogInfo("Debug incoming raid called");
            var channelName = "#" + this.Connection.ConnectionConfig.ChannelName;
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var chatMessage = $"@badge-info=;badges=;color=#888888;display-name={displayName};emotes=;id={Guid.NewGuid().ToString()};login={username};mod=0;msg-id=raid;msg-param-displayName={displayName};msg-param-login={username};msg-param-viewerCount={viewerCount};room-id=33332222;subscriber=0;system-msg={viewerCount}\\sraiders\\sfrom\\s{displayName}\\shave\\sjoined\\n!;tmi-sent-ts={timestamp};turbo=0;user-id={userID};user-type= :tmi.twitch.tv USERNOTICE {channelName}";
            this.Connection.Client.OnReadLineTest(chatMessage);
        }
        #endregion

        /**************************************************
         * INTERNAL CODE. NO NEED TO READ BELOW THIS LINE *
         **************************************************/

        #region Internal (Client)
        override internal void InitializeClient(Client client) {
            Logger.LogInfo("Setting Up Incoming Raid Notifications");
            client.OnRaidNotification -= Client_OnRaidNotification;
            client.OnRaidNotification += Client_OnRaidNotification;
        }

        private void Client_OnRaidNotification(object sender, OnRaidNotificationArgs args) {
            var user = this.UserManager.UserForRaidNotification(args.RaidNotification);
            var raid = new Models.IncomingRaid() {
                Raider = user,
                ViewerCount = Int32.Parse(args.RaidNotification.MsgParamViewerCount),
            };
            this.RaidsThisStream.Add(raid);
            this.RaidReceived(raid);
        }

        #endregion

        internal override void InitializeEventSub(EventSubWebsocketClient eventSub)
        {
            //eventSub.ChannelChatNotification += EventSub_ChannelChatNotification;
            eventSub.ChannelModerate += EventSub_ChannelModerate;

            if (this.Connection.UseDebugServer)
            {
                return;
            }

            if (!this.Connection.ChannelModerateSubscribed)
            {
                var createSub = this.HelixEventSub.CreateEventSubSubscriptionAsync(
                    "channel.moderate",
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
                    Logger.LogInfo("channel.moderate subscription created for RaidManager.");
                }, (ex) =>
                {
                    Logger.LogError(ex.ToString());
                });
                this.Connection.ChannelModerateSubscribed = true;
                /*var createSub = this.HelixEventSub.CreateEventSubSubscriptionAsync(
                    "channel.chat.notification",
                    "1",
                    new Dictionary<string, string> {
                    { "broadcaster_user_id", this.Manager.ConnectionManager.ChannelID },
                    { "user_id", this.Manager.ConnectionManager.ChannelID },
                    },
                    this.Connection.EventSub.SessionId,
                    this.Connection.ConnectionConfig.ClientID,
                    this.Manager.ConnectionManager.Secrets.AccountAccessToken
                );
                TwitchManager.RunTask(createSub, (response) =>
                {
                    Logger.LogInfo(this.Connection.EventSub.SessionId);
                    Logger.LogInfo("channel.chat.notification subscription created.");
                }, (ex) =>
                {
                    Logger.LogError(ex.ToString());
                });*/
                this.Connection.ChannelModerateSubscribed = true;
            }
        }

        private Task EventSub_ChannelChatNotification(object sender, TwitchLib.EventSub.Websockets.Core.EventArgs.Channel.ChannelChatNotificationArgs args)
        {
            Logger.LogInfo("Received notification");
            if (args.Notification.Payload.Event.NoticeType == "raid")
            {
                var infoFromArgs = args.Notification.Payload.Event.Raid;
                this.UserManager.FetchUserWithID(infoFromArgs.UserId, (user) =>
                {
                    this.CurrentRaid = new Models.OutgoingRaidUpdate()
                    {
                        RaidTarget = user,
                        TargetProfileImage = user.ProfileImage,
                        ViewerCount = infoFromArgs.ViewerCount,
                    };
                    this.RaidUpdated(this.CurrentRaid.Value);
                });
            }
            else if (args.Notification.Payload.Event.NoticeType == "unraid")
            {
                if (this.CurrentRaid.HasValue)
                {
                    this.RaidCancelled(this.CurrentRaid.Value);
                    this.CurrentRaid = null;
                }
            }
            return Task.CompletedTask;
        }

        private Task EventSub_ChannelModerate(object sender, TwitchLib.EventSub.Websockets.Core.EventArgs.Channel.ChannelModerateArgs args)
        {
            ThreadDispatcher.Enqueue(() =>
            {
                try
                {
                    if (args.Notification.Payload.Event.Action == "raid")
                    {
                        Logger.LogInfo("Receiving outgoing raid notification.");
                        var infoFromArgs = args.Notification.Payload.Event.Raid;
                        this.UserManager.FetchUserWithID(infoFromArgs.UserId, (user) =>
                        {
                            try
                            {
                                var raid = new Models.OutgoingRaidUpdate()
                                {
                                    RaidTarget = user,
                                    TargetProfileImage = user.ProfileImage,
                                    ViewerCount = infoFromArgs.ViewerCount,
                                };
                                ThreadDispatcher.Enqueue(() =>
                                {
                                    try
                                    {
                                        this.RaidUpdated(raid);
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.LogError("Error in Userspace: " + ex.Message + "\n" + ex.StackTrace);
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError("Error in Twitchmata: " + ex.Message + "\n" + ex.StackTrace);
                            }
                        }, ex =>
                        {
                            Logger.LogError("Error in Twitchmata: " + ex.Message + "\n" + ex.StackTrace);
                        });
                    }
                    else if (args.Notification.Payload.Event.Action == "unraid")
                    {
                        var infoFromArgs = args.Notification.Payload.Event.Unraid;
                        this.UserManager.FetchUserWithID(infoFromArgs.UserId, (user) =>
                        {
                            try { 
                                var raid = new Models.OutgoingRaidUpdate()
                                {
                                    RaidTarget = user,
                                    TargetProfileImage = user.ProfileImage,
                                    ViewerCount = 0,
                                };
                                ThreadDispatcher.Enqueue(() =>
                                {
                                    try
                                    {
                                        this.RaidCancelled(raid);
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.LogError("Error in Userspace: " + ex.Message + "\n" + ex.StackTrace);
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError("Error in Twitchmata: " + ex.Message + "\n" + ex.StackTrace);
                            }
                        }, ex =>
                        {
                            Logger.LogError("Error in Twitchmata: " + ex.Message + "\n" + ex.StackTrace);
                        });
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in Twitchmata: " + ex.Message + "\n" + ex.StackTrace);
                }
            });
            return Task.CompletedTask;
        }

        private void PubSub_OnRaidCancel(object sender, OnRaidCancelArgs args) {
            var user = this.UserManager.UserForRaidCancelNotification(args);
            var raid = new Models.OutgoingRaidUpdate() {
                RaidTarget = user,
                TargetProfileImage = args.TargetProfileImage,
                ViewerCount = args.ViewerCount
            };
            this.RaidCancelled(raid);
        }

        private void PubSub_OnRaidGo(object sender, OnRaidGoArgs args) {
            var user = this.UserManager.UserForRaidGoNotification(args);
            var raid = new Models.OutgoingRaidUpdate() {
                RaidTarget = user,
                TargetProfileImage = args.TargetProfileImage,
                ViewerCount = args.ViewerCount
            };
            this.RaidGo(raid);
        }

        private void PubSub_OnRaidUpdate(object sender, OnRaidUpdateV2Args args) {
            var user = this.UserManager.UserForRaidUpdateNotification(args);
            var raid = new Models.OutgoingRaidUpdate() {
                RaidTarget = user,
                TargetProfileImage = args.TargetProfileImage,
                ViewerCount = args.ViewerCount
            };
            this.RaidUpdated(raid);
        }
    }
}
