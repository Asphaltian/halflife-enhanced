using System;
using System.IO;

namespace GoldSrc;

partial class Server
{
	private const int SaveFileHeader = 0x5641534a;
	private const int SaveGameVersion = 2;
	private const int SaveGameVersionNoDecals = 1;
	private const string SaveDirectory = "SAVE/";

	private static readonly (string Map, string Title)[] TitleComments =
	[
		("T0A0", "#T0A0TITLE"),
		("C0A0", "#C0A0TITLE"),
		("C1A0", "#C0A1TITLE"),
		("C1A1", "#C1A1TITLE"),
		("C1A2", "#C1A2TITLE"),
		("C1A3", "#C1A3TITLE"),
		("C1A4", "#C1A4TITLE"),
		("C2A1", "#C2A1TITLE"),
		("C2A2", "#C2A2TITLE"),
		("C2A3", "#C2A3TITLE"),
		("C2A4D", "#C2A4TITLE2"),
		("C2A4E", "#C2A4TITLE2"),
		("C2A4F", "#C2A4TITLE2"),
		("C2A4G", "#C2A4TITLE2"),
		("C2A4", "#C2A4TITLE1"),
		("C2A5", "#C2A5TITLE"),
		("C3A1", "#C3A1TITLE"),
		("C3A2", "#C3A2TITLE"),
		("C4A1A", "#C4A1ATITLE"),
		("C4A1B", "#C4A1ATITLE"),
		("C4A1C", "#C4A1ATITLE"),
		("C4A1D", "#C4A1ATITLE"),
		("C4A1E", "#C4A1ATITLE"),
		("C4A1", "#C4A1TITLE"),
		("C4A2", "#C4A2TITLE"),
		("C4A3", "#C4A3TITLE"),
		("C5A1", "#C5TITLE"),
		("OFBOOT", "#OF_BOOT0TITLE"),
		("OF0A", "#OF1A1TITLE"),
		("OF1A1", "#OF1A3TITLE"),
		("OF1A2", "#OF1A3TITLE"),
		("OF1A3", "#OF1A3TITLE"),
		("OF1A4", "#OF1A3TITLE"),
		("OF1A", "#OF1A5TITLE"),
		("OF2A1", "#OF2A1TITLE"),
		("OF2A2", "#OF2A1TITLE"),
		("OF2A3", "#OF2A1TITLE"),
		("OF2A", "#OF2A4TITLE"),
		("OF3A1", "#OF3A1TITLE"),
		("OF3A2", "#OF3A1TITLE"),
		("OF3A", "#OF3A3TITLE"),
		("OF4A1", "#OF4A1TITLE"),
		("OF4A2", "#OF4A1TITLE"),
		("OF4A3", "#OF4A1TITLE"),
		("OF4A", "#OF4A4TITLE"),
		("OF5A", "#OF5A1TITLE"),
		("OF6A1", "#OF6A1TITLE"),
		("OF6A2", "#OF6A1TITLE"),
		("OF6A3", "#OF6A1TITLE"),
		("OF6A4b", "#OF6A4TITLE"),
		("OF6A4", "#OF6A1TITLE"),
		("OF6A5", "#OF6A4TITLE"),
		("OF6A", "#OF6A4TITLE"),
		("OF7A", "#OF7A0TITLE"),
		("ba_tram", "#BA_TRAMTITLE"),
		("ba_security", "#BA_SECURITYTITLE"),
		("ba_main", "#BA_SECURITYTITLE"),
		("ba_elevator", "#BA_SECURITYTITLE"),
		("ba_canal", "#BA_CANALSTITLE"),
		("ba_yard", "#BA_YARDTITLE"),
		("ba_xen", "#BA_XENTITLE"),
		("ba_hazard", "#BA_HAZARD"),
		("ba_power", "#BA_POWERTITLE"),
		("ba_teleport1", "#BA_YARDTITLE"),
		("ba_teleport", "#BA_TELEPORTTITLE"),
		("ba_outro", "#BA_OUTRO"),
		("contamination", "#contamination"),
		("crossfire", "#crossfire"),
		("datacore", "#datacore"),
		("disposal", "#disposal"),
		("doublecross", "#doublecross"),
		("frenzy", "#frenzy"),
		("gasworks", "#gasworks"),
		("lambda_bunker", "#lambda_bunker"),
		("pool_party", "#pool_party"),
		("rapidcore", "#rapidcore"),
		("rocket_frenzy", "#rocket_frenzy"),
		("rustmill", "#rustmill"),
		("snark_pit", "#snark_pit"),
		("stalkyard", "#stalkyard"),
		("subtransit", "#subtransit"),
		("undertow", "#undertow"),
		("xen_dm", "#xen_dm"),
		("bounce", "#bounce")
	];

