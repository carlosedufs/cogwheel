# Introduction #

You may well have a Sega Master System controller lying around and would like to use it with the emulator. Fortunately it is quite easy to do so using cheap electronic components and free software.

# Interfacing #

To connect your joypad to your PC you will need some hardware (to connect the joypad to your PC) and a driver (so Windows can recognise the controller).

These instructions assume the presense of a parallel port. USB parallel printer adapters will probably not work, but parallel ports provided by PCI expansion boards should work.

## Hardware ##

For this you will need:

  * DB-25 connector (male or female with male-to-male extension cable) to connect to your PC.
  * DE-9 connector (male) to plug the SMS controller into.
  * 6 10K pull-up resistors.
  * A quantity of wire.
  * Tools (soldering iron, solder).

To construct the adapter, please see [these instructions](http://www.geocities.com/deonvdw/Docs/Diagrams/LinuxDB9c.htm) on the PPJoy website. For the SMS you can ignore the connections marked in blue - the SMS only has two fire buttons.

## Software ##

I recommend using the excellent [PPJoy](http://www.simtel.net/product.download.php?id=75176) drivers for this task. Install the driver package, then go into control panel and open the _Parallel Port Joysticks_ applet.

Click the _Add_ button, and enter the following settings:

| Parallel port  | Whichever port your adapter is connected to. |
|:---------------|:---------------------------------------------|
| Controller type | Joystick                                     |
| Interface type | Linux DB9.c                                  |

Click _Add_ to confirm the settings. If you now open the standard _Game Controllers_ control panel you should see a new entry - _Parallel Port Joystick 1_. If you view its properties you should be able to press the buttons on your SMS pad and move the D-pad and see its status update.

## Configuring Cogwheel ##

Run the emulator, and select the _Customise Controls_ option in the _Tools_ menu. Select the _Parallel Port Joystick 1_ tab, then enter the following settings by clicking on the configuration buttons:

| 1 | Button 1 |
|:--|:---------|
| 2 | Button 2 |
| Up | Y-Axis Decrease |
| Down | Y-Axis Increase |
| Left | X-Axis Decrease |
| Right | X-Axis Increase |

Apply the settings by closing the dialog.