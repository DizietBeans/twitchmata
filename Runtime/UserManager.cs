using System;
using System.Collections.Generic;
using System.Linq;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.Channels.GetChannelVIPs;
using TwitchLib.Api.Helix.Models.Moderation.GetModerators;
using TwitchLib.Client.Models;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.PubSub.Events;
using TwitchLib.PubSub.Models.Responses.Messages;
using TwitchLib.PubSub.Models.Responses.Messages.Redemption;
using TwitchLib.Unity;
using Twitchmata.Models;
using UnityEngine;

namespace Twitchmata {
    public class UserManager {
        internal ConnectionManager ConnectionManager;
        internal UserManager(ConnectionManager connectionManager){
            this.ConnectionManager = connectionManager;
        }

        #region User Management
        private Dictionary<string, Models.User> UsersByID { get; set; } = new Dictionary<string, Models.User> {};

        /// <summary>
        /// Get an existing user, if one exists
        /// </summary>
        /// <param name="userID">The user ID to find the user for</param>
        /// <returns>A user, if that user exists locally</returns>
        public Models.User UserWithID(string userID) {
            if (UsersByID.ContainsKey(userID)) { 
                return UsersByID[userID];
            }
            return null;
        }

        /// <summary>
        /// Get an existing user, i fone exists
        /// </summary>
        /// <param name="userName">The username of to find the user for</param>
        /// <returns>A user, if that user exists locally</returns>
        public Models.User UserWithUserName(string userName) {
            var normalisedUsername = this.NormalisedUsername(userName);
            foreach (var user in UsersByID.Values) {
                if (user.UserName == normalisedUsername) {
                    return user;
                }
            }
            return null;
        }
        #endregion


        #region User Fetching
        /// <summary>
        /// Fetch a user from the Twitch API
        /// </summary>
        /// <param name="userId">The ID of the user to fetch</param>
        /// <param name="action">An action that accepts the user (or null if an error occured)</param>
        public void FetchUserWithID(string userId, Action<Models.User> action) {
            var task = this.ConnectionManager.API.Helix.Users.GetUsersAsync(new List<string> { userId });
            TwitchManager.RunTask(task, obj => {
                var users = obj.Users;
                if (users.Length == 0) {
                    action.Invoke(null);
                    return;
                }
                var user = this.ExistingOrNewUser(users[0].Id, users[0].Login, users[0].DisplayName);
                action.Invoke(user);
            });
        }

        /// <summary>
        /// Fetch a user from the Twitch API
        /// </summary>
        /// <param name="userName">The username of the user to fetch</param>
        /// <param name="action">An action that accepts the user (or null if an error occured)</param>
        public void FetchUserWithUserName(string userName, Action<Models.User> action) {
            var normalisedUsername = this.NormalisedUsername(userName);
            var task = this.ConnectionManager.API.Helix.Users.GetUsersAsync(null, new List<string> { normalisedUsername });
            TwitchManager.RunTask(task, obj => {
                var users = obj.Users;
                if (users.Length == 0) {
                    action.Invoke(null);
                    return;
                }
                var user = this.ExistingOrNewUser(users[0].Id, users[0].Login, users[0].DisplayName);
                action.Invoke(user);
            });
        }
        #endregion



        /**************************************************
        * INTERNAL CODE. NO NEED TO READ BELOW THIS LINE *
        **************************************************/

        #region Internal User-Creation Conveniences
        private Models.User ExistingOrNewUser(string userID, string userName, string displayName) {
            var user = this.UserWithID(userID);
            if (user == null) {
                user = new Models.User(userID, userName, displayName);
                UsersByID[user.UserId] = user;
            }
            return user;
        }

        internal Models.User UserForBitsRedeem(OnBitsReceivedV2Args bitsRedeem) {
            if (bitsRedeem.IsAnonymous) {
                return null;
            }
            return this.ExistingOrNewUser(bitsRedeem.UserId, bitsRedeem.UserName, bitsRedeem.UserName);
        }

