﻿using System;
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

		/// <summary>
		/// Gets the <see cref="BeamRegion"/> of the scanning electron beam.
		/// </summary>
		public BeamRegion BeamLocation { get; private set; }

		/// <summary>
		/// Gets the current <see cref="TimingProfiles"/> for the video frame being generated.
		/// </summary>
		public TimingProfiles TimingProfile { get; private set; }

		/// <summary>
		/// Gets the current vertical counter position.
		/// </summary>
		public byte VerticalCounter { get; private set; }


		/// <summary>
		/// Gets or sets the sprite overflow flag.
		/// </summary>
		/// <remarks>This flag is set automatically when more than the supported number of sprites are displayed on a scanline.</remarks>
		public bool SpriteOverflow { get; set; }

		/// <summary>
		/// Gets or sets whether a sprite collision has occured.
		/// </summary>
		public bool SpriteCollision { get; set; }

		/// <summary>
		/// Gets or sets the index of the first invalid sprite.
		/// </summary>
		public int InvalidSpriteIndex { get; set; }

		#endregion

		#region Display Timing

		/// <summary>
		/// Maps a [timing profile, scanline index] -> value returned by the actual VCount port.
		/// </summary>
		private byte[,] ReturnedVCounters;

		/// <summary>
		/// Preloads the ReturnedVCounters table with counter position data.
		/// </summary>
		private void SetupVCounters() {

			ReturnedVCounters = new byte[6, 313];

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

		/// <summary>
		/// Stores the Y scroll value taken at the start of the frame.
		/// </summary>
		private int YScroll;

		/// <summary>
		/// Gets the total number of drawn scanlines this frame.
		/// </summary>
		private int ScanlinesDrawn;

		/// <summary>
		/// Gets the number of remaining scanlines in the current region.
		/// </summary>
		private int RemainingScanlinesInRegion;

		/// <summary>
		/// Counts down to trigger line interrupts.
		/// </summary>
		private int LineInterruptCounter;

		/// <summary>
		/// The height of the current frame being rendered.
		/// </summary>
		private int ActiveFrameHeight;

		/// <summary>
		/// The width of the current frame being rendered.
		/// </summary>
		private int CroppedFrameWidth;

		/// <summary>
		/// The cropped height of the current frame being rendered.
		/// </summary>
		private int CroppedFrameHeight;

		/// <summary>
		/// Fixed 256x256 buffer to store the image as it gets rendered.
		/// </summary>
		private int[] PixelBuffer;

		/// <summary>
		/// Temporary pixel buffer used to perform Game Gear scaling.
		/// </summary>
		private int[] TempPixelBuffer;

		private int[] FastPixelColourIndex;

		private int LastBackdropColour;

		#endregion

		#region Begin/End


		/// <summary>
		/// Starts a frame.
		/// </summary>
		/// <remarks>
		/// Certain values cannot change during the build-up of a video frame, hence this method that caches them.
		/// </remarks>
		private void BeginFrame() {

			// Start at the top of the active display.
			this.BeamLocation = BeamRegion.ActiveDisplay;
			this.ScanlinesDrawn = 0;

			// Set timing profile.
			this.RemainingScanlinesInRegion = this.VerticalResolution;
			this.ActiveFrameHeight = this.VerticalResolution;

			this.CroppedFrameWidth = this.ResizingMode == ResizingModes.Normal ? 256 : 160;
			this.CroppedFrameHeight = this.ResizingMode == ResizingModes.Normal ? this.ActiveFrameHeight : 144;

			switch (this.RemainingScanlinesInRegion) {
				case 192: this.TimingProfile = this.System == VideoSystem.Ntsc ? TimingProfiles.Ntsc192 : TimingProfiles.Pal192; break;
				case 224: this.TimingProfile = this.System == VideoSystem.Ntsc ? TimingProfiles.Ntsc224 : TimingProfiles.Pal224; break;
				case 240: this.TimingProfile = this.System == VideoSystem.Ntsc ? TimingProfiles.Ntsc240 : TimingProfiles.Pal240; break;
			}


			// Cache Y scroll.
			this.YScroll = this.Registers[9];
			if (this.RemainingScanlinesInRegion == 192) this.YScroll %= 224;

			// Reset backdrop to 0.
			this.LastBackdropColour = 0;


		}

		private int[] lastCompleteFrame;
		public int[] LastCompleteFrame { get { return this.lastCompleteFrame; } }
		public int LastCompleteFrameHeight { get; private set; }
		public int LastCompleteFrameWidth { get; private set; }

		/// <summary>
		/// Ends a frame.
		/// </summary>
		private void EndFrame() {

			if (this.lastCompleteFrame == null || this.CroppedFrameWidth != this.LastCompleteFrameWidth || this.CroppedFrameHeight != this.LastCompleteFrameHeight) {
				this.LastCompleteFrameWidth = this.CroppedFrameWidth;
				this.LastCompleteFrameHeight = this.CroppedFrameHeight;
				this.lastCompleteFrame = new int[this.CroppedFrameWidth * this.CroppedFrameHeight];
			}

			switch (this.ResizingMode) {
				case ResizingModes.Normal:
					Array.Copy(this.PixelBuffer, this.lastCompleteFrame, this.lastCompleteFrame.Length);
					break;
				case ResizingModes.Cropped:
					for (int Row = 0, Source = ((256 - 160) / 2) + (128 * (this.ActiveFrameHeight - 144)), Destination = 0, Width = this.LastCompleteFrameWidth; Row < 144; ++Row, Source += 256, Destination += Width) {
						Array.Copy(this.PixelBuffer, Source, this.lastCompleteFrame, Destination, Width);
					}
					break;
				case ResizingModes.Scaled:

					#region Game Gear scaling emulation

					Array.Copy(this.PixelBuffer, this.TempPixelBuffer, this.PixelBuffer.Length);
					
					int SrcPointer = 8;
					int DestPointer = 160 * 8;
					int MaxY = 128;

					if (this.ActiveFrameHeight == 224) {
						SrcPointer = 8 + 256 * 3;
						DestPointer = 0;
						MaxY = 144;
					}

					int[,] PixelGrid = new int[5, 3];

					for (int y = 0; y < MaxY; y += 2) {
						for (int x = 0; x < 160; x += 2) {

							if (this.ActiveFrameHeight == 224) {
								for (int i = 0; i < 3; ++i) {
									PixelGrid[0, i] = SrcPointer < 256 ? this.LastBackdropColour : TempPixelBuffer[SrcPointer - 256];
									PixelGrid[1, i] = TempPixelBuffer[SrcPointer + 000];
									PixelGrid[2, i] = TempPixelBuffer[SrcPointer + 256];
									PixelGrid[3, i] = TempPixelBuffer[SrcPointer + 512];
									PixelGrid[4, i] = TempPixelBuffer[SrcPointer + 768];
									++SrcPointer;
								}
							} else {
								for (int i = 0; i < 3; ++i) {
									PixelGrid[0, i] = SrcPointer < 512 ? this.LastBackdropColour : TempPixelBuffer[SrcPointer - 512];
									PixelGrid[1, i] = SrcPointer < 256 ? this.LastBackdropColour : TempPixelBuffer[SrcPointer - 256];
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


							if (this.ActiveFrameHeight == 224) {
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

					if (this.ActiveFrameHeight == 192) {
						for (int i = 0; i < 160 * 8; ++i) {
							PixelBuffer[i] = this.LastBackdropColour;
							PixelBuffer[i + 0x5500] = this.LastBackdropColour;
						}
					}

					#endregion

					Array.Copy(this.PixelBuffer, this.lastCompleteFrame, 160 * 144);

					break;
			}

		}

		#endregion


		#region Methods


		/// <summary>
		/// Rasterise a single scanline.
		/// </summary>
		/// <returns>True if the <see cref="VideoDisplayProcessor"/> has just completed rendering the active display.</returns>
		public bool RasteriseLine() {

			// Start by updating the vertical counter.
			this.VerticalCounter = this.ReturnedVCounters[(int)this.TimingProfile, this.ScanlinesDrawn];

			#region Rendering

			bool UsingMode4 = false;

			if (this.BeamLocation == BeamRegion.ActiveDisplay) {

				// Used to keep track of foreground/background pixels (?)
				bool[] ForegroundBackground = new bool[256];

				bool amZoomed = this.ZoomSprites;

				int startPixel = this.ScanlinesDrawn * 256;

				if (!this.DisplayVisible) {
					// Screen is off
					switch (this.CurrentMode) {
						case Mode.Graphic1:
						case Mode.Graphic2:
						case Mode.Multicolour:
						case Mode.Text:
							this.LastBackdropColour = this.FixedPaletteTMS9918[this.Registers[0x7] & 0xF];
							break;
						default:
							this.LastBackdropColour = this.colourRam[(Registers[0x7] & 0xF) + 16];
							break;
					}
					for (int i = 0; i < 256; ++i) this.PixelBuffer[startPixel++] = this.LastBackdropColour;
				} else {

					this.LastBackdropColour = this.colourRam[(Registers[0x7] & 0xF) + 16];

					switch (this.CurrentMode) {

						case Mode.Mode4:
						case Mode.Mode4Resolution224:
						case Mode.Mode4Resolution240: {
								#region Mode 4 background layer

								// Get name table base address

								int row = (this.ScanlinesDrawn + this.YScroll) / 8;
								if (this.ActiveFrameHeight == 192) {
									row %= 28;
								} else {
									row %= 32;
								}

								int nameTableOffset = (row * 64) + (this.ActiveFrameHeight == 192 ? ((this.registers[0x2] & 0xE) * 1024) : ((this.registers[0x2] & 0xC) * 1024 + 0x700));

								int tileNum;
								int tileOffset;

								bool flippedX;
								bool flippedY;
								int tileRowOffset;

								int paletteNumber;

								bool foregroundTile;

								byte upperByte;

								byte currentXScroll = this.InhibitScrollX && (this.ScanlinesDrawn < 16) ? (byte)0 : this.registers[0x8];

								int masterTileRowOffset = ((this.ScanlinesDrawn + this.YScroll) & 7);

								int[] colours = new int[8];

								for (int acol = 0; acol < 32; ++acol) {

									int col = (acol - currentXScroll / 8) & 31;
									int colExtra = currentXScroll & 7;



									upperByte = vram[nameTableOffset + col * 2 + 1];

									flippedX = (upperByte & 0x2) != 0;
									flippedY = (upperByte & 0x4) != 0;

									paletteNumber = (upperByte & 0x8) * 2;

									foregroundTile = (upperByte & 0x10) != 0;

									tileNum = vram[nameTableOffset + col * 2] + (upperByte & 1) * 256;
									tileOffset = tileNum * 64;


									if (flippedY) {
										tileRowOffset = 7 - masterTileRowOffset;
									} else {
										tileRowOffset = masterTileRowOffset;
									}

									tileOffset += tileRowOffset * 8;


									if (!flippedX) {
										for (int p = 0; p <= 7; ++p) {
											colours[p] = this.FastPixelColourIndex[tileOffset++];
										}
									} else {
										for (int p = 7; p >= 0; --p) {
											colours[p] = this.FastPixelColourIndex[tileOffset++];
										}
									}

									int o = (acol * 8 + colExtra) & 0xFF;

									for (int p = 0; p < 8; p++) {
										this.PixelBuffer[startPixel + o] = this.colourRam[colours[p] + paletteNumber];
										ForegroundBackground[o] = foregroundTile && colours[p] != 0;
										if (++o == 256) break;
									}

								}

								#endregion
								UsingMode4 = true;
							} break;


						case Mode.Graphic1:
						case Mode.Graphic2: {
								#region Graphic 1/2 background layer

								//this.LastBackdropColour = this.PaletteTMS9918[CapFixedPaletteIndex, this.Registers[0x7] & 0xF];

								int NameTableStartAddress = (this.Registers[0x2] & 0x0F) << 10;

								int PatternGeneratorAddress = (this.Registers[0x4] & 0x04) << 11;

								int CharacterOffset = (this.ScanlinesDrawn / 64) * 0x100;

								int ColourTableAddress = (this.Registers[0x3] & 0x80) << 6;

								if (this.CurrentMode == Mode.Graphic1) {
									NameTableStartAddress = (this.Registers[0x2] & 0x0F) * 0x400;
									CharacterOffset = 0;
									PatternGeneratorAddress = (this.Registers[0x4] & 7) << 11;
									ColourTableAddress = this.Registers[0x3] * 0x40;
								}

								int CharacterColour;
								for (int Column = 0; Column < 32; ++Column) {

									int CharacterIndex = vram[NameTableStartAddress + Column + (this.ScanlinesDrawn / 8) * 32] + CharacterOffset;

									int CharacterPixelRow = vram[CharacterIndex * 8 + (this.ScanlinesDrawn & 7) + PatternGeneratorAddress];


									if (this.CurrentMode == Mode.Graphic1) {
										CharacterColour = vram[CharacterIndex / 8 + ColourTableAddress];
									} else {
										CharacterColour = vram[CharacterIndex * 8 + (this.ScanlinesDrawn & 7) + ColourTableAddress];
									}

									int ColourForeground = LastBackdropColour;
									int ColourBackground = LastBackdropColour;

									if ((CharacterColour & 0xF0) != 0) ColourForeground = this.FixedPaletteTMS9918[CharacterColour >> 04];
									if ((CharacterColour & 0x0F) != 0) ColourBackground = this.FixedPaletteTMS9918[CharacterColour & 0xF];

									for (int Pixel = 0; Pixel < 8; ++Pixel) {
										PixelBuffer[startPixel + Column * 8 + Pixel] = (CharacterPixelRow & 0x80) != 0 ? ColourForeground : ColourBackground;
										CharacterPixelRow <<= 1;
									}
								}

								#endregion
							} break;

						case Mode.Text: {
								#region Text background layer

								int ColourForeground = this.FixedPaletteTMS9918[this.registers[0x7] >> 04];
								int ColourBackground = this.FixedPaletteTMS9918[this.registers[0x7] & 0xF];

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

									int CharacterIndex = vram[NameTableStartAddress + Column + (this.ScanlinesDrawn / 8) * 40];
									int CharacterPixelRow = vram[CharacterIndex * 8 + (this.ScanlinesDrawn & 7) + PatternGeneratorAddress];


									for (int j = 0; j < 6; ++j) {
										PixelBuffer[PixelPosition++] = (CharacterPixelRow & 0x80) != 0 ? ColourForeground : ColourBackground;
										CharacterPixelRow <<= 1;
									}

								}


								#endregion
							} break;

						default:
							#region Random noise
							for (int i = 0; i < 256; ++i) {
								PixelBuffer[startPixel + i] = unchecked((int)0xFF000000);
							}
							break;
							#endregion

					}


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

							int maxSprites = this.SpritesPerScanline;
							int maxZoomedSprites = this.ZoomedSpritesPerScanline;

							for (int i = 0; i < 64; ++i) {

								int y = vram[SAT + i] + 1;
								if (y == 0xD1 && this.ActiveFrameHeight == 192) break;

								if (y >= 224) y -= 256;

								if (y > this.ScanlinesDrawn || (y + sh) <= this.ScanlinesDrawn) continue;

								if (maxSprites-- == 0) {
									this.SpriteOverflow = true;
									break;
								}

								int x = vram[SAT + i * 2 + 0x80] + ec;
								int n = vram[SAT + i * 2 + 0x81];

								if (ds) n &= ~1;

								int spritePtr = (n + EigthBit) * 64 + ((this.ScanlinesDrawn - y) / (amZoomed ? 2 : 1)) * 8;

								int[] colours = new int[8];

								for (int p = 0; p < 8; ++p) {
									colours[p] = this.FastPixelColourIndex[spritePtr++];
								}

								pixelX = (byte)x;

								if (amZoomed && (--maxZoomedSprites > 0)) {
									for (int p = 0; p < 16; ++p) {
										if (!ForegroundBackground[pixelX]) {
											if (colours[p / 2] != 0) {
												if (spriteCollisionBuffer[pixelX]) {
													this.SpriteCollision = true;
												} else {
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
										if (!ForegroundBackground[pixelX]) {
											if (colours[p] != 0) {
												if (spriteCollisionBuffer[pixelX]) {
													this.SpriteCollision = true;
												} else {
													spriteCollisionBuffer[pixelX] = true;
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
							#region Sprites

							bool[] SpriteCollisions = new bool[320];
							bool[] DrawnPixel = new bool[256];

							int SpriteAttributeTable = (this.Registers[0x5] & 0x7F) * 0x80;
							int SpritePatternGenerator = (this.Registers[0x6] & 0x07) * 0x800;
							int SpritePerScanlineCap = this.SpritesPerScanline;

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
								if (SpriteY > this.ScanlinesDrawn || (SpriteY + SpriteSize) <= this.ScanlinesDrawn) continue;

								// Check we haven't drawn too many sprites on this scanline!
								if (SpritePerScanlineCap-- == 0) {
									this.SpriteOverflow = true;
									this.InvalidSpriteIndex = SpriteNum;
									break;
								}

								if (SizeMode) SpriteIndex &= 0xFC;
								int SpriteTextureCoord = this.ScanlinesDrawn - SpriteY;
								if (MagMode) SpriteTextureCoord /= 2;

								int SpritePatternDataAddress = SpritePatternGenerator + SpriteIndex * 8 + SpriteTextureCoord;
								int SpritePixelRow = this.vram[SpritePatternDataAddress];

								for (int x = 0; x < SpriteSize; ++x) {

									// Position of the pixel on-screen.
									int PixelOffset = x + SpriteX;

									// Check for collisions
									if (SpriteCollisions[PixelOffset + 32]) {
										this.SpriteCollision = true;
									} else {
										SpriteCollisions[PixelOffset + 32] = true;
									}

									if (PixelOffset >= 0 && PixelOffset < 256 && !DrawnPixel[PixelOffset]) {
										bool PixelSet = (SpritePixelRow & 0x80) != 0;
										if (PixelSet && (SpriteFlags & 0x0F) != 0) {
											PixelBuffer[startPixel + PixelOffset] = this.FixedPaletteTMS9918[SpriteFlags & 0x0F];
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

					if (UsingMode4 && this.MaskColumn0) {
						for (int i = 0; i < 8; i++) PixelBuffer[startPixel + i] = LastBackdropColour;
					}

				}

			}


			#endregion


			#region Interrupts


			// Handle frame interrupts.
			bool FrameInterrupted = false;
			switch (this.ActiveFrameHeight) {
				case 192: FrameInterrupted = this.ScanlinesDrawn == 0xC1; break;
				case 224: FrameInterrupted = this.ScanlinesDrawn == 0xE1; break;
				case 240: FrameInterrupted = this.ScanlinesDrawn == 0xF1; break;
			}
			if (FrameInterrupted) {
				this.EndFrame();
				this.frameInterruptPending = true;
			}

			// Handle line interrupts.
			if (this.SupportsLineInterrupts) {
				if (this.ScanlinesDrawn <= this.ActiveFrameHeight) {
					if (this.LineInterruptCounter-- <= 0) {
						this.lineInterruptPending = true;
						this.LineInterruptCounter = this.registers[0xA];
					}
				} else {
					this.LineInterruptCounter = this.registers[0xA];
				}
			}

			this.UpdateIrq();

			#endregion

			// We've drawn yet another, got to be worth something at least.
			++this.ScanlinesDrawn;

			// Well, that was that...
			if (--this.RemainingScanlinesInRegion == 0) {

				// Move on to the next part of the display!
				while (this.RemainingScanlinesInRegion == 0) {
					this.BeamLocation = (BeamRegion)(((int)this.BeamLocation + 1) % 6);
					this.RemainingScanlinesInRegion = this.ScreenSpaceCounters[(int)this.TimingProfile, (int)this.BeamLocation];
				}

				// What are we doing now?
				switch (this.BeamLocation) {
					case BeamRegion.ActiveDisplay:
						// As we're about to re-enter the active display, we need to set up the relevant frame data.
						this.BeginFrame();
						break;
				}
			}

			return FrameInterrupted;
		}

		#endregion

		#region Pixel Blending

		private int BlendHalfRgb(int a, int b) {
			return unchecked((int)0xFF000000) | ((((a & 0x0000FF) + (b & 0x0000FF)) / 2) & 0x0000FF) | ((((a & 0x00FF00) + (b & 0x00FF00)) / 2) & 0x00FF00) | ((((a & 0xFF0000) + (b & 0xFF0000)) / 2) & 0xFF0000);
		}

		private int BlendQuarterRgb(int a, int b) {
			return unchecked((int)0xFF000000) | ((((a & 0x0000FF) + (b & 0x0000FF) * 3) / 4) & 0x0000FF) | ((((a & 0x00FF00) + (b & 0x00FF00) * 3) / 4) & 0x00FF00) | ((((a & 0xFF0000) + (b & 0xFF0000) * 3) / 4) & 0xFF0000);
		}

		private int BlendEighthRgb(int a, int b) {
			return unchecked((int)0xFF000000) | ((((a & 0x0000FF) + (b & 0x0000FF) * 7) / 8) & 0x0000FF) | ((((a & 0x00FF00) + (b & 0x00FF00) * 7) / 8) & 0x00FF00) | ((((a & 0xFF0000) + (b & 0xFF0000) * 7) / 8) & 0xFF0000);
		}

		private int BlendThirdRgb(int a, int b) {
			return unchecked((int)0xFF000000) | ((((a & 0x0000FF) + (b & 0x0000FF) * 2) / 3) & 0x0000FF) | ((((a & 0x00FF00) + (b & 0x00FF00) * 2) / 3) & 0x00FF00) | ((((a & 0xFF0000) + (b & 0xFF0000) * 2) / 3) & 0xFF0000);
		}
		private int BlendThirdRgb(int a, int b, int c) {
			return unchecked((int)0xFF000000) |
				   ((((a & 0x0000FF) + (b & 0x0000FF) + (c & 0x0000FF)) / 3) & 0x0000FF)
				 | ((((a & 0x00FF00) + (b & 0x00FF00) + (c & 0x00FF00)) / 3) & 0x00FF00)
				 | ((((a & 0xFF0000) + (b & 0xFF0000) + (c & 0xFF0000)) / 3) & 0xFF0000);
		}


		#endregion


	}
}