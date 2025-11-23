# Master Thesis - Unity Radio Propagation Simulation
## Overview

This project was created for a Master Thesis about creating and evaluating a Unity-based simulation environment for analyzing and visualizing radio signal propagation in urban areas.  

It uses:

- **Unity 6.2**
- **Mapbox Maps SDK for Unity**
- **Custom propagation models** (FSPL, Log-Distance, Hata, COST-231, Ray Tracing)
- **Real-world terrain and building data** from Mapbox

The repository contains the complete Unity project, including scripts.

## Features
- Real-time radio signal computation (FSPL, Log-Distance, Hata, COST-231)
- Deterministic ray-tracing (LOS, reflections, diffractions, scattering)
- Mapbox-based 3D terrain & building loading with custom materials
- Heatmap generation for coverage visualization
- Connection line and ray paths visualizations
- Interactive UI for toggling models, buildings, terrain, grid, etc.
- Scene management for creating and loading scenes from JSON files
- CSV and screenshot export

## Requirements

### Software
- Unity Editor v6000.2.7f2 (Download [here](https://unity.com/releases/editor/whats-new/6000.2.7f2#installs))
- Mapbox Maps SDK for Unity **v2.1.1** (Docs: https://docs.mapbox.com/unity/maps/guides/)
- Visual Studio / VS Code (optional, for viewing/editing script files)
- A free Mapbox account (required for generating a token)

### Platforms
- Windows (tested)
- macOS (not tested)

---

## Opening the Project in Unity
1. Clone or download the repository:

   ```bash
   git clone <repo-url>

2. Open **Unity Hub**
3. Select **Add project** → choose the cloned folder
4. Insert your Mapbox token (see below)
5. Open the main scene: Assets/Project/Scenes/Main/UrbanEnvironment.unity
6. Press **Play** and switch to the **Game** view

## Mapbox Access Token (Required Before First Run)
The project does **not** include a Mapbox access token.  
To run the project in the Unity Editor, you must insert your own token.

### Steps
1. Create a free Mapbox account: https://account.mapbox.com
2. Go to **Tokens → Create a new default token** (or click [here](https://mapbox.com/studio/accounts/tokens/))
3. Open the project in Unity
4. In the top menu, open: **Mapbox → Setup**
5. Paste your token into the **Access Token** field
6. Press **Submit**

## Building the Project
1. In Unity, go to `File → Build Settings`
2. Select **Windows / Mac** depending on your target
3. Ensure the main scene is added to the build list
5. Click **Build**
6. Create folder "Project" in user data directory:
   - Windows: Build/MasterThesis_Data/
   - MacOS: Build.app/Contents/Resources/Data/
7. Copy "Assets/Project/Data" folder from the repository into the created directory

## Folder Structure
- Assets/
  - Project/
    - Data/       # Simulation exports, CSV outputs, screenshots
    - Materials/  # Heatmaps, visualization materials
    - Prefabs/    # Transmitter, receiver prefabs
    - Scenes/     # Main scene
    - Scripts/    # Propagation models, ray tracing, utilities
    - Shader/     # Heatmap shader
   
## Controls
### Camera Movement
- Hold Right Mouse Button: Look around
- W / A / S / D: Move the camera
- Q / E: Move the camera up/down
- Shift: Increase movement speed
- C: Move the camera to a fixed top-down view

### Object Navigation
- T: Cycle through Transmitters and focus the camera on each one
- R: Cycle through Receivers and focus the camera on each one
- Tab: Cycle through all Transmitters/Receivers
- F: Focus the camera on the currently selected object
- ESC: Deselect the current object and close the Status Info Panel

### Placing & Selecting Objects
- Place Transmitter/Receiver:
  - Click Add Transmitter or Add Receiver, then left-click in the scene to place it (snaps to grid)
- Select Transmitter/Receiver:
  - Hover over an object and left-click to select it

### UI Panels
- Toggle UI Panels:
  - Enable/disable UI panels by clicking their buttons in the top bar
- Simulation Controls Panel:
  - Create/delete transmitters or receivers, configure parameters, toggle visualizations
- Scenario Management Panel:
  - Load/delete scenarios, create/save scenarios, export current data, create a screenshot
- Status Info Panel:
  - View and edit parameters of the selected transmitter/receiver, delete the object
- Heatmap Legend Panel:
  - View heatmap color legend and modify heatmap resolution
 
## Performance Issues
- Higher heatmap resolutions have longer loading times with the Ray Tracing Model
- Ray visualizations should be turned of when turning the heatmap on

## Author
Merlin Schumann  
Master Thesis, TU Berlin (2025)
