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
using ArcGIS.Core.Data;
using ArcGIS.Core.Events;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.Internal.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Portal;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using MissionAgentReview.datatypes;
using MissionAgentReview.Exceptions;
using MissionAgentReview.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Field = ArcGIS.Core.Data.Field;
using QueryFilter = ArcGIS.Core.Data.QueryFilter;

namespace MissionAgentReview {
    internal class Module1 : Module {
        private static Module1 _this = null;
        private SubscriptionToken _tocToken;
        private const string FIELD_AGENTNAME = "created_user";
        /// <summary>A custom field only for demos to overcome the issue with Editor Tracking when loading legacy data into an empty mission</summary>
        private const string FIELD_AUX_AGENTNAME_DEMO_USE_ONLY = "created_user_demo_only";
        private static string _field_agentName;
        //private const string FIELD_CREATEDATETIME = "created_date";
        private const string FIELD_TIMESTAMP = "location_timestamp";
        private const string AGENTTRACKLYRNAME_PREAMBLE = "Agent Path: ";
        private static FeatureLayer _agentTracksFeatureLayer;

        /// <summary>
        /// Graphic attribute to hold the agent's heading at the beginning of a line connector
        /// </summary>
        private const string ATTR_HEADING_AT_START = "HeadingBeginning";
        /// <summary>
        /// Graphic attribute to hold the agent's heading at the end of a line connector
        /// </summary>
        private const string ATTR_HEADING_AT_END = "HeadingEnd";

        /// <summary>
        /// Name of Course (heading) attribute in agent tracks feature class attribute table
        /// </summary>
        private const string ATTR_COURSE = "Course";

        /// <summary>
        /// Retrieve the singleton instance to this module here
        /// </summary>
        public static Module1 Current {
            get {
                return _this ?? (_this = (Module1)FrameworkApplication.FindModule("MissionAgentReview_Module"));
            }
        }

        #region Overrides
        /// <summary>
        /// Called by Framework when ArcGIS Pro is closing
        /// </summary>
        /// <returns>False to prevent Pro from closing, otherwise True</returns>
        protected override bool CanUnload() {
            //return false to ~cancel~ Application close
            return true;
        }

        protected override bool Initialize() {
            _tocToken = TOCSelectionChangedEvent.Subscribe(OnTOCSelectionChanged);

            return base.Initialize();
        }
        protected override void Uninitialize() {
            TOCSelectionChangedEvent.Unsubscribe(_tocToken);
        }
        #endregion Overrides

