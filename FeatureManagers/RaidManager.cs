using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TwitchLib.Unity;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using System.Threading.Tasks;
using TwitchLib.PubSub.Events;

namespace Twitchmata {
    public class RaidManager : FeatureManager {
        #region Client
        override internal void InitializeClient(Client client) {
            Debug.Log("Setting Up Incoming Raid Notifications");
            client.OnRaidNotification -= Client_OnRaidNotification;
            client.OnRaidNotification += Client_OnRaidNotification;
        }

        private void Client_OnRaidNotification(object sender, OnRaidNotificationArgs args) {
            var user = this.UserManager.UserForRaidNotification(args.RaidNotification);
            var raid = new Models.IncomingRaid() {
                Raider = user,
                ViewerCount = Int32.Parse(args.RaidNotification.MsgParamViewerCount),
            };
            this.RaidsThisStream.Add(raid);
            this.RaidReceived(raid);
        }

        #endregion


        #region PubSub
        //Cancelling a raid breaks all of PubSub so this is disabled for now
        //internal override void InitializePubSub(PubSub pubSub)
        //{
        //    Debug.Log("Setting Up Outgoing Raid Notifications");
        //    pubSub.OnRaidUpdateV2 -= PubSub_OnRaidUpdate;
        //    pubSub.OnRaidUpdateV2 += PubSub_OnRaidUpdate;
        //    pubSub.OnRaidGo -= PubSub_OnRaidGo;
        //    pubSub.OnRaidGo += PubSub_OnRaidGo;
        //    pubSub.ListenToRaid(this.ChannelID);
        //}

        //private void PubSub_OnLog(object sender, TwitchLib.PubSub.Events.OnLogArgs e)
        //{
        //    Debug.Log("Message: "+ e.Data);
        //}

        //private void PubSub_OnRaidGo(object sender, OnRaidGoArgs e) {
        //    Debug.Log($"Raid updated {e.TargetDisplayName} {e.ViewerCount} {e.TargetProfileImage}");
        //}

        //private void PubSub_OnRaidUpdate(object sender, OnRaidUpdateV2Args e) {
        //    Debug.Log($"Raid updated {e.TargetDisplayName} {e.ViewerCount} {e.TargetProfileImage}");
        //}
        #endregion


        #region Notifications
        public virtual void RaidReceived(Models.IncomingRaid raid) {
            Debug.Log($"{raid.Raider.DisplayName} raided with {raid.ViewerCount} viewers");
        }
        #endregion

        #region Outgoing Raids
        public void StartRaid(Models.User userToRaid, Action<Models.OutgoingRaid> action) {
            var task = this.HelixAPI.Raids.StartRaidAsync(this.ChannelID, userToRaid.UserId);
            TwitchManager.RunTask(task, obj => {
                var raid = obj.Data[0];
                var outgoingRaid = new Models.OutgoingRaid() {
                    RaidTarget = userToRaid,
                    CreatedAt = raid.CreatedAt,
                    IsMature = raid.IsMature,
                };
                action.Invoke(outgoingRaid);
            });
        }

        public void CancelRaid(Action action = null) {
            var task = this.HelixAPI.Raids.CancelRaidAsync(this.ChannelID);
            TwitchManager.RunTask(task, () => {
                if (action != null) {
                    action.Invoke();
                }
            });
        }

        #endregion

        #region Stats
        public List<Models.IncomingRaid> RaidsThisStream { get; private set; } = new List<Models.IncomingRaid>() { };
        #endregion
    }
}
