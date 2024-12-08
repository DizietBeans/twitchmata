using System.Collections.Generic;
using TwitchLib.PubSub.Events;
using TwitchLib.Unity;
using Twitchmata.Models;
using System;
using TwitchLib.Api.Core.Extensions.System;
using TwitchLib.EventSub.Websockets;
using System.Threading.Tasks;
using UnityEngine.UIElements;
using TwitchLib.Api.Helix.Models.Subscriptions;

namespace Twitchmata {
    /// <summary>
    /// Used to hook into Subscriber events in your overlay
    /// </summary>
    /// <remarks>
    /// To utilise SubscriberManager create a subclass and add to a GameObject (either the
    /// GameObject holding TwitchManager or a child GameObject).
    ///
    /// Then override <code>UserSubscribed()</code> and add your sub-handling code.
    /// </remarks>
    public class SubscriberManager : FeatureManager {
        #region Notifications
        /// <summary>
        /// Fired when a user subscribes or is gifted a sub.
        /// </summary>
        /// <param name="subscriber"></param>
        public virtual void UserSubscribed(Models.User subscriber) {
            if (subscriber.Subscription.IsGift == true) {
                Logger.LogInfo($"{subscriber.DisplayName} received gift sub from {subscriber.Subscription.Gifter?.DisplayName ?? "an anonymous gifter"}");
            } else {
                Logger.LogInfo($"{subscriber.DisplayName} subscribed");
            }
        }
        #endregion

        #region Stats
        /// <summary>
        /// List of users who subscribed or received a gift sub while the overlay has been open
        /// </summary>
        public List<Models.User> SubscribersThisStream { get; private set; } = new List<Models.User>() { };

        /// <summary>
        /// List of all users who gifted a sub this stream
        /// </summary>
        public List<Models.User> GiftersThisStream { get; private set; } = new List<Models.User>() { };
        #endregion


        #region Debug

        /// <summary>
        /// Simulates a subscription event
        /// </summary>
        /// <param name="displayName">The display name of the subscriber</param>
        /// <param name="userName">The username of the subscriber</param>
        /// <param name="userID">The user ID of the subscriber</param>
        /// <param name="plan">The subscription plan</param>
        /// <param name="planName">The name of the subscription plan</param>
        /// <param name="cumulativeMonths">The total number of months subscribed</param>
        /// <param name="streakMonths">The number of months in the current subscription streak</param>
        /// <param name="isResub">True if the user is re-subscribing, false if they're subscribing for the first time</param>
        /// <param name="message">The message sent with the subscription</param>
        public void Debug_NewSubscription(
            string displayName = "TWW2",
            string userName = "tww2",
            string userID = "13405587",
            SubscriptionTier plan = SubscriptionTier.Tier1,
            string planName = "Channel Subscription",
            int cumulativeMonths = 1,
            int streakMonths = 1,
            bool isResub = false,
            string message = "I just subscribed!"
            ) 
        {
            var subscription = new Models.Subscription();
            subscription.Tier = plan;
            subscription.PlanName = planName;
            subscription.SubscribedMonthCount = cumulativeMonths; 
            subscription.StreakMonths = streakMonths;
            subscription.IsGift = false;
            var user = new Models.User(userID, userName, displayName);
            user.Subscription = subscription;
            user.IsSubscriber = true;
        }

        /// <summary>
        /// Simulates a gift subscription
        /// </summary>
        /// <param name="gifterDisplayName">The display name of the user gifting the sub</param>
        /// <param name="gifterUserName">The username of the user gifting the sub</param>
        /// <param name="gifterUserID">The user ID of the user gifting the sub</param>
        /// <param name="recipientDisplayName">The display name of the user receiving the sub</param>
        /// <param name="recipientUserName">The username of the user receiving the sub</param>
        /// <param name="recipientUserID">The user ID of the user receiving the sub</param>
        /// <param name="plan">The subscription plan</param>
        /// <param name="planName">The name of the subscription plan</param>
        /// <param name="months">The number of months gifted</param>
        /// <param name="message">The message associated with the subscription</param>
        public void Debug_NewGiftSubscription(
            string gifterDisplayName = "TWW2",
            string gifterUserName = "tww2",
            string gifterUserID = "13405587",
            string recipientDisplayName = "ForstyCup",
            string recipientUserName = "forstycup",
            string recipientUserID = "19571752",
            SubscriptionTier plan = SubscriptionTier.Tier1,
            string planName = "Channel Subscription",
            int months = 1,
            string message = "I just gifted a sub!"
            ) {
            
        }
        
