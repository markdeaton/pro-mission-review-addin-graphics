//   Copyright 2020 Esri
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at

//       http://www.apache.org/licenses/LICENSE-2.0

//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
using System;
using System.Collections.Generic;

namespace MissionAgentReview.Exceptions {
    /// <summary>
    /// A special-use exception for the case when the user has removed a TimeSequencingViewshed manually from the scene
    /// using GUI exploratory analysis tools ("remove all") and then clicks the prev/next button again.
    /// </summary>
    class TimeSequencingViewshedInvalidException : InvalidOperationException {
        private const string DATA_VIEWPOINTS_LIST = "Viewpoints_List";
        private const string DATA_VIEWPOINTS_IDX = "Viewpoints_Index";

        /// <param name="msg">A string describing the error</param>
        /// <param name="exc">The exception that, when caught, prompted this exception to be created</param>
        /// <param name="viewpoints">The locations/viewpoints list from the original TimeSequencingViewshed that unexpectedly disappeared</param>
        /// <param name="viewpoints_idx">The index of the location viewpoint that was active at the time the viewshed became unavailable</param>
        public TimeSequencingViewshedInvalidException(string msg, InvalidOperationException exc, 
            IList<TSVViewpoint> viewpoints, int viewpoints_idx) : base(msg, exc) {

            Data.Add(DATA_VIEWPOINTS_LIST, viewpoints);
            Data.Add(DATA_VIEWPOINTS_IDX, viewpoints_idx);
        }

        /// <summary>
        /// The locations/viewpoints list from the original TimeSequencingViewshed that unexpectedly disappeared
        /// </summary>
        public IList<TSVViewpoint> Viewpoints {
            get {
                return (IList<TSVViewpoint>) Data[DATA_VIEWPOINTS_LIST];
            }
        }

        /// <summary>
        /// The index of the location viewpoint that was active at the time the viewshed became unavailable
        /// </summary>
        public int CurrentViewpointIndex {
            get {
                return (int)Data[DATA_VIEWPOINTS_IDX];
            }
        }
    }
}