        /// <summary>
        /// When the user selects a new TOC item, determine whether it's usable as a source for agent track analysis.
        /// </summary>
        /// <param name="obj">ArcGIS Pro map view reference</param>
        private void OnTOCSelectionChanged(MapViewEventArgs obj) {
            // TODO Determine which agent tracks aren't deselected and remove their viewsheds (if needed)
            // TODO Determine which agent tracks are selected and add viewsheds (if needed)
            QueuedTask.Run(async () => {
            bool isAgentTrackFeatLyrSelected = false;
            bool areOnlyAgentTrackGraphicsLyrsSelected = false;

            _agentTracksFeatureLayer = null;

            try {
                ClearExistingViewsheds();

                    if (obj != null && obj.MapView != null && obj.MapView.GetSelectedLayers() != null) {

                        areOnlyAgentTrackGraphicsLyrsSelected = obj.MapView.GetSelectedLayers().Count > 0;
                        IReadOnlyList<Layer> lyrs = obj.MapView.GetSelectedLayers();

                        // 1. Look for add-on feature enablement conditions in the selected layer list
                        foreach (Layer lyr in lyrs) {
                            // Only agent track result graphics layers selected?
                            // Unfortunately, we can only search by graphic layer type and layer name
                            if (IsAgentTracksGraphicsLayer(lyr)) {
                                _dctGLViewshed.Add(lyr as GraphicsLayer, null);
                            }
                            areOnlyAgentTrackGraphicsLyrsSelected &= IsAgentTracksGraphicsLayer(lyr);

                            // Look for one that has all the characteristics of a Mission agent tracks layer
                            FeatureLayer lyrFound = await extractAgentTracksFeatureLayer(lyr);
                            if (_agentTracksFeatureLayer == null && lyrFound != null) {
                                _agentTracksFeatureLayer = lyrFound;
                                isAgentTrackFeatLyrSelected = true;
                                //break;
                            }
                        }
                    }
                } finally {

                    // 2. Enable conditions/take other actions based on what we found about the selected layers list
                    if (areOnlyAgentTrackGraphicsLyrsSelected) {
                        FrameworkApplication.State.Activate("onlyAgentTrackGraphicsLyrsAreSelected_state");
                    } else {
                        FrameworkApplication.State.Deactivate("onlyAgentTrackGraphicsLyrsAreSelected_state");
                    }

                    if (isAgentTrackFeatLyrSelected) {
                        FrameworkApplication.State.Activate("trackFeatureLayerSelected_state");
                        populateAgentList();
                    } else {
                        FrameworkApplication.State.Deactivate("trackFeatureLayerSelected_state");
                        _agentList.Clear();
                    }
                }
            });

            /**
             * <summary>Find and return a layer matching specs for Agent Tracks layer. Return null if not found.</summary>
             */
            async Task<FeatureLayer> extractAgentTracksFeatureLayer(Layer lyr) {
                FeatureLayer lyrFound = null;
                if (lyr is FeatureLayer) {
                    bool isPointGeom, isJoinedTable, hasDataRows, hasUserField, hasCreateDateField;

                    FeatureLayer featLyr = (FeatureLayer)lyr;
                    isPointGeom = featLyr.ShapeType == esriGeometryType.esriGeometryPoint;
                    if (isPointGeom) await QueuedTask.Run(() => {
                        try {
                            FeatureClass fc = featLyr.GetFeatureClass();
                            isJoinedTable = fc.IsJoinedTable();
                            hasDataRows = fc.GetCount() > 0;
                            if (!isJoinedTable && hasDataRows) {
                                // Note: due to Editor Tracking, we couldn't set up an empty demo mission and populated it from
                                // archived tracks, as we wanted to. To worka round the problem *ONLY FOR DEMO PURPOSES*, we
                                // created and extra field in the tracks layer called created_user_demo_only. 
                                // Here we look to see whether that demo-only field exists, and use it as the user name if it does.
                                // Check to see which is the track creator field: user_created or user_created_demo_only
                                List<FieldDescription> fields = featLyr.GetFieldDescriptions();
                                bool hasDemoAgentField = fields.Any((fieldDesc) => {
                                    return fieldDesc.Name == FIELD_AUX_AGENTNAME_DEMO_USE_ONLY && fieldDesc.Type == FieldType.String;
                                });
                                string agentNameField = hasDemoAgentField ? FIELD_AUX_AGENTNAME_DEMO_USE_ONLY : FIELD_AGENTNAME;

                                hasUserField = fields.Any((fld => fld.Name == agentNameField && fld.Type == FieldType.String));
                                hasCreateDateField = fields.Any((fld => fld.Name == FIELD_TIMESTAMP && fld.Type == FieldType.Date));

                                if (hasUserField && hasCreateDateField) {
                                    lyrFound = featLyr;
                                    _field_agentName = agentNameField;
                                }
                            }
                        } catch (Exception e) {
                            System.Diagnostics.Debug.Write($"Error while examining feature layer '{featLyr.Name}': {e.Message}");
                        }
                    });
                } else if (lyr is GroupLayer) { // recursion for grouped layers
                    foreach (Layer groupedLyr in ((GroupLayer)lyr).Layers) {
                        try {
                            Task<FeatureLayer> groupedLyrResult = extractAgentTracksFeatureLayer(groupedLyr);
                            await groupedLyrResult;
                            if (groupedLyrResult.Result != null) {
                                lyrFound = groupedLyrResult.Result;
                                break;
                            }
                        } catch (Exception e) {
                            System.Diagnostics.Debug.Write($"Error while examining group layer '{lyr.Name}': {e.Message}");
                        }
                    } // else return null because nothing was found

                }
                return lyrFound;
            }
            void populateAgentList() {
                if (_agentTracksFeatureLayer == null) return;
                _agentList.Clear();
                QueuedTask.Run(() => {
                    QueryFilter _agentListQueryFilter = new QueryFilter() {
                        PrefixClause = "DISTINCT",
                        SubFields = _field_agentName
                    };

                    // Query the feature *layer* to take into account any query definitions on the layer
                    using (RowCursor rowCur = _agentTracksFeatureLayer.Search(_agentListQueryFilter)) {
                        while (rowCur.MoveNext()) {
                            using (Row row = rowCur.Current) {
                                _agentList.Add(row[_field_agentName].ToString()); // Assumes only one field in the query filter
                            }
                        }
                    }
                    // Raise event to notify children or clients that the list has changed
                    NotifyPropertyChanged(PROP_AGENTLIST);
                });
            }
        }

