# Lobby UI Setup Guide - MainMenu Scene

Complete step-by-step instructions for implementing the new lobby panel UI system.

---

## Prerequisites

Before starting, ensure you have:
- All new lobby scripts compiled without errors
- `ClassRegistry` GameObject in the scene with classes assigned
- `LobbyManager` GameObject in the scene
- Tab icon sprites ready (Chat, Character, Settings icons)
- A checkmark sprite for ready indicators

---

## Part 1: Create LobbyChatManager GameObject

**Location:** Root of scene (sibling to LobbyManager)

1. Create empty GameObject: `LobbyChatManager`
2. Add component: `LobbyChatManager` script
3. Position: (0, 0, 0) - doesn't matter, not visible

**Purpose:** Manages networked chat messages. Must exist in scene for chat to work.

---

## Part 2: Create Chat Message Prefab

**Location:** Assets/Prefabs/UI/

1. Create new GameObject in scene: `ChatMessage`
2. Add component: `RectTransform`
3. Add component: `TextMeshProUGUI`
   - Font Size: 14-16
   - Alignment: Left
   - Wrapping: Enabled
   - Overflow: Overflow
   - Auto Size: Off
4. Set RectTransform:
   - Anchor Presets: Stretch (horizontal)
   - Left: 5, Right: 5, Top: 0, Bottom: 0
   - Height: Auto (let text expand)
5. Add component: `ContentSizeFitter`
   - Horizontal Fit: Unconstrained
   - Vertical Fit: Preferred Size
6. Add component: `LayoutElement`
   - Preferred Height: 20 (minimum)
   - Flexible Height: 1
7. Drag to Prefabs folder to create prefab
8. Delete from scene

**Purpose:** Each chat message will be instantiated from this prefab.

---

## Part 3: Restructure Lobby Panel Hierarchy

**Location:** Canvas → LobbyPanel (already exists)

### 3.1 - Main "Phone" Panel Container

The `LobbyPanel` is your "phone" - a single consolidated UI panel that displays different content based on which tab is active.

1. Select existing `LobbyPanel`
2. Set RectTransform:
   - Anchor: Center (or position where you want the phone)
   - Width: 400-600px (phone width)
   - Height: 600-800px (phone height)
   - Position: Positioned on screen (left, center, or right)
3. Add `Image` component for the phone background/frame
   - Color: Semi-transparent dark (0, 0, 0, 200)
   - Or use a custom phone frame sprite

### 3.2 - Create Internal Structure

Under `LobbyPanel`, create:

```
LobbyPanel (the "phone")
├── TabButtonBar (RectTransform) ← Tab buttons at top or bottom
└── ContentArea (RectTransform) ← Swappable content goes here
    ├── ChatPanel (GameObject)
    ├── CharacterSelectPanel (GameObject)
    └── SettingsPanel (GameObject)
```

**ContentArea Setup:**
- RectTransform: Below (or above) TabButtonBar, fills remaining space
- Anchor: Stretch
- Top: 60 (if tabs at top), Bottom: 0
- Left: 0, Right: 0
- All child panels will be the same size and toggle active/inactive

---

## Part 4: Build Lobby Panel Content (Tabbed "Phone" UI)

### 4.1 - Tab Button Bar

Under `LobbyPanel`, create: `TabButtonBar`

```
TabButtonBar (RectTransform)
├── ChatTabButton (Button)
│   ├── Icon (Image) ← Your chat icon sprite
│   └── Label (TextMeshProUGUI) [optional]
├── CharacterTabButton (Button)
│   ├── Icon (Image) ← Your character icon sprite
│   └── Label (TextMeshProUGUI) [optional]
└── SettingsTabButton (Button)
    ├── Icon (Image) ← Your settings icon sprite
    └── Label (TextMeshProUGUI) [optional]
```

**TabButtonBar Setup:**
- RectTransform: Anchor top, stretch horizontal
- Height: 50-60px
- Add `HorizontalLayoutGroup`:
  - Spacing: 10
  - Child Force Expand: Width = true
  - Padding: 5px all sides

**Each Tab Button:**
- Add `Image` component (background)
- Add `Button` component
- Transition: Color Tint
- Icon child: 32x32 image centered
- Store reference to Icon `Image` component for highlighting

### 4.2 - Chat Panel

Under `LobbyPanel → ContentArea`, create: `ChatPanel`

```
ChatPanel (RectTransform)
├── MessageScrollRect (ScrollRect)
│   ├── Viewport (Mask, Image)
│   │   └── MessageContainer (RectTransform, VerticalLayoutGroup)
│   └── Scrollbar (optional)
└── InputArea (RectTransform)
    ├── ChatInputField (TMP_InputField)
    └── SendButton (Button) [optional if using Enter]
```