	public bool IsValidSave()
	{
		if ( !Active )
		{
			Con.Printf( "Not playing a local game.\n" );
			return false;
		}

		if ( MaxClients != 1 )
		{
			Con.Printf( "Can't save multiplayer games.\n" );
			return false;
		}

		if ( HasPendingChangeLevel || Client.Current is not { InLevel: true } )
		{
			Con.Printf( "Can't save during transition.\n" );
			return false;
		}

		if ( Client.Current is { Intermission: true } )
		{
			Con.Printf( "Can't save in intermission.\n" );
			return false;
		}

		if ( _clients[0].Active && GetEdict( 1 ).Health <= 0f )
		{
			Con.Printf( "Can't savegame with a dead player\n" );
			return false;
		}

		return true;
	}

	public string SaveGameComment()
	{
		var mapName = Globals.MapName;
		string title = null;

		foreach ( var (map, comment) in TitleComments )
		{
			if ( mapName.StartsWith( map, StringComparison.OrdinalIgnoreCase ) )
			{
				title = comment;
				break;
			}
		}

		title ??= mapName;

		var description = title.Length > 64 ? title[..64] : title.PadRight( 64 );

		return $"{description} {(int)(Time / 60.0):00}:{(int)(Time % 60.0):00}";
	}

	public bool SaveGame( string saveName, string comment )
	{
		if ( SaveGamestate() is null )
			return false;

		Globals.SaveData = null;

		var header = new GameHeader
		{
			MapName = Name,
			Comment = comment,
			MapCount = _saveDirectory.Count
		};

		var globalState = new List<SaveBlock>();
		GameDll.SaveGlobalState( globalState );

		var name = $"{SaveDirectory}{saveName}.sav";

		Con.DPrintf( $"Saving game to {name}...\n" );

		FileSystem.Data.CreateDirectory( SaveDirectory );

		if ( !saveName.Equals( "quick", StringComparison.OrdinalIgnoreCase ) || !saveName.Equals( "autosave", StringComparison.OrdinalIgnoreCase ) )
		{
			var oldName = $"{SaveDirectory}{saveName}01.sav";

			if ( FileSystem.Data.FileExists( oldName ) )
				FileSystem.Data.DeleteFile( oldName );

			if ( FileSystem.Data.FileExists( name ) )
			{
				FileSystem.Data.WriteAllBytes( oldName, FileSystem.Data.ReadAllBytes( name ).ToArray() );
				FileSystem.Data.DeleteFile( name );
			}
		}

		using var stream = FileSystem.Data.OpenWrite( name );
		using var writer = new BinaryWriter( stream );

		writer.Write( SaveFileHeader );
		writer.Write( SaveGameVersion );
		writer.Write( DateTime.UtcNow.Ticks );
		writer.Write( header.MapName );
		writer.Write( header.Comment );
		writer.Write( header.MapCount );

		WriteBlocks( writer, globalState );

		foreach ( var (level, data) in _saveDirectory )
		{
			writer.Write( level );
			WriteSaveData( writer, data );
		}

		return true;
	}

	public GameHeader LoadGame( string saveName )
	{
		var name = $"{SaveDirectory}{saveName}.sav";

		Con.Printf( $"Loading game from {name}...\n" );

		if ( !FileSystem.Data.FileExists( name ) )
			return null;

		using var stream = FileSystem.Data.OpenRead( name );
		using var reader = new BinaryReader( stream );

		if ( reader.ReadInt32() != SaveFileHeader )
			return null;

		var version = reader.ReadInt32();

		if ( version is not (SaveGameVersion or SaveGameVersionNoDecals) )
			return null;

		reader.ReadInt64();

		_saveDirectory.Clear();

		var header = new GameHeader
		{
			MapName = reader.ReadString(),
			Comment = reader.ReadString(),
			MapCount = reader.ReadInt32()
		};

		GameDll.RestoreGlobalState( ReadBlocks( reader ) );

		for ( var i = 0; i < header.MapCount; i++ )
		{
			var level = reader.ReadString();
			_saveDirectory[level] = ReadSaveData( reader, version );
		}

		return header;
	}