        /**
         * Logic that runs when an agent is selected from the dropdown list. This creates graphics tracks for that agent.
         */
        private static void OnAgentSelected(string agentName) {
            ProgressorSource ps = new ProgressorSource("Finding agent path...", false);
            QueuedTask.Run<object>(() => {
                CIMSymbolReference symbolRef = ArrowSym().MakeSymbolReference();

                // Get tracks for agent, sorted by datetime
                QueryFilter agentTracksQF = new() {
                    PostfixClause = $"ORDER BY {FIELD_TIMESTAMP}",
                    WhereClause = $"{_field_agentName} = '{agentName}'"
                };

                // Daml conditions should guard against this code running outside a map or scene view
                Map map = MapView.Active.Map;
                if (map.MapType == MapType.Map || map.MapType == MapType.Scene) {

                    // Create polylines between tracks, symbolized with arrows
                    using (RowCursor rowCur = _agentTracksFeatureLayer.Search(agentTracksQF)) {
                        // Use timestamp rather than createDate because it seems to be less affected by device time differences
                        MapPoint prevPt = null; double? prevCourse = null; DateTime? prevPtTimestamp = null;
                        IList<CIMLineGraphic> pathGraphics = new List<CIMLineGraphic>();
                        CIMPointGraphic startGraphic = null, endGraphic = null;

                        while (rowCur.MoveNext()) {

                            using (Feature feat = (Feature)rowCur.Current) {
                                MapPoint pt = (MapPoint)feat.GetShape();
                                double? currCourse = (double?)feat[ATTR_COURSE];


                                // UNDONE Some tracks need two button clicks to advance. This isn't a code problem; duplicate tracks somehow often make it into the feature class. Unknown why.
                                // Workaround to detect duplicate points that somehow strangely make it into collected tracks data
                                // Strangely, they're a few milliseconds apart, so using the string representation lets us filter them out the way we want
                                bool isDuplicatePoint = pt.X == prevPt?.X && pt.Y == prevPt?.Y &&
                                    (feat[FIELD_TIMESTAMP] as DateTime?).ToString() == prevPtTimestamp?.ToString();

                                if (prevPt == null) { // Create start graphic
                                    startGraphic = CreateAgentStartGraphic(pt);
                                } else if (!isDuplicatePoint) { // Create a linking graphic
                                    CIMLineGraphic graphic = CreateAgentTrackLinkGraphic(feat, prevPt, pt, symbolRef);

                                    if (graphic.Attributes == null) graphic.Attributes = new Dictionary<string, object>();
                                    graphic.Attributes.Add(ATTR_HEADING_AT_START, prevCourse);
                                    graphic.Attributes.Add(ATTR_HEADING_AT_END, currCourse);

                                    pathGraphics.Add(graphic);
                                }
                                prevPt = pt; prevCourse = currCourse; prevPtTimestamp = feat[FIELD_TIMESTAMP] as DateTime?;
                            }
                        }
                        System.Diagnostics.Debug.WriteLine($"{pathGraphics.Count} lines created");

                        if (prevPt != null) { // Create end graphic
                            endGraphic = CreateAgentEndGraphic(prevPt);
                        }

                        // Now that we have our graphics, we need a graphics layer. If it already exists, clear and reuse it; otherwise, create one.
                        GraphicsLayer graphicsLayer = null;

                        string travelDist = pathGraphics.AgentTravelDistanceMeters().ToString("F2");
                        string graphicsLayerName = $"{AGENTTRACKLYRNAME_PREAMBLE}{agentName} ({travelDist} m)";
                        IReadOnlyList<Layer> layers = map.FindLayers(graphicsLayerName, true);
                        if (layers.Count > 0 && layers.FirstOrDefault() is GraphicsLayer) { // Use the first one found
                            graphicsLayer = (GraphicsLayer)layers.FirstOrDefault();
                            graphicsLayer.RemoveElements();
                        } else {
                            // Create graphics overlay
                            // Unfortunately, the layer gets automatically selected and there doesn't seem to be a way to change that...
                            GraphicsLayerCreationParams gl_param = new GraphicsLayerCreationParams() { Name = graphicsLayerName };
                            graphicsLayer = LayerFactory.Instance.CreateLayer<GraphicsLayer>(gl_param, map);
                        }
                        foreach (CIMLineGraphic graphic in pathGraphics) {
                            GraphicElement elt = graphicsLayer?.AddElement(graphic);
                            int ptCt = (elt.GetGraphic() as CIMLineGraphic).Line.PointCount;
                            if (graphic.Attributes.ContainsKey(ATTR_HEADING_AT_START))
                                elt.SetCustomProperty(ATTR_HEADING_AT_START, graphic.Attributes[ATTR_HEADING_AT_START].ToString());
                            if (graphic.Attributes.ContainsKey(ATTR_HEADING_AT_END))
                                elt.SetCustomProperty(ATTR_HEADING_AT_END, graphic.Attributes[ATTR_HEADING_AT_END].ToString());
                        }

                        // Attributes improperly nulled in graphic elements added to graphics layer. * Fixed in 2.7 as long as attributes don't include Shape *
                        // Need to use GraphicElement CustomProperties as a workaround.
                        graphicsLayer?.AddElement(startGraphic); Element lastElt = graphicsLayer?.AddElement(endGraphic);

                        graphicsLayer?.SetVisibility(true);
                        // NOTE: Crash upon exiting Pro if the following line is run with a null or empty parameter:
                        //graphicsLayer?.UnSelectElements(new List<Element>() { lastElt });
                        // Here's the workaround:
                        graphicsLayer?.ClearSelection();
                    }
                }
                return null;
            }, ps.Progressor);

            CIMPointGraphic CreateAgentStartGraphic(MapPoint pt) {
                CIMPointGraphic start = new CIMPointGraphic() {
                    Location = pt,
                    Symbol = AgentStartSymbol.MakeSymbolReference()
                };
                return start;
            }
            CIMPointGraphic CreateAgentEndGraphic(MapPoint pt) {
                CIMPointGraphic end = new CIMPointGraphic() {
                    Location = pt,
                    Symbol = AgentEndSymbol.MakeSymbolReference()
                };
                return end;
            }
            CIMLineSymbol ArrowSym() {
                Random rand = new Random();
                double r = Math.Floor(rand.NextDouble() * 256.0); double g = Math.Floor(rand.NextDouble() * 256.0); double b = Math.Floor(rand.NextDouble() * 256.0);
                CIMColor color = ColorFactory.Instance.CreateRGBColor(r, g, b);
                /*CIMMarker markerTriangle = SymbolFactory.Instance.ConstructMarker(color, 8, SimpleMarkerStyle.Triangle);
                markerTriangle.Rotation = -90; // or -90
                markerTriangle.MarkerPlacement = new CIMMarkerPlacementOnLine() { AngleToLine = true, RelativeTo = PlacementOnLineRelativeTo.LineEnd };*/
                var lineSymbolWithArrow = new CIMLineSymbol() {
                    SymbolLayers = new CIMSymbolLayer[] {
                        //markerTriangle,
                        //SymbolFactory.Instance.ConstructStroke(color, 2),
                        new CIMSolidStroke() {
                            Color = color,
                            Width = 3,
                            CapStyle = LineCapStyle.Square,
                            JoinStyle = LineJoinStyle.Miter,
                            MiterLimit = 5,
                            LineStyle3D = Simple3DLineStyle.Tube,
                            Enable = true,
                            Effects = new CIMGeometricEffect[] {
                                new CIMGeometricEffectArrow() {
                                    ArrowType = GeometricEffectArrowType.Block,
                                    Width = 3
                                },
                                new CIMGeometricEffectScale() {
                                    XScaleFactor = 0.90,
                                    YScaleFactor = 0.90
                                }
                            }
                        }
                    }
                };
                return lineSymbolWithArrow;
            }
            CIMLineGraphic CreateAgentTrackLinkGraphic(Feature feat, MapPoint ptStart, MapPoint ptEnd, CIMSymbolReference symbolRef) {
                CIMLineGraphic link = new CIMLineGraphic() {
                    Line = PolylineBuilder.CreatePolyline(new List<MapPoint>() { ptStart, ptEnd }),
                    Symbol = symbolRef
                };
                // Fill in attributes from feature
                link.Attributes = new Dictionary<string, object>();
                foreach (Field fld in feat.GetFields()) {
                    // TODO Find out if any other field types than Geometry cause problems when adding a graphic to a graphics layer
                    if (fld.FieldType != FieldType.Geometry)
                        link.Attributes.Add(fld.Name, feat[fld.Name]);
                }
                return link;
            }
        }

