using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PD2Launcherv2.Models
{
    /// <summary>An item to show to BH (see PD2Launcherv2.BHPreview/README.md for every field).</summary>
    public class BhPreviewItemSpec
    {
        [JsonProperty("code")] public string Code { get; set; } = "";
        [JsonProperty("quality", NullValueHandling = NullValueHandling.Ignore)] public string? Quality { get; set; }
        [JsonProperty("itemLevel", NullValueHandling = NullValueHandling.Ignore)] public int? ItemLevel { get; set; }
        [JsonProperty("flags", NullValueHandling = NullValueHandling.Ignore)] public List<string>? Flags { get; set; }
        [JsonProperty("fileIndex", NullValueHandling = NullValueHandling.Ignore)] public int? FileIndex { get; set; }
        [JsonProperty("sockets", NullValueHandling = NullValueHandling.Ignore)] public int? Sockets { get; set; }
        [JsonProperty("runeword", NullValueHandling = NullValueHandling.Ignore)] public int? Runeword { get; set; }
        [JsonProperty("stats", NullValueHandling = NullValueHandling.Ignore)] public List<BhPreviewStat>? Stats { get; set; }

        /// <summary>Launcher-side label for the row; not sent to the host.</summary>
        [JsonIgnore] public string Label { get; set; } = "";
    }

    public class BhPreviewStat
    {
        [JsonProperty("stat")] public int Stat { get; set; }
        [JsonProperty("layer")] public int Layer { get; set; }
        [JsonProperty("value")] public int Value { get; set; }
    }

    public class BhPreviewItemResult
    {
        /// <summary>The name the game itself gives the item, before BH rewrites it.</summary>
        [JsonProperty("gameName")] public string GameName { get; set; } = "";
        /// <summary>The name BH produced, with ÿc colour codes; empty when the filter hides the item.</summary>
        [JsonProperty("name")] public string Name { get; set; } = "";
        [JsonProperty("hidden")] public bool Hidden { get; set; }
        /// <summary>Text BH appends to the item's tooltip (filter descriptions, ilvl, ...).</summary>
        [JsonProperty("description")] public string Description { get; set; } = "";
        /// <summary>The colour code the game puts in front of the name (quality colour).</summary>
        [JsonProperty("qualityColor")] public int QualityColor { get; set; }
        [JsonProperty("error")] public string? Error { get; set; }
    }

    public class BhPreviewResponse
    {
        [JsonProperty("items")] public List<BhPreviewItemResult> Items { get; set; } = new();
        /// <summary>Chat lines BH printed (drop notifications).</summary>
        [JsonProperty("messages")] public List<string> Messages { get; set; } = new();
        [JsonProperty("error")] public string? Error { get; set; }
    }

    public class BhCatalog
    {
        [JsonProperty("items")] public List<BhCatalogItem> Items { get; set; } = new();
        [JsonProperty("uniques")] public List<BhCatalogNamed> Uniques { get; set; } = new();
        [JsonProperty("sets")] public List<BhCatalogNamed> Sets { get; set; } = new();
        [JsonProperty("runewords")] public List<BhCatalogNamed> Runewords { get; set; } = new();
    }

    public class BhCatalogItem
    {
        [JsonProperty("code")] public string Code { get; set; } = "";
        [JsonProperty("name")] public string Name { get; set; } = "";
        [JsonProperty("type")] public string Type { get; set; } = "";
        [JsonProperty("group")] public string Group { get; set; } = "";
        [JsonProperty("level")] public int Level { get; set; }
        [JsonProperty("maxSockets")] public int MaxSockets { get; set; }
    }

    public class BhCatalogNamed
    {
        [JsonProperty("index")] public int Index { get; set; }
        [JsonProperty("code")] public string Code { get; set; } = "";
        [JsonProperty("name")] public string Name { get; set; } = "";
    }

    /// <summary>The simulated world the items are evaluated in.</summary>
    public class BhPreviewWorld
    {
        public int CharacterClass { get; set; } = 1;
        public int CharacterLevel { get; set; } = 90;
        public int Difficulty { get; set; } = 2;
        public int Area { get; set; } = 109;
        public int Act { get; set; } = 4;

        public JObject ToJson() => new JObject
        {
            ["character"] = new JObject { ["class"] = CharacterClass, ["level"] = CharacterLevel },
            ["difficulty"] = Difficulty,
            ["area"] = Area,
            ["act"] = Act,
        };
    }
}
