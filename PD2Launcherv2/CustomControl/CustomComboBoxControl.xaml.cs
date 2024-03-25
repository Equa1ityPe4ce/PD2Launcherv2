using ProjectDiablo2Launcherv2;
using ProjectDiablo2Launcherv2.Models;
using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace PD2Launcherv2.CustomControl
{
    /// <summary>
    /// Interaction logic for CustomComboBoxControl.xaml
    /// </summary>
    public partial class CustomComboBoxControl : UserControl
    {
        public List<DisplayValuePair> OptionsMaxFPS => Constants.MaxFpsPickerItems();
        public List<DisplayValuePair>OptionsModePicker => Constants.ModePickerItems();
        public List<DisplayValuePair>OptionsGameTicks => Constants.MaxGameTicksPickerItems();
        public List<DisplayValuePair>OptionsSaveWindowPosition => Constants.SaveWindowPositionPickerItems();
        public List<DisplayValuePair>OptionsRenderer => Constants.RendererPickerItems();
        public List<DisplayValuePair>OptionsAPIHook => Constants.SaveWindowPositionPickerItems();
        public List<DisplayValuePair>OptionsForceMinimumFPS => Constants.MinFpsPickerItems();
        public List<DisplayValuePair>OptionsShadeSupport => Constants.ShaderPickerItems();

        public CustomComboBoxControl()
        {
            InitializeComponent();
        }

        // You can expose any properties of ComboBox you need using DependencyProperty
        // For example, ItemsSource, SelectedItem, etc.

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(IEnumerable), typeof(CustomComboBoxControl), new PropertyMetadata(null));

        public IEnumerable ItemsSource
        {
            get { return (IEnumerable)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public static readonly DependencyProperty SelectedOptionProperty =
    DependencyProperty.Register(
        "SelectedOption",
        typeof(DisplayValuePair),
        typeof(CustomComboBoxControl),
        new PropertyMetadata(null)
    );

        public DisplayValuePair SelectedOption
        {
            get { return (DisplayValuePair)GetValue(SelectedOptionProperty); }
            set { SetValue(SelectedOptionProperty, value); }
        }
    }
}