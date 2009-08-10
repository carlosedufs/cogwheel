﻿using System;
using BeeDevelopment.Brazil;
namespace BeeDevelopment.Sega8Bit.Hardware {
	public partial class VideoDisplayProcessor {

		#region Types

		/// <summary>
		/// Defines the possible regions of the scanning electron beam.
		/// </summary>
		public enum BeamRegion {
			/// <summary>
			/// Image data generated by the <see cref="VideoDisplayProcessor"/> is output in this region.
			/// </summary>
			ActiveDisplay,
			/// <summary>
			/// Region underneath the active display filled with the current border colour.
			/// </summary>
			BottomBorder,
			/// <summary>
			/// Region underneath the bottom border filled with light black.
			/// </summary>
			BottomBlanking,
			/// <summary>
			/// Region underneath the bottom blanking filled with pure black as a synchronisation signal.
			/// </summary>
			VerticalBlanking,
			/// <summary>
			/// Region above the top border filled with light black.
			/// </summary>
			TopBlanking,
			/// <summary>
			/// Region above the active display filled with the current border colour.
			/// </summary>
			TopBorder,
		}

		/// <summary>
		/// Defines the possible combinations of resolution and video format for timing.
		/// </summary>
		public enum TimingProfiles {
			/// <summary>
			/// NTSC at 192-line resolution.
			/// </summary>
			Ntsc192,
			/// <summary>
			/// NTSC at 224-line resolution.
			/// </summary>
			Ntsc224,
			/// <summary>
			/// NTSC at 240-line resolution.
			/// </summary>
			Ntsc240,
			/// <summary>
			/// PAL at 192-line resolution.
			/// </summary>
			Pal192,
			/// <summary>
			/// PAL at 224-line resolution.
			/// </summary>
			Pal224,
			/// <summary>
			/// PAL at 240-line resolution.
			/// </summary>
			Pal240,
		}

		#endregion

		#region Properties

		private BeamRegion beamLocation;
		/// <summary>
		/// Gets the <see cref="BeamRegion"/> of the scanning electron beam.
		/// </summary>
		public BeamRegion BeamLocation {
			get { return this.beamLocation; }
			private set { this.beamLocation = value; }
		}

		private TimingProfiles timingProfile;
		/// <summary>
		/// Gets the current <see cref="TimingProfiles"/> for the video frame being generated.
		/// </summary>		
		public TimingProfiles TimingProfile {
			get { return this.timingProfile; }
			private set { this.timingProfile = value; }
		}

		private bool spriteOverflow;
		/// <summary>
		/// Gets or sets the sprite overflow flag.
		/// </summary>
		/// <remarks>This flag is set automatically when more than the supported number of sprites are displayed on a scanline.</remarks>
		public bool SpriteOverflow {
			get { return this.spriteOverflow; }
			set { this.spriteOverflow = value; }
		}

		private bool spriteCollision;
		/// <summary>
		/// Gets or sets whether a sprite collision has occured.
		/// </summary>
		public bool SpriteCollision {
			get { return this.spriteCollision; }
			set { this.spriteCollision = value; }
		}		

		private int invalidSpriteIndex;
		/// <summary>
		/// Gets or sets the index of the first invalid sprite.
		/// </summary>
		public int InvalidSpriteIndex {
			get { return this.invalidSpriteIndex; }
			set { this.invalidSpriteIndex = value; }
		}

		private int lastBackdropColour;
		/// <summary>
		/// Gets or sets the last background colour as a 32-bit RGB value.
		/// </summary>
		public int LastBackdropColour {
			get { return this.lastBackdropColour; }
			set { this.lastBackdropColour = value; }
		}

		/// <summary>
		/// Gets the frame rate of the VDP in Hertz.
		/// </summary>
		[StateNotSaved()]
		public int FrameRate {
			get {
				switch (this.System) {
					case VideoSystem.Ntsc:
						return 60;
					case VideoSystem.Pal:
						return 50;
					default:
						throw new InvalidOperationException();
				}
			}
		}

		private int cpuCyclesPerScanline;
		/// <summary>
		/// Gets or sets the number of CPU cycles that are executed per scanline.
		/// </summary>
		public int CpuCyclesPerScanline {
			get { return this.cpuCyclesPerScanline; }
			set { this.cpuCyclesPerScanline = value; }
		}

		private int cpuCyclesAtStartOfScanline;
		/// <summary>
		/// Gets or sets the total number of CPU cycles that had been executed at the start of the current scanline.
		/// </summary>
		public int CpuCyclesAtStartOfScanline {
			get { return this.cpuCyclesAtStartOfScanline; }
			set { this.cpuCyclesAtStartOfScanline = value; }
		}

		private bool spriteLayerEnabled;
		/// <summary>
		/// Gets or sets whether the sprite layer is enabled.
		/// </summary>
		public bool SpriteLayerEnabled {
			get { return this.spriteLayerEnabled; }
			set { this.spriteLayerEnabled = value; }
		}

