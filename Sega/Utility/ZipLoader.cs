﻿using System;
using BeeDevelopment.Zip;
using System.IO;

namespace BeeDevelopment.Sega8Bit.Utility {
	
	/// <summary>
	/// Provides methods for searching for and loading a ROM from a zip archive.
	/// </summary>
	public static class ZipLoader {

		/// <summary>
		/// Loads ROM data from a zip file.
		/// </summary>
		public static byte[] FindRom(ref string searchFileName) {

			try {

				using (var RomStream = File.OpenRead(searchFileName)) {
					byte[] FileHeader = new byte[4];

					bool IsZipFile = false;

					if (RomStream.Read(FileHeader, 0, 4) == 4) {

						byte[] ZipHeader = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
						IsZipFile = true;
						for (int i = 0; i < 4; ++i) {
							if (FileHeader[i] != ZipHeader[i]) {
								IsZipFile = false;
								break;
							}
						}

						RomStream.Seek(0, SeekOrigin.Begin);

						if (IsZipFile) {
							
							var Zipped = ZipFile.FromFile(searchFileName);
							foreach (var ZipEntry in Zipped) {
								switch (Path.GetExtension(ZipEntry.Name).ToLowerInvariant()) {
									case ".gg":
									case ".sms":
									case ".sc":
									case ".sg":
									case ".rom":
									case ".mv":
										searchFileName = Path.Combine(searchFileName, ZipEntry.Name);
										return ZipEntry.Data;
								}
							}


						} else {
							return File.ReadAllBytes(searchFileName);
						}

					}
				}
			} catch {
				return File.ReadAllBytes(searchFileName);
			}
			return File.ReadAllBytes(searchFileName);
		}
	}
}
