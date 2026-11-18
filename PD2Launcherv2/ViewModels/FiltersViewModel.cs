using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using PD2Launcherv2.Enums;
using PD2Launcherv2.Helpers;
using PD2Launcherv2.Interfaces;
using PD2Launcherv2.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;

namespace PD2Launcherv2.ViewModels
{
    public class FiltersViewModel : ViewModelBase
    {
        private readonly FilterHelpers _filterHelpers;
        private readonly ILocalStorage _localStorage;
        public RelayCommand SaveFilterCommand { get; private set; }
        public RelayCommand OpenAuthorsPageCommand { get; private set; }
        public RelayCommand OpenHelpPageCommand { get; private set; }
        public RelayCommand CopyToLocalCommand { get; private set; }
        public ICommand OpenFilterPreviewCommand { get; }
        public ICommand CloseFilterPreviewCommand { get; }
        public ICommand ToggleItemBuilderCommand { get; }
        public ICommand IncreaseFilterLevelCommand { get; }
        public ICommand DecreaseFilterLevelCommand { get; }
        public ICommand AddBuiltItemCommand { get; }
        public ICommand RemovePreviewItemCommand { get; }

        // Filter preview: the installed BH.dll evaluates every item inside a simulated game
        // (see PD2Launcherv2.BHPreview), so the preview cannot drift from what BH does in game.
        private readonly BhPreviewService _bhPreview = new();
        private BhCatalog? _bhCatalog;
        private string? _previewFilterKey;
        private bool _previewRefreshPending;
        // Settings changes refresh the preview once the user stops typing.
        private readonly System.Windows.Threading.DispatcherTimer _previewRefreshTimer =
            new() { Interval = TimeSpan.FromMilliseconds(500) };
        // Items the user built and kept, shown above the default list.
        private readonly List<BhSavedPreviewItem> _savedItems = BhPreviewSavedItems.Load();

        public BhItemBuilder ItemBuilder { get; } = new();

        private bool _isItemBuilderOpen;
        public bool IsItemBuilderOpen
        {
            get => _isItemBuilderOpen;
            set { _isItemBuilderOpen = value; OnPropertyChanged(); }
        }

        private bool _isPreviewVisible;
        public bool IsPreviewVisible
        {
            get => _isPreviewVisible;
            set { _isPreviewVisible = value; OnPropertyChanged(); }
        }

        public ObservableCollection<BhPreviewRow> PreviewRows { get; } = new();

        private string _previewStatus = "";
        public string PreviewStatus
        {
            get => _previewStatus;
            set { _previewStatus = value; OnPropertyChanged(); }
        }

