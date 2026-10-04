using System;
using System.IO;

namespace GoldSrc.Studio;

public sealed class File
{
	private const string SamplerName = "studio sampler";

	public Header Header { get; }
	public Bone[] Bones { get; }
	public BoneController[] BoneControllers { get; }
	public BoundingBox[] HitBoxes { get; }
	public SequenceDesc[] Sequences { get; }
	public Event[][] Events { get; }
	public SequenceGroup[] SequenceGroups { get; }
	public BodyParts[] BodyParts { get; }
	public Attachment[] Attachments { get; }
	public byte[] Transitions { get; }
	public bool HasChrome { get; }

	private readonly byte[] _data;
	private readonly byte[][] _sequenceGroupData;
	private readonly int[] _animationIndex;
	private readonly bool[] _controlled;

	private readonly Sandbox.Model _renderModel;
	private SkinnedModelRenderer _sampler;
	private int _animation;

	public File( string name, byte[] data, Func<string, byte[]> loadFile, Sandbox.Model renderModel )
	{
		_data = data;
		_renderModel = renderModel;

		using var reader = new BinaryReader( new MemoryStream( data ) );

		Header = new Header( reader );

		Bones = Read( reader, Header.BoneIndex, Header.NumBones, r => new Bone( r ) );
		BoneControllers = Read( reader, Header.BoneControllerIndex, Header.NumBoneControllers, r => new BoneController( r ) );
		HitBoxes = Read( reader, Header.HitBoxIndex, Header.NumHitBoxes, r => new BoundingBox( r ) );
		Sequences = Read( reader, Header.SeqIndex, Header.NumSeq, r => new SequenceDesc( r ) );
		SequenceGroups = Read( reader, Header.SeqGroupIndex, Header.NumSeqGroups, r => new SequenceGroup( r ) );
		BodyParts = Read( reader, Header.BodyPartIndex, Header.NumBodyParts, r => new BodyParts( r ) );
		Attachments = Read( reader, Header.AttachmentIndex, Header.NumAttachments, r => new Attachment( r ) );
		Transitions = data.AsSpan( Header.TransitionIndex, Header.NumTransitions * Header.NumTransitions ).ToArray();

		var textureData = data;
		var textureHeader = Header;

		if ( Header.NumTextures == 0 && loadFile( $"{name[..^4]}t.mdl" ) is { } textureFile )
		{
			textureData = textureFile;

			using var textureReader = new BinaryReader( new MemoryStream( textureFile ) );
			textureHeader = new Header( textureReader );
		}

		using ( var textureReader = new BinaryReader( new MemoryStream( textureData ) ) )
		{
			var textures = Read( textureReader, textureHeader.TextureIndex, textureHeader.NumTextures, r => new Texture( r ) );
			HasChrome = Array.Exists( textures, x => (x.Flags & TextureFlags.Chrome) == TextureFlags.Chrome );
		}

		Events = new Event[Sequences.Length][];

		for ( var i = 0; i < Sequences.Length; i++ )
			Events[i] = Read( reader, Sequences[i].EventIndex, Sequences[i].NumEvents, r => new Event( r ) );

		_sequenceGroupData = new byte[SequenceGroups.Length][];

		for ( var i = 1; i < SequenceGroups.Length; i++ )
			_sequenceGroupData[i] = loadFile( SequenceGroups[i].Name.Replace( '\\', '/' ) );

		_animationIndex = new int[Sequences.Length];

		var animation = 0;

		for ( var i = 0; i < Sequences.Length; i++ )
		{
			_animationIndex[i] = animation;
			animation += Math.Max( Sequences[i].NumBlends, 1 );
		}

		_controlled = new bool[Bones.Length];

		for ( var i = 0; i < Bones.Length; i++ )
			_controlled[i] = Array.Exists( Bones[i].BoneController, x => x != -1 );
	}

	public bool IsControlled( int bone ) => _controlled[bone];

	public int SequenceIndex( in SequenceDesc sequence )
	{
		for ( var i = 0; i < Sequences.Length; i++ )
		{
			if ( Sequences[i].AnimIndex == sequence.AnimIndex && Sequences[i].SeqGroup == sequence.SeqGroup )
				return i;
		}

		return 0;
	}

	public void Sample( in SequenceDesc sequence, int blend, float frame, Vector3[] positions, Rotation[] rotations )
	{
		if ( !_sampler.IsValid() )
		{
			var gameObject = new GameObject( true, SamplerName )
			{
				Flags = GameObjectFlags.NotSaved | GameObjectFlags.Hidden,
				NetworkMode = NetworkMode.Never
			};

			_sampler = gameObject.AddComponent<SkinnedModelRenderer>();
			_sampler.Model = _renderModel;
			_sampler.UseAnimGraph = false;
			_sampler.PlaybackRate = 0f;
			_sampler.Sequence.Looping = false;
			_sampler.Sequence.Blending = false;
			_animation = -1;
		}

		var scene = _sampler.SceneModel;

		scene.RenderingEnabled = false;

		var animation = _animationIndex[SequenceIndex( sequence )] + blend;

		if ( animation != _animation )
		{
			_animation = animation;
			_sampler.Sequence.Name = _renderModel.GetAnimationName( animation );
		}

		_sampler.Sequence.Time = sequence.Fps > 0f && frame > 0f ? frame / sequence.Fps : 0f;

		scene.Update( 0f );

		for ( var i = 0; i < Bones.Length; i++ )
		{
			var local = scene.GetParentSpaceBone( i );

			positions[i] = local.Position;
			rotations[i] = local.Rotation;
		}
	}

	public ReadOnlySpan<byte> GetAnim( in SequenceDesc sequence )
	{
		if ( sequence.SeqGroup == 0 )
			return _data.AsSpan( sequence.AnimIndex );

		return _sequenceGroupData[sequence.SeqGroup].AsSpan( sequence.AnimIndex );
	}

	private static T[] Read<T>( BinaryReader reader, int offset, int count, Func<BinaryReader, T> read )
	{
		reader.BaseStream.Seek( offset, SeekOrigin.Begin );

		var items = new T[count];
		for ( var i = 0; i < items.Length; i++ )
			items[i] = read( reader );

		return items;
	}
}