	public string LatestSaveGame()
	{
		string latest = null;
		var latestTime = 0L;

		foreach ( var file in FileSystem.Data.FindFile( SaveDirectory, "*.sav" ) )
		{
			if ( file.Equals( "HLSave.sav", StringComparison.OrdinalIgnoreCase ) )
				continue;

			var time = SaveGameTime( $"{SaveDirectory}{file}" );

			if ( time > 0 && (latest is null || time > latestTime) )
			{
				latest = file;
				latestTime = time;
			}
		}

		return latest is null ? null : Path.GetFileNameWithoutExtension( latest );
	}

	public static IEnumerable<string> SaveGameFiles => FileSystem.Data.DirectoryExists( SaveDirectory ) ? FileSystem.Data.FindFile( SaveDirectory, "*.sav" ) : [];

	public static bool SaveGameExists( string file ) => FileSystem.Data.FileExists( $"{SaveDirectory}{file}" );

	public static bool SaveReadNameAndComment( string file, out string mapName, out string comment, out DateTime time )
	{
		mapName = null;
		comment = null;
		time = default;

		using var stream = FileSystem.Data.OpenRead( $"{SaveDirectory}{file}" );
		using var reader = new BinaryReader( stream );

		if ( stream.Length < 16 || reader.ReadInt32() != SaveFileHeader || reader.ReadInt32() is not (SaveGameVersion or SaveGameVersionNoDecals) )
			return false;

		time = new DateTime( reader.ReadInt64(), DateTimeKind.Utc );
		mapName = reader.ReadString();
		comment = reader.ReadString();

		return mapName.Length > 0 && comment.Length > 0;
	}

	private static long SaveGameTime( string name )
	{
		using var stream = FileSystem.Data.OpenRead( name );
		using var reader = new BinaryReader( stream );

		if ( stream.Length < 16 || reader.ReadInt32() != SaveFileHeader || reader.ReadInt32() is not (SaveGameVersion or SaveGameVersionNoDecals) )
			return 0;

		return reader.ReadInt64();
	}

	private static void WriteSaveData( BinaryWriter writer, SaveRestoreData data )
	{
		writer.Write( data.CurrentMapName );
		writer.Write( data.Time );
		writer.Write( data.LightStyles.Length );

		foreach ( var style in data.LightStyles )
			writer.Write( style ?? string.Empty );

		writer.Write( data.Skill );
		writer.Write( data.SkyName );
		writer.Write( data.Sky.Length );

		foreach ( var value in data.Sky )
			writer.Write( value );

		writer.Write( data.LevelList.Count );

		foreach ( var level in data.LevelList )
		{
			writer.Write( level.MapName );
			writer.Write( level.LandmarkName ?? string.Empty );
			WriteVector( writer, level.LandmarkOrigin );
		}

		writer.Write( data.Table.Length );

		foreach ( var entry in data.Table )
		{
			writer.Write( entry.Id );
			writer.Write( entry.Flags );
			writer.Write( entry.ClassName ?? string.Empty );
			WriteBlocks( writer, entry.Blocks );
		}

		writer.Write( data.Decals?.Count ?? -1 );

		foreach ( var decal in data.Decals ?? [] )
		{
			writer.Write( decal.Name );
			writer.Write( decal.EntityIndex );
			writer.Write( decal.Depth );
			writer.Write( decal.Flags );
			WriteVector( writer, decal.Position );
		}
	}

