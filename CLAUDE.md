# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 정식 명칭
STIGMA (폴더명 prototype은 그대로 유지)

## 개발 환경
- Claude Code + Unity MCP 연동 완료
- Unity MCP 서버: http://127.0.0.1:8080
- 매 세션 시작 시 Unity에서 Window → MCP for Unity → Start Server 먼저 실행할 것

## Project

**STIGMA** — a Lovecraftian roguelike deck-building card game (Slay the Spire-style) built in Unity 6 with URP 17.3.0. All in-game strings and comments are in Korean.

## Build & Test

- **Build**: Unity Editor → File → Build Settings → Build (no custom scripts)
- **Tests**: Unity Test Runner (Window → General → Test Runner). The `com.unity.test-framework` package is installed but no test files exist yet.
- **IDE**: Visual Studio with `ManagedGame` workload (see `.vsconfig`)

## Architecture

### Persistent Singleton Managers (DontDestroyOnLoad)

All core managers are MonoBehaviour singletons that persist across scenes:

| Manager | File | Role |
|---|---|---|
| `GameManager` | `Assets/Scripts/GameManager.cs` | Global state: player HP, gold, deck, save/load (`save.json`), scene transitions |
| `BattleManager` | `Assets/Scripts/BattleManager.cs` | Battle loop, card resolution, monster AI, gaze mechanic |
| `MapSceneManager` | `Assets/Scripts/MapSceneManager.cs` | Dungeon map UI, node selection, state restoration after battles |
| `MapGenerator` | `Assets/Scripts/MapGenerator.cs` | Procedural map generation (15–20 layers); result is cached and restored |
| `FadeManager` | `Assets/Scripts/FadeManager.cs` | Cross-scene fade transitions |
| `EffectManager` | `Assets/Scripts/EffectManager.cs` | Visual effects for card abilities |

### Scene Flow

```
IntroScene → MainMenu → NodeMap ←→ BattleScene → RewardScene
                                ↕
                            ShopScene / Cutscene
```

`NodeMap` is the hub. After every battle, `GameManager` restores map state before returning.

### Data Layer (ScriptableObjects)

- `CardData` — immutable card definition: 17 effect types, rarity (Common→Mythic), mana cost, gaze impact
- `MonsterData` — HP, type, action pool for AI turn selection
- `NodeData` — map node state (layer, index, visited, accessible)

### Battle Resolution Order

1. Player plays cards (drag-to-monster or field)
2. Card effects resolve (damage, buffs, draws, gaze changes)
3. End-of-turn phase: regeneration → forbidden-card curse check → gaze overflow (≥100) → status decrements → monster turn → defense reset → draw to 5

### Gaze Mechanic

Core tension resource (0–100). Increases when Forbidden cards are played.
- ≥75: monster gains +2 damage
- =100: curse triggers (20 damage, monster +3 strength, resets to 30)

### Card Drag & Targeting

`CardUI` uses `IPointerEnterHandler` / `IBeginDragHandler` / `IEndDragHandler`. Target detection uses `Physics2D.OverlapPoint()` — monsters must be tagged `"Monster"`. `DragArrow` renders a Bézier curve arrow during drag.

### Save System

`GameManager` serializes to `Application.persistentDataPath/save.json` via `JsonUtility`. Saved fields: `playerMaxHp`, `playerCurrentHp`, `playerGold`, `bossesDefeated`, `deckCardNames[]`, and full map node state (layer/index/visited/accessible per node).

## Key Conventions

- Scripts use **legacy `Input` class** despite the New Input System package being present — don't migrate card/UI input to the new system without updating all affected scripts.
- All UI text is **TextMesh Pro**; do not use legacy `Text` components.
- `Physics2D` (not 3D raycasts) is used for overlap detection in battle.
- Coroutines drive animation sequencing throughout — `StartCoroutine` / `IEnumerator` is the standard pattern for timed sequences.
