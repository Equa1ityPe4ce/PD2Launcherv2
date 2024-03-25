using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using PD2Launcherv2.Enums;
using PD2Launcherv2.Helpers;
using PD2Launcherv2.Interfaces;
using ProjectDiablo2Launcherv2.Models;
using ProjectDiablo2Launcherv2;
using System.Diagnostics;
using System.Windows;

namespace PD2Launcherv2.ViewModels
{
    public class OptionsViewModel : ViewModelBase
    {
        private readonly ILocalStorage _localStorage;
        public RelayCommand ToggleAdvancedOptionsCommand { get; }

        public OptionsViewModel(ILocalStorage localStorage)
        {
            _localStorage = localStorage;
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

        private void CloseView()
        {
            // Sending a message to anyone who's listening for NavigationMessage
            Messenger.Default.Send(new NavigationMessage { Action = NavigationAction.GoBack });
        }
    }
}