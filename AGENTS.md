# AGENTS.md — Unity First-Person Game Development Context

This document provides architecture guidelines, coding conventions, and system context for AI agents and Model Context Protocol (MCP) tools interacting with this Unity project.

---

## 1. Project Overview & Architecture

* **Engine:** Unity (C#)
* **Core Focus:** First-person mechanics, immersive interaction systems, modular character controllers, and procedural animations.
* **Architecture Style:** Component-driven development, modular scriptable object architectures, event-driven communication (via C# events / ScriptableObject event channels), and clean separation between input handling, kinematic/rigidbody movement, and view rendering.

---

## 2. Directory Structure

```text
Assets/
├── Animations/       # Animation controllers, clips, and avatar masks
├── Art/              # 3D models, textures, materials, and shaders
├── Audio/            # SFX, music tracks, and audio mixers
├── Prefabs/          # Reusable game objects (Player, Props, Environment)
├── Scenes/           # Prototype and gameplay levels
├── ScriptableObjects/# Data assets (Item data, configuration profiles)
└── Scripts/
    ├── Core/         # Game managers, state machines, initialization
    ├── Interaction/  # Raycast interactables, inventory, and use logic
    ├── Player/       # First-person controller, camera sway, head bob, input
    ├── Systems/      # IK foot placement, vaulting, procedural systems
    └── UI/           # HUD, dynamic crosshairs, dialogue node executors
    