﻿using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace BeeDevelopment.Cogwheel {
	class KeyButton : CheckBox {

		#region Converting Keys into human-readable strings.

		/// <summary>
		/// Retrieves a string that represents the name of a key.
		/// </summary>
		/// <param name="lParam">Specifies the second parameter of the keyboard message (such as <c>WM_KEYDOWN</c>) to be processed.</param>
		/// <param name="lpString">Pointer to a buffer that will receive the key name.</param>
		/// <param name="size">Specifies the maximum length, in TCHAR, of the key name, including the terminating null character. (This parameter should be equal to the size of the buffer pointed to by the lpString parameter).</param>
		/// <returns>The length of the returned string.</returns>
		[DllImport("user32.dll")]
		static extern int GetKeyNameText(int lParam, StringBuilder lpString, int size);

		/// <summary>
		/// Translates (maps) a virtual-key code into a scan code or character value, or translates a scan code into a virtual-key code.
		/// </summary>
		/// <param name="uCode">Specifies the virtual-key code or scan code for a key. How this value is interpreted depends on the value of the <paramref name="uMapType"/> parameter.</param>
		/// <param name="uMapType">Specifies the translation to perform. The value of this parameter depends on the value of the <paramref name="uCode"/> parameter.</param>
		/// <returns>Either a scan code, a virtual-key code, or a character value, depending on the value of <paramref="uCode"/> and <paramref="uMapType"/>. If there is no translation, the return value is zero.</returns>
		[DllImport("user32.dll")]
		static extern int MapVirtualKey(int uCode, MapVirtualKeyMode uMapType);

		enum MapVirtualKeyMode {
			/// <summary>uCode is a virtual-key code and is translated into a scan code. If it is a virtual-key code that does not distinguish between left- and right-hand keys, the left-hand scan code is returned. If there is no translation, the function returns 0.</summary>
			MAPVK_VK_TO_VSC = 0,
			/// <summary>uCode is a scan code and is translated into a virtual-key code that does not distinguish between left- and right-hand keys. If there is no translation, the function returns 0.</summary>
			MAPVK_VSC_TO_VK = 1,
			/// <summary>uCode is a virtual-key code and is translated into an unshifted character value in the low-order word of the return value. Dead keys (diacritics) are indicated by setting the top bit of the return value. If there is no translation, the function returns 0.</summary>
			MAPVK_VK_TO_CHAR = 2,
			/// <summary>uCode is a scan code and is translated into a virtual-key code that distinguishes between left- and right-hand keys. If there is no translation, the function returns 0.</summary>
			MAPVK_VSC_TO_VK_EX = 3,
			MAPVK_VK_TO_VSC_EX = 4,
		}

		/// <summary>
		/// Converts a <see cref="Keys"/> value into a human-readable string describing the key.
		/// </summary>
		/// <param name="key">The <see cref="Keys"/> to convert.</param>
		/// <returns>A human-readable string describing the key.</returns>
		private static string GetKeyName(Keys key) {

			// Convert the virtual key code into a scancode (as required by GetKeyNameText).
			int Scancode = MapVirtualKey((int)key, MapVirtualKeyMode.MAPVK_VK_TO_VSC);

			// If that returned 0 (failure) just use the value returned by Keys.ToString().
			if (Scancode == 0) return key.ToString();

			// Certain keys end up being mapped to the number pad by the above function,
			// as their virtual key can be generated by the number pad too.
			// If it's one of the known number-pad duplicates, set the extended bit:
			switch (key) {
				case Keys.Insert:
				case Keys.Delete:
				case Keys.Home:
				case Keys.End:
				case Keys.PageUp:
				case Keys.PageDown:
				case Keys.Left:
				case Keys.Right:
				case Keys.Up:
				case Keys.Down:
				case Keys.NumLock:
					Scancode |= 0x100;
					break;
			}

			// Perform the conversion:
			StringBuilder KeyName = new StringBuilder("".PadRight(32));
			if (GetKeyNameText((Scancode << 16), KeyName, KeyName.Length) != 0) {
				return KeyName.ToString();
			} else {
				return key.ToString();
			}
		}

		#endregion

		#region Events

		public event EventHandler SettingChanged;
		/// <summary>
		/// Triggered when the value of the control is changed.
		/// </summary>
		/// <param name="e"></param>
		protected virtual void OnSettingChanged(EventArgs e) {
			if (this.SettingChanged != null) this.SettingChanged(this, e);
		}

		#endregion

		#region Public Properties

		/// <summary>
		/// Gets or sets the index of the controller that this button corresponds to.
		/// </summary>
		public int ControllerIndex { get; set; }

		/// <summary>
		/// Gets or sets the <see cref="InputButton"/> that this button corresponds to.
		/// </summary>
		public InputButton InputButton { get; set; }

		private Keys key;
		/// <summary>
		/// Gets or sets the current <see cref="Keys"/> that this button is mapped to.
		/// </summary>
		public Keys KeyboardTrigger {
			get { return this.key; }
			set { this.key = value; this.UpdateText(); }
		}

		private JoystickInputSource.InputTrigger joystickTrigger;
		/// <summary>
		/// Gets or sets the current <see cref="JoystickInputSource.InputTrigger"/> trigger that this button is mapped to.
		/// </summary>
		public JoystickInputSource.InputTrigger JoystickTrigger {
			get { return this.joystickTrigger; }
			set { this.joystickTrigger = value; this.UpdateText(); }
		}

		private XInputSource.InputTrigger xInputTrigger;
		/// <summary>
		/// Gets or sets the current <see cref="XInputSource.InputTrigger"/> trigger that this button is mapped to.
		/// </summary>
		public XInputSource.InputTrigger XInputTrigger {
			get { return this.xInputTrigger; }
			set { this.xInputTrigger = value; this.UpdateText(); }
		}

		public enum Modes {
			Keyboard,
			Joystick,
			XInput,
		}

		private Modes mode;
		public Modes Mode {
			get { return this.mode; }
			set { this.mode = value; this.UpdateText(); }
		}

		#endregion

		#region Event Handling

		protected override void OnLostFocus(System.EventArgs e) {
			this.Checked = false;
			base.OnLostFocus(e);
		}

		protected override void OnLeave(EventArgs e) {
			this.Checked = false;
			base.OnLeave(e);
		}

		protected override void OnMouseDown(MouseEventArgs mevent) {
			switch (mevent.Button) {
				case MouseButtons.Left:
					this.Checked ^= true;
					break;
				case MouseButtons.Right:
					this.Checked = false;
					this.KeyboardTrigger = Keys.None;
					this.JoystickTrigger = JoystickInputSource.InputTrigger.None;
					this.XInputTrigger = XInputSource.InputTrigger.None;
					this.OnSettingChanged(new EventArgs());
					break;
			}
		}

		protected override void OnKeyDown(KeyEventArgs e) {
			if (this.Checked && this.mode == Modes.Keyboard) {
				e.Handled = true;
				this.KeyboardTrigger = e.KeyCode;
				this.OnSettingChanged(new EventArgs());
			}
			this.Checked = false;
			base.OnKeyDown(e);
		}

		#endregion

		private void UpdateText() {
			this.Image = null;
			switch (this.Mode) { 
				case Modes.Keyboard:
					base.Text = GetKeyName(this.KeyboardTrigger);
					break;
				case Modes.Joystick:
					base.Text = "";
					switch (this.JoystickTrigger) {
						case JoystickInputSource.InputTrigger.XAxisDecrease:
							this.Image = Properties.Resources.Icon_ArrowLeft;
							break;
						case JoystickInputSource.InputTrigger.XAxisIncrease:
							this.Image = Properties.Resources.Icon_ArrowRight;
							break;
						case JoystickInputSource.InputTrigger.YAxisDecrease:
							this.Image = Properties.Resources.Icon_ArrowUp;
							break;
						case JoystickInputSource.InputTrigger.YAxisIncrease:
							this.Image = Properties.Resources.Icon_ArrowDown;
							break;
						case JoystickInputSource.InputTrigger.PovN:
							this.Image = Properties.Resources.Icon_PovN;
							break;
						case JoystickInputSource.InputTrigger.PovNE:
							this.Image = Properties.Resources.Icon_PovNE;
							break;
						case JoystickInputSource.InputTrigger.PovE:
							this.Image = Properties.Resources.Icon_PovE;
							break;
						case JoystickInputSource.InputTrigger.PovSE:
							this.Image = Properties.Resources.Icon_PovSE;
							break;
						case JoystickInputSource.InputTrigger.PovS:
							this.Image = Properties.Resources.Icon_PovS;
							break;
						case JoystickInputSource.InputTrigger.PovSW:
							this.Image = Properties.Resources.Icon_PovSW;
							break;
						case JoystickInputSource.InputTrigger.PovW:
							this.Image = Properties.Resources.Icon_PovW;
							break;
						case JoystickInputSource.InputTrigger.PovNW:
							this.Image = Properties.Resources.Icon_PovNW;
							break;
						case JoystickInputSource.InputTrigger.ZAxisDecrease:
						case JoystickInputSource.InputTrigger.ZAxisIncrease:
						case JoystickInputSource.InputTrigger.UAxisDecrease:
						case JoystickInputSource.InputTrigger.UAxisIncrease:
						case JoystickInputSource.InputTrigger.VAxisDecrease:
						case JoystickInputSource.InputTrigger.VAxisIncrease:
							base.Text = this.JoystickTrigger.ToString()[0] + "-Axis " + this.JoystickTrigger.ToString().Substring(5);
							break;
						case JoystickInputSource.InputTrigger.RudderDecrease:
						case JoystickInputSource.InputTrigger.RudderIncrease:
							base.Text = this.JoystickTrigger.ToString().Replace("Rudder", "Rudder ");
							break;
						default:
							base.Text = this.JoystickTrigger.ToString().Replace("Button", "");
							break;
					}
					
					break;
				case Modes.XInput:
					base.Text = "";
					switch (this.XInputTrigger) {
						case XInputSource.InputTrigger.DPadLeft:
						case XInputSource.InputTrigger.LeftThumbLeft:
						case XInputSource.InputTrigger.RightThumbLeft:
							this.Image = Properties.Resources.Icon_ArrowLeft;
							base.Text = (this.XInputTrigger.ToString().StartsWith("DPad")) ? "D-Pad" : this.XInputTrigger.ToString().Split('T')[0] + " Stick";
							break;
						case XInputSource.InputTrigger.DPadRight:
						case XInputSource.InputTrigger.LeftThumbRight:
						case XInputSource.InputTrigger.RightThumbRight:
							this.Image = Properties.Resources.Icon_ArrowRight;
							base.Text = (this.XInputTrigger.ToString().StartsWith("DPad")) ? "D-Pad" : this.XInputTrigger.ToString().Split('T')[0] + " Stick";
							break;
						case XInputSource.InputTrigger.DPadUp:
						case XInputSource.InputTrigger.LeftThumbUp:
						case XInputSource.InputTrigger.RightThumbUp:
							this.Image = Properties.Resources.Icon_ArrowUp;
							base.Text = (this.XInputTrigger.ToString().StartsWith("DPad")) ? "D-Pad" : this.XInputTrigger.ToString().Split('T')[0] + " Stick";
							break;
						case XInputSource.InputTrigger.DPadDown:
						case XInputSource.InputTrigger.LeftThumbDown:
						case XInputSource.InputTrigger.RightThumbDown:
							this.Image = Properties.Resources.Icon_ArrowDown;
							base.Text = (this.XInputTrigger.ToString().StartsWith("DPad")) ? "D-Pad" : this.XInputTrigger.ToString().Split('T')[0] + " Stick";
							break;
						default:
							base.Text = new Regex("([A-Z])").Replace(this.XInputTrigger.ToString(), " $1").Trim();
							break;
					}
					break;
				default:
					base.Text = "";
					break;
			}
			this.ForeColor = Color.FromKnownColor(this.Text != "None" ? KnownColor.ControlText : KnownColor.GrayText);
			

		}

		public override string Text {
			get { return base.Text; }
			set { }
		}

		public KeyButton() {
			this.KeyboardTrigger = Keys.None;
			this.JoystickTrigger = JoystickInputSource.InputTrigger.None;
			this.Appearance = Appearance.Button;
			this.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.TextImageRelation = TextImageRelation.TextBeforeImage;
			this.AutoCheck = false;
		}

		protected override bool IsInputKey(Keys keyData) {
			return this.Checked;
		}

	}
}