		private bool backgroundLayerEnabled;
		/// <summary>
		/// Gets or sets whether the background layer is enabled.
		/// </summary>
		public bool BackgroundLayerEnabled {
			get { return this.backgroundLayerEnabled; }
			set { this.backgroundLayerEnabled = value; }
		}
		

		#endregion

		#region Display Timing

		/// <summary>
		/// Maps a [timing profile, scanline index] -> value returned by the actual VCount port.
		/// </summary>
		private byte[,] ReturnedVCounters;

		/// <summary>
		/// Maps a [pixel index] -> value returned by the HCount port.
		/// </summary>
		private byte[] ReturnedHCounters;

		/// <summary>
		/// Preloads the ReturnedVCounters/ReturnedHCounters tables with counter position data.
		/// </summary>
		private void SetupTimingCounters() {

			this.ReturnedVCounters = new byte[6, 313];

			for (int i = 0; i < 6; ++i) {
				int VCountReturn = 0;
				for (int j = 0; j < 313; ++j) {
					this.ReturnedVCounters[i, j] = (byte)VCountReturn;
					++VCountReturn;
					switch ((TimingProfiles)i) {
						case TimingProfiles.Ntsc192: if (VCountReturn == 0xDB) VCountReturn = 0x1D5; break;
						case TimingProfiles.Ntsc224: if (VCountReturn == 0xEB) VCountReturn = 0x1E5; break;
						case TimingProfiles.Pal192: if (VCountReturn == 0xF3) VCountReturn = 0x1BA; break;
						case TimingProfiles.Pal224: if (VCountReturn == 0x103) VCountReturn = 0x1CA; break;
						case TimingProfiles.Pal240: if (VCountReturn == 0x10B) VCountReturn = 0x1D2; break;
					}
				}
			}

			this.ReturnedHCounters = new byte[342];
			byte CounterValue = 0;
			bool CounterRepeated = false;
			bool BlockRepeated = false;
			for (int i = 0; i < ReturnedHCounters.Length; ++i) {
				ReturnedHCounters[i] = CounterValue;
				if (((CounterValue + 1) % 3) == 0 && !CounterRepeated) {
					CounterRepeated = true;
				} else {
					CounterRepeated = false;
					++CounterValue;
				}
				if (CounterValue == 0xF0 && !BlockRepeated) {
					BlockRepeated = true;
					CounterValue = 0x94;
				}
			}
		}

		/// <summary>Table used to preload the scanline counter when timing frame zones.</summary>
		private int[,] ScreenSpaceCounters = { 
            { 192, 24, 3, 3, 13, 27 },
            { 224, 8,  3, 3, 13, 11 },
            { 240, 0,  0, 0, 0,  0  },
            { 192, 48, 3, 3, 13, 54 },
            { 224, 32, 3, 3, 13, 38 },
            { 240, 24, 3, 3, 13, 30 }
        };

		#endregion

		#region Frame State

		private int yScrollAtStartOfFrame;
		/// <summary>
		/// Stores the Y scroll value taken at the start of the frame.
		/// </summary>
		public int YScrollAtStartOfFrame {
			get { return this.yScrollAtStartOfFrame; }
			private set { this.yScrollAtStartOfFrame = value; }
		}		

		private int scanlinesDrawn;
		/// <summary>
		/// Gets the total number of drawn scanlines this frame.
		/// </summary>
		public int ScanlinesDrawn {
			get { return this.scanlinesDrawn; }
			private set { this.scanlinesDrawn = value; }
		}

		private int remainingScanlinesInRegion;
		/// <summary>
		/// Gets the number of remaining scanlines in the current region.
		/// </summary>
		public int RemainingScanlinesInRegion {
			get { return this.remainingScanlinesInRegion; }
			private set { this.remainingScanlinesInRegion = value; }
		}

		private int lineInterruptCounter;
		/// <summary>
		/// Counts down to trigger line interrupts.
		/// </summary>
		public int LineInterruptCounter {
			get { return this.lineInterruptCounter; }
			private set { this.lineInterruptCounter = value; }
		}

		private int activeFrameHeight;
		/// <summary>
		/// The height of the current frame being rendered.
		/// </summary>
		public int ActiveFrameHeight {
			get { return this.activeFrameHeight; }
			private set { this.activeFrameHeight = value; }
		}

		private int croppedFrameWidth;
		/// <summary>
		/// The width of the current frame being rendered.
		/// </summary>
		public int CroppedFrameWidth {
			get { return this.croppedFrameWidth; }
			private set { this.croppedFrameWidth = value; }
		}

		private int croppedFrameHeight;
		/// <summary>
		/// The cropped height of the current frame being rendered.
		/// </summary>
		public int CroppedFrameHeight {
			get { return this.croppedFrameHeight; }
			private set { this.croppedFrameHeight = value; }
		}