        internal Models.User UserForEventSubBitsRedeem(ChannelCheer bitsRedeem)
        {
            if (bitsRedeem.IsAnonymous)
            {
                return null;
            }
            return this.ExistingOrNewUser(bitsRedeem.UserId, bitsRedeem.UserLogin, bitsRedeem.UserName);
        }

        internal Models.User UserForRaidNotification(RaidNotification raidNotification) {
            var user = this.ExistingOrNewUser(raidNotification.UserId, raidNotification.Login, raidNotification.DisplayName);
            user.IsModerator = raidNotification.Moderator;
            user.IsSubscriber = raidNotification.Subscriber;
            return user;
        }

        internal Models.User UserForRaidUpdateNotification(OnRaidUpdateV2Args raidNotification) {
            return this.ExistingOrNewUser(raidNotification.TargetChannelId, raidNotification.TargetLogin, raidNotification.TargetDisplayName);
        }

        internal Models.User UserForRaidCancelNotification(OnRaidCancelArgs raidNotification) {
            return this.ExistingOrNewUser(raidNotification.TargetChannelId, raidNotification.TargetLogin, raidNotification.TargetDisplayName);
        }

        internal Models.User UserForRaidGoNotification(OnRaidGoArgs raidNotification) {
            return this.ExistingOrNewUser(raidNotification.TargetChannelId, raidNotification.TargetLogin, raidNotification.TargetDisplayName);
        }

        internal Models.User UserForFollowNotification(OnFollowArgs followNotification) {
            return this.ExistingOrNewUser(followNotification.UserId, followNotification.Username, followNotification.DisplayName);
        }
        internal Models.User UserForEventSubFollowNotification(ChannelFollow followNotification)
        {
            return this.ExistingOrNewUser(followNotification.UserId, followNotification.UserLogin, followNotification.UserName);
        }
        internal Models.User UserForSubscriptionNotification(ChannelSubscription subscriptionNotification) {
            var subscription = new Models.Subscription();
            subscription.SubscribedMonthCount = subscriptionNotification.CumulativeMonths ?? 1;
            subscription.StreakMonths = subscriptionNotification.StreakMonths ?? 1;
            subscription.Tier = (SubscriptionTier)subscriptionNotification.SubscriptionPlan;
            subscription.PlanName = subscriptionNotification.SubscriptionPlanName;


            Models.User subscriber = null;
            if (subscriptionNotification.IsGift == true) {
                subscription.IsGift = true;
                if (subscriptionNotification.UserId != null) {
                    subscription.Gifter = this.ExistingOrNewUser(subscriptionNotification.UserId, subscriptionNotification.Username, subscriptionNotification.DisplayName);
                }
                subscriber = this.ExistingOrNewUser(subscriptionNotification.RecipientId, subscriptionNotification.RecipientName, subscriptionNotification.RecipientDisplayName);
            } else {
                subscription.IsGift = false;
                subscriber = this.ExistingOrNewUser(subscriptionNotification.UserId, subscriptionNotification.Username, subscriptionNotification.DisplayName);
            }

            subscriber.IsSubscriber = true;
            subscriber.Subscription = subscription;

            return subscriber;
        }
        internal Models.User UserForEventSubSubscriptionMessageNotification(ChannelSubscriptionMessage subscriptionNotification)
        {
            var subscription = new Models.Subscription();
            subscription.SubscribedMonthCount = subscriptionNotification.CumulativeMonths;
            subscription.StreakMonths = subscriptionNotification.StreakMonths ?? 1;
            subscription.Tier = Subscription.TierForString(subscriptionNotification.Tier);
            subscription.PlanName = "Channel Subscription (" + subscriptionNotification.BroadcasterUserLogin + ")"; //not present on EventSub notification


            Models.User subscriber = null;
            subscription.IsGift = false;
            subscriber = this.ExistingOrNewUser(subscriptionNotification.UserId, subscriptionNotification.UserLogin, subscriptionNotification.UserName);

            subscriber.IsSubscriber = true;
            subscriber.Subscription = subscription;

            return subscriber;
        }
        internal Models.User UserForEventSubSubscriptionGiftNotification(ChannelSubscribe subscriptionNotification, TwitchLib.Api.Helix.Models.Subscriptions.Subscription giftData)
        {
            var subscription = new Models.Subscription();

            subscription.SubscribedMonthCount = 1; //can't get, frustrating
            subscription.StreakMonths = 1; //ditto
            subscription.Tier = Subscription.TierForString(subscriptionNotification.Tier);
            subscription.PlanName = "Channel Subscription (" + subscriptionNotification.BroadcasterUserLogin + ")"; //not present on EventSub notification
            
            subscription.IsGift = subscriptionNotification.IsGift;
            if (subscription.IsGift) {

            }
            Models.User subscriber = this.ExistingOrNewUser(subscriptionNotification.UserId, subscriptionNotification.UserLogin, subscriptionNotification.UserName);

            subscriber.IsSubscriber = true;
            subscriber.Subscription = subscription;

            return subscriber;
        }

