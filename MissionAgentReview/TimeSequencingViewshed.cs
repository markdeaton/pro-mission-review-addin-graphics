using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace MissionAgentReview {
    public class TimeSequencingViewshed : Viewshed {
        public TimeSequencingViewshed(TimeSequencingViewshed tsv): base(tsv) {
            _timer.Elapsed += OnIntervalElapsed;
        }
        public TimeSequencingViewshed(Camera observer, double verticalAngle, double horizontalAngle, double minimumDistance, double maximumDistance) 
                               : base(observer, verticalAngle, horizontalAngle, minimumDistance, maximumDistance) {
            _timer.Elapsed += OnIntervalElapsed;
         }

        private Timer _timer = new Timer() {  AutoReset = true, Interval = 2000 };
        /// <summary>
        /// How long he animation will pause with a viewshed analysis at each agent trackpoint
        /// </summary>
        /// <remarks>Default value is 2000 ms</remarks>
        public double DwellTimeMs {
            get { return _timer.Interval; }
            set { _timer.Interval = value; }
        }

        /// <summary>
        /// Keeps a record of the location currently showing a viewshed; intended for use in stopping and starting the timer.
        /// </summary>
        private int _locationIndex = 0;
        private IList<Camera> _locations;
        /// <summary>
        /// The agent trackpoint camera viewpoints at which to sequentially generate viewpoints
        /// </summary>
        public IList<Camera> Locations {
            get { return _locations; }
            set { _locations = value; }
        }
    
        /// <summary>
        /// Begins animating using the DwellTimeMs property as the interval
        /// </summary>
        public void Start() {
            _timer.Start();
        }
        /// <summary>
        /// Stops animating
        /// </summary>
        public void Stop() {
            _timer.Stop();
        }

        private void OnIntervalElapsed(Object source, System.Timers.ElapsedEventArgs e) {
            if (_locationIndex < 0 || _locationIndex >= Locations?.Count) _locationIndex = 0;

            this.SetObserver(Locations?[_locationIndex]);
            _locationIndex++;
        }

        public void ShowNext() {
            if (_locationIndex < 0 || _locationIndex >= Locations?.Count) _locationIndex = 0;

            this.SetObserver(Locations?[_locationIndex]);
            _locationIndex++;
        }
    }
}
