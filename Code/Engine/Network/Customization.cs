using System;
using System.IO;

namespace GoldSrc;

public sealed class CustomDecalImage
{
	public int Width;
	public int Height;
	public bool AlphaTest;
	public byte[] Pixels;
	public byte[] Palette;
	public Texture Texture;
}

public sealed class CustomDecalWad
{
	private const string Name = "tempdecal.wad";
	private const int MaxCacheEntries = 16;
	private const int HeaderSize = 12;
	private const int LumpInfoSize = 32;
	private const int TextureHeaderSize = 0x20;
	private const int MipTexSize = 40;
	private const int MipLevels = 4;
	private const int MaxTextureSize = 256;
	private const int PaletteColors = 256;

	private readonly CustomDecalImage[] _cache = new CustomDecalImage[MaxCacheEntries];

	private int _lumpOffset;
	private int _lumpDiskSize;
	private int _lumpSize;

	public int LumpCount { get; private set; }

	public static CustomDecalWad Init( byte[] data, int size )
	{
		if ( data is null || size < HeaderSize || data[0] != 'W' || data[1] != 'A' || data[2] != 'D' || data[3] != '3' )
		{
			Log.Info( "Custom file doesn't have WAD3 id\n" );
			return null;
		}

		var lumps = BitConverter.ToInt32( data, 4 );
		var infoTable = BitConverter.ToInt32( data, 8 );

		if ( lumps != 1 )
		{
			Log.Info( $"Custom file has wrong number of lumps {lumps}\n" );
			return null;
		}

		if ( infoTable < 1 )
		{
			Log.Info( $"Custom file has bogus infotableofs {infoTable}\n" );
			return null;
		}

		if ( infoTable + LumpInfoSize != size )
		{
			Log.Info( $"Custom file has bogus infotableofs ( {infoTable + LumpInfoSize} > {size} )\n" );
			return null;
		}

		var wad = new CustomDecalWad
		{
			_lumpOffset = BitConverter.ToInt32( data, infoTable ),
			_lumpDiskSize = BitConverter.ToInt32( data, infoTable + 4 ),
			_lumpSize = BitConverter.ToInt32( data, infoTable + 8 )
		};

		if ( wad._lumpSize != wad._lumpDiskSize )
		{
			Log.Info( $"Custom file has mismatched lump size ( {wad._lumpSize} vs. {wad._lumpDiskSize} )\n" );
			return null;
		}

		if ( wad._lumpDiskSize < 1 )
		{
			Log.Info( $"Custom file has bogus lump size {wad._lumpDiskSize}\n" );
			return null;
		}

		if ( (uint)wad._lumpOffset < HeaderSize )
		{
			Log.Info( $"Custom file has bogus lump offset {wad._lumpOffset}\n" );
			return null;
		}

		if ( wad._lumpDiskSize + wad._lumpOffset > infoTable )
		{
			Log.Info( "Custom file has bogus lump 0\n" );
			return null;
		}

		wad.LumpCount = 1;

		return wad;
	}

	public static bool Validate( byte[] data, int size )
	{
		return Init( data, size ) is { } wad && wad.CacheGet( data, 0 ) is not null;
	}

	public CustomDecalImage CacheGet( byte[] data, int index )
	{
		if ( index >= LumpCount )
			throw new InvalidDataException( $"Cache wad indexed before load {Name}: {index}" );

		if ( _cache[index] is { } cached )
			return cached;

		if ( !ValidateCustomLogo( data ) )
			return null;

		var width = BitConverter.ToInt32( data, _lumpOffset + 16 );
		var height = BitConverter.ToInt32( data, _lumpOffset + 20 );
		var pixels = _lumpOffset + BitConverter.ToInt32( data, _lumpOffset + 24 );
		var palette = pixels + MipChainSize( width * height ) + 2;

		var image = new CustomDecalImage
		{
			Width = width,
			Height = height,
			Pixels = data.AsSpan( pixels, width * height ).ToArray(),
			Palette = new byte[PaletteColors * 3]
		};

		Array.Copy( data, palette, image.Palette, 0, Math.Min( image.Palette.Length, data.Length - palette ) );

		var last = (PaletteColors - 1) * 3;

		image.AlphaTest = image.Palette[last] == 0 && image.Palette[last + 1] == 0 && image.Palette[last + 2] == 255;

		return _cache[index] = image;
	}

