# STORM CHASERS – Game Design Document
*(10-Day Prototype Scope)*

## Executive Summary

- **Genre:** Co-op Boss Rush
- **Platform:** PC (Steam)
- **Players:** 2–4 players (online co-op)
- **Core Loop:** Fight boss → Select power-up → Fight harder boss → Repeat
- **Art Style:** Stylized cel-shaded with high contrast (easier to render, reads well on stream)
- **Development Timeline:** 10-day prototype → 6–8 month full development

> **Elevator Pitch:**  
> *Risk of Rain 2 meets Monster Hunter if they had a baby raised by Persona’s UI designers. 2–4 players fight giant elemental bosses in 15-minute runs, chaining power-ups to create absurd synergy builds.*

## Core Pillars

1. **"Oh Shit!" Moments** – Boss attacks should make players yell, laugh, or scream for help
2. **Build Expression** – Power-up combos should feel broken (that’s the fun)
3. **Coordination Rewarded** – Teams that communicate should perform 2x better than silent players
4. **Streamable Chaos** – Every run should create at least 3 clippable moments

---

## Prototype Scope (10-Day MVP)

### What MUST Be In the Prototype

#### Core Systems

- ✅ 2-player online co-op (host/client architecture)
- ✅ Character movement (WASD + jump + dodge roll)
- ✅ Basic melee attack (single combo chain)
- ✅ ONE complete boss fight with 3 attack patterns
- ✅ Boss telegraph system (visual warning before attacks)
- ✅ Power-up selection screen between "rounds" (3 choices, pick 1)
- ✅ 3-5 power-ups with clear visual/mechanical effects
- ✅ Health system for players and boss
- ✅ Simple arena (flat ground, some obstacles)

#### Visual/Audio

- ✅ Placeholder 3D models (capsules for players, primitive shapes for boss)
- ✅ Color-coded attack telegraphs (red = danger, yellow = warning)
- ✅ Hit/hurt feedback (screen shake, freeze frames, VFX)
- ✅ UI: Health bars, power-up selection menu, connection status
- ✅ Sound: Hit sounds, boss roars, attack whooshes (can be free SFX)

#### Game Flow

- ✅ Lobby screen (host/join with Steam Friend integration if possible, or manual IP)
- ✅ Fight boss until it dies
- ✅ Power-up selection (both players choose)
- ✅ Boss respawns with more health/damage
- ✅ Repeat 2–3 times, then "Complete" screen

---

## Detailed Design

### Player Character Design

#### Movement

- **Base Speed:** 7 units/second
- **Jump:** Single jump, 3-unit height
- **Dodge Roll:** 0.5-second invincibility, 8-unit distance, 2-second cooldown

> *Fast enough to feel responsive, slow enough that dodging requires prediction*

#### Combat

- **Attack:** 3-hit combo (light → light → heavy)
    - Hit 1: 10 damage, 0.3s animation
    - Hit 2: 10 damage, 0.4s animation
    - Hit 3: 25 damage, 0.6s animation (knockback)
- **Attack Range:** 2 units (need to be close)
- **Health:** 100 HP (visible as health bar)

> *Punishes button mashing, rewards completing full combo, creates risk/reward positioning*

---

### Boss Design: "Tempest Titan" (Lightning Elemental)

- **Visual Identity:** Large humanoid made of storm clouds with electric arcs.
    - Placeholder: Gray capsule 3× player size with sphere "hands" and particle effects.

#### Health Scaling

- Round 1: 500 HP
- Round 2: 800 HP
- Round 3: 1,200 HP

#### Behavior Pattern (State Machine)

1. **IDLE (2s):** Boss stands still, selecting next attack
2. **TELEGRAPH (1.5s):** Visual indicator shows which attack is coming
3. **ATTACK (0.5–2s):** Execute attack
4. **COOLDOWN (1s):** Boss vulnerable, staggers if hit during this
5. **Loop back to IDLE**

#### Boss Attacks

- **Attack Pattern 1: Ground Slam**
    - **Telegraph:** Both fists above head, red circles on ground (3m radius)
    - **Attack:** Slams ground, 40 damage AoE, small knockback
    - **Counterplay:** Dodge roll out of circle or stay outside 3m range
    - *Teaches telegraph reading and aggressive positioning.*

- **Attack Pattern 2: Lightning Bolt Sweep**
    - **Telegraph:** Charges one hand, yellow line traces arena
    - **Attack:** Sweeping 180° lightning beam, 30 damage + stun (0.5s)
    - **Counterplay:** Get behind boss or dodge roll through beam
    - *Punishes passivity, rewards positioning.*

