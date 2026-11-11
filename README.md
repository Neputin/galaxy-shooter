**Galaxy Shooter**

A Unity 2D space shooter with increasingly large enemy waves, collectible weapons and upgrades, enemy shields and special behaviors, and a final boss encounter.

**Open the project**

1. Install Unity **6000.6.6f1** through Unity Hub. The exact project version is recorded in `ProjectSettings/ProjectVersion.txt`.
2. Install Git and Git LFS, then clone this repository:

```powershell
git lfs install
git clone https://github.com/Neputin/galaxy-shooter.git
Set-Location galaxy-shooter
git lfs pull
```

3. In Unity Hub, choose **Add project from disk** and select the cloned folder.
4. Open the project and let Unity restore its packages and import its assets.
5. Open `Assets/Scenes/MainMenu.unity` to start through the menu, or `Assets/Scenes/game.unity` to play the game directly.

In the game scene, destroy the asteroid to start the waves.

**Controls**

| Input | Action |
|---|---|
| WASD or arrow keys | Move |
| Space or left mouse button | Fire |
| Left Shift | Use thrusters while moving |
| C | Pull nearby pickups toward the player, including harmful pickups |
| R | Restart after game over or victory |

**Gameplay**

- Five waves containing 3, 5, 7, 9, and 11 regular enemies, followed by a boss.
- Raiders, dodging Interceptors, seeking-projectile Hunters, and backward-firing Rear Gunners.
- Wave, zigzag, and diagonal movement; ramming and one-hit enemy shields.
- Fifteen-shot ammunition capacity with refill pickups.
- Health, shield, speed, triple-shot, radial, and homing pickups, plus a harmful slow pickup.
- Weighted pickup spawning: frequent ammunition and rare health/special weapons.
- Thruster charge/cooldown, shield strength, ammo, wave, and boss UI.

**Learning and version control**

- [Combat systems explained step by step](Docs/CombatSystemsGuide.md)
- [Saving changes to GitHub](Docs/GitHubWorkflow.md)
- [Combat re-check results](Docs/CombatRecheckResults.json)

`Assets`, including Unity `.meta` files, `Packages`, `ProjectSettings`, and the documentation belong in Git. Unity rebuilds `Library`, `Temp`, `Logs`, and other generated folders locally. Binary assets use Git LFS, so clone with Git LFS installed.

The project includes third-party tutorial assets and package resources; their existing notices and licenses remain with those files.