        /// <summary>
        /// Handler for Select Agent Tracks dataset button click
        /// </summary>
        internal static async void OnAgentTracksBrowseButtonClick() {
            bool isDemoMode = false; // In certain demo circumstances, we'll use hardcoded choices
            ProgressorSource ps = new ProgressorSource("Finding avaiable Missions...", /*"Mission search canceled",*/ true);

            var lstMissions = await QueuedTask.Run(async () => {
                //IEnumerable<MissionTracksItem> listItems = new List<MissionTracksItem>();
                IList<MissionItemDetails> missions = new List<MissionItemDetails>();

                ps.Message = "Checking active portal";
                ArcGISPortal portal = ArcGISPortalManager.Current.GetActivePortal();
                ps.Message = $"Connecting to {portal.PortalUri}";
                if (!portal.IsSignedOn()) {
                    MessageBox.Show("You must be signed into a portal for this. Please do so before trying to open a Mission.");
                    return missions;
                }

                ps.Message = "Finding available Missions...";


                /// Find important information about a Mission.
                /// This isn't straightforward, because the name given a Mission during setup isn't the same as the title the Mission item gets.
                /// Here we search the user's groups to find which ones contain Mission items; then look for associated items and info within that group.

                // Get user's groups
                IReadOnlyList<PortalGroup> groups = await portal.GetGroupsFromUserAsync(portal.GetSignOnUsername());
                foreach (PortalGroup group in groups) {
                    IEnumerable<Item> items = group.GetItems();
                    // Mission, Map, Tracks features exist?
                    Func<Item, bool> qryMission = itm => itm.Type == "Mission";
                    Func<Item, bool> qryWebMap = itm => itm.Type == "Web Map";
                    Func<Item, bool> qryTracks = itm => itm.Type == "Feature Service" && itm.Title.StartsWith("Tracks_");
                    if (items.Any(qryMission) && items.Any(qryWebMap) && items.Any(qryTracks)) {

                        // Find the Mission, Mission Map, and Tracks feature service in this group
                        PortalItem mission = items.Where(qryMission).FirstOrDefault() as PortalItem;
                        PortalItem map = items.Where(qryWebMap).FirstOrDefault() as PortalItem;
                        PortalItem tracks = items.Where(qryTracks).FirstOrDefault() as PortalItem;

                        // Only the Folder and the Map have the Mission's name, and we don't always get access to the Folder
                        // We need to remove extraneous text from the front of the Map description first, though...
                        const String MAP_DESC_PREAMBLE = "A map for mission "; // What to look for in the Mission Map Description
                        int mapDescPreambleEndLoc = map.Description.IndexOf(MAP_DESC_PREAMBLE) + MAP_DESC_PREAMBLE.Length;
                        String missionName = map.Description.Substring(mapDescPreambleEndLoc, map.Description.Length - mapDescPreambleEndLoc);

                        MissionItemDetails missionDetails = new MissionItemDetails() {
                            MissionName = missionName,
                            Group = group,
                            MissionItem = mission,
                            TracksItem = tracks
                        };

                        if (!missions.Contains(missionDetails)) missions.Add(missionDetails);
                    }
                }
                // Now we should have all missions available
                System.Diagnostics.Debug.WriteLine($"{missions.Count} missions found");

                return missions;

            }, ps.Progressor);

            // If there was a problem enumerating Missions, don't show a chooser dialog
            if (lstMissions.Count() <= 0) {
                MessageBox.Show("No available Missions could be found.");
                return;
            }

            // Pass Mission items list to list dialog and show it
            DlgChooseMission dlg = new DlgChooseMission(lstMissions, isDemoMode);
            bool? result = dlg.ShowDialog();
            if (result ?? false) {
                // Do something with chosen item
                MissionItemDetails item = dlg.SelectedItem;
                await QueuedTask.Run(() => {
                    var layerParams = new FeatureLayerCreationParams(item.TracksItem) {
                        Name = item.MissionName
                    };
                    LayerFactory.Instance.CreateLayer<FeatureLayer>(layerParams, MapView.Active.Map);
                });
            }
            //else canceled

        }



