## Scripts/Target/

This folder contains all logic related to the **Image Target system** and **content placement workflow**.

### Structure

- **Components/**
  - MonoBehaviour scripts attached to scene objects.
  - Provide metadata and identifiers.
  - Example: `ImageTargetPlaceholder` (stores targetId).

- **Models/**
  - Pure data structures used for placement and serialization.
  - Example: placement records, vector data, target-content mapping.

- **Managers/**
  - Handle high-level logic and coordination.
  - Example:
    - Target selection
    - Content creation and management

- **Controllers/**
  - Responsible for manipulating scene objects.
  - Example:
    - Transform controls (position, rotation, scale)
    - Object interaction

- **Services/**
  - Handle data processing and system-level operations.
  - Example:
    - Binding scene objects to data model
    - Exporting placement configuration (JSON)

- **Upload/**
  - Feature-specific upload logic related to targets.
  - Example:
    - Uploading target images or media assets

### Notes

- This module follows a **feature-based architecture**.
- Clear separation between:
  - Scene representation (Components)
  - Data (Models)
  - Logic (Managers / Controllers)
  - Integration (Services)

- Enables scalability and easier maintenance as the target system grows.