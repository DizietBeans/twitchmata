using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using External.Twitchmata.Models;
using TwitchLib.Api.Helix.Models.Chat;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.EventSub.Websockets;
using Twitchmata.Adapters.Models;
using Twitchmata.Models;
using UnityEngine;

namespace Twitchmata {
    /// <summary>
    /// Used to respond to and send chat messages and notification in your overlay
    /// </summary>
    /// <remarks>
    /// To utilise ChatMessageManager create a subclass and add to a GameObject (either the)
    /// Game object holding TwitchManager or a child GameObject).
    ///
    /// See <code>RegisterChatCommand()</code> for details on how to set up commands
    /// </remarks>
    public class ChatMessageManager : FeatureManager {
        
        #region ChatMessages

        /// <summary>
        /// Fired when a user sends a message
        /// </summary>
        public virtual void ChatMessageReceived(User user, ChatMessage chatMessage)
        {
            Logger.LogInfo($"{user.DisplayName}: {chatMessage.Message}"); 
        }

        #endregion
        
        #region Command Registration
        /// <summary>
        /// Register a chat command that viewers can invoke
        /// </summary>
        /// <param name="command">The name of the command (excluding any command prefix such as !)</param>
        /// <param name="permissions">Permissions for who can invoke the command</param>
        /// <param name="callback">The delegate method to call when this command is invoked</param>
        public void RegisterChatCommand(string command, Permissions permissions, ChatCommandCallback callback) {
            this.RegisterChatCommand(command, permissions, (arguments, user, message) => callback(arguments, user));
        }

        public void RegisterChatCommand(string command, Permissions permissions, ChatCommandMessageCallback callback) {
            this.RegisteredCommands[command.ToLower()] = new RegisteredChatCommand() {
                Permissions = permissions,
                Callback = callback,
            };
        }
        #endregion

        #region Message Matchers
        private List<MessageMatcher> MessageMatchers = new List<MessageMatcher>();
        
        /// <summary>
        /// Convenience method for adding a match against multiple strings. Useful if multiple variants of a string should have the same outcome (e.g. "hi", "hello", "hey" playing a greeting sound)
        /// </summary>
        /// <param name="matches">An array of strings to match</param>
        public void AddStringMessageMatchers(string[] matches, StringMatchKind kind, MessageMatcherCallback callback, MessageMatcherOptions options = MessageMatcherOptions.None, Permissions permissions = Permissions.Everyone) {
            foreach (var match in matches) {
                this.AddStringMessageMatcher(match, kind, callback, options, permissions);
            }
        }
        
        /// <summary>
        /// Look for matches against the supplied string in chat messages, invoking the callback if a match is found
        /// </summary>
        /// <param name="match">The string to match against</param>
        /// <param name="kind">The kind of match (Equals, StartsWith, or Contains)</param>
        /// <param name="callback">A callback to be invoked with the match</param>
        /// <param name="options">Any options for the match</param>
        /// <param name="permissions">Permissions for the match</param>
        public void AddStringMessageMatcher(string match, StringMatchKind kind, MessageMatcherCallback callback, MessageMatcherOptions options = MessageMatcherOptions.None, Permissions permissions = Permissions.Everyone) {
            var matchWholeWords = ((options & MessageMatcherOptions.FullWordsOnly) == MessageMatcherOptions.FullWordsOnly);
            var isCaseInsensitive = ((options & MessageMatcherOptions.CaseInsensitive) == MessageMatcherOptions.CaseInsensitive);
            if (matchWholeWords && (kind == StringMatchKind.StartsWith)) {
                var regex = new Regex("^" + Regex.Escape(match) + "[.!*?;:]*( |$)", (isCaseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None));
                this.AddRegexMessageMatcher(regex, callback, permissions);
            } else if (matchWholeWords && (kind == StringMatchKind.Contains)) {
                var regex = new Regex("(^| )[*:;]*" + Regex.Escape(match) + "[.!*?;:]*( |$)", (isCaseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None));
                this.AddRegexMessageMatcher(regex, callback, permissions);
            } else {
                var matcher = new StringMessageMatcher(match, kind, callback, isCaseInsensitive, permissions);
                this.MessageMatchers.Add(matcher);
            }
        }

