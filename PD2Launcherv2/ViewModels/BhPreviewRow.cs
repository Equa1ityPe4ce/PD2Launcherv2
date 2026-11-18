using PD2Launcherv2.Helpers;
using PD2Launcherv2.Models;

namespace PD2Launcherv2.ViewModels
{
    /// <summary>One item in the filter preview: what was dropped, and what BH showed for it.</summary>
    public class BhPreviewRow
    {
        public BhPreviewRow(BhPreviewItemSpec spec, BhPreviewItemResult result)
        {
            Label = string.IsNullOrEmpty(spec.Label) ? D2ColorText.Strip(result.GameName).Replace('\n', ' ') : spec.Label;
            Hidden = result.Hidden;
            Error = result.Error;
            // The game prefixes the quality colour after BH's hook runs, and draws item
            // names bottom line first.
            NameLines = D2ColorText.Parse(result.Name, result.QualityColor, bottomLineFirst: true);
            Description = D2ColorText.Strip(result.Description).Trim();
        }

        public string Label { get; }
        public bool Hidden { get; }
        public bool Shown => !Hidden;
        public string? Error { get; }
        public bool HasError => !string.IsNullOrEmpty(Error);
        public List<List<ColoredRun>> NameLines { get; }
        public string Description { get; }
        public bool HasDescription => Description.Length > 0;
    }
}