**ChatPanel Setup:**
- RectTransform: Stretch (fills entire ContentArea)
- Anchor: Stretch
- Left/Right/Top/Bottom: 0
- Add component: `LobbyChatUI` script
- Set Active: false (initially hidden, activated by tab switch)

**MessageScrollRect Setup:**
- Component: `ScrollRect`
- Content: MessageContainer
- Vertical Scrollbar: Optional
- Movement Type: Elastic
- Inertia: true

**MessageContainer Setup:**
- Component: `VerticalLayoutGroup`
  - Child Alignment: Upper Left
  - Child Force Expand: Width = true, Height = false
  - Spacing: 2-5px
  - Padding: 10px all sides
- Component: `ContentSizeFitter`
  - Vertical Fit: Preferred Size

**ChatInputField Setup:**
- Component: `TMP_InputField`
- Placeholder: "Type message..."
- Character Limit: 500
- Line Type: Multi Line Submit
- Height: 40-50px

**Wire LobbyChatUI References:**
- `messageScrollRect` → MessageScrollRect
- `messageContainer` → MessageContainer
- `chatInputField` → ChatInputField
- `messagePrefab` → Your ChatMessage prefab
- `sendButton` → SendButton (if using)

### 4.3 - Character Select Panel

Under `LobbyPanel → ContentArea`, create: `CharacterSelectPanel`

```
CharacterSelectPanel (RectTransform)
├── CarouselArea (RectTransform)
│   ├── LeftArrowButton (Button)
│   │   └── Arrow Icon (Image) ← Left arrow sprite
│   ├── ClassDisplayArea (RectTransform)
│   │   ├── ClassIcon (Image) ← Shows current class icon
│   │   └── ClassName (TextMeshProUGUI)
│   └── RightArrowButton (Button)
│       └── Arrow Icon (Image) ← Right arrow sprite
├── ActionButtonArea (RectTransform)
│   ├── SelectButton (Button)
│   │   └── Text: "Select"
│   └── ViewButton (Button)
│       └── Text: "View Details"
└── SelectedIndicator (GameObject)
    └── Text (TextMeshProUGUI): "SELECTED"
```

**CharacterSelectPanel Setup:**
- RectTransform: Stretch (fills entire ContentArea)
- Anchor: Stretch
- Left/Right/Top/Bottom: 0
- Add component: `CharacterSelectPanel` script
- Set Active: true (default active tab content)

**ClassIcon Setup:**
- Size: 128x128 or larger
- Image Type: Simple
- Preserve Aspect: true
- Color: White

**SelectedIndicator Setup:**
- Position: Below ClassIcon
- Color: Green or highlight color
- Set Active: false (shown when current class is selected)

**Wire CharacterSelectPanel References:**
- `currentClassIcon` → ClassIcon Image
- `currentClassName` → ClassName TMP
- `leftArrowButton` → LeftArrowButton
- `rightArrowButton` → RightArrowButton
- `selectButton` → SelectButton
- `viewButton` → ViewButton
- `selectedIndicator` → SelectedIndicator GameObject
- `selectedText` → Text inside SelectedIndicator (optional)
- `tabController` → (will set later)
- `characterViewPanel` → (will reference external panel created in Part 5)
- `defaultClassSprite` → Fallback sprite if class has no icon

### 4.4 - Settings Panel

Under `LobbyPanel → ContentArea`, create: `SettingsPanel`



```
SettingsPanel (RectTransform)
├── VoiceChatRow (RectTransform)
│   ├── Label (TextMeshProUGUI): "Voice Chat"
│   ├── VoiceChatToggle (Toggle)
│   └── StatusLabel (TextMeshProUGUI): "ON" / "OFF"
├── MusicVolumeRow (RectTransform)
│   ├── Label (TextMeshProUGUI): "Music Volume"
│   ├── MusicVolumeSlider (Slider)
│   └── VolumeLabel (TextMeshProUGUI): "80%"
└── SFXVolumeRow (RectTransform)
    ├── Label (TextMeshProUGUI): "SFX Volume"
    ├── SFXVolumeSlider (Slider)
    └── VolumeLabel (TextMeshProUGUI): "80%"
```

**SettingsPanel Setup:**
- RectTransform: Stretch (fills entire ContentArea)
- Anchor: Stretch
- Left/Right/Top/Bottom: 0
- Add component: `LobbySettingsPanel` script
- Set Active: false (hidden until Settings tab clicked)

