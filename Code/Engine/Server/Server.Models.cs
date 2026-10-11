using System;
using Sandbox.Mounting;

namespace GoldSrc;

partial class Server
{
	private const int MaxModels = 512;

	public Studio.BoneSetup BoneSetup { get; } = new();

	private readonly List<Model> _models = [];
	private readonly List<string> _modelNames = [];
	private readonly List<int> _modelFlags = [];
	private bool _loading;

	public BrushModel WorldBrushModel => (BrushModel)_models[1];

	public int PrecacheModel( string name )
	{
		if ( name is null )
			throw HostError( "PF_precache_model_I: NULL pointer" );

		if ( name.Length == 0 || name[0] <= ' ' )
			throw HostError( $"PF_precache_model_I: Bad string '{name}'" );

		var optional = name[0] == '!';

		if ( optional )
			name = name[1..];

		var index = FindModel( name );
		if ( index >= 0 )
			return index;

		if ( !_loading )
			throw HostError( $"PF_precache_model_I: '{name}' Precache can only be done in spawn functions" );

		if ( _models.Count >= MaxModels )
			throw HostError( $"PF_precache_model_I: Model '{name}' failed to precache because the item count is over the {MaxModels} limit.\nReduce the number of brush models and/or regular models in the map to correct this." );

		_models.Add( ModelCache.ForName( name ) ?? throw HostError( $"Mod_NumForName: {name} not found" ) );
		_modelNames.Add( name );
		_modelFlags.Add( optional ? 0 : Resource.FatalIfMissing );

		return _models.Count - 1;
	}

	public int FindModel( string name )
	{
		return Common.IndexOfName( _modelNames, name );
	}

	public int ModelIndex( string name )
	{
		if ( string.IsNullOrEmpty( name ) )
			return 0;

		var index = FindModel( name );

		if ( index < 0 )
			throw HostError( $"SV_ModelIndex: model {name} not precached" );

		return index;
	}

	public Model GetModelByIndex( int index )
	{
		return index > 0 && index < _models.Count ? _models[index] : null;
	}

	public BrushModel GetModel( int index )
	{
		return index > 0 && index < _models.Count ? _models[index] as BrushModel : null;
	}

	public Studio.File GetModelPtr( Edict edict )
	{
		var index = edict?.ModelIndex ?? 0;

		return index > 0 && index < _models.Count ? (_models[index] as StudioModel)?.File : null;
	}

	public Vector3 GetBonePosition( Edict edict, int bone )
	{
		BoneSetup.ServerSetupBones( GetModelPtr( edict ), edict.Frame, edict.Sequence, edict.Angles, edict.Origin, edict.Controller, edict.Blending, bone );

		return BoneSetup.ServerBonePosition( bone );
	}

	public Vector3 GetAttachment( Edict edict, int attachment )
	{
		var header = GetModelPtr( edict );
		var info = header.Attachments[attachment];

		BoneSetup.ServerSetupBones( header, edict.Frame, edict.Sequence, edict.Angles with { x = -edict.Angles.x }, edict.Origin, edict.Controller, edict.Blending, info.Bone );

		return BoneSetup.ServerTransformPoint( info.Bone, info.Org );
	}

	public Studio.File GetStudioModel( int index )
	{
		return index > 0 && index < _models.Count ? (_models[index] as StudioModel)?.File : null;
	}

	public SpriteModel GetSpriteModel( int index )
	{
		return index > 0 && index < _models.Count ? _models[index] as SpriteModel : null;
	}

	public int ModelFrames( int index )
	{
		if ( index < 1 || index >= MaxModels )
		{
			Con.DPrintf( "Bad sprite index!\n" );
			return 1;
		}

		return ModelFrames( index < _models.Count ? _models[index] : null );
	}

	public static int ModelFrames( Model model )
	{
		var frames = model switch
		{
			SpriteModel sprite => sprite.Frames.Length,
			StudioModel studio => StudioFrames( studio.File ),
			_ => 1
		};

		return Math.Max( frames, 1 );
	}

	private static int StudioFrames( Studio.File file )
	{
		var frames = 1;

		foreach ( var bodyPart in file.BodyParts )
			frames *= bodyPart.NumModels;

		return frames;
	}

	public Sandbox.Model GetRenderModel( int index )
	{
		return index > 0 && index < _models.Count ? _models[index].RenderModel : null;
	}

	public void SetModel( Edict edict, string name )
	{
		if ( string.IsNullOrEmpty( name ) )
		{
			edict.ModelName = name;
			edict.ModelIndex = 0;

			SetMinMaxSize( edict, Vector3.Zero, Vector3.Zero );
			return;
		}

		var index = FindModel( name );
		if ( index < 0 )
			throw HostError( $"no precache: {name}\n" );

		edict.ModelName = name;
		edict.ModelIndex = index;

		SetMinMaxSize( edict, _models[index].Mins, _models[index].Maxs );
	}

	public void SetSize( Edict edict, Vector3 mins, Vector3 maxs )
	{
		SetMinMaxSize( edict, mins, maxs );
	}

	public void SetOrigin( Edict edict, Vector3 origin )
	{
		edict.Origin = origin;
		LinkEdict( edict, false );

		if ( edict == _shadowPlayer && _shadow.IsValid() )
		{
			_shadow.SetPosition( origin );
			_shadowOldOrigin = origin;
		}
	}

	private void SetMinMaxSize( Edict edict, Vector3 mins, Vector3 maxs )
	{
		if ( mins.x > maxs.x || mins.y > maxs.y || mins.z > maxs.z )
			throw HostError( "backwards mins/maxs" );

		edict.Mins = mins;
		edict.Maxs = maxs;
		edict.Size = maxs - mins;

		LinkEdict( edict, false );
	}

	private void PrecacheWorldModel( string modelName )
	{
		ModelCache.Clear();

		_models.Clear();
		_modelNames.Clear();
		_modelFlags.Clear();

		_models.Add( null );
		_modelNames.Add( null );
		_modelFlags.Add( 0 );

		foreach ( var model in BrushModel.Load( WorldModel, modelName, GetMountPath( modelName ) ) )
		{
			ModelCache.Add( model );

			_models.Add( model );
			_modelNames.Add( model.Name );
			_modelFlags.Add( Resource.FatalIfMissing );
		}
	}

	public static byte[] LoadMountFile( string fileName )
	{
		var task = MountUtility.FindLoader( GetMountPath( fileName ) )?.GetOrCreate();

		return task is { IsCompletedSuccessfully: true } ? task.Result as byte[] : null;
	}
}
