using System;
using System.IO;

namespace GoldSrc;

partial class SpriteModel
{
	private const int MapImageBufferSize = 0x300000;
	private const int MapTileSize = 128;
	private const int MaxMapSpriteName = 0x103;
	private const int TgaHeaderSize = 18;
	private const int BmpFileHeaderSize = 14;
	private const int BmpInfoHeaderSize = 40;
	private const int BmpMaxColors = 256;

	private static byte[] LoadTga( string name, int bufferSize, out int width, out int height )
	{
		width = 0;
		height = 0;

		if ( Server.ReadFile( name ) is not { } data )
			return null;

		if ( data.Length < TgaHeaderSize + 2 )
		{
			Con.Printf( "LoadTGA: file shorter than TGA file header length\n" );
			return null;
		}

		var idLength = data[0];
		var colorMapType = data[1];
		var imageType = data[2];
		var pixelSize = data[16];

		if ( imageType != 2 && imageType != 10 )
		{
			Con.Printf( "LoadTGA: Only type 2 and 10 targa RGB images supported\n" );
			return null;
		}

		if ( colorMapType != 0 || (pixelSize != 32 && pixelSize != 24) )
		{
			Con.Printf( "Texture_LoadTGA: Only 32 or 24 bit images supported (no colormaps)\n" );
			return null;
		}

		width = data[12] | (data[13] << 8);
		height = data[14] | (data[15] << 8);

		var count = width * height;

		if ( count * 4 > bufferSize )
		{
			Con.Printf( $"LoadTGA: texture too large ({width}x{height}>256x256)\n" );
			return null;
		}

		var pixels = new byte[count * 4];
		var position = TgaHeaderSize + idLength;
		var bytes = pixelSize / 8;

		if ( imageType == 2 )
		{
			if ( position + (count * bytes) > data.Length )
			{
				Con.Printf( "LoadTGA: file shorter than length of texture\n" );
				return null;
			}

			for ( var row = height - 1; row >= 0; row-- )
			{
				var target = row * width * 4;

				for ( var column = 0; column < width; column++ )
				{
					pixels[target] = data[position + 2];
					pixels[target + 1] = data[position + 1];
					pixels[target + 2] = data[position];
					pixels[target + 3] = bytes == 4 ? data[position + 3] : (byte)255;

					position += bytes;
					target += 4;
				}
			}

			return pixels;
		}

		var y = height - 1;
		var x = 0;

		while ( y >= 0 && position < data.Length )
		{
			var header = data[position++];
			var packetSize = 1 + (header & 0x7f);
			var runLength = (header & 0x80) != 0;
			byte red = 0, green = 0, blue = 0, alpha = 255;

			for ( var i = 0; i < packetSize && y >= 0; i++ )
			{
				if ( !runLength || i == 0 )
				{
					if ( position + bytes > data.Length )
						return pixels;

					blue = data[position];
					green = data[position + 1];
					red = data[position + 2];
					alpha = bytes == 4 ? data[position + 3] : (byte)255;

					position += bytes;
				}

				var target = ((y * width) + x) * 4;

				pixels[target] = red;
				pixels[target + 1] = green;
				pixels[target + 2] = blue;
				pixels[target + 3] = alpha;

				if ( ++x == width )
				{
					x = 0;
					y--;
				}
			}
		}

		return pixels;
	}

