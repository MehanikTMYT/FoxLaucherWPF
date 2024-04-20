using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Downloader;
using FoxLauncherWPF;
using FoxLauncherWPF.Properties;
using MySqlConnector;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using static System.Net.WebRequestMethods;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace FoxLauncherWPF
{
    // Получение данных из внешнего файла конфигурации
    public class AppConfig
    {
        public class DatabaseConfig
        {
            public string DBName { get; set; }
            public string UserDB { get; set; }
            public string PasswordDB { get; set; }
        }

        public class ServerConfig
        {
            public string BaseUrl { get; set; }
        }

        public DatabaseConfig Database { get; set; }
        public ServerConfig Server { get; set; }

        public static AppConfig Load()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = "FoxLauncherWPF.appsettings.json";

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        throw new Exception($"Resource '{resourceName}' not found in assembly '{assembly.FullName}'.");
                    }

                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string json = reader.ReadToEnd();
                        return JsonConvert.DeserializeObject<AppConfig>(json);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при загрузке AppConfig: {ex.Message}", ex);
            }
        }
    }

    internal class WorkSpace
    {


        internal static readonly object fileChangedLock = new object();
        internal static readonly object progressChangedLock = new object();
        internal static ObservableCollection<string> Versions { get; set; } = [];
        internal static VersionsWindow versionWindow = new();
        internal static DownloadingWindow downloadingWindow = new();
        internal static LogsWindow logsWindow = new();
        internal static Window? PreviousWindow;
        internal static Progress<DownloadFileChangedEventArgs> downloadProgress = new();
        internal static readonly Progress<ProgressChangedEventArgs> fileProgress = new();
        internal static AppConfig config;
        internal static string DB;
        internal static string UserDB;
        internal static string PassDB;
        internal static string BaseUrl;
        internal static string url;
        internal static string baseUrl;
        internal static int BufferSize = 10 * 1024 * 1024;
        internal static string connectionString;

        internal static void ChooseNickname(MainWindow mainWindow)
        {
            try
            {
                DispatcherTimer timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(5) // Интервал ожидания изменений
                };

                void SaveNickname()
                {
                    timer.Stop();

                    if (mainWindow.Nickname.Text != Settings.Default.LastEnteredNickname)
                    {
                        UpdateTextBoxDebug($"Установка значения {mainWindow.Nickname.Text} и сохранение в памяти");
                        Settings.Default.LastEnteredNickname = mainWindow.Nickname.Text;
                        Settings.Default.Save();
                    }
                }

                timer.Tick += (sender, e) =>
                {
                    SaveNickname();
                };

                mainWindow.Nickname.TextChanged += (sender, e) =>
                {
                    timer.Stop();
                    timer.Start();
                };

                mainWindow.Nickname.LostFocus += (sender, e) =>
                {
                    SaveNickname();
                };

                mainWindow.PreviewKeyDown += (sender, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        mainWindow.Nickname.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                        SaveNickname();
                    }
                };

                timer.Start();
            }
            catch (Exception ex)
            {
                UpdateTextBoxDebug($"Ошибка: {ex.Message}");
            }
        }

        //Метод выбора ОЗУ (автоматический)
        internal static void RAM_ValueChanged(MainWindow mainWindow)
        {
            try
            {
                // Проверяем, изменилось ли значение слайдера
                if (Settings.Default.LastEnteredRAM != Convert.ToInt32(mainWindow.sliderRAM.Value))
                {
                    DispatcherTimer timer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(5) // Интервал ожидания изменений
                    };

                    mainWindow.RAM.Text = $"Значение ОЗУ {mainWindow.sliderRAM.Value} MB ({mainWindow.sliderRAM.Value / 1024} ГБ)";
                    timer.Tick += (sender, e) =>
                    {
                        // Записываем значение слайдера в логи и сохраняем в память

                        UpdateTextBoxDebug($"Значение ОЗУ: {mainWindow.sliderRAM.Value} MB ({mainWindow.sliderRAM.Value / 1024} ГБ)");
                        Settings.Default.LastEnteredRAM = (int)mainWindow.sliderRAM.Value;
                        Settings.Default.Save();

                        timer.Stop();
                    };

                    timer.Start();
                }
            }
            catch (Exception ex)
            {
                UpdateTextBoxDebug($"Ошибка при сохранении значения слайдера: {ex.Message}");
            }
        }

        //Метод вывода сообщений в окно логов
        internal static async void UpdateTextBoxDebug(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    logsWindow.Debug.Text += $"{DateTime.Now} - {message}\r";
                    logsWindow.Debug.CaretIndex = logsWindow.Debug.Text.Length;
                });
            }
        }

        //Метод возрата значений при перезагрузке приложения 
        internal static void LoadingSettings(ref MainWindow mainWindow)
        {
            //Устанавливаем максимальное количество одновременных соединений для каждого хоста. 
            System.Net.ServicePointManager.DefaultConnectionLimit = 256;
            //Получение переменных из файла внешнего файла конфигурации (можно убрать конф файл
            //и тогда нужно будет подставить значения в переменные DB UserDB PassDB BaseUrl тут или в начале кода
            //если в значения задать в начале кода тут можно удалить соответсвующие строчки 
            config = AppConfig.Load();
            DB = config.Database.DBName;
            UserDB = config.Database.UserDB;
            PassDB = config.Database.PasswordDB;
            BaseUrl = config.Server.BaseUrl;
            url = $"http://{BaseUrl}";
            baseUrl = $"{url}:5000";
            connectionString = $"Server={BaseUrl};Database={DB};User={UserDB};Password={PassDB};Port=3306;";
            //Инициализация заполнения списка версий
            GetVersions();
            //Возрат значений при перезагрузке приложения
            //Последний выбранный клиент (если выбран)
            if (!string.IsNullOrEmpty(Settings.Default.LastSelectedClient))
            {
                UpdateTextBoxDebug($"Обнаружен выбранный клиент. Установка - {Settings.Default.LastSelectedClient}");
                mainWindow.Version.Text = "Выбранный клиент " + Settings.Default.LastSelectedClient;
            }
            //Последний выбранный никнейм (если выбран)
            if (string.IsNullOrEmpty(Settings.Default.LastEnteredNickname))
            {
                UpdateTextBoxDebug($"Не обнаружено значение имени пользователя.");
                mainWindow.Nickname.Text = "User";
                Settings.Default.LastEnteredNickname = "User";
            }
            else
            {
                UpdateTextBoxDebug($"Обнаружено значение имени пользователя. Установка значения {Settings.Default.LastEnteredNickname}");
                mainWindow.Nickname.Text = Settings.Default.LastEnteredNickname;
            }

            //Последний выбранный путь к клиентам (если выбран)
            if (string.IsNullOrEmpty(Settings.Default.LastSelectedFolderPath))
            {
                UpdateTextBoxDebug("Путь не выбран установка...");
                SelectFolder(mainWindow);
            }
            else
            {
                UpdateTextBoxDebug($"Путь обнаружен в памяти '{Settings.Default.LastSelectedFolderPath}'");
                mainWindow.Folder.Text = Settings.Default.LastSelectedFolderPath;
            }
            //Последний выбранный размер ОЗУ (если выбран)
            if (Settings.Default.LastEnteredRAM != 0)
            {
                UpdateTextBoxDebug($"Обнаружено ОЗУ в памяти, установка значения {Settings.Default.LastEnteredRAM} МБ ({Settings.Default.LastEnteredRAM / 1024}) ГБ");
                mainWindow.sliderRAM.Value = Settings.Default.LastEnteredRAM;
                mainWindow.RAM.Text = $"Значение ОЗУ {mainWindow.sliderRAM.Value} MB ({mainWindow.sliderRAM.Value / 1024}) ГБ)";
            }
        }

        // Метод окна выбора папки
        internal static void SelectFolder(MainWindow mainWindow)
        {
            if (string.IsNullOrEmpty(Settings.Default.LastSelectedFolderPath) || mainWindow.Folder.Text != "Выберите папку")
            {
                VistaFolderBrowserDialog dialog = new()
                {
                    Description = "Выберите папку для лаунчера",
                    UseDescriptionForTitle = true
                };

                // Проверяем, поддерживается ли новый стиль диалога выбора папки
                if (!VistaFolderBrowserDialog.IsVistaFolderDialogSupported)
                {
                    MessageBox.Show("Поскольку вы не используете Windows Vista или более позднюю версию, будет использовано стандартное диалоговое окно выбора папки. Пожалуйста, используйте Windows Vista, чтобы увидеть новый диалог.", "Диалог выбора папки");
                }

                // Открываем диалоговое окно и получаем результат выбора
                if (dialog.ShowDialog() == true)
                {
                    // Сохраняем выбранный путь в настройках
                    if (Settings.Default.LastSelectedFolderPath != dialog.SelectedPath)
                    {
                        Settings.Default.LastSelectedFolderPath = dialog.SelectedPath;
                        mainWindow.Folder.Text = dialog.SelectedPath;
                        UpdateTextBoxDebug($"Выбрана и сохранена папка c путём: {dialog.SelectedPath}");
                        Settings.Default.Save();
                    }                 
                }
                else
                {
                    MessageBox.Show("Вы не выбрали папку! Повторная попытка");
                    SelectFolder(mainWindow);
                    return;
                }
            }
        }

        //Метод проверки коректности данных для лаунчера и иницилизация загрузки
        internal static async Task StartLauncher(MainWindow mainWindow)
        {
            if (string.IsNullOrEmpty(Settings.Default.LastEnteredRAM.ToString()))
            {
                MessageBox.Show("Пожалуйста, укажите количество ОЗУ используя слайдер");
            }
            else if (string.IsNullOrEmpty(Settings.Default.LastEnteredNickname.ToString()))
            {
                MessageBox.Show("Установка стандартного никнейма.");
                ChooseNickname(mainWindow);
                // Повторная проверка после выбора никнейма
                await StartLauncher(mainWindow);
                return;
            }
            else if (string.IsNullOrEmpty(Settings.Default.LastSelectedFolderPath))
            {
                MessageBox.Show("Выбор папки");
                SelectFolder(mainWindow);
                // Повторная проверка после выбора папки
                await StartLauncher(mainWindow);
                return;
            }
            else if (string.IsNullOrEmpty(Settings.Default.LastSelectedClient))
            {
                MessageBox.Show("Пожалуйста, выберите версию.");
                ChooseVersion();
            }
            else
            {
                // Скрыть предыдущее окно, если оно существует
                PreviousWindow?.Hide();

                await GetFiles();

                downloadingWindow.Show();
                PreviousWindow = downloadingWindow;
            }
        }

        //Метод перехода к окну логов
        internal static void ShowLogs()
        {
            logsWindow.Show();
            PreviousWindow?.Hide();
        }

        //Метод выбора версии
        internal static void Confirm(MainWindow mainWindow)
        {
            if (versionWindow.Version.SelectedItem != null)
            {
                UpdateTextBoxDebug($"Выбранный клиент: {versionWindow.Version.SelectedItem}");
                mainWindow.Version.Text = "Выбранный клиент: " + versionWindow.Version.SelectedItem.ToString();
                Settings.Default.LastSelectedClient = versionWindow.Version.SelectedItem.ToString();
                Settings.Default.Save();
                versionWindow.Hide();
                PreviousWindow?.Show();
            }
            else
            {
                //Вывод ошибки
                MessageBox.Show("Версия не выбрана");
                UpdateTextBoxDebug("Версия не выбрана!");
            }
        }
        //Метод получения списка версий от БД
        static internal async Task GetVersions()
        {
            try
            {
                //Подключение к БД
                using MySqlConnection connection = new(connectionString);
                await connection.OpenAsync();

                //Иницализация списка
                List<string> versionsList = [];

                //Запрос к БД
                string query = "SELECT name FROM profiles";
                using (MySqlCommand command = new(query, connection))
                {
                    using MySqlDataReader reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        //Заполнение списка новыми
                        string version = reader.GetString(0);
                        versionsList.Add(version.Trim('"'));
                    }
                }
                //Очистка экранного списка и заполнение данными из списка
                Versions.Clear();
                foreach (var version in versionsList)
                {
                    Versions.Add(version);
                }
            }
            //Вывод ошибки
            catch (Exception ex)
            {
                UpdateTextBoxDebug($"Ошибка: {ex.Message}");
            }
        }

        //Метод перехода на окно выбора версии
        internal static void ChooseVersion()
        {
            versionWindow.Show();
            PreviousWindow?.Hide();
        }

        //Метод очистки папок клиента
        internal static void Clear()
        {
            if (!string.IsNullOrEmpty(Settings.Default.LastSelectedClient))
            {
                if (Directory.Exists(Path.Combine(Settings.Default.LastSelectedFolderPath, Settings.Default.LastSelectedClient, "data")))
                {
                    Directory.Delete(Path.Combine(Settings.Default.LastSelectedFolderPath, Settings.Default.LastSelectedClient, "data"));
                }
                if (Directory.Exists(Path.Combine(Settings.Default.LastSelectedFolderPath, Settings.Default.LastSelectedClient, "game")))
                {
                    Directory.Delete(Path.Combine(Settings.Default.LastSelectedFolderPath, Settings.Default.LastSelectedClient, "game"));
                }
                //Обновление параметра типа для повторной загрузки файлов
                Settings.Default.UpdateType = true;
                Settings.Default.Save();
            }
        }

        //Метод полной обработки файлов
        internal static async Task GetFiles()
        {
            //Получение версии профиля
            string profileName = await GetProfileName();
            //Получение списка файлов с сервера
            List<string> fileNames = await GetFileNames(profileName);
            //Обработка файлов
            await ProcessFiles(profileName, fileNames);
            //Запуск клиента
            await StartGame(profileName, Settings.Default.UpdateType);
        }

        internal static async Task StartGame(string profileName, bool update)
        {
            // Инициализация лаунчера по заданному пути
            CMLauncher launcher = new CMLauncher(new MinecraftPath(Path.Combine(Settings.Default.LastSelectedFolderPath, "game")));

            // Обработчик изменений файлов
            launcher.FileChanged += (e) =>
            {
                lock (fileChangedLock)
                {
                    downloadingWindow.Dispatcher.Invoke(() =>
                    {
                        downloadingWindow.statusTextBox.Text = $"Обработка файла {e.FileName} ({e.ProgressedFileCount} из {e.TotalFileCount})";
                    });
                }
            };

            // Обработчик изменений прогресса файлов
            launcher.ProgressChanged += (s, e) =>
            {
                lock (progressChangedLock)
                {
                    downloadingWindow.Dispatcher.Invoke(() =>
                    {
                        downloadingWindow.statusTextBox.Text = $"{e.ProgressPercentage}%";
                        downloadingWindow.progressBar.Value = e.ProgressPercentage;
                    });
                }
            };

            // Проверка типа обновлений и запуск
            using (var process = await launcher.CreateProcessAsync(Settings.Default.LastSelectedClient, new MLaunchOption
            {
                MaximumRamMb = Settings.Default.LastEnteredRAM,
                Session = MSession.CreateOfflineSession(Settings.Default.LastEnteredNickname),
            }, checkAndDownload: update))
            {
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.EnableRaisingEvents = true;

                process.ErrorDataReceived += async (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() => UpdateTextBoxDebug($"Ошибка: {e.Data}"));
                    }
                };

                process.OutputDataReceived += async (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() => UpdateTextBoxDebug($"Обработка данных: {e.Data}"));
                    }
                };

                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
                 
                // Ожидание завершения процесса
                await process.WaitForExitAsync();

                // Закрытие приложения после завершения процесса
                Application.Current.Shutdown();
            }
        }

        //Метод для получения версии профиля
        internal static async Task<string> GetProfileName()
        {
            //Имя профиля
            string profileName = Settings.Default.LastSelectedClient;

            //Блок подключения к БД
            using MySqlConnection connection = new(connectionString);
            await connection.OpenAsync();

            //Запрос к БД
            string query = "SELECT version FROM profiles WHERE name = @name";
            using MySqlCommand command = new(query, connection);
            command.Parameters.AddWithValue("@name", Settings.Default.LastSelectedClient);

            //Обработка запроса получения версии профиля
            using MySqlDataReader reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                profileName = reader.GetString(0);
            }
            else
            {
                //Вывод ошибки
                downloadingWindow.statusTextBox.Text = $"Профиль с именем {profileName} не найден";
                UpdateTextBoxDebug($"Профиль с именем {profileName} не найден");
            }

            return profileName;
        }

        //Метод получения списка файлов с сервера
        internal static async Task<List<string>> GetFileNames(string profileName)
        {
            using HttpClient client = new();
            HttpResponseMessage response = await client.GetAsync($"{baseUrl}/profile/{profileName}/files");

            if (response.IsSuccessStatusCode)
            {
                string jsonString = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<string>>(jsonString);
            }
            else
            {
                return [];
            }
        }

        //Метод обработки файлов
        internal static async Task ProcessFiles(string profileName, List<string> fileNames)
        {
            //Списки архивов и модов для скачивания
            List<DownloadFile> archivesToDownload = [];
            List<DownloadFile> modsToDownload = [];

            //Путь к папке с архивами и начало ссылки скачивания
            string dirArchive = Path.Combine(Settings.Default.LastSelectedFolderPath, Settings.Default.LastSelectedClient, "data");
            string url1 = $"{baseUrl}/ftb/";

            // Проверяем наличие файла "mods.zip" в директории "data"
            bool modsZipInDataExists = System.IO.File.Exists(Path.Combine(dirArchive, "mods.zip"));

            foreach (string file in fileNames)
            {
                //Конечная ссылка скачивания и путь к локальному файлу
                string fileUrl = $"{url1}{Settings.Default.LastSelectedClient}/{file}";
                string localFilePath = Path.Combine(dirArchive, file);

                // Если файл - "mods.zip", проверяем его наличие в директории "data"
                if (file == "mods.zip")
                {
                    //Данная проверка будет работать начиная со второго раза ибо в первый раз и так не будет архива (он будет скачиваться)
                    if (modsZipInDataExists)
                    {
                        // Инициализируем проверку модов в директории "game/mods"
                        modsToDownload = await CheckModsInGame(profileName);
                        continue; // Пропускаем загрузку "mods.zip"
                    }
                }

                // Если файл - архив, добавляем его в список для загрузки в "data"
                if (Path.GetExtension(file) == ".zip")
                {
                    archivesToDownload.Add(new DownloadFile(localFilePath, fileUrl));
                }
            }

            // Загружаем все архивы в папку "data"
            await DownloadFilesAsync(archivesToDownload, url1);

            // Загружаем моды в папку "game/mods"
            await DownloadFilesAsync(modsToDownload, url1);

            //Распаковка
            await UnzipFiles(dirArchive, modsZipInDataExists, Settings.Default.UpdateType);
        }

        //Метод распаковки файлов
        internal static async Task UnzipFiles(string dirArchive, bool modsZipInDataExists, bool update)
        {
            List<string> files;

            if (update)
            {
                files = new List<string>(Directory.GetFiles(dirArchive));
            }
            else
            {
                files = new List<string>(Directory.GetFiles(dirArchive).Where(file => Path.GetFileName(file) != "mods.zip"));
            }

            string gameDir = Path.Combine(Settings.Default.LastSelectedFolderPath, Settings.Default.LastSelectedClient, "games");

            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                string destinationDir = Path.Combine(gameDir, fileName[..fileName.LastIndexOf('.')]);


                if (fileName == "mods.zip" && modsZipInDataExists)
                {
                    continue;
                }

                // Создаем директорию для распаковки, если она не существует
                Directory.CreateDirectory(destinationDir);

                // Используем ZipFile класс для распаковки архива
                try
                {
                    await Task.Run(() => ZipFile.ExtractToDirectory(file, destinationDir));
                }
                catch (Exception ex)
                {
                    // Обработка ошибок при распаковке архива
                    UpdateTextBoxDebug($"Ошибка при распаковке архива {file}: {ex.Message}");
                }
            }
        }

        //Метод скачивания файлов
        internal static async Task DownloadFilesAsync(List<DownloadFile> filesToDownload, string url1)
        {
            AsyncParallelDownloader downloadPar = new(10);
            SequenceDownloader downloadSec = new();
            DownloadFile[] files = [.. filesToDownload];

            await Task.Run(async () =>
            {
                await downloadPar.DownloadFiles(files, downloadProgress, fileProgress);

                if (!System.IO.File.Exists(Path.Combine(Settings.Default.LastSelectedFolderPath, Settings.Default.LastSelectedClient, "games", "options.txt")))
                {
                    string OptionsPath = Path.Combine(Settings.Default.LastSelectedFolderPath, Settings.Default.LastSelectedClient, "games", "options.txt");

                    string UrlOptions = $"{url1}{Settings.Default.LastSelectedClient}/game/options.txt";

                    DownloadFile[] options = [new(OptionsPath, UrlOptions)];
                    await downloadSec.DownloadFiles(options, downloadProgress, fileProgress);
                }
                string ServersPath = Path.Combine(Settings.Default.LastSelectedFolderPath, Settings.Default.LastSelectedClient, "games", "servers.dat");
                string UrlServers = $"{url1}{Settings.Default.LastSelectedClient}/game/servers.dat";
                DownloadFile[] servers = [new(ServersPath, UrlServers)];
                await downloadSec.DownloadFiles(servers, downloadProgress, fileProgress);
            });

        }

        //Метод проверки модов
        internal static async Task<List<DownloadFile>> CheckModsInGame(string profileName)
        {
            List<DownloadFile> modsToDownload = [];

            // Получаем список модов с веб-сервера
            List<string> serverMods = await GetModsFilesList(profileName);

            // Получаем список файлов в директории "game/mods"
            string gameModsDir = Path.Combine(Settings.Default.LastSelectedFolderPath, Settings.Default.LastSelectedClient, "games", "mods");
            string[] localMods = Directory.GetFiles(gameModsDir);

            // Удаляем файлы модов из директории "game/mods", которых нет на веб-сервере
            foreach (string localMod in localMods)
            {
                string modName = Path.GetFileName(localMod);
                if (!serverMods.Contains(modName))
                {
                    System.IO.File.Delete(localMod);
                }
            }

            // Проверяем хеш-суммы и добавляем недостающие моды в список для скачивания
            foreach (string mod in serverMods)
            {
                string localModPath = Path.Combine(gameModsDir, mod);
                string webModHash = await GetWebFileHashAsync(mod);
                string localModHash = await GetLocalFileHash(localModPath);

                if (webModHash != localModHash || !System.IO.File.Exists(localModPath))
                {
                    if (System.IO.File.Exists(localModPath))
                    {
                        System.IO.File.Delete(localModPath);
                    }
                    string modUrl = $"{baseUrl}/ftb/{Settings.Default.LastSelectedClient}/game/mods/{mod}";
                    modsToDownload.Add(new DownloadFile(localModPath, modUrl));
                }
            }

            return modsToDownload;
        }

        //Метод получения списка модов
        internal static async Task<List<string>> GetModsFilesList(string profileName)
        {
            using HttpClient client = new();
            HttpResponseMessage response = await client.GetAsync($"{baseUrl}/profile/{profileName}/files/mods");

            if (response.IsSuccessStatusCode)
            {
                string jsonString = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<string>>(jsonString);
            }
            else
            {
                return [];
            }
        }

        //Метод проверки хеш-суммы локальных файлов
        internal static async Task<string?> GetLocalFileHash(string filePath)
        {
            string hash = null;
            using SHA256 hashAlgorithm = SHA256.Create();

            if (System.IO.File.Exists(filePath))
            {
                using FileStream stream = System.IO.File.OpenRead(filePath);

                byte[] hashBytes = await hashAlgorithm.ComputeHashAsync(stream);
                hash = BitConverter.ToString(hashBytes).Replace("-", "");
            }
            return hash;
        }

        //Метод получения хеш-суммы файлов с сервера
        internal static async Task<string?> GetWebFileHashAsync(string fileName)
        {
            string url = $"{baseUrl}/profile/{Settings.Default.LastSelectedClient}/hash";

            //Http запрос для получения хеш-суммы файлов с сервера
            using HttpClient client = new();
            try
            {
                HttpResponseMessage response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    JObject jsonResponse = JObject.Parse(responseBody);

                    if (jsonResponse.TryGetValue(fileName, out JToken hashToken))
                    {
                        string fileHash = hashToken.Value<string>();
                        return fileHash;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    UpdateTextBoxDebug($"Ошибка при выполнении запроса: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                UpdateTextBoxDebug($"Произошла ошибка: {ex.Message}");
                return null;
            }
        }
    }
}