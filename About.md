## About Page README

The About page provides information about the application and allows users to switch between different data sources (e.g., Live and Beta environments). This page aims to offer users insight into the project and facilitate easy switching for testing or other purposes.
Key Elements

    Switch To Text: Indicates the functionality below, allowing users to switch between different environments.
    Live Button: Switches the application's data source to the Live environment. It triggers a change in the application's configuration and updates the UI accordingly.
    Beta Button: Similar to the Live button but switches the application's data source to the Beta environment for testing upcoming features.
___
**ViewModel Integration**

_The AboutViewModel controls the page's dynamic aspects, particularly the switching between Live and Beta environments. It interacts with the ILocalStorage to persist the user's choice and uses Messenger to communicate changes across the application.
Commands_

    ProdBucket: Executed when the Live button is clicked. It updates the application's data source to the Live environment and sends a message to navigate back.
    BetaBucket: Executed when the Beta button is clicked. Similar to ProdBucket but switches to the Beta environment.
___
**Messaging**

_The ViewModel utilizes the Messenger class to send navigation messages, allowing the user to return to the previous view after making a selection._