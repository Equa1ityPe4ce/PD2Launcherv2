# Local Storage Management for Project Diablo 2 Launcher

The Project Diablo 2 (PD2) Launcher leverages a local storage system to persist user configurations, update models, selected loot filters, news items, and other essential data across sessions. This document outlines the structure and functionality of the local storage system, providing developers with the necessary information to understand, extend, or integrate with the storage system.
Overview

_The PD2 Launcher's local storage system is built around a JSON-based file storage mechanism. It stores configurations and data in a file named launcherSettings.json, located within the AppData directory of the launcher's installation path. This system ensures that user preferences and essential data are retained between launcher sessions, providing a seamless user experience._
___
## Interfaces and Implementations

#### ILocalStorage Interface

_ILocalStorage is the core interface that defines the operations for loading, updating, and initializing data within the local storage. It provides methods for:_

    Loading all settings: Retrieves all stored settings as a single object.
    Updating settings: Saves a specific configuration object identified by a key.
    Loading a specific section: Retrieves a particular configuration object by its key.
    Initializing default values: Ensures default values are set for specified keys if they do not exist.

#### LocalStorage Implementation

_LocalStorage is the concrete implementation of ILocalStorage, handling the actual file operations, JSON serialization, and deserialization processes. It focuses on:_

    Ensuring the existence of the storage file and directory.
    Reading from and writing to the launcherSettings.json file.
    Serializing objects to JSON for storage and deserializing them back into .NET objects.
___
## Data Model

#### AllSettings

_AllSettings serves as the root object for all stored configurations. It aggregates:_

    DDraw options (DdrawOptions)
    File update model (FileUpdateModel)
    Selected author and filter for loot filters (SelectedAuthorAndFilter)
    Author list for loot filters (Pd2AuthorList)
    Launcher arguments (LauncherArgs)
    News items (News)
___
## Model Definitions

Models such as `DdrawOptions`, `FileUpdateModel`, `SelectedAuthorAndFilter`, `Pd2AuthorList`, and `News` encapsulate specific settings or data structures relevant to various launcher functionalities, such as rendering options, update sources, and news management.
Usage

_The LocalStorage system is integrated throughout the launcher to manage settings related to graphics options, updates, loot filters, and more. For example, when a user selects a new loot filter, the SelectedAuthorAndFilter object is updated and persisted through ILocalStorage.Update(). Similarly, on launcher startup, settings are loaded via ILocalStorage.Load() to initialize the user interface with the previously selected options.
Extensibility__

Developers looking to add new settings or data models to the launcher's local storage system can do so by:

    Defining a new model class for the configuration or data.
    Extending the AllSettings class to include the new model.
    Implementing the necessary ILocalStorage methods to support loading, updating, and initializing the new model.