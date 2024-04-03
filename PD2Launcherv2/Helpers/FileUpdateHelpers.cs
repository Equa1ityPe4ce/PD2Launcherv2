﻿using Force.Crc32;
using PD2Launcherv2.Interfaces;
using PD2Launcherv2.Models;
using ProjectDiablo2Launcherv2;
using ProjectDiablo2Launcherv2.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows;

namespace PD2Launcherv2.Helpers
{
    public class FileUpdateHelpers
    {
        private readonly HttpClient _httpClient;
        private readonly List<string> _excludedFiles = Constants.excludedFiles;

        public FileUpdateHelpers(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public void StartUpdateProcess()
        {
            string launcherDirectory = Directory.GetCurrentDirectory();
            string updateUtilityPath = Path.Combine(launcherDirectory, "UpdateUtility.exe");
            string mainLauncherPath = Path.Combine(launcherDirectory, "PD2Launcher.exe");
            string tempLauncherPath = Path.Combine(launcherDirectory, "TempLauncher.exe");
            Debug.WriteLine(File.Exists(updateUtilityPath));
            Debug.WriteLine($"\n\n\nupdateUtilityPath: {updateUtilityPath}");
            Debug.WriteLine($"mainLauncherPath: {mainLauncherPath}");
            Debug.WriteLine($"mainLauncherPath: {mainLauncherPath}");
            Debug.WriteLine($"tempLauncherPath: {tempLauncherPath}\n\n\n");

            if (File.Exists(updateUtilityPath) && File.Exists(tempLauncherPath))
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = updateUtilityPath,
                    Arguments = $"\"{mainLauncherPath}\" \"{tempLauncherPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                Process.Start(startInfo);
            }
            else
            {
                Debug.WriteLine("Update Utility or TempLauncher.exe not found.");
            }
        }

        public async Task<List<CloudFileItem>> GetCloudFileMetadataAsync(string cloudFileBucket)
        {
            var response = await _httpClient.GetAsync(cloudFileBucket);
            response.EnsureSuccessStatusCode();
            Debug.WriteLine($"code: {response.StatusCode}");

            var content = await response.Content.ReadAsStringAsync();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var json = JsonSerializer.Deserialize<CloudFilesResponse>(content, options);

            return json?.Items.Select(i => new CloudFileItem
            {
                Name = i.Name,
                MediaLink = i.MediaLink,
                Crc32c = i.Crc32c,
            }).ToList() ?? new List<CloudFileItem>();
        }

        public async Task DownloadFileAsync(string mediaLink, string path, IProgress<double> progress = null)
        {
            var response = await _httpClient.GetAsync(mediaLink, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L; // Total download size
            var totalBytesRead = 0L;
            var buffer = new byte[8192]; // Buffer size

            await using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            await using (var stream = await response.Content.ReadAsStreamAsync())
            {
                var bytesRead = 0;
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalBytesRead += bytesRead;

                    // Report progress as a percentage of total download
                    // this will report progress as a continuous stream of data
                    progress?.Report(totalBytes != -1 ? (double)totalBytesRead / totalBytes : totalBytesRead);
                }
            }
        }

        public uint Crc32CFromFile(string filePath)
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var memoryStream = new MemoryStream();
            fileStream.CopyTo(memoryStream);
            byte[] fileBytes = memoryStream.ToArray();

            return Crc32CAlgorithm.Compute(fileBytes);
        }

        /**
         * Method for checking if a file needs to be updated
         */
        public bool CompareCRC(string filePath, string crcHash)
        {
            uint localCrc = Crc32CFromFile(filePath);
            byte[] remoteCrcBytes = Convert.FromBase64String(crcHash);
            uint remoteCrc = BitConverter.ToUInt32(remoteCrcBytes.Reverse().ToArray());
            return localCrc == remoteCrc;
        }

