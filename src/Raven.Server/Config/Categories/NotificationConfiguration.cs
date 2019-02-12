using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Raven.Server.Config.Attributes;
using Raven.Server.NotificationCenter.Notifications;
using Raven.Server.ServerWide;
using Raven.Server.ServerWide.BackgroundTasks;

namespace Raven.Server.Config.Categories
{
    public class NotificationConfiguration : ConfigurationCategory
    {
        [Description("Semicolon seperated list of notification names which will not be shown in the Studio. If not specified, all notifications are allowed. Example: \"SlowIO;Server_NewVersionAvailable;ClusterTopologyWarning\".")]
        [DefaultValue(null)]
        [ConfigurationEntry("Notifications.FilterOut", ConfigurationEntryScope.ServerWideOnly)]
        public HashSet<string> FilteredOutNotifications { get; set; }

        [Description("Release channel for new server version notification.")]
        [DefaultValue(LatestVersionCheck.Channel.Patch)]
        [ConfigurationEntry("Notifications.ServerUpdatesChannel", ConfigurationEntryScope.ServerWideOnly)]
        public LatestVersionCheck.Channel ServerUpdatesChannel { get; set; }

        public bool ShouldFilterOut(Notification notification)
        {
            if (FilteredOutNotifications == null)
                return false;

            string name;

            // A user may choose to filter out a specific notification of either AlertRaised or PerformanceHint types
            // or they may filter out an entire notification type (e.g. PerformanceHint, DatabaseChanged, NotificationUpdated, etc.)
            if (FilteredOutNotifications.Contains(notification.Type.ToString()))
                return true;

            switch (notification)
            {
                case AlertRaised alert:
                    name = alert.AlertType.ToString();
                    break;
                case PerformanceHint hint:
                    name = hint.HintType.ToString();
                    break;
                default:
                    return false;
            }
            
            return FilteredOutNotifications.Contains(name);
        }
    }
}
