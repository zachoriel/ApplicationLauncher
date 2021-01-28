using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CustomGameLauncher
{
    struct Version
    {
        internal static Version zero = new Version(0, 0, 0);

        short major;
        short minor;
        short subMinor;

        // Constructor for creating new game versions.
        internal Version(short _major, short _minor, short _subMinor)
        {
            major = _major;
            minor = _minor;
            subMinor = _subMinor;
        }

        // Constructor for reading game versions in files.
        internal Version(string _version)
        {
            // Split the version string into separate components separated by a dot.
            string[] _versionStrings = _version.Split('.');

            // If the version is invalid (ex. 1.1 instead of 1.1.1).
            if (_versionStrings.Length != 3)
            {
                major = 0;
                minor = 0;
                subMinor = 0;
                return;
            }

            // If the version is valid, parse the strings into the appropriate category.
            major = short.Parse(_versionStrings[0]);
            minor = short.Parse(_versionStrings[1]);
            subMinor = short.Parse(_versionStrings[2]);
        }

        // Method for comparing local game version with online version.
        internal bool IsDifferentThan(Version _otherVersion)
        {
            if (major != _otherVersion.major)
            {
                return true;
            }
            else
            {
                if (minor != _otherVersion.minor)
                {
                    return true;
                }
                else
                {
                    if (subMinor != _otherVersion.subMinor)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // Method for comparing online version with local game version.
        internal bool IsGreaterThan(Version _localVersion)
        {
            if (major > _localVersion.major)
            {
                return true;
            }
            else
            {
                if (minor > _localVersion.minor)
                {
                    return true;
                }
                else
                {
                    if (subMinor > _localVersion.subMinor)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // ToString() override to return the version numbers separated by dots.
        public override string ToString()
        {
            return $"{major}.{minor}.{subMinor}";
        }
    }

    enum LauncherStatus
    {
        Ready,
        Failed,
        Waiting,
        DownloadingGame,
        DownloadingUpdate
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // File paths.
        string rootPath;
        string extractionPath;
        string versionFile;
        string gameZip;
        string gameExe;

        bool needsUpdate; // Helps control the type of download.

        LauncherStatus _status;
        internal LauncherStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                switch (_status)
                {
                    case LauncherStatus.Ready:
                        PlayButton.Content = "Play";
                        break;
                    case LauncherStatus.Failed:
                        PlayButton.Content = "Update Failed - Retry";
                        break;
                    case LauncherStatus.Waiting:
                        PlayButton.Content = "Start Download";
                        break;
                    case LauncherStatus.DownloadingGame:
                        PlayButton.Content = "Downloading Game...";
                        break;
                    case LauncherStatus.DownloadingUpdate:
                        PlayButton.Content = "Downloading Update...";
                        break;
                    default:
                        break;
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            rootPath = Directory.GetCurrentDirectory(); // The game will download here, but be extracted elsewhere.
            extractionPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            versionFile = Path.Combine(extractionPath, "Cafe Interstellar", "Cosmechanics_Build", "Version.txt");
            gameZip = Path.Combine(rootPath, "Cosmechanics_Build.zip"); // <- Will need to be maintained OR kept consistent.
            gameExe = Path.Combine(rootPath, "Cosmechanics_Build", "ProjectFlorpMajor.exe"); // <- Will need to be maintained OR kept consistent.
        }

        // Function which checks online for a new game version.
        void CheckForUpdates()
        {
            // If a local version already exists...
            if (File.Exists(versionFile))
            {
                // Get the local version number and set it to the launcher's VersionText.
                Version localVersion = new Version(File.ReadAllText(versionFile));
                VersionText.Text = localVersion.ToString(); // No script reference; VersionText is an element in MainWindow.xaml

                try
                {
                    // Retrieve the most recent online version from the download site.
                    WebClient webClient = new WebClient();
                    Version onlineVersion = new Version(webClient.DownloadString("https://github.com/SheaMcAuley995/Cosmechanics/releases/latest/download/Version.txt")); // <- Where the version file is found.

                    // If the local version is different from the online version...
                    if (onlineVersion.IsGreaterThan(localVersion))
                    {
                        // Set the launcher status to waiting and prompt the user to initiate download.
                        needsUpdate = true;
                        Status = LauncherStatus.Waiting;
                        PercentageText.Text = "There is an update available.";
                    }
                    // Otherwise...
                    else
                    {
                        // Set launcher status to Ready.
                        Status = LauncherStatus.Ready;
                        PercentageText.Text = "Game up to date. Ready to play!";
                    }
                }
                // This should only happen if the download location changes and nobody updates the web link stored into onlineVersion,
                // or if there are connection problems.
                catch (Exception ex)
                {
                    Status = LauncherStatus.Failed;
                    MessageBox.Show($"Error checking for game updates: {ex}");
                }
            }
            // If a local version does not exist...
            else
            {
                // Set the launcher status to waiting and prompt the user to initiate download.
                needsUpdate = false;
                Status = LauncherStatus.Waiting;
                PercentageText.Text = "There is an update available.";
            }
        }

        // Method for installing game files from a source.
        void InstallGameFiles(bool _isUpdate, Version _onlineVersion)
        {
            // Reset the loading bar for a fresh installation process.
            LoadingBar.Value = 0;
            PercentageText.Text = "0%";

            try
            {
                WebClient webClient = new WebClient();

                // If the download is an update, set status accordingly.
                if (_isUpdate)
                {
                    Status = LauncherStatus.DownloadingUpdate;
                }
                // If the download is a fresh install, set status accordingly.
                else
                {
                    Status = LauncherStatus.DownloadingGame;
                    _onlineVersion = new Version(webClient.DownloadString("https://github.com/SheaMcAuley995/Cosmechanics/releases/latest/download/Version.txt")); // <- Where the version file is found.
                }

                // Download progress & completion event subscription.
                webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(ProgressChangedCallback);
                webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadGameCompletedCallback);
                
                // Downloads the game files asynchronously. 
                webClient.DownloadFileAsync(new Uri("https://github.com/SheaMcAuley995/Cosmechanics/releases/latest/download/Cosmechanics_Build.zip"), gameZip, _onlineVersion); // <- Where the game is downloaded from.
            }
            // This should only happen if the download location changes and nobody updates the web links,
            // or if there are connection problems.
            catch (Exception ex)
            {
                Status = LauncherStatus.Failed;
                MessageBox.Show($"Error installing game files: {ex}");
            }
        }

        // Called when progress is made on the download.
        void ProgressChangedCallback(object sender, ProgressChangedEventArgs e)
        {
            try
            {
                // Update the loading bar.
                LoadingBar.Value = e.ProgressPercentage;
                PercentageText.Text = $"{e.ProgressPercentage}%";
            }
            catch (Exception ex) // I can't think of a reason this would occur, but I like to have the catch just in case.
            {
                MessageBox.Show($"Error displaying download progress: {ex}");
            }
        }

        // Called when the download completes.
        void DownloadGameCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                // The onlineVersion string was used above as the userToken for our async download. That event's UserState returns said identifier.
                // Thus, we can cast that object to type Version and store that value as our new onlineVersion string.
                string onlineVersion = ((Version)e.UserState).ToString();

                // Fun fact: gameZip, which was initialized above as the path to our zip file, is not actually itself a zip file; it's a string. 
                // Strings cannot be extracted, so we have to convert it into a proper zip file first using the ZipArchive class.
                using (FileStream zipToOpen = new FileStream(gameZip, FileMode.OpenOrCreate))
                {
                    using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update))
                    {
                        // Identification/creation of the full extraction path for Cafe Interstellar games.
                        var folder = Path.Combine(extractionPath, "Cafe Interstellar");
                        var newLoc = Directory.CreateDirectory(folder);

                        // Extract the game files.
                        ZipFileExtensions.ExtractToDirectory(archive, newLoc.FullName, true);
                    }
                }
                
                // Delete the (now empty) zip folder.
                File.Delete(gameZip);

                // Write the online version text into the local version file.
                File.WriteAllText(versionFile, onlineVersion);

                // Update the launcher version text & set status to ready.
                VersionText.Text = onlineVersion;
                Status = LauncherStatus.Ready;
                PercentageText.Text = "Game up to date. Ready to play!";
            }
            // This should only happen if something goes wrong in the extraction or if there's an IO error 
            // (permissions problem or process in use, probably) during the writing of the version file.
            catch (Exception ex)
            {
                // Delete game files if they exist, as they'll be only partially extracted at best.
                var path = Path.Combine(extractionPath, "Cafe Interstellar", "Cosmechanics_Build");
                if (Directory.Exists(path))
                {
                    Directory.Delete(path);
                }

                Status = LauncherStatus.Failed;
                MessageBox.Show($"Error finishing download: {ex}");
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            
        }

        private void CosmechanicsButton_Click(object sender, RoutedEventArgs e)
        {
            // Show the update screen elements.
            LoadingBar.Visibility = Visibility.Visible;
            LoadingBar.IsEnabled = true;
            PercentageText.Visibility = Visibility.Visible;
            PercentageText.IsEnabled = true;
            PlayButton.Visibility = Visibility.Visible;
            PlayButton.IsEnabled = true;
            BackButton.Visibility = Visibility.Visible;
            BackButton.IsEnabled = true;
            VersionText.Visibility = Visibility.Visible;
            VersionText.IsEnabled = true;

            // Hide the game selection elements.
            GameSelectionText.Visibility = Visibility.Hidden;
            GameSelectionText.IsEnabled = false;
            CosmechanicsButton.Visibility = Visibility.Hidden;
            CosmechanicsButton.IsEnabled = false;

            // Change the background image.
            BackgroundImage.Source = new BitmapImage(new Uri(@"/CustomGameLauncher;component/Images/CosmechanicsImage2.png", UriKind.Relative));

            // Check for updates as soon as the launcher opens & renders.
            CheckForUpdates();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Only go back to game selection if a download is not in progress.
            if (Status != LauncherStatus.DownloadingGame && Status != LauncherStatus.DownloadingUpdate)
            {
                // Show the game selection elements.
                GameSelectionText.Visibility = Visibility.Visible;
                GameSelectionText.IsEnabled = true;
                CosmechanicsButton.Visibility = Visibility.Visible;
                CosmechanicsButton.IsEnabled = true;

                // Hide the update screen elements.
                LoadingBar.Visibility = Visibility.Hidden;
                LoadingBar.IsEnabled = false;
                PercentageText.Visibility = Visibility.Hidden;
                PercentageText.IsEnabled = false;
                PlayButton.Visibility = Visibility.Hidden;
                PlayButton.IsEnabled = false;
                BackButton.Visibility = Visibility.Hidden;
                BackButton.IsEnabled = false;
                VersionText.Visibility = Visibility.Hidden;
                VersionText.IsEnabled = false;

                // Change the background image.
                BackgroundImage.Source = new BitmapImage(new Uri(@"/CustomGameLauncher;component/Images/Cafe_Interstellar_Splash_screen_B.png", UriKind.Relative));
            }
        }

        // Play button click event.
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            // Special case when the user has first booted up the launcher.
            if (Status == LauncherStatus.Waiting)
            {
                // If a local version does not exist...
                if (!needsUpdate)
                {
                    // Download a brand new installation.
                    InstallGameFiles(needsUpdate, Version.zero);
                }
                else
                {
                    // Install the update files and update the local version number.
                    WebClient webClient = new WebClient();
                    Version onlineVersion = new Version(webClient.DownloadString("https://github.com/SheaMcAuley995/Cosmechanics/releases/latest/download/Version.txt")); // <- Where the version file is found.
                    
                    InstallGameFiles(needsUpdate, onlineVersion);
                }

                return;
            }

            var path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var folder = Path.Combine(path, "Cafe Interstellar");
            gameExe = Path.Combine(folder, "Cosmechanics_Build", "ProjectFlorpMajor.exe");

            // If the game exe exists and the status is set to ready...
            if (File.Exists(gameExe) && Status == LauncherStatus.Ready)
            {
                // Set the new application's working directory and start the application.
                ProcessStartInfo startInfo = new ProcessStartInfo(gameExe);
                startInfo.WorkingDirectory = Path.Combine(folder, "Cosmechanics_Build");
                Process.Start(startInfo);

                // Close the launcher.
                Close();
            }
            // If the status is failed...
            else if (Status == LauncherStatus.Failed)
            {
                // Check again for updates.
                CheckForUpdates();
            }
            else if (!File.Exists(gameExe))
            {
                MessageBox.Show("Error: Application executable not found. This may be the result of a moved or deleted .exe file, or perhaps Zach" +
                    " needs to update the filepaths.");
            }
        }
    }
}