        /// <summary>
        /// Simulates an anonymous gift subscription
        /// </summary>
        /// <param name="recipientDisplayName">The display name of the user receiving the sub</param>
        /// <param name="recipientUserName">The username of the user receiving the sub</param>
        /// <param name="recipientUserID">The user ID of the user receiving the sub</param>
        /// <param name="plan">The subscription plan</param>
        /// <param name="planName">The name of the subscription plan</param>
        /// <param name="months">The number of months gifted</param>
        /// <param name="message">The message associated with the subscription</param>
        public void Debug_NewAnonymousGiftSubscription(
            string recipientDisplayName = "ForstyCup",
            string recipientUserName = "forstycup",
            string recipientUserID = "19571752",
            SubscriptionTier plan = SubscriptionTier.Tier1,
            string planName = "Channel Subscription",
            int months = 1,
            string message = "I just gifted a sub!"
            ) {
            
        }

        #endregion

        /**************************************************
         * INTERNAL CODE. NO NEED TO READ BELOW THIS LINE *
         **************************************************/

        #region Internal

        internal override void InitializeEventSub(EventSubWebsocketClient eventSub)
        {
            Logger.LogInfo("Setting up Subscriber Manager");
            eventSub.ChannelSubscriptionMessage -= EventSub_ChannelSubscriptionMessage;
            eventSub.ChannelSubscriptionMessage += EventSub_ChannelSubscriptionMessage;
            eventSub.ChannelSubscribe -= EventSub_ChannelSubscribe;
            eventSub.ChannelSubscribe += EventSub_ChannelSubscribe;

            Logger.LogInfo("Creating EventSub subscriptions for SubscriberManager");
            var createSub = this.HelixEventSub.CreateEventSubSubscriptionAsync(
                "channel.subscription.message",
                "1",
                new Dictionary<string, string> {
                    { "broadcaster_user_id", this.Manager.ConnectionManager.ChannelID },
                },
                this.Connection.EventSub.SessionId,
                this.Connection.ConnectionConfig.ClientID,
                this.Manager.ConnectionManager.Secrets.AccountAccessToken
            );
            TwitchManager.RunTask(createSub, (response) =>
            {
                Logger.LogInfo("channel.subscription.message subscription created.");
            }, (ex) =>
            {
                Logger.LogError(ex.ToString());
            });

            var createSub2 = this.HelixEventSub.CreateEventSubSubscriptionAsync(
                "channel.subscribe",
                "1",
                new Dictionary<string, string> {
                    { "broadcaster_user_id", this.Manager.ConnectionManager.ChannelID },
                },
                this.Connection.EventSub.SessionId,
                this.Connection.ConnectionConfig.ClientID,
                this.Manager.ConnectionManager.Secrets.AccountAccessToken
            );
            TwitchManager.RunTask(createSub2, (response) =>
            {
                Logger.LogInfo("channel.subscribe subscription created.");
            }, (ex) =>
            {
                Logger.LogError(ex.ToString());
            });
        }

        private System.Threading.Tasks.Task EventSub_ChannelSubscriptionMessage(object sender, TwitchLib.EventSub.Websockets.Core.EventArgs.Channel.ChannelSubscriptionMessageArgs args)
        {
            var ev = args.Notification.Payload.Event;
            var user = this.UserManager.UserForEventSubSubscriptionMessageNotification(ev);
            this.SubscribersThisStream.Add(user);
            this.UserSubscribed(user);
            return Task.CompletedTask;
        }

        private System.Threading.Tasks.Task EventSub_ChannelSubscribe(object sender, TwitchLib.EventSub.Websockets.Core.EventArgs.Channel.ChannelSubscribeArgs args)
        {
            var ev = args.Notification.Payload.Event;
            if (ev.IsGift)
            {
                var subsTask = this.HelixAPI.Subscriptions.CheckUserSubscriptionAsync(ev.BroadcasterUserId, ev.UserId);
                var subs = subsTask.Result.Data;
                var sub = subs[0];
                var user = this.UserManager.UserForEventSubSubscriptionGiftNotification(ev, sub);
                this.SubscribersThisStream.Add(user);
                var gifter = user.Subscription.Gifter;
                if (this.GiftersThisStream.Contains(gifter) == false)
                {
                    this.GiftersThisStream.Add(gifter);
                }
                this.UserSubscribed(user);
            }
            return Task.CompletedTask;
        }

        #endregion
    }
}
