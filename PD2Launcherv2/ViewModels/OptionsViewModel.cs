using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using PD2Launcherv2.Enums;
using PD2Launcherv2.Helpers;
using PD2Launcherv2.Interfaces;
using ProjectDiablo2Launcherv2.Models;
using ProjectDiablo2Launcherv2;
using System.Windows;
using PD2Launcherv2.Models;
using System.Diagnostics;

namespace PD2Launcherv2.ViewModels
{
    public class OptionsViewModel : ViewModelBase
    {
        private DDrawHelpers _ddrawHelpers;
        private readonly ILocalStorage _localStorage;
        private readonly string SetWindowsPermissionsScript = "SetPD2WindowsSettings.ps1";
        private readonly string RemoveWindowsPermissionsScript = "RemovePD2WindowsSettings.ps1";

        public Dictionary<string, bool> CheckboxStates { get; set; }

        public RelayCommand ToggleAdvancedOptionsCommand { get; }
        public RelayCommand RestoreDefaultsCommand { get; }
        public RelayCommand SetWindowsPermissionsCommand { get; }
        public RelayCommand RemoveWindowsPermissionsCommand { get; }

        public OptionsViewModel(ILocalStorage localStorage)
        {
            _localStorage = localStorage;
            _ddrawHelpers = new DDrawHelpers();
            _ddrawHelpers.ReadDdrawOptions();
            CheckboxStates = new Dictionary<string, bool>();
            OptionsModePicker = Constants.ModePickerItems();
            MaxFpsPickerItems = Constants.MaxFpsPickerItems();
            MaxGameTicksPickerItems = Constants.MaxGameTicksPickerItems();
            SaveWindowPositionPickerItems = Constants.SaveWindowPositionPickerItems();
            RendererPickerItems = Constants.RendererPickerItems();
            HookPickerItems = Constants.HookPickerItems();
            MinFpsPickerItems = Constants.MinFpsPickerItems();
            ShaderPickerItems = Constants.ShaderPickerItems();
            LoadLauncherArgs();
            LoadDDrawStorage();
            DealWithLoadingModeComboBox(_localStorage);
            CloseCommand = new RelayCommand(CloseView);
            ToggleAdvancedOptionsCommand = new RelayCommand(ToggleAdvancedOptions);
            RestoreDefaultsCommand = new RelayCommand(ResetDdrawOptionsToDefaults);
            SetWindowsPermissionsCommand = new RelayCommand(SetWindowsPermissions);
            RemoveWindowsPermissionsCommand = new RelayCommand(RemoveWindowsPermissions);
        }

        public Visibility NonFullScreenVisibility
        {
            get => string.Equals(SelectedMode, "fullscreen", StringComparison.OrdinalIgnoreCase)
                ? Visibility.Collapsed : Visibility.Visible;
        }
        public Visibility AdvancedOptionsVisibility => ShowAdvancedOptions ? Visibility.Visible : Visibility.Collapsed;
        public Visibility DDrawControlsVisible => IsDdrawSelected ? Visibility.Visible : Visibility.Collapsed;

        private bool _showAdvancedOptions = false;
        public bool ShowAdvancedOptions
        {
            get => _showAdvancedOptions;
            set
            {
                if (_showAdvancedOptions != value)
                {
                    _showAdvancedOptions = value;
                    OnPropertyChanged(nameof(ShowAdvancedOptions));
                    OnPropertyChanged(nameof(AdvancedOptionsVisibility));
                }
            }
        }

        private string _selectedMode;
        public string SelectedMode
        {
            get => _selectedMode;
            set
            {
                if (_selectedMode != value)
                {
                    Debug.WriteLine($"SelectedMode: {value}");
                    _selectedMode = value;
                    OnPropertyChanged(nameof(SelectedMode));
                    OnPropertyChanged(nameof(NonFullScreenVisibility));
                }
            }
        }

        private bool _isDdrawSelected;
        public bool IsDdrawSelected
        {
            get => _isDdrawSelected;
            set
            {
                if (_isDdrawSelected != value)
                {
                    Debug.WriteLine($"is ddraw selected? {value}");
                    _isDdrawSelected = value;
                    OnPropertyChanged(nameof(IsDdrawSelected));
                    OnPropertyChanged(nameof(DDrawControlsVisible));
                }
            }
        }

        private List<DisplayValuePair> _optionsModePicker;
        public List<DisplayValuePair> OptionsModePicker
        {
            get => _optionsModePicker;
            set
            {
                if (_optionsModePicker != value)
                {
                    _optionsModePicker = value;
                    OnPropertyChanged(nameof(OptionsModePicker));
                }
            }
        }

