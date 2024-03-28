using PD2Launcherv2.ViewModels;

namespace PD2Launcherv2
{
    public class ViewModelLocator
    {
        private readonly IServiceProvider _serviceProvider;
        public FiltersViewModel FiltersViewModel => App.Resolve<FiltersViewModel>();
        public AboutViewModel AboutViewModel => App.Resolve<AboutViewModel>();
        public OptionsViewModel OptionsViewModel => App.Resolve<OptionsViewModel>();

    }
}