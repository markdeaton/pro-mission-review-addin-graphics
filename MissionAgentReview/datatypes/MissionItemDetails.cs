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
            return obj is MissionItemDetails details &&
                MissionName == details.MissionName &&
                MissionItem.ItemID == details.MissionItem.ItemID &&
                TracksItem.ItemID == details.TracksItem.ItemID;
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