**Each Row:**
- Height: 50-60px
- Horizontal spacing for label → control → value

**Sliders:**
- Min Value: 0
- Max Value: 100
- Whole Numbers: true

**Wire LobbySettingsPanel References:**
- `voiceChatToggle` → VoiceChatToggle
- `voiceChatLabel` → VoiceChatRow StatusLabel
- `musicVolumeSlider` → MusicVolumeSlider
- `musicVolumeLabel` → MusicVolumeRow VolumeLabel
- `sfxVolumeSlider` → SFXVolumeSlider
- `sfxVolumeLabel` → SFXVolumeRow VolumeLabel

### 4.5 - Add Tab Controller

On `LobbyPanel` or a dedicated manager GameObject:
1. Add component: `LobbyTabController` script
2. Wire up references:
   - `chatTabButton` → ChatTabButton
   - `characterTabButton` → CharacterTabButton
   - `settingsTabButton` → SettingsTabButton
   - `chatTabImage` → ChatTabButton Icon Image
   - `characterTabImage` → CharacterTabButton Icon Image
   - `settingsTabImage` → SettingsTabButton Icon Image
   - `chatPanel` → ChatPanel GameObject
   - `characterSelectPanel` → CharacterSelectPanel GameObject
   - `settingsPanel` → SettingsPanel GameObject
   - `characterViewPanel` → CharacterViewPanel GameObject (the external floating panel)

3. Now go back and wire cross-references:
   - CharacterSelectPanel: `characterViewPanel` → CharacterViewPanel GameObject

---

## Part 5: Additional UI Elements (Outside the "Phone")

These elements exist outside the main LobbyPanel:

### 5.1 - Character View Panel (Floating Details Panel)

Under `Canvas`, create: `CharacterViewPanel`

```
CharacterViewPanel (RectTransform + Image background)
├── HeaderArea (RectTransform)
│   ├── ClassNameText (TextMeshProUGUI): "NAME"
│   └── CloseButton (Button)
│       └── Icon (Image): "X" close icon
├── ClassArtImage (Image) ← Large character portrait
├── ClassDescriptionText (TextMeshProUGUI)
├── AbilitiesArea (RectTransform)
│   ├── Ability1 (RectTransform)
│   │   ├── Icon (Image)
│   │   ├── Name (TextMeshProUGUI): "Q - Ability Name"
│   │   └── Description (TextMeshProUGUI)
│   ├── Ability2 (RectTransform)
│   │   ├── Icon (Image)
│   │   ├── Name (TextMeshProUGUI): "E - Ability Name"
│   │   └── Description (TextMeshProUGUI)
│   └── Ability3 (RectTransform)
│       ├── Icon (Image)
│       ├── Name (TextMeshProUGUI): "R - Ability Name"
│       └── Description (TextMeshProUGUI)
```

**CharacterViewPanel Setup:**
- Position: To the right of LobbyPanel (or wherever desired)
- Width: 400-600px
- Height: 600-800px (similar to lobby panel)
- Background: Semi-transparent panel with border
- Add component: `CharacterViewPanel` script
- Set Active: false (hidden until "View" button clicked)

**CloseButton Setup:**
- Position: Top-right corner
- Size: 32x32
- Wire OnClick to close the panel

**ClassArtImage:**
- Size: 300x300 or larger
- Preserve Aspect: true
- Centered in upper portion of panel

**Each Ability Container:**
- Horizontal Layout Group (icon on left, text on right)
- Spacing: 10px
- Icon size: 48x48

**Wire CharacterViewPanel References:**
- `classArtImage` → ClassArtImage
- `classNameText` → ClassNameText
- `classDescriptionText` → ClassDescriptionText
- `ability1Icon` → Ability1 Icon
- `ability1NameText` → Ability1 Name
- `ability1DescriptionText` → Ability1 Description
- (repeat for ability2, ability3)
- `closeButton` → CloseButton (this will call the same hide logic as View button)
- `defaultClassSprite` → Fallback sprite
- `defaultAbilityIcon` → Fallback ability icon

**Wire CharacterSelectPanel Reference:**
- Go back to CharacterSelectPanel
- `characterViewPanel` → CharacterViewPanel GameObject

**Behavior:**
- Clicking "View" button on CharacterSelectPanel toggles this panel on/off
- Clicking "X" close button hides the panel
- Panel automatically updates to show the currently selected class from the carousel as you navigate with arrow buttons
- Panel automatically closes when switching to Chat or Settings tabs (only visible when Character tab is active)

### 5.2 - Character Art Display (Optional)

If you want to show large character art outside the phone panel:

Under `Canvas`, create: `CharacterArtDisplay`

```
CharacterArtDisplay (Image)
```

**Setup:**
- Position: To the right or left of LobbyPanel
- RectTransform: Size 400x400 (or larger)
- Image Type: Simple
- Preserve Aspect: true
- Raycast Target: false

**Purpose:** Shows the large character art of the currently selected class.

### 5.3 - Player List Panel (Separate from Phone)

Under `Canvas`, create: `PlayerListPanel`

```
PlayerListPanel (RectTransform + Image background)
├── TopSection (RectTransform)
│   ├── PlayerCountText (TextMeshProUGUI): "Players: 0/5"
│   └── AllPlayersReadyText (TextMeshProUGUI): "Waiting..."
├── PlayerListArea (RectTransform)
│   └── PlayerListContainer (RectTransform + VerticalLayoutGroup)
└── BottomSection (RectTransform)
    ├── ReadyButton (Button)
    │   └── ReadyButtonText (TextMeshProUGUI): "Ready!"
    ├── StartGameButton (Button) ← Host only
    │   └── Text: "Start Game"
    └── LeaveLobbyButton (Button)
        └── Text: "Leave Lobby"
```

**PlayerListPanel Setup:**
- Position: To the right of LobbyPanel
- Width: 250-300px
- Background: Semi-transparent panel

**PlayerListContainer Setup:**
- Component: `VerticalLayoutGroup`
  - Child Alignment: Upper Center
  - Spacing: 5px
  - Padding: 10px
- Component: `ContentSizeFitter`
  - Vertical Fit: Preferred Size

**ReadyButton Setup:**
- Normal Color: Green (0.4, 0.8, 0.4)
- Pressed Color: Darker green
- Height: 50px

**StartGameButton Setup:**
- Initially disabled (will enable when all ready)
- Height: 50px
- Set Active: false (NetworkMenu will show for host only)

---

## Part 6: Alternative - Integrated Layout

If you want player list and ready button INSIDE the phone panel, add them to each content panel that needs them (typically CharacterSelectPanel). This keeps everything in one consolidated UI.

---

## Part 7: Update LobbyPlayerEntry Prefab

**Location:** Assets/Prefabs/UI/LobbyPlayerEntry

Find your existing prefab or create new:

```
LobbyPlayerEntry
├── Background (Image) [optional]
├── PlayerNameText (TextMeshProUGUI)
├── HostIndicatorText (TextMeshProUGUI): "(Host)"
└── ReadyIndicator (GameObject)
    ├── CheckmarkIcon (Image) ← Checkmark sprite
    └── ReadyText (TextMeshProUGUI): "Ready" [optional]
```

**ReadyIndicator Setup:**
- Position: Right side of entry
- CheckmarkIcon: 24x24, green color
- Set Active: false (shown when player is ready)

**Update LobbyPlayerEntry Script References:**
- `playerNameText` → PlayerNameText
- `hostIndicatorText` → HostIndicatorText
- `backgroundImage` → Background Image
- `readyIndicator` → ReadyIndicator GameObject
- `readyIcon` → CheckmarkIcon Image
- `readyText` → ReadyText TMP (if using)

---

## Part 8: Wire NetworkMenu References

Select the GameObject with `NetworkMenu` component (usually NetworkManager or NetworkMenuManager).

**New References to Wire:**

### Lobby Panels:
- `lobbyTabController` → LobbyTabController component
- `lobbyChatUI` → LobbyChatUI component on ChatPanel
- `characterSelectPanel` → CharacterSelectPanel component
- `characterViewPanel` → CharacterViewPanel component (the external floating panel)
- `lobbySettingsPanel` → LobbySettingsPanel component

### Character Art (if using separate display):
- `characterArtDisplay` → CharacterArtDisplay Image (if outside phone panel)
- `defaultCharacterSprite` → A default/placeholder sprite

### Ready System:
- `readyButton` → PlayerListPanel ReadyButton (if outside phone)
- `readyButtonText` → ReadyButton Text child
- `allPlayersReadyText` → PlayerListPanel AllPlayersReadyText

**Existing References (verify these are still connected):**
- `lobbyPanel` → LobbyPanel GameObject (the "phone" panel)- `playerListPanel` → PlayerListPanel GameObject (the separate player list container)- `startGameButton` → PlayerListPanel StartGameButton
- `leaveLobbyButton` → PlayerListPanel LeaveLobbyButton
- `playerCountText` → PlayerListPanel PlayerCountText
- `playerListContainer` → PlayerListContainer Transform
- `playerEntryPrefab` → Your updated LobbyPlayerEntry prefab

