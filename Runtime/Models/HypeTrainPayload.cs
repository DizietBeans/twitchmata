using System;
using System.Collections.Generic;
using TwitchLib.EventSub.Core.Models.HypeTrain;

namespace Twitchmata.Models
{
    /// <summary>
    /// HypeTrain Payload used when a HypeTrain progress changes
    /// </summary>
    public class HypeTrainPayload
    {
        /// <summary>
        /// Progress of the last Progression Event
        /// </summary>
        public int Progress { get; set; }
        /// <summary>
        /// Goal to next Level
        /// </summary>
        public int Goal { get; set; }
        /// <summary>
        /// Last Contributor
        /// </summary>
        public HypeTrainContribution LastContribution { get; set; }
        /// <summary>
        /// Date and Time of the last Progress Event
        /// </summary>
        public DateTimeOffset ExpiresAt { get; set; }
        /// <summary>
        /// Total gathered Hypetrain Points
        /// </summary>
        public int Total { get; set; }
        /// <summary>
        /// List of the Top Contributions
        /// </summary>
        public List<HypeTrainContribution> TopContributions { get; set; }
        /// <summary>
        /// Date and Time that the HypeTrain started
        /// </summary>
        public DateTimeOffset StartedAt { get; set; }
        /// <summary>
        /// Current Level of the Hypetrain
        /// </summary>
        public int Level { get; set; }
    }
}