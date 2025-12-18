# Class Selection UI Setup Instructions for MainMenu.unity

## Overview
You need to add a class selection dropdown panel to the MainMenu scene's lobby area. This panel will allow players to select their class before the game starts.

## Step-by-Step Setup Instructions

### Step 1: Create the Class Selection Panel
1. Open the **MainMenu.unity** scene in the Unity Editor
2. In the Hierarchy, find the **Canvas** that contains the lobby UI
3. Under the Canvas, look for the **LobbyPanel** GameObject
4. Create a new **Panel** as a child of Canvas (or LobbyPanel):
   - Right-click → UI → Panel (or Panel, Image)
   - Rename it to `ClassSelectionPanel`
5. Position and size it appropriately within your lobby UI (e.g., bottom right, above or beside player list)

### Step 2: Add Text Label
1. Create a new **TextMeshPro - Text** as a child of ClassSelectionPanel:
   - Right-click ClassSelectionPanel → TextMeshPro - Text
   - Rename to `Label`
   - Set text to "Select Your Class"
   - Adjust size and anchor it appropriately

### Step 3: Add Dropdown UI Component
1. Create a new **TMP_Dropdown** as a child of ClassSelectionPanel:
   - Right-click ClassSelectionPanel → TextMeshPro - Dropdown
   - Rename to `ClassDropdown`
2. Add initial dummy options (these will be populated at runtime):
   - Select ClassDropdown in Hierarchy
   - In the Inspector, find the TMP_Dropdown component
   - Set **Options** count to 5
   - Fill in with dummy values: Fighter, Ranger, Elementalist, Assassin, Enchanter
   - These will be replaced dynamically but this ensures proper initialization

### Step 4: Create LobbyClassSelectionUI Component
1. Select the **ClassSelectionPanel** GameObject
2. In the Inspector, click **Add Component**
3. Search for and add **LobbyClassSelectionUI** component
4. In the LobbyClassSelectionUI component settings:
   - Drag **ClassSelectionPanel** into the "Class Selection Panel" field
   - Drag **ClassDropdown** into the "Class Dropdown" field

### Step 5: Wire Up NetworkMenu
1. Select the **NetworkMenu** GameObject (should be on the Canvas or a parent)
2. In the NetworkMenu component in Inspector, find the section "UI References - Lobby"
3. Locate the field **Class Selection UI** (it should be empty)
4. Drag the **ClassSelectionPanel** (or the GameObject with LobbyClassSelectionUI) into this field

### Step 6: Assign PlayerClass Assets
1. Select any **PlayerController** prefab in Assets/Prefabs (or the player prefab used in game)
2. Find the **PlayerClassManager** component
3. In the Inspector, find the **Available Classes** array (should be size 5)
4. If not already assigned, drag the class assets into the array:
   - Assets/Data/Fighter.asset
   - Assets/Data/Ranger.asset
   - Assets/Data/Elementalist.asset
   - Assets/Data/Assassin.asset
   - Assets/Data/Enchanter.asset

**NOTE:** The order matters! They should be in the order of the `PlayerClassType` enum:
```
0: Fighter
1: Ranger (default)
2: Elementalist
3: Assassin
4: Enchanter
```

### Step 7: Testing
1. Save the scene
2. Play in the Editor
3. Click Host or Join to enter the lobby
4. You should see the Class Selection dropdown in the lobby
5. Try selecting different classes from the dropdown
6. If hosting, verify that other players see your selection
7. The class should persist and be used when the game starts

## Expected Behavior

### In Lobby
- Dropdown shows all 5 class names: Fighter, Ranger, Elementalist, Assassin, Enchanter
- Default selection is **Ranger**
- When you change the dropdown value, it syncs to the server
- Other players' selections are displayed (visible via events if UI is extended later)

### On Game Start
- When players spawn in the game scene, PlayerClassManager reads their selected class from LobbyManager
- The selected class abilities are loaded instead of defaulting to Fighter/Ranger

## Troubleshooting

**Dropdown shows no options:**
- Verify PlayerClass assets are assigned to the PlayerClassManager in the player prefab
- Check that the array is in the correct order matching the enum

**Class selection doesn't persist to game:**
- Verify LobbyManager is added to the menu scene as its own GameObject
- Check that NetworkManager is properly set up with player prefab assigned

**Dropdown is grayed out / not interactive:**
- Verify CanvasGroup (if present) has Interactable = true
- Check that the dropdown component is properly enabled

**Error about LobbyManager.Instance being null:**
- Ensure NetworkMenu.cs's ShowLobby() is being called
- LobbyManager must be instantiated before accessing GetPlayerClass()