        internal Models.User UserForChannelPointsRedeem(Redemption rewardRedemption) {
            return this.ExistingOrNewUser(rewardRedemption.User.Id, rewardRedemption.User.Login, rewardRedemption.User.DisplayName); ;
        }

        internal Models.User UserForEventSubChannelPointsRedeem(ChannelPointsCustomRewardRedemption rewardRedemption)
        {
            return this.ExistingOrNewUser(rewardRedemption.UserId, rewardRedemption.UserLogin, rewardRedemption.UserName); ;
        }


        internal Models.User UserForChannelPointsRedemptionResponse(RewardRedemption rewardRedemption) {
            return this.ExistingOrNewUser(rewardRedemption.UserId, rewardRedemption.UserLogin,
                rewardRedemption.UserName);
        }

        internal Models.User UserForChatMessage(ChatMessage chatMessage) {
            var user = this.ExistingOrNewUser(chatMessage.UserId, chatMessage.Username, chatMessage.DisplayName);
            user.IsBroadcaster = chatMessage.IsBroadcaster;
            user.IsSubscriber = chatMessage.IsSubscriber;
            user.IsVIP = chatMessage.IsVip;
            user.IsModerator = chatMessage.IsModerator;
            user.ChatColor = this.ColorForHex(chatMessage.ColorHex);
            if (user.Subscription != null) {
                user.Subscription.SubscribedMonthCount = chatMessage.SubscribedMonthCount;
            }
            return user;
        }

        internal Models.User UserForAPISubscription(TwitchLib.Api.Helix.Models.Subscriptions.Subscription subscriber) {
            var user = this.ExistingOrNewUser(subscriber.UserId, subscriber.UserLogin, subscriber.UserName);

            user.IsSubscriber = true;

            var sub = new Models.Subscription();
            sub.Tier = Models.Subscription.TierForString(subscriber.Tier);
            sub.PlanName = subscriber.PlanName;
            sub.IsGift = subscriber.IsGift;

            var gifter = ExistingOrNewUser(subscriber.GiftertId, subscriber.GifterLogin, subscriber.GifterName);

            user.Subscription = sub;

            return user;
        }

        internal Models.User UserForAPIVIP(ChannelVIPsResponseModel vip) {
            var user = this.ExistingOrNewUser(vip.UserId, vip.UserLogin, vip.UserName);

            user.IsVIP = true;
            
            return user;
        }

        internal Models.User UserForAPIModerator(Moderator moderator) {
            var user = this.ExistingOrNewUser(moderator.UserId, moderator.UserLogin, moderator.UserName);

            user.IsModerator = true;
            
            return user;
        }
        #endregion