        #region LOS Toolset

        private const double HORIZ_ANGLE = 120;
        private const double VERT_ANGLE = 30;
        private const double MIN_DIST = 1;
        private const double MAX_DIST = 75;
        private static Dictionary<GraphicsLayer, TimeSequencingViewshed> _dctGLViewshed = new Dictionary<GraphicsLayer, TimeSequencingViewshed>();

        private bool IsAgentTracksGraphicsLayer(Layer lyr) {
            return (lyr is GraphicsLayer && lyr.Name.StartsWith(AGENTTRACKLYRNAME_PREAMBLE));
        }

        internal static void OnAgentTrackViewshedNextButtonClick() {
            foreach (GraphicsLayer lyr in _dctGLViewshed.Keys.ToList()) {
                TimeSequencingViewshed tsv = _dctGLViewshed[lyr];
                if (tsv == null) tsv = InitializeViewshedAndViewpoints(lyr);
                try {
                    tsv?.ShowNext();
                } catch (TimeSequencingViewshedInvalidException e) {
                    // The viewshed has probably been removed through the GUI. But we can recreate it.
                    TimeSequencingViewshed newViewshed = new TimeSequencingViewshed(e.Viewpoints, e.CurrentViewpointIndex, VERT_ANGLE,
                        HORIZ_ANGLE, MIN_DIST, MAX_DIST);
                    MapView mapView = MapView.Active;
                    mapView?.RemoveExploratoryAnalysisAsync(tsv); 
                    tsv.Dispose();
                    tsv = newViewshed;
                    mapView?.AddExploratoryAnalysisAsync(tsv);
                }
                _dctGLViewshed[lyr] = tsv;
            }
        }
        internal static void OnAgentTrackViewshedPrevButtonClick() {
            foreach (GraphicsLayer lyr in _dctGLViewshed.Keys.ToList()) {
                TimeSequencingViewshed tsv = _dctGLViewshed[lyr];
                if (tsv == null) tsv = InitializeViewshedAndViewpoints(lyr);
                try {
                    tsv?.ShowPrev();
                } catch (TimeSequencingViewshedInvalidException e) {
                    // The viewshed has probably been removed through the GUI. But we can recreate it.
                    TimeSequencingViewshed newViewshed = new TimeSequencingViewshed(e.Viewpoints, e.CurrentViewpointIndex, VERT_ANGLE,
                        HORIZ_ANGLE, MIN_DIST, MAX_DIST);
                    MapView mapView = MapView.Active;
                    mapView?.RemoveExploratoryAnalysisAsync(tsv);
                    tsv.Dispose();
                    tsv = newViewshed;
                    mapView?.AddExploratoryAnalysisAsync(tsv);
                }
                _dctGLViewshed[lyr] = tsv;
            }

        }

