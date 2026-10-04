using System.IO;

namespace GoldSrc.Bsp;

public enum LumpType
{
	Entities,
	Planes,
	Textures,
	Vertexes,
	Visibility,
	Nodes,
	TexInfo,
	Faces,
	Lighting,
	ClipNodes,
	Leafs,
	MarkSurfaces,
	Edges,
	SurfEdges,
	Models,

	Count
}

public enum PlaneType
{
	X,
	Y,
	Z,
	AnyX,
	AnyY,
	AnyZ
}

public enum Contents
{
	Empty = -1,
	Solid = -2,
	Water = -3,
	Slime = -4,
	Lava = -5,
	Sky = -6,
	Origin = -7,
	Clip = -8,
	Current0 = -9,
	Current90 = -10,
	Current180 = -11,
	Current270 = -12,
	CurrentUp = -13,
	CurrentDown = -14,
	Translucent = -15
}

public readonly struct Lump( BinaryReader reader )
{
	public readonly int FileOffset = reader.ReadInt32();
	public readonly int FileLength = reader.ReadInt32();
}

public readonly record struct Plane( Vector3 Normal, float Dist, PlaneType Type )
{
	public const int Size = 20;

	public Plane( BinaryReader reader ) : this( reader.ReadVector3(), reader.ReadSingle(), (PlaneType)reader.ReadInt32() )
	{
	}
}

public readonly struct Node( BinaryReader reader )
{
	public const int Size = 24;

	public readonly int PlaneNum = reader.ReadInt32();
	public readonly short Child0 = reader.ReadInt16();
	public readonly short Child1 = reader.ReadInt16();
	public readonly Short3 Mins = new( reader );
	public readonly Short3 Maxs = new( reader );
	public readonly ushort FirstFace = reader.ReadUInt16();
	public readonly ushort NumFaces = reader.ReadUInt16();

	public short GetChild( int side ) => side == 0 ? Child0 : Child1;
}

public readonly record struct ClipNode( int PlaneNum, short Child0, short Child1 )
{
	public const int Size = 8;

	public ClipNode( BinaryReader reader ) : this( reader.ReadInt32(), reader.ReadInt16(), reader.ReadInt16() )
	{
	}

	public short GetChild( int side ) => side == 0 ? Child0 : Child1;
}

public readonly struct TexInfo( BinaryReader reader )
{
	public const int Size = 40;

	public readonly Vector3 S = reader.ReadVector3();
	public readonly float OffsetS = reader.ReadSingle();
	public readonly Vector3 T = reader.ReadVector3();
	public readonly float OffsetT = reader.ReadSingle();
	public readonly int MipTex = reader.ReadInt32();
	public readonly TexInfoFlags Flags = (TexInfoFlags)reader.ReadInt32();
}

[System.Flags]
public enum TexInfoFlags
{
	Special = 1
}

public readonly struct Edge( BinaryReader reader )
{
	public const int Size = 4;

	public readonly ushort V0 = reader.ReadUInt16();
	public readonly ushort V1 = reader.ReadUInt16();
}

public readonly struct Face( BinaryReader reader )
{
	public const int Size = 20;
	public const int MaxLightmaps = 4;

	public readonly short PlaneNum = reader.ReadInt16();
	public readonly short Side = reader.ReadInt16();
	public readonly int FirstEdge = reader.ReadInt32();
	public readonly short NumEdges = reader.ReadInt16();
	public readonly short TexInfo = reader.ReadInt16();
	public readonly byte[] Styles = reader.ReadBytes( MaxLightmaps );
	public readonly int LightOffset = reader.ReadInt32();
}

public readonly struct Leaf( BinaryReader reader )
{
	public const int Size = 28;
	public const int NumAmbients = 4;

	public readonly Contents Contents = (Contents)reader.ReadInt32();
	public readonly int VisOffset = reader.ReadInt32();
	public readonly Short3 Mins = new( reader );
	public readonly Short3 Maxs = new( reader );
	public readonly ushort FirstMarkSurface = reader.ReadUInt16();
	public readonly ushort NumMarkSurfaces = reader.ReadUInt16();
	public readonly byte[] AmbientLevel = reader.ReadBytes( NumAmbients );
}

public readonly struct Model( BinaryReader reader )
{
	public const int Size = 64;
	public const int MaxHulls = 4;

	public readonly Vector3 Mins = reader.ReadVector3();
	public readonly Vector3 Maxs = reader.ReadVector3();
	public readonly Vector3 Origin = reader.ReadVector3();
	public readonly int[] HeadNode = [reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()];
	public readonly int VisLeafs = reader.ReadInt32();
	public readonly int FirstFace = reader.ReadInt32();
	public readonly int NumFaces = reader.ReadInt32();
}

public readonly struct Short3( BinaryReader reader )
{
	public readonly short X = reader.ReadInt16();
	public readonly short Y = reader.ReadInt16();
	public readonly short Z = reader.ReadInt16();

	public static implicit operator Vector3( Short3 value ) => new( value.X, value.Y, value.Z );
}

static class BinaryReaderExtensions
{
	public static Vector3 ReadVector3( this BinaryReader reader )
	{
		return new Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
	}
}
