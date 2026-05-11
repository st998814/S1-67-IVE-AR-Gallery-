## Scripts/

This folder contains all runtime and editor scripts for the project, organized by responsibility and feature domains.

### Structure

- **Core/**
  - Shared utilities and low-level systems used across multiple features.
  - Example: common helpers, base classes, WebGL integrations.

- **Target/**
  - Main feature module handling image targets and content placement logic.
  - Includes models, managers, controllers, and services related to target-based AR content.

- **UI/**
  - Handles UI logic and interaction binding between UXML/USS and runtime behavior.
  - Example: button callbacks, data binding, input handling.

- **Upload/** *(optional / if exists outside Target)*
  - General upload-related logic not tied to a specific feature.
  - May include platform bridges (e.g., WebGL upload adapters).

- **Editor/**
  - Unity Editor-only scripts.
  - Custom inspectors, editor tools, and debugging utilities.

### Notes

- Scripts are grouped by **feature and responsibility**, not by type alone.
- Encourages separation of concerns:
  - Models → data
  - Managers → flow control
  - Controllers → object manipulation
  - Services → system-level operations