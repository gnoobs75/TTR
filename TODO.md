# TTR - TODO List (Feb 22, Session 3)

## Status of Current Session Work
- [x] Fix backwards graffiti text (ScenerySpawner -inward)
- [x] Double cheerleader poop size (CheerOverlay all dims 2x)
- [x] Reorganize UI layout (race info LEFT, stats RIGHT)
- [x] AI racer physics (harder stumble, water slowdown, invincibility)
- [x] Fix fork camera (playerFwd lookTarget, angular clamp)
- [x] Race music from ogg files (Resources/music/, random selection)
- [x] Dynamic music loading (Resources.LoadAll, user added 4 tracks)
- [x] Disable procedural BGM (ogg tracks replace it)
- [x] Splash screen music (plays random ogg on load)
- [x] Volume sliders on splash screen (Music + Sound, PlayerPrefs)
- [x] Comprehensive debug logging (#if UNITY_EDITOR across 15+ files)

## NEEDS TESTING (Play and Verify)
- [ ] Fork camera - does it still jerk/spin at pipe forks?
- [ ] Volume sliders visible and functional on splash screen?
- [ ] Splash music plays on load, fades when game starts?
- [ ] Race music plays during race, fades on finish?
- [ ] No procedural BGM plays (zone ambient should still work)
- [ ] Graffiti text readable (not mirrored)?
- [ ] Cheerleaders visibly larger at bottom?
- [ ] UI layout balanced (race info left, stats right)?
- [ ] AI racers stumble harder on obstacles?
- [ ] AI racers slow in water/drop zones?
- [ ] Console shows [HIT] [FORK] [ZONE] [RACE] etc logs in editor?

## KNOWN ISSUES TO INVESTIGATE
- [ ] Fork camera may still need tuning (max angular velocity 90deg/s)
  - Try adjusting if still feels jerky: PipeCamera.cs line ~229
  - Could increase to 120 or decrease to 60 depending on feel
- [ ] Camera breathing/fog may still be too heavy in later zones
  - Zone profiles in PipeCamera.cs lines 286-292
  - Fog density in PipeZoneSystem.cs
- [x] SceneBootstrapper raceDistance already correct (2000m)

## FUTURE POLISH IDEAS
- [x] Pause menu volume sliders (Music + Sound, synced with PlayerPrefs)
- [x] Music crossfade between splash and race (1s fade-out, 1.5s fade-in)
- [x] Power-ups: Shield (5s invincibility), Magnet (8s coins), Slow-Mo (3s time slow)
- [ ] Persist high score per track/mode
- [ ] AI racer names and personalities visible in pre-race screen
- [ ] Fork preview camera (brief peek down each branch before choosing)
- [ ] Replay system (record inputs, play back after race)
- [ ] Ghost racer (race against your personal best)
- [ ] More obstacle variety per zone (themed creatures)
- [ ] Boss encounters at zone boundaries
- [ ] Seasonal skins and themed pipes
- [ ] Leaderboard integration (GameCenter/Google Play)
- [ ] Tutorial improvements (interactive first-race walkthrough)
- [ ] Performance profiling on mobile (particle budget, draw calls)
- [ ] Sound design pass (replace some procedural SFX with recorded)

## DEBUG LOGGING TAGS (for console filtering)
All wrapped in `#if UNITY_EDITOR` - zero cost in builds.
- `[HIT]` - Player hit/stun/recovery/invincibility transitions
- `[FORK]` - Fork enter/exit, branch selection, blend values
- `[CAM]` - Camera fork blending, forward angle
- `[ZONE]` - Zone transitions (Porcelain→Grimy→Toxic etc)
- `[SPAWN]` - Obstacle placement (type, distance, angle)
- `[GAME]` - Game start/over, milestones, multiplier changes
- `[RACE]` - Race state changes, position changes, countdown
- `[AI]` - AI racer stumbles, decisions
- `[COMBO]` - Combo milestones (5/10/20/50), resets
- `[DROP]` - Vertical drop enter/exit
- `[JUMP]` - Jump launches
- `[STOMP]` - Stomp combos
- `[BOOST]` - Speed boost pickups
- `[RAMP]` - Jump ramp triggers
- `[BIGAIR]` - Big air ramp triggers
- `[GRATE]` - Grate collision hits
- `[OBSTACLE]` - Obstacle trigger enter (type, state)
- `[VDROP]` - Vertical drop trigger
- `[TTR Splash]` - Splash screen music
- `TTR Race:` - Race music playback

## FILES MODIFIED THIS SESSION
1. ScenerySpawner.cs - Graffiti text fix
2. CheerOverlay.cs - 2x poop sizes
3. SceneBootstrapper.cs - UI layout, volume sliders, music setup
4. RaceManager.cs - Race HUD left side, music from ProceduralAudio vol
5. GameUI.cs - Hide duplicate position, splash music, volume handlers
6. RaceLeaderboard.cs - Left column layout
7. RacerAI.cs - Harder stumble, water slow, logging
8. PipeCamera.cs - Fork fix (playerFwd + angular clamp)
9. ProceduralAudio.cs - Disabled procedural BGM
10. TurdController.cs - Hit/fork/drop/jump/stomp logging
11. GameManager.cs - Start/over/milestone/hit logging
12. PipeZoneSystem.cs - Zone transition logging
13. ObstacleSpawner.cs - Spawn logging
14. ComboSystem.cs - Combo milestone/reset logging
15. Obstacle.cs - Trigger enter logging
16. VerticalDrop.cs - Drop trigger logging
17. SpeedBoost.cs - Boost pickup logging
18. JumpRamp.cs - Ramp trigger logging
19. BigAirRamp.cs - Big air trigger logging
20. GrateBehavior.cs - Grate collision logging
