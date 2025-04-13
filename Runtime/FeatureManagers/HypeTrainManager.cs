using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchLib.Api.Helix.Models.HypeTrain;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using Twitchmata.Models;
using HypeTrainContribution = TwitchLib.EventSub.Core.Models.HypeTrain.HypeTrainContribution;

namespace Twitchmata
{
    /// <summary>
    /// Used to hook into HypeTrain events for your overlay
    /// </summary>
    /// <remarks>
    /// To utilise HypeTrainManager create a subclass and add to a GameObject (either the
    /// GameObject holding TwitchManager or a child GameObject).
    ///
    /// Then override <code>HypeTrainBegins()</code>, <code>HypeTrainProgressed()</code> and <code>HypeTrainCompleted()</code> to add your HypeTrain-handling code.
    /// </remarks>
    public class HypeTrainManager : FeatureManager
    {
        #region Notifications

        /// <summary>
        /// Fired when a HypeTrain begins
        /// </summary>
        /// <param name="payload"></param>
        public virtual void HypeTrainBegins(HypeTrainPayload payload)
        {
            Logger.LogInfo("ChannelHypeTrainBegin received.");
        }

        /// <summary>
        /// Fired when the Progress of the HypeTrain changed
        /// </summary>
        /// <param name="payload"></param>
        public virtual void HypeTrainProgressed(HypeTrainPayload payload)
        {
            Logger.LogInfo("ChannelHypeTrainProgress received.");
        }

        /// <summary>
        /// Fired when the HypeTrain is completed
        /// </summary>
        public virtual void HypeTrainCompleted()
        {
            Logger.LogInfo("ChannelHypeTrainEnd received.");
        }
        
        #endregion

        #region Stats

        /// <summary>
        /// List of users who contributed to the current/last HypeTrain
        /// Will be reset at the begin of a new HypeTrain
        /// </summary>
        public List<HypeTrainContribution> HypeTrainContributors { get; private set; } = new List<HypeTrainContribution>() { };
        /// <summary>
        /// List of the top users who contributed to the current/last HypeTrain
        /// Will be reset at the begin of a new HypeTrain
        /// </summary>
        public List<HypeTrainContribution> HypeTrainTopContributors { get; private set; } = new List<HypeTrainContribution>() { };
        /// <summary>
        /// Last Contribution of the current/Last Hypetrain
        /// Will be reset at the Begin of a new HypeTrain
        /// </summary>
        public HypeTrainContribution LastContribution { get; set; }
        /// <summary>
        /// Total gathered HypeTrain Points of the current/last HypeTrain
        /// /// Will be reset at the Begin of a new HypeTrain
        /// </summary>
        public int TotalHypeTrainPoints { get; set; } = 0;
        /// <summary>
        /// Hype Train Level
        /// Will be reset at the Begin of a new HypeTrain
        /// </summary>
        public int HypeTrainLevel { get; set; } = 0;
        /// <summary>
        /// Date and Time when the current/last HypeTrain started
        /// </summary>
        public DateTimeOffset StartedAt { get; set; }
        /// <summary>
        /// Date and Time when the last HypeTrain ended
        /// </summary>
        public DateTimeOffset EndedAt { get; set; }

        #endregion
        
        /**************************************************
         * INTERNAL CODE. NO NEED TO READ BELOW THIS LINE *
         **************************************************/
        #region Internal

