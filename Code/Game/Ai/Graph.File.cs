using System;
using System.IO;
using System.Text;
using GoldSrc;

namespace HalfLife;

partial class Graph
{
	private const string GraphDirectory = "maps/graphs";
	private const int HeaderSize = 8396;
	private const int NodeSize = 88;
	public const int LinkSize = 24;
	private const int DistInfoSize = 16;

	private static string GraphFileName( string mapName ) => $"{GraphDirectory}/{mapName}.nod";

	public static string ReportFileName( string mapName ) => $"{GraphDirectory}/{mapName}.nrp";

	private static byte[] LoadGraphFile( string mapName )
	{
		var fileName = GraphFileName( mapName );

		return FileSystem.Data.FileExists( fileName ) ? FileSystem.Data.ReadAllBytes( fileName ).ToArray() : Server.LoadMountFile( fileName );
	}

	public bool CheckNodFile( string mapName ) => LoadGraphFile( mapName ) is not null;

	private static Vector3 ReadVector( BinaryReader reader ) => new( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );

	private static void Write( BinaryWriter writer, Vector3 vector )
	{
		writer.Write( vector.x );
		writer.Write( vector.y );
		writer.Write( vector.z );
	}

	private static void Read( BinaryReader reader, int[] values )
	{
		for ( var i = 0; i < values.Length; i++ )
			values[i] = reader.ReadInt32();
	}

	private static void Read( BinaryReader reader, int[,] values )
	{
		for ( var i = 0; i < values.GetLength( 0 ); i++ )
		{
			for ( var j = 0; j < values.GetLength( 1 ); j++ )
				values[i, j] = reader.ReadInt32();
		}
	}

	private static void Write( BinaryWriter writer, Array values )
	{
		foreach ( int value in values )
			writer.Write( value );
	}

	private void ReadHeader( BinaryReader reader )
	{
		GraphPresent = reader.ReadInt32() != 0;
		GraphPointersSet = reader.ReadInt32() != 0;
		RoutingComplete = reader.ReadInt32() != 0;

		reader.BaseStream.Position += 12;

		NodeCount = reader.ReadInt32();
		LinkCount = reader.ReadInt32();
		_routeInfoSize = reader.ReadInt32();

		reader.BaseStream.Position += 4;

		Read( reader, _rangeStart );
		Read( reader, _rangeEnd );

		_shortest = reader.ReadSingle();
		_nearest = reader.ReadInt32();

		Read( reader, _min );
		Read( reader, _max );
		Read( reader, _minBox );
		Read( reader, _maxBox );

		_checkedCounter = reader.ReadInt32();

		for ( var axis = 0; axis < 3; axis++ )
			_regionMin[axis] = reader.ReadSingle();

		for ( var axis = 0; axis < 3; axis++ )
			_regionMax[axis] = reader.ReadSingle();

		for ( var i = 0; i < CacheSize; i++ )
		{
			_cache[i].Origin = ReadVector( reader );
			_cache[i].Node = reader.ReadInt16();

			reader.BaseStream.Position += 2;
		}

		Read( reader, _hashPrimes );

		reader.BaseStream.Position += 4;

		_hashLinkCount = reader.ReadInt32();
		LastActiveIdleSearch = reader.ReadInt32();
		LastCoverSearch = reader.ReadInt32();
	}

	private void WriteHeader( BinaryWriter writer )
	{
		writer.Write( GraphPresent ? 1 : 0 );
		writer.Write( GraphPointersSet ? 1 : 0 );
		writer.Write( RoutingComplete ? 1 : 0 );
		writer.Write( new byte[12] );
		writer.Write( NodeCount );
		writer.Write( LinkCount );
		writer.Write( _routeInfoSize );
		writer.Write( 0 );

		Write( writer, _rangeStart );
		Write( writer, _rangeEnd );

		writer.Write( _shortest );
		writer.Write( _nearest );

		Write( writer, _min );
		Write( writer, _max );
		Write( writer, _minBox );
		Write( writer, _maxBox );

		writer.Write( _checkedCounter );

		foreach ( var value in _regionMin )
			writer.Write( value );

		foreach ( var value in _regionMax )
			writer.Write( value );

		foreach ( var entry in _cache )
		{
			Write( writer, entry.Origin );
			writer.Write( entry.Node );
			writer.Write( (short)0 );
		}

		Write( writer, _hashPrimes );

		writer.Write( 0 );
		writer.Write( _hashLinkCount );
		writer.Write( LastActiveIdleSearch );
		writer.Write( LastCoverSearch );
	}