        #region Fetching Initial User Info
        internal string BroadcasterID { get; private set; }
        internal string BotID { get; private set; }
        internal void PerformSetup(Action callback) {
            var channelName = this.ConnectionManager.ConnectionConfig.ChannelName;
            var channelID = this.ConnectionManager.Secrets.ChannelIDForChannel(channelName);
            if (channelID != null && channelID.Length > 0) {
                this.BroadcasterID = channelID;
                this.FetchUserInfo();
                this.FetchUserWithID(channelID, (user) => {
                    user.IsBroadcaster = true;
                });
                callback.Invoke();
            }
            else {
                this.FetchUserWithUserName(channelName, (user) => {
                    user.IsBroadcaster = true;
                    this.ConnectionManager.Secrets.SetChannelIDForChannel(channelName, user.UserId);
                    this.BroadcasterID = user.UserId;
                    this.FetchUserInfo();
                    callback.Invoke();

                });
            }
         
        }

        internal void FetchBotInfo() {
            var botName = this.ConnectionManager.ConnectionConfig.BotName;

            if (botName == null || botName.Length == 0) {
                return;
            }

            var botID = this.ConnectionManager.Secrets.ChannelIDForChannel(botName);
            if (botID != null && botID.Length > 0) {
                
                this.BotID = botID;
                this.FetchUserWithID(botID, (user) => { });
            } else {
                this.FetchUserWithUserName(botName, (user) => {
                    this.BotID = user.UserId;
                });
            }
        }


        internal void FetchUserInfo() {
            this.FetchBotInfo();
            this.FetchNextSubscribers();
            this.FetchNextVIPs();
            this.FetchNextModerators();
        }

        private string ChannelID {
            get { return this.ConnectionManager.ChannelID; }
        }

        private static int FetchSize = 100;

        private void FetchNextSubscribers(string pagination = null) {
            var task = this.ConnectionManager.API.Helix.Subscriptions.GetBroadcasterSubscriptionsAsync(this.ChannelID, UserManager.FetchSize, pagination);
            TwitchManager.RunTask(task, obj => {
                var subscribers = obj.Data;
                if (subscribers.Length == UserManager.FetchSize && obj.Pagination != null) {
                    this.FetchNextSubscribers(obj.Pagination.Cursor);
                }

                foreach (var subscriber in subscribers) {
                    this.UserForAPISubscription(subscriber);
                }
            });
        }

        private void FetchNextVIPs(string pagination = null) {
            var task = this.ConnectionManager.API.Helix.Channels.GetVIPsAsync(this.ChannelID, null, UserManager.FetchSize, pagination);
            TwitchManager.RunTask(task, obj => {
                var vips = obj.Data;
                if (vips.Length == UserManager.FetchSize && obj.Pagination != null) {
                    this.FetchNextVIPs(obj.Pagination.Cursor);
                }

                foreach (var vip in vips) {
                    this.UserForAPIVIP(vip);
                }
            });
        }

        private void FetchNextModerators(string pagination = null) {
            var task = this.ConnectionManager.API.Helix.Moderation.GetModeratorsAsync(this.ChannelID, null, UserManager.FetchSize, pagination);
            TwitchManager.RunTask(task, obj => {
                var moderators = obj.Data;
                if (moderators.Length == UserManager.FetchSize && obj.Pagination != null) {
                    this.FetchNextModerators(obj.Pagination.Cursor);
                }

                foreach (var moderator in moderators) {
                    this.UserForAPIModerator(moderator);
                }
            });
        }
        #endregion


        #region Helpers

        private string NormalisedUsername(string username) {
            if (username.StartsWith("@") && username.Length > 1)
            {
                return username.Substring(1);
            }
            return username;
        }

        public Color? ColorForHex(string hexString) {
            if (hexString.Length != 7) {
                return null;
            }
            var rHex = hexString.Substring(1, 2);
            var gHex = hexString.Substring(3, 2);
            var bHex = hexString.Substring(5, 2);

            var r = Convert.ToInt32(rHex, 16);
            var g = Convert.ToInt32(gHex, 16);
            var b = Convert.ToInt32(bHex, 16);

            return new Color(r / 255f, g / 255f, b / 255f);
        }

        #endregion
    }
}

