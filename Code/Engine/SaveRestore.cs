using System;

namespace GoldSrc;

public enum FieldType
{
	Float,
	String,
	Entity,
	ClassPtr,
	EHandle,
	EntVars,
	Edict,
	Vector,
	PositionVector,
	Pointer,
	Integer,
	Function,
	Boolean,
	Short,
	Character,
	Time,
	ModelName,
	SoundName
}

[AttributeUsage( AttributeTargets.Property | AttributeTargets.Field )]
public sealed class SaveAttribute : Attribute
{
	public FieldType Type { get; }
	public bool Global { get; init; }

	public SaveAttribute( FieldType type )
	{
		Type = type;
	}
}

public sealed class SaveBlock
{
	public string Name { get; }
	public Dictionary<string, object> Fields { get; } = [];

	public SaveBlock( string name )
	{
		Name = name;
	}
}

public sealed class EntityTable
{
	public const int Player = unchecked((int)0x80000000);
	public const int Removed = 0x40000000;
	public const int Moveable = 0x20000000;
	public const int Global = 0x10000000;

	public int Id;
	public Edict Edict;
	public int Flags;
	public string ClassName;
	public List<SaveBlock> Blocks;
}

public sealed class LevelList
{
	public string MapName;
	public string LandmarkName;
	public Edict Landmark;
	public Vector3 LandmarkOrigin;
}

public sealed class GameHeader
{
	public string MapName;
	public string Comment;
	public int MapCount;
}

public sealed class SaveRestoreData
{
	public const int MaxLevelConnections = 16;

	public EntityTable[] Table = [];
	public int CurrentIndex;
	public List<LevelList> LevelList = [];
	public bool UseLandmark;
	public string LandmarkName = "";
	public string CurrentMapName;
	public Vector3 LandmarkOffset;
	public float Time;
	public string[] LightStyles = [];
	public int Skill;
	public string SkyName;
	public float[] Sky = [];
	public List<DecalEntry> Decals;

	public EntityTable Current => Table[CurrentIndex];
}
