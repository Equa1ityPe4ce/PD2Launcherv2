using PD2Launcherv2.Models;
using System.Collections.ObjectModel;

namespace PD2Launcherv2.ViewModels
{
    /// <summary>
    /// The filter preview's item builder: a base, its rarity, flags, item level, the auto mod it
    /// spawned with and its staff mods, all taken from the installed game data (the host catalog).
    /// </summary>
    public class BhItemBuilder : ViewModelBase
    {
        private static readonly BhAutoMod NoAutoMod = new() { Index = -1, Text = "None" };
        private static readonly BhSkillTab NoTab = new() { Layer = -1, Text = "None" };
        private static readonly BhSkill NoSkill = new() { Id = -1, Name = "None" };
        private const int DefaultItemLevel = 87;

        private BhCatalog _catalog = new();
        private Dictionary<int, BhAutoMod> _autoModsByIndex = new();

        public List<BhCatalogItem> Bases { get; private set; } = new();
        public List<string> Qualities { get; } = new() { "Normal", "Superior", "Magic", "Rare", "Crafted", "Unique", "Set" };
        public List<int> Bonuses { get; } = new() { 0, 1, 2, 3 };

        public void SetCatalog(BhCatalog catalog)
        {
            _catalog = catalog;
            _autoModsByIndex = catalog.AutoMods.ToDictionary(m => m.Index);
            Bases = catalog.Items.Where(i => !string.IsNullOrWhiteSpace(i.Name)).OrderBy(i => i.Name).ToList();
            OnPropertyChanged(nameof(Bases));
            SelectedBase ??= Bases.FirstOrDefault(b => b.Code == "utp") ?? Bases.FirstOrDefault();
        }

        private BhCatalogItem? _selectedBase;
        public BhCatalogItem? SelectedBase
        {
            get => _selectedBase;
            set
            {
                if (value == null || value == _selectedBase) return;
                _selectedBase = value;
                OnPropertyChanged();
                UpdateForBase();
            }
        }

        private string _quality = "Rare";
        public string Quality
        {
            get => _quality;
            set { _quality = value; OnPropertyChanged(); UpdateNamedChoices(); }
        }

        public List<BhCatalogNamed> NamedChoices { get; private set; } = new();
        public bool HasNamedChoice => Quality is "Unique" or "Set";

        private BhCatalogNamed? _selectedNamed;
        public BhCatalogNamed? SelectedNamed
        {
            get => _selectedNamed;
            set { _selectedNamed = value; OnPropertyChanged(); }
        }

        private bool _identified;
        public bool Identified
        {
            get => _identified;
            set { _identified = value; OnPropertyChanged(); }
        }

        private bool _ethereal;
        public bool Ethereal
        {
            get => _ethereal;
            set { _ethereal = value; OnPropertyChanged(); }
        }

        public List<int> SocketChoices { get; private set; } = new() { 0 };

        private int _sockets;
        public int Sockets
        {
            get => _sockets;
            set { _sockets = value; OnPropertyChanged(); }
        }

        private string _itemLevel = DefaultItemLevel.ToString();
        public string ItemLevel
        {
            get => _itemLevel;
            set { _itemLevel = value; OnPropertyChanged(); }
        }

        // ---------------------------------------------------------------- auto mod
        public List<BhAutoMod> AutoModChoices { get; private set; } = new() { NoAutoMod };
        public bool HasAutoMods => AutoModChoices.Count > 1;

        private BhAutoMod _selectedAutoMod = NoAutoMod;
        public BhAutoMod SelectedAutoMod
        {
            get => _selectedAutoMod;
            set { _selectedAutoMod = value ?? NoAutoMod; OnPropertyChanged(); OnPropertyChanged(nameof(RolledAutoModText)); }
        }

        /// <summary>Where in each mod's min..max range the auto mod rolled, 0-100.</summary>
        private double _autoModRoll = 100;
        public double AutoModRoll
        {
            get => _autoModRoll;
            set { _autoModRoll = value; OnPropertyChanged(); OnPropertyChanged(nameof(RolledAutoModText)); }
        }

        public string RolledAutoModText =>
            SelectedAutoMod.Index < 0 ? "" : string.Join(", ", SelectedAutoMod.Mods.Select(RolledText));

        // ---------------------------------------------------------------- staff mods
        public bool HasStaffMods => StaffClass != null;
        public BhCharacterClass? StaffClass { get; private set; }
        public List<BhSkillTab> TabChoices { get; private set; } = new() { NoTab };
        public List<BhSkill> SkillChoices { get; private set; } = new() { NoSkill };
        public string ClassSkillsText => StaffClass?.AllSkills.Replace("+N ", "") ?? "";

        private int _classSkills;
        public int ClassSkills
        {
            get => _classSkills;
            set { _classSkills = value; OnPropertyChanged(); }
        }

        private BhSkillTab _tab = NoTab;
        public BhSkillTab Tab
        {
            get => _tab;
            set { _tab = value ?? NoTab; OnPropertyChanged(); }
        }

        private int _tabBonus = 1;
        public int TabBonus
        {
            get => _tabBonus;
            set { _tabBonus = value; OnPropertyChanged(); }
        }

        public ObservableCollection<SkillSlot> SkillSlots { get; } = new() { new(), new(), new() };

        public class SkillSlot : ViewModelBase
        {
            private BhSkill _skill = NoSkill;
            public BhSkill Skill
            {
                get => _skill;
                set { _skill = value ?? NoSkill; OnPropertyChanged(); }
            }

            private int _bonus = 1;
            public int Bonus
            {
                get => _bonus;
                set { _bonus = value; OnPropertyChanged(); }
            }
        }

