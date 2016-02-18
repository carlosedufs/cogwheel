# Cogwheel #

A software emulator primarily for 8-bit Sega hardware, such as the Sega Master System, Sega Game Gear and SG-1000. The ColecoVision is also supported.

The project uses a modular design based around a Z80 core emulator class library. The rest of the Sega-specific emulation is implemented in another class library, and the user interface is implemented as a separate application. This would allow the main emulator to be easily adapted to different interfaces.

The code is written in C# 3.0 (targetting .NET 2.0). The user interface uses Windows Forms, with [SlimDX](http://code.google.com/p/slimdx/) being used to render the graphical output to the form as quickly as possible.

## Features ##

The following features are currently supported:

  * Z80 CPU (emulates all documented and most undocumented features).
  * TMS9918A video with custom Sega VDP Master System and Game Gear extensions. Supports multiple versions (hardware revisions, PAL/NTSC) and emulates bugs depending on version.
  * SN76489 (PSG) sound.
  * YM2413 (FM) sound.
  * Standard ROM mapper, Codemasters mapper and a 64KB RAM mapper.
  * Sega and ColecoVision joypad emulation.
  * Hardware profiles for SG-1000, SC-3000, SMS1, SMS2, Game Gear and ColecoVision.
  * Japanese/Export hardware differences.