---

## Part 9: Testing Checklist

### Solo Test (Host):
1. **Enter Play Mode**
2. Click "Play" → "Host"
3. **Verify lobby "phone" panel loads:**
   - ✓ LobbyPanel is visible and positioned correctly
   - ✓ Character tab is active by default (highlighted)
   - ✓ Character select panel shows inside the phone with Ranger (or first class)
   - ✓ Left/right arrows work
   - ✓ "Select" button works, shows "SELECTED" indicator
4. **Click "View" button:**
   - ✓ Character view panel appears as separate floating panel
   - ✓ Shows class art, name, description
   - ✓ Shows Q/E/R ability details
   - ✓ Click "View" again or "X" button to hide panel
   - ✓ Panel stays visible when switching tabs
5. **Click Chat tab:**
   - ✓ Phone content switches to chat panel
   - ✓ Chat tab icon is highlighted
   - ✓ Can type and send messages (will only see your own)
   - ✓ Character view panel auto-closes (if it was open)
6. **Click Settings tab:**
   - ✓ Phone content switches to settings panel
   - ✓ Settings tab icon is highlighted
   - ✓ Sliders work (check console for debug logs)
   - ✓ Character view panel auto-closes (if it was open)
7. **Check player list (if outside phone):**
   - ✓ Your name appears in player list
   - ✓ "(Host)" indicator shows
   - ✓ Player count shows "1/5"
8. **Click "Ready!" button:**
   - ✓ Button changes to "Unready"
   - ✓ Button color changes
   - ✓ Checkmark appears next to your name
   - ✓ "All players ready!" message appears
   - ✓ Start Game button becomes enabled
9. **Click Start Game:** Should load game scene

### Two-Player Test:
1. **Host:** Start host (as above)
2. **Client:** Join with host's IP
3. **Verify on both:**
   - ✓ Both players see each other in player list
   - ✓ Host has "(Host)" indicator
   - ✓ Player count shows "2/5"
4. **Chat test:**
   - ✓ Host sends message → Client sees it
   - ✓ Client sends message → Host sees it
   - ✓ Colors: "You:" is blue, other player is white
5. **Ready test:**
   - ✓ Both click "Ready!"
   - ✓ Both see checkmarks on both players
   - ✓ Host's Start Game button enables
   - ✓ Client cannot see Start Game button
6. **Host starts game:** Both load into game scene

---

## Common Issues & Solutions

### Issue: Panels don't switch when clicking tabs
**Solution:** Check that all panel GameObjects are correctly assigned in LobbyTabController, and that each panel has correct SetActive state.

### Issue: Chat messages don't appear
**Solution:** Verify LobbyChatManager GameObject exists in scene, ChatMessage prefab is assigned, and MessageContainer has VerticalLayoutGroup + ContentSizeFitter.

### Issue: Character selection doesn't sync
**Solution:** Ensure LobbyManager is in scene and initialized, CharacterSelectPanel calls correct LobbyManager methods.

### Issue: Ready button doesn't work
**Solution:** Check LobbyManager has MSG_PLAYER_READY handler registered, RefreshPlayerList passes IsReady parameter.

### Issue: Start Game always disabled
**Solution:** Verify UpdateStartButtonState() is being called, AreAllPlayersReady() returns true when all ready.

### Issue: Player names don't show
**Solution:** Check PlayerNameManager exists, LobbyManager.SendLocalPlayerName() is called on join.

### Issue: Class selection doesn't show abilities
**Solution:** Ensure PlayerClass ScriptableObjects have ability prefabs assigned with AbilityBase components containing AbilityData references.

---

## Visual Polish (Optional)

After basic functionality works:

1. **Add backgrounds/frames** to panels for visual separation
2. **Add header text** to each panel ("Chat", "Character Selection", "Settings")
3. **Style buttons** with custom sprites, hover effects
4. **Add icons** to ability displays in character view
5. **Add scrollbars** to chat and player list if needed
6. **Adjust spacing/padding** for better visual flow
7. **Add subtle animations** for tab switching, ready state changes
8. **Test at different resolutions** (1920x1080, 1280x720, etc.)

---

## Final Notes

- **Save frequently** while building the UI hierarchy
- **Test after each major section** to catch issues early
- **Keep prefabs updated** as you make changes
- **Use Canvas Scaler** for UI scaling across resolutions
- **Anchor UI elements properly** for responsive layouts
- **Check console for errors** during runtime

Once everything is wired up correctly, the lobby should provide a fully functional pre-game experience with chat, class selection, and ready synchronization!
