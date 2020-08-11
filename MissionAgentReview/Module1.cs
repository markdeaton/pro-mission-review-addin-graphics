using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Events;
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
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace MissionAgentReview {
    internal class Module1 : Module {
        private static Module1 _this = null;
        private SubscriptionToken _tocToken;
        private QueryFilter _agentListQueryFilter;
        private const string FIELD_AGENTNAME = "created_user";
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
           
            _agentListQueryFilter = new QueryFilter();
            _agentListQueryFilter.PrefixClause = "DISTINCT";
            _agentListQueryFilter.SubFields = FIELD_AGENTNAME;
            
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
                                hasCreateDateField = fields.Any((fld => fld.Name == "created_date" && fld.FieldType == FieldType.Date));
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
                                _agentList.Add(row[row.FindField(FIELD_AGENTNAME)].ToString()); // Assumes only one field in the query filter
                            }
                        }
                    }
                    // Raise event to notify children or clients that the list has changed
                    NotifyPropertyChanged(PROP_AGENTLIST);
                });
            }
        }

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
            }
        }

        #endregion
    }
}
