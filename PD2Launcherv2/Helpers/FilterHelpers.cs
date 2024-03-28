
using Newtonsoft.Json;
using PD2Launcherv2.Interfaces;
using PD2Launcherv2.Models;
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
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (!string.IsNullOrWhiteSpace(eTag))
            {
                request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue($"\"{eTag}\""));
                request.Headers.Add("User-Agent", "PD2Launcherv2");
            }
            return await _httpClient.SendAsync(request);
        }

        private async Task<HttpResponseMessage> GetFilterListAsync(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "PD2Launcherv2");
            return await _httpClient.SendAsync(request);
        }

        public async Task<List<FilterFile>> FetchFilterContentsAsync(string url)
        {
            try
            {
                var response = await GetFilterListAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var allFiles = JsonConvert.DeserializeObject<List<FilterFile>>(content);
                    var filterFiles = allFiles.Where(f => f.Name.EndsWith(".filter")).ToList();
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
            try
            {
                var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                using (var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
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
                string installPath = Directory.GetCurrentDirectory();
                string filtersBasePath = Path.Combine(installPath, "filters");
                string localPath = Path.Combine(filtersBasePath, "local");
                string onlinePath = Path.Combine(filtersBasePath, "online");
                string defaultFilterPath = Path.Combine(installPath, "loot.filter");

                // Ensure necessary directories exist
                Directory.CreateDirectory(localPath);
                Directory.CreateDirectory(onlinePath);
                string targetFilterPath;
                if (author.Equals("Local Filter", StringComparison.OrdinalIgnoreCase))
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

                // Create or update the loot filter
                File.Copy(targetFilterPath, defaultFilterPath, true);

                Debug.WriteLine("Filter applied successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying loot filter: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CheckAndUpdateFilterAsync(SelectedAuthorAndFilter selected)
        {
            try
            {
                // Use GetFilterListAsync to ensure User-Agent is set and to handle request consistently
                var filterListResponse = await GetFilterListAsync(selected.selectedAuthor.Url);

                if (!filterListResponse.IsSuccessStatusCode)
                {
                    return false;
                }

                var filterListContent = await filterListResponse.Content.ReadAsStringAsync();
                var filters = JsonConvert.DeserializeObject<List<FilterFile>>(filterListContent);

                var targetFilter = filters?.FirstOrDefault(f => f.Name.Equals(selected.selectedFilter.Name, StringComparison.OrdinalIgnoreCase));
                if (targetFilter == null)
                {
                    return false;
                }

                if (targetFilter.Sha.Equals(selected.selectedFilter.Sha, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine("The filter is up-to-date.");
                    return true;
                }
                return await ApplyLootFilterAsync(selected.selectedAuthor.Name, selected.selectedFilter.Name, targetFilter.DownloadUrl);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking and updating filter: {ex.Message}");
                return false;
            }
            finally
            {
                Debug.WriteLine("CheckAndUpdateFilterAsync end");
            }
        }
    }
}