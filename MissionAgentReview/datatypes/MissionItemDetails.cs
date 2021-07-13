using ArcGIS.Desktop.Core.Portal;

namespace MissionAgentReview.datatypes {
    public class MissionItemDetails {
        public string MissionName { get; set; }
        public PortalGroup Group { get; set; }
        public PortalItem MissionItem { get; set; }
        //public string FolderId { get; set; }
        public PortalItem TracksItem { get; set; }

        public override bool Equals(object obj) {
            if (obj is MissionItemDetails) {
                // For equality, Mission name and all item IDs EXCEPT GROUP must match
                // This handles the possibility the same mission is in more than one group
                MissionItemDetails mids = obj as MissionItemDetails;
                return (
                    this.MissionName == mids.MissionName &&
                    this.MissionItem.ItemID == mids.MissionItem.ItemID &&
                    TracksItem.ItemID == mids.TracksItem.ItemID);
            } else return MissionName.Equals(obj);
        }
    }
}
