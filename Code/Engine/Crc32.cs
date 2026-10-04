using System;

namespace GoldSrc;

public static class Crc32
{
	private static readonly uint[] Table = BuildTable();

	private static uint[] BuildTable()
	{
		var table = new uint[256];

		for ( var i = 0u; i < 256; i++ )
		{
			var value = i;

			for ( var bit = 0; bit < 8; bit++ )
				value = (value & 1) != 0 ? (value >> 1) ^ 0xedb88320 : value >> 1;

			table[i] = value;
		}

		return table;
	}

	public static byte TableByte( int offset ) => (byte)(Table[offset >> 2] >> ((offset & 3) * 8));

	public const uint Initial = 0xffffffff;

	public static uint Process( uint crc, ReadOnlySpan<byte> buffer )
	{
		foreach ( var value in buffer )
			crc = Table[(byte)(crc ^ value)] ^ (crc >> 8);

		return crc;
	}

	public static uint Compute( ReadOnlySpan<byte> buffer ) => Process( Initial, buffer ) ^ 0xffffffff;

	public static uint Compute( Vector3 vector )
	{
		Span<byte> buffer = stackalloc byte[12];

		BitConverter.TryWriteBytes( buffer, vector.x );
		BitConverter.TryWriteBytes( buffer[4..], vector.y );
		BitConverter.TryWriteBytes( buffer[8..], vector.z );

		return Compute( buffer );
	}

	public static uint Compute( short a, short b )
	{
		Span<byte> buffer = stackalloc byte[4];

		BitConverter.TryWriteBytes( buffer, a );
		BitConverter.TryWriteBytes( buffer[2..], b );

		return Compute( buffer );
	}
}