        private bool _isPreviewBusy;
        public bool IsPreviewBusy
        {
            get => _isPreviewBusy;
            set { _isPreviewBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsPreviewIdle)); }
        }
        public bool IsPreviewIdle => !_isPreviewBusy;

        private bool _showHiddenItems = true;
        public bool ShowHiddenItems
        {
            get => _showHiddenItems;
            set { _showHiddenItems = value; OnPropertyChanged(); RefreshFilterPreview(); }
        }

        public List<string> PreviewDifficulties { get; } = new() { "Normal", "Nightmare", "Hell" };

        private int _previewDifficulty = 2;
        public int PreviewDifficulty
        {
            get => _previewDifficulty;
            set { _previewDifficulty = value; OnPropertyChanged(); SchedulePreviewRefresh(); }
        }

        private string _previewCharacterLevel = "90";
        public string PreviewCharacterLevel
        {
            get => _previewCharacterLevel;
            set { _previewCharacterLevel = value; OnPropertyChanged(); SchedulePreviewRefresh(); }
        }

        private const int MinFilterLevel = 0, MaxFilterLevel = 15;

        /// <summary>The BH filter level to preview, kept within 0-15.</summary>
        private string _previewFilterLevel = "1";
        public string PreviewFilterLevel
        {
            get => _previewFilterLevel;
            set
            {
                // Typed numbers are clamped; anything else is ignored. Empty is allowed while typing.
                if (int.TryParse(value, out var level)) value = Math.Clamp(level, MinFilterLevel, MaxFilterLevel).ToString();
                else if (!string.IsNullOrEmpty(value)) value = _previewFilterLevel;
                _previewFilterLevel = value;
                OnPropertyChanged();
                SchedulePreviewRefresh();
            }
        }

        private void StepFilterLevel(int delta)
        {
            int level = int.TryParse(PreviewFilterLevel, out var l) ? l : 1;
            PreviewFilterLevel = Math.Clamp(level + delta, MinFilterLevel, MaxFilterLevel).ToString();
        }

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
            SaveFilterCommand = new RelayCommand(SaveFilterExecute);
            OpenAuthorsPageCommand = new RelayCommand(OpenAuthorsPageExecute);
            OpenHelpPageCommand = new RelayCommand(OpenHelpPageExecute);
            OpenFilterPreviewCommand = new RelayCommand(OpenFilterPreview);
            CloseFilterPreviewCommand = new RelayCommand(CloseFilterPreview);
            ToggleItemBuilderCommand = new RelayCommand(() => IsItemBuilderOpen = !IsItemBuilderOpen);
            IncreaseFilterLevelCommand = new RelayCommand(() => StepFilterLevel(1));
            DecreaseFilterLevelCommand = new RelayCommand(() => StepFilterLevel(-1));
            AddBuiltItemCommand = new RelayCommand(AddBuiltItem);
            RemovePreviewItemCommand = new RelayCommand<BhPreviewRow>(RemovePreviewItem);
            _previewRefreshTimer.Tick += (_, _) => { _previewRefreshTimer.Stop(); RefreshFilterPreview(); };
            CopyToLocalCommand = new RelayCommand(CopyFilterToLocalExecute);
            IsPreviewVisible = false;
        }

        public async Task InitializeAsync()
        {
            Debug.WriteLine("Initializing FiltersViewModel...");
            await FetchAndStoreFilterAuthorsAsync();
            SelectStoredAuthorAndFilter();
        }

        private void CheckLocalFilterTooltip()
        {
            // Show tooltip if the selected filter is local
            if (SelectedAuthor?.Name == "Local Filter")
            {
                // Show tooltip
                MessageBox.Show("Note: Local filters do not receive automatic updates.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private BhPreviewWorld PreviewWorld()
        {
            int level = int.TryParse(PreviewCharacterLevel, out var l) ? Math.Clamp(l, 1, 99) : 90;
            return new BhPreviewWorld { CharacterLevel = level, Difficulty = PreviewDifficulty };
        }

        private int? PreviewFilterLevelValue() =>
            int.TryParse(PreviewFilterLevel, out var l) && l >= 0 ? l : null;

        /// <summary>Writes the selected filter to a file the preview host can read.</summary>
        private async Task<string?> PreviewFilterFileAsync()
        {
            if (SelectedFilter == null) return null;
            if (SelectedAuthor?.Name == "Local Filter") return File.Exists(SelectedFilter.Path) ? SelectedFilter.Path : null;
            var content = await _filterHelpers.FetchFilterContentAsync(SelectedFilter.DownloadUrl);
            if (string.IsNullOrEmpty(content)) return null;
            var dir = Path.Combine(Path.GetTempPath(), "PD2BHPreview", "filters");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, SelectedFilter.Name);
            await File.WriteAllTextAsync(path, content);
            return path;
        }

        private void OpenFilterPreview()
        {
            if (SelectedFilter == null) return;
            IsPreviewVisible = true;
            RefreshFilterPreview();
        }

        private void SchedulePreviewRefresh()
        {
            if (!IsPreviewVisible) return;
            _previewRefreshTimer.Stop();
            _previewRefreshTimer.Start();
        }

        /// <summary>Keeps the built item at the top of every preview until it is removed.</summary>
        private void AddBuiltItem()
        {
            var item = ItemBuilder.Build(out var error);
            if (item == null)
            {
                PreviewStatus = error ?? "";
                return;
            }
            _savedItems.Insert(0, item);
            BhPreviewSavedItems.Save(_savedItems);
            RefreshFilterPreview();
        }

        private void RemovePreviewItem(BhPreviewRow? row)
        {
            if (row?.Source == null) return;
            if (_savedItems.Remove(row.Source)) BhPreviewSavedItems.Save(_savedItems);
            PreviewRows.Remove(row);
        }

        private async void RefreshFilterPreview()
        {
            if (!IsPreviewVisible || SelectedFilter == null) return;
            if (IsPreviewBusy)
            {
                _previewRefreshPending = true; // run again with the latest settings when done
                return;
            }
            IsPreviewBusy = true;
            try
            {
                var world = PreviewWorld();
                var filterLevel = PreviewFilterLevelValue();
                var filterKey = $"{SelectedAuthor?.Name}/{SelectedFilter.Name}";
                if (!_bhPreview.IsRunning || _previewFilterKey != filterKey || _bhPreview.FilterLevel != filterLevel)
                {
                    PreviewStatus = "Loading the filter...";
                    var filterPath = await PreviewFilterFileAsync();
                    if (filterPath == null)
                    {
                        PreviewStatus = "Could not read the selected filter.";
                        return;
                    }
                    var error = await _bhPreview.StartAsync(Directory.GetCurrentDirectory(), filterPath, filterLevel, world);
                    if (error != null)
                    {
                        PreviewStatus = error;
                        return;
                    }
                    _previewFilterKey = filterKey;
                    if (_bhCatalog == null)
                    {
                        _bhCatalog = await _bhPreview.CatalogAsync();
                        ItemBuilder.SetCatalog(_bhCatalog);
                    }
                }

                PreviewStatus = "Checking items...";
                var custom = _savedItems.ToList();
                foreach (var c in custom) c.Spec.Label = c.Label;
                int customCount = custom.Count;
                var specs = custom.Select(c => c.Spec).Concat(BhPreviewSamples.Build(_bhCatalog ?? new BhCatalog())).ToList();
                var response = await _bhPreview.EvaluateAsync(specs, world);
                if (response.Error != null)
                {
                    PreviewStatus = response.Error;
                    return;
                }
                PreviewRows.Clear();
                int hidden = 0;
                for (int i = 0; i < specs.Count && i < response.Items.Count; i++)
                {
                    bool isCustom = i < customCount;
                    var row = new BhPreviewRow(specs[i], response.Items[i], isCustom ? custom[i] : null);
                    if (row.Hidden && !isCustom) hidden++;
                    // Items the user asked about are always shown, hidden or not.
                    if (row.Hidden && !ShowHiddenItems && !isCustom) continue;
                    PreviewRows.Add(row);
                }
                PreviewStatus = $"{specs.Count - customCount} items, {hidden} hidden by this filter.";
            }
            catch (Exception ex)
            {
                PreviewStatus = $"Preview failed: {ex.Message}";
                Debug.WriteLine($"BH preview: {ex}");
            }
            finally
            {
                IsPreviewBusy = false;
                if (_previewRefreshPending)
                {
                    _previewRefreshPending = false;
                    RefreshFilterPreview();
                }
            }
        }

        private void CloseFilterPreview()
        {
            IsPreviewVisible = false;
            _previewRefreshTimer.Stop();
            _previewRefreshPending = false;
            PreviewRows.Clear();
            _bhPreview.Stop();
            _previewFilterKey = null;
        }

        private void OpenHelpPageExecute()
        {
            OpenUrlInBrowser("https://github.com/Project-Diablo-2/LootFilters");
        }

        private void SelectStoredAuthorAndFilter()
        {
            var storedSelection = _localStorage.LoadSection<SelectedAuthorAndFilter>(StorageKey.SelectedAuthorAndFilter);
            if (storedSelection?.selectedAuthor != null)
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
            string rootDirectory = Directory.GetCurrentDirectory();
            var localFiltersPath = Path.Combine(rootDirectory, "filters", "local");
            Debug.WriteLine($"localFiltersPath: {localFiltersPath}");
            Debug.WriteLine($"localFiltersPath: {localFiltersPath}");
            Debug.WriteLine($"localFiltersPath: {localFiltersPath}");
            Debug.WriteLine($"localFiltersPath: {localFiltersPath}");
            if (Directory.Exists(localFiltersPath))
            {
                var filterFiles = Directory.EnumerateFiles(localFiltersPath, "*.filter")
                                           .Select(path => new FilterFile { Name = Path.GetFileName(path), Path = path })
                                           .ToList();

                FiltersList = filterFiles;
            }

        }


        private async void CopyFilterToLocalExecute()
        {
            if (SelectedFilter == null || SelectedAuthor == null)
            {
                MessageBox.Show("Please select a filter to copy.", "No Filter Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Confirm copy action with a warning message
            var result = MessageBox.Show("Applying Local Filter: Note that a local filter will not receive automatic updates unless you manually update it.\n\n" +
                                         "Do you want to proceed with copying this filter to local storage?",
                                         "Copy Filter to Local", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

            if (result == MessageBoxResult.OK)
            {
                try
                {
                    // try to create the local filter path if it doesnt exist
                    string localPath = Path.Combine(Directory.GetCurrentDirectory(), "filters", "local", SelectedFilter.Name);
                    Directory.CreateDirectory(Path.GetDirectoryName(localPath) ?? string.Empty);

                    // turn filter into a string
                    var filterContent = await _filterHelpers.FetchFilterContentAsync(SelectedFilter.DownloadUrl);

                    // Save the filter string to local path
                    await File.WriteAllTextAsync(localPath, filterContent);

                    // navigate to local filters page
                    SelectedAuthor = AuthorsList.FirstOrDefault(author => author.Name == "Local Filter");

                    Debug.WriteLine("Filter copied to local storage and 'Local Filter' selected.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to copy filter to local storage: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Debug.WriteLine("Error copying filter to local storage: " + ex.Message);
                }
            }
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

        private async void SaveFilterExecute()
        {
            SaveFilterToStorage();
            if (SelectedFilter != null && SelectedAuthor != null)
            {
                bool success = await _filterHelpers.ApplyLootFilterAsync(SelectedAuthor.Name, SelectedFilter.Name, SelectedFilter.DownloadUrl,true);

                if (success)
                {
                    // Update storage to reflect the new or updated filter
                    SaveFilterToStorage();
                    Debug.WriteLine("Filter applied");
                    Messenger.Default.Send(new NavigationMessage { Action = NavigationAction.GoBack });
                }
                else
                {
                    Debug.WriteLine("Failed to apply filter.");
                }
            }
        }

        private async void OpenAuthorsPageExecute()
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
            }
        }
    }
}