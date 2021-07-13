using ArcGIS.Desktop.Core.Portal;

namespace MissionAgentReview.datatypes {
    public class MissionItemDetails {
        public string MissionName { get; set; }
        public PortalGroup Group { get; set; }
        public PortalItem MissionItem { get; set; }
        //public string FolderId { get; set; }
        public PortalItem TracksItem { get; set; }
    }
}
