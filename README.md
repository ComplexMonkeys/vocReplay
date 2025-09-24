# vocReplay

A lightweight fork of the **VocReplayableAudio** mod for Voice of Cards games.
The core functionality is essentially identical to the original, but the replay button has been removed from the cards and the codebase has been streamlined. All of the original functionality is still accessible via the same keybinds as before.

## Things to know

### Original features

- The audio associated with a card plays every time you interact with it and not just on the first occasion
- Replay any given cards audio
- Copy a cards contents text straight to your clipboard (great for yomitan etc.)

### Differences from the original

| What changed           | Details                                                                                                                                                          |
| ---------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Visual UI              | The replay button is gone from the cards                                                                                                                         |
| Interaction            | All actions are performed through keybinds (e.g., `Ctrl + C` to copy text).                                                                                      |
| Codebase               | Streamlined code and some very minor additional error handling operations                                                                                        |
| Copied text formatting | I disliked the original format in which the text was copied to your clipboard so I changed it to be a bit more simple. An example is included at the very bottom |

### Controls

| Shortcut     | Action                                                                                        |
| ------------ | --------------------------------------------------------------------------------------------- |
| **Ctrl + C** | Copy the text of the card you’re hovering over (or the selected item in the collection menu). |
| **F3**       | Replay the current card’s audio.<br>If no card is selected, replays the last audio played.    |

## Installation

1.  **Download MelonLoader** – Get the appropriate installer for your platform from the [MelonLoader wiki](https://melonwiki.xyz).
2.  **Install MelonLoader** – Run the installer and choose your _Voice of Cards_ installation.
    - **Important:** Choose **version 0.5.7**; the mod does not work with other versions.
3.  **Download the mod** – Grab the latest `vocReplay.dll`.
4.  **Copy the DLL** – Place `vocReplay.dll` into the `Mods` folder inside your game’s installation directory.

-> To hide the MelonLoader console when launching via Steam, add `--melonloader.hideconsole` to the game’s launch options.

## Changes in copied text format

The original copy text content resulted in something approximately like this being copied:

Title: タイトル <br>
Content: コンテンツ

I didn't like this formatting so I altered it to be more minimalistic and to remove any halfwidth caracters, resulting in the copied text looking something like this which I thought looked better:

【タイトル】<br>
&nbsp; コンテンツ

<br>

#### Happy learning everybody.
