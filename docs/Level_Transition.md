# Level Transition

Architecture	Player lifetime	Main advantage	Main tradeoff
A. Full scene replacement	New instance per level	Simple, independent levels	Persistent data must be restored
B. Persistent GameRoot + Player	Same instance across levels	Natural continuity of actor state	Temporary state needs explicit cleanup
C. Persistent GameRoot + PlayerData	New instance per level	Clean separation of gameplay data and actor lifetime	More state-transfer code
D. Autoload PlayerManager + reparenting	Same instance across levels	Convenient global player access	More lifecycle and hierarchy coupling
E. Persistent world with streamed regions	Usually persistent	Seamless connected worlds	Greater memory and world-state complexity

## Full scene replacement

SceneTree
├── GameSession (autoload/data)
└── Level1
    ├── Player
    ├── Enemies
    └── HUD

      ChangeSceneToPacked()

SceneTree
├── GameSession (autoload/data)
└── Level2
    ├── New Player
    ├── Enemies
    └── HUD

## Persistent GameRoot and Player

GameRoot
├── World
│   ├── CurrentLevel  ← replaced
│   └── Player        ← retained
└── PlayerHud         ← retained

## Persistent GameRoot with independently persistent PlayerData

GameRoot
├── PlayerSessionData  ← persistent
├── World
│   └── CurrentLevel
│       └── Player    ← recreated
└── PlayerHud
