using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Core.Events;
using ArcGIS.Desktop.Internal.Mapping.Locate;
using System.Windows;

namespace MissionAgentReview {
    internal class Module1 : Module {
        private static Module1 _this = null;
        private SubscriptionToken _tocToken;


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

        private Layer _lyrAgentTracks;
        private void OnTOCSelectionChanged(MapViewEventArgs obj) {
            MapView mv = obj.MapView;

            QueuedTask.Run(() => {
                foreach (Layer lyr in mv.GetSelectedLayers()) {
                    // look for one that has all the characteristics of a Mission agent tracks layer
                    Task<Layer> lyrFound = isAgentTracksLayer(lyr);
                    if (lyrFound.Result != null) {
                        _lyrAgentTracks = lyrFound.Result;
                        break;
                    }
                }

            });

            //throw new NotImplementedException();
            // Find and return a layer matching specs for Agent Tracks layer. Return null if not found.
            async Task<Layer> isAgentTracksLayer(Layer lyr) {
                Layer lyrFound = null;
                if (lyr is FeatureLayer) {
                    bool isPointGeom, isJoinedTable, hasDataRows, hasUserField, hasCreateDateField;

                    FeatureLayer featLyr = (FeatureLayer)lyr;
                    isPointGeom = featLyr.ShapeType == esriGeometryType.esriGeometryPoint;
                    if (isPointGeom) await QueuedTask.Run(() => {
                        FeatureClass fc = featLyr.GetFeatureClass();
                        isJoinedTable = fc.IsJoinedTable();
                        hasDataRows = fc.GetCount() > 0;
                        if (!isJoinedTable && hasDataRows) {
                            TableDefinition tblDef = fc.GetDefinition();
                            IReadOnlyList<Field> fields = tblDef.GetFields();
                            hasUserField = fields.Any((fld => fld.Name == "created_user" && fld.FieldType == FieldType.String));
                            hasCreateDateField = fields.Any((fld => fld.Name == "created_date" && fld.FieldType == FieldType.Date));
                            if (hasUserField && hasCreateDateField) lyrFound = lyr;
                        }
                    });
                } else if (lyr is GroupLayer) { // recursion for grouped layers
                    foreach (Layer groupedLyr in ((GroupLayer)lyr).Layers) {
                        Task<Layer> groupedLyrResult = isAgentTracksLayer(groupedLyr);
                        await groupedLyrResult;
                        if (groupedLyrResult.Result != null) {
                            lyrFound = groupedLyrResult.Result;
                            break;
                        }
                    } // else return null because nothing was found

                }
                return lyrFound;
            }
        }
    }
}
