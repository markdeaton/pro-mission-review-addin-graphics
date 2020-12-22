using MissionAgentReview.datatypes;
using System;
using System.Collections.Generic;
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

        public DlgChooseMission(IList<MissionTracksItem> items) {
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

        private void lstMissions_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            SelectedItem = (MissionTracksItem)e.AddedItems[0];
        }
    }
}
