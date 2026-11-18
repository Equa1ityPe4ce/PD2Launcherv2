using PD2Launcherv2.Models;

namespace PD2Launcherv2.Helpers
{
    /// <summary>
    /// The items the filter preview shows by default: the things loot filters usually care
    /// about. Names and codes are checked against the installed game data (the host's catalog),
    /// so anything a season removes simply drops out of the list.
    /// </summary>
    public static class BhPreviewSamples
    {
        // Every rare is shown at this item level, high enough for the affix-level rules filters
        // apply to end-game rares.
        private const int RareItemLevel = 87;

        private static readonly string[] Gems =
        {
            "gcv", "gfv", "gsv", "gzv", "gpv", "gcy", "gfy", "gsy", "gly", "gpy", "gcb", "gfb", "gsb", "glb", "gpb",
            "gcg", "gfg", "gsg", "glg", "gpg", "gcr", "gfr", "gsr", "glr", "gpr", "gcw", "gfw", "gsw", "glw", "gpw",
            "skc", "skf", "sku", "skl", "skz",
        };
        private static readonly string[] Consumables =
        {
            "hp1", "hp2", "hp3", "hp4", "hp5", "mp1", "mp2", "mp3", "mp4", "mp5", "rvs", "rvl", "yps", "vps", "wms",
            "isc", "tsc", "key", "pk1", "pk2", "pk3", "bey", "mbr", "dhn", "toa",
        };
        private static readonly string[] Uniques =
        {
            "Harlequin Crest", "The Stone of Jordan", "Arachnid Mesh", "Mara's Kaleidoscope", "Griffon's Eye",
            "Annihilus", "Hellfire Torch", "Gheed's Fortune", "Death's Web", "Tyrael's Might",
        };
        private static readonly string[] Sets =
        {
            "Tal Rasha's Guardianship", "Immortal King's Stone Crusher", "Trang-Oul's Claws", "Angelic Halo",
        };

        public static List<BhPreviewItemSpec> Build(BhCatalog catalog)
        {
            var byCode = catalog.Items.GroupBy(i => i.Code).ToDictionary(g => g.Key, g => g.First());
            var specs = new List<BhPreviewItemSpec>();
            void Add(string code, string label, Action<BhPreviewItemSpec>? configure = null)
            {
                if (!byCode.ContainsKey(code)) return;
                var spec = new BhPreviewItemSpec { Code = code, Label = label };
                configure?.Invoke(spec);
                specs.Add(spec);
            }

            Add("gld", "Gold (small pile)", s => s.Stats = new() { new BhPreviewStat { Stat = 14, Value = 120 } });
            Add("gld", "Gold (large pile)", s => s.Stats = new() { new BhPreviewStat { Stat = 14, Value = 8000 } });

            for (int r = 1; r <= 33; r++)
            {
                var code = $"r{r:00}";
                if (byCode.TryGetValue(code, out var rune)) Add(code, rune.Name);
            }
            foreach (var code in Gems.Concat(Consumables))
                if (byCode.TryGetValue(code, out var item)) Add(code, item.Name);

            foreach (var name in Uniques)
            {
                var u = catalog.Uniques.FirstOrDefault(x => x.Name == name);
                // Unidentified, as a unique drops: that is the name the filter sees on the ground.
                if (u != null) Add(u.Code, $"{u.Name} (unique, unidentified)", s => { s.Quality = "unique"; s.FileIndex = u.Index; s.Flags = new() { }; });
            }
            foreach (var name in Sets)
            {
                var si = catalog.Sets.FirstOrDefault(x => x.Name == name);
                // Unidentified, as a set item drops.
                if (si != null) Add(si.Code, $"{si.Name} (set, unidentified)", s => { s.Quality = "set"; s.FileIndex = si.Index; s.Flags = new() { }; });
            }

            // Jewellery and charms in each quality a filter tends to treat differently.
            foreach (var code in new[] { "rin", "amu", "jew", "cm1", "cm2", "cm3" })
            {
                if (!byCode.TryGetValue(code, out var item)) continue;
                Add(code, $"{item.Name} (magic)", s => s.Quality = "magic");
                if (code != "cm1" && code != "cm2" && code != "cm3")
                    Add(code, $"{item.Name} (rare)", s => { s.Quality = "rare"; s.ItemLevel = RareItemLevel; });
            }

            // Popular bases, plain and in the variants filters key on.
            foreach (var code in new[] { "utp", "uit", "ci3", "7cr", "7wa", "7gd", "uap", "6ws" })
            {
                if (!byCode.TryGetValue(code, out var item)) continue;
                int sockets = Math.Max(0, Math.Min(item.MaxSockets, 6));
                Add(code, $"{item.Name}");
                Add(code, $"{item.Name} (superior)", s => s.Quality = "superior");
                Add(code, $"{item.Name} (ethereal)", s => s.Flags = new() { "identified", "ethereal" });
                if (sockets > 0)
                    Add(code, $"{item.Name} ({sockets} sockets)", s => s.Sockets = sockets);
                Add(code, $"{item.Name} (magic)", s => s.Quality = "magic");
                Add(code, $"{item.Name} (rare, unidentified)", s => { s.Quality = "rare"; s.Flags = new() { }; s.ItemLevel = RareItemLevel; });
            }
            return specs;
        }
    }
}
