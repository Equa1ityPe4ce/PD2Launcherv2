using Force.Crc32;
using PD2Launcherv2.Interfaces;
using PD2Launcherv2.Models;
using ProjectDiablo2Launcherv2;
using ProjectDiablo2Launcherv2.Models;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

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

        public async Task DownloadFileAsync(string mediaLink, string path)
        {
            var response = await _httpClient.GetAsync(mediaLink, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await response.Content.CopyToAsync(fileStream);
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

        public async Task UpdateFilesCheck(ILocalStorage _localStorage)
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
                var cloudFileItems = await GetCloudFileMetadataAsync(fileUpdateModel.Client);

                foreach (var cloudFile in cloudFileItems)
                {
                    if (cloudFile.Name.EndsWith("/")) // Skip directory markers so I dont try to download a folder
                    {
                        var directPath = Path.Combine(fullUpdatePath, cloudFile.Name.TrimEnd('/'));
                        Directory.CreateDirectory(directPath);
                        continue;
                    }

                    var localFilePath = Path.Combine(fullUpdatePath, cloudFile.Name);
                    var directoryPath = Path.GetDirectoryName(localFilePath);
                    Directory.CreateDirectory(directoryPath); // Ensure directory for the file exists

                    var installFilePath = Path.Combine(installPath, cloudFile.Name);
                    var installDirectoryPath = Path.GetDirectoryName(installFilePath);
                    Directory.CreateDirectory(installDirectoryPath); // Ensure directory in parent ProjectD2 folder

                    // Determine if the file is to be excluded only if it already exists
                    bool shouldExclude = IsFileExcluded(cloudFile.Name) && File.Exists(installFilePath);

                    // Download and update the file if needed, and not excluded or does not exist
                    if (!shouldExclude && (!File.Exists(localFilePath) || !CompareCRC(localFilePath, cloudFile.Crc32c)))
                    {
                        Debug.WriteLine($"Updating file: {cloudFile.Name}");
                        await DownloadFileAsync(cloudFile.MediaLink, localFilePath);
                    }

                    // Copy the file to parent ProjectD2 folder if it was updated or does not exist
                    if (!shouldExclude || !File.Exists(installFilePath))
                    {
                        File.Copy(localFilePath, installFilePath, true);
                    }
                }
            }
            else
            {
                Debug.WriteLine("FileUpdateModel is not set or directory does not exist.");
            }
            Debug.WriteLine("end UpdateFilesCheck \n");
        }

        public async Task UpdateLauncherCheck(ILocalStorage _localStorage)
        {
            Debug.WriteLine("\nstart UpdateLauncherCheck");
            var fileUpdateModel = _localStorage.LoadSection<FileUpdateModel>(StorageKey.FileUpdateModel);
            var installPath = Directory.GetCurrentDirectory();

            if (fileUpdateModel != null)
            {
                var cloudFileItems = await GetCloudFileMetadataAsync(fileUpdateModel.Launcher);

                foreach (var cloudFile in cloudFileItems)
                {
                    if (cloudFile.Name.EndsWith("/")) // Skip directory markers so I dont try to download a folder
                    {
                        var directPath = Path.Combine(installPath, cloudFile.Name.TrimEnd('/'));
                        Directory.CreateDirectory(directPath);
                        continue;
                    }

                    var localFilePath = Path.Combine(installPath, cloudFile.Name);
                    var launcherNeedsUpdate = false;

                    // Download and update the file if needed, and not excluded or does not exist
                    if (!File.Exists(localFilePath) || !CompareCRC(localFilePath, cloudFile.Crc32c))
                    {
                        // Launcher update found
                        if (cloudFile.Name == "PD2Launcher.exe")
                        {
                            // Update after downloading the updater.exe
                            launcherNeedsUpdate = true;
                        }
                        else
                        {
                            Debug.WriteLine($"Updating file: {cloudFile.Name}");
                            await DownloadFileAsync(cloudFile.MediaLink, localFilePath);
                        }
                    }

                    if (launcherNeedsUpdate)
                    {
                        var updaterPath = Path.Combine(installPath, "updater.exe");
                        if (File.Exists(updaterPath))
                        {
                            // Launch the updater and kill current process
                            var startInfo = new ProcessStartInfo
                            {
                                FileName = updaterPath,
                                Arguments = "/l",
                                WorkingDirectory = installPath
                            };

                            if (Process.Start(startInfo) != null)
                            {
                                System.Environment.Exit(0);
                            }
                        }
                    }
                }
            }
            else
            {
                Debug.WriteLine("FileUpdateModel is not set or directory does not exist.");
            }
            Debug.WriteLine("end UpdateLauncherCheck \n");
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