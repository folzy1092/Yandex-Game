# Audio source manifest

## Original DroneStrike assets

`Drone/motor_*.wav`, `Drone/wind_*.wav`, `signal_weak.wav` and
`battery_low.wav` are original project audio generated offline by
`Tools/GenerateDroneStrikeAudio.py`. (`Explosions` used to be generated
there too; since 2026-10-01 they are built from recordings — see below.
Do not regenerate them with that script.)
They are imported into Unity as normal clips. The game does not synthesize
those assets at runtime.

## Kenney Impact Sounds

| Final files | Original files |
| --- | --- |
| `Impacts/Ground/impact_ground_01..03.ogg` | `impactSoft_medium_000..002.ogg` |
| `Impacts/Hard/impact_hard_01..03.ogg` | `impactPlate_heavy_000..002.ogg` |
| `Impacts/Metal/impact_metal_01..03.ogg` | `impactMetal_medium_000..002.ogg` |

- Author: Kenney Vleugels / Kenney.nl
- Source: https://kenney.nl/assets/impact-sounds
- License: CC0 1.0 Universal
- Changes: renamed for stable DroneStrike IDs; audio samples unchanged

SHA-256, in each row's numeric filename order:

| Final group | SHA-256 |
| --- | --- |
| Ground | `7d3ba0bb5e60a11b5d3e558c141303dcf494256675fbf753c0d252d2cf0481e3`, `7642a4fd43e547afe4f7adfadb3dabb681c0ff512f52c1674bae30a726841faf`, `5069e3571a77d7f7aae9ef71d0364aa245fb7d64a7c8cc9956f221d03088c089` |
| Hard | `112d4f93ddcc370b410630f971c0f5d991856102da9c76bc5c5540d388e75aaa`, `142fd6c77f13d25f318265901163f70f8dfe12f87829169a570025be43de7011`, `b0cab75d1befa32b4cf215d8ee7e1c400a39182e8b105963b8e310f825b198a1` |
| Metal | `a96f879fec0864a8938e0c745b6996a6c5679c16a234ce31a01cc995e8401003`, `e8a9eaba7c4d27422e4eeb3e6c7100d5d7dc0f83e005efc98c960adcc5265337`, `7e89ce2ca0dbda95ea2b78d4b50791cab35c10d20e9f5ccd45dd0b00e99ff548` |

## Kenney Interface Sounds

| Final file | Original file |
| --- | --- |
| `UI/ui_focus.wav` | `click_004.wav` |
| `UI/ui_confirm.wav` | `confirmation_003.wav` |
| `UI/ui_back.wav` | `back_002.wav` |
| `UI/ui_unavailable.wav` | `error_003.wav` |
| `UI/signal_lost.wav` | `glitch_003.wav` |
| `UI/target_destroyed.wav` | `select_006.wav` |

- Author: Kenney Vleugels / Kenney.nl
- Source: https://kenney.nl/assets/interface-sounds
- License: CC0 1.0 Universal
- Changes: renamed for stable DroneStrike IDs; audio samples unchanged

SHA-256:

| Final file | SHA-256 |
| --- | --- |
| `ui_focus.wav` | `435a6701378802f98739dbcb39f70a505f6cce88b91886eef4f680192e93412c` |
| `ui_confirm.wav` | `d9b3719731a409c8109c5a049aceffbff9ff6c0ffe074a02fe52664393139b79` |
| `ui_back.wav` | `45f1c9df0db7af4c55166bad72568dcbd6e729ee362ce470b88d9deee51a5e5c` |
| `ui_unavailable.wav` | `350375a1a4adb8fe6f7b4cd775cdbb9a3015e2473b31a0e6fad9e3b3a1e8a9fd` |
| `signal_lost.wav` | `adf21d2bb6812539d183a91137d4164b69f0938c8214adc2fec7b56b01a157ae` |
| `target_destroyed.wav` | `f62c605bdea38edd8b1a88e9806d96ac19df43ccfedcc0f424ed156c455341e1` |

## Recorded FPV motor loops (qubodup, CC0)

| Final file | Source |
| --- | --- |
| `Drone/fpv_real_low.wav` | "FPV Drone Flight 1", https://freesound.org/people/qubodup/sounds/854464/ |
| `Drone/fpv_real_high.wav` | "FPV Drone Flight 3", https://freesound.org/people/qubodup/sounds/854466/ |

- Author: qubodup (extracted and normalised the drone audio from "Maneuver
  Battle Lab Quarterly Drone Race EPK(H)" by Brandon Dorrill, Fort Benning
  Public Affairs Office - a US government work)
- License: CC0 1.0 Universal (as listed on each Freesound page)
- Taken from Freesound's public HQ MP3 previews (48 kHz), decoded offline.
- Changes: 70 Hz one-pole high-pass; low loop = 0.8-12.6 s of Flight 1,
  high loop = 2.0-11.2 s of Flight 3; each made seamless with a 0.6 s
  equal-power crossfade of its tail into its head; RMS-normalised to 0.22,
  peak-limited to 0.89; written as 16-bit mono WAV.
- SHA-256 of the final files: `fpv_real_low.wav`
  `d85b07ba2f8d7522cc87c4e7cb3b3945fc43bac1a1936beb2ec2435dca7e9b17`,
  `fpv_real_high.wav`
  `75e7877300fcdeb9dcaa0a7ebe738128ef1b9e293089a4aa1bea054fed0804fa`.


## Explosions (recorded, CC0)

`Explosions/explosion_{compact,standard,heavy}_0{1,2,3}.wav` are built by
`Tools/BuildExplosionClips.py` from five CC0 Freesound recordings (HQ
previews): qubodup 182432 "Explosive 1 v1 [DOD 130303]", 182797 "Windy
Explosion" and 855898 "Fire Explosion" (all extracted from US-government
footage, public domain), qubodup 840510 "Loud Firewords Bang Cut Off", and
areniporgen 693421 "CTS 7290" (a real flashbang). URLs and the exact
processing are in the script header. Compact = sharp crack, standard = the
DOD detonation, heavy = the forced ammunition explosion, each in three
pitch/layer variants.

## Music (Kevin MacLeod, CC BY 4.0)

| Final file | Track |
| --- | --- |
| `Music/menu_theme.mp3` | "For the Fallen", https://incompetech.com/music/royalty-free/mp3-royaltyfree/For%20the%20Fallen.mp3 |
| `Music/mission_theme.mp3` | "At Launch", https://incompetech.com/music/royalty-free/mp3-royaltyfree/At%20Launch.mp3 |

- Author: Kevin MacLeod (incompetech.com)
- License: Creative Commons Attribution 4.0,
  https://creativecommons.org/licenses/by/4.0/ — attribution is shown on the
  main menu and in CREDITS.txt.
- Unmodified MP3s (renamed). Imported as mono, Vorbis q0.4, streamed
  (DroneBuildSetup.ConfigureMusicImport).
- SHA-256: menu_theme `1c73f6cc23b746942ddcc6b56ef24542d42dbb2ba1a6932848848bfb10d17849`,
  mission_theme `52215f8ad85bfbced05752052adadd58bffa5caef16696f6cea4d9d49a647a8e`.
