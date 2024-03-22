using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using PD2Launcherv2.Enums;
using PD2Launcherv2.Helpers;
using PD2Launcherv2.Interfaces;
using PD2Launcherv2.Models;
using ProjectDiablo2Launcherv2.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using static Dropbox.Api.TeamPolicies.SmartSyncPolicy;

namespace PD2Launcherv2.ViewModels
{
    public class FiltersViewModel : ViewModelBase
    {
        private readonly FilterHelpers _filterHelpers;
        private readonly ILocalStorage _localStorage;
        public RelayCommand SaveFilterCommand { get; private set; }
        public RelayCommand ViewReadmeCommand { get; private set; }
        public RelayCommand OpenAuthorsPageCommand { get; private set; }

        private List<FilterAuthor> _authorsList;
        public List<FilterAuthor> AuthorsList
        {
            get => _authorsList;
            set
            {
                if (_authorsList != value)
                {
                    _authorsList = value;
                    OnPropertyChanged();
                }
            }
        }

        private FilterAuthor _selectedAuthor;
        public FilterAuthor SelectedAuthor
        {
            get => _selectedAuthor;
            set
            {
                if (_selectedAuthor != value)
                {
                    _selectedAuthor = value;
                    OnPropertyChanged();
                    FetchDataFromAuthorUrl(value.Url);
                }
            }
        }

