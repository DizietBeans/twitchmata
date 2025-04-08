using System.Collections.Generic;
using Twitchmata.Models;
using TwitchLib.EventSub.Websockets;
using System.Threading.Tasks;
using TwitchLib.EventSub.Websockets.Handler.Channel.Subscription;
using TwitchLib.EventSub.Core.Models.Subscriptions;
using TwitchLib.Unity;


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
            ThreadDispatcher.Enqueue(delegate {
                if (subscriber.Subscription.IsGift == true)
                {
                    Logger.LogInfo($"{subscriber.DisplayName} received gift sub from {subscriber.Subscription.Gifter?.DisplayName ?? "an anonymous gifter"}");
                }
                else
                {
                    Logger.LogInfo($"{subscriber.DisplayName} subscribed");
                }
            });
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
            var follow = new
            {
                user_id = userID,
                user_name = displayName,
                user_login = userName,
                broadcaster_user_id = "",
                broadcaster_user_name = "",
                broadcaster_user_login = "",
                tier = plan == SubscriptionTier.Tier3 ? 3000 : (plan == SubscriptionTier.Tier2 ? 2000 : 1000),
                message = new
                {
                    text = message,
                    emotes = new SubscriptionMessageEmote[0],
                },
                cumulative_months = cumulativeMonths,
                streak_months = streakMonths,
                duration_months = 1
            };
            var arg = new
            {
                metadata = new
                {
                    message_id = "test",
                    message_type = "",
                    message_timestamp = "",
                },
                payload = new
                {
                    @event = follow,
                }
            };
            var argString = Newtonsoft.Json.JsonConvert.SerializeObject(arg);
            var handler = new ChannelSubscriptionMessageHandler();
            handler.Handle(this.Connection.EventSub, argString);
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
            SubscriptionTier plan = SubscriptionTier.Tier1,
            int numberOfGifts = 1,
            int totalGiftsSentByUser = 10
        ) 
        {
            var follow = new
            {
                user_id = gifterUserID,
                user_name = gifterDisplayName,
                user_login = gifterUserName,
                broadcaster_user_id = "",
                broadcaster_user_name = "",
                broadcaster_user_login = "",
                tier = plan == SubscriptionTier.Tier3 ? 3000 : (plan == SubscriptionTier.Tier2 ? 2000 : 1000),
                total = numberOfGifts,
                cumulative_months = totalGiftsSentByUser,
                is_anonymous = false
            };
            var arg = new
            {
                metadata = new
                {
                    message_id = "test",
                    message_type = "",
                    message_timestamp = "",
                },
                payload = new
                {
                    @event = follow,
                }
            };
            var argString = Newtonsoft.Json.JsonConvert.SerializeObject(arg);
            var handler = new ChannelSubscriptionGiftHandler();
            handler.Handle(this.Connection.EventSub, argString);
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
            SubscriptionTier plan = SubscriptionTier.Tier1,
            string planName = "Channel Subscription",
            int numberOfGifts = 1
        )
        {
            var follow = new
            {
                user_id = "",
                user_name = "",
                user_login = "",
                broadcaster_user_id = "",
                broadcaster_user_name = "",
                broadcaster_user_login = "",
                tier = plan == SubscriptionTier.Tier3 ? 3000 : (plan == SubscriptionTier.Tier2 ? 2000 : 1000),
                total = numberOfGifts,
                cumulative_months = numberOfGifts,
                is_anonymous = true
            };
            var arg = new
            {
                metadata = new
                {
                    message_id = "test",
                    message_type = "",
                    message_timestamp = "",
                },
                payload = new
                {
                    @event = follow,
                }
            };
            var argString = Newtonsoft.Json.JsonConvert.SerializeObject(arg);
            var handler = new ChannelSubscriptionGiftHandler();
            handler.Handle(this.Connection.EventSub, argString);
        }

        #endregion

        /**************************************************
         * INTERNAL CODE. NO NEED TO READ BELOW THIS LINE *
         **************************************************/

        #region Internal

        internal override void InitializeClient(Client client)
        {
            client.OnGiftedSubscription += Client_OnGiftedSubscription;
        }

        private void Client_OnGiftedSubscription(object sender, TwitchLib.Client.Events.OnGiftedSubscriptionArgs e)
        {
            this.UserManager.FetchUserWithID(e.GiftedSubscription.UserId, (gifter) =>
            {
                this.UserManager.FetchUserWithID(e.GiftedSubscription.MsgParamRecipientId, (user) =>
                {
                    this.SubscribersThisStream.Add(gifter);
                    user.Subscription = new Models.Subscription();
                    int months = 1;
                    if (!int.TryParse(e.GiftedSubscription.MsgParamMonths, out months))
                    {
                        months = 1;
                    }
                    user.Subscription.SubscribedMonthCount = months;
                    user.Subscription.IsGift = true;
                    user.Subscription.Gifter = gifter;
                    switch (e.GiftedSubscription.MsgParamSubPlan) {
                        case TwitchLib.Client.Enums.SubscriptionPlan.Tier1:
                            user.Subscription.Tier = SubscriptionTier.Tier1; 
                            break;
                        case TwitchLib.Client.Enums.SubscriptionPlan.Tier2:
                            user.Subscription.Tier = SubscriptionTier.Tier2;
                            break;
                        case TwitchLib.Client.Enums.SubscriptionPlan.Tier3:
                            user.Subscription.Tier = SubscriptionTier.Tier3;
                            break;
                        case TwitchLib.Client.Enums.SubscriptionPlan.Prime:
                            user.Subscription.Tier = SubscriptionTier.Prime;
                            break;
                        default:
                            user.Subscription.Tier = SubscriptionTier.NotSet;
                            break;
                    }
                    user.Subscription.PlanName = e.GiftedSubscription.MsgParamSubPlanName;
                    this.SubscribersThisStream.Add(user);
                    this.UserSubscribed(user);
                });
            });
        }

        internal override void InitializeEventSub(EventSubWebsocketClient eventSub)
        {
            eventSub.ChannelSubscriptionMessage -= EventSub_ChannelSubscriptionMessage;
            eventSub.ChannelSubscriptionMessage += EventSub_ChannelSubscriptionMessage;
            eventSub.ChannelSubscribe -= EventSub_ChannelSubscribe;
            eventSub.ChannelSubscribe += EventSub_ChannelSubscribe;
            //eventSub.ChannelSubscriptionGift -= EventSub_ChannelSubscriptionGift;
            //eventSub.ChannelSubscriptionGift += EventSub_ChannelSubscriptionGift;

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

            /*var createSub3 = this.HelixEventSub.CreateEventSubSubscriptionAsync(
                "channel.subscription.gift",
                "1",
                new Dictionary<string, string> {
                    { "broadcaster_user_id", this.Manager.ConnectionManager.ChannelID },
                },
                this.Connection.EventSub.SessionId,
                this.Connection.ConnectionConfig.ClientID,
                this.Manager.ConnectionManager.Secrets.AccountAccessToken
            );
            TwitchManager.RunTask(createSub3, (response) =>
            {
                Logger.LogInfo("channel.subscription.gift subscription created.");
            }, (ex) =>
            {
                Logger.LogError(ex.ToString());
            });*/
        }

        private Task EventSub_ChannelSubscriptionGift(object sender, TwitchLib.EventSub.Websockets.Core.EventArgs.Channel.ChannelSubscriptionGiftArgs args)
        {
            var ev = args.Notification.Payload.Event;
            Logger.LogInfo(ev.UserLogin + " just gave " + ev.Total.ToString() + " gifts.");
            return Task.CompletedTask;
        }

        private System.Threading.Tasks.Task EventSub_ChannelSubscriptionMessage(object sender, TwitchLib.EventSub.Websockets.Core.EventArgs.Channel.ChannelSubscriptionMessageArgs args)
        {
            var ev = args.Notification.Payload.Event;
            var user = this.UserManager.UserForEventSubSubscriptionMessageNotification(ev);
            this.SubscribersThisStream.Add(user);
            ThreadDispatcher.Enqueue(() =>
            {
                this.UserSubscribed(user);
            });
        
            return Task.CompletedTask;
        }

        private System.Threading.Tasks.Task EventSub_ChannelSubscribe(object sender, TwitchLib.EventSub.Websockets.Core.EventArgs.Channel.ChannelSubscribeArgs args)
        {
           
            var ev = args.Notification.Payload.Event;
            var user = this.UserManager.UserForEventSubSubscriptionNotification(ev);
            this.SubscribersThisStream.Add(user);
            ThreadDispatcher.Enqueue(() =>
            {
                this.UserSubscribed(user);
            });
            return Task.CompletedTask;
        }

        #endregion
    }
}
