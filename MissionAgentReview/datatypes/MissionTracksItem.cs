using ArcGIS.Desktop.Core.Portal;

namespace MissionAgentReview.datatypes {
    public class MissionTracksItem {
        public MissionTracksItem(PortalItem portalItem, string missionName, string trackFCTitle) {
            this.PortalItem = portalItem;
            this.MissionName = missionName;
            this.TrackFCTitle = trackFCTitle;
        }

        public override string ToString() {
            return $"{MissionName} [{TrackFCTitle}]";
        }

        #region Properties
        public PortalItem PortalItem { get; set; }
        public string MissionName { get; set; }
        public string TrackFCTitle { get; set; }

        #endregion
    }
}
