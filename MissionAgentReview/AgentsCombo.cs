//   Copyright 2020 Esri
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at

//       http://www.apache.org/licenses/LICENSE-2.0

//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
using ArcGIS.Desktop.Framework.Contracts;
using System.ComponentModel;

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
