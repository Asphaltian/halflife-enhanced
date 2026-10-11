using System.IO;

namespace GoldSrc;

public enum SpriteType
{
	ParallelUpright,
	FacingUpright,
	Parallel,
	Oriented,
	ParallelOriented
}

public sealed class SpriteFrame
{
	public float Up { get; init; }
	public float Down { get; init; }
	public float Left { get; init; }
	public float Right { get; init; }
	public Material Material { get; init; }
	public Texture Texture { get; init; }
}

public sealed partial class SpriteModel : Model
{
	public const int Id = 0x50534449;
	public const int Version = 2;

	public override ModelType Type => ModelType.Sprite;
	public SpriteType SpriteType { get; private init; }
	public SpriteFrame[] Frames { get; private init; }

	public static SpriteModel Load( string name, byte[] data, string mountPath )
	{
		using var reader = new BinaryReader( new MemoryStream( data ) );

		reader.ReadInt32();

		var version = reader.ReadInt32();
		if ( version != Version )
			throw new InvalidDataException( $"{name} has wrong version number ({version} should be {Version})" );

		var type = (SpriteType)reader.ReadInt32();
		reader.ReadInt32();
		reader.ReadSingle();
		var width = reader.ReadInt32();
		var height = reader.ReadInt32();
		var numFrames = reader.ReadInt32();
		reader.ReadSingle();
		reader.ReadInt32();

		reader.BaseStream.Seek( reader.ReadInt16() * 3, SeekOrigin.Current );

		if ( numFrames < 1 )
			throw new InvalidDataException( $"Mod_LoadSpriteModel: Invalid # of frames: {numFrames}\n" );

		var frames = new SpriteFrame[numFrames];

		for ( var i = 0; i < numFrames; i++ )
		{
			if ( reader.ReadInt32() == 0 )
			{
				frames[i] = ReadFrame( reader, Material.Load( $"{mountPath}/{i}.vmat" ) );
				continue;
			}

			var groupFrames = reader.ReadInt32();

			for ( var interval = 0; interval < groupFrames; interval++ )
			{
				if ( reader.ReadSingle() <= 0f )
					throw new InvalidDataException( "Mod_LoadSpriteGroup: interval<=0" );
			}

			for ( var frame = 0; frame < groupFrames; frame++ )
				ReadFrame( reader, null );
		}

		return new SpriteModel
		{
			Name = name,
			SpriteType = type,
			Frames = frames,
			Mins = new Vector3( -(width / 2), -(width / 2), -(height / 2) ),
			Maxs = new Vector3( width / 2, width / 2, height / 2 )
		};
	}

	private static SpriteFrame ReadFrame( BinaryReader reader, Material material )
	{
		var originX = reader.ReadInt32();
		var originY = reader.ReadInt32();
		var width = reader.ReadInt32();
		var height = reader.ReadInt32();

		reader.BaseStream.Seek( width * height, SeekOrigin.Current );

		return new SpriteFrame
		{
			Up = originY,
			Down = originY - height,
			Left = originX,
			Right = originX + width,
			Material = material
		};
	}
}
