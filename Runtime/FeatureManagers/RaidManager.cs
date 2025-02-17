using System;
using System.Collections.Generic;
using TwitchLib.Unity;
using TwitchLib.Client.Events;
using TwitchLib.PubSub.Events;
using UnityEngine;
using TwitchLib.EventSub.Websockets;

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
            Logger.LogInfo($"Preparing to raid {raid.RaidTarget.DisplayName} with {raid.ViewerCount} viewers");
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
            Logger.LogInfo($"Cancelled raid of {raid.RaidTarget.DisplayName}");
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


        #region PubSub
        //Cancelling a raid breaks all of PubSub so this is disabled for now
        internal override void InitializePubSub(PubSub pubSub)
        {
            Debug.Log("Setting Up Outgoing Raid Notifications");
            pubSub.OnRaidUpdateV2 -= PubSub_OnRaidUpdate;
            pubSub.OnRaidUpdateV2 += PubSub_OnRaidUpdate;
            pubSub.OnRaidGo -= PubSub_OnRaidGo;
            pubSub.OnRaidGo += PubSub_OnRaidGo;
            pubSub.OnRaidCancel -= PubSub_OnRaidCancel;
            pubSub.OnRaidCancel += PubSub_OnRaidCancel;
            pubSub.ListenToRaid(this.ChannelID);
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
        #endregion
    }
}