	private static bool LoadBmp8( byte[] data, out byte[] palette, out byte[] indices, out int width, out int height )
	{
		palette = null;
		indices = null;
		width = 0;
		height = 0;

		if ( data.Length < BmpFileHeaderSize + BmpInfoHeaderSize )
			return false;

		var fileSize = BitConverter.ToUInt32( data, 2 );

		if ( BitConverter.ToInt16( data, 6 ) != 0 || BitConverter.ToInt16( data, 8 ) != 0 || fileSize > (uint)data.Length )
			return false;

		var info = BmpFileHeaderSize;

		if ( BitConverter.ToInt32( data, info ) != BmpInfoHeaderSize || BitConverter.ToInt16( data, info + 12 ) != 1 || BitConverter.ToInt16( data, info + 14 ) != 8 || BitConverter.ToInt32( data, info + 16 ) != 0 )
			return false;

		var imageWidth = BitConverter.ToInt32( data, info + 4 );
		var imageHeight = BitConverter.ToInt32( data, info + 8 );

		if ( imageWidth <= 0 || imageHeight <= 0 )
			return false;

		var colors = BitConverter.ToUInt32( data, info + 32 );

		if ( colors == 0 )
			colors = BmpMaxColors;
		else if ( colors > BmpMaxColors )
			return false;

		var position = info + BmpInfoHeaderSize;

		if ( position + (colors * 4) > data.Length )
			return false;

		var remaining = fileSize - (uint)(position + (colors * 4));
		var stride = (uint)((imageWidth + 3) & ~3);
		var size = stride * (uint)imageHeight;

		if ( size < stride || size < (uint)imageHeight || size > remaining )
			return false;

		palette = new byte[BmpMaxColors * 3];

		for ( var i = 0; i < colors; i++ )
		{
			palette[i * 3] = data[position + (i * 4) + 2];
			palette[(i * 3) + 1] = data[position + (i * 4) + 1];
			palette[(i * 3) + 2] = data[position + (i * 4)];
		}

		position += (int)(colors * 4);

		indices = new byte[remaining];

		for ( var row = 0; row < imageHeight; row++ )
			Array.Copy( data, position + ((imageHeight - 1 - row) * stride), indices, row * stride, stride );

		width = imageWidth;
		height = imageHeight;

		return true;
	}

	public static SpriteModel LoadMapSprite( string fileName )
	{
		var name = fileName.Length > MaxMapSpriteName ? fileName[..MaxMapSpriteName] : fileName;

		name = name.Replace( '\\', '/' );

		byte[] pixels;
		int width;
		int height;

		if ( name.EndsWith( ".tga", StringComparison.OrdinalIgnoreCase ) )
		{
			pixels = LoadTga( name, MapImageBufferSize, out width, out height );

			if ( pixels is null )
			{
				Con.Printf( $"Couldn't open {fileName}\n" );
				return null;
			}

			for ( var i = 0; i < width * height; i++ )
			{
				if ( pixels[i * 4] == 0 && pixels[(i * 4) + 1] == 255 && pixels[(i * 4) + 2] == 0 )
					pixels.AsSpan( i * 4, 4 ).Clear();
			}
		}
		else
		{
			if ( !name.EndsWith( ".bmp", StringComparison.OrdinalIgnoreCase ) )
				throw new InvalidDataException( "Unsupported map image file format.\n" );

			if ( Server.ReadFile( name ) is not { } data )
			{
				Con.Printf( $"Couldn't open {name}\n" );
				return null;
			}

			if ( !LoadBmp8( data, out var palette, out var indices, out width, out height ) )
			{
				Con.Printf( $"Error loading {name}\n" );
				return null;
			}

			pixels = new byte[width * height * 4];

			for ( var i = 0; i < width * height; i++ )
			{
				var color = indices[i] * 3;
				var red = palette[color];
				var green = palette[color + 1];
				var blue = palette[color + 2];

				if ( red == 0 && green == 255 && blue == 0 )
					continue;

				pixels[i * 4] = red;
				pixels[(i * 4) + 1] = green;
				pixels[(i * 4) + 2] = blue;
				pixels[(i * 4) + 3] = 255;
			}
		}

		if ( width * height == 0 || width % MapTileSize != 0 || height % MapTileSize != 0 )
		{
			Con.Printf( $"Wrong map image dimensions or file could not be loaded in {name}\n" );
			return null;
		}

		var frames = new SpriteFrame[(width / MapTileSize) * (height / MapTileSize)];
		var tile = new byte[MapTileSize * MapTileSize * 4];
		var index = 0;

		for ( var y = 0; y < height; y += MapTileSize )
		{
			for ( var x = 0; x < width; x += MapTileSize )
			{
				for ( var row = 0; row < MapTileSize; row++ )
					Array.Copy( pixels, (((y + row) * width) + x) * 4, tile, row * MapTileSize * 4, MapTileSize * 4 );

				frames[index++] = new SpriteFrame
				{
					Up = 0f,
					Down = -MapTileSize,
					Left = 0f,
					Right = MapTileSize,
					Texture = Texture.Create( MapTileSize, MapTileSize ).WithData( tile ).Finish()
				};
			}
		}

		var model = new SpriteModel
		{
			Name = name,
			SpriteType = SpriteType.ParallelOriented,
			Frames = frames,
			Mins = new Vector3( -(MapTileSize / 2), -(MapTileSize / 2), -(MapTileSize / 2) ),
			Maxs = new Vector3( MapTileSize / 2, MapTileSize / 2, MapTileSize / 2 )
		};

		ModelCache.Add( model );

		return model;
	}
}
