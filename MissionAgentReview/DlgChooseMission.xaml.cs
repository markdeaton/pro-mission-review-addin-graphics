using MissionAgentReview.datatypes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MissionAgentReview {
    /// <summary>
    /// Interaction logic for DlgChooseMission.xaml
    /// </summary>
    public partial class DlgChooseMission : ArcGIS.Desktop.Framework.Controls.ProWindow {
        private bool _isListItemSelected = false;
        private MissionTracksItem _selectedItem;
        private IList<MissionTracksItem> _missionItems;

        public DlgChooseMission(IEnumerable<MissionTracksItem> items) {
            InitializeComponent();

            lstMissions.ItemsSource = items;
        }

        public bool IsListItemSelected { 
            get => _isListItemSelected; 
            set => _isListItemSelected = value; 
        }
        public MissionTracksItem SelectedItem { 
            get => _selectedItem;
            set {
                _selectedItem = value;
                IsListItemSelected = _selectedItem != null;
            }
        }

        public IList<MissionTracksItem> MissionItems { get => _missionItems; set => _missionItems = value; }

        private void OK_Click(object sender, RoutedEventArgs e) {
            // Set selected item
            this.DialogResult = true;
        }

        private void LstMissions_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            SelectedItem = (MissionTracksItem)e.AddedItems[0];
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
