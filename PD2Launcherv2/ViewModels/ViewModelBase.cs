using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace PD2Launcherv2.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Define a command that can be bound to a Close or Back action
        public ICommand CloseCommand { get; protected set; }
    }
}