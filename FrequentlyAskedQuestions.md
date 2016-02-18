# Frequently Asked Questions #

None of these questions have actually been asked yet, so they are really Pre-emptively Answered Questions.

## Games Not Working ##

If many games are not working, it could be for one of the following reasons.

### Missing `.romdata` Files ###
Cogwheel uses `.romdata` files to identify dumps. Some dumps are known to be "bad", or have extra padding that needs to be trimmed before use. Some dumps also have the wrong file extension, which would make Cogwheel emulate the wrong machine type. If the `.romdata` files are not present, Cogwheel cannot identify these ROMs correctly, so will make a guess - which can be wrong.

You can download the latest `.romdata` files from [Maxim's website](http://www.smspower.org/maxim/smschecker/datafiles/) -- extract the files into a directory named `ROM Data` directly underneath the Cogwheel executables.

### Missing `COLECO.ROM` ###
Unlike Sega's consoles, the ColecoVision had a BIOS ROM that added helpful functionality to developers on that platform. This means that whilst SMS ROMs will run perfectly happily without a BIOS ROM, you must have a ColecoVision BIOS installed to run ColecoVision games.

You will need to source a copy of `COLECO.ROM` and copy it to the same directory as the emulator executables.