        /// <summary>
        /// Look for matches against the supplied regular expression in chat messages, invoking the callback if a match is found
        /// </summary>
        /// <param name="regex">The regular expression to check against</param>
        /// <param name="callback">A callback to be invoked with the match</param>
        /// <param name="permissions">Permissions for the match</param>
        public void AddRegexMessageMatcher(Regex regex, MessageMatcherCallback callback, Permissions permissions = Permissions.Everyone) {
            var matcher = new RegexMessageMatcher(regex, callback, permissions);
            this.MessageMatchers.Add(matcher);
        }

        #endregion

        
        #region Announcements
        /// <summary>
        /// Send an announcement to the chat
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="colour">The announcement colour to use (default: null, i.e. the channel colour)</param>
        /// <param name="preferredAccount">The preferredAccount to post with (falls back to broadcaster if bot is not available)</param>
        public void SendAnnouncement(string message, AnnouncementColors colour = null, PosterAccount preferredAccount = PosterAccount.Broadcaster) {
            var account = this.Connection.ChannelID;
            var accessToken = this.Connection.Secrets.AccountAccessToken;
            if (preferredAccount == PosterAccount.Bot && this.Connection.BotID != null) {
                account = this.Connection.BotID;
                accessToken = this.Connection.Secrets.BotAccessToken;
            }

            var task = this.Connection.API.Helix.Chat.SendChatAnnouncementAsync(this.Connection.ChannelID, account, message, colour, accessToken);
            TwitchManager.RunTask(task, () => {
                Logger.LogInfo("Announcement sent");
            });
        }
        #endregion


        #region Shoutouts
        //Implement in TwitchLib
        //Add rate limit (2min per shoutout, 60m per streamer)
        //Sender (broadcaster or chat bot)
        //Type (native, text, auto [uses native but falls back to text if necessary])
        [Tooltip("The template to use for textual shoutouts. {{user}} will be replaced by streamer's display name, {{category}} by the category they last streamed in, and {{url}} with a link to their stream")]
        public string ShoutOutTemplate = "Check out {{user}}, they are streaming {{category}} at {{url}}";

        private int ShoutOutLimit = 120;
        private int PerStreamerShoutoutLimit = 3600;

        private DateTime? LastNativeShoutout = null;
        private Dictionary<string, DateTime> PastNativeShoutouts = new Dictionary<string, DateTime>() {};

        private Regex UserRegex = new Regex(Regex.Escape("{{user}}"));
        private Regex CategoryRegex = new Regex(Regex.Escape("{{category}}"));
        private Regex UrlRegex = new Regex(Regex.Escape("{{url}}"));
        public void ShoutOut(string streamerName, bool prefersNative = true) {
            this.UserManager.FetchUserWithUserName(streamerName, (user) => {
                if (user == null) {
                    Logger.LogError($"Could not shout out {streamerName}");
                    return;
                }

                bool useNative = true;
                if (prefersNative == false) {
                    useNative = false;
                } else if ((this.LastNativeShoutout != null) && DateTime.Now.Subtract((DateTime)this.LastNativeShoutout).TotalSeconds < this.ShoutOutLimit) {
                    useNative = false;
                } else if (this.PastNativeShoutouts.ContainsKey(streamerName)) {
                    var pastShoutoutTime = this.PastNativeShoutouts[streamerName];
                    if (DateTime.Now.Subtract(pastShoutoutTime).TotalSeconds < this.PerStreamerShoutoutLimit) {
                        useNative = false;
                    }
                }

                if (useNative) {
                    var task = this.HelixAPI.Chat.SendShoutoutAsync(this.Connection.ChannelID, user.UserId, this.Connection.ChannelID);
                    TwitchManager.RunTask(task, () => {
                        Logger.LogInfo("Shout out sent");
                        this.LastNativeShoutout = DateTime.Now;
                        this.PastNativeShoutouts[streamerName] = DateTime.Now;
                    });
                } else {
                    var task = this.HelixAPI.Channels.GetChannelInformationAsync(user.UserId);
                    TwitchManager.RunTask(task, (channelInfo) => {
                        var userReplacedMsg = this.UserRegex.Replace(this.ShoutOutTemplate, user.DisplayName);
                        var categoryReplacedMsg = this.CategoryRegex.Replace(userReplacedMsg, channelInfo.Data[0].GameName);
                        var urlReplacedMsg = this.UrlRegex.Replace(categoryReplacedMsg, "https://twitch.tv/" + user.UserName);
                        this.SendChatMessage(urlReplacedMsg);
                    });
                    
                }
            });
        }
        #endregion

        #region Types

        public delegate void MessageMatcherCallback(MessageMatch match);
    
