using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MissionAgentReview.Extensions {
    public static class Extensions {

        /// <summary>
        /// This returns the total distance spanned by the agent track segments in a track segment graphics layer.
        /// </summary>
        /// <returns>Total distance traveled by the agent from start to finish, in map distance units</returns>
        public static double AgentTravelDistance(this IList<CIMLineGraphic> graphics) {
            double length = 0;

            foreach (CIMLineGraphic lg in graphics) {
                if (lg != null) {
                    length += lg.Line.Length;
                }
            }

            return length;
        }
    }
}
