using System;
using System.IO;
using System.Text;
using GoldSrc.Bsp;

namespace GoldSrc.Studio;

[Flags]
public enum MotionFlags
{
	X = 0x0001,
	Y = 0x0002,
	Z = 0x0004,
	XR = 0x0008,
	YR = 0x0010,
	ZR = 0x0020,
	LX = 0x0040,
	LY = 0x0080,
	LZ = 0x0100,
	AX = 0x0200,
	AY = 0x0400,
	AZ = 0x0800,
	AXR = 0x1000,
	AYR = 0x2000,
	AZR = 0x4000,
	Types = 0x7FFF,
	RLoop = 0x8000
}

[Flags]
public enum SequenceFlags
{
	Looping = 0x0001
}

[Flags]
public enum TextureFlags
{
	FlatShade = 0x0001,
	Chrome = 0x0002,
	FullBright = 0x0004,
	NoMips = 0x0008,
	Alpha = 0x0010,
	Additive = 0x0020,
	Masked = 0x0040
}

public readonly struct Header( BinaryReader reader )
{
	public const int Id = 0x54534449;
	public const int Version = 10;

	public readonly int Ident = reader.ReadInt32();
	public readonly int FileVersion = reader.ReadInt32();
	public readonly string Name = reader.ReadFixedString( 64 );
	public readonly int Length = reader.ReadInt32();
	public readonly Vector3 EyePosition = reader.ReadVector3();
	public readonly Vector3 Min = reader.ReadVector3();
	public readonly Vector3 Max = reader.ReadVector3();
	public readonly Vector3 BBMin = reader.ReadVector3();
	public readonly Vector3 BBMax = reader.ReadVector3();
	public readonly int Flags = reader.ReadInt32();
	public readonly int NumBones = reader.ReadInt32();
	public readonly int BoneIndex = reader.ReadInt32();
	public readonly int NumBoneControllers = reader.ReadInt32();
	public readonly int BoneControllerIndex = reader.ReadInt32();
	public readonly int NumHitBoxes = reader.ReadInt32();
	public readonly int HitBoxIndex = reader.ReadInt32();
	public readonly int NumSeq = reader.ReadInt32();
	public readonly int SeqIndex = reader.ReadInt32();
	public readonly int NumSeqGroups = reader.ReadInt32();
	public readonly int SeqGroupIndex = reader.ReadInt32();
	public readonly int NumTextures = reader.ReadInt32();
	public readonly int TextureIndex = reader.ReadInt32();
	public readonly int TextureDataIndex = reader.ReadInt32();
	public readonly int NumSkinRef = reader.ReadInt32();
	public readonly int NumSkinFamilies = reader.ReadInt32();
	public readonly int SkinIndex = reader.ReadInt32();
	public readonly int NumBodyParts = reader.ReadInt32();
	public readonly int BodyPartIndex = reader.ReadInt32();
	public readonly int NumAttachments = reader.ReadInt32();
	public readonly int AttachmentIndex = reader.ReadInt32();
	public readonly int SoundTable = reader.ReadInt32();
	public readonly int SoundIndex = reader.ReadInt32();
	public readonly int SoundGroups = reader.ReadInt32();
	public readonly int SoundGroupIndex = reader.ReadInt32();
	public readonly int NumTransitions = reader.ReadInt32();
	public readonly int TransitionIndex = reader.ReadInt32();
}

public readonly struct Bone( BinaryReader reader )
{
	public readonly string Name = reader.ReadFixedString( 32 );
	public readonly int Parent = reader.ReadInt32();
	public readonly int Flags = reader.ReadInt32();
	public readonly int[] BoneController = reader.ReadInt32Array( 6 );
	public readonly float[] Value = reader.ReadSingleArray( 6 );
	public readonly float[] Scale = reader.ReadSingleArray( 6 );
}

public readonly struct BoneController( BinaryReader reader )
{
	public readonly int Bone = reader.ReadInt32();
	public readonly MotionFlags Type = (MotionFlags)reader.ReadInt32();
	public readonly float Start = reader.ReadSingle();
	public readonly float End = reader.ReadSingle();
	public readonly int Rest = reader.ReadInt32();
	public readonly int Index = reader.ReadInt32();
}

public readonly struct BoundingBox( BinaryReader reader )
{
	public readonly int Bone = reader.ReadInt32();
	public readonly int Group = reader.ReadInt32();
	public readonly Vector3 BBMin = reader.ReadVector3();
	public readonly Vector3 BBMax = reader.ReadVector3();
}

