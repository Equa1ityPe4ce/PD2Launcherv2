using PD2Launcherv2.ViewModels;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace PD2Launcherv2.Views
{
    /// <summary>
    /// Interaction logic for AboutView.xaml
    /// </summary>
    public partial class AboutView : Page
    {
        private DispatcherTimer _holdTimer;
        private DateTime _holdStartTime;
        private const int RequiredHoldSeconds = 10;

        public AboutView()
        {
            InitializeComponent();
            DataContext = App.Resolve<AboutViewModel>();
        }

        private void CustomUnlockButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _holdStartTime = DateTime.Now;

            _holdTimer = new DispatcherTimer();
            _holdTimer.Interval = TimeSpan.FromMilliseconds(100);
            _holdTimer.Tick += CheckHoldDuration;
            _holdTimer.Start();
        }

        private void CustomUnlockButton_MouseUp(object sender, MouseButtonEventArgs e)
        {
            CancelHold();
        }

        private void CustomUnlockButton_MouseLeave(object sender, MouseEventArgs e)
        {
            CancelHold();
        }

        private void CheckHoldDuration(object sender, EventArgs e)
        {
            if ((DateTime.Now - _holdStartTime).TotalSeconds >= RequiredHoldSeconds)
            {
                _holdTimer.Stop();
                _holdTimer.Tick -= CheckHoldDuration;

                var viewModel = DataContext as AboutViewModel;
                if (viewModel != null)
                {
                    viewModel.ShowCustomEnv = !viewModel.ShowCustomEnv;
                    Debug.WriteLine("Custom toggled");
                }
            }
        }

        private void CancelHold()
        {
            if (_holdTimer != null)
            {
                _holdTimer.Stop();
                _holdTimer.Tick -= CheckHoldDuration;
                _holdTimer = null;
            }
        }
    }
}