        /// <summary>
        /// The type of string match
        /// </summary>
        public enum StringMatchKind {
            /// <summary>
            /// The message must exactly match the match string
            /// </summary>
            Equals,
            /// <summary>
            /// The message must start with the match string
            /// </summary>
            StartsWith,
            /// <summary>
            /// The message must contain the match string
            /// </summary>
            Contains
        }

        /// <summary>
        /// Options for message matchers
        /// </summary>
        [Flags]
        public enum MessageMatcherOptions {
            /// <summary>
            /// No options
            /// </summary>
            None,
            /// <summary>
            /// Match should be case insensitive
            /// </summary>
            CaseInsensitive,
            /// <summary>
            /// Should only match full words
            /// </summary>
            FullWordsOnly
        }

        #endregion


        /**************************************************
         * INTERNAL CODE. NO NEED TO READ BELOW THIS LINE *
         **************************************************/

        #region Internal
        private Dictionary<string, RegisteredChatCommand> RegisteredCommands = new Dictionary<string, RegisteredChatCommand>();

        //In prep for if we want to replace Chat Bot (IRC Client) as well as PubSub
        internal override void InitializeEventSub(EventSubWebsocketClient eventSub)
        {
           /* Logger.LogInfo("Setting up ChatMessageManager with EventSub");
            eventSub.ChannelChatMessage += EventSub_ChannelChatMessage;

            Logger.LogInfo("Creating EventSub subscriptions for ChatMessageManager");
            var createSub = this.HelixEventSub.CreateEventSubSubscriptionAsync(
                "channel.chat.message",
                "1",
                new Dictionary<string, string> {
                    { "broadcaster_user_id", this.Manager.ConnectionManager.BotID },
                    { "user_id", this.Manager.ConnectionManager.BotID }
                },
                this.Connection.EventSub.SessionId,
                this.Connection.ConnectionConfig.ClientID,
                this.Manager.ConnectionManager.Secrets.AccountAccessToken
            );
            TwitchManager.RunTask(createSub, (response) =>
            {
                Logger.LogInfo("channel.chat.message subscription created.");
            }, (ex) =>
            {
                Logger.LogError(ex.ToString());
            });*/
        }

        private System.Threading.Tasks.Task EventSub_ChannelChatMessage(object sender, TwitchLib.EventSub.Websockets.Core.EventArgs.Channel.ChannelChatMessageArgs args)
        {
            var ev = args.Notification.Payload.Event;
            var user = new User(ev.ChatterUserId, ev.ChatterUserLogin, ev.ChatterUserName);
            user.ChatColor = this.UserManager.ColorForHex(ev.Color);
            user.IsModerator = ev.IsModerator;
            user.IsSubscriber = ev.IsSubscriber;
            user.IsVIP = ev.IsVip;
            user.IsBroadcaster = ev.IsBroadcaster;

            var message = new ChatMessageAdapter(ev, user, this.Connection);
            
            foreach (var messageMatcher in this.MessageMatchers)
            {
                messageMatcher.HandleMessage(message.Wrapped, this.UserManager);
            }
            ChatMessageReceived(user, message.Wrapped);
            return Task.CompletedTask;
        }

        override internal void InitializeClient(TwitchLib.Unity.Client client)
        {
            client.OnChatCommandReceived -= Client_OnChatCommandReceived;
            client.OnChatCommandReceived += Client_OnChatCommandReceived;

            client.OnMessageReceived -= Client_OnMessageReceived;
            client.OnMessageReceived += Client_OnMessageReceived;
        }

        private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            foreach (var messageMatcher in this.MessageMatchers)
            {
                messageMatcher.HandleMessage(e.ChatMessage, this.UserManager);
            }
            var user = this.UserManager.UserForChatMessage(e.ChatMessage);
            ChatMessageReceived(user, e.ChatMessage);
        }

        private void Client_OnChatCommandReceived(object sender, OnChatCommandReceivedArgs args)
        {
            var command = args.Command;
            if (this.RegisteredCommands.ContainsKey(command.CommandText.ToLower()) == false)
            {
                return;
            }

            var user = this.UserManager.UserForChatMessage(command.ChatMessage);

            var registeredCommand = this.RegisteredCommands[command.CommandText.ToLower()];
            if (user.IsPermitted(registeredCommand.Permissions) == false)
            {
                this.SendChatMessage("You don't have permission to use this command");
                return;
            }

            registeredCommand.Callback.Invoke(command.ArgumentsAsList, user, command.ChatMessage);
        }
        #endregion

    }
}