        private void UpdateForBase()
        {
            var b = _selectedBase!;
            SocketChoices = Enumerable.Range(0, Math.Clamp(b.MaxSockets, 0, 6) + 1).ToList();
            if (Sockets >= SocketChoices.Count) Sockets = 0;
            OnPropertyChanged(nameof(SocketChoices));

            AutoModChoices = new List<BhAutoMod> { NoAutoMod }
                .Concat(b.AutoMods.Where(_autoModsByIndex.ContainsKey).Select(i => _autoModsByIndex[i]).OrderBy(m => m.Level))
                .ToList();
            SelectedAutoMod = AutoModChoices.Count > 1 ? AutoModChoices[^1] : NoAutoMod;
            OnPropertyChanged(nameof(AutoModChoices));
            OnPropertyChanged(nameof(HasAutoMods));

            StaffClass = _catalog.Classes.FirstOrDefault(c => c.Id == b.StaffModClass);
            TabChoices = new List<BhSkillTab> { NoTab }.Concat(StaffClass?.Tabs ?? new()).ToList();
            SkillChoices = new List<BhSkill> { NoSkill }
                .Concat(_catalog.Skills.Where(s => s.Class == b.StaffModClass).OrderBy(s => s.Name)).ToList();
            Tab = NoTab;
            ClassSkills = 0;
            foreach (var slot in SkillSlots) slot.Skill = NoSkill;
            OnPropertyChanged(nameof(StaffClass));
            OnPropertyChanged(nameof(HasStaffMods));
            OnPropertyChanged(nameof(TabChoices));
            OnPropertyChanged(nameof(SkillChoices));
            OnPropertyChanged(nameof(ClassSkillsText));
            UpdateNamedChoices();
        }

        private void UpdateNamedChoices()
        {
            var code = _selectedBase?.Code;
            NamedChoices = Quality switch
            {
                "Unique" => _catalog.Uniques.Where(u => u.Code == code).ToList(),
                "Set" => _catalog.Sets.Where(s => s.Code == code).ToList(),
                _ => new(),
            };
            SelectedNamed = NamedChoices.FirstOrDefault();
            OnPropertyChanged(nameof(NamedChoices));
            OnPropertyChanged(nameof(HasNamedChoice));
        }

        private int Roll(BhModStat s) => s.Min + (int)Math.Round((s.Max - s.Min) * AutoModRoll / 100.0);

        private string RolledText(BhModRoll mod)
        {
            var first = mod.Stats.FirstOrDefault();
            if (first == null || first.Min == first.Max) return mod.Text;
            return mod.Text.Replace($"{first.Min}-{first.Max}", Roll(first).ToString());
        }

        /// <summary>The item as built, or null with a reason when it cannot be.</summary>
        public BhSavedPreviewItem? Build(out string? error)
        {
            error = null;
            var b = _selectedBase;
            if (b == null) { error = "Pick a base item."; return null; }
            if (HasNamedChoice && SelectedNamed == null) { error = $"There is no {Quality.ToLowerInvariant()} {b.Name}."; return null; }

            var spec = new BhPreviewItemSpec
            {
                Code = b.Code,
                Quality = Quality.ToLowerInvariant(),
                ItemLevel = int.TryParse(ItemLevel, out var ilvl) ? Math.Clamp(ilvl, 1, 99) : DefaultItemLevel,
                Sockets = Sockets > 0 ? Sockets : null,
                FileIndex = HasNamedChoice ? SelectedNamed!.Index : null,
            };
            spec.Flags = new();
            if (Identified) spec.Flags.Add("identified");
            if (Ethereal) spec.Flags.Add("ethereal");

            var stats = new List<BhPreviewStat>();
            var parts = new List<string>();
            if (Quality != "Normal") parts.Add(HasNamedChoice ? $"{Quality.ToLowerInvariant()} {SelectedNamed!.Name}" : Quality.ToLowerInvariant());
            if (!Identified) parts.Add("unid");
            if (Ethereal) parts.Add("eth");
            if (Sockets > 0) parts.Add($"{Sockets}os");
            parts.Add($"ilvl {spec.ItemLevel}");

            if (SelectedAutoMod.Index >= 0)
            {
                spec.AutoPrefix = SelectedAutoMod.Index;
                foreach (var mod in SelectedAutoMod.Mods)
                    foreach (var s in mod.Stats)
                        stats.Add(new BhPreviewStat { Stat = s.Stat, Layer = s.Layer, Value = Roll(s) << s.Shift });
                parts.Add(RolledAutoModText);
            }
            if (StaffClass != null)
            {
                if (ClassSkills > 0)
                {
                    stats.Add(new BhPreviewStat { Stat = 83, Layer = StaffClass.Id, Value = ClassSkills });
                    parts.Add($"+{ClassSkills} {ClassSkillsText}");
                }
                if (Tab.Layer >= 0 && TabBonus > 0)
                {
                    stats.Add(new BhPreviewStat { Stat = 188, Layer = Tab.Layer, Value = TabBonus });
                    parts.Add(Tab.Text.Replace("+N", $"+{TabBonus}"));
                }
                foreach (var slot in SkillSlots.Where(s => s.Skill.Id >= 0 && s.Bonus > 0))
                {
                    stats.Add(new BhPreviewStat { Stat = 107, Layer = slot.Skill.Id, Value = slot.Bonus });
                    parts.Add($"+{slot.Bonus} {slot.Skill.Name}");
                }
            }
            if (stats.Count > 0) spec.Stats = stats;
            var label = $"{b.Name}: {string.Join(", ", parts)}";
            spec.Label = label;
            return new BhSavedPreviewItem { Label = label, Spec = spec };
        }
    }
}
