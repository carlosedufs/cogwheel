# Controls #

The _Cogwheel_ interface supports keyboard and joysticks.

You can use an original Sega Master System joypad if you so wish - see UsingSmsJoypads for more information.

## Default Keys ##

By default, only the player 1 joypad is configured.

| **Console function** | **Keyboard** |
|:---------------------|:-------------|
| 1                    | Ctrl         |
| 2                    | Alt          |
| Direction pad        | Cursor keys  |
| Start _(Game Gear)_  | Space        |
| Pause _(SMS)_        | Space        |
| Reset _(SMS)_        | Backspace    |

Note that _Reset_ is a soft-reset, and its operation depends on whether the running software checks its state or not. As the Sega Master System didn't have a dedicated _Start_ button, _1_ is used instead in most games (it is marked _1 Start_ on the joypad).

# Customising Controls #

You can customise the controls using the _Tools_->_Customise Controls_ dialog.

Select an input device from the tab strip along the top of the dialog, then left-click on the buttons on the form to change which input key or event causes the emulated button to be pressed. Right-click a button to reset its binding.

## Keyboard ##

To change a keyboard key binding, just click the button you wish to customise. It will appear pressed; at this point press the key on your keyboard you wish to bind to it. Clicking anywhere else will cancel.

If you wish to remove a key binding, right-click on the button.

## Joysticks ##

When you click on a button to customise its binding you will be presented with a drop-down menu of possible options. The button bindings are simple enough; you can map any joystick button to any emulated button.

For joystick axes, there are two options - axis decrease and axis increase. This event is triggered by the selected joystick increasing or decreasing past a threshold; for example,_X-Axis Increase_ would be triggered by moving the joystick right, and _Y-Axis Decrease_ would be triggered by moving the joystick upwards.