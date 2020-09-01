using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Events;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using MissionAgentReview.Exceptions;
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
        private QueryFilter _agentListQueryFilter = new QueryFilter() {
            PrefixClause = "DISTINCT",
            SubFields = FIELD_AGENTNAME
        };
        private const string FIELD_AGENTNAME = "created_user";
        private const string FIELD_CREATEDATETIME = "created_date";
        private const string AGENTTRACKLYRNAME_PREAMBLE = "Agent Path: ";
        private FeatureLayer _agentTracksFeatureLayer;

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
            //TODO - add your business logic
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
            bool isAgentTrackFeatLyrSelected = false;
            bool areOnlyAgentTrackGraphicsLyrsSelected = true;

            MapView mv = obj.MapView;
            _agentTracksFeatureLayer = null;

            QueuedTask.Run(() => {
                // 1. Look for add-on feature enablement conditions in the selected layer list
                foreach (Layer lyr in mv.GetSelectedLayers()) {
                    // Only agent track result graphics layers selected?
                    // Unfortunately, we can only search by graphic layer type and layer name
                    areOnlyAgentTrackGraphicsLyrsSelected &= isAgentTracksGraphicsLayer(lyr);

                    // look for one that has all the characteristics of a Mission agent tracks layer
                    Task<FeatureLayer> lyrFound = isAgentTracksFeatureLayer(lyr);
                    if (_agentTracksFeatureLayer == null && lyrFound.Result != null) {
                        _agentTracksFeatureLayer = lyrFound.Result;
                        isAgentTrackFeatLyrSelected = true;
                        //break;
                    }
                }
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
            });

            //throw new NotImplementedException();
            // Find and return a layer matching specs for Agent Tracks layer. Return null if not found.
            async Task<FeatureLayer> isAgentTracksFeatureLayer(Layer lyr) {
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
                                TableDefinition tblDef = fc.GetDefinition();
                                IReadOnlyList<Field> fields = tblDef.GetFields();
                                hasUserField = fields.Any((fld => fld.Name == FIELD_AGENTNAME && fld.FieldType == FieldType.String));
                                hasCreateDateField = fields.Any((fld => fld.Name == FIELD_CREATEDATETIME && fld.FieldType == FieldType.Date));
                                if (hasUserField && hasCreateDateField) lyrFound = featLyr;
                            }
                        } catch (Exception e) {
                            System.Diagnostics.Debug.Write($"Error while examining feature layer '{featLyr.Name}': {e.Message}");
                        }
                    });
                } else if (lyr is GroupLayer) { // recursion for grouped layers
                    foreach (Layer groupedLyr in ((GroupLayer)lyr).Layers) {
                        try {
                            Task<FeatureLayer> groupedLyrResult = isAgentTracksFeatureLayer(groupedLyr);
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
                    FeatureClass fc = _agentTracksFeatureLayer.GetFeatureClass();
                    using (RowCursor rowCur = fc.Search(_agentListQueryFilter)) {
                        while (rowCur.MoveNext()) {
                            using (Row row = rowCur.Current) {
                                _agentList.Add(row[FIELD_AGENTNAME].ToString()); // Assumes only one field in the query filter
                            }
                        }
                    }
                    // Raise event to notify children or clients that the list has changed
                    NotifyPropertyChanged(PROP_AGENTLIST);
                });
            }
        }

        private void OnAgentSelected(string agentName) {
            ProgressorSource ps = new ProgressorSource("Finding agent path...", false);
            QueuedTask.Run<object>(() => {
                CIMSymbolReference symbolRef = ArrowSym().MakeSymbolReference();

                // Get tracks for agent, sorted by datetime
                QueryFilter agentTracksQF = new QueryFilter() {
                    PostfixClause = $"ORDER BY {FIELD_CREATEDATETIME}",
                    WhereClause = $"{FIELD_AGENTNAME} = '{agentName}'"
                };

                Map map = MapView.Active.Map;
                if (map.MapType == MapType.Map || map.MapType == MapType.Scene) {
                    string graphicsLayerName = $"{AGENTTRACKLYRNAME_PREAMBLE}{agentName}";

                    // Create polylines between tracks, symbolized with arrows
                    /* TODO Creating graphics layer adds and selects it, triggering the TOCSelectionChange event and nulling out the _lyrAgentTracks reference.
                     * Have to wait till after this query to create the graphics layer and add elemnts to it. */
                    using (RowCursor rowCur = _agentTracksFeatureLayer.Search(agentTracksQF)) {
                        MapPoint prevPt = null; double? prevCourse = null;
                        List<CIMLineGraphic> graphics = new List<CIMLineGraphic>();
                        CIMPointGraphic startGraphic = null, endGraphic = null;

                        while (rowCur.MoveNext()) {

                            using (Feature feat = (Feature)rowCur.Current) {
                                MapPoint pt = (MapPoint)feat.GetShape();
                                double? currCourse = (double?)feat[ATTR_COURSE];

                                if (prevPt == null) { // Create start graphic
                                    startGraphic = CreateAgentStartGraphic(pt);
                                }

                                if (prevPt != null) { // Create a linking graphic
                                    CIMLineGraphic graphic = CreateAgentTrackLinkGraphic(prevPt, pt, symbolRef);
                                    Dictionary<string, object> attrs = new Dictionary<string, object>(2);
                                    attrs.Add(ATTR_HEADING_AT_START, prevCourse);
                                    attrs.Add(ATTR_HEADING_AT_END, currCourse);

                                    graphic.Attributes = attrs;

                                    graphics.Add(graphic);
                                }
                                prevPt = pt; prevCourse = currCourse;
                            }
                        }
                        System.Diagnostics.Debug.WriteLine($"{graphics.Count} lines created");

                        if (prevPt != null) { // Create end graphic
                            endGraphic = CreateAgentEndGraphic(prevPt);
                        }

                        // Now that we have our graphics, we need a graphics layer. If it already exists, clear and reuse it; otherwise, create one.
                        GraphicsLayer graphicsLayer = null;

                        IReadOnlyList<Layer> layers = map.FindLayers(graphicsLayerName, true);
                        if (layers.Count > 0 && layers.FirstOrDefault() is GraphicsLayer) { // Use the first one found
                            graphicsLayer = (GraphicsLayer)layers.FirstOrDefault();
                            graphicsLayer.RemoveElements();
                        } else {
                            // Create graphics overlay
                            // Unfortunately, the layer gets automatically selected and there doesn't seem to be a way to change that...
                            GraphicsLayerCreationParams gl_param = new GraphicsLayerCreationParams() { Name = graphicsLayerName };
                            graphicsLayer = LayerFactory.Instance.CreateLayer<ArcGIS.Desktop.Mapping.GraphicsLayer>(gl_param, map);
                        }
                        foreach (CIMLineGraphic graphic in graphics) {
                            GraphicElement elt = graphicsLayer?.AddElement(graphic);
                            if (graphic.Attributes.ContainsKey(ATTR_HEADING_AT_START))
                                elt.SetCustomProperty(ATTR_HEADING_AT_START, graphic.Attributes[ATTR_HEADING_AT_START].ToString());
                            if (graphic.Attributes.ContainsKey(ATTR_HEADING_AT_END))
                                elt.SetCustomProperty(ATTR_HEADING_AT_END, graphic.Attributes[ATTR_HEADING_AT_END].ToString());
                         }
                        
                        // Attributes improperly nulled in graphic elements added to graphics layer.
                        // Need to use GraphicElement CustomProperties as a workaround.
                        graphicsLayer?.AddElement(startGraphic); Element lastElt = graphicsLayer?.AddElement(endGraphic);
                        
                        graphicsLayer?.SetVisibility(true);
                        // TODO Crash upon exiting Pro if the following line is run with a null or empty parameter:
                        //graphicsLayer?.UnSelectElements(new List<Element>() { lastElt });
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
            CIMLineGraphic CreateAgentTrackLinkGraphic(MapPoint ptStart, MapPoint ptEnd, CIMSymbolReference symbolRef) {
                CIMLineGraphic link = new CIMLineGraphic() {
                    Line = PolylineBuilder.CreatePolyline(new List<MapPoint>() { ptStart, ptEnd }),
                    Symbol = symbolRef // _agentTrackLinkSymbol.MakeSymbolReference()
                };
                return link;
            }
        }

        #region LOS Toolset

        private bool isAgentTracksGraphicsLayer(Layer lyr) {
            return (lyr is GraphicsLayer && lyr.Name.StartsWith(AGENTTRACKLYRNAME_PREAMBLE));
        }

        internal static void OnAgentTrackViewshedNextButtonClick() {
            if (_viewshed?.Locations == null) BuildViewpoints();
            try {
                _viewshed?.ShowNext();
            } catch (TimeSequencingViewshedInvalidException e) {
                // The viewshed has probably been removed through the GUI. But we can recreate it.
                TimeSequencingViewshed newViewshed = new TimeSequencingViewshed(e.Viewpoints, e.CurrentViewpointIndex, VERT_ANGLE, 
                    HORIZ_ANGLE, MIN_DIST, MAX_DIST);
                MapView mapView = MapView.Active;
                mapView?.RemoveExploratoryAnalysis(_viewshed);
                _viewshed = newViewshed;
                mapView?.AddExploratoryAnalysis(_viewshed);
            }
        }
        internal static void OnAgentTrackViewshedPrevButtonClick() {
            if (_viewshed?.Locations == null) BuildViewpoints();
            try {
                _viewshed?.ShowPrev();
            } catch (TimeSequencingViewshedInvalidException e) {
                // The viewshed has probably been removed through the GUI. But we can recreate it.
                TimeSequencingViewshed newViewshed = new TimeSequencingViewshed(e.Viewpoints, e.CurrentViewpointIndex, VERT_ANGLE,
                    HORIZ_ANGLE, MIN_DIST, MAX_DIST);
                MapView mapView = MapView.Active;
                mapView?.RemoveExploratoryAnalysis(_viewshed);
                _viewshed = newViewshed;
                mapView?.AddExploratoryAnalysis(_viewshed);
            }
        }

        internal static void OnAgentTrackViewshedButtonClick() {
            const string checkedState = "sequencingViewshedRunning_state";

            System.Diagnostics.Debug.WriteLine("Viewshed Button clicked");
            if (FrameworkApplication.State.Contains(checkedState)) { // Stop viewshed
                FrameworkApplication.State.Deactivate(checkedState);
                StopViewshedSequence();
            }
            else { // Start viewshed
                FrameworkApplication.State.Activate(checkedState);
                StartViewshedSequence();
            }
        }

        private const double HORIZ_ANGLE = 120;
        private const double VERT_ANGLE = 30;
        private const double MIN_DIST = 1;
        private const double MAX_DIST = 75;

        private static TimeSequencingViewshed _viewshed = null;
        private static void BuildViewpoints() {
            MapView mapView = MapView.Active;
            ArcGIS.Core.Geometry.SpatialReference sr = mapView.Map.SpatialReference;
            IReadOnlyCollection<ExploratoryAnalysis> analyses = mapView?.GetExploratoryAnalysisCollection();
            foreach (ExploratoryAnalysis analysis in analyses) {
                if (analysis is TimeSequencingViewshed) {
                    _viewshed = (TimeSequencingViewshed)analysis;
                    break;
                }
            }
            if (_viewshed == null) {
                QueuedTask.Run(() => {
                    //Create placeholder camera for now
                    Camera cam = new Camera(0, 0, 0, 0, 0, SpatialReferences.WebMercator);
                    _viewshed = new TimeSequencingViewshed(cam, VERT_ANGLE, HORIZ_ANGLE, MIN_DIST, MAX_DIST);
                    mapView?.AddExploratoryAnalysis(_viewshed);
                }).Wait();
            }
            // Now set observer points 
            // TODO If more than one agent track layer selected, eventually iterate along all agent points in selected layers
            // Because this button can only be clicked if a valid layer is selected, we don't need to do any searching
            GraphicsLayer glyr = (GraphicsLayer)mapView.GetSelectedLayers().FirstOrDefault();

            QueuedTask.Run(() => {
                IReadOnlyList<GraphicElement> graphics = glyr.GetElementsAsFlattenedList();
                List<Camera> viewpoints = new List<Camera>();

                foreach (GraphicElement gelt in graphics) {
                    if (!(gelt.GetGraphic() is CIMLineGraphic)) break;
                    CIMLineGraphic lineGraphic = (CIMLineGraphic)gelt.GetGraphic();
                    Polyline line = lineGraphic.Line;
                    // Generally add the end point of the line as a viewshed spot, but make sure to also add the very starting point
                    if (gelt == graphics.First() && !String.IsNullOrEmpty(gelt.GetCustomProperty(ATTR_HEADING_AT_START))) {
                        MapPoint ptFirst = line.Points.First();
                        Camera cam1 = ConstructCamera(ptFirst, sr, Double.Parse(gelt.GetCustomProperty(ATTR_HEADING_AT_START)));
                        viewpoints.Add(cam1);
                    }
                    // Add the endpoint as viewshed camera
                    MapPoint pt = line.Points.Last();
                    Camera cam = ConstructCamera(pt, sr, Double.Parse(gelt.GetCustomProperty(ATTR_HEADING_AT_END)));
                    viewpoints.Add(cam);
                }
                _viewshed.Locations = viewpoints;

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
            }).Wait();
        }
        private static void StartViewshedSequence() {
            if (_viewshed?.Locations == null) BuildViewpoints();
            // And finally, start the sequence
            _viewshed?.Start();
        }
        private static void StopViewshedSequence() {
            _viewshed?.Stop();
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
                Current.OnAgentSelected(value);
            }
        }

        #endregion
    }
}
