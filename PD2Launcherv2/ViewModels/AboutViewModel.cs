using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using PD2Launcherv2.Enums;
using PD2Launcherv2.Helpers;
using PD2Launcherv2.Interfaces;
using PD2Launcherv2.Models;
using ProjectDiablo2Launcherv2.Models;
using System.Diagnostics;
using System.IO;
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
        public RelayCommand UpdateFilesCall { get; private set; }
        public RelayCommand ReadStorageCall { get; private set; }

        public AboutViewModel(ILocalStorage localStorage)
        {
            _localStorage = localStorage;
            _fileUpdateHelpers = new FileUpdateHelpers(new HttpClient());

            ProdBucket = new RelayCommand(ProdBucketAssign);
            BetaBucket = new RelayCommand(BetaBucketAssign);
            UpdateFilesCall = new RelayCommand(async () => await UpdateFilesCheck());
            ReadStorageCall = new RelayCommand(ReadStorageCheck);

            CloseCommand = new RelayCommand(CloseView);
        }


        public void ProdBucketAssign()
        {
            Debug.WriteLine("\nstart ProdBucketAssign");
            var fileUpdateModel = new FileUpdateModel
            {
                Client = "https://storage.googleapis.com/storage/v1/b/pd2-client-files/o",
                FilePath = "E:\\Games\\Blizzard\\d2\\Diablo II\\ProjectD2"
            };
            _localStorage.Update(StorageKey.FileUpdateModel, fileUpdateModel);
            Debug.WriteLine("end ProdBucketAssign\n");
        }
        public void BetaBucketAssign()
        {
            Debug.WriteLine("\nstart BetaBucketAssign");
            var fileUpdateModel = new FileUpdateModel
            {
                Client = "https://storage.googleapis.com/storage/v1/b/pd2-beta-client-files/o",
                FilePath = "E:\\Games\\Blizzard\\d2\\Diablo II\\ProjectD2Beta"
            };
            _localStorage.Update(StorageKey.FileUpdateModel, fileUpdateModel);
            Debug.WriteLine("end BetaBucketAssign \n");
        }
        public async Task UpdateFilesCheck()
        {
            Debug.WriteLine("\nstart UpdateFilesCheck");
            var fileUpdateModel = _localStorage.LoadSection<FileUpdateModel>(StorageKey.FileUpdateModel);
            if (fileUpdateModel != null && Directory.Exists(fileUpdateModel.FilePath))
            {
                var cloudFileItems = await _fileUpdateHelpers.GetCloudFileMetadataAsync(fileUpdateModel.Client);

                foreach (var cloudFile in cloudFileItems)
                {
                    // Skip directory markers
                    if (cloudFile.Name.EndsWith("/"))
                    {
                        // Ensure the directory structure is created for directory markers
                        var directPath = Path.Combine(fileUpdateModel.FilePath, cloudFile.Name.TrimEnd('/'));
                        if (!Directory.Exists(directPath))
                        {
                            Directory.CreateDirectory(directPath);
                        }
                        continue;
                    }

                    var localFilePath = Path.Combine(fileUpdateModel.FilePath, cloudFile.Name);

                    // Ensure the directory for the file exists
                    var directoryPath = Path.GetDirectoryName(localFilePath);
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    // Download and update the file if needed
                    if (!_fileUpdateHelpers.CompareCRC(localFilePath, cloudFile.Crc32c))
                    {
                        Debug.WriteLine($"Updating file: {cloudFile.Name}");
                        await _fileUpdateHelpers.DownloadFileAsync(cloudFile.MediaLink, localFilePath);
                    }
                    else
                    {
                    }
                }
            }
            else
            {
                Debug.WriteLine("FileUpdateModel is not set or directory does not exist.");
            }
            Debug.WriteLine("end UpdateFilesCheck \n");
        }


        public void ReadStorageCheck()
        {
            Debug.WriteLine("\nstart ReadStorageCheck");
            var fileUpdateModel = _localStorage.LoadSection<FileUpdateModel>(StorageKey.FileUpdateModel);
            if (fileUpdateModel != null)
            {
                Debug.WriteLine($"Client: {fileUpdateModel.Client}");
                Debug.WriteLine($"FilePath: {fileUpdateModel.FilePath}");
            }
            else
            {
                Debug.WriteLine("FileUpdateModel is not set.");
            }
            Debug.WriteLine("end ReadStorageCheck \n");
        }

        private void CloseView()
        {
            Messenger.Default.Send(new NavigationMessage { Action = NavigationAction.GoBack });
        }
    }
}