using Force.Crc32;
using Newtonsoft.Json;
using PD2Launcherv2.Interfaces;
using PD2Launcherv2.Models;
using ProjectDiablo2Launcherv2.Models;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace PD2Launcherv2.Helpers
{
    public class FilterHelpers
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorage _localStorage;
        private const string FilterAuthorUrl = "https://raw.githubusercontent.com/Project-Diablo-2/LootFilters/main/filters.json";

        public FilterHelpers(HttpClient httpClient, ILocalStorage localStorage)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
        }
        private async Task<HttpResponseMessage> GetAsync(string url, string eTag = null)
        {
            Debug.WriteLine($"in GetAsync {url}");
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (!string.IsNullOrWhiteSpace(eTag))
            {
                request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue($"\"{eTag}\""));
                request.Headers.Add("User-Agent", "PD2Launcherv2");
            }
            Debug.WriteLine($"eTag sent out {eTag}");
            return await _httpClient.SendAsync(request);
        }

        private async Task<HttpResponseMessage> GetFilterListAsync(string url)
        {
            Debug.WriteLine($"in GetFilterListAsync {url}");
            Debug.WriteLine($"Getting Filter Liost from {url}");
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "PD2Launcherv2");
            return await _httpClient.SendAsync(request);
        }

        public async Task<List<FilterFile>> FetchFilterContentsAsync(string url)
        {
            try
            {
                var response = await GetFilterListAsync(url);
                Debug.WriteLine($"response.content {response.Content}");
                Debug.WriteLine($"{response.StatusCode}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var allFiles = JsonConvert.DeserializeObject<List<FilterFile>>(content);
                    Debug.WriteLine($"content: {content}");
                    Debug.WriteLine($"allFiles: {allFiles}");
                    // Filter the list to include only .filter files and README.md
                    var filterFiles = allFiles.Where(f => f.Name.EndsWith(".filter") || f.Name.Equals("README.md", StringComparison.OrdinalIgnoreCase)).ToList();
                    Debug.WriteLine($"filterFiles {filterFiles}");
                    return filterFiles;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching filter contents: {ex.Message}");
            }

            return null;
        }

        public async Task FetchAndStoreFilterAuthorsAsync()
        {
            Debug.WriteLine("\nstart FetchAndStoreFilterAuthorsAsync");
            try
            {
                var storedData = _localStorage.LoadSection<Pd2AuthorList>(StorageKey.Pd2AuthorList);
                var eTag = storedData?.StorageETag ?? string.Empty;

                var response = await GetAsync(FilterAuthorUrl, eTag);
                Debug.WriteLine($"ETAG: {eTag}");
                Debug.WriteLine($"response.StatusCode {response.StatusCode}");
                Debug.WriteLine($"response.Content {response.Content}");
                Debug.WriteLine($"response.ReasonPhrase {response.ReasonPhrase}");
                Debug.WriteLine($"response.Version {response.Version}");
                // Iterate over all headers
                foreach (var header in response.Headers)
                {
                    Debug.WriteLine($"Header: {header.Key}, Value: {string.Join(", ", header.Value)}");
                }


                if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
                {
                    // Data has not changed; no need to update
                    Console.WriteLine($"Code 304? {System.Net.HttpStatusCode.NotModified}");
                    Console.WriteLine("Filter authors data not changed.");
                    return;
                }

                Debug.WriteLine($"response.IsSuccessStatusCode {response.IsSuccessStatusCode}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var authors = JsonConvert.DeserializeObject<List<FilterAuthor>>(content);
                    Debug.WriteLine("is authors null?: " + authors != null);
                    Debug.WriteLine($"content {content}");
                    Debug.WriteLine($"authors {authors}");
                    Debug.WriteLine($"ETag Header: {response.Headers.ETag?.Tag}");
                    Debug.WriteLine($"2nd ETag Header: {response.Headers.ETag.ToString}");
                    var eTagValue = response.Headers.ETag?.Tag?.Trim('"');
                    Debug.WriteLine($"eTagValue {eTagValue}");
                    if (authors != null)
                    {
                        Pd2AuthorList eTaggedData = new()
                        {
                            StorageETag = eTagValue,
                            StorageAuthorList = authors
                        };

                        // Serialize eTaggedData to JSON for debugging
                        string eTaggedDataJson = JsonConvert.SerializeObject(eTaggedData, Formatting.Indented);
                        Debug.WriteLine($"eTaggedData JSON: {eTaggedDataJson}");

                        Debug.WriteLine($"eTaggedData: {eTaggedData}");
                        Debug.WriteLine($"final response.Headers.ETag?.Tag,: {response.Headers.ETag?.Tag}");

                        _localStorage.Update(StorageKey.Pd2AuthorList, eTaggedData);
                        Console.WriteLine("Filter authors data updated.");
                    }
                }
                else
                {
                    Console.WriteLine("Failed to fetch filter authors.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching filter authors: {ex.Message}");
            }
            Debug.WriteLine("end FetchAndStoreFilterAuthorsAsync\n");
        }

        // This method handles the actual file download
        private async Task<bool> DownloadFileAsync(string downloadUrl, string targetPath)
        {
            Debug.WriteLine($"DownloadFileAsync start");
            Debug.WriteLine($"downloadUrl {downloadUrl}");
            Debug.WriteLine($"targetPath {targetPath}");
            try
            {
                var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                Debug.WriteLine($"response {response.Content}");
                Debug.WriteLine($"response {response.StatusCode}");

                using (var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    Debug.WriteLine($"fileStream {fileStream}");
                    await response.Content.CopyToAsync(fileStream);
                }

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> ApplyLootFilterAsync(string author, string filterName, string downloadUrl)
        {
            try
            {
                var fileUpdateModel = _localStorage.LoadSection<FileUpdateModel>(StorageKey.FileUpdateModel);
                if (fileUpdateModel == null || string.IsNullOrWhiteSpace(fileUpdateModel.FilePath))
                {
                    Debug.WriteLine("Base path is not set.");
                    return false;
                }

                string basePath = fileUpdateModel.FilePath;
                string filtersBasePath = Path.Combine(basePath, "filters");
                string localPath = Path.Combine(filtersBasePath, "local");
                string onlinePath = Path.Combine(filtersBasePath, "online");
                string defaultFilterPath = Path.Combine(basePath, "loot.filter");

                // Ensure necessary directories exist
                Directory.CreateDirectory(localPath);
                Directory.CreateDirectory(onlinePath);

                string targetFilterPath;
                if (author.Equals("local", StringComparison.OrdinalIgnoreCase))
                {
                    targetFilterPath = Path.Combine(localPath, filterName);
                }
                else
                {
                    targetFilterPath = Path.Combine(onlinePath, filterName);
                    // Directly download the filter file for online sources if it does not exist
                    if (!File.Exists(targetFilterPath))
                    {
                        bool downloadSuccess = await DownloadFileAsync(downloadUrl, targetFilterPath);
                        if (!downloadSuccess)
                        {
                            Debug.WriteLine("Failed to download or update the filter file.");
                            return false;
                        }
                    }
                }

                // Create or update the symbolic link for the loot filter
                if (File.Exists(defaultFilterPath))
                {
                    File.Delete(defaultFilterPath);
                }
                File.CreateSymbolicLink(defaultFilterPath, targetFilterPath);

                Debug.WriteLine("Filter applied successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying loot filter: {ex.Message}");
                return false;
            }
        }
    }
}