        internal static void OnAgentTrackAnimateViewshedButtonClick() {
            const string CHECKED_STATE = "sequencingViewshedRunning_state";

            //foreach (GraphicsLayer glyr in _dctGLViewshed.Keys.ToList()) {
            if (FrameworkApplication.State.Contains(CHECKED_STATE)) { // Stop viewshed
                FrameworkApplication.State.Deactivate(CHECKED_STATE);
                foreach (TimeSequencingViewshed tsv in _dctGLViewshed.Values.ToList()) {
                    tsv.Stop();
                }
            } else { // Start viewshed
                FrameworkApplication.State.Activate(CHECKED_STATE);
                foreach (GraphicsLayer glyr in _dctGLViewshed.Keys.ToList()) {
                    if (_dctGLViewshed[glyr] == null) {
                        TimeSequencingViewshed newViewshed = InitializeViewshedAndViewpoints(glyr);
                        _dctGLViewshed[glyr] = newViewshed;
                    } else if (!_dctGLViewshed[glyr].IsValidAnalysisLayer) {
                        // Has the viewshed been removed through other GUI means and is now invalid?
                        // We could just recreate everything from scratch, but here we save time by reusing previously calculated viewpoints
                        TimeSequencingViewshed tsvInvalid = _dctGLViewshed[glyr];
                        TimeSequencingViewshed newViewshed = new TimeSequencingViewshed(tsvInvalid.Viewpoints, tsvInvalid.ViewpointIndex, VERT_ANGLE,
                            HORIZ_ANGLE, MIN_DIST, MAX_DIST);
                        MapView mapView = MapView.Active;
                        //mapView?.RemoveExploratoryAnalysis(tsvInvalid);
                        mapView?.RemoveExploratoryAnalysisAsync(tsvInvalid); 
                        tsvInvalid.Dispose();
                        mapView?.AddExploratoryAnalysisAsync(newViewshed);
                        _dctGLViewshed[glyr] = newViewshed;
                    }
                    // And finally, start the sequence
                    _dctGLViewshed[glyr]?.Start();
                }
            }
        }

