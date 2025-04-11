using System.Collections.Generic;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;
using UnityEngine;

namespace Twitchmata.Models {
    /// <summary>
    /// Delegate that is invoked when a channel point redemption is successful
    /// </summary>
    /// <param name="redemption">Details of the redemption</param>
    public delegate void RewardRedemptionCallback(ChannelPointRedemption redemption, CustomRewardRedemptionStatus status);

    /// <summary>
    /// Represents a reward created and managed by your Twitch overlay
    /// </summary>
    /// <remark>
    /// Note: updating the properties of a ManagedReward after registering it will not
    /// lead to any updates until the overlay is restarted. If you wish to update properties
    /// while the overlay is running, please look at the Update Rewards region in ChannelPointManager
    /// </remark>
    public class ManagedReward {
        /// <summary>
        /// The Id of the reward
        /// </summary>
        /// <remarks>
        /// This is set when the reward is fetched from the API
        /// </remarks>
        public string Id { get; internal set; }

        /// <summary>
        /// The title of the reward. Must be unique.
        /// </summary>
        /// <remarks>
        /// This is used to fetch the reward from the API so should be unique and match a reward
        /// that was created by Twitchmata.
        ///
        /// If a reward already exists with this title that wasn't created by your overlay then you 
        /// will need to delete it on Twitch's website if you want to utilise managed features such
        /// as auto-refunding, enabling/disabling, and updating cost.
        /// 
        /// If you don't care about the above features then consider an Unmanaged Reward
        /// </remarks>
        public string Title { get; internal set; }

        /// <summary>
        /// The cost of the reward in channel points
        /// </summary>
        public int Cost { get; internal set; }

        /// <summary>
        /// Whether the reward is enabled
        /// </summary>
        public bool IsEnabled { get; internal set; }

        /// <summary>
        /// The permissions of who can use the reward
        /// </summary>
        public Permissions Permissions { get; private set; }

        /// <summary>
        /// Creates a new managed reward. See the above properties for details on arguments.
        /// </summary>
        public ManagedReward(string title, string description, int cost, Permissions permissions = Permissions.Everyone, bool isEnabled = true) {
            this.Title = title;
            this.Description = description;
            this.Cost = cost;
            this.IsEnabled = isEnabled;
            this.Permissions = permissions;
        }

        #region Extra Options

        /// <summary>
        /// If set, holds the maximum number of times this reward can be redeemed each stream
        /// </summary>
        public int? MaxPerStream { get; set; }

        /// <summary>
        /// If set, holds the maximum number of times a user can redeem this reward each stream
        /// </summary>
        public int? MaxPerUserPerStream { get; set; }

        /// <summary>
        /// If set, holds the minimum number of seconds between redeems of this reward
        /// </summary>
        public int? GlobalCooldownSeconds { get; set; }

        /// <summary>
        /// A description of what the reward does
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Whether the user needs to enter input to redeem a reward
        /// </summary>
        public bool RequiresUserInput { get; set; } = false;

        /// <summary>
        /// A list of valid inputs if the reward requires user input. Leave empty if any input is valid
        /// </summary>
        /// <remarks>
        /// If an invalid input is entered the channel points will be refunded.
        /// Ensure you use lower case values for inputs
        /// </remarks>
        public List<string> ValidInputs { get; set; } = new List<string>();

        /// <summary>
        /// Whether the reward's redemption is auto-fulfilled by Twitchmata
        /// </summary>
        public bool AutoFulfills { get; set; } = true;


        /// <summary>
        /// If true, then the callback you associated with the reward will be called even if the redemption is canceled 
        /// </summary>
        public bool InvokesCallbackIfCancelled { get; set; } = false;

        #endregion
        
        #region Stats

        /// <summary>
        /// A list of all fulfilled redemptions of this managed reward this stream
        /// </summary>
        public List<ChannelPointRedemption> RedemptionsThisStream = new List<ChannelPointRedemption>();

        internal void HandleRedemption(ChannelPointRedemption redemption, CustomRewardRedemptionStatus status) {
            redemption.Reward = this;
            
            if ((this.InvokesCallbackIfCancelled == false) && (status == CustomRewardRedemptionStatus.CANCELED)) {
                return;
            }

            if (status == CustomRewardRedemptionStatus.FULFILLED) {
                this.RedemptionsThisStream.Add(redemption);
            }

            this.Callback(redemption, status);
        }
        
        #endregion


        #region Internal Helpers

