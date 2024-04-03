using Microsoft.Extensions.DependencyInjection;
using PD2Launcherv2.Helpers;
using PD2Launcherv2.Interfaces;
using PD2Launcherv2.Storage;
using PD2Launcherv2.ViewModels;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;

namespace PD2Launcherv2
{
    /// <summary>
    /// Represents the main entry point for the application, handling application startup tasks
    /// and dependency injection configuration.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Holds the service provider for dependency injection.
        /// </summary>
        private readonly IServiceProvider _serviceProvider;
        public static IServiceProvider ServiceProvider { get; private set; }
        public static T Resolve<T>() => ((App)Current)._serviceProvider.GetRequiredService<T>();

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// Configures services and builds the service provider.
        /// </summary>
        public App()
        {
            // Initializes a new instance of the service collection
            ServiceCollection services = new();
            ConfigureServices(services);
            // Builds the service provider from the service collection
            _serviceProvider = services.BuildServiceProvider();
            ServiceProvider = _serviceProvider;

            // Subscribe to unhandled exception events
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        /// <summary>
        /// Configures services for the application's dependency injection container.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        private static void ConfigureServices(ServiceCollection services)
        {
            // Registers the LocalStorage service with its interface for dependency injection.
            // This makes LocalStorage available throughout the application via DI.
            services.AddSingleton<ILocalStorage, LocalStorage>();
            services.AddSingleton<FilterHelpers>();
            services.AddSingleton<HttpClient>();
            services.AddSingleton<LaunchGameHelpers>();
            services.AddSingleton<NewsHelpers>();
            services.AddSingleton<DDrawHelpers>();
            services.AddSingleton<FileUpdateHelpers>(provider =>
            new FileUpdateHelpers( provider.GetRequiredService<HttpClient>()));
            services.AddTransient<OptionsViewModel>();
            services.AddTransient<AboutViewModel>();
            services.AddTransient<FiltersViewModel>();

            // Additional services and view models can be registered here as needed.
            // This allows for easy expansion and maintenance of the application's components.
        }

        /// <summary>
        /// Overrides the <see cref="OnStartup"/> method to perform tasks when the application starts.
        /// This method is used to display the main window of the application using the services
        /// provided by dependency injection.
        /// </summary>
        /// <param name="e">Contains the arguments for the startup event.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            // The name here should match the name of your application's executable without the .exe part
            var currentProcessName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;

            // Check if there are any other processes with the same name as the current process
            if (System.Diagnostics.Process.GetProcessesByName(currentProcessName).Length > 1)
            {
                MessageBox.Show("An instance of the launcher is already running.");
                Application.Current.Shutdown(); // Shut down the current application instance
                return;
            }

            base.OnStartup(e);
            // Retrieves the MainWindow instance from the service provider and shows it.
            // This demonstrates how dependency injection can be used to create and manage
            // the application's main window.
            var mainWindow = _serviceProvider.GetService<MainWindow>();
            mainWindow?.Show();
        }

        // Handle non-UI thread exceptions
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogException(e.ExceptionObject as Exception);
        }

        // Handle UI thread exceptions
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception);
            e.Handled = true; // Prevent application from crashing
        }

        private void LogException(Exception ex)
        {
            if (ex == null) return;

            // Define the log file path. Consider using a more appropriate location.
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LauncherErrorLogs");

            string logFile = Path.Combine(logPath, $"ErrorLog_{DateTime.Now:yyyy-MM-dd}.txt");

            // Prepare the log entry
            var sb = new StringBuilder();
            sb.AppendLine("==============================================================================");
            sb.AppendLine($"Timestamp: {DateTime.Now}");
            sb.AppendLine("Exception Message:");
            sb.AppendLine(ex.Message);
            sb.AppendLine("Stack Trace:");
            sb.AppendLine(ex.StackTrace);

            // Include inner exception details if available
            if (ex.InnerException != null)
            {
                sb.AppendLine("Inner Exception Message:");
                sb.AppendLine(ex.InnerException.Message);
                sb.AppendLine("Inner Exception Stack Trace:");
                sb.AppendLine(ex.InnerException.StackTrace);
            }
            sb.AppendLine("==============================================================================");

            // Append the log entry to the file
            File.AppendAllText(logFile, sb.ToString());
        }
    }
}