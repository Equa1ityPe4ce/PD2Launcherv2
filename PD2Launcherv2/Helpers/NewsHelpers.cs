using Newtonsoft.Json;
using PD2Launcherv2.Interfaces;
using PD2Launcherv2.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
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
                string newsUrl = "https://raw.githubusercontent.com/PritchardJasonR/news/main/news.json";

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
    }
}