        internal ManagedReward(CustomReward remoteReward)
        {
            this.Id = remoteReward.Id;
            this.Title = remoteReward.Title;
            this.Cost = remoteReward.Cost;
            this.IsEnabled = remoteReward.IsEnabled;
            this.Description = remoteReward.Prompt;
            this.RequiresUserInput = remoteReward.IsUserInputRequired;

            if (remoteReward.MaxPerStreamSetting.IsEnabled) {
                this.MaxPerStream = remoteReward.MaxPerStreamSetting.MaxPerStream;
            } else {
                this.MaxPerStream = null;
            }

            if (remoteReward.MaxPerUserPerStreamSetting.IsEnabled) {
                this.MaxPerUserPerStream = remoteReward.MaxPerUserPerStreamSetting.MaxPerUserPerStream;
            } else {
                this.MaxPerUserPerStream = null;
            }

            if (remoteReward.GlobalCooldownSetting.IsEnabled) {
                this.GlobalCooldownSeconds = remoteReward.GlobalCooldownSetting.GlobalCooldownSeconds;
            } else {
                this.GlobalCooldownSeconds = null;
            }
        }

        internal RewardRedemptionCallback Callback { get; set; }

        internal UpdateCustomRewardRequest UpdateRequestForDifferencesFrom(ManagedReward otherReward) {
            var hasUpdated = false;
            var updateRequest = new UpdateCustomRewardRequest();
            if (this.Cost != otherReward.Cost) {
                updateRequest.Cost = this.Cost;
                hasUpdated = true;
            }
            if (this.IsEnabled != otherReward.IsEnabled) {
                updateRequest.IsEnabled = this.IsEnabled;
                hasUpdated = true;
            }
            if (this.RequiresUserInput != otherReward.RequiresUserInput) {
                updateRequest.IsUserInputRequired = this.RequiresUserInput;
                hasUpdated = true;
            }
            if (this.Description != otherReward.Description) {
                updateRequest.Prompt = this.Description;
                hasUpdated = true;
            }
            if (this.MaxPerStream != otherReward.MaxPerStream) {
                updateRequest.IsMaxPerStreamEnabled = (this.MaxPerStream != null);
                if (this.MaxPerStream != null) { 
                    updateRequest.MaxPerStream = (int)this.MaxPerStream;
                }
                hasUpdated = true;
            }
            if (this.MaxPerUserPerStream != otherReward.MaxPerUserPerStream) {
                updateRequest.IsMaxPerUserPerStreamEnabled = (this.MaxPerUserPerStream != null);
                if (this.MaxPerUserPerStream != null) {
                    updateRequest.MaxPerUserPerStream = (int)this.MaxPerUserPerStream;
                }
                hasUpdated = true;
            }
            if (this.GlobalCooldownSeconds != otherReward.GlobalCooldownSeconds) {
                updateRequest.IsGlobalCooldownEnabled = (this.GlobalCooldownSeconds != null);
                if (this.GlobalCooldownSeconds != null)  {
                    updateRequest.GlobalCooldownSeconds = (int)this.GlobalCooldownSeconds;
                }
                hasUpdated = true;
            }
            if (this.Description != otherReward.Description) {
                updateRequest.Prompt = this.Description;
                hasUpdated = true;
            }

            if (hasUpdated == false) {
                return null;
            }
            return updateRequest;
        }

        internal CreateCustomRewardsRequest CreateRewardRequest() {
            var request = new CreateCustomRewardsRequest();
            request.Title = this.Title;
            request.Prompt = this.Description;
            request.Cost = this.Cost;
            request.IsEnabled = this.IsEnabled;
            request.IsUserInputRequired = this.RequiresUserInput;
            request.IsMaxPerStreamEnabled = (this.MaxPerStream != null);
            if (this.MaxPerStream != null) { 
                request.MaxPerStream = (int)this.MaxPerStream;
            }
            request.IsMaxPerUserPerStreamEnabled = (this.MaxPerUserPerStream != null);
            if (this.MaxPerUserPerStream != null) {
                request.MaxPerUserPerStream = (int)this.MaxPerUserPerStream;
            }
            request.IsGlobalCooldownEnabled = (this.GlobalCooldownSeconds != null);
            if (this.GlobalCooldownSeconds != null) {
                request.GlobalCooldownSeconds = (int)this.GlobalCooldownSeconds;
            }
            return request;
        }
        #endregion
    }
}