public readonly struct SequenceGroup( BinaryReader reader )
{
	public readonly string Label = reader.ReadFixedString( 32 );
	public readonly string Name = reader.ReadFixedString( 64 );
	public readonly int Unused1 = reader.ReadInt32();
	public readonly int Unused2 = reader.ReadInt32();
}

public readonly struct SequenceDesc( BinaryReader reader )
{
	public readonly string Label = reader.ReadFixedString( 32 );
	public readonly float Fps = reader.ReadSingle();
	public readonly SequenceFlags Flags = (SequenceFlags)reader.ReadInt32();
	public readonly int Activity = reader.ReadInt32();
	public readonly int ActWeight = reader.ReadInt32();
	public readonly int NumEvents = reader.ReadInt32();
	public readonly int EventIndex = reader.ReadInt32();
	public readonly int NumFrames = reader.ReadInt32();
	public readonly int NumPivots = reader.ReadInt32();
	public readonly int PivotIndex = reader.ReadInt32();
	public readonly MotionFlags MotionType = (MotionFlags)reader.ReadInt32();
	public readonly int MotionBone = reader.ReadInt32();
	public readonly Vector3 LinearMovement = reader.ReadVector3();
	public readonly int AutoMovePosIndex = reader.ReadInt32();
	public readonly int AutoMoveAngleIndex = reader.ReadInt32();
	public readonly Vector3 BBMin = reader.ReadVector3();
	public readonly Vector3 BBMax = reader.ReadVector3();
	public readonly int NumBlends = reader.ReadInt32();
	public readonly int AnimIndex = reader.ReadInt32();
	public readonly MotionFlags[] BlendType = [(MotionFlags)reader.ReadInt32(), (MotionFlags)reader.ReadInt32()];
	public readonly float[] BlendStart = reader.ReadSingleArray( 2 );
	public readonly float[] BlendEnd = reader.ReadSingleArray( 2 );
	public readonly int BlendParent = reader.ReadInt32();
	public readonly int SeqGroup = reader.ReadInt32();
	public readonly int EntryNode = reader.ReadInt32();
	public readonly int ExitNode = reader.ReadInt32();
	public readonly int NodeFlags = reader.ReadInt32();
	public readonly int NextSeq = reader.ReadInt32();
}

public readonly struct Event( BinaryReader reader )
{
	public readonly int Frame = reader.ReadInt32();
	public readonly int Index = reader.ReadInt32();
	public readonly int Type = reader.ReadInt32();
	public readonly string Options = reader.ReadFixedString( 64 );
}

public readonly struct Attachment( BinaryReader reader )
{
	public readonly string Name = reader.ReadFixedString( 32 );
	public readonly int Type = reader.ReadInt32();
	public readonly int Bone = reader.ReadInt32();
	public readonly Vector3 Org = reader.ReadVector3();
	public readonly Vector3[] Vectors = [reader.ReadVector3(), reader.ReadVector3(), reader.ReadVector3()];
}

public readonly struct Texture( BinaryReader reader )
{
	public readonly string Name = reader.ReadFixedString( 64 );
	public readonly TextureFlags Flags = (TextureFlags)reader.ReadInt32();
	public readonly int Width = reader.ReadInt32();
	public readonly int Height = reader.ReadInt32();
	public readonly int Index = reader.ReadInt32();
}

public readonly struct BodyParts( BinaryReader reader )
{
	public readonly string Name = reader.ReadFixedString( 64 );
	public readonly int NumModels = reader.ReadInt32();
	public readonly int Base = reader.ReadInt32();
	public readonly int ModelIndex = reader.ReadInt32();
}

internal static class ReaderExtensions
{
	public static string ReadFixedString( this BinaryReader reader, int length )
	{
		var bytes = reader.ReadBytes( length );
		var end = Array.IndexOf( bytes, (byte)0 );

		return Encoding.Latin1.GetString( bytes, 0, end < 0 ? bytes.Length : end );
	}

	public static int[] ReadInt32Array( this BinaryReader reader, int count )
	{
		var values = new int[count];

		for ( var i = 0; i < count; i++ )
			values[i] = reader.ReadInt32();

		return values;
	}

	public static float[] ReadSingleArray( this BinaryReader reader, int count )
	{
		var values = new float[count];

		for ( var i = 0; i < count; i++ )
			values[i] = reader.ReadSingle();

		return values;
	}
}
