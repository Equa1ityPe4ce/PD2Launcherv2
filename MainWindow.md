## MainWindow README

The MainWindow serves as the primary interface for the Project Diablo 2 Launcher application. It orchestrates user navigation, updates, news fetching, and the launching of the game. This document outlines its key components, functionalities, and its interactions with the application's ViewModel and services.
Key Components

    Top Navigation Hyperlinks: Quick access to external resources like Home, Trade, Reddit, etc., enhancing user engagement.
    News Section: Dynamically updates with the latest news fetched from a JSON file stored on GitHub, using the NewsHelpers service.
    Game Launch Button: Initiates the game launch sequence, including file updates and filter application, through the LaunchGameHelpers and FileUpdateHelpers services.
    Options, Loot Filter, and About Buttons: Navigate to respective views for additional functionalities and information, managed by commands in the ViewModel.

___
#### ViewModel and Commands

    Utilizes commands such as OpenOptionsCommand, OpenLootCommand, and OpenAboutCommand to navigate to different application views without leaving the main window.
    Interacts with LocalStorage for reading and writing application settings, ensuring preferences persistence across sessions.
___
#### Helpers Integration

    FileUpdateHelpers: Checks and applies updates for game files and the launcher itself.
    FilterHelpers: Manages loot filter updates and applications based on user selection.
    LaunchGameHelpers: Handles the launching of the game with customized settings and updates.
    NewsHelpers: Fetches and stores the latest news, displaying it in the main window for user information.
    DDrawHelpers: Reads and writes DDraw settings for graphical configuration.
___
#### Event Handlers and Navigation

    Registers and handles messages for navigation (NavigationMessage) and configuration changes (ConfigurationChangeMessage), enabling dynamic UI updates.
    Implements event handlers for UI elements like buttons and hyperlinks to execute commands or open external links.
___
#### UI and Usability Features

    Customized buttons and hyperlinks for a cohesive look aligned with the game's theme.
    Drag functionality for the window movement and options for minimizing and closing the window.
    Visibility toggles for elements like the Beta notification, enhancing user experience based on application state.
___
#### Additional Notes

    The MainWindow's layout and functionalities are designed with user experience in mind, making navigation intuitive and providing quick access to essential features.
    Integration with various helpers ensures the application remains up-to-date and provides users with the latest information and updates.
    The application's architecture allows for easy expansion or modification of functionalities as the project evolves.