	private static int MipChainSize( int pixels ) => pixels + (pixels >> 2) + (pixels >> 4) + (pixels >> 6);

	private bool ValidateCustomLogo( byte[] data )
	{
		if ( _lumpDiskSize < MipTexSize )
		{
			Log.Info( $"Draw_ValidateCustomLogo: Bad cached wad {Name}\n" );
			return false;
		}

		var width = BitConverter.ToUInt32( data, _lumpOffset + 16 );
		var height = BitConverter.ToUInt32( data, _lumpOffset + 20 );

		if ( width == 0 || height == 0 || width > MaxTextureSize || height > MaxTextureSize )
		{
			Log.Info( $"Draw_ValidateCustomLogo: Bad cached wad tex size {width}x{height} on {Name}\n" );
			return false;
		}

		var pixels = (int)(width * height);
		var size = MipChainSize( pixels );

		if ( _lumpSize + TextureHeaderSize <= size + MipTexSize )
			Log.Info( $"Draw_ValidateCustomLogo: Bad cached wad size {size + MipTexSize}/{_lumpSize + TextureHeaderSize} on {Name}\n" );

		Span<int> offsets = stackalloc int[MipLevels];

		for ( var i = 0; i < MipLevels; i++ )
			offsets[i] = BitConverter.ToInt32( data, _lumpOffset + 24 + (i * 4) );

		for ( var i = 0; i < MipLevels - 1; i++, pixels >>= 2 )
		{
			if ( offsets[i] + pixels == offsets[i + 1] )
				continue;

			Log.Info( $"Draw_ValidateCustomLogo: Bad cached wad {Name}\n" );
			return false;
		}

		var count = size + MipTexSize + 2 <= _lumpDiskSize ? BitConverter.ToUInt16( data, _lumpOffset + MipTexSize + size ) : 0;

		if ( count > PaletteColors )
		{
			Log.Info( $"Draw_ValidateCustomLogo: Bad cached wad palette size {count} on {Name}\n" );
			return false;
		}

		if ( offsets[0] >= MipTexSize && (uint)(offsets[0] + size + (count * 3) + 2) <= (uint)_lumpDiskSize )
			return true;

		Log.Info( $"Draw_ValidateCustomLogo: Bad cached wad {Name}\n" );

		return false;
	}
}

public sealed class Customization
{
	public const int FromHpak = 1 << 0;
	public const int WadOnly = 1 << 1;
	public const int IgnoreInit = 1 << 2;

	private const int MinDecalSize = 1024;
	private const int MaxDecalSize = 20480;

	public bool InUse;
	public Resource Resource;
	public bool Translated;
	public int UserData1;
	public int UserData2;
	public CustomDecalWad Info;
	public byte[] Buffer;

	public bool IsDecal => (Resource.Flags & Resource.Custom) != 0 && Resource.Type == ResourceKind.Decal;

	public static bool Create( List<Customization> list, Resource resource, int playerNum, int flags, out Customization customization )
	{
		customization = null;

		if ( resource.DownloadSize <= 0 )
			return false;

		var created = new Customization { Resource = resource.Clone(), InUse = true };

		if ( (flags & FromHpak) == 0 )
			created.Buffer = Server.ReadFile( resource.Name );
		else if ( !Hpak.GetDataPointer( Hpak.Custom, resource, out created.Buffer ) )
			return false;

		if ( created.IsDecal )
		{
			created.Resource.PlayerNum = (byte)playerNum;

			if ( !CustomDecalWad.Validate( created.Buffer, resource.DownloadSize ) )
				return false;

			if ( (flags & IgnoreInit) == 0 )
			{
				if ( resource.DownloadSize < MinDecalSize || resource.DownloadSize > MaxDecalSize )
					return false;

				if ( CustomDecalWad.Init( created.Buffer, resource.DownloadSize ) is not { LumpCount: > 0 } wad )
					return false;

				created.Translated = true;
				created.UserData1 = 0;
				created.UserData2 = wad.LumpCount;

				if ( (flags & WadOnly) == 0 )
					created.Info = wad;
			}
		}

		list.Insert( 0, created );

		customization = created;

		return true;
	}
}