        /// <summary>
        /// Clears any existing Viewshed analyses from the active map. Clears out the data structure linking
        /// viewsheds to their agent track graphic layers.
        /// </summary>
        private static void ClearExistingViewsheds() {
            foreach (TimeSequencingViewshed tsv in _dctGLViewshed.Values) {
                if (tsv != null) {
                    //MapView.Active.RemoveExploratoryAnalysis(tsv);
                    MapView.Active.RemoveExploratoryAnalysisAsync(tsv);
                    tsv.Dispose();
                }
            }
            _dctGLViewshed.Clear();
        }

        /// <summary>
        /// This creates the viewshed analysis layer and calculates the list of track viewpoints, if needed.
        /// This is intended to be a first-time initialization; it's different from what happens if a
        /// viewshed was manually removed and then found to be missing.
        /// </summary>
        private static TimeSequencingViewshed InitializeViewshedAndViewpoints(GraphicsLayer glyr) {
            Task<TimeSequencingViewshed> tskTsv = QueuedTask.Run(() => {
                //Create placeholder camera for now
                //Camera cam = new Camera(0, 0, 0, 0, 0, SpatialReferences.WebMercator);
                TimeSequencingViewshed tsv = new TimeSequencingViewshed(MapView.Active.Map.SpatialReference, VERT_ANGLE, HORIZ_ANGLE, MIN_DIST, MAX_DIST);
                MapView.Active?.AddExploratoryAnalysisAsync(tsv);

                if (tsv?.Viewpoints == null) BuildViewpoints(glyr, tsv);
                return tsv;
            });
            tskTsv.Wait();
            return tskTsv.Result;

            async void BuildViewpoints(GraphicsLayer glyrBV, TimeSequencingViewshed tsvBV) {
                if (tsvBV == null) throw new InvalidOperationException("Viewshed cannot be null when building viewpoints");
                MapView mapView = MapView.Active;
                SpatialReference sr = mapView.Map.SpatialReference;
                IReadOnlyCollection<ExploratoryAnalysis> analyses = mapView?.GetExploratoryAnalysisCollection();
                foreach (ExploratoryAnalysis analysis in analyses) {
                    if (analysis is TimeSequencingViewshed) {
                        tsvBV = (TimeSequencingViewshed)analysis;
                        break;
                    }
                }
                // Now set observer points 
                // TODO If more than one agent track layer selected, want multiple viewsheds in selected layers
                // Because this button can only be clicked if a valid layer is selected, we don't need to do any searching

                await QueuedTask.Run(() => {
                    IReadOnlyList<GraphicElement> graphics = glyrBV.GetElementsAsFlattenedList();
                    List<TSVViewpoint> viewpoints = new List<TSVViewpoint>();

                    for (int idx = 0; idx < graphics.Count; idx++) {
                        GraphicElement gelt = graphics[idx];
                        // Don't deal with start or end point graphics
                        if (!(gelt.GetGraphic() is CIMLineGraphic)) continue;

                        CIMLineGraphic lineGraphic = (CIMLineGraphic)gelt.GetGraphic();
                        Polyline line = lineGraphic.Line;

                        // Graphics with no points should no longer be added (see OnAgentSelected() above).
                        // However, leaving this line in as a safety check.
                        if (line.PointCount <= 0) continue;

                        // Generally add the end point of the line as a viewshed spot, but make sure to also add the very starting point
                        if (gelt == graphics.First() && !String.IsNullOrEmpty(gelt.GetCustomProperty(ATTR_HEADING_AT_START))) {
                            MapPoint ptFirst = line.Points.First();
                            Camera cam1 = ConstructCamera(ptFirst, sr, Double.Parse(gelt.GetCustomProperty(ATTR_HEADING_AT_START)));
                            DateTime dt = graphicElementDateTime(gelt);
                            viewpoints.Add(new TSVViewpoint(dt, cam1));
                        }
                        // Add the endpoint as viewshed camera
                        MapPoint pt = line.Points.Last();
                        Camera cam = ConstructCamera(pt, sr, Double.Parse(gelt.GetCustomProperty(ATTR_HEADING_AT_END)));
                        DateTime datetime = graphicElementDateTime(gelt);
                        viewpoints.Add(new TSVViewpoint(datetime, cam));
                    }
                    tsvBV.Viewpoints = viewpoints;

                    Camera ConstructCamera(MapPoint pt, ArcGIS.Core.Geometry.SpatialReference srMap, double heading) {
                        MapPoint ptProj = (MapPoint)GeometryEngine.Instance.Project(pt, srMap);

                        // Heading adjust to -180 - 180; see https://pro.arcgis.com/en/pro-app/sdk/api-reference/#topic11449.html
                        double fixedHeading = heading;
                        if (fixedHeading == 360)
                            fixedHeading = 0;
                        else if ((int)(-fixedHeading / -180) == 0) fixedHeading = -fixedHeading % -180;
                        else fixedHeading = (-fixedHeading % -180) + 180;

                        Camera cam = new Camera(ptProj.X, ptProj.Y, ptProj.Z, -10, fixedHeading, srMap, CameraViewpoint.LookFrom);
                        return cam;
                    }
                    /**
                     * <summary>Strangely, datetime fields are extracted as .NET DateTime type when the tracks graphics are first generated...
                     * ...but are long (millisecond) epoch values when persisted in the Pro project document. Check for both conditions.</summary>
                     **/
                    DateTime graphicElementDateTime(GraphicElement gelt) {
                        var datetimeAttr = gelt.GetGraphic().Attributes[FIELD_TIMESTAMP];
                        DateTime datetime = datetimeAttr is long
                            ? DateTimeOffset.FromUnixTimeMilliseconds((long)datetimeAttr).DateTime.ToLocalTime()
                            : (DateTime)datetimeAttr;
                        return datetime;
                    }
                });
            }
        }

