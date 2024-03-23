using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using PD2Launcherv2.Enums;
using PD2Launcherv2.Helpers;
using PD2Launcherv2.Interfaces;
using ProjectDiablo2Launcherv2.Models;

namespace PD2Launcherv2.ViewModels
{
    public class OptionsViewModel : ViewModelBase
    {
        private readonly ILocalStorage _localStorage;
        public OptionsViewModel(ILocalStorage localStorage)
        {
            CloseCommand = new RelayCommand(CloseView);
            _localStorage = localStorage;
        }

        private List<DisplayValuePair> _optionsModePicker;
        public List<DisplayValuePair> OptionsModePicker
        {
            get { return _optionsModePicker; }
            set
            {
                _optionsModePicker = value;
                OnPropertyChanged(nameof(OptionsModePicker));
            }
        }

        private void CloseView()
        {
            // Sending a message to anyone who's listening for NavigationMessage
            Messenger.Default.Send(new NavigationMessage { Action = NavigationAction.GoBack });
        }
    }
}