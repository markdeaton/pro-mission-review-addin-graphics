﻿using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Events;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.Internal.CIM;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
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
            MapView mv = obj.MapView;
            bool isButtonEnabled = false;
            _lyrAgentTracks = null;

            QueuedTask.Run(() => {
                foreach (Layer lyr in mv.GetSelectedLayers()) {
                    // look for one that has all the characteristics of a Mission agent tracks layer
                    Task<FeatureLayer> lyrFound = isAgentTracksLayer(lyr);
                    if (lyrFound.Result != null) {
                        _lyrAgentTracks = lyrFound.Result;
                        isButtonEnabled = true;
                        break;
                    }
                }
                if (isButtonEnabled) {
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
            QueuedTask.Run(() => {

                // Get tracks for agent, sorted by datetime
                QueryFilter agentTracksQF = new QueryFilter() {
                    PostfixClause = $"ORDER BY {FIELD_CREATEDATETIME}",
                    WhereClause = $"{FIELD_AGENTNAME} = '{agentName}'"
                };
                // Create graphics overlay
                var map = MapView.Active.Map;
                if (map.MapType != MapType.Map)
                    return; // not 2D
                var gl_param = new GraphicsLayerCreationParams { Name = $"Agent Path: {agentName}" };

                // Create polylines between tracks, symbolized with arrows
                /* TODO Creating graphics layer adds and selects it, triggering the TOCSelectionChange event and nulling out the _lyrAgentTracks reference.
                 * Have to wait till after this query to create the graphics layer and add elemnts to it. */
                using (RowCursor rowCur = _lyrAgentTracks.Search(agentTracksQF)) {
                    MapPoint prevPt = null;
                    List<CIMLineGraphic> graphics = new List<CIMLineGraphic>();

                    while (rowCur.MoveNext()) {

                        using (Feature feat = (Feature)rowCur.Current) {
                            MapPoint pt = (MapPoint)feat.GetShape();

                            if (prevPt == null) { // Create start graphic
                                CreateAgentStartGraphic(pt);
                            }

                            if (prevPt != null) { // Create a linking graphic
                                CIMLineGraphic graphic = CreateAgentTrackLinkGraphic(prevPt, pt);
                                graphics.Add(graphic);
                            }
                            prevPt = pt;
                        }
                    }

                    if (prevPt != null) { // Create end graphic
                        CreateAgentEndGraphic(prevPt);
                    }

                    //By default will be added to the top of the TOC
                    GraphicsLayer graphicsLayer = LayerFactory.Instance.CreateLayer<ArcGIS.Desktop.Mapping.GraphicsLayer>(gl_param, map);
                    foreach (CIMLineGraphic graphic in graphics) graphicsLayer.AddElement(graphic);
                }
            });

            void CreateAgentStartGraphic(MapPoint pt) {

            }
            void CreateAgentEndGraphic(MapPoint pt) {

            }
            CIMLineGraphic CreateAgentTrackLinkGraphic(MapPoint ptStart, MapPoint ptEnd) {
                CIMLineGraphic link = new CIMLineGraphic() {
                    Line = PolylineBuilder.CreatePolyline(new List<MapPoint>() { ptStart, ptEnd }),
                    Symbol = ArrowSym().MakeSymbolReference() // _agentTrackLinkSymbol.MakeSymbolReference()
                };
                return link;

                CIMLineSymbol ArrowSym() {
                    var markerTriangle = SymbolFactory.Instance.ConstructMarker(ColorFactory.Instance.RedRGB, 12, SimpleMarkerStyle.Triangle);
                    markerTriangle.Rotation = -90; // or -90
                    markerTriangle.MarkerPlacement = new CIMMarkerPlacementOnLine() { AngleToLine = true, RelativeTo = PlacementOnLineRelativeTo.LineEnd };

                    var lineSymbolWithArrow = new CIMLineSymbol() {
                        SymbolLayers = new CIMSymbolLayer[2] { 
                            markerTriangle, SymbolFactory.Instance.ConstructStroke(ColorFactory.Instance.RedRGB, 2)
                        }
                    };
                    return lineSymbolWithArrow;
                }
            }
        }
        private CIMLineSymbol _agentTrackLinkSymbol = SymbolFactory.Instance.ConstructLineSymbol(
            ColorFactory.Instance.CreateColor(Colors.Gold), 3);

        #region Properties
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
