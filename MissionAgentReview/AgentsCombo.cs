using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
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

namespace MissionAgentReview {
    /// <summary>
    /// Represents the ComboBox
    /// </summary>
    internal class AgentsCombo : ComboBox {

        private bool _isInitialized;

        /// <summary>
        /// Combo Box constructor
        /// </summary>
        public AgentsCombo() {
            Module1.Current.PropertyChanged += Module1_PropertyChanged;
            UpdateCombo();
        }

        /// <summary>
        /// Updates the combo box with all the items.
        /// </summary>

        private void UpdateCombo() {
            Clear();
            foreach (string agentName in Module1.AgentList) {
                Add(new ComboBoxItem(agentName));
            }
            _isInitialized = true;


            //Enabled = true; //enables the ComboBox
            //SelectedItem = ItemCollection.FirstOrDefault(); //set the default item in the comboBox

        }

        private void Module1_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            System.Diagnostics.Debug.WriteLine("Property changed");
            if (e.PropertyName == Module1.PROP_AGENTLIST) UpdateCombo();
        }

        #region Overrides
        /// <summary>
        /// The on comboBox selection change event. 
        /// </summary>
        /// <param name="item">The newly selected combo box item</param>
        protected override void OnSelectionChange(ComboBoxItem item) {

            if (item == null)
                return;

            if (string.IsNullOrEmpty(item.Text))
                return;

            
            Module1.SelectedAgentName = item.Text;
        }
        
        
        #endregion

    }
}
