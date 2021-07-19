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
using MissionAgentReview.datatypes;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace MissionAgentReview {
    /// <summary>
    /// Interaction logic for DlgChooseMission.xaml
    /// </summary>
    public partial class DlgChooseMission : ArcGIS.Desktop.Framework.Controls.ProWindow {
        private MissionItemDetails _selectedItem;
        private IList<MissionItemDetails> _missionItems;

        internal DlgChooseMission(IEnumerable<MissionItemDetails> items, bool isDemoMode = false) {
            InitializeComponent();

            lstMissions.ItemsSource = items;

            if (isDemoMode) Title += " [DM]";
        }

        public MissionItemDetails SelectedItem { 
            get => _selectedItem;
            set {
                _selectedItem = value;
                btnOK.IsEnabled = SelectedItem != null;
            }
        }

        public IList<MissionItemDetails> MissionItems { get => _missionItems; set => _missionItems = value; }

        private void OK_Click(object sender, RoutedEventArgs e) {
            // Set selected item
            this.DialogResult = true;
        }

        private void LstMissions_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            SelectedItem = (MissionItemDetails)e.AddedItems[0];
        }

        #region Column Sorting

        // Thanks to MSDN: https://docs.microsoft.com/en-us/dotnet/desktop/wpf/controls/how-to-sort-a-gridview-column-when-a-header-is-clicked?view=netframeworkdesktop-4.8
        GridViewColumnHeader _lastHeaderClicked = null;
        ListSortDirection _lastDirection = ListSortDirection.Ascending;
        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e) {
            if (e.OriginalSource is GridViewColumnHeader headerClicked) { 
            ListSortDirection direction;

                if (headerClicked.Role != GridViewColumnHeaderRole.Padding) {
                    if (headerClicked != _lastHeaderClicked) {
                        direction = ListSortDirection.Ascending;
                    } else {
                        if (_lastDirection == ListSortDirection.Ascending) {
                            direction = ListSortDirection.Descending;
                        } else {
                            direction = ListSortDirection.Ascending;
                        }
                    }

                    var columnBinding = headerClicked.Column.DisplayMemberBinding as Binding;
                    var sortBy = columnBinding?.Path.Path ?? headerClicked.Column.Header as string;

                    Sort(sortBy, direction);

 /*                   if (direction == ListSortDirection.Ascending) {
                        headerClicked.Column.HeaderTemplate =
                          Resources["HeaderTemplateArrowUp"] as DataTemplate;
                    } else {
                        headerClicked.Column.HeaderTemplate =
                          Resources["HeaderTemplateArrowDown"] as DataTemplate;
                    }

                    // Remove arrow from previously sorted header
                    if (_lastHeaderClicked != null && _lastHeaderClicked != headerClicked) {
                        _lastHeaderClicked.Column.HeaderTemplate = null;
                    }*/

                    _lastHeaderClicked = headerClicked;
                    _lastDirection = direction;
                }
            }
        }
        private void Sort(string sortBy, ListSortDirection direction) {
            ICollectionView dataView =
              CollectionViewSource.GetDefaultView(lstMissions.ItemsSource);

            dataView.SortDescriptions.Clear();
            SortDescription sd = new SortDescription(sortBy, direction);
            dataView.SortDescriptions.Add(sd);
            dataView.Refresh();
        }
    }
    #endregion
}