        #endregion

        #region Properties
        private static CIMPointSymbol _agentStartSymbol;
        private static CIMPointSymbol AgentStartSymbol {
            get {
                if (_agentStartSymbol == null) {
                    _agentStartSymbol = SymbolFactory.Instance.ConstructPointSymbol(ColorFactory.Instance.GreenRGB, 16, SimpleMarkerStyle.Circle);
                    //markerTriangle.Rotation = -90; // or -90
                }
                return _agentStartSymbol;
            }
        }

        private static CIMPointSymbol _agentEndSymbol;
        private static CIMPointSymbol AgentEndSymbol {
            get {
                if (_agentEndSymbol == null) {
                    _agentEndSymbol = SymbolFactory.Instance.ConstructPointSymbol(ColorFactory.Instance.RedRGB, 16, SimpleMarkerStyle.Square);
                }
                return _agentEndSymbol;
            }
        }

        public static string PROP_AGENTLIST = "Agent List";
        private static StringCollection _agentList = new StringCollection();
        public static StringCollection AgentList {
            get {
                return _agentList;
            }
        }

        private static string _selectedAgentName;
        public static string SelectedAgentName {
            get {
                return _selectedAgentName;
            }
            set {
                _selectedAgentName = value;
                // Do something with the agent name
                OnAgentSelected(value);
            }
        }

        #endregion
    }
}