	private static GraphNode ReadNode( BinaryReader reader )
	{
		var node = new GraphNode
		{
			Origin = ReadVector( reader ),
			OriginPeek = ReadVector( reader )
		};

		reader.Read( node.Region );
		reader.BaseStream.Position += 1;

		node.NodeInfo = reader.ReadInt32();
		node.NumLinks = reader.ReadInt32();
		node.FirstLink = reader.ReadInt32();

		Read( reader, node.NextBestNode );

		node.ClosestSoFar = reader.ReadSingle();
		node.PreviousNode = reader.ReadInt32();
		node.HintType = reader.ReadInt16();
		node.HintActivity = reader.ReadInt16();
		node.HintYaw = reader.ReadSingle();

		return node;
	}

	private static void Write( BinaryWriter writer, GraphNode node )
	{
		Write( writer, node.Origin );
		Write( writer, node.OriginPeek );

		writer.Write( node.Region );
		writer.Write( (byte)0 );
		writer.Write( node.NodeInfo );
		writer.Write( node.NumLinks );
		writer.Write( node.FirstLink );

		Write( writer, node.NextBestNode );

		writer.Write( node.ClosestSoFar );
		writer.Write( node.PreviousNode );
		writer.Write( node.HintType );
		writer.Write( node.HintActivity );
		writer.Write( node.HintYaw );
	}

	private static GraphLink ReadLink( BinaryReader reader, out bool linkEntitySaved )
	{
		var link = new GraphLink
		{
			SourceNode = reader.ReadInt32(),
			DestinationNode = reader.ReadInt32()
		};

		linkEntitySaved = reader.ReadInt32() != 0;

		var name = reader.ReadBytes( 4 );
		var end = Array.IndexOf( name, (byte)0 );

		link.LinkEntityModelName = Encoding.Latin1.GetString( name, 0, end < 0 ? name.Length : end );
		link.LinkInfo = reader.ReadInt32();
		link.Weight = reader.ReadSingle();

		return link;
	}

	private static void Write( BinaryWriter writer, in GraphLink link )
	{
		var name = new byte[4];

		Encoding.Latin1.GetBytes( link.LinkEntityModelName ?? "", name );

		writer.Write( link.SourceNode );
		writer.Write( link.DestinationNode );
		writer.Write( link.LinkEntity is null ? 0 : 1 );
		writer.Write( name );
		writer.Write( link.LinkInfo );
		writer.Write( link.Weight );
	}

