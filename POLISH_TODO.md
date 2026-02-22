# TTR Polish TODO - Remaining Items

## Status: 44 polish passes completed (commits up to dfa4dbe)
## Last session: Fixed ScorePopup compiler errors, added wind loop, feedback gap fixes

---

## HIGH PRIORITY (Major Game Feel Impact)

### 1. SewerTour Audio Transitions (40% feedback score - biggest gap)
- **File:** `SewerTour.cs` lines 69-112, 544-549
- No audio transition entering/exiting tour mode (music cuts abruptly)
- Add: fade-out race music, fade-in ambient/exploratory loop
- Add: audio chime when tour starts
- ExitTour() at line 544 has zero feedback - just hard restarts
- Add: exit sound, fade-to-black, "tour complete" stinger
- Add: category-based audio cues near showcase items (obstacle=warning, collectible=chime)
- Add: UI click sounds for tour buttons

### 2. Waterfall Screen Tint
- **File:** `SewerWaterEffects.cs` TriggerScreenSplash() line 579
- Currently only particles + shake + audio when passing through waterfalls
- Missing: brief murky blue-green screen tint overlay (0.3s fade)
- Use ScreenEffects to trigger a water-colored hit flash
- Also: no drain pipe collision feedback at all

### 3. Zone Transition Pre-Warning & Climax
- **File:** `PipeZoneSystem.cs` Update() line 164
- No pre-transition warning (lighting shift starts too late at 30m)
- Add: foreboding audio build 5s before zone boundary
- Hellsewer lacks climax feel - needs rhythmic vignette pulse
- Zone audio doesn't change with water sounds (same splash everywhere)

### 4. Obstacle Proximity Escalation Enhancement
- **File:** `ObstacleRadar.cs`, `ObstacleSpawner.cs`
- Radar blips are tiny (5-8px) and hard to see at high speed
- Missing: brief highlight flash when obstacle enters danger zone
- Missing: radar "ping" sound design
- No directional indicators (arrow pointing to wall obstacle)
- Obstacle types indistinguishable on radar (all same color)

---

## MEDIUM PRIORITY (Polish Refinements)

### 5. Death Sequence Enhancement
- **File:** `GameManager.cs` GameOver() lines ~399-536
- Death tumble exists (0.8s) but no slow-motion zoom-out
- No deceleration-based camera lag (dreamy heavy feel)
- Could add brief desaturation wave on death

### 6. Steering/Wall Feedback
- **File:** `TurdController.cs`
- No visual/audio cue when player is near pipe wall
- Missing: sparks + grinding sound when grazing wall
- No steering resistance feedback at high speed
- Wall-run (angle > 45) has no visual distinction from center travel

### 7. Jump Anticipation Polish
- **File:** `JumpRamp.cs`, `TurdController.cs`
- No pre-launch compression (squash before launch)
- Camera shake is moderate 0.12f (could be 0.18f for more impact)
- Landing impact doesn't scale with fall height

### 8. Camera Intro Sequence
- **File:** `PipeCamera.cs`
- Game launches with camera immediately positioned
- Missing: fade-in from black with brief zoom to establish spawn
- No death cam behavior (special slow-mo on final hit)

### 9. Finish Line Early Confetti
- **File:** `RaceFinish.cs`
- Confetti only plays after podium reveal sequence
- Should begin during banner flash for immediate celebration
- Camera pan around podium lacks audio sync

---

## LOWER PRIORITY (Nice-to-Have)

### 10. Water Depth Fogging Per Zone
- All zones have same underwater visibility
- Should reduce in Toxic/Hellsewer zones
- Missing: underwater tint shift (blue-green to brown-red)

### 11. Floating Debris Interaction
- Debris doesn't react to player passing through
- No buoyancy pop when rubber duck appears
- Poop buddy eyes don't react to danger/speed changes

### 12. Recovery Phase Visual
- "GET UP!" cheer exists but no shield bubble aura during i-frames
- Invincibility shimmer exists but lacks dramatic shield start flash

### 13. Mobile Text Scaling Consistency
- DPI-based scale exists (GameUI.cs Awake)
- Some procedural UI text may not use consistent scaling

### 14. Fork Entry Anticipation
- Fork approach has warning popup + audio + haptic (pass 42)
- Missing: branch preview showing obstacle density per path
- Poop crew could warn "LEFT IS CLEAR!" etc.

---

## ALREADY WELL-POLISHED (No Action Needed)
- Combo system (escalating feedback at all tiers)
- Speed boost pickup + active boost (trail color morph, speed streaks, wind-down pulses)
- Near-miss streaks (milestones with escalating cheers)
- Stomp bounce (combo escalation, squelch audio, score popups)
- Poop buddy pickup/chain (celebrations, cheers, particles)
- Race position changes (overtake celebrations, personality reactions)
- Zone transitions (audio sweep, screen flash, FOV punch, particles, cheer)
- Pause menu (fade animation, elastic scale, "PIPE BLOCKED!" theme)
- Shop/Gallery panels (fade transitions, CanvasGroup alpha)
- Game over panel (elastic bounce entrance, high score celebration)
- Start screen (fade-out with zoom, challenge quips)
- Flush countdown sequence (punch scale, whirl overlay, camera dive)
- Wind loop (speed-scaled tunnel airflow)
- Zone ambient audio (5 unique loops with crossfading)
- Drift sparks + grinding audio
- Obstacle proximity pings (radar + audio + haptics)
- Coin magnet spiral arc trajectory
