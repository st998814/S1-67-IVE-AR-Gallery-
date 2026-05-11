# Unity Test Runner – EditMode Regression Tests

## Overview

This project includes a suite of EditMode unit tests that verify the core coordinate
transformation and placement logic used in the AR Gallery.

| Test Class | Tests | What It Covers |
|---|---|---|
| `PlacementOffsetTests` | 7 | Content placement offset formula (X step, fixed Y/Z, default scale) |
| `ScaleClampTests` | 7 | Scale clamping via `Mathf.Max(0.1f, scale)` — boundary & negative values |
| `Vector3DataTests` | 12 | `Vector3Data` constructor precision and round-trip conversion |

## Running the Tests

1. Open the project in Unity Editor
2. Go to **Window > General > Test Runner**
3. Select the **EditMode** tab
4. Click **Run All**

All 26 tests should pass with no failures.

## Assembly Structure

- **`ARGallery.Models`** (`Assets/Scripts/Target/Models/`)  
  Production assembly containing `Vector3Data` and related model classes.

- **`ARGallery.Tests.EditMode`** (`Assets/Tests/EditMode/`)  
  Editor-only test assembly. References `ARGallery.Models` and Unity Test Framework.

## Adding New Tests

Place new test `.cs` files under `Assets/Tests/EditMode/`. They will be picked up
automatically by the `ARGallery.Tests.EditMode` assembly on next compile.
