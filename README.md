# MECoalescedConvert

A command line utility that can convert a Mass Effect 'Coalesced' file into a regular text INI, or vice-versa. This allows you to make changes to the file without breaking its structure.

Supported File Formats:
- Mass Effect 2
- Mass Effect 3
- Mass Effect Legendary Edition (1, 2, and 3)

## Example
This example will use MassEffect 1 Legendary Edition, downloaded by Origin. Your paths may differ.

Open a console prompt (cmd / PowerShell) and navigate to the folder where you extracted the app.

Enter:

`.\mecoalc "C:\Program Files (x86)\Origin Games\Mass Effect Legendary Edition\Game\ME1\BioGame\CookedPCConsole\Coalesced_INT.bin"`

![Decoding Using PowerShell](https://raw.githubusercontent.com/cmrazek/MECoalescedConvert/master/assets/decode-ps.png)

This will create a new file __Coalesced_INT-export.ini__ in the same folder. Using the text editor of your choice edit this file.

![Editing the File](https://raw.githubusercontent.com/cmrazek/MECoalescedConvert/master/assets/edit-ini.png)

Once you've completed your changes, run __mecoalc.exe__ again, this time passing in the file name of the text file you just edited.

`.\mecoalc "C:\Program Files (x86)\Origin Games\Mass Effect Legendary Edition\Game\ME1\BioGame\CookedPCConsole\Coalesced_INT-export.ini"`

![Encoding Using PowerShell](https://raw.githubusercontent.com/cmrazek/MECoalescedConvert/master/assets/encode-ps.png)

The first time this is done, the app will make a backup of the original file, and will then save your changes to __Coalesced_INT.bin__.

Play the game.
