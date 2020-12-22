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
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MissionAgentReview {
    internal class ShowDlgChooseMission : Button {

        private DlgChooseMission _dlgchoosemission = null;

        protected override void OnClick() {
            //already open?
            if (_dlgchoosemission != null)
                return;
/*            _dlgchoosemission = new DlgChooseMission();
            _dlgchoosemission.Owner = FrameworkApplication.Current.MainWindow;
            _dlgchoosemission.Closed += (o, e) => { _dlgchoosemission = null; };
            _dlgchoosemission.Show();*/
            //uncomment for modal
            //_dlgchoosemission.ShowDialog();
        }

    }
}
