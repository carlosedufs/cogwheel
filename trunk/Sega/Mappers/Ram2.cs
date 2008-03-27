﻿using System;

namespace BeeDevelopment.Sega8Bit.Mappers {

	/// <summary>
	/// A simple 2KB of RAM.
	/// </summary>
	[Serializable()]
	public class Ram2 : IMemoryMapper {

		#region Private Fields

		/// <summary>
		/// The entire cartridge RAM.
		/// </summary>
		private byte[] Memory;

		#endregion

		#region Reading and Writing

		/// <summary>
		/// Reads a byte from memory.
		/// </summary>
		/// <param name="address">The address to read from.</param>
		/// <returns>The byte read from memory from address <paramref name="address"/>.</returns>
		public byte ReadMemory(ushort address) {
			return this.Memory[address & 0x7FF];
		}

		/// <summary>
		/// Writes a byte to memory.
		/// </summary>
		/// <param name="address">The address to write to .</param>
		/// <param name="value">The data to write.</param>
		public void WriteMemory(ushort address, byte value) {
			this.Memory[address & 0x7FF] = value;
		}

		#endregion

		#region Initialisation

		/// <summary>
		/// Resets the mapper to its default state.
		/// </summary>
		public void Reset() {
			// Do nothing...
		}

		/// <summary>
		/// Loads ROM data.
		/// </summary>
		/// <param name="data">Data taken from a ROM dump.</param>
		public void Load(byte[] data) {
			Array.Copy(data,this.Memory,Math.Min(data.Length, this.Memory.Length));
		}

		/// <summary>
		/// Creates an instance of the <see cref="Ram2"/> mapper.
		/// </summary>
		public Ram2() {
			this.Memory = new byte[0x800];
		}

		#endregion
	}
}