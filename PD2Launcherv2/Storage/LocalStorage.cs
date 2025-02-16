using Newtonsoft.Json;
using PD2Launcherv2.Interfaces;
using PD2Launcherv2.Models;
using ProjectDiablo2Launcherv2.Models;
using System.Diagnostics;
using System.IO;

namespace PD2Launcherv2.Storage
{
    public class LocalStorage : ILocalStorage
    {
        private const string StorageFileName = "launcherSettings.json";
        private readonly string _storageDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppData");

        public AllSettings Load()
        {
            string filePath = Path.Combine(_storageDirectory, StorageFileName);
            string backupFilePath = filePath + ".backup";

            if (!File.Exists(filePath))
            {
                return new AllSettings();
            }

            try
            {
                Debug.WriteLine($"Loading settings from: {filePath}");
                string json = File.ReadAllText(filePath);
                var settings = JsonConvert.DeserializeObject<AllSettings>(json);

                if (settings == null)
                {
                    throw new JsonException("Deserialized settings are null.");
                }

                // If JSON is valid, create a backup
                File.Copy(filePath, backupFilePath, true);

                return settings;
            }
            catch (JsonException ex) // s10 Corrupted json Bug Fix (Lunboks)
            {
                Debug.WriteLine($"JSON Error: {ex.Message}. Attempting to restore last valid backup.");

                // If a backup exists, use it instead
                if (File.Exists(backupFilePath))
                {
                    try
                    {
                        string backupJson = File.ReadAllText(backupFilePath);
                        return JsonConvert.DeserializeObject<AllSettings>(backupJson) ?? new AllSettings();
                    }
                    catch
                    {
                        Debug.WriteLine("Backup is also corrupted. Using default settings.");
                    }
                }

                return new AllSettings(); // Default settings as last resort
            }
        }

        public void Update<T>(StorageKey key, T value) where T : class
        {
            Debug.WriteLine($"Updating key: {key} with value: {value}");
            var settings = Load();

            switch (key)
            {
                case StorageKey.LauncherArgs:
                    settings.LauncherArgs = value as LauncherArgs ?? new LauncherArgs();
                    break;
                case StorageKey.DdrawOptions:
                    settings.DdrawOptions = value as DdrawOptions ?? new DdrawOptions();
                    break;
                case StorageKey.FileUpdateModel:
                    settings.FileUpdateModel = value as FileUpdateModel ?? new FileUpdateModel();
                    break;
                case StorageKey.SelectedAuthorAndFilter:
                    settings.SelectedAuthorAndFilter = value as SelectedAuthorAndFilter ?? new SelectedAuthorAndFilter();
                    break;
                case StorageKey.Pd2AuthorList:
                    settings.Pd2AuthorList = value as Pd2AuthorList ?? new Pd2AuthorList();
                    break;
                case StorageKey.News:
                    settings.News = value as News ?? new News();
                    break;
                case StorageKey.WindowPosition:
                    settings.WindowPosition = value as WindowPositionModel ?? new WindowPositionModel();
                    break;
                case StorageKey.ResetInfo:
                    settings.ResetInfo = value as ResetInfo ?? new ResetInfo();
                    break;
            }

            if (!Directory.Exists(_storageDirectory))
            {
                Directory.CreateDirectory(_storageDirectory);
            }

            string filePath = Path.Combine(_storageDirectory, StorageFileName);
            string tempFilePath = filePath + ".tmp"; // Temporary file

            try
            {
                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);

                // Validate JSON before writing
                if (IsValidJson(json))
                {
                    File.WriteAllText(tempFilePath, json);
                    File.Replace(tempFilePath, filePath, null);
                    Debug.WriteLine("Settings updated successfully.");
                }
                else
                {
                    Debug.WriteLine("Skipping settings update due to invalid JSON.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error writing settings file: {ex.Message}");
            }
        }

        public T LoadSection<T>(StorageKey key) where T : class
        {
            var settings = Load();

            return key switch
            {
                StorageKey.LauncherArgs => settings.LauncherArgs as T ?? Activator.CreateInstance<T>(),
                StorageKey.DdrawOptions => settings.DdrawOptions as T ?? Activator.CreateInstance<T>(),
                StorageKey.FileUpdateModel => settings.FileUpdateModel as T ?? Activator.CreateInstance<T>(),
                StorageKey.Pd2AuthorList => settings.Pd2AuthorList as T ?? Activator.CreateInstance<T>(),
                StorageKey.SelectedAuthorAndFilter => settings.SelectedAuthorAndFilter as T ?? Activator.CreateInstance<T>(),
                StorageKey.WindowPosition => settings.WindowPosition as T ?? Activator.CreateInstance<T>(),
                StorageKey.News => settings.News as T ?? Activator.CreateInstance<T>(),
                StorageKey.ResetInfo => settings.ResetInfo as T ?? Activator.CreateInstance<T>(),
                _ => Activator.CreateInstance<T>()
            };
        }

        public void InitializeIfNotExists<T>(StorageKey key, T defaultValue) where T : class, new()
        {
            var existingValue = LoadSection<T>(key);
            if (existingValue == null)
            {
                Update(key, defaultValue);
            }
        }

        private bool IsValidJson(string json)
        {
            try
            {
                JsonConvert.DeserializeObject(json);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}