- **Attack Pattern 3: Thunder Clap (Enrage at <30% HP)**
    - **Telegraph:** Claps hands, screen shakes, expanding red circle
    - **Attack:** AoE shockwave, 50 damage, massive knockback
    - **Counterplay:** Be far away or time perfect dodge roll
    - *Panic moment, team coordination.*

#### Boss AI Decision Tree

- 60%: Ground Slam
- 30%: Lightning Sweep
- 10%: Thunder Clap (only if <30% HP)
- Always faces nearest player during IDLE

---

### Power-Up System (Simplified for Prototype)

#### How It Works

1. After boss dies, both players see power-up selection screen
2. Each player sees 3 random options from the pool
3. Players select 1, game resumes with powered-up players
4. Power-ups stack (e.g. two × +20% damage = +40%)

#### Prototype Power-Up Pool

| Power-Up        | Effect                   | Visual Feedback                |
|-----------------|-------------------------|--------------------------------|
| Berserker Rage  | +20% damage dealt       | Character glows red, attacks have red trail  |
| Stone Skin      | +30 max HP              | Gray aura                      |
| Lightning Step  | -1s dodge cooldown      | Blue afterimages on dodge      |
| Giant Slayer    | +15 damage to boss      | Weapon glows purple            |
| Vampire Touch   | Heal 5 HP per hit       | Green sparkles on hit          |

> *Clear mechanical benefits, easy to code (stat multipliers), obvious visual feedback for team, simple synergies (Berserker + Vampire = aggressive sustain build)*

---

### Arena Design: "Storm Peak"

- **Layout:** 30m×30m flat circular arena
- **Obstacles:** 4 large pillars (3m diameter)
- **Skybox:** Storm cloud with lightning
- **Edge:** Swirling wind wall (visual boundary)

> *Large space for spreading; obstacles break sight lines; predictable boss rotation; simple geometry = fast/easy to build.*

---

### UI/UX Design

#### HUD (In-Game)

- Top Left: Player health bar + name
- Top Right: Teammate health bar + name
- Bottom Center: Boss health bar (large, chunky)
- Top Center: Current round ("Round 2")
- Connection status icon (green/yellow/red)

#### Power-Up Selection Screen

- Fullscreen darkened overlay
- 3 cards in a row: icon, name, description
- "Waiting for other player..." if teammate hasn't chosen
- Both must select before game continues

#### Lobby Screen

- "Host Game" button
- "Join Game" button + IP field
- Player name input
- Instructions: "Host shares IP with friend, friend joins"

---

### Audio Design (Prototype Minimal)

- **Player SFX:**
    - Attack swing (whoosh)
    - Hit connect (thud/crack)
    - Dodge roll (cloth rustle)
    - Take damage (grunt/gasp)
    - Death (optional, low priority)

- **Boss SFX:**
    - Idle breathing (ambient)
    - Telegraph windup (energy build)
    - Ground Slam (earthquake boom)
    - Lightning Sweep (crackle + zap)
    - Thunder Clap (huge explosion)
    - Boss hurt (roar/grunt)
    - Boss death (collapse)

- **Music:** Single looping combat track (royalty-free or Kevin MacLeod)

> *Priority: Use freesound.org or Unity Asset Store; don’t spend time on custom audio for prototype.*

---

### Art Style & Visual Identity

- **Color Palette:**
    - Players: Bright, saturated (red, blue)
    - Boss: Dark grays/blacks with electric blue/white accents
    - Telegraphs: Red (danger), Yellow (warning), Green (safe)
    - Arena: Muted purples/blues (stormy)

> *Why Cel-Shading: Looks good/simple, high contrast, hides lack of texture, faster to render, better FPS*

- **Prototype Art Assets:**
    - Players: Colored capsules, simple faces
    - Boss: Gray capsule, sphere hands, electricity particles
    - Arena: Plane + texture, cylinder pillars
    - VFX: Unity built-in particles (sparks, smoke, energy)

---

### Metrics & Success Criteria for Prototype

- **Must Achieve:**
    - 2 players connect and fight boss together
    - Boss has 3 distinct attack patterns
    - Power-ups visibly affect gameplay
    - 0 crashes in 5-minute playtest
    - At least 1 “oh shit!” moment per boss fight (playtesters react)

- **Nice to Have:**
    - Boss fight feels "fair but hard"
    - Players complete 3 rounds in 10–15 min
    - Power-up synergies create “broken” builds

- **Failure States:**
    - Players can’t connect
    - Boss AI is trivial or unbeatable
    - Power-ups don’t change gameplay
    - Game crashes or is game-breaking
