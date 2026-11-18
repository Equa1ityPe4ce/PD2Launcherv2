using Newtonsoft.Json;
using PD2Launcherv2.Models;
using System.Diagnostics;
using System.IO;

namespace PD2Launcherv2.Helpers
{
    /// <summary>
    /// The items a user built in the filter preview and chose to keep. They are shown at the top
    /// of every preview until removed, and live next to the launcher's settings.
    /// </summary>
    public static class BhPreviewSavedItems
    {
        private static readonly string FilePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppData", "bhPreviewItems.json");

        public static List<BhSavedPreviewItem> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new();
                return JsonConvert.DeserializeObject<List<BhSavedPreviewItem>>(File.ReadAllText(FilePath)) ?? new();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not read saved preview items: {ex.Message}");
                return new();
            }
        }

        public static void Save(List<BhSavedPreviewItem> items)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(items, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not save preview items: {ex.Message}");
            }
        }
    }
}
