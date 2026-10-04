using System;
using System.IO;
using System.Text;

namespace GoldSrc.GameUI;

public static class LogoFile
{
	private const string LogoName = "LOGO";
	private const string WadFileName = "tempdecal.wad";
	private const int LumpBufferSize = 0x5000;
	private const int MipTexHeaderSize = 40;
	private const int BitmapPixelOffset = 0x428;
	private const int MipLevels = 4;
	private const int PaletteSize = 256;
	private const int WadHeaderSize = 12;
	private const int LumpInfoSize = 32;
	private const byte TypeMipTex = 0x40;

	private static int GrabMip( byte[] bitmap, byte[] lump, byte red, byte green, byte blue, out int width, out int height )
	{
		width = BitConverter.ToInt32( bitmap, 4 );
		height = BitConverter.ToInt32( bitmap, 8 );

		if ( ((width | height) & 15) != 0 )
			return 0;

		Encoding.Latin1.GetBytes( LogoName, 0, LogoName.Length, lump, 0 );
		BitConverter.TryWriteBytes( lump.AsSpan( 16 ), width );
		BitConverter.TryWriteBytes( lump.AsSpan( 20 ), height );
		BitConverter.TryWriteBytes( lump.AsSpan( 24 ), MipTexHeaderSize );

		var position = MipTexHeaderSize;

		for ( var y = 0; y < height; y++ )
		{
			Array.Copy( bitmap, BitmapPixelOffset + ((height - 1 - y) * width), lump, position, width );

			position += width;
		}

		for ( var level = 1; level < MipLevels; level++ )
		{
			var step = 1 << level;

			BitConverter.TryWriteBytes( lump.AsSpan( 24 + (level * 4) ), position );

			for ( var y = 0; y < height; y += step )
			{
				for ( var x = 0; x < width; x += step )
					lump[position++] = lump[MipTexHeaderSize + (y * width) + x];
			}
		}

		BitConverter.TryWriteBytes( lump.AsSpan( position ), (short)PaletteSize );

		position += 2 + ((PaletteSize - 1) * 3);

		lump[position++] = red;
		lump[position++] = green;
		lump[position++] = blue;

		return position;
	}

	public static bool UpdateLogoWad( byte[] bitmap, byte red, byte green, byte blue )
	{
		if ( bitmap is null )
			return false;

		var lump = new byte[LumpBufferSize];
		var size = GrabMip( bitmap, lump, red, green, blue, out var width, out var height );

		if ( size == 0 || width != height || width is not (16 or 32 or 64) )
			return false;

		while ( (size & 3) != 0 )
			size++;

		using var stream = new MemoryStream();
		using var writer = new BinaryWriter( stream );

		writer.Write( Encoding.Latin1.GetBytes( "WAD3" ) );
		writer.Write( 1 );
		writer.Write( size + WadHeaderSize );
		writer.Write( lump, 0, size );

		var info = new byte[LumpInfoSize];

		BitConverter.TryWriteBytes( info.AsSpan( 0 ), WadHeaderSize );
		BitConverter.TryWriteBytes( info.AsSpan( 4 ), size );
		BitConverter.TryWriteBytes( info.AsSpan( 8 ), size );

		info[12] = TypeMipTex;

		Encoding.Latin1.GetBytes( LogoName, 0, LogoName.Length, info, 16 );

		writer.Write( info );
		writer.Flush();

		Server.WriteFile( WadFileName, stream.ToArray() );

		return true;
	}
}
