using PD2Launcherv2.Helpers;
using PD2Launcherv2.Models;

namespace PD2Launcherv2.ViewModels
{
    /// <summary>One item in the filter preview: what was dropped, and what BH showed for it.</summary>
    public class BhPreviewRow
    {
        public BhPreviewRow(BhPreviewItemSpec spec, BhPreviewItemResult result, BhSavedPreviewItem? source = null)
        {
            Label = D2ColorText.Strip(string.IsNullOrEmpty(spec.Label) ? result.GameName.Replace('\n', ' ') : spec.Label);
            // The colour the game draws the item's own name in: its quality colour, unless the
            // name carries its own colour code (runes are orange through their string table entry).
            var gameLines = D2ColorText.Parse(result.GameName, result.QualityColor, bottomLineFirst: true);
            LabelBrush = gameLines.SelectMany(l => l).FirstOrDefault()?.Brush ?? D2ColorText.BrushFor(result.QualityColor);
            Source = source;
            Hidden = result.Hidden;
            Error = result.Error;
            // The game prefixes the quality colour after BH's hook runs, and draws item
            // names bottom line first.
            NameLines = D2ColorText.Parse(result.Name, result.QualityColor, bottomLineFirst: true);
            Description = D2ColorText.Strip(result.Description).Trim();
        }

        public string Label { get; }
        public System.Windows.Media.Brush LabelBrush { get; }
        /// <summary>The built item this row shows (one the user kept), if any.</summary>
        public BhSavedPreviewItem? Source { get; }
        public bool IsCustom => Source != null;
        public bool Hidden { get; }
        public bool Shown => !Hidden;
        public string? Error { get; }
        public bool HasError => !string.IsNullOrEmpty(Error);
        public List<List<ColoredRun>> NameLines { get; }
        public string Description { get; }
        public bool HasDescription => Description.Length > 0;
    }
}
