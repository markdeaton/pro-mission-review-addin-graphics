using ArcGIS.Desktop.Core.Portal;
using System.Collections.Generic;

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

        public override int GetHashCode() {
            int hashCode = -17125422;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(MissionName);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(MissionItem.ItemID);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(TracksItem.ItemID);
            return hashCode;
        }
    }
}
