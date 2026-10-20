<div align="center">

# DropZone 300

---

**A third-person battle-royale shooter prototype for Windows PC, built in Unity 6 (URP).**  
**Inspired by BGMI, engineered from the ground up with zero commercial assets.**

**Task ID:** `BGMIClone` — *GDG on Campus NITK × Iris Web Team Recruitments 2026 (Mentor: Ritvik Gampa)*

[![Unity 6](https://img.shields.io/badge/Engine-Unity%206%20(6000.6.5f1)-black?logo=unity)](https://unity.com/)
[![Render Pipeline](https://img.shields.io/badge/Pipeline-URP-blue)](https://unity.com/srp/universal-render-pipeline)
[![Language](https://img.shields.io/badge/Language-C%23-239120?logo=csharp)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20PC-0078D6?logo=windows)](https://apexphoenix.itch.io/bgmi-clone-gdg)
[![Play on itch.io](https://img.shields.io/badge/Playable%20Build-itch.io-FA5C5C?logo=itchdotio)](https://apexphoenix.itch.io/bgmi-clone-gdg)

</div>

---

<details>
<summary><h2>Table of Contents</h2></summary>

- [Architecture Diagram](#architecture-diagram)
  - [Project Structure](#project-structure)
- [Demo Video & Playable Build](#demo-video--playable-build)
- [Gameplay Screenshots](#gameplay-screenshots)
- [The Problem](#the-problem)
- [Our Solution](#our-solution)
  - [Task Requirements & Feature Matrix](#task-requirements--feature-matrix)
- [How It Works — Core Systems](#how-it-works--core-systems)
  - [1. Camera-Center Raycast Shooting, Spread & Recoil](#1-camera-center-raycast-shooting-spread--recoil)
  - [2. Autonomous Bot AI (Sight, Hearing & Line-of-Sight)](#2-autonomous-bot-ai-sight-hearing--line-of-sight)
  - [3. Humanoid Animation & 2D Velocity Blend Trees](#3-humanoid-animation--2d-velocity-blend-trees)
  - [4. Air Drop Sequence State Machine](#4-air-drop-sequence-state-machine)
  - [5. Loot Generation, Shared Ammo & Backpack Inventory](#5-loot-generation-shared-ammo--backpack-inventory)
- [Controls](#controls)
- [Technologies Used](#technologies-used)
- [Not Implemented & Known Limitations](#not-implemented--known-limitations)
- [Installation & Setup Guide](#installation--setup-guide)
  - [1. Prerequisites](#1-prerequisites)
  - [2. Clone the Repository](#2-clone-the-repository)
  - [3. Open the Project in Unity Hub](#3-open-the-project-in-unity-hub)
  - [4. Load the Main Scene](#4-load-the-main-scene)
  - [5. Run in Editor Play Mode](#5-run-in-editor-play-mode)
  - [6. Build a Standalone Windows Executable](#6-build-a-standalone-windows-executable)
  - [7. Download & Run the Playable Build from itch.io](#7-download--run-the-playable-build-from-itchio)
- [Deployment](#deployment)
- [Team Members & Acknowledgements](#team-members--acknowledgements)
- [Credits & Attributions](#credits--attributions)
- [License & Disclaimer](#license--disclaimer)

</details>

---

## Architecture Diagram

The diagram below illustrates how our gameplay scripts, procedural world generators, AI agents, and code-built UI canvases interact at runtime inside `Assets/Scenes/bgmiclone_plane.unity`:

~~~~mermaid
flowchart LR
    classDef orangeNode fill:#d97706,stroke:#fbbf24,stroke-width:2px,color:#ffffff,font-weight:bold;
    classDef yellowNode fill:#eab308,stroke:#fef08a,stroke-width:2px,color:#111827,font-weight:bold;
    classDef cyanNode fill:#0284c7,stroke:#7dd3fc,stroke-width:2px,color:#ffffff,font-weight:bold;
    classDef greenNode fill:#16a34a,stroke:#86efac,stroke-width:2px,color:#ffffff,font-weight:bold;
    classDef darkNode fill:#1e293b,stroke:#94a3b8,stroke-width:2px,color:#f8fafc,font-weight:bold;

    subgraph LobbyAndDrop["1. Match Flow & Air Drop Sequence"]
        direction TB
        MML["MainMenuLobby.cs<br/>(3D Sky Stage & Order 20 UI)"]:::orangeNode
        MSS["MatchStartSequence.cs<br/>(C-130 Plane -> Freefall -> Parachute -> Land)"]:::orangeNode
        MML ==>|"START clicked: unlocks match,<br/>restores HUD, triggers sequence"| MSS
    end

    subgraph PlayerRoot["2. Player Systems ('Player' Root)"]
        direction TB
        PM["PlayerMovement.cs<br/>(Move, Crouch, Jump, Fall Damage)"]:::yellowNode
        CC["CharacterController<br/>(Capsule Physics)"]:::darkNode
        PA["PlayerAnimator.cs<br/>(2D Velocity Blend Tree)"]:::yellowNode
        ANIM["Animator<br/>(Medea Humanoid Rig)"]:::darkNode
        SPS["StarterPistolSetup.cs<br/>(9mm Pistol + Shared SMG Ammo)"]:::yellowNode
        WS["WeaponSwitcher.cs<br/>(Slots 1-4, Q/E, Scroll)"]:::yellowNode
        GUN["Gun.cs<br/>(Raycast Fire, Reload, Spread, Recoil)"]:::yellowNode
        GE["GunEffects.cs & GunScope.cs & WeaponModel.cs<br/>(Tracers, RedDot/Sniper Scope, RightHand Bind)"]:::darkNode
        SC["ScopeController.cs<br/>(Right-Click FOV Zoom & Sensitivity)"]:::yellowNode
        PINV["PlayerInventory.cs<br/>(Tab Backpack, H Heal, G Drop)"]:::yellowNode
        FS["FootstepSounds.cs<br/>(3 Grass Footstep Clips)"]:::darkNode
        PH["Health.cs & PlayerDeath.cs<br/>(Player HP & PlayerKills Counter)"]:::yellowNode
    end

    subgraph CameraAndUI["3. Camera & Code-Built HUD"]
        direction TB
        VCAM["CinemachineCamera<br/>(Orbital Follow + Rotation Composer)"]:::cyanNode
        GHUD["GameHUD.cs<br/>(HP Bar, Weapon Slots, Kills)"]:::cyanNode
        MMAP["MiniMap.cs<br/>(Top-Down Ortho Camera)"]:::cyanNode
        ROD["RestartOnDeath.cs<br/>(YOU DIED -> Enter Reloads)"]:::cyanNode
    end

    subgraph WorldAndAI["4. Procedural World, Loot & AI Bots"]
        direction TB
        LS["LootSpawner.cs<br/>(~100 Pickups + 3D Models)"]:::greenNode
        MPS["MapPropSpawner.cs<br/>(Enterable Shacks, Trees, Rocks, NavMeshObstacle)"]:::greenNode
        PICK["ItemPickup.cs & WeaponPickup.cs<br/>(Medkit, Bandage, Ammo, Guns)"]:::darkNode
        ES["EnemySpawner.cs<br/>(Spawns Bots on NavMesh)"]:::greenNode
        EAI["EnemyAI.cs & EnemyAnimator.cs<br/>(Patrol, Sight/Hearing LOS, Chase, Shoot)"]:::greenNode
        EH["Health.cs<br/>(Bot HP)"]:::darkNode
    end

    %% Cross-System Data Flow
    MSS ==>|"On F Jump: enables"| ES
    MSS ==>|"On Landing: ResetFall() & enables"| PM
    MSS ==>|"On Landing: enables"| CC & WS & PINV & SC & GUN

    PM --> CC
    PA --> ANIM
    SPS --> WS & GUN
    WS --> GUN
    GUN --> GE
    GUN -.->|"Pitch/Yaw Recoil"| VCAM
    SC -.->|"FOV Zoom & Aim Compensation"| VCAM

    LS --> PICK
    MPS -->|"Spawns interior shack loot"| PICK
    PICK ==>|"Collects items / weapons"| PINV & WS & GUN

    ES --> EAI
    EAI --> EH
    GUN ==>|"Raycast Hit -> TakeDamage()"| EH
    EH -.->|"Increments Health.PlayerKills"| GHUD
    EAI ==>|"Distance-Spread Raycast"| PH
    PH --> GHUD & ROD

    style LobbyAndDrop fill:#451a03,stroke:#f59e0b,stroke-width:2px,color:#fef3c7
    style PlayerRoot fill:#422006,stroke:#eab308,stroke-width:2px,color:#fef9c3
    style CameraAndUI fill:#082f49,stroke:#38bdf8,stroke-width:2px,color:#e0f2fe
    style WorldAndAI fill:#052e16,stroke:#4ade80,stroke-width:2px,color:#dcfce7
~~~~

### Project Structure

~~~~text
Assets/
├── Audio/                           # Gunshot, reload, and grass footstep audio clips (Pixabay)
├── Character/                       # Mixamo humanoid FBX skins (Medea, Ely, X Bot), materials & Animator controllers
│   └── Animations/                  # Mixamo humanoid animation clips (idle, walk, run, crouch, jump, death)
├── Editor/                          # Editor-only utilities (CharacterAnimatorBuilder.cs) excluded from player builds
├── FBX/                             # Base environment mesh assets
├── gunsFBX/                         # Quaternius 3D firearm models (AssaultRifle_2, SubmachineGun_5, SniperRifle_3)
├── kenney_city-kit-commercial_2.1/  # Kenney CC0 modular commercial/apartment buildings & colormap textures
├── Materials/                       # Shared URP Lit environment and prop materials
├── Prefabs/                         # Configured prefabs for Enemy bots, trees, buildings, and pickups
├── Scenes/
│   ├── bgmiclone.unity              # Initial ground-combat prototype scene
│   └── bgmiclone_plane.unity        # Main production scene (3D Lobby + Air Drop + Full Map + Bots)
├── Skyboxes/                        # Skybox materials and lighting settings
├── Ultimate Nature Pack - Jun 2019/ # BirchTree and Rock_2 nature assets
├── GameHUD.cs                       # In-game HUD (health bar, weapon slots, kill counter, death banner)
├── Gun.cs                           # Raycast weapon firing, spread cone, camera recoil, and reloading
├── GunEffects.cs                    # Bullet tracers, muzzle flash, and hit particle puffs
├── GunScope.cs                      # Per-weapon scope configuration (RedDot vs. Sniper FOV/spread)
├── Health.cs                        # Shared health component for Player and Bots + static kill tracking
├── ItemPickup.cs                    # Trigger pickup logic for Medkits, Bandages, and Ammo crates
├── LootSpawner.cs                   # Spawns ~100 ground pickups & upgrades cubes into 3D procedural models
├── MainMenuLobby.cs                 # 3D character showcase lobby, career/controls modals, and START flow
├── MapPropSpawner.cs                # Procedural enterable shacks (with interior loot), trees, rocks & NavMesh carving
├── MatchStartSequence.cs            # Matchmaking overlay, C-130 plane flight, freefall, parachute, and landing
├── Minimap.cs                       # Overhead orthographic minimap camera and UI display
├── PlayerAnimator.cs                # Drives humanoid Animator blend trees from local movement velocity
├── PlayerDeath.cs                   # Handles player death state and disables active combat inputs
├── PlayerInventory.cs               # Backpack UI (Tab), healing item usage (H), and weapon/item dropping (G)
├── PlayerMovement.cs                # Third-person CharacterController movement, crouching, jumping & fall damage
├── RestartOnDeath.cs                # Detects player death and reloads the active scene when pressing Enter
├── ScopeController.cs               # Right-click ADS FOV zoom, Cinemachine aim-point compensation & scope overlays
├── StarterPistolSetup.cs            # Converts the melee slot into a 3D 9mm starter pistol sharing SMG reserve ammo
├── WeaponModel.cs                   # Attaches 3D gun meshes to the humanoid RightHand bone aligned to camera aim
├── WeaponPickup.cs                  # Trigger pickup logic for ground and dropped weapons (preserves mag/reserve ammo)
└── WeaponSwitcher.cs                # Manages owned weapons, 1-4/Q/E/scroll switching, and fallback selection
~~~~

---

## Demo Video & Playable Build

<!-- TODO: Replace (link coming soon) below with your final Demo Video link (YouTube / Google Drive) before submitting -->
- **🎥 Demo Video:** (link coming soon)
- **🎮 Playable Windows Build (itch.io):** [https://apexphoenix.itch.io/bgmi-clone-gdg](https://apexphoenix.itch.io/bgmi-clone-gdg)

---

## Gameplay Screenshots

| 3D Interactive Main Menu Lobby | Enterable Shacks & Open-Field Combat |
| :---: | :---: |
| ![3D Main Menu Lobby](assets/lobby.jpg) | ![Enterable Shacks and Combat](assets/shacks-and-combat.jpg) |
| *3D character turntable, profile card, and mode selector (`MainMenuLobby.cs`)* | *Walk-in wooden shacks, cover boulders, and active HUD (`MapPropSpawner.cs`)* |

| Urban Buildings & 3D Ground Loot | Death Screen & Match Restart Loop |
| :---: | :---: |
| ![City Buildings and Ground Loot](assets/city-and-loot.jpg) | ![Player Death and Restart Prompt](assets/death-screen.jpg) |
| *Kenney city apartments, nature trees, and 3D loot pickups (`LootSpawner.cs`)* | *Humanoid death animation and `Press ENTER to play again` (`RestartOnDeath.cs`)* |

---

## The Problem

The **GDG on Campus NITK × Iris Web Team Recruitments 2026** task (`Task ID: BGMIClone`) challenges us to build a functional 3D battle-royale shooter prototype from scratch using Unity or Godot. Rather than relying on paid templates or pre-built gameplay kits, the objective is to demonstrate a solid engineering grasp of core 3D game-development fundamentals:
- Third-person camera mathematics and input handling
- Character physics, collision detection, and surface-aware movement
- Raycast ballistics, spread cones, and camera recoil
- Autonomous AI perception, NavMesh pathfinding, and obstacle avoidance
- Inventory management, weapon state machines, 3D humanoid animation retargeting, spatial audio, and dynamic UI rendering

---

## Our Solution

We built **DropZone 300**, a self-contained third-person battle-royale prototype in **Unity 6 (6000.6.5f1, URP)**. Every menu, HUD element, scope overlay, aircraft, parachute, enterable shack, and loot crate is constructed dynamically through modular C# scripts, paired with CC0 3D models and Mixamo humanoid animations.

### Task Requirements & Feature Matrix

The table below maps our implementation against Sections 1–9 of the recruitment specification:

| # | Task Section | Implemented Features in DropZone 300 | Status |
| :-: | :--- | :--- | :-: |
| **1** | **Player Mechanics** | Third-person `CharacterController` movement (walk, sprint, crouch with collider height adjustment, jump), health system (`100 HP`), height-based fall damage, bandage/medkit healing, and humanoid death animation. | ✅ Complete |
| **2** | **Weapons & Gunplay** | 4-slot weapon system: **Assault Rifle** (AKM-style), **SMG** (UMP/Uzi-style), **Sniper** (Kar98k-style), and a **Starter 9mm Pistol** (which shares its 9mm reserve ammo pool with the SMG). Supports weapon pickups, switching (`1–4`, `Q`/`E`, scroll wheel), camera-center raycast shooting, magazine/reserve reloading (`R`), dynamic spread cones, smooth Cinemachine pitch/yaw recoil, bullet tracers, muzzle flash, hit puffs, and ADS scopes (**Red-Dot** for Rifle/SMG/Pistol; **Full-Screen Sniper Scope** for Sniper). | ✅ Complete |
| **3** | **Inventory & Loot** | Code-built Backpack screen (`Tab`) with capacity limits (`5 Bandages`, `2 Medkits`), quick-heal key (`H`), and weapon/item dropping (`G`) where dropped weapons retain their exact magazine and reserve ammo. `LootSpawner.cs` scatters ~100 supplies across the terrain and inside walk-in shacks with 3D procedural models. | ✅ Complete |
| **4** | **Map & Environment** | `300×300 m` terrain (`-150` to `+150`) with sculpted hills, Kenney commercial buildings, nature pack trees/rocks, and procedurally generated **enterable wooden shacks** (with doorways, sniper windows, and interior loot), solid bulletproof boulder clusters, and sandbag barricades. Includes a baked `NavMeshSurface`, dynamic `NavMeshObstacle` carving, and a live overhead `MiniMap`. | ✅ Complete |
| **5** | **Match System *(Bonus)*** | 3D interactive character lobby (`MainMenuLobby.cs`), simulated matchmaking counter (`Finding players... 12/20` to `20/20`), deployment countdown, procedural C-130 transport plane flying at `250 m` altitude, jump prompt (`F`), steerable freefall (`50 m/s`), manual/auto parachute deployment (`Space` or `60 m` AGL at `6 m/s`), and zero-damage ground landing. | 🟡 Partial *(Bonus)* |
| **6** | **HUD & UI** | Code-generated uGUI canvases: health bar (`100/100`), top-right kill counter, dynamic crosshair (hides during ADS), bottom-right weapon slots (`1–4`) with live magazine/reserve ammo counts, top-left minimap, and a vertical altitude/speed gauge during the air drop. | ✅ Complete |
| **7** | **AI Bots** | `EnemySpawner` spawns humanoid bots across the NavMesh when the player jumps from the plane. Bots run a perception loop checking sight cone + hearing radius with physics line-of-sight, patrol waypoints, chase via `NavMeshAgent`, navigate around carved obstacles, stop to fire distance-inaccurate raycasts at the player's collider, and play humanoid movement/death animations. | ✅ Complete |
| **8** | **SFX & Animation** | Per-weapon gunshot and reload audio, 3 grass footstep variants, 3D spatial bot gunfire, Mixamo humanoid animations driven by 2D velocity blend trees (idle, walk, run, strafe, crouch, jump, freefall, death), and particle hit feedback. | ✅ Complete |
| **9** | **Multiplayer** | Not implemented (single-player vs. autonomous AI bots). | ❌ Not Implemented |

---

## How It Works — Core Systems

### 1. Camera-Center Raycast Shooting, Spread & Recoil
Each weapon is a child object of `Player` carrying `Gun.cs`, `GunEffects.cs`, `GunScope.cs`, and a 3D mesh bound to `HumanBodyBones.RightHand` via `WeaponModel.cs`.
- **Ballistics & Spread:** When firing, `Gun.Shoot()` casts a ray from `Camera.main` along the camera's forward vector, perturbed by a random cone offset (`currentSpread`) that expands per shot and recovers over time. Holding Right-Click activates `ScopeController.cs`, which narrows the `CinemachineCamera` FOV, scales the `CinemachineRotationComposer` screen position so the crosshair never shifts off-target, lowers mouse sensitivity, and multiplies weapon spread by `GunScope.SpreadMultiplier`.
- **Smooth Recoil:** Each shot queues vertical (`recoilPitch`) and horizontal (`recoilYaw`) kick values that exponentially decay into `CinemachineOrbitalFollow.VerticalAxis` and `HorizontalAxis` every frame.

### 2. Autonomous Bot AI (Sight, Hearing & Line-of-Sight)
Bots are spawned on the baked NavMesh by `EnemySpawner.cs` and controlled by `EnemyAI.cs`:
- **Perception Loop:** Bots periodically evaluate distance and angle to the player. A bot detects the player if the player is within its hearing radius or inside its vision cone with an unobstructed `Physics.Raycast` line-of-sight.
- **Combat & Crouching Support:** Once alerted, the bot chases the player using `NavMeshAgent` and stops within firing range to shoot. Bot raycasts aim at the center of the player's active `CharacterController` collider rather than a fixed height, ensuring bots can still target a crouching player while applying distance-scaled aim inaccuracy.

### 3. Humanoid Animation & 2D Velocity Blend Trees
Both the player (**Medea**) and AI bots (**Ely** / **X Bot**) use Unity's **Humanoid Avatar** retargeting system:
- `PlayerAnimator.cs` converts the player's world velocity into local-space forward (`velZ`) and strafe (`velX`) parameters, feeding a **2D Freeform Directional Blend Tree** that smoothly interpolates across 18 Mixamo clips (standing idle/walk/run/strafe, crouching idle/walk, jump, freefall, and death).
- Whenever `CharacterController.isGrounded` is false during the plane jump, the Animator automatically transitions into the freefall pose without requiring separate animation scripts.

### 4. Air Drop Sequence State Machine
`MatchStartSequence.cs` manages the battle-royale insertion through a 6-stage state machine (`Matchmaking` → `Countdown` → `InPlane` → `Freefall` → `Parachute` → `Landed`):
- During flight and descent, `CharacterController`, `PlayerMovement`, `WeaponSwitcher`, `PlayerInventory`, `ScopeController`, and all `Gun` components are disabled so the script can translate the player transform directly.
- Pressing `F` ejects the player at `250 m` altitude, activates `EnemySpawner`, and enables `WASD` air steering at `50 m/s` terminal velocity.
- Pressing `Space` (or reaching `60 m` above the ground via downward raycast) deploys the procedural canopy and slows descent to `6 m/s`. Touching any terrain or building collider snaps the player to the surface, invokes `PlayerMovement.ResetFall()` to prevent fall damage, and re-enables all combat components.

### 5. Loot Generation, Shared Ammo & Backpack Inventory
- **Procedural 3D Pickups (`LootSpawner.cs`):** Raycasts across the `300×300 m` terrain at startup to spawn ~100 pickups (Medkits, Bandages, Ammo Crates, Rifles, SMGs, Snipers) on flat ground and replaces plain cube primitives with multi-part 3D models.
- **Enterable Shacks & Cover (`MapPropSpawner.cs`):** Generates 10 walk-in wooden shacks (with hollow walls, doorways, window cutouts, and 3 guaranteed interior loot items), 55 solid trees, 40 boulder clusters, and 18 sandbag barricades equipped with `NavMeshObstacle` (`carving = true`) and a post-spawn buried-loot rescue pass.
- **Starter Pistol & Shared 9mm Pool (`StarterPistolSetup.cs`):** Converts Slot 4 into a permanent 9mm Tactical Pistol with a 3D slide/frame model and Red-Dot sight, synchronizing its `reserveAmmo` every frame with the SMG.

---

## Controls

| Key / Input | Action | Context |
| :--- | :--- | :--- |
| **`W` `A` `S` `D`** | Move / Strafe (Ground) or Steer (Freefall & Parachute) | Ground & Air Drop |
| **`Mouse`** | 360° Camera Look & Aim (or Drag Character in Lobby) | Everywhere |
| **`Left Shift`** | Sprint | Ground |
| **`Space`** | Jump (Ground) / Open Parachute (Freefall) | Ground & Air Drop |
| **`C`** | Toggle Crouch | Ground |
| **`Left Mouse Button`** | Fire Equipped Weapon | Ground Combat |
| **`Right Mouse Button` (Hold)** | Aim Down Sights (Red-Dot / Sniper Scope) | Ground Combat |
| **`R`** | Reload Weapon Magazine | Ground Combat |
| **`1` – `4`** / **`Q` `E`** / **`Scroll Wheel`** | Switch Equipped Weapon (`1` Rifle, `2` SMG, `3` Sniper, `4` Pistol) | Ground Combat |
| **`Tab`** | Open / Close Backpack Inventory (Frees Mouse Cursor) | Ground Combat |
| **`H`** | Quick-Use Best Healing Item (Medkit or Bandage) | Ground Combat |
| **`G`** | Drop Currently Equipped Primary/Secondary Weapon | Ground Combat |
| **`F`** | Jump from C-130 Transport Aircraft | Air Drop Sequence |
| **`Escape`** | Unlock Mouse Cursor | In-Game |
| **`Enter` (`Return`)** | Deploy from Lobby / Play Again After Death (`RestartOnDeath.cs`) | Lobby & Death Screen |

---

## Technologies Used

| Technology | Purpose in Project |
| :--- | :--- |
| **Unity 6 (`6000.6.5f1`)** | Core 3D game engine, physics simulation, raycasting, and build pipeline |
| **C#** | All gameplay mechanics, state machines, procedural mesh generation, and UI |
| **Universal Render Pipeline (URP)** | Modern lit rendering, directional shadows, and build-safe material instancing |
| **Unity Cinemachine 3** | Third-person `CinemachineOrbitalFollow` camera, recoil injection, and dynamic FOV scoping |
| **Unity AI Navigation** | `NavMeshSurface` baking, `NavMeshAgent` bot pathfinding, and `NavMeshObstacle` carving |
| **Unity UI (`uGUI`)** | Code-generated screen-space overlays for Lobby (Order 20), Drop HUD (Order 13), Backpack (Order 12), and Scopes (Order 11) |
| **Unity Animator (Humanoid)** | Skeletal retargeting and 2D velocity blend trees for Mixamo character rigs |
| **Adobe Mixamo** | 3D humanoid soldier skins (`Medea`, `Ely`, `X Bot`) and 18 combat/movement animation clips |
| **Visual Studio 2022** | C# IDE and debugging |
| **Git & GitHub** | Version control and source code hosting |
| **itch.io** | Hosting and distribution of the standalone Windows playable build |

---

## Not Implemented & Known Limitations

In accordance with the recruitment guidelines, we want to be completely transparent about the current scope and limitations of this prototype:
- **No Multiplayer Networking:** The game is strictly single-player against autonomous AI bots. The `"Finding players... 12/20"` screen prior to boarding the plane is a cosmetic simulation.
- **No Shrinking Safe Zone (Blue Zone) or Win Screen:** There is no shrinking storm circle and no `"Winner Winner Chicken Dinner"` victory screen when all bots are eliminated; combat continues until the player dies, after which pressing **`Enter`** reloads the match.
- **Desktop Keyboard & Mouse Only:** Built exclusively for Windows PC using Unity's legacy `Input` class (`Input.GetKeyDown` / `Input.GetAxis`). Mobile touch controls and WebGL builds are not included.
- **Cosmetic Urban Buildings:** While the 10 procedurally generated wooden shacks have walk-in interiors and indoor loot, the multi-story Kenney apartment buildings act as solid exterior cover and cannot be entered.

---

## Installation & Setup Guide

### 1. Prerequisites
- **OS:** Windows 10 or Windows 11 (64-bit)
- **Unity Hub** with **Unity 6 (`6000.6.5f1`)** installed (including the *Windows Build Support (IL2CPP / Mono)* module)
- **Git** (or GitHub Desktop)

### 2. Clone the Repository
~~~~bash
git clone https://github.com/aryanatul2008-ship-it/Bgmi-clone.git
cd Bgmi-clone
~~~~
*(Note: If the repository is private for evaluation, ensure your GitHub account has been granted collaborator access).*

### 3. Open the Project in Unity Hub
1. Launch **Unity Hub** and click **Add > Add project from disk**.
2. Select the cloned project folder and open it with **Unity 6000.6.5f1**.
3. Wait for Unity to import packages and compile C# scripts (initial import takes ~1–2 minutes).

### 4. Load the Main Scene
1. In the bottom **Project** window, navigate to **`Assets/Scenes/`**.
2. Double-click **`bgmiclone_plane.unity`** to open the full game scene.

### 5. Run in Editor Play Mode
1. Click the **Play (▶)** button at the top center of the Unity Editor.
2. You will boot into the **3D Character Lobby**. Click **START** (or press **Enter**) to launch the matchmaking overlay, C-130 plane drop, parachute glide, and ground combat!
3. *(Optional)* To skip the lobby or plane drop during testing, select **`Player`** in the Hierarchy and untick `Enable Lobby Menu` on `MainMenuLobby` or `Enable Drop Sequence` on `MatchStartSequence`.

### 6. Build a Standalone Windows Executable
1. Open **File > Build Profiles** (`Ctrl + Shift + B`).
2. Ensure **`Scenes/bgmiclone_plane`** is ticked at index **`0`** in the Scene List.
3. Select **Windows** (`Intel 64-bit`) under Platforms and click **Build**.
4. Choose an output folder (e.g., `Desktop/Itch_WindowsBuild`) and wait for compilation to finish.

### 7. Download & Run the Playable Build from itch.io
1. Visit the itch.io page: **[https://apexphoenix.itch.io/bgmi-clone-gdg](https://apexphoenix.itch.io/bgmi-clone-gdg)**
2. Download the Windows `.zip` archive and extract all files into a folder on your PC (keep `Bgmi clone.exe`, `Bgmi clone_Data`, and `UnityPlayer.dll` together in the same folder).
3. Double-click **`Bgmi clone.exe`** to launch the game.
4. **Windows SmartScreen Note:** Because this is an unsigned student executable, Windows Defender SmartScreen may display a *"Windows protected your PC"* popup. Click **More info** → **Run anyway** to start the game.

---

## Deployment

- **Binary Distribution:** Packaged as a standalone 64-bit Windows DirectX12 executable (`Bgmi clone.exe` + `_Data` directory) compressed into a `.zip` archive and hosted on **itch.io** at [https://apexphoenix.itch.io/bgmi-clone-gdg](https://apexphoenix.itch.io/bgmi-clone-gdg).
- **Shader Stripping Protection:** All runtime procedural meshes clone their base `Material` directly from an active URP primitive instance rather than relying solely on unreferenced `Shader.Find` strings, preventing missing-shader magenta artifacts in standalone player builds.

---

## Team Members & Acknowledgements

- **Developer (Solo Project):** **Aryan Patil** — [@aryanatul2008-ship-it](https://github.com/aryanatul2008-ship-it)
- **Mentor:** **Ritvik Gampa** (*GDG on Campus NITK × Iris Web Team Recruitments 2026*)

---

## Credits & Attributions

We gratefully credit all third-party CC0/free asset creators, sound designers, and tools used in **DropZone 300**:

| Asset / Resource | Author / Source | License | Link | Usage in DropZone 300 |
| :--- | :--- | :--- | :--- | :--- |
| **City Kit (Commercial) 2.1** | Kenney | CC0 1.0 Universal | [kenney.nl](https://kenney.nl/assets/city-kit-commercial) | Multi-story urban commercial buildings & one-story houses (`building-e`, `building-g`, `building-j`) |
| **Ultimate Guns Pack** | Quaternius | CC0 1.0 Universal | [quaternius.com](https://quaternius.com/) | 3D firearm models: `AssaultRifle_2` (Rifle), `SubmachineGun_5` (SMG), and `SniperRifle_3` (Sniper) |
| **Unity Nature Assets Pack** | Unity Asset Store / Quaternius | Free / CC0 | [Asset Store / quaternius.com](https://quaternius.com/) | Base environment trees (`BirchTree`) and rocks (`Rock_2`) |
| **Player Character Skin (`Medea`)** | M. Arrebola via Adobe Mixamo | Mixamo Royalty-Free | [mixamo.com](https://www.mixamo.com/) | Main player third-person character rig & skin (`Solider_Player`) |
| **Enemy Bot Skins (`Ely` & `X Bot`)** | K. Atienza & Adobe Mixamo | Mixamo Royalty-Free | [mixamo.com](https://www.mixamo.com/) | AI bot character skins and humanoid rigs |
| **18 Humanoid Animation Clips** | Adobe Mixamo | Mixamo Royalty-Free | [mixamo.com](https://www.mixamo.com/) | `X Bot`, `Rifle Idle`, `Walk With Rifle`, `Walk Backward`, `Strafe Left`, `Strafe Right`, `Run Forward`, `Run Backward`, `Run Forward Left`, `Run Forward Right`, `Rifle Jump`, `Death`, `Rifle Idle Crouching`, `Walk Crouching Forward/Backward/Left/Right` |
| **Gunshot & Footstep SFX** | `freesound_community` via Pixabay | Pixabay Content License | [pixabay.com/sound-effects](https://pixabay.com/sound-effects/) | Rifle shot (`assault-rifle-single-burst`), SMG shot (`uzi-burst`), Sniper shot (`shot-and-reload`), and grass footsteps (`running-in-grass`), trimmed & edited |
| **Weapon Reload SFX** | `Dragon Studio` via Pixabay | Pixabay Content License | [pixabay.com/sound-effects](https://pixabay.com/sound-effects/) | `gun-reload-2` reload audio clip |
| **Procedural 3D Models & Code UI** | Built in C# by Aryan Patil | Custom Code | In Repository | C-130 transport aircraft, parachute canopy, enterable wooden shacks, procedural pine/oak trees, boulder clusters, sandbag barricades, 3D medkits, bandages, ammo crates, 9mm tactical pistol skin, and all UI canvases |
| **Core Unity Packages** | Unity Technologies | Unity Companion License | [unity.com](https://unity.com/) | Universal RP (URP), Cinemachine 3, AI Navigation, uGUI, TextMesh Pro |
| **AI Coding & Documentation Assistance** | Anthropic (Claude) & Google (Gemini / Antigravity) | AI Disclosure | — | Parts of the C# scripts, procedural generation logic, and documentation were written with the assistance of Claude and Gemini/Antigravity, then reviewed, debugged, tested, and integrated by us. |

---

## License & Disclaimer

- **Source Code:** All rights reserved by the author (**Aryan Patil**) for the **GDG on Campus NITK × Iris Web Team Recruitments 2026** evaluation *(no separate open-source license file attached)*. Third-party 3D models, animations, and audio files retain their respective licenses as listed in [Credits & Attributions](#credits--attributions).
- **Disclaimer:** *This is a student project inspired by BGMI. It is not affiliated with Krafton or any commercial game, and uses no commercial assets.*
