using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace GoldSrc;

public static class Hpak
{
	public const string Custom = "custom.hpk";

	private const string Signature = "HPAK";
	private const string Extension = ".hpk";
	private const int Version = 1;
	private const int HeaderSize = 12;
	private const int NameSize = 64;
	private const int ResourceSize = 0x88;
	private const int EntrySize = 0x90;
	private const int MaxEntries = 0x8000;
	private const int MinLumpSize = 0x400;
	private const int MaxLumpSize = 0x20000;

	private sealed record QueuedLump( string PakName, Resource Resource, byte[] Data );

	private sealed class Entry
	{
		public Resource Resource;
		public int Offset;
		public int Size;
	}

	[ConVar( "hpk_maxsize", ConVarFlags.Saved )]
	private static float MaxSize { get; set; } = 4f;

	private static List<QueuedLump> _queue = [];

	public static string Md5Print( byte[] hash )
	{
		var text = new StringBuilder( hash.Length * 2 );

		foreach ( var value in hash )
			text.Append( value.ToString( "x2" ) );

		return text.ToString();
	}

	public static byte[] HexConvert( string text, int length )
	{
		var result = new byte[length / 2];

		for ( var i = 0; i < length; i += 2 )
			result[i / 2] = (byte)((HexDigit( text, i, 0 ) << 4) | HexDigit( text, i + 1, '0' ));

		return result;
	}

	private static int HexDigit( string text, int index, int invalid )
	{
		return (index < text.Length ? text[index] : '\0') switch
		{
			>= '0' and <= '9' and var digit => digit - '0',
			>= 'A' and <= 'F' and var digit => digit - 'A' + 10,
			>= 'a' and <= 'f' and var digit => digit - 'a' + 10,
			_ => invalid
		};
	}

	private static string FileName( string pakName ) => Common.DefaultExtension( pakName, Extension );

	private static Resource ReadResource( byte[] data, int offset )
	{
		var length = Array.IndexOf( data, (byte)0, offset, NameSize ) - offset;

		var resource = new Resource
		{
			Name = Encoding.Latin1.GetString( data, offset, length < 0 ? NameSize : length ),
			Type = (ResourceKind)BitConverter.ToInt32( data, offset + 0x40 ),
			Index = BitConverter.ToInt32( data, offset + 0x44 ),
			DownloadSize = BitConverter.ToInt32( data, offset + 0x48 ),
			Flags = data[offset + 0x4c],
			PlayerNum = data[offset + 0x5d]
		};

		Array.Copy( data, offset + 0x4d, resource.Md5, 0, Resource.Md5Size );
		Array.Copy( data, offset + 0x5e, resource.Reserved, 0, Resource.ReservedSize );

		return resource;
	}

	private static void WriteEntry( BinaryWriter writer, Entry entry )
	{
		var data = new byte[EntrySize];
		var resource = entry.Resource;

		Encoding.Latin1.GetBytes( resource.Name, 0, Math.Min( resource.Name.Length, Resource.MaxNameLength ), data, 0 );
		BitConverter.TryWriteBytes( data.AsSpan( 0x40 ), (int)resource.Type );
		BitConverter.TryWriteBytes( data.AsSpan( 0x44 ), resource.Index );
		BitConverter.TryWriteBytes( data.AsSpan( 0x48 ), resource.DownloadSize );

		data[0x4c] = (byte)resource.Flags;
		data[0x5d] = (byte)resource.PlayerNum;

		Array.Copy( resource.Md5, 0, data, 0x4d, Resource.Md5Size );
		Array.Copy( resource.Reserved, 0, data, 0x5e, Resource.ReservedSize );

		BitConverter.TryWriteBytes( data.AsSpan( ResourceSize ), entry.Offset );
		BitConverter.TryWriteBytes( data.AsSpan( ResourceSize + 4 ), entry.Size );

		writer.Write( data );
	}

	private static void WritePak( string fileName, byte[] lumps, int lumpsSize, List<Entry> entries )
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter( stream );

		writer.Write( Encoding.Latin1.GetBytes( Signature ) );
		writer.Write( Version );
		writer.Write( lumpsSize );
		writer.Write( lumps, HeaderSize, lumpsSize - HeaderSize );
		writer.Write( entries.Count );

		foreach ( var entry in entries )
			WriteEntry( writer, entry );

		writer.Flush();

