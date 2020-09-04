using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
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
        /// <returns>Total distance traveled by the agent from start to finish, in meters>
        public static double AgentTravelDistanceMeters(this IList<CIMLineGraphic> graphics) {
            double length = 0;

            foreach (CIMLineGraphic lg in graphics) {
                if (lg != null) {
                    Polyline line = lg.Line;
                    if (line.SpatialReference.Unit != LinearUnit.Meters) {
                        // Project it so it is in meters
                        Polyline lineProj = (Polyline)GeometryEngine.Instance.Project(line, SpatialReferences.WebMercator);
                        length += lineProj.Length;
                    } else {
                        length += lg.Line.Length;
                    }
                }
            }

            return length;
        }
    }
}
