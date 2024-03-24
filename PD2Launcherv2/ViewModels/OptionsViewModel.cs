using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using PD2Launcherv2.Enums;
using PD2Launcherv2.Helpers;
using PD2Launcherv2.Interfaces;
using ProjectDiablo2Launcherv2.Models;
using ProjectDiablo2Launcherv2;
using System.Diagnostics;

namespace PD2Launcherv2.ViewModels
{
    public class OptionsViewModel : ViewModelBase
    {
        private readonly ILocalStorage _localStorage;
        public OptionsViewModel(ILocalStorage localStorage)
        {
            _localStorage = localStorage;
            OptionsModePicker = Constants.ModePickerItems();
            MaxFpsPickerItems = Constants.MaxFpsPickerItems();
            CloseCommand = new RelayCommand(CloseView);
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



        private void CloseView()
        {
            // Sending a message to anyone who's listening for NavigationMessage
            Messenger.Default.Send(new NavigationMessage { Action = NavigationAction.GoBack });
        }
    }
}