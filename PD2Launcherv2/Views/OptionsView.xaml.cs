using PD2Launcherv2.ViewModels;
using System.Windows.Controls;

namespace PD2Launcherv2.Views
{
    /// <summary>
    /// Interaction logic for OptionsView.xaml
    /// </summary>
    public partial class OptionsView : Page
    {
        public OptionsView()
        {
            InitializeComponent();
            DataContext = App.Resolve<OptionsViewModel>();
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}