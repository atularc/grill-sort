# Audio sources and licences

Everything here is **CC0 (public domain)**: free for commercial use, no attribution required. Crediting is optional but kind.

| Use in game | Source file | Author / pack | Licence | Link |
|---|---|---|---|---|
| Effects (pick, click, invalid, star, whoosh, pop, drops, coins) | `Kenney/*.ogg` | Kenney: Interface Sounds, Impact Sounds, RPG Audio | CC0 1.0 | https://kenney.nl/assets/interface-sounds , /impact-sounds , /rpg-audio |
| Win / serve / no-moves jingles (steel drums) | `Kenney/jingles_STEEL*.ogg` | Kenney: Music Jingles | CC0 1.0 | https://kenney.nl/assets/music-jingles |
| Background music (in use) | `Music/buy_something_bossa.mp3` | "Buy Something!" (Shop Theme), OpenGameArt | CC0 | https://opengameart.org/content/shop-theme |
| Background music (alternate) | `Music/apple_cider_alt.ogg` | "Apple Cider" by Zane Little Music, OpenGameArt | CC0 | https://opengameart.org/content/apple-cider |
| Music option | `Music/mustard_bossa.ogg` | "Which Brand of Mustard Shall I Buy?", OpenGameArt | CC0 | https://opengameart.org/content/which-brand-of-mustard-shall-i-buy |
| Music option | `Music/two_left_socks_bossa.ogg` | "Two Left Socks", OpenGameArt | CC0 | https://opengameart.org/content/two-left-socks |
| Music option | `Music/chill_lofi.ogg` | "Chill Lofi Inspired", OpenGameArt | CC0 | https://opengameart.org/content/chill-lofi-inspired |
| Music option | `Music/funked_up.mp3` | "Funked Up", OpenGameArt | CC0 | https://opengameart.org/content/funked-up |
| Sizzle one-shot + sizzle ambience | synthesized | `Tools/ArtPipeline/make_audio.py` (our own) | ours | - |

Checked on 2026-11-21 on each page (Kenney `License.txt` in every pack: "Creative Commons Zero, CC0 ... free to use in personal, educational and commercial projects").

## From the client's own games (NOT CC0)

| Use in game | File | Source | Notes |
|---|---|---|---|
| Win jingle (`SFX/win.ogg`) | `iKameRef/win_raw.wav` | Skewer Jam (iKame), level complete, from the client's screen recording | music-free moment |
| Order served (`SFX/serve.ogg`) | `iKameRef/serve_raw.wav` | Screwdom 3D (iKame), "Well Done" reward chime (first 0.75 s) | faint music removed |

These belong to iKame and are used in iKame's own game with the client's references. Replace them with iKame's original sound files before release (they are re-recordings from phone captures). Built by `Tools/ArtPipeline/extract_ref_sfx.py`. The earlier synthesized versions are `SFX/serve_synth.ogg` and `SFX/win_synth.ogg`.
