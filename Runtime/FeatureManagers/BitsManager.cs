using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TwitchLib.EventSub.Websockets;
using UnityEngine;

namespace Twitchmata {
    /// <summary>
    /// Used to hook into bits events in your overlay
    /// </summary>
    /// <remarks>
    /// To utilise BitsManager create a subclass and add to a GameObject (either the
    /// GameObject holding TwitchManager or a child GameObject).
    ///
    /// Then override <code>ReceivedBits()</code> and add your bit-handling code.
    /// </remarks>
    public class BitsManager : FeatureManager {
        #region Notifications
        /// <summary>
        /// Fired when a user sends the broadcaster bits. 
        /// </summary>
        /// <param name="bitsInfo">Info on the bits received</param>
        public virtual void ReceivedBits(Models.BitsRedemption bitsRedemption) {
            Logger.LogInfo($"Received {bitsRedemption.BitsUsed} Bits from {bitsRedemption.User?.DisplayName}");
        }
        #endregion


        #region Stats
        /// <summary>
        /// List of bit redemptions that have occurred while the overlay has been open
        /// </summary>
        public List<Models.BitsRedemption> RedemptionsThisStream { get; private set; } = new List<Models.BitsRedemption>() {};
        #endregion


        #region Debug
        /// <summary>
        /// Trigger a bits event from a named user
        /// </summary>
        /// <param name="bitsUsed">The number of bits to send</param>
        /// <param name="userName">The name of the user</param>
        /// <param name="userID">The id of the user</param>
        /// <param name="chatMessage">The message the user sent with the bits</param>
        public void Debug_SendBits(int bitsUsed = 100, string userName = "jwp", string userID = "95546976", string chatMessage = "Have some test bits") {

            Debug.Log("Debug_SendBits called");
            //There isn't a sending system in EventSub. Just trigger directly.
            Models.User user = new Models.User(userID, userName, userName);
            
            var redemption = new Models.BitsRedemption()
            {
                BitsUsed = bitsUsed,
                TotalBitsUsed = bitsUsed,
                User = user,
                RedeemedAt = DateTime.Now,
                Message = chatMessage,
            };

            this.RedemptionsThisStream.Add(redemption);
            this.ReceivedBits(redemption);
        }

        /// <summary>
        /// Trigger a bits event from an anonymous user
        /// </summary>
        /// <param name="bitsUsed">The number of bits to send</param>
        /// <param name="chatMessage">The message the user sent with the bits</param>
        public void Debug_SendAnonymousBits(int bitsUsed = 100, string chatMessage = "Have some test bits") {
            var redemption = new Models.BitsRedemption()
            {
                BitsUsed = bitsUsed,
                TotalBitsUsed = bitsUsed,
                User = null,
                RedeemedAt = DateTime.Now,
                Message = chatMessage,
            };

            this.RedemptionsThisStream.Add(redemption);
            this.ReceivedBits(redemption);
        }

        #endregion


        /**************************************************
         * INTERNAL CODE. NO NEED TO READ BELOW THIS LINE *
         **************************************************/

        #region Internal

        internal override void InitializeEventSub(EventSubWebsocketClient eventSub)
        {
            eventSub.ChannelCheer -= EventSub_ChannelCheer;
            eventSub.ChannelCheer += EventSub_ChannelCheer;

            var createSub = this.HelixEventSub.CreateEventSubSubscriptionAsync(
                "channel.cheer",
                "1",
                new Dictionary<string, string> {
                    { "broadcaster_user_id", this.Manager.ConnectionManager.ChannelID },
                },
                eventSub.SessionId,
                this.Connection.ConnectionConfig.ClientID,
                this.Manager.ConnectionManager.Secrets.AccountAccessToken
            );
            TwitchManager.RunTask(createSub, (response) =>
            {
                Logger.LogInfo("channel.cheer subscription created.");
            }, (ex) =>
            {
                Logger.LogError(ex.ToString());
            });
        }

        private System.Threading.Tasks.Task EventSub_ChannelCheer(object sender, TwitchLib.EventSub.Websockets.Core.EventArgs.Channel.ChannelCheerArgs args)
        {
            var ev = args.Notification.Payload.Event;
            Models.User user = null;
            if ((ev.IsAnonymous == false) && (ev.UserId != null))
            {
                user = this.UserManager.UserForEventSubBitsRedeem(ev);
            }

            var redemption = new Models.BitsRedemption()
            {
                BitsUsed = ev.Bits,
                TotalBitsUsed = ev.Bits, //Not available in EventSub? Annoying.
                User = user,
                RedeemedAt = args.Notification.Metadata.MessageTimestamp,
                Message = ev.Message,
            };

            this.RedemptionsThisStream.Add(redemption);
            this.ReceivedBits(redemption);
            return Task.CompletedTask;
        }

        #endregion

    }
}