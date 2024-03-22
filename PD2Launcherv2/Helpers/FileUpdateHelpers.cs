using Force.Crc32;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PD2Launcherv2.Helpers
{
    internal class FileUpdateHelpers
    {
        private readonly HttpClient _httpClient;
        private readonly string stringToFilePath;

        public FileUpdateHelpers(String filePath, HttpClient httpClient)
        {
            _httpClient = httpClient;
            stringToFilePath = filePath;
        }

        public async Task<List<CloudFileItem>> GetCloudFileMetadataAsync(string cloudFileBucket)
        {
            var response = await _httpClient.GetAsync(cloudFileBucket);
            response.EnsureSuccessStatusCode();
            Debug.WriteLine($"code: {response.StatusCode}");

            var content = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"Response Content: {content}");

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
            Debug.WriteLine($"Downloading file to: {path}");
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