        private List<FilterFile> _filtersList;
        public List<FilterFile> FiltersList
        {
            get => _filtersList;
            set
            {
                _filtersList = value;
                OnPropertyChanged();
                SelectStoredFilter();
            }
        }
        private FilterFile _selectedFilter;
        public FilterFile SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (_selectedFilter != value)
                {
                    _selectedFilter = value;
                    OnPropertyChanged();
                    SaveFilterToStorage();
                }
            }
        }

        private string _changeProperty;
        public string ChangeProperty
        {
            get => _changeProperty;
            set
            {
                if (_changeProperty != value)
                {
                    _changeProperty = value;
                    OnPropertyChanged();
                }
            }
        }

        public RelayCommand AuthorCall { get; private set; }
        public RelayCommand FilterCall { get; private set; }

        public FiltersViewModel(ILocalStorage localStorage)
        {
            CloseCommand = new RelayCommand(CloseView);
            _localStorage = localStorage;
            _filterHelpers = new FilterHelpers(new HttpClient(), _localStorage);
            AuthorCall = new RelayCommand(async () => await AuthorCall_Click());
            FilterCall = new RelayCommand(FilterCall_Click);
            SaveFilterCommand = new RelayCommand(SaveFilterExecute);
            ViewReadmeCommand = new RelayCommand(ViewReadmeExecute);
            OpenAuthorsPageCommand = new RelayCommand(OpenAuthorsPageExecute);
        }

        public async Task InitializeAsync()
        {
            Debug.WriteLine("Initializing FiltersViewModel...");
            await FetchAndStoreFilterAuthorsAsync();
            SelectStoredAuthorAndFilter();
        }

        private void SelectStoredAuthorAndFilter()
        {
            var storedSelection = _localStorage.LoadSection<SelectedAuthorAndFilter>(StorageKey.SelectedAuthorAndFilter);
            if (storedSelection != null)
            {
                // Attempt to select the stored author
                SelectedAuthor = AuthorsList.FirstOrDefault(a => a.Author == storedSelection.selectedAuthor.Author);
            }
        }

        private void SelectStoredFilter()
        {
            // Ensure we have a selected author to match the filter with
            if (SelectedAuthor == null) return;

            var storedSelection = _localStorage.LoadSection<SelectedAuthorAndFilter>(StorageKey.SelectedAuthorAndFilter);
            if (storedSelection?.selectedFilter != null)
            {
                // Attempt to select the stored filter
                SelectedFilter = FiltersList.FirstOrDefault(f => f.Name == storedSelection.selectedFilter.Name);
            }
        }

        private async void FetchDataFromAuthorUrl(string url)
        {
            if (SelectedAuthor.Name == "Local Filter")
            {
                LoadLocalFilters();
                return;
            }

            // Use HttpClient to fetch data from the specified URL
            Debug.WriteLine($"Fetching data from {url}");
            _filterHelpers.FetchFilterContentsAsync(url);
            var filterContents = await _filterHelpers.FetchFilterContentsAsync(url);
            if (filterContents != null)
            {
                // Filter to include only .filter files and README.md, if necessary
                var validFiles = filterContents.Where(f => f.Name.EndsWith(".filter") || f.Name == "README.md").ToList();
                FiltersList = validFiles;
            }
        }

        private async Task FetchAndStoreFilterAuthorsAsync()
        {
            Debug.WriteLine("start FetchAndStoreFilterAuthorsAsync");
            await _filterHelpers.FetchAndStoreFilterAuthorsAsync();
            LoadAuthorsFromStorage();
            var storedData = _localStorage.LoadSection<Pd2AuthorList>(StorageKey.Pd2AuthorList);
            if (storedData?.StorageAuthorList != null)
            {
                // Prepend "Local" author to the list
                var updatedList = new List<FilterAuthor> { new FilterAuthor { Name = "Local Filter", Url = "local", Author = "Local Filter" } };
                updatedList.AddRange(storedData.StorageAuthorList);
                AuthorsList = updatedList;
            }
            Debug.WriteLine("end FetchAndStoreFilterAuthorsAsync");
        }

        private void LoadAuthorsFromStorage()
        {
            // Load the Pd2AuthorList which contains ETag and the actual AuthorList
            var storedData = _localStorage.LoadSection<Pd2AuthorList>(StorageKey.Pd2AuthorList);
            // Ensure "Local" author is at the top
            if (storedData?.StorageAuthorList != null)
            {
                AuthorsList = storedData.StorageAuthorList;
            }
        }

        private void LoadLocalFilters()
        {
            // Retrieve the base path from storage
            var fileUpdateModel = _localStorage.LoadSection<FileUpdateModel>(StorageKey.FileUpdateModel);
            if (fileUpdateModel != null)
            {
                var localFiltersPath = Path.Combine(fileUpdateModel.FilePath, "filters", "local");
                if (Directory.Exists(localFiltersPath))
                {
                    var filterFiles = Directory.EnumerateFiles(localFiltersPath, "*.filter")
                                               .Select(path => new FilterFile { Name = Path.GetFileName(path), Path = path })
                                               .ToList();

                    FiltersList = filterFiles;
                }
            }
        }

        public async Task AuthorCall_Click()
        {
            Debug.WriteLine("start AuthorCall_Click");
            await FetchAndStoreFilterAuthorsAsync();
            Debug.WriteLine("end AuthorCall_Click");
        }

        public void FilterCall_Click()
        {
            Debug.WriteLine("FilterCall_Click start");
            Debug.WriteLine("FilterCall_Click end");
        }

        private void SaveFilterToStorage()
        {
            if (SelectedAuthor != null && SelectedFilter != null)
            {
                var selection = new SelectedAuthorAndFilter
                {
                    selectedAuthor = SelectedAuthor,
                    selectedFilter = SelectedFilter
                };

                _localStorage.Update(StorageKey.SelectedAuthorAndFilter, selection);
            }
        }

        private void CloseView()
        {
            Messenger.Default.Send(new NavigationMessage { Action = NavigationAction.GoBack });
        }

        private void SaveFilterExecute()
        {
            SaveFilterToStorage();
        }

        private void ViewReadmeExecute()
        {
            // Implementation for viewing readme
        }

        private void OpenAuthorsPageExecute()
        {
            if (SelectedAuthor != null && !string.IsNullOrEmpty(SelectedAuthor.Url))
            {
                string modifiedUrl = SelectedAuthor.Url
                    .Replace("api.github.com/repos", "github.com")
                    .Replace("/contents", "");

                OpenUrlInBrowser(modifiedUrl);
            }
        }

        private void OpenUrlInBrowser(string url)
        {
            if (url == "local") { return; }
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open URL: {url}. Error: {ex.Message}");
                // Optionally, inform the user that opening the URL failed.
            }
        }
    }
}