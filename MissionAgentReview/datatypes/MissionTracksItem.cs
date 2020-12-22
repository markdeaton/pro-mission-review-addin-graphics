using ArcGIS.Desktop.Core.Portal;

namespace MissionAgentReview.datatypes {
    public class MissionTracksItem {
        public MissionTracksItem(PortalItem portalItem, string missionName, string trackFCTitle) {
            this.PortalItem = portalItem;
            this.MissionName = missionName;
            this.trackFCTitle = trackFCTitle;
        }

        public override string ToString() {
            return $"{MissionName} [{trackFCTitle}]";
        }

        #region Properties
        private PortalItem _portalItem;
        private string _missionName;
        private string trackFCTitle;

        public PortalItem PortalItem { get => _portalItem; set => _portalItem = value; }
        public string MissionName { get => _missionName; set => _missionName = value; }
        public string TrackFCTitle { get => trackFCTitle; set => trackFCTitle = value; }

        #endregion    
    }
}
