using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using PD2Launcherv2.Enums;
using PD2Launcherv2.Helpers;
using PD2Launcherv2.Interfaces;
using PD2Launcherv2.Messages;
using PD2Launcherv2.Models;
using ProjectDiablo2Launcherv2;
using ProjectDiablo2Launcherv2.Models;
using System.Diagnostics;
using System.Net.Http;

namespace PD2Launcherv2.ViewModels
{
    public class AboutViewModel : ViewModelBase
    {
        public string cloudFileBucket { get; set; }
        public string folderPath { get; set; }
        private readonly ILocalStorage _localStorage;
        private readonly FileUpdateHelpers _fileUpdateHelpers;

        public RelayCommand ProdBucket { get; private set; }
        public RelayCommand BetaBucket { get; private set; }

        public AboutViewModel(ILocalStorage localStorage)
        {
            _localStorage = localStorage;
            _fileUpdateHelpers = new FileUpdateHelpers(new HttpClient());

            ProdBucket = new RelayCommand(ProdBucketAssign);
            BetaBucket = new RelayCommand(BetaBucketAssign);

            CloseCommand = new RelayCommand(CloseView);
        }

        public void ProdBucketAssign()
        {
            Debug.WriteLine("\nstart ProdBucketAssign");
            var fileUpdateModel = new FileUpdateModel
            {
                Client = "https://storage.googleapis.com/storage/v1/b/pd2-client-files/o",
                Launcher = "https://storage.googleapis.com/storage/v1/b/pd2-beta-launcher-update/o",
                FilePath = "Live"
            };
            _localStorage.Update(StorageKey.FileUpdateModel, fileUpdateModel);
            Messenger.Default.Send(new ConfigurationChangeMessage { IsBeta = false });
            Debug.WriteLine("end ProdBucketAssign\n");
        }
        public void BetaBucketAssign()
        {
            Debug.WriteLine("\nstart BetaBucketAssign");
            var fileUpdateModel = new FileUpdateModel
            {
                Client = "https://storage.googleapis.com/storage/v1/b/pd2-beta-client-files/o",
                Launcher = "https://storage.googleapis.com/storage/v1/b/pd2-beta-launcher-update/o",
                FilePath = "Beta"
            };
            _localStorage.Update(StorageKey.FileUpdateModel, fileUpdateModel);
            Messenger.Default.Send(new ConfigurationChangeMessage { IsBeta = true });
            Debug.WriteLine("end BetaBucketAssign \n");
        }

        private void CloseView()
        {
            Messenger.Default.Send(new NavigationMessage { Action = NavigationAction.GoBack });
        }
    }
}