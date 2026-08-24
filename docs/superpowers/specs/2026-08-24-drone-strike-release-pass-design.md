# Drone Strike — release pass

**Date:** 2026-08-24
**Goal:** close every outstanding gameplay defect, replace the industrial-estate map with a
front-line position, and add the menu and rewarded-ad loadout the monetisation depends on, so
the game can be submitted to Yandex Games.

## Why this pass exists

The build before this one was playable but failed in five ways the player could see, all of
them reported from live play:

1. Flying into the back of a tank did not detonate the warhead.
2. The supply tent had no working collision and could not be destroyed.
3. Destroyed targets went black but did not burn — no fire was visible at all.
4. The signal-loss mechanic never triggered on this map's scale.
5. Patrol trucks drove through the warehouses.

Plus two structural gaps: the map was an industrial estate rather than a military position, and
there was no menu, no drone choice, and therefore no place to put a rewarded ad.

## Decisions

### Detonation is a property of hitting a target, not of speed

`Warhead.OnCollisionEnter` gated every impact on `collision.relativeVelocity.magnitude >=
armingSpeed`. Relative velocity is measured along the contact normal, so a hit into the flank or
rear of a vehicle reports a low figure even at forty metres a second — which is exactly the
reported bug. The gate now applies only to scenery. Anything with a `Target` in its parent chain
detonates on contact, however gentle.

The gate stays for scenery so that clipping a branch on the way in does not end the run.

### Colliders come from the model, and never below a floor thickness

`FitColliderToModel` already measured a placed model's renderer bounds. Two things were added:

- `NormalizeModelSize` rescales an imported model so its longest horizontal dimension is a
  known number of metres (7.2 m for the tank, 5.4 m for the tent). A `.glb` from a public model
  site can arrive in metres, centimetres or inches and there is no way to tell which without
  opening it; scaling to a known footprint makes every coordinate in the scene builder a real
  distance. The correction is skipped when it lands between 0.75× and 1.33×, so a model that
  already arrives about right keeps its own proportions.
- A 1.2 m minimum on every collider axis. A tent panel measures centimetres through, and a drone
  covers that inside one physics step; padding the box is what makes the tent destructible.

### Wreckage burns

`Target.SpawnFire` adds a looping flame system, a smoke column and a flickering point light,
sized from the target's own collider so a tank throws a bigger column than a crate stack. A
one-frame spark burst and a black repaint does not read as "destroyed" from two hundred metres
up, which is the range the player judges their progress from.

### Signal range is tuned to the map, not to a round number

`cleanRange` 260 → 220 m, `maximumRange` 400 → 330 m. The launch point sits at (-100, -120); the
furthest target is ~225 m out, so the whole position is flyable with a degrading picture at the
edges, and the far corners of the 700 m map drop the link. Previously nothing on the map could
reach 400 m from launch, so the mechanic was unreachable.

### The map is a position, not an estate

The four warehouses are gone, along with the fence and the concrete apron. A staging area behind
a front line is tents, earth berms, camouflage netting and a graded road.

- **Road.** A 190 × 150 m rectangle of asphalt with solid edge lines and a broken centreline —
  the one man-made line on the map, and the landmark the buildings used to be. The two patrol
  trucks drive it.
- **Field works.** Six earth berms, four sandbag revetments, two camouflage nets on poles. All
  cover, none of it a target.
- **Targets.** Eleven: three armour, four trucks (two of them patrolling), three tents, two
  masts, spread right across the position.
- **Clutter.** Fuel drums, crate stacks and concrete blocks, scattered by rejection sampling.

### Overlaps are caught arithmetically

There is no editor to eyeball the map in, so every placement registers the ground it occupies as
circles on the XZ plane, and everything placed afterwards checks against that list. Long thin
objects (berms, sandbag walls) claim a chain of small circles down their length rather than one
circle around the whole thing, or a berm could never stand beside the vehicle it shields.
Anything hand-placed that overlaps logs a warning at build time. `ClearOfRoad` keeps every prop
and every tree off the patrol route — that is the actual fix for trucks driving through
buildings, generalised so it cannot recur.

### Monetisation: two rewarded placements and one interstitial

The whole business model, and both halves have to be honest or the placement gets flagged.

- **Airframe unlocks** (`MainMenuUI`). Three drones: Разведчик (starter), Шершень (+35% thrust,
  +30% speed, −15% endurance) and Молот (+55% blast, −15% thrust). Each locked one is unlocked
  permanently by a completed rewarded view. The starter clears every target on the map, so the
  ad is an offer rather than a toll. State is in `PlayerPrefs`, which on WebGL is browser
  storage, so an unlock survives a reload.
- **+1 drone after a loss** (`MissionManager.RequestExtraDrone`). Offered on the results screen,
  capped at three per mission. The mission is revived rather than restarted, so every target
  already destroyed stays destroyed — which is what makes it worth taking. The cap stops the ad
  slot being farmed.
- **Interstitial on restart.** The one break in play where an ad interrupts nothing.

In the editor both rewarded paths grant the reward regardless, because there is no ad network
there and the feature could otherwise never be tested before a build. That branch is inside
`#if UNITY_EDITOR`; a shipped build grants nothing without a completed view.

### The warhead is visible

`DroneFactory.BuildWarheadView` parents a PG-7-style rocket — bulbous ogive nose, band, tube,
four fanned fins — to the camera rather than to the airframe, so it stays framed the same way
however the drone is tilted, the way it sits in real FPV footage. The compact charge is visibly
smaller, so the loadout is legible before a HUD number is read.

## Out of scope

Campaign and mission select, a second map, drone customisation beyond the three airframes, an
economy or currency, and menu localisation (language *detection* per Yandex requirement 2.14 is
already wired through `YandexAds.OnLanguageDetected` → `Localization`; the menu copy itself is
Russian only).

## Verification

Not verifiable from here — there is no Unity install in this environment. The build has to be
re-run in the editor (`Tools > Drone Strike > BUILD EVERYTHING`) and play-tested. What to check
is listed in `DroneStrike/README.md`.