		/// <summary>
		/// Fixed 256x256 buffer to store the image as it gets rendered.
		/// </summary>
		private int[] PixelBuffer;

		/// <summary>
		/// Temporary pixel buffer used to perform Game Gear scaling.
		/// </summary>
		private int[] TempPixelBuffer;

		private int[] FastPixelColourIndex;

		/// <summary>
		/// Counts the number of scanlines seen by the left eye.
		/// </summary>
		private int ActiveScanlinesLeftEye;
		/// <summary>
		/// Counts the number of scanlines seen by the right eye.
		/// </summary>
		private int ActiveScanlinesRightEye;

		#endregion

		#region Private Methods

		/// <summary>
		/// Starts a frame.
		/// </summary>
		/// <remarks>
		/// Certain values cannot change during the build-up of a video frame, hence this method that caches them.
		/// </remarks>
		private void BeginFrame() {

			// Start at the top of the active display.
			this.beamLocation = BeamRegion.ActiveDisplay;
			this.scanlinesDrawn = 0;

			// Set timing profile.
			this.remainingScanlinesInRegion = this.VerticalResolution;
			this.activeFrameHeight = this.VerticalResolution;

			this.croppedFrameWidth = this.resizingMode == ResizingModes.Normal ? 256 : 160;
			this.croppedFrameHeight = this.resizingMode == ResizingModes.Normal ? this.activeFrameHeight : 144;

			switch (this.remainingScanlinesInRegion) {
				case 192: this.timingProfile = this.System == VideoSystem.Ntsc ? TimingProfiles.Ntsc192 : TimingProfiles.Pal192; break;
				case 224: this.timingProfile = this.System == VideoSystem.Ntsc ? TimingProfiles.Ntsc224 : TimingProfiles.Pal224; break;
				case 240: this.timingProfile = this.System == VideoSystem.Ntsc ? TimingProfiles.Ntsc240 : TimingProfiles.Pal240; break;
			}


			// Cache Y scroll.
			this.yScrollAtStartOfFrame = this.Registers[9];
			if (this.remainingScanlinesInRegion == 192) this.yScrollAtStartOfFrame %= 224;

			// Reset backdrop to 0.
			this.lastBackdropColour = 0;

			// Reset the number of scanlines seen by the left and right eyes.
			this.ActiveScanlinesLeftEye = this.ActiveScanlinesRightEye = 0;
		}

		private int[] lastCompleteFrame;
		[StateNotSaved()]
		public int[] LastCompleteFrame { get { return this.lastCompleteFrame; } }
		[StateNotSaved()]
		public int LastCompleteFrameHeight { get; private set; }
		[StateNotSaved()]
		public int LastCompleteFrameWidth { get; private set; }

