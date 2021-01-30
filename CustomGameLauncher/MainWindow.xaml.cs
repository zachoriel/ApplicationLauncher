using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Forms;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Collections.Generic;

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

        /// <summary>
        /// Method for comparing local game version with online version.
        /// </summary>
        /// <param name="_otherVersion"> The latest online release of the software. </param>
        /// <returns> True if the versions are not equal, false if they are. </returns>
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

        /// <summary>
        /// Method for comparing online version with local game version.
        /// </summary>
        /// <param name="_localVersion"> The local version of the software stored in its main directory. </param>
        /// <returns> True if the latest version online is greater than the local version, false if not. </returns>
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

        /// <summary>
        /// Loops through all of the release tags at a web address (intended for github releases) and stores existing tags in a list.
        /// </summary>
        /// <param name="address"> The web location containing app releases. </param>
        /// <param name="latest"> The latest public release version. </param>
        /// <returns> A list containing all publicly available versions of the software. </returns>
        internal List<string> LoopThroughTags(string address, Version latest)
        {
            List<string> availableVersions = new List<string>();

            for (int i = latest.major; i >= 0; i--)
            {
                for (int j = 9; j >= 0; j--)
                {
                    for (int k = 9; k >= 0; k--)
                    {
                        string ver = $"{i}.{j}.{k}";

                        try
                        {
                            string url = address + ver;
                            if (!string.IsNullOrEmpty(url))
                            {
                                UriBuilder uriBuilder = new UriBuilder(url);
                                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uriBuilder.Uri);
                                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                                if (response.StatusCode == HttpStatusCode.OK)
                                {
                                    availableVersions.Add(ver);
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }
                        // Link probably returned a 404; move onto the next tag.
                        catch (Exception ex)
                        {
                            continue;
                        }
                    }
                }
            }

            return availableVersions;
        }


        /// <summary>
        /// ToString() override to return the version numbers separated by dots.
        /// </summary>
        /// <returns> The version number separated by dots. </returns>
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
        DownloadingSoftware,
        InstallingUpdate
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // File paths.
        string savedLocData;
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
                    case LauncherStatus.DownloadingSoftware:
                        PlayButton.Content = "Downloading Software...";
                        break;
                    case LauncherStatus.InstallingUpdate:
                        PlayButton.Content = "Installing Update...";
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
        }

        // Function which checks online for a new game version.
        void CheckForUpdates(string webLink)
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
                    Version onlineVersion = new Version(webClient.DownloadString(webLink)); // <- Where the version file is found.

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
                        PercentageText.Text = "The application up to date. Ready to launch!";
                    }
                }
                // This should only happen if the download location changes and nobody updates the web link stored into onlineVersion,
                // or if there are connection problems.
                catch (Exception ex)
                {
                    Status = LauncherStatus.Failed;
                    System.Windows.MessageBox.Show($"Error checking for updates: {ex}");
                }
            }
            // If a local version does not exist...
            else
            {
                // Set the launcher status to waiting and prompt the user to initiate download.
                needsUpdate = false;
                Status = LauncherStatus.Waiting;
                PercentageText.Text = "The application is available for download.";
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
                    Status = LauncherStatus.InstallingUpdate;
                }
                // If the download is a fresh install, set status accordingly.
                else
                {
                    Status = LauncherStatus.DownloadingSoftware;
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
                System.Windows.MessageBox.Show($"Error installing game files: {ex}");
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
                System.Windows.MessageBox.Show($"Error displaying download progress: {ex}");
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
                PercentageText.Text = "The application is up to date. Ready to launch!";
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
                System.Windows.MessageBox.Show($"Error finishing download: {ex}");
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            // Check for available updates to the launcher as soon as the main window renders.
            versionFile = Path.Combine(Directory.GetCurrentDirectory(), "Version.txt");

            // If a local version already exists...
            if (File.Exists(versionFile))
            {
                // Get the local version number and set it to the launcher's VersionText.
                Version localVersion = new Version(File.ReadAllText(versionFile));

                try
                {
                    // Retrieve the most recent online version from the download site.
                    WebClient webClient = new WebClient();
                    Version onlineVersion = new Version(webClient.DownloadString("https://github.com/zachoriel/ApplicationLauncher/releases/latest/download/Version.txt")); // <- Where the version file is found.

                    // If the local version is different from the online version...
                    if (onlineVersion.IsGreaterThan(localVersion))
                    {
                        // Ask the user if they'd like to download the new version.
                        System.Windows.MessageBoxResult result = System.Windows.MessageBox.Show("It looks like there's a newer version " +
                            "of this launcher available. Would you like to download it?", "New Version Available!", MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        // If they clicked yes...
                        if (result.ToString() == "Yes")
                        {
                            // Download the new version from the releases page.
                            var startInfo = new ProcessStartInfo()
                            {
                                FileName = "https://github.com/zachoriel/ApplicationLauncher/releases/latest/download/ApplicationLauncher_Windows.zip",
                                UseShellExecute = true
                            };
                            Process.Start(startInfo);

                            Close();
                        }
                    }
                    // Otherwise, there are no available updates to the launcher so nothing needs to happen.
                    else { }
                }
                // This should only happen if the download location changes and nobody updates the web link stored into onlineVersion,
                // or if there are connection problems.
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error checking for launcher updates: {ex}");
                }
            }
            // There should always be a local version file for the launcher. The only reason there wouldn't be is if Zach forgets to put one 
            // in the uploaded files. If that happens, we'll assume that the user has the most up to date version by creating a version file
            // and assigning its contents to that of the latest online version. Then they will be notified of any future updates.
            else
            {
                // Retrieve the most recent online version from the download site.
                WebClient webClient = new WebClient();
                Version onlineVersion = new Version(webClient.DownloadString("https://github.com/zachoriel/ApplicationLauncher/releases/latest/download/Version.txt")); // <- Where the version file is found.

                // Create a version file and write the online version string into it.
                string localVersionPath = Path.Combine(Directory.GetCurrentDirectory(), "Version.txt");
                using (StreamWriter sw = File.CreateText(localVersionPath))
                {
                    sw.WriteLine(onlineVersion);
                }
            }

            //// TESTING TAG LOOPS
            //WebClient client = new WebClient();
            //Version latestVersion = new Version(client.DownloadString("https://github.com/zachoriel/ApplicationLauncher/releases/latest/download/Version.txt"));

            //latestVersion.LoopThroughTags("https://github.com/zachoriel/ApplicationLauncher/releases/tag/", latestVersion);
        }

        /// <summary>
        /// Allows users to select a location for their downloaded software.
        /// </summary>
        /// <param name="changingLocation"> False if the install location is being picked for the first time, true if it's being changed. </param>
        void PickLocation(bool changingLocation)
        {
            using (var browserDialog = new FolderBrowserDialog())
            {
                // Bring up the Windows system's Browse window.
                DialogResult result = browserDialog.ShowDialog();

                // If the user presses "Ok" and the selected directory is not empty...
                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(browserDialog.SelectedPath))
                {
                    // If the user has writing permissions to their selected directory...
                    if (DirectoryHasPermissions(browserDialog.SelectedPath, FileSystemRights.CreateDirectories))
                    {
                        // If an installation already exists and we're changing the path...
                        if (changingLocation)
                        {
                            // If the new location is not the same as the old location...
                            if (browserDialog.SelectedPath != extractionPath)
                            {
                                // Move the existing install to the newly selected installation location.
                                string existingInstall = Path.Combine(extractionPath, "Cafe Interstellar");
                                Directory.Move(existingInstall, browserDialog.SelectedPath + "/Cafe Interstellar");
                            }
                        }

                        // Store the selected directory as the extraction path.
                        extractionPath = browserDialog.SelectedPath;

                        // Write the selected path to the saved install location document.
                        using (StreamWriter sw = File.CreateText(savedLocData))
                        {
                            sw.WriteLine(browserDialog.SelectedPath);
                        }
                    }
                    // If they don't have the right permissions...
                    else
                    {
                        // Display an error message prompting the user to retry and recursively call the function.
                        System.Windows.MessageBox.Show("Sorry, you do not have system permissions to install in that location. Please try somewhere else (example: Desktop or Games)");
                        PickLocation(changingLocation);
                    }
                }
                // If the selected directory is not valid...
                else
                {
                    // Display an error message prompting the user to retry and recursively call the function.
                    System.Windows.MessageBox.Show("Sorry, that location is invalid. Please try somewhere else (example: Desktop)");
                    PickLocation(changingLocation);
                }
            }
        }

        /// <summary>
        /// Checks if the user has permissions to a given directory.
        /// </summary>
        /// <param name="folderPath"> The full path to be checked for permissions. </param>
        /// <param name="accessRight"> The access right to be checked for. Example: 'Write' or 'CreateDirectories'. </param>
        /// <returns> True if the user does have the passed-in access right, false if they do not. </returns>
        bool DirectoryHasPermissions(string folderPath, FileSystemRights accessRight)
        {
            // Automatically return false if the path is empty.
            if (string.IsNullOrEmpty(folderPath)) return false;

            try
            {
                // Get access rules for the current user.
                DirectoryInfo di = new DirectoryInfo(folderPath);
                AuthorizationRuleCollection rules = di.GetAccessControl().GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));
                WindowsIdentity identity = WindowsIdentity.GetCurrent(); // The logged-in user.

                // For every file access rule...
                foreach (FileSystemAccessRule rule in rules)
                {
                    // If the user has a permission rule...
                    if (identity.Groups.Contains(rule.IdentityReference) || identity.Owner.Equals(rule.IdentityReference))
                    {
                        // If that permission rule matches the one we're checking for...
                        if ((accessRight & rule.FileSystemRights) == accessRight)
                        {
                            // If that permission is set to 'Allow'...
                            if (rule.AccessControlType == AccessControlType.Allow)
                            {
                                // The user has permissions for that folder and we can return true.
                                return true;
                            }
                        }
                    }
                }
            }
            // Return false by default if the check cannot be performed.
            catch { }
            return false;
        }

        private void ChangeInstallLocation_Click(object sender, RoutedEventArgs e)
        {
            PickLocation(true);
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
            ChangeLocationButton.Visibility = Visibility.Visible;
            ChangeLocationButton.IsEnabled = true;

            // Hide the game selection elements.
            GameSelectionText.Visibility = Visibility.Hidden;
            GameSelectionText.IsEnabled = false;
            CosmechanicsButton.Visibility = Visibility.Hidden;
            CosmechanicsButton.IsEnabled = false;

            // Change the background image.
            BackgroundImage.Source = new BitmapImage(new Uri(@"/CustomGameLauncher;component/Images/CosmechanicsImage2.png", UriKind.Relative));

            // Check if a saved install location file exists in the launcher's working directory.
            savedLocData = Path.Combine(Directory.GetCurrentDirectory(), "Saved Install Location.txt");
            if (!File.Exists(savedLocData))
            {
                // Create the file and write a temporary empty line.
                using (StreamWriter sw = File.CreateText(savedLocData))
                {
                    sw.WriteLine("");
                }

                // Prompt the user to pick an installation location.
                System.Windows.MessageBox.Show("Please select an install location.");
                PickLocation(false);
            }
            else
            {
                // Read from the saved install location file and store the contents into extractionPath.
                using (StreamReader sr = File.OpenText(savedLocData))
                {
                    extractionPath = sr.ReadLine();
                }

                // Special case for if the saved location is empty (should only happen if the program is exited before an initial location is picked)...
                if (string.IsNullOrWhiteSpace(extractionPath))
                {
                    // Prompt the user to pick an installation location.
                    System.Windows.MessageBox.Show("Please select an install location.");
                    PickLocation(false);
                }
            }

            versionFile = Path.Combine(extractionPath, "Cafe Interstellar", "Cosmechanics_Build", "Version.txt");
            gameZip = Path.Combine(rootPath, "Cosmechanics_Build.zip"); // <- Will need to be maintained OR kept consistent.

            // Check for updates as soon as the install location is determined.
            CheckForUpdates("https://github.com/SheaMcAuley995/Cosmechanics/releases/latest/download/Version.txt");
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Only go back to game selection if a download is not in progress.
            if (Status != LauncherStatus.DownloadingSoftware && Status != LauncherStatus.InstallingUpdate)
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
                ChangeLocationButton.Visibility = Visibility.Hidden;
                ChangeLocationButton.IsEnabled = false;

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

            var path = extractionPath;
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
                CheckForUpdates("https://github.com/SheaMcAuley995/Cosmechanics/releases/latest/download/Version.txt");
            }
            else if (!File.Exists(gameExe))
            {
                System.Windows.MessageBox.Show("Error: Application executable not found. This may be the result of a moved or deleted .exe file, or perhaps Zach" +
                    " needs to update the filepaths.");
            }
        }
    }
}