        public async Task UpdateFilesCheck(ILocalStorage _localStorage, IProgress<double> progress, Action onDownloadComplete)
        {
            Debug.WriteLine("\nstart UpdateFilesCheck");
            var fileUpdateModel = _localStorage.LoadSection<FileUpdateModel>(StorageKey.FileUpdateModel);
            var installPath = Directory.GetCurrentDirectory();
            var fullUpdatePath = Path.Combine(installPath, fileUpdateModel.FilePath);
            if (!Directory.Exists(fullUpdatePath))
            {
                Directory.CreateDirectory(fullUpdatePath);
            }

            if (fileUpdateModel != null)
            {
                try
                {
                    var cloudFileItems = await GetCloudFileMetadataAsync(fileUpdateModel.Client);
                    int totalFiles = cloudFileItems.Count;
                    int processedFiles = 0;

                    foreach (var cloudFile in cloudFileItems)
                    {
                        if (cloudFile.Name.EndsWith("/"))
                        {
                            continue;
                        }

                        var localFilePath = Path.Combine(fullUpdatePath, cloudFile.Name);
                        var directoryPath = Path.GetDirectoryName(localFilePath);
                        if (!Directory.Exists(directoryPath))
                        {
                            Directory.CreateDirectory(directoryPath);
                        }

                        var installFilePath = Path.Combine(installPath, cloudFile.Name);
                        var installDirectoryPath = Path.GetDirectoryName(installFilePath);
                        if (!Directory.Exists(installDirectoryPath))
                        {
                            Directory.CreateDirectory(installDirectoryPath);
                        }

                        bool shouldExclude = IsFileExcluded(cloudFile.Name) && File.Exists(installFilePath);
                        if (!shouldExclude && (!File.Exists(localFilePath) || !CompareCRC(localFilePath, cloudFile.Crc32c)))
                        {
                            Debug.WriteLine($"Updating file: {cloudFile.Name}");
                            await DownloadFileAsync(cloudFile.MediaLink, localFilePath);
                        }

                        if (!shouldExclude || !File.Exists(installFilePath))
                        {
                            File.Copy(localFilePath, installFilePath, true);
                        }

                        processedFiles++;
                        progress?.Report((double)processedFiles / totalFiles);
                    }

                    onDownloadComplete?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"An error occurred while updating files: {ex.Message}");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ShowErrorMessage($"An error occurred while updating files: {ex.Message}\nPlease verify your game is closed and try again.");
                    });
                }
            }
            else
            {
                Debug.WriteLine("FileUpdateModel is not set or directory does not exist.");
            }
            Debug.WriteLine("end UpdateFilesCheck \n");
        }

        private void ShowErrorMessage(string message)
        {

            MessageBox.Show(message, "Error Updating Files", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public async Task UpdateLauncherCheck(ILocalStorage _localStorage, IProgress<double> progress = null, Action onDownloadComplete = null)
        {
            Debug.WriteLine("\n\n\n\nstart UpdateLauncherCheck");
            var fileUpdateModel = _localStorage.LoadSection<FileUpdateModel>(StorageKey.FileUpdateModel);
            var installPath = Directory.GetCurrentDirectory();
            var tempDownloadPath = Path.Combine(installPath, "TempLauncher.exe");

            // Step 1: Check and delete existing TempLauncher.exe
            if (File.Exists(tempDownloadPath))
            {
                File.Delete(tempDownloadPath);
            }

            if (fileUpdateModel != null)
            {
                var cloudFileItems = await GetCloudFileMetadataAsync(fileUpdateModel.Launcher);

                foreach (var cloudFile in cloudFileItems)
                {
                    // Skip directory markers and non-launcher files
                    if (cloudFile.Name.EndsWith("/") || cloudFile.Name != "PD2Launcher.exe")
                    {
                        continue;
                    }

                    var localFilePath = Path.Combine(installPath, cloudFile.Name);
                    var launcherNeedsUpdate = !File.Exists(localFilePath) || !CompareCRC(localFilePath, cloudFile.Crc32c);
                    if (launcherNeedsUpdate)
                    {
                        // Step 2: Notify user about the update
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show("A launcher update has been identified and is starting now.", "Update Ready", MessageBoxButton.OK, MessageBoxImage.Information);
                        });

                        // Step 3: Download the update
                        await DownloadFileAsync(cloudFile.MediaLink, tempDownloadPath, progress);

                        // Step 4: Close launcher and initiate the update process
                        await Application.Current.Dispatcher.Invoke(async () =>
                        {
                            // Notify user that the application will close for the update
                            MessageBox.Show("The launcher will now close to apply an update please wait for launcher to reopen.", "Update in Progress", MessageBoxButton.OK, MessageBoxImage.Information);

                           // Start the update process
                            StartUpdateProcess();

                            // Close the application or hide the main window
                            Application.Current.MainWindow?.Close();

                            // Wait a moment for the main window to close properly
                            await Task.Delay(1000); // Adjust this delay as needed

                            // Optionally, invoke the onDownloadComplete action if you have any additional steps to run after this
                            onDownloadComplete?.Invoke();
                        });

                        return; // Exit the method after initiating the update process
                    }
                }
            }
            else
            {
                Debug.WriteLine("FileUpdateModel is not set or directory does not exist.");
            }
            Debug.WriteLine("end UpdateLauncherCheck \n");
        }

        public async Task DownloadLauncherUpdateAsync(string updateUrl, string tempDownloadPath, IProgress<double> progress = null)
        {
            // Delete existing TempLauncher.exe if it exists
            if (File.Exists(tempDownloadPath))
            {
                File.Delete(tempDownloadPath);
            }

            // Inform the user - this should be done in the UI, not here
            // MessageBox.Show("A launcher update has been identified and is starting now.");

            using (var response = await _httpClient.GetAsync(updateUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var totalBytesRead = 0L;
                var buffer = new byte[4096];
                var isMoreToRead = true;

                using (var fileStream = new FileStream(tempDownloadPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    do
                    {
                        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0)
                        {
                            isMoreToRead = false;
                            continue;
                        }

                        await fileStream.WriteAsync(buffer, 0, bytesRead);

                        totalBytesRead += bytesRead;
                        if (totalBytes > 0)
                        {
                            progress?.Report((double)totalBytesRead / totalBytes);
                        }
                    }
                    while (isMoreToRead);
                }
            }

            // Close launcher and initiate update process
            // This would typically involve starting the secondary utility you created earlier
        }

        private bool IsFileExcluded(string fileName)
        {
            return _excludedFiles.Any(excluded =>
                excluded.EndsWith("/*") && fileName.StartsWith(excluded.TrimEnd('*', '/')) ||
                excluded.Equals(fileName, StringComparison.OrdinalIgnoreCase) ||
                (excluded.Contains("*") && new Regex("^" + Regex.Escape(excluded).Replace("\\*", ".*") + "$").IsMatch(fileName)));
        }

    }

    public class CloudFileItem
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("mediaLink")]
        public string MediaLink { get; set; }

        [JsonPropertyName("crc32c")]
        public string Crc32c { get; set; }
    }

    public class CloudFilesResponse
    {
        [JsonPropertyName("items")]
        public List<CloudFileItem> Items { get; set; }
    }
}