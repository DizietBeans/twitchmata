using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TwitchLib.Unity;
using Twitchmata.Adapters;
using Twitchmata.Adapters.Args;
using Twitchmata.Adapters.Models;

namespace Twitchmata
{
    /// <summary>
    /// Used to hook into bits events in your overlay
    /// </summary>
    /// <remarks>
    /// To utilise PowerUpsManager create a subclass and add to a GameObject (either the
    /// GameObject holding TwitchManager or a child GameObject).
    ///
    /// Then override <code>ReceivedPowerUp()</code> and add your power-up handling code.
    /// </remarks>
    public class PowerUpsManager : FeatureManager
    {
        #region Notifications
        /// <summary>
        /// Fired when a user uses bits to redeem a Custom Power Up. 
        /// </summary>
        /// <param name="redemption">Info on the power up redeemed.</param>
        public virtual void ReceivedPowerUp(PowerUpRedemption redemption)
        {
            Logger.LogInfo($"Received {redemption.CustomPowerUp.Bits} Bits from {redemption.UserName}, on custom power up {redemption.CustomPowerUp.Title} and prompt {redemption.CustomPowerUp.Prompt}");
        }
        #endregion

        /**************************************************
         * INTERNAL CODE. NO NEED TO READ BELOW THIS LINE *
         **************************************************/

        #region Internal

        internal override void InitializeEventSub(ExtendedEventSubWebsocketClient eventSub)
        {
            Logger.LogInfo("Initialising Power Ups Manager");
            eventSub.PowerUpRedemption -= EventSub_PowerUpRedemption;
            eventSub.PowerUpRedemption += EventSub_PowerUpRedemption;

            if (this.Connection.UseDebugServer)
            {
                return;
            }
            var createSub = this.HelixEventSub.CreateEventSubSubscriptionAsync(
                "channel.custom_power_up_redemption.add",
                "1",
                new Dictionary<string, string> {
                    { "broadcaster_user_id", this.Manager.ConnectionManager.ChannelID },
                },
                eventSub.SessionId,
                this.Connection.ConnectionConfig.ClientID,
                this.Manager.ConnectionManager.Secrets.AccountAccessToken
            );
            TwitchManager.RunTask(createSub, (response) =>
            {
                Logger.LogInfo("channel.custom_power_up_redemption.add subscription created.");
            }, (ex) =>
            {
                Logger.LogError(ex.ToString());
            });
        }

        private System.Threading.Tasks.Task EventSub_PowerUpRedemption(object sender, PowerUpRedemptionArgs args)
        {
            Logger.LogInfo("Power Up Redemption Triggered");
            ThreadDispatcher.Enqueue(() => {
                try
                {
                    var ev = args.Notification.Payload.Event;
                    try
                    {
                        this.ReceivedPowerUp(ev);
                    }
                    catch (Exception ex2)
                    {
                        Logger.LogError("Error in Userspace: " + ex2.Message + "\n" + ex2.StackTrace);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in Twitchmata: " + ex.Message + "\n" + ex.StackTrace);
                }
            });

            return Task.CompletedTask;
        }

        #endregion

    }
}