//   Copyright 2022 Esri
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at

//       http://www.apache.org/licenses/LICENSE-2.0

//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using System.Collections.Generic;

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
