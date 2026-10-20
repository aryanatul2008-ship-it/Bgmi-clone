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

```mermaid
flowchart TD
    subgraph LobbyAndDrop["1. Match Flow & Air Drop Sequence"]
        MML["MainMenuLobby.cs<br/>(3D Sky Stage & Sorting Order 20 UI)"]
        MSS["MatchStartSequence.cs<br/>(Matchmaking -> C-130 Plane -> Freefall -> Parachute -> Land)"]
        MML -->|"START clicked: unlocks match,<br/>restores HUD, triggers sequence"| MSS
    end

    subgraph PlayerRoot["2. Player Root Object ('Player')"]
        PM["PlayerMovement.cs<br/>(Walk, Sprint, Crouch, Jump, Fall Damage)"]
        CC["CharacterController<br/>(Capsule Physics & Grounding)"]
        PA["PlayerAnimator.cs<br/>(2D Velocity Blend Tree Driver)"]
        ANIM["Animator<br/>(PlayerAnimator Controller + Medea Rig)"]
        SPS["StarterPistolSetup.cs<br/>(9mm Starter Pistol + Shared SMG Ammo)"]
        WS["WeaponSwitcher.cs<br/>(Slots 1-4, Q/E, Scroll, Pickup/Remove)"]
        GUN["Gun.cs<br/>(Raycast Fire, Reload, Spread, Recoil)"]
        GE["GunEffects.cs<br/>(Muzzle Flash, Bullet Tracers, Hit Puffs)"]
        GS["GunScope.cs<br/>(RedDot / Sniper FOV & Spread Multipliers)"]
        WM["WeaponModel.cs<br/>(RightHand Bone Binding & Camera Alignment)"]
        SC["ScopeController.cs<br/>(Right-Click Zoom, Sensitivity Scaling, Scope Overlays)"]
        PINV["PlayerInventory.cs<br/>(Tab Backpack UI, Medkits/Bandages, G Drop)"]
        FS["FootstepSounds.cs<br/>(Grass Footstep Clips)"]
        PH["Health.cs<br/>(Player HP & Static PlayerKills Counter)"]
        PD["PlayerDeath.cs<br/>(Death State & Component Disable)"]
    end

    subgraph CameraRig["3. Camera System"]
        VCAM["CinemachineCamera<br/>(Orbital Follow + Rotation Composer)"]
    end

    subgraph WorldAndAI["4. World Generation, Loot & AI Bots"]
        LS["LootSpawner.cs<br/>(~100 Map Pickups + 3D Procedural Models)"]
        IP["ItemPickup.cs<br/>(Heal, Bandage, Ammo Triggers)"]
        WP["WeaponPickup.cs<br/>(Rifle, SMG, Sniper Triggers)"]
        MPS["MapPropSpawner.cs<br/>(Enterable Shacks, Trees, Rocks, NavMeshObstacle)"]
        ES["EnemySpawner.cs<br/>(Spawns Bot Prefabs on NavMesh)"]
        EAI["EnemyAI.cs<br/>(Patrol, Sight/Hearing LOS, Chase, Raycast Shoot)"]
        EAN["EnemyAnimator.cs<br/>(Bot Blend Tree & Death Trigger)"]
        EH["Health.cs<br/>(Bot HP)"]
    end

    subgraph UISystem["5. Code-Built HUD & Match Loop"]
        GHUD["GameHUD.cs<br/>(Health Bar, Weapon Slots, Kill Counter)"]
        MMAP["MiniMap.cs<br/>(Top-Down Orthographic Camera + RenderTexture)"]
        ROD["RestartOnDeath.cs<br/>(YOU DIED Prompt -> Enter Reloads Scene)"]
    end

    %% Connections
    MSS -->|"On F Jump: enables"| ES
    MSS -->|"On Ground Landing:<br/>calls ResetFall() & enables"| PM
    MSS -->|"On Ground Landing: enables"| CC & WS & PINV & SC & GUN

    PM --> CC
    PA --> ANIM
    SPS --> WS & GUN
    WS --> GUN
    GUN --> GE & GS & WM
    GUN -->|"Applies pitch/yaw recoil"| VCAM
    SC -->|"Reads active GunScope"| GS
    SC -->|"Zooms FOV & scales screen composer"| VCAM

    LS --> IP & WP
    MPS -->|"Spawns interior shack loot"| IP & WP
    IP -->|"Adds Medkit/Bandage/Ammo"| PINV & GUN
    WP -->|"Equips weapon or adds ammo"| WS

    ES --> EAI
    EAI --> EAN & EH
    GUN -->|"Raycast hit -> TakeDamage(dmg, true)"| EH
    EH -->|"Increments Health.PlayerKills"| GHUD
    EAI -->|"Distance-spread raycast"| PH
    PH --> PD & GHUD & ROD