		/// <summary>
		/// Ends a frame.
		/// </summary>
		private void EndFrame() {

			this.LastCompleteFrameWidth = this.croppedFrameWidth;
			this.LastCompleteFrameHeight = this.croppedFrameHeight;
			
			if (this.lastCompleteFrame == null || this.lastCompleteFrame.Length != this.croppedFrameWidth * this.croppedFrameHeight) {
				this.lastCompleteFrame = new int[this.croppedFrameWidth * this.croppedFrameHeight];
			}

			this.LastOpenGlassesShutter = this.ActiveScanlinesLeftEye > this.ActiveScanlinesRightEye ? Emulator.GlassesShutter.Left : Emulator.GlassesShutter.Right;

			switch (this.resizingMode) {
				case ResizingModes.Normal:
					Array.Copy(this.PixelBuffer, this.lastCompleteFrame, this.lastCompleteFrame.Length);
					break;
				case ResizingModes.Cropped:
					for (int Row = 0, Source = ((256 - 160) / 2) + (128 * (this.activeFrameHeight - 144)), Destination = 0, Width = this.LastCompleteFrameWidth; Row < 144; ++Row, Source += 256, Destination += Width) {
						Array.Copy(this.PixelBuffer, Source, this.lastCompleteFrame, Destination, Width);
					}
					break;
				case ResizingModes.Scaled:

					#region Game Gear scaling emulation

					Array.Copy(this.PixelBuffer, this.TempPixelBuffer, this.PixelBuffer.Length);
					
					int SrcPointer = 8;
					int DestPointer = 160 * 8;
					int MaxY = 128;

					if (this.activeFrameHeight == 224) {
						SrcPointer = 8 + 256 * 3;
						DestPointer = 0;
						MaxY = 144;
					}

					int[,] PixelGrid = new int[5, 3];

					for (int y = 0; y < MaxY; y += 2) {
						for (int x = 0; x < 160; x += 2) {

							if (this.activeFrameHeight == 224) {
								for (int i = 0; i < 3; ++i) {
									PixelGrid[0, i] = SrcPointer < 256 ? this.lastBackdropColour : TempPixelBuffer[SrcPointer - 256];
									PixelGrid[1, i] = TempPixelBuffer[SrcPointer + 000];
									PixelGrid[2, i] = TempPixelBuffer[SrcPointer + 256];
									PixelGrid[3, i] = TempPixelBuffer[SrcPointer + 512];
									PixelGrid[4, i] = TempPixelBuffer[SrcPointer + 768];
									++SrcPointer;
								}
							} else {
								for (int i = 0; i < 3; ++i) {
									PixelGrid[0, i] = SrcPointer < 512 ? this.lastBackdropColour : TempPixelBuffer[SrcPointer - 512];
									PixelGrid[1, i] = SrcPointer < 256 ? this.lastBackdropColour : TempPixelBuffer[SrcPointer - 256];
									PixelGrid[2, i] = TempPixelBuffer[SrcPointer + 000];
									PixelGrid[3, i] = TempPixelBuffer[SrcPointer + 256];
									PixelGrid[4, i] = TempPixelBuffer[SrcPointer + 512];
									++SrcPointer;
								}
							}


							for (int i = 0; i < 5; ++i) {
								int Pixel1 = (PixelGrid[i, 0] & 0xFFFF00) | (PixelGrid[i, 1] & 0x0000FF);
								int Pixel2 = (PixelGrid[i, 1] & 0xFF0000) | (PixelGrid[i, 2] & 0x00FFFF);
								PixelGrid[i, 0] = Pixel1;
								PixelGrid[i, 1] = Pixel2;
							}


							if (this.activeFrameHeight == 224) {
								for (int i = 0; i < 2; ++i) {
									PixelBuffer[DestPointer + 000] = BlendThirdRgb(PixelGrid[1, i], PixelGrid[2, i], BlendHalfRgb(PixelGrid[0, i], PixelGrid[3, i]));
									PixelBuffer[DestPointer + 160] = BlendThirdRgb(PixelGrid[2, i], PixelGrid[3, i], BlendHalfRgb(PixelGrid[1, i], PixelGrid[4, i]));
									++DestPointer;
								}
							} else {
								for (int i = 0; i < 2; ++i) {
									PixelBuffer[DestPointer + 000] = BlendThirdRgb(PixelGrid[1, i], PixelGrid[2, i], BlendHalfRgb(PixelGrid[0, i], PixelGrid[3, i]));
									PixelBuffer[DestPointer + 160] = BlendThirdRgb(PixelGrid[2, i], PixelGrid[3, i], PixelGrid[4, i]);
									++DestPointer;
								}
							}

						}
						DestPointer += 160;
						SrcPointer += 16 + 512;
					}

					if (this.activeFrameHeight == 192) {
						for (int i = 0; i < 160 * 8; ++i) {
							PixelBuffer[i] = this.lastBackdropColour;
							PixelBuffer[i + 0x5500] = this.lastBackdropColour;
						}
					}

					#endregion

					Array.Copy(this.PixelBuffer, this.lastCompleteFrame, 160 * 144);

					break;
			}

		}