	public bool LoadGraph( string mapName )
	{
		if ( LoadGraphFile( mapName ) is not { } file )
			return false;

		using var reader = new BinaryReader( new MemoryStream( file ) );

		var length = file.Length;

		bool Take( int size )
		{
			length -= size;

			return length >= 0;
		}

		if ( !Take( sizeof( int ) ) )
			return false;

		var version = reader.ReadInt32();

		if ( version != Version )
		{
			Util.Alert( AlertType.AiConsole, $"**ERROR** Graph version is {version}, expected {Version}\n" );
			return false;
		}

		if ( !Take( HeaderSize ) )
			return false;

		ReadHeader( reader );

		Nodes = null;
		LinkPool = null;
		_sortedBy = null;
		_checkedEvent = null;
		_routeInfo = null;
		_hashLinks = null;

		if ( !Take( NodeSize * NodeCount ) )
			return false;

		Nodes = new GraphNode[NodeCount];

		for ( var i = 0; i < NodeCount; i++ )
			Nodes[i] = ReadNode( reader );

		if ( !Take( LinkSize * LinkCount ) )
			return false;

		LinkPool = new GraphLink[LinkCount];
		_linkEntitySaved = new bool[LinkCount];

		for ( var i = 0; i < LinkCount; i++ )
			LinkPool[i] = ReadLink( reader, out _linkEntitySaved[i] );

		if ( !Take( DistInfoSize * NodeCount ) )
			return false;

		_sortedBy = new int[NodeCount, 3];
		_checkedEvent = new int[NodeCount];

		for ( var i = 0; i < NodeCount; i++ )
		{
			for ( var axis = 0; axis < 3; axis++ )
				_sortedBy[i, axis] = reader.ReadInt32();

			reader.BaseStream.Position += 4;
		}

		RoutingComplete = false;
		_checkedCounter = 0;

		if ( !Take( _routeInfoSize ) )
			return false;

		_routeInfo = new sbyte[_routeInfoSize];

		for ( var i = 0; i < _routeInfoSize; i++ )
			_routeInfo[i] = reader.ReadSByte();

		RoutingComplete = true;

		if ( !Take( sizeof( short ) * _hashLinkCount ) )
			return false;

		_hashLinks = new short[_hashLinkCount];

		for ( var i = 0; i < _hashLinkCount; i++ )
			_hashLinks[i] = reader.ReadInt16();

		GraphPresent = true;
		GraphPointersSet = false;

		if ( length != 0 )
			Util.Alert( AlertType.AiConsole, $"***WARNING***:Node graph was longer than expected by {length} bytes.!\n" );

		return true;
	}

	public bool SaveGraph( string mapName )
	{
		if ( !GraphPresent || !GraphPointersSet )
		{
			Util.Alert( AlertType.AiConsole, "Graph not ready!\n" );
			return false;
		}

		var fileName = GraphFileName( mapName );
		var stream = new MemoryStream();

		using var writer = new BinaryWriter( stream );

		writer.Write( Version );

		WriteHeader( writer );

		for ( var i = 0; i < NodeCount; i++ )
			Write( writer, Nodes[i] );

		for ( var i = 0; i < LinkCount; i++ )
			Write( writer, LinkPool[i] );

		for ( var i = 0; i < NodeCount; i++ )
		{
			for ( var axis = 0; axis < 3; axis++ )
				writer.Write( _sortedBy[i, axis] );

			writer.Write( _checkedEvent[i] );
		}

		if ( _routeInfo is not null && _routeInfoSize != 0 )
		{
			for ( var i = 0; i < _routeInfoSize; i++ )
				writer.Write( _routeInfo[i] );
		}

		if ( _hashLinks is not null && _hashLinkCount != 0 )
		{
			foreach ( var link in _hashLinks )
				writer.Write( link );
		}

		writer.Flush();

		FileSystem.Data.CreateDirectory( GraphDirectory );
		FileSystem.Data.WriteAllBytes( fileName, stream.ToArray() );

		Util.Alert( AlertType.AiConsole, $"Created: {fileName}\n" );

		return true;
	}

	public bool SetGraphPointers()
	{
		for ( var i = 0; i < LinkCount; i++ )
		{
			if ( !_linkEntitySaved[i] )
				continue;

			var name = LinkPool[i].LinkEntityModelName;
			var linkEntity = Server.Current.FindEntityByString( null, "model", name );

			if ( Util.IsNullEnt( linkEntity ) )
			{
				Util.Alert( AlertType.AiConsole, $"**Could not find model {name}\n" );
				LinkPool[i].LinkEntity = null;
				continue;
			}

			LinkPool[i].LinkEntity = (BaseEntity)linkEntity;

			linkEntity.EntityFlags |= EntityFlags.Graphed;
		}

		GraphPointersSet = true;

		return true;
	}
}
