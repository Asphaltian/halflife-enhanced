using System;
using System.IO;
using System.Text;

namespace GoldSrc.Bsp;

public sealed partial class File
{
	public const int Version = 30;
	public const int QuakeVersion = 29;

	public string Entities { get; }
	public Plane[] Planes { get; }
	public Vector3[] Vertexes { get; }
	public byte[] Visibility { get; }
	public Node[] Nodes { get; }
	public TexInfo[] TexInfos { get; }
	public Face[] Faces { get; }
	public byte[] Lighting { get; }
	public ClipNode[] ClipNodes { get; }
	public Leaf[] Leafs { get; }
	public ushort[] MarkSurfaces { get; }
	public Edge[] Edges { get; }
	public int[] SurfEdges { get; }
	public Model[] Models { get; }
	public string[] TextureNames { get; }
	public (int Width, int Height)[] TextureSizes { get; private set; }

	private readonly byte[] _data;
	private readonly Lump[] _lumps = new Lump[(int)LumpType.Count];

	public File( byte[] data )
	{
		_data = data;

		using var reader = new BinaryReader( new MemoryStream( data ) );

		var version = reader.ReadInt32();
		if ( version is not (Version or QuakeVersion) )
			throw new InvalidDataException( $"BSP has wrong version number ({version} should be {Version})" );

		for ( var i = 0; i < _lumps.Length; i++ )
			_lumps[i] = new Lump( reader );

		Planes = ReadLump( reader, LumpType.Planes, Plane.Size, r => new Plane( r ) );
		Vertexes = ReadLump( reader, LumpType.Vertexes, 12, r => r.ReadVector3() );
		Nodes = ReadLump( reader, LumpType.Nodes, Node.Size, r => new Node( r ) );
		TexInfos = ReadLump( reader, LumpType.TexInfo, TexInfo.Size, r => new TexInfo( r ) );
		Faces = ReadLump( reader, LumpType.Faces, Face.Size, r => new Face( r ) );
		ClipNodes = ReadLump( reader, LumpType.ClipNodes, ClipNode.Size, r => new ClipNode( r ) );
		Leafs = ReadLump( reader, LumpType.Leafs, Leaf.Size, r => new Leaf( r ) );
		MarkSurfaces = ReadLump( reader, LumpType.MarkSurfaces, sizeof( ushort ), r => r.ReadUInt16() );
		Edges = ReadLump( reader, LumpType.Edges, Edge.Size, r => new Edge( r ) );
		SurfEdges = ReadLump( reader, LumpType.SurfEdges, sizeof( int ), r => r.ReadInt32() );
		Models = ReadLump( reader, LumpType.Models, Model.Size, r => new Model( r ) );

		TextureNames = ReadTextureNames();
		Visibility = GetLump( LumpType.Visibility ).ToArray();
		Lighting = GetLump( LumpType.Lighting ).ToArray();
		Entities = Encoding.Latin1.GetString( GetLump( LumpType.Entities ) ).TrimEnd( '\0' );

		LoadModelData();
	}

	public string GetTextureName( in Face face )
	{
		var mipTex = TexInfos[face.TexInfo].MipTex;
		return mipTex >= 0 && mipTex < TextureNames.Length ? TextureNames[mipTex] : null;
	}

	public byte[] GetTexturePalette( int index )
	{
		var lump = GetLump( LumpType.Textures );
		var offset = BitConverter.ToInt32( lump[(sizeof( int ) * (index + 1))..] );

		return offset < 0 ? null : MipTexPalette( lump[offset..] );
	}

	public static byte[] MipTexPalette( ReadOnlySpan<byte> mipTex )
	{
		const int headerSize = 40;

		if ( mipTex.Length < headerSize || BitConverter.ToInt32( mipTex[24..] ) == 0 )
			return null;

		var width = BitConverter.ToInt32( mipTex[16..] );
		var height = BitConverter.ToInt32( mipTex[20..] );
		var pixels = (int)((uint)(width * height) >> 6) * 85;

		if ( headerSize + pixels + sizeof( ushort ) > mipTex.Length )
			return null;

		var size = BitConverter.ToUInt16( mipTex[(headerSize + pixels)..] ) * 3;
		var start = headerSize + pixels + sizeof( ushort );

		return start + size > mipTex.Length ? null : mipTex.Slice( start, size ).ToArray();
	}

	public Vector3 GetVertex( in Face face, int index )
	{
		var surfEdge = SurfEdges[face.FirstEdge + index];
		return Vertexes[surfEdge >= 0 ? Edges[surfEdge].V0 : Edges[-surfEdge].V1];
	}

	public (int Width, int Height) GetTextureSize( in Face face )
	{
		var mipTex = TexInfos[face.TexInfo].MipTex;
		return mipTex >= 0 && mipTex < TextureSizes.Length ? TextureSizes[mipTex] : default;
	}

	private string[] ReadTextureNames()
	{
		const int nameLength = 16;

		var lump = GetLump( LumpType.Textures );
		if ( lump.IsEmpty )
		{
			TextureSizes = [];
			return [];
		}

		var names = new string[BitConverter.ToInt32( lump )];

		TextureSizes = new (int, int)[names.Length];

		for ( var i = 0; i < names.Length; i++ )
		{
			var offset = BitConverter.ToInt32( lump[(sizeof( int ) * (i + 1))..] );
			if ( offset < 0 )
				continue;

			var name = lump.Slice( offset, nameLength );
			var end = name.IndexOf( (byte)0 );

			names[i] = Encoding.Latin1.GetString( end < 0 ? name : name[..end] );
			TextureSizes[i] = (BitConverter.ToInt32( lump[(offset + nameLength)..] ), BitConverter.ToInt32( lump[(offset + nameLength + sizeof( int ))..] ));
		}

		return names;
	}

	private ReadOnlySpan<byte> GetLump( LumpType type )
	{
		var lump = _lumps[(int)type];
		if ( lump.FileOffset < 0 || lump.FileLength < 0 || lump.FileOffset + lump.FileLength > _data.Length )
			throw new InvalidDataException( $"BSP {type} lump is out of bounds" );

		return _data.AsSpan( lump.FileOffset, lump.FileLength );
	}

	private T[] ReadLump<T>( BinaryReader reader, LumpType type, int size, Func<BinaryReader, T> read )
	{
		var length = GetLump( type ).Length;
		if ( length % size != 0 )
			throw new InvalidDataException( $"BSP {type} lump has an odd size" );

		reader.BaseStream.Seek( _lumps[(int)type].FileOffset, SeekOrigin.Begin );

		var items = new T[length / size];
		for ( var i = 0; i < items.Length; i++ )
			items[i] = read( reader );

		return items;
	}
}
