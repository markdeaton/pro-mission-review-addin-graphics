﻿using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Mapping;
using MissionAgentReview.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace MissionAgentReview {
    public class TimeSequencingViewshed : Viewshed, IDisposable {
        public TimeSequencingViewshed(TimeSequencingViewshed tsv) : base(tsv) {
            _timer.Elapsed += OnIntervalElapsed;
        }
        public TimeSequencingViewshed(Camera observer, double verticalAngle, double horizontalAngle, double minimumDistance, double maximumDistance)
                               : base(observer, verticalAngle, horizontalAngle, minimumDistance, maximumDistance) {
            _timer.Elapsed += OnIntervalElapsed;
        }
        public TimeSequencingViewshed(IList<Camera> locations, int idxLocation, double verticalAngle, double horizontalAngle, double minimumDistance, double maximumDistance)
                                : base(locations[idxLocation], verticalAngle, horizontalAngle, minimumDistance, maximumDistance) {
            _viewpointIndex = idxLocation;
        }

        public bool IsValidAnalysisLayer {
            get { return this.MapView != null && this.MapView.GetExploratoryAnalysisCollection().Contains(this); }
        }

        private bool _disposed = false;
/*        public bool Disposed {
            get { return _disposed; }
        }*/

        private Timer _timer = new Timer() { AutoReset = true, Interval = 2000 };
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
        private int _viewpointIndex = -1;
        private IList<Camera> _viewpoints;
        /// <summary>
        /// The agent trackpoint camera viewpoints at which to sequentially generate viewpoints
        /// </summary>
        public IList<Camera> Viewpoints {
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
            } catch (TimeSequencingViewshedInvalidException exc) {
                Stop();
                Dispose(); _disposed = true;
                MessageBox.Show("The viewshed analysis was unexpectedly removed from the scene. Please invoke the analysis again.", "Invalid Viewshed Object");
            }
        }

        public void ShowNext() {
            try {
                _viewpointIndex++;
                if (_viewpointIndex < 0 || _viewpointIndex > Viewpoints?.Count - 1) _viewpointIndex = 0;

                this.SetObserver(Viewpoints?[_viewpointIndex]);
            } catch (InvalidOperationException e) {
                throw new TimeSequencingViewshedInvalidException("Error in viewshed ShowNext", e, _viewpoints, _viewpointIndex);
            }
        }
        public void ShowPrev() {
            try {
                _viewpointIndex--;
                if (_viewpointIndex > Viewpoints?.Count - 1 || _viewpointIndex < 0) _viewpointIndex = Viewpoints.Count - 1;

                this.SetObserver(Viewpoints?[_viewpointIndex]);
            } catch (InvalidOperationException e) {
                Stop();
                throw new TimeSequencingViewshedInvalidException("Error in viewshed ShowPrev", e, _viewpoints, _viewpointIndex);
            }
        }

        public void Dispose() {
            _timer.Close();
            ((IDisposable)_timer).Dispose();
        }
    }
}
