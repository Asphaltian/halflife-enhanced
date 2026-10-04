using System;

namespace GoldSrc.Vgui1;

public sealed class BitmapFont
{
	private const int CharCount = 256;
	private const int HeaderSize = 18;
	private const int RunLengthTrueColor = 10;
	private const int BitsPerPixel = 32;
	private const int SpaceChar = 0x20;

	private readonly int[] _widths = new int[CharCount];
	private int _charWide;
	private int _tall;
	private byte[] _mask;

	public int Tall => _tall;

	public static BitmapFont Load( byte[] file )
	{
		if ( !LoadImage( file, out var wide, out var tall, out var pixels ) )
			return null;

		var font = new BitmapFont
		{
			_mask = new byte[tall * wide],
			_charWide = wide / CharCount,
			_tall = tall
		};

		var stride = font._charWide * CharCount;

		for ( var ch = 0; ch < CharCount; ch++ )
		{
			var last = 0;

			for ( var y = 0; y < tall; y++ )
			{
				var row = (y * stride) + (ch * font._charWide);

				for ( var x = 0; x < font._charWide; x++ )
				{
					if ( pixels[row + x] == 0 )
					{
						font._mask[row + x] = 0;
						continue;
					}

					font._mask[row + x] = 1;

					if ( last < x )
						last = x;
				}
			}

			font._widths[ch] = ch == SpaceChar ? font._charWide / 4 : last;
		}

		return font;
	}

	private static bool LoadImage( byte[] file, out int wide, out int tall, out uint[] pixels )
	{
		wide = 0;
		tall = 0;
		pixels = null;

		if ( file is null || file.Length < HeaderSize || file[16] != BitsPerPixel || file[2] != RunLengthTrueColor )
			return false;

		var position = HeaderSize + file[0];

		wide = BitConverter.ToUInt16( file, 12 );
		tall = BitConverter.ToUInt16( file, 14 );
		pixels = new uint[tall * wide];

		for ( var y = tall - 1; y >= 0; y-- )
		{
			var pixel = y * wide;
			var x = 0;

			while ( x < wide )
			{
				if ( position >= file.Length )
					return false;

				var packet = file[position++];
				var count = (packet & 0x7f) + 1;

				x += count;

				if ( wide < x )
					return false;

				if ( (packet & 0x80) != 0 )
				{
					if ( position + 4 > file.Length )
						return false;

					var color = BitConverter.ToUInt32( file, position );

					position += 4;

					for ( ; count != 0; count-- )
						pixels[pixel++] = color;
				}
				else
				{
					if ( position + (count * 4) > file.Length )
						return false;

					for ( ; count != 0; count-- )
					{
						pixels[pixel++] = BitConverter.ToUInt32( file, position );
						position += 4;
					}
				}
			}
		}

		return true;
	}

	public void GetCharRgba( int ch, int rgbaX, int rgbaY, int rgbaWide, int rgbaTall, byte[] rgba )
	{
		var source = Math.Clamp( ch, 0, CharCount ) * _charWide;

		for ( var y = 0; y < _tall; y++ )
		{
			for ( var x = 0; x < _charWide; x++ )
			{
				if ( rgbaX + x >= rgbaWide || rgbaY + y >= rgbaTall )
					continue;

				var index = source + x;
				var value = index < _mask.Length && _mask[index] != 0 ? (byte)0xff : (byte)0;
				var pixel = ((rgbaX + x) + ((rgbaY + y) * rgbaWide)) * 4;

				rgba[pixel + 0] = value;
				rgba[pixel + 1] = value;
				rgba[pixel + 2] = value;
				rgba[pixel + 3] = value;
			}

			source += CharCount * _charWide;
		}
	}

	public void GetCharAbcWide( int ch, out int a, out int b, out int c )
	{
		a = 0;
		b = _widths[Math.Clamp( ch, 0, CharCount - 1 )] + 1;
		c = 0;
	}
}

public sealed class Font
{
	private const int FirstId = 100;

	private static int _nextId = FirstId;

	private readonly BitmapFont _plat;

	public string Name { get; }

	public int Id { get; } = -1;

	public Font( string name, byte[] fileData, int tall, int wide, float rotation, int weight, bool italic, bool underline, bool strikeout, bool symbol )
	{
		Name = name;

		if ( fileData is null )
			return;

		_plat = BitmapFont.Load( fileData );

		if ( _plat is null )
			return;

		Id = _nextId++;
	}

	public void GetCharRgba( int ch, int rgbaX, int rgbaY, int rgbaWide, int rgbaTall, byte[] rgba )
	{
		_plat?.GetCharRgba( ch, rgbaX, rgbaY, rgbaWide, rgbaTall, rgba );
	}

	public void GetCharAbcWide( int ch, out int a, out int b, out int c )
	{
		a = 0;
		b = 0;
		c = 0;

		_plat?.GetCharAbcWide( ch, out a, out b, out c );
	}

	public int Tall => _plat?.Tall ?? 0;

	public void GetTextSize( string text, out int wide, out int tall )
	{
		wide = 0;
		tall = 0;

		if ( text is null )
			return;

		tall = Tall;

		var x = 0;

		foreach ( var character in text )
		{
			if ( character == '\n' )
			{
				x = 0;
				tall += Tall;
				continue;
			}

			GetCharAbcWide( (sbyte)character, out var a, out var b, out var c );

			x += a + b + c;

			if ( wide < x )
				wide = x;
		}
	}
}
