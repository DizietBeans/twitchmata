namespace Twitchmata.Models {
    /// <summary>
    /// Details of a Twitch subscription
    /// </summary>
    /// <remarks>
    /// Due to the Twitch API not giving full details of a subscription in all callbacks
    /// this cannot be guaranteed to be fully filled out. Pay attention to the remarks for
    /// each property for when it should be available
    /// </remarks>
    public class Subscription {
        /// <summary>
        /// The total number of months the user has been subscribed to the channel
        /// </summary>
        /// <remarks>
        /// Set if the user subscribed/re-subscribed or chatted since the overlay was opened
        /// </remarks>
        public int SubscribedMonthCount { get; internal set; }

        /// <summary>
        /// The number of concurrent months in the user has been subscribed in their current streak
        /// </summary>
        /// <remarks>
        /// This is only set if the user subscribed/re-subscribed since the overlay was opened
        /// </remarks>
        public int StreakMonths { get; internal set; }

        /// <summary>
        /// The tier the user subscribed at.
        /// </summary>
        /// <remarks>
        /// This should always be set if the user is subscribed and the data is available
        /// </remarks>
        public SubscriptionTier Tier { get; internal set; }

        /// <summary>
        /// The name of the subscription plan (usually something like "Channel Subscription (channelname)")
        /// </summary>
        /// <remarks>
        /// This should always be set if the user is subscribed
        /// </remarks>
        public string PlanName { get; internal set; }

        /// <summary>
        /// Whether the subscription is a gift sub
        /// </summary>
        /// <remarks>
        /// This should always be set if the user is subscribed
        /// </remarks>
        public bool IsGift { get; internal set; }

        /// <summary>
        /// A user with details of the gifter
        /// </summary>
        /// <remarks>
        /// This will be null if this is not a gift subscription or the gift was anonymous
        /// </remarks>
        public User Gifter { get; internal set; }

        public string Message { get; internal set; }

        static internal SubscriptionTier TierForString(string tierString) {
            if (tierString == "1000") {
                return SubscriptionTier.Tier1;
            } else if (tierString == "2000") {
                return SubscriptionTier.Tier2;
            } else if (tierString == "3000") {
                return SubscriptionTier.Tier3;
            } else if (tierString == "Prime") {
                return SubscriptionTier.Prime;
            }
            return SubscriptionTier.NotSet;
        }

        static internal string StringForTier(SubscriptionTier tier) {
            switch (tier) {
                case SubscriptionTier.Prime:
                    return "Prime";
                case SubscriptionTier.Tier1:
                    return "1000";
                case SubscriptionTier.Tier2:
                    return "2000";
                case SubscriptionTier.Tier3:
                    return "3000";
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// A list of subscription tiers
    /// </summary>
    public enum SubscriptionTier {
        /// <summary>
        /// If the tier is not available, or if Twitch returns an unknown tier string the tier will be NotSet
        /// </summary>
        NotSet,
        Prime,
        Tier1,
        Tier2,
        Tier3
    }
}

