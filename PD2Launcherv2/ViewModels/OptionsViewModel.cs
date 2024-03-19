﻿using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using PD2Launcherv2.Enums;
using PD2Launcherv2.Helpers;

namespace PD2Launcherv2.ViewModels
{
    public class OptionsViewModel : ViewModelBase
    {
        public OptionsViewModel()
        {
            CloseCommand = new RelayCommand(CloseView);
        }

        private void CloseView()
        {
            // Sending a message to anyone who's listening for NavigationMessage
            Messenger.Default.Send(new NavigationMessage { Action = NavigationAction.GoBack });
        }
    }
}