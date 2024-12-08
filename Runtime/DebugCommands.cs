using System;
using System.Collections.Generic;
using Twitchmata.Models;

namespace Twitchmata {
    internal class DebugCommands {
        private TwitchManager TwitchManager;
        internal DebugCommands(TwitchManager twitchManager) {
            this.TwitchManager = twitchManager;
            this.SetupChatCommands();
        }


        private void SetupChatCommands() {
            var chatMessageManager = this.TwitchManager.GetFeatureManager<ChatMessageManager>();
            
            chatMessageManager.RegisterChatCommand("debug-bits", Permissions.Broadcaster, OnDebugBits);
            chatMessageManager.RegisterChatCommand("debug-follow", Permissions.Broadcaster, OnDebugFollow);
            chatMessageManager.RegisterChatCommand("debug-sub", Permissions.Broadcaster, OnDebugSub);
            chatMessageManager.RegisterChatCommand("debug-gift-sub", Permissions.Broadcaster, OnDebugGiftSub);
            chatMessageManager.RegisterChatCommand("debug-raid", Permissions.Broadcaster, OnDebugRaid);
        }

        //!debug-bits <bits> <username>|anon
        private void OnDebugBits(List<string> arguments, User user) {
            var bitAmount = 100;
            if (arguments.Count >= 1) {
                bitAmount = Int32.Parse(arguments[0]);
            }

            string username = null;
            if (arguments.Count >= 2) {
                username = arguments[1];
            }

            this.TwitchManager.GetFeatureManager<BitsManager>().Debug_SendBits(bitAmount, user.UserName, user.UserId);
        }
        
        //!debug-follow <username>
        private void OnDebugFollow(List<string> arguments, User user) {
            string username = null;
            if (arguments.Count >= 1) {
                username = arguments[0];
            }

            if (username == null) {
                this.TwitchManager.GetFeatureManager<FollowManager>().Debug_NewFollow();
                return;
            }
            
            this.TwitchManager.UserManager.FetchUserWithUserName(username, follower => {
                this.TwitchManager.GetFeatureManager<FollowManager>().Debug_NewFollow(follower.DisplayName, follower.UserName, follower.UserId);
            });
        }

        //!debug-sub tier1|tier2|tier3|prime <username>
        private void OnDebugSub(List<string> arguments, User user) {
            SubscriptionTier tier = SubscriptionTier.Tier1;
            if (arguments.Count >= 1) {
                string tierString = arguments[0];
                if (tierString == "tier1") {
                    tier = SubscriptionTier.Tier1;
                } else if (tierString == "tier2") {
                    tier = SubscriptionTier.Tier2;
                } else if (tierString == "tier3") {
                    tier = SubscriptionTier.Tier3;
                } else if (tierString == "prime") {
                    tier = SubscriptionTier.Prime;
                }
            }
            
            
            string username = null;
            if (arguments.Count >= 2) {
                username = arguments[1];
            }

            if (username == null) {
                this.TwitchManager.GetFeatureManager<SubscriberManager>().Debug_NewSubscription(plan: tier);
                return;
            }
            
            this.TwitchManager.UserManager.FetchUserWithUserName(username, subscriber => {
                this.TwitchManager.GetFeatureManager<SubscriberManager>().Debug_NewSubscription(subscriber.DisplayName, subscriber.UserName, subscriber.UserId, tier);
            });
        }

        //!debug-gift-sub tier1|tier2|tier3|prime <recipient-username> <gifter-username>|anon
        private void OnDebugGiftSub(List<string> arguments, User user) {
            SubscriptionTier tier = SubscriptionTier.Tier1;
            if (arguments.Count >= 1) {
                string tierString = arguments[0];
                if (tierString == "tier1") {
                    tier = SubscriptionTier.Tier1;
                } else if (tierString == "tier2") {
                    tier = SubscriptionTier.Tier2;
                } else if (tierString == "tier3") {
                    tier = SubscriptionTier.Tier3;
                } else if (tierString == "prime") {
                    tier = SubscriptionTier.Prime;
                }
            }
            
            
            string recipientUsername = null;
            if (arguments.Count >= 2) {
                recipientUsername = arguments[1];
            }
            
            string gifterUsername = null;
            if (arguments.Count >= 3) {
                gifterUsername = arguments[2];
            }

            if (recipientUsername == null) {
                this.TwitchManager.GetFeatureManager<SubscriberManager>().Debug_NewGiftSubscription(plan: tier);
                return;
            }

            this.TwitchManager.UserManager.FetchUserWithUserName(recipientUsername, recipient => {
                if (gifterUsername == "anon") {
                    this.TwitchManager.GetFeatureManager<SubscriberManager>().Debug_NewAnonymousGiftSubscription(recipient.DisplayName, recipient.UserName, recipient.UserId, tier);
                    return;
                }

                this.TwitchManager.UserManager.FetchUserWithUserName(gifterUsername, gifter => {
                    this.TwitchManager.GetFeatureManager<SubscriberManager>().Debug_NewGiftSubscription(gifter.DisplayName, gifter.UserName, gifter.UserId, recipient.DisplayName, recipient.UserName, recipient.UserId, tier);
                });
            });
        }

        //!debug-raid <viewer-count> <raider-username>
        private void OnDebugRaid(List<string> arguments, User user) {
            var viewerCount = 26;
            if (arguments.Count >= 1) {
                viewerCount = Int32.Parse(arguments[0]);
            }
            
            string username = null;
            if (arguments.Count >= 2) {
                username = arguments[1];
            }

            if (username == null) {
                this.TwitchManager.GetFeatureManager<RaidManager>().Debug_IncomingRaid(viewerCount);
                return;
            }
            
            this.TwitchManager.UserManager.FetchUserWithUserName(username, raider => {
                this.TwitchManager.GetFeatureManager<RaidManager>().Debug_IncomingRaid(viewerCount, raider.DisplayName, raider.UserName, raider.UserId);
            });
        }
    }
}