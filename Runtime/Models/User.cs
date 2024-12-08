using UnityEngine;

namespace Twitchmata.Models {
    /// <summary>
    /// Details about a user received from an API callback
    /// </summary>
    /// <remarks>
    /// Due to the nature of Twitch's API this is not guaranteed to be a full representation of the user.
    /// For example, some APIs will return a user's subscription details, some just whether they're subscribed,
    /// and others won't return that info at all.
    ///
    /// Twitchmata tries to pre-fetch the Broadcaster, Moderators, VIPs, and Subscribers to work around this
    /// </remarks>
    public class User {
        /// <summary>
        /// The ID of the user, usually a series of numbers
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// The user's username, used to log in to Twitch
        /// </summary>
        public string UserName { get; }

        /// <summary>
        /// The user's display name, shown in Twitch's UI
        /// </summary>
        public string DisplayName { get; }

        internal User(string userID, string userName, string displayName) {
            this.UserId = userID;
            this.UserName = userName;
            this.DisplayName = displayName;
        }

        /// <summary>
        /// Whether or not the user is the broadcaster
        /// </summary>
        public bool IsBroadcaster { get; internal set; } = false;

        /// <summary>
        /// Whether or not the user is a moderator
        /// </summary>
        public bool IsModerator { get; internal set; } = false;

        /// <summary>
        /// Whether or not the user is a VIP
        /// </summary>
        public bool IsVIP { get; internal set; } = false;

        /// <summary>
        /// Whether or not the user is Subscribed
        /// </summary>
        public bool IsSubscriber { get; internal set; } = false;

        /// <summary>
        /// Whether or not the user is lurking in chat
        /// </summary>
        public bool IsLurking { get; internal set; } = false;

        /// <summary>
        /// Details of the user's subscription. Will be null if the user isn't subscribed. May also be null even if the user *is* subscribed.
        /// </summary>
        public Subscription Subscription { get; internal set; } = null;

        
        public Color? ChatColor { get; internal set; } = null;


        #region Permissions
        internal bool IsPermitted(Permissions permissions) {
            if ((permissions & Permissions.Everyone) == Permissions.Everyone) {
                return true;
            }
            if (this.IsBroadcaster) {
                return true;
            }
            if ((permissions & Permissions.Mods) == Permissions.Mods && this.IsModerator) {
                return true;
            }
            if ((permissions & Permissions.VIPs) == Permissions.VIPs && this.IsVIP) {
                return true;
            }
            if ((permissions & Permissions.Subscribers) == Permissions.Subscribers && this.IsSubscriber) {
                return true;
            }
            return false;
        }
        #endregion


        public override int GetHashCode()
        {
            return this.UserId.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is User)
            {
                return this.UserId.Equals((obj as User).UserId);
            }
            return false;
        }
    }
}