        internal override void InitializeEventSub(EventSubWebsocketClient eventSub)
        {
            eventSub.ChannelHypeTrainBegin -= EventSub_ChannelHypeTrainBegin;
            eventSub.ChannelHypeTrainBegin += EventSub_ChannelHypeTrainBegin;
            eventSub.ChannelHypeTrainEnd -= EventSub_ChannelHypeTrainEnd;
            eventSub.ChannelHypeTrainEnd += EventSub_ChannelHypeTrainEnd;
            eventSub.ChannelHypeTrainProgress -= EventSub_ChannelHypeTrainProgress;
            eventSub.ChannelHypeTrainProgress += EventSub_ChannelHypeTrainProgress;
            
            if (this.Connection.UseDebugServer)
            {
                return;
            }
            
            var createSubBegin = this.HelixEventSub.CreateEventSubSubscriptionAsync(
                "channel.hype_train.begin",
                "1",
                new Dictionary<string, string> {
                    { "broadcaster_user_id", this.Manager.ConnectionManager.ChannelID },
                },
                this.Connection.EventSub.SessionId,
                this.Connection.ConnectionConfig.ClientID,
                this.Manager.ConnectionManager.Secrets.AccountAccessToken
            );
            TwitchManager.RunTask(createSubBegin, (response) =>
            {
                Logger.LogInfo("channel.hype_train.begin subscription created.");
            }, (ex) =>
            {
                Logger.LogError(ex.ToString());
            });
            
            var createSubProgress = this.HelixEventSub.CreateEventSubSubscriptionAsync(
                "channel.hype_train.progress",
                "1",
                new Dictionary<string, string> {
                    { "broadcaster_user_id", this.Manager.ConnectionManager.ChannelID },
                },
                this.Connection.EventSub.SessionId,
                this.Connection.ConnectionConfig.ClientID,
                this.Manager.ConnectionManager.Secrets.AccountAccessToken
            );
            TwitchManager.RunTask(createSubProgress, (response) =>
            {
                Logger.LogInfo("channel.hype_train.progress subscription created.");
            }, (ex) =>
            {
                Logger.LogError(ex.ToString());
            });
            
            var createSubEnd = this.HelixEventSub.CreateEventSubSubscriptionAsync(
                "channel.hype_train.end",
                "1",
                new Dictionary<string, string> {
                    { "broadcaster_user_id", this.Manager.ConnectionManager.ChannelID },
                },
                this.Connection.EventSub.SessionId,
                this.Connection.ConnectionConfig.ClientID,
                this.Manager.ConnectionManager.Secrets.AccountAccessToken
            );
            TwitchManager.RunTask(createSubEnd, (response) =>
            {
                Logger.LogInfo("channel.hype_train.end subscription created.");
            }, (ex) =>
            {
                Logger.LogError(ex.ToString());
            });
        }
        
        private Task EventSub_ChannelHypeTrainBegin(object sender, ChannelHypeTrainBeginArgs args)
        {
            // Reset Contributor Lists before filling it with new HypeTrain data
            HypeTrainContributors.Clear();
            HypeTrainTopContributors.Clear();
            
            var ev = args.Notification.Payload.Event;
            var payload = new HypeTrainPayload()
            {
                StartedAt = ev.StartedAt,
                ExpiresAt = ev.ExpiresAt,
                Goal = ev.Goal,
                Level = ev.Level,
                Progress = ev.Progress,
                LastContribution = ev.LastContribution,
                TopContributions = ev.TopContributions.OfType<HypeTrainContribution>().ToList()
            };
            StartedAt = ev.StartedAt;
            EndedAt = DateTime.MinValue;
            HypeTrainTopContributors.Add(payload.LastContribution);
            HypeTrainContributors = payload.TopContributions;
            LastContribution = payload.LastContribution;
            TotalHypeTrainPoints = payload.Total;
            HypeTrainLevel = payload.Level;
            
            HypeTrainBegins(payload);
            return Task.CompletedTask;
        }
        private Task EventSub_ChannelHypeTrainProgress(object sender, ChannelHypeTrainProgressArgs args)
        {
            var ev = args.Notification.Payload.Event;
            var payload = new HypeTrainPayload()
            {
                StartedAt = ev.StartedAt,
                ExpiresAt = ev.ExpiresAt,
                Goal = ev.Goal,
                Level = ev.Level,
                Progress = ev.Progress,
                LastContribution = ev.LastContribution,
                TopContributions = ev.TopContributions.OfType<HypeTrainContribution>().ToList()
            };
            HypeTrainTopContributors.Add(payload.LastContribution);
            HypeTrainContributors = payload.TopContributions;
            LastContribution = payload.LastContribution;
            TotalHypeTrainPoints = payload.Total;
            HypeTrainLevel = payload.Level;
            
            HypeTrainProgressed(payload);
            return Task.CompletedTask;
        }

        private Task EventSub_ChannelHypeTrainEnd(object sender, ChannelHypeTrainEndArgs args)
        {
            var ev = args.Notification.Payload.Event;
            EndedAt = ev.EndedAt;
            HypeTrainTopContributors = ev.TopContributions.OfType<HypeTrainContribution>().ToList();
            HypeTrainLevel = ev.Level;
            TotalHypeTrainPoints = ev.Total;
            
            HypeTrainCompleted();
            return Task.CompletedTask;
        }
        #endregion
    }
}