Filters Page README

The Filters page in the application serves to allow users to select and apply loot filters for their game experience. It fetches a list of available filters from various authors and presents them in a user-friendly interface. Users can browse, select, and save their preferred filter configurations.
Key Components

    
    Help Button: Opens webbrowser to additional information or guidance on loot filters.
    Authors List: Displays a list of authors who have contributed loot filters. Selecting an author will load the filters they have available.
    Filters List: Shows the available filters from the selected author. Users can select a filter to view more details or apply it.
    Save Filter Button: Saves the currently selected filter, applying it to the user's game.
    Authors Page Button: Navigates to a more detailed page about the filter authors, offering further insights or contact information.
___
**ViewModel Integration (FiltersViewModel)**

_The FiltersViewModel manages data and interactions on the Filters page. It handles the loading of filter authors and their respective filters, selection actions by the user, and applying the chosen filter to the game.
Key Properties and Commands_

    AuthorsList and FiltersList: Bind to the ListBox controls on the UI, displaying the available authors and filters.
    SelectedAuthor and SelectedFilter: Track the user's current selections. Changing these properties triggers updates in the UI and potentially fetches new data.
    SaveFilterCommand: Executed when the "Save Filter" button is clicked, applying the selected filter.
    OpenAuthorsPageCommand and OpenHelpPageCommand: Provide navigation functionality, leading users to additional information or assistance.
___
**Functionality**

_Upon initialization, the ViewModel fetches and displays a list of filter authors. When an author is selected, their available filters are loaded into the Filters List. Selecting a filter enables the "Save Filter" button, allowing the user to apply it. The ViewModel interacts with a FilterHelpers class to manage the fetching, storing, and application of filters, ensuring the game uses the user's selected preferences._
___
**Additional Notes**

_The Filters page leverages external data (fetched from GitHub) to populate the list of authors and filters. It supports adding local filters by scanning a predefined directory and integrating them into the UI for selection alongside online filters._