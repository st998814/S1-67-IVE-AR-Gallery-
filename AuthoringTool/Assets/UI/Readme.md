## UI/

This folder contains all UI Toolkit assets used to build the authoring interface.

### Structure

- **UXML/**
  - Defines the structure and layout of the UI.
  - Equivalent to HTML.
  - Example: panels, buttons, input fields.

- **USS/**
  - Defines the visual styling of UI elements.
  - Equivalent to CSS.
  - Example: colors, spacing, layout, fonts.

- **PanelSettings/**
  - Configuration for UI Toolkit rendering.
  - Controls how UI is displayed in the Game view.

- **Themes/**
  - Contains shared visual resources.
  - May include Unity-provided theme assets (e.g., icons, default styles).

- **Icons/** *(optional)*
  - Project-specific UI icons and visual assets.

### Notes

- UI follows a **separation of concerns**:
  - UXML → structure
  - USS → styling
  - C# (Scripts/UI) → behavior

- Unity default theme assets are kept separate from project-specific UI assets.
- Designed for extensibility and easy UI iteration.