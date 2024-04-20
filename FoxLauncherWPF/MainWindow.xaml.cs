using FoxLauncher;
using System;
using System.Windows;


namespace FoxLauncherWPF
{
    public partial class MainWindow : Window
    {
        internal static MainWindow mainWindow;
        public MainWindow()
        {
            try
            {
                InitializeComponent();
                mainWindow = this;
                WorkSpace.LoadingSettings(ref mainWindow);
                Nickname.TextChanged += (sender, e) => WorkSpace.ChooseNickname(mainWindow);
                sliderRAM.ValueChanged += (sender, e) => WorkSpace.RAM_ValueChanged(mainWindow);
                btnFolder.Click += (sender, e) => WorkSpace.SelectFolder(mainWindow);
                btnVersion.Click += (sender, e) => WorkSpace.ChooseVersion();
                btnStart.Click += (sender, e) => WorkSpace.StartLauncher(mainWindow);
                btnClear.Click += (sender, e) => WorkSpace.Clear();
                WorkSpace.PreviousWindow = this;
                btnLogs.Click += (sender, e) => WorkSpace.ShowLogs();
                Closed += (sender, e) => Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при инициализации MainWindow: {ex.Message}");
            }
        }

    }
}