		Server.WriteFile( fileName, stream.ToArray() );
	}

	private static List<Entry> ReadDirectory( byte[] file, int count )
	{
		var offset = BitConverter.ToInt32( file, 8 ) + 4;
		var entries = new List<Entry>( count );

		for ( var i = 0; i < count; i++, offset += EntrySize )
		{
			entries.Add( new Entry
			{
				Resource = ReadResource( file, offset ),
				Offset = BitConverter.ToInt32( file, offset + ResourceSize ),
				Size = BitConverter.ToInt32( file, offset + ResourceSize + 4 )
			} );
		}

		return entries;
	}

	private static bool IsPak( byte[] file ) => Encoding.Latin1.GetString( file, 0, 4 ) == Signature;

	private static Entry FindResource( List<Entry> entries, byte[] hash )
	{
		foreach ( var entry in entries )
		{
			if ( entry.Resource.Md5.AsSpan().SequenceEqual( hash ) )
				return entry;
		}

		return null;
	}

	private static QueuedLump FindQueued( string pakName, byte[] hash )
	{
		foreach ( var lump in _queue )
		{
			if ( lump.PakName.Equals( pakName, StringComparison.OrdinalIgnoreCase ) && lump.Resource.Md5.AsSpan().SequenceEqual( hash ) )
				return lump;
		}

		return null;
	}

	private static List<Entry> OpenDirectory( string fileName, byte[] file )
	{
		if ( !IsPak( file ) )
		{
			Log.Info( $"{fileName} is not an HPAK file\n" );
			return null;
		}

		if ( BitConverter.ToInt32( file, 4 ) != Version )
		{
			Log.Info( "HPAK_List:  version mismatch\n" );
			return null;
		}

		var count = BitConverter.ToInt32( file, BitConverter.ToInt32( file, 8 ) );

		if ( count < 1 || count > MaxEntries )
		{
			Log.Info( $"ERROR: HPAK had bogus # of directory entries:  {count}\n" );
			return null;
		}

		return ReadDirectory( file, count );
	}

	public static void AddLump( bool queue, string pakName, Resource resource, byte[] data )
	{
		var size = resource.DownloadSize;

		if ( size < MinLumpSize || size > MaxLumpSize )
		{
			Log.Info( $"HPAK_AddLump called with bogus lump, size:  {size}\n" );
			return;
		}

		var hash = MD5.HashData( data.AsSpan( 0, size ) );

		if ( !resource.Md5.AsSpan().SequenceEqual( hash ) )
		{
			Log.Info( "HPAK_AddLump called with bogus lump, md5 mismatch\n" );
			Log.Info( $"Purported:  {Md5Print( resource.Md5 )}\n" );
			Log.Info( $"Actual   :  {Md5Print( hash )}\n" );
			Log.Info( "Ignoring lump addition\n" );

			return;
		}

		if ( queue )
		{
			_queue.Insert( 0, new QueuedLump( pakName, resource.Clone(), data.AsSpan( 0, size ).ToArray() ) );
			return;
		}

		var fileName = FileName( pakName );

		if ( Server.ReadFile( fileName ) is not { } file )
		{
			CreatePak( fileName, resource, data );
			return;
		}

		if ( BitConverter.ToInt32( file, 4 ) != Version )
		{
			Log.Info( "Invalid .hpk version in HPAK_AddLump\n" );
			return;
		}

		var directory = BitConverter.ToInt32( file, 8 );
		var count = BitConverter.ToInt32( file, directory );

		if ( (uint)count > MaxEntries )
		{
			Log.Info( $"ERROR: .hpk had bogus # of directory entries:  {count}\n" );
			return;
		}

		var entries = ReadDirectory( file, count );

		if ( FindResource( entries, resource.Md5 ) is not null )
			return;

		var index = entries.FindIndex( entry => resource.Md5.AsSpan().SequenceCompareTo( entry.Resource.Md5 ) < 0 );

		entries.Insert( index < 0 ? entries.Count : index, new Entry { Resource = resource.Clone(), Offset = directory, Size = size } );

		var lumps = new byte[directory + size];

		Array.Copy( file, lumps, directory );
		Array.Copy( data, 0, lumps, directory, size );

		WritePak( fileName, lumps, lumps.Length, entries );
	}

	private static void CreatePak( string fileName, Resource resource, byte[] data )
	{
		Log.Info( $"Creating HPAK {fileName}.\n" );

		var size = resource.DownloadSize;
		var lumps = new byte[HeaderSize + size];

		Array.Copy( data, 0, lumps, HeaderSize, size );

		WritePak( fileName, lumps, lumps.Length, [new Entry { Resource = resource.Clone(), Offset = HeaderSize, Size = size }] );
	}

	public static bool GetDataPointer( string pakName, Resource resource ) => GetDataPointer( pakName, resource, false, out _ );

	public static bool GetDataPointer( string pakName, Resource resource, out byte[] data ) => GetDataPointer( pakName, resource, true, out data );

	private static bool GetDataPointer( string pakName, Resource resource, bool read, out byte[] data )
	{
		data = null;

		if ( FindQueued( pakName, resource.Md5 ) is { } queued )
		{
			if ( read )
				data = [.. queued.Data];

			return true;
		}

		var fileName = FileName( pakName );

		if ( Server.ReadFile( fileName ) is not { } file || OpenDirectory( fileName, file ) is not { } entries )
			return false;

		if ( FindResource( entries, resource.Md5 ) is not { } entry )
			return false;

		if ( read && entry.Size > 0 )
			data = file.AsSpan( entry.Offset, entry.Size ).ToArray();

		return true;
	}

	public static bool ResourceForHash( string pakName, byte[] hash, out Resource resource )
	{
		resource = null;

		if ( FindQueued( pakName, hash ) is { } queued )
		{
			resource = queued.Resource.Clone();
			return true;
		}

		var fileName = FileName( pakName );

		if ( Server.ReadFile( fileName ) is not { } file )
		{
			Log.Info( $"ERROR: couldn't open {fileName}.\n" );
			return false;
		}

		if ( OpenDirectory( fileName, file ) is not { } entries || FindResource( entries, hash ) is not { } entry )
			return false;

		resource = entry.Resource.Clone();

		return true;
	}

	public static void RemoveLump( string pakName, Resource resource )
	{
		if ( string.IsNullOrEmpty( pakName ) || resource is null )
		{
			Log.Info( "HPAK_RemoveLump:  Invalid arguments\n" );
			return;
		}

		FlushHostQueue();

		var fileName = FileName( pakName );

		if ( Server.ReadFile( fileName ) is not { } file )
		{
			Log.Info( $"Error:  couldn't open HPAK file {fileName} for removal.\n" );
			return;
		}

		if ( !IsPak( file ) )
		{
			Log.Info( $"{fileName} is not an HPAK file\n" );
			return;
		}

		if ( BitConverter.ToInt32( file, 4 ) != Version )
		{
			Log.Info( "ERROR: HPAK version outdated\n" );
			return;
		}

		var count = BitConverter.ToInt32( file, BitConverter.ToInt32( file, 8 ) );

		if ( count < 1 || count > MaxEntries )
		{
			Log.Info( $"ERROR: HPAK had bogus # of directory entries:  {count}\n" );
			return;
		}

		if ( count == 1 )
		{
			Log.Info( $"Removing final lump from HPAK, deleting HPAK:\n  {fileName}\n" );

			Server.RemoveFile( fileName );
			return;
		}

		var entries = ReadDirectory( file, count );

		if ( FindResource( entries, resource.Md5 ) is null )
		{
			Log.Info( $"ERROR: HPAK doesn't contain specified lump:  {resource.Name}\n" );
			return;
		}

		Log.Info( $"Removing {resource.Name} from HPAK {fileName}.\n" );

		using var lumps = new MemoryStream();

		lumps.Write( file, 0, HeaderSize );

		var kept = new List<Entry>( count - 1 );

		foreach ( var entry in entries )
		{
			if ( entry.Resource.Md5.AsSpan().SequenceEqual( resource.Md5 ) )
				continue;

			kept.Add( new Entry { Resource = entry.Resource, Offset = (int)lumps.Position, Size = entry.Size } );

			lumps.Write( file, entry.Offset, entry.Size );
		}

		WritePak( fileName, lumps.ToArray(), (int)lumps.Length, kept );
	}

	public static void FlushHostQueue()
	{
		while ( _queue.Count > 0 )
		{
			var lump = _queue[0];

			_queue.RemoveAt( 0 );

			AddLump( false, lump.PakName, lump.Resource, lump.Data );
		}
	}

	public static void CheckSize( string pakName )
	{
		if ( MaxSize == 0f || pakName is null )
			return;

		if ( MaxSize < 0f )
		{
			Log.Info( "hpk_maxsize < 0, setting to 0\n" );

			Cvar.Set( "hpk_maxsize", "0" );
			return;
		}

		var fileName = FileName( pakName );
		var size = (float)Math.Max( Server.FileSize( fileName ), 0 );

		if ( size < MaxSize * 1000000f )
			return;

		Log.Info( $"Server: Size of {fileName} > {MaxSize:F6} MB, deleting.\n" );
		Server.LogPrintf( $"Server: Size of {fileName} > {MaxSize:F6} MB, deleting.\n" );

		Server.RemoveFile( fileName );
	}
}
