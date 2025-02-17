using System.Collections.Generic;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Models;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using Twitchmata.Models;

namespace Twitchmata.Adapters.Models
{
    public class ChatMessageAdapter 
    {
        public ChatMessage Wrapped { get; protected set; }

        public ChatMessageAdapter(ChannelChatMessage ev, User user, ConnectionManager connectionManager)
        {
            int monthsSubbed = 0;
            bool isPartner = false;
            bool isTurbo = false;
            bool isStaff = false;
            List<KeyValuePair<string, string>> badges = new List<KeyValuePair<string, string>>();
            CheerBadge cheerBadge = null;
            var userType = TwitchLib.Client.Enums.UserType.Viewer;
            if(ev.IsBroadcaster)
            {
                userType = TwitchLib.Client.Enums.UserType.Broadcaster;
            }
            if(ev.IsModerator)
            {
                userType = TwitchLib.Client.Enums.UserType.Moderator;
            }
            foreach (var badge in ev.Badges)
            {
                if (badge.SetId == "subscriber")
                {
                    monthsSubbed = int.Parse(badge.Info);
                }
                if (badge.SetId == "partner")
                {
                    isPartner = true;
                }
                if (badge.SetId == "turbo")
                {
                    isTurbo = true;
                }
                if (badge.SetId == "staff")
                {
                    isStaff = true;
                    userType = TwitchLib.Client.Enums.UserType.Staff;
                }
                if (badge.SetId == "bits")
                {
                    cheerBadge = new CheerBadge(int.Parse(badge.Info));
                }
                badges.Add(new KeyValuePair<string, string>(badge.SetId, badge.Info));
            }
            int bits = ev.Cheer != null ? ev.Cheer.Bits : 0;
            this.Wrapped = new ChatMessage(
                connectionManager.ConnectionConfig.BotName,
                user.UserId,
                user.UserName,
                user.DisplayName,
                ev.Color,
                System.Drawing.Color.FromArgb(
                    (int)(255 * user.ChatColor.Value.r),
                    (int)(255 * user.ChatColor.Value.g),
                    (int)(255 * user.ChatColor.Value.b)
                ),
                new TwitchLib.Client.Models.EmoteSet((string)null, ev.Message.Text),
                ev.Message.Text,
                userType,
                connectionManager.ChannelID,
                ev.MessageId,
                ev.IsSubscriber,
                monthsSubbed,
                "",
                isTurbo,
                ev.IsModerator,
                ev.Message.Text.Contains("\\u0001ACTION"),
                ev.IsBroadcaster,
                ev.IsVip,
                isPartner,
                isStaff,
                Noisy.False,
                ev.Message.Text,
                ev.Message.Text,
                badges,
                cheerBadge,
                bits,
                ConvertBitsToUsd(bits)
            );
        }
        private static double ConvertBitsToUsd(int bits)
        {
            if (bits < 1500)
            {
                return (double)bits / 100.0 * 1.4;
            }

            if (bits < 5000)
            {
                return (double)bits / 1500.0 * 19.95;
            }

            if (bits < 10000)
            {
                return (double)bits / 5000.0 * 64.4;
            }

            if (bits < 25000)
            {
                return (double)bits / 10000.0 * 126.0;
            }

            return (double)bits / 25000.0 * 308.0;
        }
    }
}