using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.UtilityNetwork.Trace;
using ArcGIS.Core.Events;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.Internal.CIM;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
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
        private FeatureLayer _lyrAgentTracks;

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
            _lyrAgentTracks = null;

            QueuedTask.Run(() => {
                // 1. Look for add-on feature enablement conditions in the selected layer list
                foreach (Layer lyr in mv.GetSelectedLayers()) {
                    // Only agent track result graphics layers selected?
                    // Unfortunately, we can only search by graphic layer type and layer name
                    areOnlyAgentTrackGraphicsLyrsSelected &=
                        (lyr is GraphicsLayer && lyr.Name.StartsWith(AGENTTRACKLYRNAME_PREAMBLE));

                    // look for one that has all the characteristics of a Mission agent tracks layer
                    Task<FeatureLayer> lyrFound = isAgentTracksLayer(lyr);
                    if (lyrFound.Result != null) {
                        _lyrAgentTracks = lyrFound.Result;
                        isAgentTrackFeatLyrSelected = true;
                        break;
                    }
                }
                // 2. Enable conditions/take other actions based on what we found about the selected layers list
                if (areOnlyAgentTrackGraphicsLyrsSelected) {
                    FrameworkApplication.State.Activate("agentTrackResultsAnalysis_state");
                } else {
                    FrameworkApplication.State.Deactivate("agentTrackResultsAnalysis_state");
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
            async Task<FeatureLayer> isAgentTracksLayer(Layer lyr) {
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
                            Task<FeatureLayer> groupedLyrResult = isAgentTracksLayer(groupedLyr);
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
                if (_lyrAgentTracks == null) return;
                _agentList.Clear();
                QueuedTask.Run(() => {
                    FeatureClass fc = _lyrAgentTracks.GetFeatureClass();
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
                    using (RowCursor rowCur = _lyrAgentTracks.Search(agentTracksQF)) {
                        MapPoint prevPt = null;
                        List<CIMLineGraphic> graphics = new List<CIMLineGraphic>();
                        CIMPointGraphic startGraphic = null, endGraphic = null;

                        while (rowCur.MoveNext()) {

                            using (Feature feat = (Feature)rowCur.Current) {
                                MapPoint pt = (MapPoint)feat.GetShape();

                                if (prevPt == null) { // Create start graphic
                                    startGraphic = CreateAgentStartGraphic(pt);
                                }

                                if (prevPt != null) { // Create a linking graphic
                                    CIMLineGraphic graphic = CreateAgentTrackLinkGraphic(prevPt, pt, symbolRef);
                                    graphics.Add(graphic);
                                }
                                prevPt = pt;
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
                        foreach (CIMLineGraphic graphic in graphics) graphicsLayer?.AddElement(graphic);
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
                CIMMarker markerTriangle = SymbolFactory.Instance.ConstructMarker(color, 8, SimpleMarkerStyle.Triangle);
                markerTriangle.Rotation = -90; // or -90
                markerTriangle.MarkerPlacement = new CIMMarkerPlacementOnLine() { AngleToLine = true, RelativeTo = PlacementOnLineRelativeTo.LineEnd };

                var lineSymbolWithArrow = new CIMLineSymbol() {
                    SymbolLayers = new CIMSymbolLayer[2] {
                            markerTriangle, SymbolFactory.Instance.ConstructStroke(color, 2)
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
        internal static void OnAgentTrackViewshedButtonClick() {
            System.Diagnostics.Debug.WriteLine("Viewshed Button clicked");
        }
        internal static bool CanOnAgentTrackViewshedButtonClick {
            get {
                bool isCanClick = FrameworkApplication.State.Contains("agentTrackResultsAnalysis_state");
                return isCanClick;
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
                Current.OnAgentSelected(value);
            }
        }

        #endregion
    }
}