	private static SaveRestoreData ReadSaveData( BinaryReader reader, int version )
	{
		var data = new SaveRestoreData
		{
			CurrentMapName = reader.ReadString(),
			Time = reader.ReadSingle()
		};

		data.LightStyles = new string[reader.ReadInt32()];

		for ( var i = 0; i < data.LightStyles.Length; i++ )
			data.LightStyles[i] = reader.ReadString();

		data.Skill = reader.ReadInt32();
		data.SkyName = reader.ReadString();
		data.Sky = new float[reader.ReadInt32()];

		for ( var i = 0; i < data.Sky.Length; i++ )
			data.Sky[i] = reader.ReadSingle();

		var levelCount = reader.ReadInt32();

		for ( var i = 0; i < levelCount; i++ )
		{
			data.LevelList.Add( new LevelList
			{
				MapName = reader.ReadString(),
				LandmarkName = reader.ReadString(),
				LandmarkOrigin = ReadVector( reader )
			} );
		}

		data.Table = new EntityTable[reader.ReadInt32()];

		for ( var i = 0; i < data.Table.Length; i++ )
		{
			var entry = new EntityTable
			{
				Id = reader.ReadInt32(),
				Flags = reader.ReadInt32(),
				ClassName = reader.ReadString()
			};

			if ( entry.ClassName.Length == 0 )
				entry.ClassName = null;

			entry.Blocks = ReadBlocks( reader );
			data.Table[i] = entry;
		}

		if ( version == SaveGameVersionNoDecals )
			return data;

		var decalCount = reader.ReadInt32();

		if ( decalCount < 0 )
			return data;

		data.Decals = [];

		for ( var i = 0; i < decalCount; i++ )
			data.Decals.Add( new DecalEntry( reader.ReadString(), reader.ReadInt16(), reader.ReadByte(), reader.ReadByte(), ReadVector( reader ) ) );

		return data;
	}

	private static void WriteBlocks( BinaryWriter writer, List<SaveBlock> blocks )
	{
		writer.Write( blocks?.Count ?? -1 );

		if ( blocks is null )
			return;

		foreach ( var block in blocks )
		{
			writer.Write( block.Name );
			writer.Write( block.Fields.Count );

			foreach ( var (name, value) in block.Fields )
			{
				writer.Write( name );
				WriteValue( writer, value );
			}
		}
	}

	private static List<SaveBlock> ReadBlocks( BinaryReader reader )
	{
		var count = reader.ReadInt32();
		if ( count < 0 )
			return null;

		var blocks = new List<SaveBlock>( count );

		for ( var i = 0; i < count; i++ )
		{
			var block = new SaveBlock( reader.ReadString() );
			var fieldCount = reader.ReadInt32();

			for ( var field = 0; field < fieldCount; field++ )
			{
				var name = reader.ReadString();
				block.Fields[name] = ReadValue( reader );
			}

			blocks.Add( block );
		}

		return blocks;
	}

	private static void WriteValue( BinaryWriter writer, object value )
	{
		switch ( value )
		{
			case null:
				writer.Write( (byte)0 );
				break;

			case float number:
				writer.Write( (byte)1 );
				writer.Write( number );
				break;

			case int number:
				writer.Write( (byte)2 );
				writer.Write( number );
				break;

			case short number:
				writer.Write( (byte)3 );
				writer.Write( number );
				break;

			case byte number:
				writer.Write( (byte)4 );
				writer.Write( number );
				break;

			case bool flag:
				writer.Write( (byte)5 );
				writer.Write( flag );
				break;

			case string text:
				writer.Write( (byte)6 );
				writer.Write( text );
				break;

			case Vector3 vector:
				writer.Write( (byte)7 );
				WriteVector( writer, vector );
				break;

			case long number:
				writer.Write( (byte)8 );
				writer.Write( number );
				break;

			case object[] array:
				writer.Write( (byte)9 );
				writer.Write( array.Length );

				foreach ( var element in array )
					WriteValue( writer, element );

				break;

			default:
				throw new InvalidOperationException( $"Bad field type {value.GetType()}" );
		}
	}

	private static object ReadValue( BinaryReader reader )
	{
		switch ( reader.ReadByte() )
		{
			case 1:
				return reader.ReadSingle();

			case 2:
				return reader.ReadInt32();

			case 3:
				return reader.ReadInt16();

			case 4:
				return reader.ReadByte();

			case 5:
				return reader.ReadBoolean();

			case 6:
				return reader.ReadString();

			case 7:
				return ReadVector( reader );

			case 8:
				return reader.ReadInt64();

			case 9:
				var array = new object[reader.ReadInt32()];

				for ( var i = 0; i < array.Length; i++ )
					array[i] = ReadValue( reader );

				return array;

			default:
				return null;
		}
	}

	private static void WriteVector( BinaryWriter writer, Vector3 vector )
	{
		writer.Write( vector.x );
		writer.Write( vector.y );
		writer.Write( vector.z );
	}

	private static Vector3 ReadVector( BinaryReader reader )
	{
		return new Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
	}
}
