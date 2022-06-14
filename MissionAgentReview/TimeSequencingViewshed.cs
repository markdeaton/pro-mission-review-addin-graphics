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
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Mapping;
using MissionAgentReview.Exceptions;
using System;
using System.Collections.Generic;
using System.Timers;

namespace MissionAgentReview {
    /// <summary>
    /// Helper class to associate each camera viewpoint with a timestamp
    /// (for use in time-referenced viewshed coordination)
    /// </summary>
    public class TSVViewpoint {
        public TSVViewpoint(DateTime dt, Camera camera) {
            Timestamp = dt;
            Camera = camera;
        }
        public DateTime Timestamp { get;}
        public Camera Camera { get; }
    }

    public class TimeSequencingViewshed : Viewshed {
        public TimeSequencingViewshed(TimeSequencingViewshed tsv) : base(tsv) {
            _timer.Elapsed += OnIntervalElapsed;
        }
        // Constructor with dummy camera
        public TimeSequencingViewshed(SpatialReference sr, double verticalAngle, double horizontalAngle, double minimumDistance, double maximumDistance)
                               : base(new Camera(0, 0, 0, 0, 0, sr), verticalAngle, horizontalAngle, minimumDistance, maximumDistance) {
            _timer.Elapsed += OnIntervalElapsed;
        }
        public TimeSequencingViewshed(IList<TSVViewpoint> locations, int idxLocation, double verticalAngle, double horizontalAngle, double minimumDistance, double maximumDistance)
                                : base(locations[idxLocation].Camera, verticalAngle, horizontalAngle, minimumDistance, maximumDistance) {
            _viewpoints = locations;
            _viewpointIndex = idxLocation;
            _timer.Elapsed += OnIntervalElapsed;
        }

        public bool IsValidAnalysisLayer {
            get {
                bool valid = false;
                try {
                    valid = base.MapView != null && !Double.IsNaN(MinimumDistance) && !Double.IsNaN(MaximumDistance) && !Double.IsNaN(HorizontalAngle) && !Double.IsNaN(VerticalAngle);
                } catch (InvalidOperationException) { // Base viewshed has been otherwise removed from the scene
                    valid = false;
                }
                return valid;                
            }
        }

        private Timer _timer = new Timer() { AutoReset = true, Interval = 2000 };
        /// <summary>
        /// How long the animation will pause with a viewshed analysis at each agent trackpoint
        /// </summary>
        /// <remarks>Default value is 2000 ms</remarks>
        public double DwellTimeMs {
            get { return _timer.Interval; }
            set { _timer.Interval = value; }
        }

        /// <summary>
        /// Keeps a record of the location currently showing a viewshed; intended for use in stopping and starting the timer.
        /// </summary>
        private int _viewpointIndex = -1;
        private IList<TSVViewpoint> _viewpoints;
        /// <summary>
        /// The agent trackpoint camera viewpoints at which to sequentially generate viewpoints
        /// </summary>
        public IList<TSVViewpoint> Viewpoints {
            get { return _viewpoints; }
            set { _viewpoints = value; }
        }
        public int ViewpointIndex {
            get { return _viewpointIndex; }
        }
        /// <summary>
        /// Begins animating using the DwellTimeMs property as the interval
        /// </summary>
        public void Start() {
            _timer.Start();
            FrameworkApplication.State.Activate("sequencingViewshedRunning_state");
        }
        /// <summary>
        /// Stops animating
        /// </summary>
        public void Stop() {
            _timer.Stop();
            FrameworkApplication.State.Deactivate("sequencingViewshedRunning_state");
        }

        private void OnIntervalElapsed(Object source, System.Timers.ElapsedEventArgs e) {
            try {
                ShowNext();
            } catch (TimeSequencingViewshedInvalidException) {
                Stop();
                Dispose();
                MessageBox.Show("The viewshed analysis was unexpectedly removed from the scene. Please invoke the analysis again.", "Invalid Viewshed Object");
            }
        }

        public void ShowNext() {
            try {
                _viewpointIndex++;
                if (_viewpointIndex < 0 || _viewpointIndex > Viewpoints?.Count - 1) _viewpointIndex = 0;

                this.SetObserver(Viewpoints?[_viewpointIndex]?.Camera);
            } catch (InvalidOperationException e) {
                throw new TimeSequencingViewshedInvalidException("Error in viewshed ShowNext", e, _viewpoints, _viewpointIndex);
            }
        }
        public void ShowPrev() {
            try {
                _viewpointIndex--;
                if (_viewpointIndex > Viewpoints?.Count - 1 || _viewpointIndex < 0) _viewpointIndex = Viewpoints.Count - 1;

                this.SetObserver(Viewpoints?[_viewpointIndex]?.Camera);
            } catch (InvalidOperationException e) {
                Stop();
                throw new TimeSequencingViewshedInvalidException("Error in viewshed ShowPrev", e, _viewpoints, _viewpointIndex);
            }
        }

        public void Dispose() {
            _timer.Close();
            ((IDisposable)_timer).Dispose();
        }

        #region Overrides
        #endregion
    }
}