        private List<DisplayValuePair> _maxFpsPickerItems;
        public List<DisplayValuePair> MaxFpsPickerItems
        {
            get { return _maxFpsPickerItems; }
            set
            {
                _maxFpsPickerItems = value;
                OnPropertyChanged(nameof(MaxFpsPickerItems));
            }
        }

        private void ToggleAdvancedOptions()
        {
            ShowAdvancedOptions = !ShowAdvancedOptions;
        }

        private void SetWindowsPermissions()
        {
            var startInfo = new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy ByPass -File \"{SetWindowsPermissionsScript}\"",
                UseShellExecute = false
            };
            Process.Start(startInfo);
        }

        private void RemoveWindowsPermissions()
        {
            var startInfo = new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy ByPass -File \"{RemoveWindowsPermissionsScript}\"",
                UseShellExecute = false
            };
            Process.Start(startInfo);
        }

        private List<DisplayValuePair> _maxGameTicksPickerItems;
        public List<DisplayValuePair> MaxGameTicksPickerItems
        {
            get { return _maxGameTicksPickerItems; }
            set
            {
                _maxGameTicksPickerItems = value;
                OnPropertyChanged(nameof(MaxGameTicksPickerItems));
            }
        }

        private List<DisplayValuePair> _saveWindowPositionPickerItems;
        public List<DisplayValuePair> SaveWindowPositionPickerItems
        {
            get { return _saveWindowPositionPickerItems; }
            set
            {
                _saveWindowPositionPickerItems = value;
                OnPropertyChanged(nameof(SaveWindowPositionPickerItems));
            }
        }

        private List<DisplayValuePair> _rendererPickerItems;
        public List<DisplayValuePair> RendererPickerItems
        {
            get { return _rendererPickerItems; }
            set
            {
                _rendererPickerItems = value;
                OnPropertyChanged(nameof(RendererPickerItems));
            }
        }

        private List<DisplayValuePair> _hookPickerItems;
        public List<DisplayValuePair> HookPickerItems
        {
            get { return _hookPickerItems; }
            set
            {
                _hookPickerItems = value;
                OnPropertyChanged(nameof(HookPickerItems));
            }
        }

        private List<DisplayValuePair> _minFpsPickerItems;
        public List<DisplayValuePair> MinFpsPickerItems
        {
            get { return _minFpsPickerItems; }
            set
            {
                _minFpsPickerItems = value;
                OnPropertyChanged(nameof(MinFpsPickerItems));
            }
        }

        private List<DisplayValuePair> _shaderPickerItems;
        public List<DisplayValuePair> ShaderPickerItems
        {
            get { return _shaderPickerItems; }
            set
            {
                _shaderPickerItems = value;
                OnPropertyChanged(nameof(ShaderPickerItems));
            }
        }

        private string _selectedMaxFps;
        public string SelectedMaxFps
        {
            get => _selectedMaxFps;
            set
            {
                if (_selectedMaxFps != value)
                {
                    _selectedMaxFps = value;
                    OnPropertyChanged(nameof(SelectedMaxFps));
                }
            }
        }

        private string _selectedMaxGameTicks;
        public string SelectedMaxGameTicks
        {
            get => _selectedMaxGameTicks;
            set
            {
                if (_selectedMaxGameTicks != value)
                {
                    _selectedMaxGameTicks = value;
                    OnPropertyChanged(nameof(SelectedMaxGameTicks));
                }
            }
        }

        private string _selectedSaveWindowPosition;
        public string SelectedSaveWindowPosition
        {
            get => _selectedSaveWindowPosition;
            set
            {
                if (_selectedSaveWindowPosition != value)
                {
                    _selectedSaveWindowPosition = value;
                    OnPropertyChanged(nameof(SelectedSaveWindowPosition));
                }
            }
        }

        private string _selectedRenderer;
        public string SelectedRenderer
        {
            get => _selectedRenderer;
            set
            {
                if (_selectedRenderer != value)
                {
                    _selectedRenderer = value;
                    OnPropertyChanged(nameof(SelectedRenderer));
                }
            }
        }

        private string _selectedHook;
        public string SelectedHook
        {
            get => _selectedHook;
            set
            {
                if (_selectedHook != value)
                {
                    _selectedHook = value;
                    OnPropertyChanged(nameof(SelectedHook));
                }
            }
        }

        private string _selectedMinFps;
        public string SelectedMinFps
        {
            get => _selectedMinFps;
            set
            {
                if (_selectedMinFps != value)
                {
                    _selectedMinFps = value;
                    OnPropertyChanged(nameof(SelectedMinFps));
                }
            }
        }

        private string _selectedShader;
        public string SelectedShader
        {
            get => _selectedShader;
            set
            {
                if (_selectedShader != value)
                {
                    _selectedShader = value;
                    OnPropertyChanged(nameof(SelectedShader));
                }
            }
        }

        private bool _maintainAspectRatio;
        public bool MaintainAspectRatio
        {
            get => _maintainAspectRatio;
            set
            {
                if (_maintainAspectRatio != value)
                {
                    _maintainAspectRatio = value;
                    OnPropertyChanged(nameof(MaintainAspectRatio));
                }
            }
        }

        private bool _windowboxing;
        public bool Windowboxing
        {
            get => _windowboxing;
            set
            {
                if (_windowboxing != value)
                {
                    _windowboxing = value;
                    OnPropertyChanged(nameof(Windowboxing));
                }
            }
        }

        private bool _automaticMouseSensitivity;
        public bool AutomaticMouseSensitivity
        {
            get => _automaticMouseSensitivity;
            set
            {
                if (_automaticMouseSensitivity != value)
                {
                    _automaticMouseSensitivity = value;
                    OnPropertyChanged(nameof(AutomaticMouseSensitivity));
                }
            }
        }

        private bool _verticalSync;
        public bool VerticalSync
        {
            get => _verticalSync;
            set
            {
                if (_verticalSync != value)
                {
                    _verticalSync = value;
                    OnPropertyChanged(nameof(VerticalSync));
                }
            }
        }

        private bool _unlockCursor;
        public bool UnlockCursor
        {
            get => _unlockCursor;
            set
            {
                if (_unlockCursor != value)
                {
                    _unlockCursor = value;
                    OnPropertyChanged(nameof(UnlockCursor));
                }
            }
        }

        private bool _showWindowBorders;
        public bool ShowWindowBorders
        {
            get => _showWindowBorders;
            set
            {
                if (_showWindowBorders != value)
                {
                    _showWindowBorders = value;
                    OnPropertyChanged(nameof(ShowWindowBorders));
                }
            }
        }

        private bool _resizableWindow;
        public bool ResizableWindow
        {
            get => _resizableWindow;
            set
            {
                if (_resizableWindow != value)
                {
                    _resizableWindow = value;
                    OnPropertyChanged(nameof(ResizableWindow));
                }
            }
        }

        private bool _enableD3d9Linear;
        public bool EnableD3d9Linear
        {
            get => _enableD3d9Linear;
            set
            {
                if (_enableD3d9Linear != value)
                {
                    _enableD3d9Linear = value;
                    OnPropertyChanged(nameof(EnableD3d9Linear));
                }
            }
        }

        private bool _forceCpu0Affinity;
        public bool ForceCpu0Affinity
        {
            get => _forceCpu0Affinity;
            set
            {
                if (_forceCpu0Affinity != value)
                {
                    _forceCpu0Affinity = value;
                    OnPropertyChanged(nameof(ForceCpu0Affinity));
                }
            }
        }

        private bool _noActivateApp;
        public bool NoActivateApp
        {
            get => _noActivateApp;
            set
            {
                if (_noActivateApp != value)
                {
                    _noActivateApp = value;
                    OnPropertyChanged(nameof(NoActivateApp));
                }
            }
        }

        private bool _handleMouse;
        public bool HandleMouse
        {
            get => _handleMouse;
            set
            {
                if (_handleMouse != value)
                {
                    _handleMouse = value;
                    OnPropertyChanged(nameof(HandleMouse));
                }
            }
        }

        private bool _nonExclusive;
        public bool NonExclusive
        {
            get => _nonExclusive;
            set
            {
                if (_nonExclusive != value)
                {
                    _nonExclusive = value;
                    OnPropertyChanged(nameof(NonExclusive));
                }
            }
        }

        private bool _skipToBnet;
        public bool SkipToBnet
        {
            get => _skipToBnet;
            set
            {
                if (_skipToBnet != value)
                {
                    _skipToBnet = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _sndBkg;
        public bool SndBkg
        {
            get => _sndBkg;
            set
            {
                if (_sndBkg != value)
                {
                    _sndBkg = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _width;
        public int Width
        {
            get => _width;
            set
            {
                if (_width != value)
                {
                    _width = value;
                    OnPropertyChanged(nameof(Width));
                }
            }
        }

        private int _height;
        public int Height
        {
            get => _height;
            set
            {
                if (_height != value)
                {
                    _height = value;
                    OnPropertyChanged(nameof(Height));
                }
            }
        }

        private int _ddrawPosX;
        public int DdrawPosX
        {
            get => _ddrawPosX;
            set
            {
                if (_ddrawPosX != value)
                {
                    _ddrawPosX = value;
                    OnPropertyChanged(nameof(DdrawPosX));
                }
            }
        }

        private int _ddrawPosY;
        public int DdrawPosY
        {
            get => _ddrawPosY;
            set
            {
                if (_ddrawPosY != value)
                {
                    _ddrawPosY = value;
                    OnPropertyChanged(nameof(DdrawPosY));
                }
            }
        }

        private void LoadLauncherArgs()
        {
            Debug.WriteLine("\nStart LoadLauncherArgs");
            LauncherArgs launcherArgs = _localStorage.LoadSection<LauncherArgs>(StorageKey.LauncherArgs);
            if (launcherArgs != null)
            {
                // Assuming true represents "ddraw" and false represents "3dfx"
                IsDdrawSelected = launcherArgs.graphics;
                SkipToBnet = launcherArgs.skiptobnet;
                SndBkg = launcherArgs.sndbkg;
            }
            Debug.WriteLine($"launcherArgs.graphics {launcherArgs.graphics}");
            Debug.WriteLine($"launcherArgs.skiptobnet {launcherArgs.skiptobnet}");
            Debug.WriteLine($"launcherArgs.sndbkg {launcherArgs.sndbkg}");
            Debug.WriteLine("end LoadLauncherArgs\n");
        }

        private void UpdateLauncherArgsStorage()
        {
            Debug.WriteLine("\nStart UpdateLauncherArgsStorage");
            LauncherArgs launcherArgs = new LauncherArgs
            {
                // Again, assuming true represents "ddraw"
                graphics = IsDdrawSelected,
                skiptobnet = SkipToBnet,
                sndbkg = SndBkg,
            };
            _localStorage.Update(StorageKey.LauncherArgs, launcherArgs);
            Debug.WriteLine($"launcherArgs.graphics {launcherArgs.graphics}");
            Debug.WriteLine($"launcherArgs.skiptobnet {launcherArgs.skiptobnet}");
            Debug.WriteLine($"launcherArgs.sndbkg {launcherArgs.sndbkg}");
            Debug.WriteLine("end UpdateLauncherArgsStorage\n");
        }

        private void LoadDDrawCheckBoxOptions()
        {
            DdrawOptions dDrawOptions = _localStorage.LoadSection<DdrawOptions>(StorageKey.DdrawOptions);
            if (dDrawOptions != null)
            {
                MaintainAspectRatio = dDrawOptions.Maintas;
                Windowboxing = dDrawOptions.Boxing;
                VerticalSync = dDrawOptions.Vsync;
                AutomaticMouseSensitivity = dDrawOptions.AdjMouse;
                UnlockCursor = dDrawOptions.DevMode;
                ShowWindowBorders = dDrawOptions.Border;
                ResizableWindow = dDrawOptions.Resizeable;
                EnableD3d9Linear = dDrawOptions.D3d9Linear;
                NoActivateApp = dDrawOptions.NoActivateApp;
                HandleMouse = dDrawOptions.HandleMouse;
                NonExclusive = dDrawOptions.NonExclusive;
                ForceCpu0Affinity = dDrawOptions.SingleCpu;
            }
        }

        private void DealWithLoadingModeComboBox(ILocalStorage localStorage)
        {
            DdrawOptions dDrawOptions = _localStorage.LoadSection<DdrawOptions>(StorageKey.DdrawOptions);
            if (dDrawOptions != null)
            {
                Debug.WriteLine($"\n\n dDrawOptions.Fullscreen: {dDrawOptions.Fullscreen}");
                Debug.WriteLine($"dDrawOptions.Windowed: {dDrawOptions.Windowed} \n\n");
                if (dDrawOptions.Fullscreen && dDrawOptions.Windowed)
                {
                    SelectedMode = "borderless";
                }
                else if (!dDrawOptions.Fullscreen && dDrawOptions.Windowed)
                {
                    SelectedMode = "windowed";
                }
                else
                {
                    SelectedMode = "fullscreen";
                }
            }
        }
        private void LoadDDrawComboBoxOptions()
        {
            DdrawOptions dDrawOptions = _localStorage.LoadSection<DdrawOptions>(StorageKey.DdrawOptions);
            if (dDrawOptions != null)
            {
                SelectedMaxFps = dDrawOptions.MaxFps;
                SelectedMaxGameTicks = dDrawOptions.MaxGameTicks;
                SelectedSaveWindowPosition = dDrawOptions.SaveSettings;
                SelectedRenderer = dDrawOptions.Renderer;
                SelectedHook = dDrawOptions.Hook;
                SelectedShader = dDrawOptions.Shader;
                SelectedMinFps = dDrawOptions.MinFps;
            }
        }

        private void LoadDDrawTextBoxOptions()
        {
            DdrawOptions dDrawOptions = _localStorage.LoadSection<DdrawOptions>(StorageKey.DdrawOptions);
            if (dDrawOptions != null)
            {
                Width = dDrawOptions.Width;
                Height = dDrawOptions.Height;
                DdrawPosX = dDrawOptions.PosX;
                DdrawPosY = dDrawOptions.PosY;
            }
        }

        private void SaveDDrawOptions()
        {
            DdrawOptions dDrawOptions = _localStorage.LoadSection<DdrawOptions>(StorageKey.DdrawOptions);
            //textbox's
            dDrawOptions.Width = Width;
            dDrawOptions.Height = Height;
            dDrawOptions.PosX = DdrawPosX;
            dDrawOptions.PosY = DdrawPosY;

            //checkbox's
            dDrawOptions.Maintas = MaintainAspectRatio;
            dDrawOptions.Boxing = Windowboxing;
            dDrawOptions.Vsync = VerticalSync;
            dDrawOptions.AdjMouse = AutomaticMouseSensitivity;
            dDrawOptions.DevMode = UnlockCursor;
            dDrawOptions.Border = ShowWindowBorders;
            dDrawOptions.Resizeable = ResizableWindow;
            dDrawOptions.D3d9Linear = EnableD3d9Linear;
            dDrawOptions.NoActivateApp = NoActivateApp;
            dDrawOptions.HandleMouse = HandleMouse;
            dDrawOptions.NonExclusive = NonExclusive;
            dDrawOptions.SingleCpu = ForceCpu0Affinity;

            //combo box's
            dDrawOptions.MaxFps = SelectedMaxFps;
            dDrawOptions.MaxGameTicks = SelectedMaxGameTicks;
            dDrawOptions.SaveSettings = SelectedSaveWindowPosition;
            dDrawOptions.Renderer = SelectedRenderer;
            dDrawOptions.Hook = SelectedHook;
            dDrawOptions.Shader = SelectedShader;
            dDrawOptions.MinFps = SelectedMinFps;
            switch (SelectedMode)
            {
                case "borderless":
                    dDrawOptions.Fullscreen = true;
                    dDrawOptions.Windowed = true;
                    break;
                case "windowed":
                    dDrawOptions.Fullscreen = false;
                    dDrawOptions.Windowed = true;
                    break;
                case "fullscreen":
                    dDrawOptions.Fullscreen = true;
                    dDrawOptions.Windowed = false;
                    break;
                default:
                    dDrawOptions.Fullscreen = true;
                    dDrawOptions.Windowed = false;
                    break;
            }
            _localStorage.Update(StorageKey.DdrawOptions, dDrawOptions);
        }

        private void LoadDDrawStorage()
        {
            LoadDDrawTextBoxOptions();
            LoadDDrawComboBoxOptions();
            LoadDDrawCheckBoxOptions();
        }

        private void UpdateDDrawStorage()
        {
            SaveDDrawOptions();
        }

        public void ResetDdrawOptionsToDefaults()
        {
            var defaultDdrawOptions = Constants.Ddraw.DefaultDdrawOptions;

            // Optionally, you could update the settings in local storage if needed
            _localStorage.Update(StorageKey.DdrawOptions, defaultDdrawOptions);

            // Write the default options to ddraw.ini file
            _ddrawHelpers.WriteDdrawOptions();
            LoadDDrawStorage();
        }

        private void CloseView()
        {
            //save LauncherArgs Storage
            UpdateLauncherArgsStorage();
            //save 
            UpdateDDrawStorage();
            //write ddrawstorage to .ini
            _ddrawHelpers.WriteDdrawOptions();

            // Sending a message to anyone who's listening for NavigationMessage
            Messenger.Default.Send(new NavigationMessage { Action = NavigationAction.GoBack });
        }
    }
}