# MECoalescedConvert

## Introduction
A command line utility that can convert a Mass Effect "Coalesced" file into a regular text INI, or vice-versa. This allows you to make changes to the file without breaking its structure.

Supported File Formats:
- Mass Effect 2
- Mass Effect 3
- Mass Effect Legendary Edition (1, 2, and 3)

## Downloading
1. Navigate to the [releases page](https://github.com/cmrazek/MECoalescedConvert/releases).
2. Find the file matching your current operating system and architecture.
   - If you're not sure which one you have, it's likely `win-x64`.
3. Save the file as `mecc.exe` if you are on Windows, or `mecc` if you are on Linux or Mac.

## Example
> NOTE: This example will use Mass Effect 1 Legendary Edition, downloaded by Origin. Your paths may differ.

Open a console prompt (cmd / PowerShell) and navigate to the folder where you downloaded the application.

Type `.\mecc "C:\Program Files (x86)\Origin Games\Mass Effect Legendary Edition\Game\ME1\BioGame\CookedPCConsole\Coalesced_INT.bin"`

![Decoding Using PowerShell](https://raw.githubusercontent.com/cmrazek/MECoalescedConvert/master/assets/decode-ps.png)

This will create a new file `Coalesced_INT-export.ini` in the same folder. Edit this file using the text editor of your choice.

![Editing the File](https://raw.githubusercontent.com/cmrazek/MECoalescedConvert/master/assets/edit-ini.png)

Once you've completed your changes, run `mecc` again, this time passing in the file name of the `.ini` text file you just edited.

`.\mecc "C:\Program Files (x86)\Origin Games\Mass Effect Legendary Edition\Game\ME1\BioGame\CookedPCConsole\Coalesced_INT-export.ini"`

![Encoding Using PowerShell](https://raw.githubusercontent.com/cmrazek/MECoalescedConvert/master/assets/encode-ps.png)

It will save your changes to `Coalesced_INT.bin`. The first time this is done, the app will make a backup of the original file (just in case).

Play the game.

## ChangeLog

### Version 1.1.0
- Include the .NET runtime with the application so that there is no longer a dependency to have it installed.

### Version 1.0.1
- Changed file name to mecc.exe, because it's easier to type and remember than mecoalc.
