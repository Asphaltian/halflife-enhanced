using System.IO;

namespace GoldSrc;

public static class DecalWad
{
	private const int MaxDecals = 512;

	private const int MaxNameLength = 15;

	private const string FileName = "decals.wad";

	private static (string Name, int Width, int Height)[] _lumps;
	private static int[] _lumpSizes;

	public static (string Name, int Width, int Height)[] Textures
	{
		get
		{
			Load();

			return _lumps;
		}
	}

	public static int[] Sizes
	{
		get
		{
			Load();

			return _lumpSizes;
		}
	}

	private static void Load()
	{
		if ( _lumps is not null )
			return;

		if ( Server.LoadMountFile( FileName ) is not { } data )
			throw new InvalidDataException( $"Couldn't find '{FileName}' in \"DEFAULTGAME\" search path\n" );

		using var reader = new BinaryReader( new MemoryStream( data ) );

		reader.ReadInt32();

		var count = reader.ReadInt32();
		var directory = reader.ReadInt32();

		if ( count > MaxDecals )
			throw new InvalidDataException( $"Too many decals: {count} / {MaxDecals}\n" );

		var textures = new (string, int, int)[count];
		var sizes = new int[count];

		for ( var i = 0; i < count; i++ )
		{
			reader.BaseStream.Seek( directory + (i * 32), SeekOrigin.Begin );

			var offset = reader.ReadInt32();

			reader.ReadInt32();

			sizes[i] = reader.ReadInt32();

			reader.BaseStream.Seek( 4, SeekOrigin.Current );

			var name = CleanupName( reader.ReadBytes( 16 ) );

			reader.BaseStream.Seek( offset + 16, SeekOrigin.Begin );

			textures[i] = (name, reader.ReadInt32(), reader.ReadInt32());
		}

		_lumps = textures;
		_lumpSizes = sizes;
	}

	private static string CleanupName( byte[] lump )
	{
		var name = new char[MaxNameLength];
		var length = 0;

		for ( ; length < MaxNameLength && lump[length] != 0; length++ )
			name[length] = lump[length] is >= (byte)'A' and <= (byte)'Z' ? (char)(lump[length] + ' ') : (char)lump[length];

		return new string( name, 0, length );
	}
}
