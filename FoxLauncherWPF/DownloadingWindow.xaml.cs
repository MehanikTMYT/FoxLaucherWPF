using FoxLauncher;
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
using System.Windows.Shapes;

namespace FoxLauncherWPF
{
    public partial class DownloadingWindow : Window
    {


        public DownloadingWindow()
        {
            InitializeComponent();
            btnLogs.Click += (sender, e) => WorkSpace.ShowLogs();
            Closed += (sender, e) => Application.Current.Shutdown();
        }

    }
}