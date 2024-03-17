﻿using Microsoft.Extensions.DependencyInjection;
using PD2Launcherv2.Interfaces;
using PD2Launcherv2.Storage;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// Configures services and builds the service provider.
        /// </summary>
        public App()
        {
            // Initializes a new instance of the service collection
            ServiceCollection services = new ServiceCollection();
            ConfigureServices(services);
            // Builds the service provider from the service collection
            _serviceProvider = services.BuildServiceProvider();
        }

        /// <summary>
        /// Configures services for the application's dependency injection container.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        private void ConfigureServices(ServiceCollection services)
        {
            // Registers the LocalStorage service with its interface for dependency injection.
            // This makes LocalStorage available throughout the application via DI.
            services.AddSingleton<ILocalStorage, LocalStorage>();

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
            base.OnStartup(e);
            // Retrieves the MainWindow instance from the service provider and shows it.
            // This demonstrates how dependency injection can be used to create and manage
            // the application's main window.
            var mainWindow = _serviceProvider.GetService<MainWindow>();
            mainWindow?.Show();
        }
    }
}