		/// <summary>
		/// Gets the offset to the name table in VRAM.
		/// </summary>
		/// <param name="yScroll">The current y scroll offset.</param>
		/// <returns>The address of the first tile in the row that corresponds to the number of scanlines rendered and the y-scroll offset.</returns>
		private int GetNameTableOffset(int yScroll) {

			int TilemapRow = (this.scanlinesDrawn + yScroll) / 8;

			if (this.activeFrameHeight == 192) {
				TilemapRow %= 28;
			} else {
				TilemapRow %= 32;
			}

			if (this.supportsMirroredNameTable && (this.registers[0x2] & 0x01) == 0) {
				TilemapRow &= 0xEF;
			}

			return (TilemapRow * 64) + (this.activeFrameHeight == 192 ? ((this.registers[0x2] & 0xE) * 1024) : ((this.registers[0x2] & 0xC) * 1024 + 0x700));
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Rasterise a single scanline.
		/// </summary>
		/// <returns>True if the <see cref="VideoDisplayProcessor"/> has just completed rendering the active display.</returns>
		public bool RasteriseLine() {

			this.cpuCyclesAtStartOfScanline = this.Emulator.ExpectedExecutedCycles;

			// Start by updating the vertical counter.
			this.VerticalCounter = this.ReturnedVCounters[(int)this.timingProfile, this.scanlinesDrawn];

			#region Rendering

			bool UsingMode4 = false;

			if (this.beamLocation == BeamRegion.ActiveDisplay) {

				// Used to keep track of foreground/background pixels (?)
				bool[] ForegroundBackground = new bool[256];

				bool amZoomed = this.ZoomSprites;

				int startPixel = this.scanlinesDrawn * 256;

				int[] FixedPalette = this.fixedPaletteMode == FixedPaletteModes.MasterSystem ? this.FixedPaletteMasterSystem : this.FixedPaletteTMS9918;

				if (!this.DisplayVisible) {
					// Screen is off
					switch (this.CurrentMode) {
						case Mode.Graphic1:
						case Mode.Graphic2:
						case Mode.Multicolor:
						case Mode.Text:
							this.lastBackdropColour = FixedPalette[this.Registers[0x7] & 0xF];
							break;
						default:
							this.lastBackdropColour = this.colourRam[(Registers[0x7] & 0xF) + 16];
							break;
					}
					for (int i = 0; i < 256; ++i) this.PixelBuffer[startPixel++] = this.lastBackdropColour;
				} else {

					this.lastBackdropColour = this.colourRam[(this.registers[0x7] & 0xF) + 16];

					switch (this.CurrentMode) {

						case Mode.Mode4:
						case Mode.Mode4Resolution224:
						case Mode.Mode4Resolution240: {
								#region Mode 4 background layer

								int NameTableOffset = GetNameTableOffset(this.yScrollAtStartOfFrame);

								int TileNum;
								int TileOffset;

								bool FlippedX, FlippedY;

								int TileRowOffset;

								int PaletteNumber;

								bool ForegroundTile;

								byte UpperByte;

								byte CurrentXScroll = this.InhibitScrollX && (this.scanlinesDrawn < 16) ? (byte)0 : this.registers[0x8];

								int MasterTileRowOffset = ((this.scanlinesDrawn + this.yScrollAtStartOfFrame) & 7);

								int[] Colours = new int[8];

								// Clear eight leftmost pixels to overscan colour.
								for (int i = 0; i < 8; ++i) {
									this.PixelBuffer[startPixel + i] = this.lastBackdropColour;
								}

								for (int ScreenColumn = 0; ScreenColumn < 32; ++ScreenColumn) {

									// Handle Y-scroll inhibition.
									if (ScreenColumn == 24 && this.InhibitScrollY) {
										MasterTileRowOffset = this.scanlinesDrawn & 7;
										NameTableOffset = GetNameTableOffset(0);
									}

									int NameTableColumn = (ScreenColumn - CurrentXScroll / 8) & 31;
									int NameTableColumnFine = CurrentXScroll & 7;

									UpperByte = vram[(NameTableOffset + NameTableColumn * 2 + 1) & 0x3FFF];

									FlippedX = (UpperByte & 0x2) != 0;
									FlippedY = (UpperByte & 0x4) != 0;

									PaletteNumber = (UpperByte & 0x8) * 2;

									ForegroundTile = (UpperByte & 0x10) != 0;

									TileNum = this.vram[(NameTableOffset + NameTableColumn * 2) & 0x3FFF] + (UpperByte & 1) * 256;
									TileOffset = TileNum * 64;

									if (FlippedY) {
										TileRowOffset = 7 - MasterTileRowOffset;
									} else {
										TileRowOffset = MasterTileRowOffset;
									}

									TileOffset += TileRowOffset * 8;

									// Read the tile data (either backwards or forwards depending on FlippedX).
									if (FlippedX) {
										for (int p = 7; p >= 0; --p) Colours[p] = this.FastPixelColourIndex[TileOffset++];
									} else {
										for (int p = 0; p <= 7; ++p) Colours[p] = this.FastPixelColourIndex[TileOffset++];
									}

									int o = (ScreenColumn * 8 + NameTableColumnFine) & 0xFF;

									for (int p = 0; p < 8; p++) {
										this.PixelBuffer[startPixel + o] = this.colourRam[Colours[p] + PaletteNumber];
										ForegroundBackground[o] = ForegroundTile && Colours[p] != 0;
										if (++o == 256) break;
									}

								}

								#endregion
								UsingMode4 = true;
							} break;


						case Mode.Graphic1:
						case Mode.Graphic2: {
								#region Graphic 1/2 background layer

								this.lastBackdropColour = FixedPalette[this.Registers[0x7] & 0xF];

								int NameTableStartAddress = (this.Registers[0x2] & 0x0F) << 10;
								int PatternGeneratorAddress = (this.Registers[0x4] & 0x04) << 11;

								int CharacterOffset = (this.scanlinesDrawn / 64) * 0x100;

								int ColourTableAddress = (this.Registers[0x3] & 0x80) << 6;

								if (this.CurrentMode == Mode.Graphic1) {
									NameTableStartAddress = (this.Registers[0x2] & 0x0F) * 0x400;
									CharacterOffset = 0;
									PatternGeneratorAddress = (this.Registers[0x4] & 7) << 11;
									ColourTableAddress = this.Registers[0x3] * 0x40;
								}

								int CharacterColour;
								for (int Column = 0; Column < 32; ++Column) {

									int CharacterIndex = vram[NameTableStartAddress + Column + (this.scanlinesDrawn / 8) * 32] + CharacterOffset;

									int CharacterPixelRow = vram[CharacterIndex * 8 + (this.scanlinesDrawn & 7) + PatternGeneratorAddress];


									if (this.CurrentMode == Mode.Graphic1) {
										CharacterColour = vram[CharacterIndex / 8 + ColourTableAddress];
									} else {
										CharacterColour = vram[CharacterIndex * 8 + (this.scanlinesDrawn & 7) + ColourTableAddress];
									}

									int ColourForeground = lastBackdropColour;
									int ColourBackground = lastBackdropColour;

									if ((CharacterColour & 0xF0) != 0) ColourForeground = FixedPalette[CharacterColour >> 04];
									if ((CharacterColour & 0x0F) != 0) ColourBackground = FixedPalette[CharacterColour & 0xF];

									for (int Pixel = 0; Pixel < 8; ++Pixel) {
										PixelBuffer[startPixel + Column * 8 + Pixel] = (CharacterPixelRow & 0x80) != 0 ? ColourForeground : ColourBackground;
										CharacterPixelRow <<= 1;
									}
								}

								#endregion
							} break;

						case Mode.Text: {
								#region Text background layer

								int ColourForeground = FixedPalette[this.registers[0x7] >> 04];
								int ColourBackground = FixedPalette[this.registers[0x7] & 0xF];

								int NameTableStartAddress = (this.Registers[0x2] & 0x0F) << 10;
								int PatternGeneratorAddress = (this.Registers[0x4] & 0x07) << 11;


								// Fill in the backdrop colour for the 8 pixels either side that are not in use.
								for (int i = 0; i < 8; ++i) {
									PixelBuffer[startPixel + i] = ColourBackground;
									PixelBuffer[startPixel + i + 248] = ColourBackground;
								}

								// Fill in a line of text
								int PixelPosition = startPixel + 8;
								for (int Column = 0; Column < 40; ++Column) {

									int CharacterIndex = vram[NameTableStartAddress + Column + (this.scanlinesDrawn / 8) * 40];
									int CharacterPixelRow = vram[CharacterIndex * 8 + (this.scanlinesDrawn & 7) + PatternGeneratorAddress];


									for (int j = 0; j < 6; ++j) {
										PixelBuffer[PixelPosition++] = (CharacterPixelRow & 0x80) != 0 ? ColourForeground : ColourBackground;
										CharacterPixelRow <<= 1;
									}

								}


								#endregion
							} break;

						case Mode.Multicolor: {
								#region Multicolor Mode

								int NameTableStartAddress = (this.Registers[0x2] & 0x0F) << 10;
								int PatternGeneratorAddress = (this.Registers[0x4] & 0x04) << 11;

								int Row = this.scanlinesDrawn / 8;
								int HalfRowOffset = (this.scanlinesDrawn / 4) & 1;

								for (int Column = 0, PixelPosition = startPixel; Column < 32; ++Column) {

									byte ValueFromNameTable = this.vram[(NameTableStartAddress + Row * 32 + Column) & 0x3FFF];
									int PatternGeneratorOffset = HalfRowOffset + (ValueFromNameTable * 8) + (Row & 3) * 2;

									byte ColourAttribute = this.vram[(PatternGeneratorAddress + PatternGeneratorOffset) & 0x3FFF];

									for (int i = 0; i < 2; ++i) {

										int Colour = (ColourAttribute & 0xF0) == 0 ? this.lastBackdropColour : FixedPalette[ColourAttribute >> 4];
										ColourAttribute <<= 4;

										for (int j = 0; j < 4; ++j) {
											this.PixelBuffer[PixelPosition++] = Colour;
										}
									}
								}

								#endregion
							} break;

						default:
							#region Blank
							for (int i = 0; i < 256; ++i) {
								PixelBuffer[startPixel + i] = unchecked((int)0xFF000000);
							}
							break;
							#endregion

					}			

					if (!this.backgroundLayerEnabled) for (int i = 0; i < 256; ++i) this.PixelBuffer[startPixel + i] = this.lastBackdropColour;

					if (this.spriteLayerEnabled) {

						switch (this.CurrentMode) {

							case Mode.Mode4:
							case Mode.Mode4Resolution224:
							case Mode.Mode4Resolution240:
								#region Sprites

								int SAT = (Registers[5] & 0x7E) * 128;

								int ec = this.EarlyClock ? -8 : 0;

								bool ds = this.JoinSprites;
								int sh = (ds ? 16 : 8) * (amZoomed ? 2 : 1);
								int EigthBit = (Registers[0x6] & 0x4) << 6;

								bool[] spriteCollisionBuffer = new bool[256];
								byte pixelX;

								int maxSprites = this.spritesPerScanline;
								int maxZoomedSprites = this.zoomedSpritesPerScanline;

								for (int i = 0; i < 64; ++i) {

									int y = vram[SAT + i] + 1;
									if (y == 0xD1 && this.activeFrameHeight == 192) break;

									if (y >= 224) y -= 256;

									if (y > this.scanlinesDrawn || (y + sh) <= this.scanlinesDrawn) continue;

									if (maxSprites-- == 0) {
										this.spriteOverflow = true;
										break;
									}

									int x = vram[SAT + i * 2 + 0x80] + ec;
									int n = vram[SAT + i * 2 + 0x81];

									if (ds) n &= ~1;

									int spritePtr = (n + EigthBit) * 64 + ((this.scanlinesDrawn - y) / (amZoomed ? 2 : 1)) * 8;

									int[] colours = new int[8];

									for (int p = 0; p < 8; ++p) {
										colours[p] = this.FastPixelColourIndex[spritePtr++];
									}

									pixelX = (byte)x;

									if (amZoomed && (--maxZoomedSprites > 0)) {
										for (int p = 0; p < 16; ++p) {

											if (colours[p / 2] != 0) {
												if (spriteCollisionBuffer[pixelX]) {
													this.spriteCollision = true;
												} else {
													if (!ForegroundBackground[pixelX]) {
														spriteCollisionBuffer[pixelX] = true;
														PixelBuffer[startPixel + pixelX] = this.colourRam[colours[p / 2] + 16];
													}
												}
											}
											if (pixelX == 0xFF) break;
											++pixelX;
										}
									} else {
										for (int p = 0; p < 8; ++p) {

											if (colours[p] != 0) {
												if (spriteCollisionBuffer[pixelX]) {
													this.spriteCollision = true;
												} else {
													spriteCollisionBuffer[pixelX] = true;
													if (!ForegroundBackground[pixelX]) {
														PixelBuffer[startPixel + pixelX] = this.colourRam[colours[p] + 16];
													}
												}
											}
											if (pixelX == 0xFF) break;
											++pixelX;
										}
									}

								}

								#endregion
								break;

							case Mode.Graphic1:
							case Mode.Graphic2:
							case Mode.Multicolor:
								#region Sprites

								bool[] SpriteCollisions = new bool[320];
								bool[] DrawnPixel = new bool[256];

								int SpriteAttributeTable = (this.Registers[0x5] & 0x7F) * 0x80;
								int SpritePatternGenerator = (this.Registers[0x6] & 0x07) * 0x800;
								int SpritePerScanlineCap = this.spritesPerScanline;

								bool MagMode = (this.Registers[0x1] & 0x1) != 0;
								bool SizeMode = (this.Registers[0x1] & 0x2) != 0;

								int SpriteSize = SizeMode ? (MagMode ? 32 : 16) : (MagMode ? 16 : 8);
								int HalfSize = SpriteSize / 2 - 1;

								for (int SpriteNum = 0; SpriteNum < 32; ++SpriteNum) {
									int SpriteY = this.vram[SpriteAttributeTable++];
									int SpriteX = this.vram[SpriteAttributeTable++];
									int SpriteIndex = this.vram[SpriteAttributeTable++];
									int SpriteFlags = this.vram[SpriteAttributeTable++];

									if (SpriteY == 0xD0) break; // Special terminator value.

									// Sprites can bleed off the top of the display:
									if (SpriteY > 224) SpriteY -= 256;

									// Sprites appear one scanline below the declared line:
									++SpriteY;

									// Handle Early Clock bit (shift sprite left 32 pixels if set).
									if ((SpriteFlags & 0x80) != 0) SpriteX -= 32;

									// Is the sprite visible on this scanline?
									if (SpriteY > this.scanlinesDrawn || (SpriteY + SpriteSize) <= this.scanlinesDrawn) continue;

									// Check we haven't drawn too many sprites on this scanline!
									if (SpritePerScanlineCap-- == 0) {
										this.spriteOverflow = true;
										this.invalidSpriteIndex = SpriteNum;
										break;
									}

									if (SizeMode) SpriteIndex &= 0xFC;
									int SpriteTextureCoord = this.scanlinesDrawn - SpriteY;
									if (MagMode) SpriteTextureCoord /= 2;

									int SpritePatternDataAddress = SpritePatternGenerator + SpriteIndex * 8 + SpriteTextureCoord;
									int SpritePixelRow = this.vram[SpritePatternDataAddress];

									for (int x = 0; x < SpriteSize; ++x) {

										// Position of the pixel on-screen.
										int PixelOffset = x + SpriteX;

										// Check for collisions
										if (SpriteCollisions[PixelOffset + 32]) {
											this.spriteCollision = true;
										} else {
											SpriteCollisions[PixelOffset + 32] = true;
										}

										if (PixelOffset >= 0 && PixelOffset < 256 && !DrawnPixel[PixelOffset]) {
											bool PixelSet = (SpritePixelRow & 0x80) != 0;
											if (PixelSet && (SpriteFlags & 0x0F) != 0) {
												PixelBuffer[startPixel + PixelOffset] = FixedPalette[SpriteFlags & 0x0F];
												DrawnPixel[PixelOffset] = true;
											}
										}

										// Move to next pixel:
										if (!MagMode || (x & 1) == 1) {
											if (SizeMode && x == HalfSize) {
												SpritePixelRow = this.vram[SpritePatternDataAddress + 16];
											} else {
												SpritePixelRow <<= 1;
											}

										}


									}

								}

								#endregion
								break;
						}

					}

					if (UsingMode4 && this.MaskColumn0) {
						for (int i = 0; i < 8; i++) PixelBuffer[startPixel + i] = lastBackdropColour;
					}

				}

			}


			#endregion


			#region Interrupts


			// Handle frame interrupts.
			bool FrameInterrupted = false;
			switch (this.activeFrameHeight) {
				case 192: FrameInterrupted = this.scanlinesDrawn == 0xC1; break;
				case 224: FrameInterrupted = this.scanlinesDrawn == 0xE1; break;
				case 240: FrameInterrupted = this.scanlinesDrawn == 0xF1; break;
			}
			if (FrameInterrupted) {
				this.EndFrame();
				this.frameInterruptPending = true;
			}

			// Handle line interrupts.
			if (this.supportsLineInterrupts) {
				if (this.scanlinesDrawn <= this.activeFrameHeight) {
					if (this.lineInterruptCounter-- <= 0) {
						this.lineInterruptPending = true;
						this.lineInterruptCounter = this.registers[0xA];
					}
				} else {
					this.lineInterruptCounter = this.registers[0xA];
				}
			}

			this.UpdateIrq();

			#endregion

			// We've drawn yet another, got to be worth something at least.
			++this.scanlinesDrawn;

			// Well, that was that...
			if (--this.remainingScanlinesInRegion == 0) {

				// Move on to the next part of the display!
				while (this.remainingScanlinesInRegion == 0) {
					this.beamLocation = (BeamRegion)(((int)this.beamLocation + 1) % 6);
					this.remainingScanlinesInRegion = this.ScreenSpaceCounters[(int)this.timingProfile, (int)this.beamLocation];
				}

				// What are we doing now?
				switch (this.beamLocation) {
					case BeamRegion.ActiveDisplay:
						// As we're about to re-enter the active display, we need to set up the relevant frame data.
						this.BeginFrame();
						break;
				}
			}

			// Copy the open glasses shutter value from the middle of the current frame.
			if (this.beamLocation == BeamRegion.ActiveDisplay/* && this.RemainingScanlinesInRegion == this.ActiveFrameHeight / 2*/) {
				switch (this.Emulator.OpenGlassesShutter) {
					case Emulator.GlassesShutter.Left:
						++this.ActiveScanlinesLeftEye;
						break;
					case Emulator.GlassesShutter.Right:
						++this.ActiveScanlinesRightEye;
						break;
				}
			}

			return FrameInterrupted;
		}

		#endregion

		#region Pixel Blending

		internal static int BlendHalfRgb(int a, int b) {
			return unchecked((int)0xFF000000) | ((((a & 0x0000FF) + (b & 0x0000FF)) / 2) & 0x0000FF) | ((((a & 0x00FF00) + (b & 0x00FF00)) / 2) & 0x00FF00) | ((((a & 0xFF0000) + (b & 0xFF0000)) / 2) & 0xFF0000);
		}

		internal static int BlendQuarterRgb(int a, int b) {
			return unchecked((int)0xFF000000) | ((((a & 0x0000FF) + (b & 0x0000FF) * 3) / 4) & 0x0000FF) | ((((a & 0x00FF00) + (b & 0x00FF00) * 3) / 4) & 0x00FF00) | ((((a & 0xFF0000) + (b & 0xFF0000) * 3) / 4) & 0xFF0000);
		}

		internal static int BlendEighthRgb(int a, int b) {
			return unchecked((int)0xFF000000) | ((((a & 0x0000FF) + (b & 0x0000FF) * 7) / 8) & 0x0000FF) | ((((a & 0x00FF00) + (b & 0x00FF00) * 7) / 8) & 0x00FF00) | ((((a & 0xFF0000) + (b & 0xFF0000) * 7) / 8) & 0xFF0000);
		}

		internal static int BlendThirdRgb(int a, int b) {
			return unchecked((int)0xFF000000) | ((((a & 0x0000FF) + (b & 0x0000FF) * 2) / 3) & 0x0000FF) | ((((a & 0x00FF00) + (b & 0x00FF00) * 2) / 3) & 0x00FF00) | ((((a & 0xFF0000) + (b & 0xFF0000) * 2) / 3) & 0xFF0000);
		}
		internal static int BlendThirdRgb(int a, int b, int c) {
			return unchecked((int)0xFF000000) |
				   ((((a & 0x0000FF) + (b & 0x0000FF) + (c & 0x0000FF)) / 3) & 0x0000FF)
				 | ((((a & 0x00FF00) + (b & 0x00FF00) + (c & 0x00FF00)) / 3) & 0x00FF00)
				 | ((((a & 0xFF0000) + (b & 0xFF0000) + (c & 0xFF0000)) / 3) & 0xFF0000);
		}


		#endregion

	}
}