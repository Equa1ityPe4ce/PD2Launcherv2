## Options Page README

The Options page is a comprehensive interface allowing users to customize their gameplay experience through various settings, including graphics, sound, and other preferences. This page interacts with the OptionsViewModel to manage user selections and apply configurations to the game.
Key Components and Features

    Graphics and Sound Settings: Users can toggle between different rendering options (3dfx and ddraw), adjust sound settings, and set background sound options.
    DDraw Settings: Offers advanced graphical settings like fullscreen/windowed mode, window resolution, and aspect ratio adjustments. Includes a "Set to Default" button to revert to the original settings.
    Advanced Options: A dedicated section for more technical settings, accessible through a toggle button. Includes options for vertical sync, mouse sensitivity, and windowed mode tweaks.
    Permissions Management: Provides buttons to set or remove Windows permissions, assisting users in resolving permission-related issues.
    Customization through ComboBoxes and CheckBoxes: Allows detailed customization of game settings, including max FPS, window position saving, renderer choice, and more.
___
**ViewModel Integration (OptionsViewModel)**

_OptionsViewModel manages the state and user interactions on the Options page. It handles data loading, user command execution, and storage of user preferences.
Key Properties and Commands_

    CheckBox and ComboBox Bindings: Binds UI elements to properties, enabling dynamic updates and persistence of user preferences.
    ToggleAdvancedOptionsCommand: Manages the visibility of advanced options, enhancing user interface usability.
    RestoreDefaultsCommand: Resets settings to their default values, offering users a quick way to revert changes.
    SetWindowsPermissionsCommand and RemoveWindowsPermissionsCommand: Executes scripts to adjust Windows permissions, aiding users in resolving potential permission issues.
___
**Functionality**

_Upon page load, OptionsViewModel fetches and displays current settings, enabling users to modify them as desired. It interacts with services like DDrawHelpers to read from and write to configuration files, ensuring settings are accurately applied and persisted.
Additional Notes_

    The page supports both graphical and technical settings adjustments, catering to a wide range of user preferences and system requirements.
    Advanced options are hidden by default to maintain a clean user interface while still offering in-depth customization for advanced users.
    Interaction with external scripts for permissions adjustments is handled securely, providing necessary functionalities without compromising system integrity.