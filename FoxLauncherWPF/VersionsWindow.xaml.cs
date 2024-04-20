
using FoxLauncher;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;


namespace FoxLauncherWPF
{

    public partial class VersionsWindow : Window
    {

        public VersionsWindow()
        {
            InitializeComponent();

            btnConfirm.Click += (s, e) => WorkSpace.Confirm(MainWindow.mainWindow);
            Version.MouseDoubleClick += (s, e) => WorkSpace.Confirm(MainWindow.mainWindow);

            Version.ItemsSource = WorkSpace.Versions;
            Closed += (sender, e) => Application.Current.Shutdown();
        }

    }
}
