using Newtonsoft.Json;
using PD2Launcherv2.Interfaces;
using PD2Launcherv2.Models;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Globalization;

namespace PD2Launcherv2.Helpers
{
    public class NewsHelpers
    {
        private readonly HttpClient _httpClient;
        public NewsHelpers()
        {
            _httpClient = (HttpClient)App.ServiceProvider.GetService(typeof(HttpClient));
        }

        public async Task FetchAndStoreNewsAsync(ILocalStorage _localStorage)
        {
            Debug.WriteLine("\nstart FetchAndStoreNewsAsync");
            try
            {
                string newsUrl = "https://raw.githubusercontent.com/Project-Diablo-2/news/main/news.json";

                // Load the current news data, if available
                var currentNewsData = _localStorage.LoadSection<News>(StorageKey.News);

                using var httpClient = new HttpClient();
                var requestMessage = new HttpRequestMessage(HttpMethod.Get, newsUrl);

                // Include the ETag in the request if available
                if (!string.IsNullOrEmpty(currentNewsData?.ETag) && currentNewsData.ETag != "null")
                {
                    requestMessage.Headers.IfNoneMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue($"\"{currentNewsData.ETag}\""));
                }

                var response = await httpClient.SendAsync(requestMessage);

                if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
                {
                    Debug.WriteLine("News data not changed.");
                    return;
                }

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var newsItems = JsonConvert.DeserializeObject<List<NewsItem>>(content);

                    if (newsItems != null)
                    {
                        foreach (var item in newsItems)
                        {
                            // Convert the date string to a DateTime object
                            if (DateTime.TryParse(item.Date, null, DateTimeStyles.AssumeUniversal, out var date))
                            {
                                // Convert the DateTime object to a more readable string format
                                item.Date = date.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture);
                            }
                        }
                        var eTagValue = response.Headers.ETag?.Tag?.Trim('"');

                        // Update the news data and ETag in local storage
                        var newNewsData = new News
                        {
                            ETag = eTagValue,
                            news = newsItems
                        };

                        _localStorage.Update(StorageKey.News, newNewsData);
                        Debug.WriteLine("News data and ETag updated.");
                    }
                }
                else
                {
                    Debug.WriteLine($"Failed to fetch news. Status Code: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching news: {ex.Message}");
            }
            Debug.WriteLine("end FetchAndStoreNewsAsync\n");
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

        public async Task<ResetInfo> FetchResetInfoAsync(ILocalStorage _localStorage)
        {
            Debug.WriteLine("\n\n Start FetchResetInfoAsync");
            string resetUrl = "https://raw.githubusercontent.com/PritchardJasonR/news/main/reset.json";

            // Load the current reset info data, if available
            var currentResetInfoData = _localStorage.LoadSection<ResetInfo>(StorageKey.ResetInfo);

            var requestMessage = new HttpRequestMessage(HttpMethod.Get, resetUrl);

            // Include the ETag in the request if available
            if (!string.IsNullOrEmpty(currentResetInfoData?.ETag) && currentResetInfoData.ETag != "null")
            {
                requestMessage.Headers.IfNoneMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue($"\"{currentResetInfoData.ETag}\""));
            }

            var response = await _httpClient.SendAsync(requestMessage);
            Debug.WriteLine($"response.StatusCode {response.StatusCode}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
            {
                Debug.WriteLine("Reset data not changed.");
                return currentResetInfoData; // or return null if you don't need to update the UI
            }

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var resetItem = JsonConvert.DeserializeObject<ResetItem>(content);
                var eTagValue = response.Headers.ETag?.Tag?.Trim('"');
                Debug.WriteLine($"new eTagValue {eTagValue}");

                // Update the reset data and ETag in local storage
                var newResetInfoData = new ResetInfo
                {
                    ETag = eTagValue,
                    ResetData = resetItem
                };

                _localStorage.Update(StorageKey.ResetInfo, newResetInfoData);
                Debug.WriteLine("Reset data and ETag updated.");
                return newResetInfoData;
            }
            else
            {
                Debug.WriteLine($"Failed to fetch reset info. Status Code: {response.StatusCode}");
                return null;
            }
            Debug.WriteLine("FetchResetInfoAsync end\n\n");
        }
    }
}