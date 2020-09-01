using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MissionAgentReview.Exceptions {
    class TimeSequencingViewshedInvalidException : InvalidOperationException {
        private const string DATA_VIEWPOINTS_LIST = "Viewpoints_List";
        private const string DATA_VIEWPOINTS_IDX = "Viewpoints_Index";


        public TimeSequencingViewshedInvalidException(string s, InvalidOperationException e, 
            IList<Camera> viewpoints, int viewpoints_idx) : base(s, e) {

            Data.Add(DATA_VIEWPOINTS_LIST, viewpoints);
            Data.Add(DATA_VIEWPOINTS_IDX, viewpoints_idx);
        }
        public IList<Camera> Viewpoints {
            get {
                return (IList<Camera>) Data[DATA_VIEWPOINTS_LIST];
            }
        }
        public int CurrentViewpointIndex {
            get {
                return (int)Data[DATA_VIEWPOINTS_IDX];
            }
        }
    }
}
