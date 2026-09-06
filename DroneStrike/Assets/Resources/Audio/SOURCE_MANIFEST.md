# Audio source manifest

## Original DroneStrike assets

`Drone`, `Explosions`, `signal_weak.wav` and `battery_low.wav` are original
project audio generated offline by `Tools/GenerateDroneStrikeAudio.py`.
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
