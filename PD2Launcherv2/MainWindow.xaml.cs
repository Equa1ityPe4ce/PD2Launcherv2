
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using PD2Launcherv2.Enums;
using PD2Launcherv2.Helpers;
using PD2Launcherv2.Interfaces;
using PD2Launcherv2.Messages;
using PD2Launcherv2.Models;
using PD2Launcherv2.Views;
using ProjectDiablo2Launcherv2.Models;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace PD2Launcherv2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ILocalStorage _localStorage;
        private readonly FileUpdateHelpers _fileUpdateHelpers;
        private readonly FilterHelpers _filterHelpers;
        public bool IsBeta { get; private set; }
        public ICommand OpenOptionsCommand { get; private set; }
        public ICommand OpenLootCommand { get; private set; }
        public ICommand OpenDonateCommand { get; private set; }
        public ICommand OpenAboutCommand { get; private set; }
        public ICommand OpenHomeCommand { get; set; }
        public ICommand OpenTradeCommand { get; set; }
        public ICommand OpenRedditCommand { get; set; }
        public ICommand OpenTwitterCommand { get; set; }
        public ICommand OpenDiscordCommand { get; set; }
        public ICommand OpenWikiCommand { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            OpenOptionsCommand = new RelayCommand(ShowOptionsView);
            OpenLootCommand = new RelayCommand(ShowLootView);
            OpenAboutCommand = new RelayCommand(ShowAboutView);
            _localStorage = (ILocalStorage)App.ServiceProvider.GetService(typeof(ILocalStorage));
            _fileUpdateHelpers = (FileUpdateHelpers)App.ServiceProvider.GetService(typeof(FileUpdateHelpers));
            _filterHelpers = (FilterHelpers)App.ServiceProvider.GetService(typeof(FilterHelpers));
            LoadConfiguration();

        // Registering to receive NavigationMessage
        Messenger.Default.Register<NavigationMessage>(this, OnNavigationMessageReceived);
        Messenger.Default.Register<ConfigurationChangeMessage>(this, OnConfigurationChanged);
            DataContext = this;
        }
        private void OnNavigationMessageReceived(NavigationMessage message)
        {
            Overlay.Visibility = Visibility.Collapsed;
            // Handle the message
            if (message.Action == NavigationAction.GoBack)
            {
                // Assuming MainFrame is your Frame control
                if (MainFrame.CanGoBack)
                {
                    MainFrame.GoBack();
                }
                else
                {
                    // If no navigation history, just clear the content of the frame.
                    MainFrame.Content = null;
                }
            }
        }

        private void ClearNavigationStack()
        {
            while (MainFrame.CanGoBack)
            {
                MainFrame.RemoveBackEntry();
            }
        }

        private void BackgroundImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private async void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("PlayButton_Click start");
            var selectedAuthorAndFilter = _localStorage.LoadSection<SelectedAuthorAndFilter>(StorageKey.SelectedAuthorAndFilter);
            if (selectedAuthorAndFilter?.selectedFilter != null)
            {
                bool isUpdated = await _filterHelpers.CheckAndUpdateFilterAsync(selectedAuthorAndFilter);
            }
            // Set the play button to the updating image immediately
            var updatingImageUri = new Uri("pack://application:,,,/Resources/Images/updating_disabled.jpg");
            PlayButton.NormalImageSource = new BitmapImage(updatingImageUri);

            // Load or setup default file update model
            FileUpdateModel storeUpdate = _localStorage.LoadSection<FileUpdateModel>(StorageKey.FileUpdateModel) ?? new FileUpdateModel
            {
                Client = "https://storage.googleapis.com/storage/v1/b/pd2-client-files/o",
                FilePath = "Live"
            };
            if (storeUpdate.Client is null)
            {
                _localStorage.Update(StorageKey.FileUpdateModel, storeUpdate);
            }

            await _fileUpdateHelpers.UpdateFilesCheck(_localStorage);

            // Optionally reset the play button image after updates are completed
            var playImageUri = new Uri("pack://application:,,,/Resources/Images/play.jpg");
            PlayButton.NormalImageSource = new BitmapImage(playImageUri);

            Debug.WriteLine("PlayButton_Click end");
        }

        private void OptionsButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("OptionsButton_Click start");
            ShowOptionsView();
            Debug.WriteLine("OptionsButton_Click end");
        }

        private void ShowOptionsView()
        {
            ClearNavigationStack();
            Overlay.Visibility = Visibility.Visible;
            MainFrame.Navigate(new OptionsView());
        }

        private void ShowLootView()
        {
            ClearNavigationStack();
            Overlay.Visibility = Visibility.Visible;
            MainFrame.Navigate(new FiltersView());
        }

        private void ShowAboutView()
        {
            ClearNavigationStack();
            Overlay.Visibility = Visibility.Visible;
            MainFrame.Navigate(new AboutView());
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // Use the ProcessStartInfo class to open the link in the default browser
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });

            // Prevent the default behavior of opening the link
            e.Handled = true;
        }
        private void DonateButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("DonateButton_Click start");
            Debug.WriteLine("DonateButton_Click end");
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("CloseButton_Click start");
            this.Close();
            Debug.WriteLine("CloseButton_Click end");
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MinimizeButton_Click start");
            this.WindowState = WindowState.Minimized;
            Debug.WriteLine("MinimizeButton_Click end");
        }

        private void TextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock && textBlock.Tag is string url)
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }
        private void LoadConfiguration()
        {
            // Assuming _localStorage has already been initialized
            var fileUpdateModel = _localStorage.LoadSection<FileUpdateModel>(StorageKey.FileUpdateModel);
            IsBeta = fileUpdateModel?.FilePath == "Beta";

            // Directly setting the Visibility of BetaNotification
            BetaNotification.Visibility = IsBeta ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnConfigurationChanged(ConfigurationChangeMessage message)
        {
            // Update UI based on the message content
            BetaNotification.Visibility = message.IsBeta ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}