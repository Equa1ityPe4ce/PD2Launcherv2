using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight;
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
        private readonly ILocalStorage _localStorage;
        public Dictionary<string, bool> CheckboxStates { get; set; }

        public RelayCommand ToggleAdvancedOptionsCommand { get; }

        public OptionsViewModel(ILocalStorage localStorage)
        {
            _localStorage = localStorage;
            CheckboxStates = new Dictionary<string, bool>();
            LoadLauncherArgs();
            OptionsModePicker = Constants.ModePickerItems();
            MaxFpsPickerItems = Constants.MaxFpsPickerItems();
            MaxGameTicksPickerItems = Constants.MaxGameTicksPickerItems();
            SaveWindowPositionPickerItems = Constants.SaveWindowPositionPickerItems();
            RendererPickerItems = Constants.RendererPickerItems();
            HookPickerItems = Constants.HookPickerItems();
            MinFpsPickerItems = Constants.MinFpsPickerItems();
            ShaderPickerItems = Constants.ShaderPickerItems();
            CloseCommand = new RelayCommand(CloseView);
            ToggleAdvancedOptionsCommand = new RelayCommand(ToggleAdvancedOptions);
        }

        private string _selectedMode;
        public string SelectedMode
        {
            get => _selectedMode;
            set
            {
                if (_selectedMode != value)
                {
                    _selectedMode = value;
                    OnPropertyChanged(nameof(SelectedMode));
                    OnPropertyChanged(nameof(NonFullScreenVisibility));
                }
            }
        }

        public Visibility NonFullScreenVisibility
        {
            get => string.Equals(SelectedMode, "fullscreen", StringComparison.OrdinalIgnoreCase)
                ? Visibility.Collapsed : Visibility.Visible;
        }

        private bool _showAdvancedOptions = false;
        public Visibility AdvancedOptionsVisibility => ShowAdvancedOptions ? Visibility.Visible : Visibility.Collapsed;
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
        public Visibility DDrawControlsVisible => IsDdrawSelected ? Visibility.Visible : Visibility.Collapsed;

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

        private string _width;
        public string Width
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

        private string _height;
        public string Height
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

        private string _ddrawPosX;
        public string DdrawPosX
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

        private string _ddrawPosY;
        public string DdrawPosY
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

        private void CloseView()
        {
            //save LauncherArgs Storage
            UpdateLauncherArgsStorage();

            // Sending a message to anyone who's listening for NavigationMessage
            Messenger.Default.Send(new NavigationMessage { Action = NavigationAction.GoBack });
        }
    }
}