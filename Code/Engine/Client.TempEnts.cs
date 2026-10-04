using System;

namespace GoldSrc;

[Flags]
public enum TempEntityFlags
{
	None = 0,
	SineWave = 0x00000001,
	Gravity = 0x00000002,
	Rotate = 0x00000004,
	SlowGravity = 0x00000008,
	SmokeTrail = 0x00000010,
	CollideWorld = 0x00000020,
	Flicker = 0x00000040,
	FadeOut = 0x00000080,
	SprAnimate = 0x00000100,
	HitSound = 0x00000200,
	Spiral = 0x00000400,
	SprCycle = 0x00000800,
	CollideAll = 0x00001000,
	Persist = 0x00002000,
	CollideKill = 0x00004000,
	PlyrAttachment = 0x00008000,
	SprAnimateLoop = 0x00010000,
	SparkShower = 0x00020000,
	NoModel = 0x00040000,
	ClientCustom = 0x00080000
}

public sealed class TempEnt
{
	public const int BounceGlass = 0x01;
	public const int BounceMetal = 0x02;
	public const int BounceFlesh = 0x04;
	public const int BounceWood = 0x08;
	public const int BounceShrap = 0x10;
	public const int BounceShell = 0x20;
	public const int BounceConcrete = 0x40;
	public const int BounceShotShell = 0x80;

	public int Index { get; init; }

	public TempEntityFlags Flags;
	public float Die;
	public float FrameMax;
	public float X;
	public float Y;
	public float Z;
	public float FadeSpeed;
	public float BounceFactor;
	public int HitSound;
	public Action<TempEnt, PmTrace> HitCallback;
	public Action<TempEnt, float, float> Callback;
	public TempEnt Next;
	public int Priority;
	public short ClientIndex;
	public Vector3 TentOffset;

	public Model Model;
	public Vector3 Origin;
	public Vector3 Angles;
	public Vector3 PrevOrigin;
	public float PrevFrame;
	public Vector3 Velocity;
	public Vector3 AngularVelocity;
	public float BaselineRenderAmt;
	public float BaselineScale;
	public float BaselineFrameRate;
	public Vector3 BaselineVUser1;
	public Vector3 BaselineVUser2;
	public float Frame;
	public float FrameRate;
	public int Body;
	public int Skin;
	public int Effects;
	public float Scale;
	public RenderMode RenderMode;
	public float RenderAmt;
	public Vector3 RenderColor;
	public RenderFx RenderFx;

	internal void Prepare( Model model, double time )
	{
		Flags = TempEntityFlags.None;
		Die = (float)(time + 0.75);
		FrameMax = 0f;
		X = 0f;
		Y = 0f;
		Z = 0f;
		FadeSpeed = 0.5f;
		BounceFactor = 1f;
		HitSound = 0;
		HitCallback = null;
		Callback = null;
		ClientIndex = -1;

		Model = model;
		Origin = Vector3.Zero;
		Angles = Vector3.Zero;
		PrevOrigin = Vector3.Zero;
		PrevFrame = 0f;
		Velocity = Vector3.Zero;
		AngularVelocity = Vector3.Zero;
		BaselineRenderAmt = 0f;
		BaselineScale = 0f;
		BaselineFrameRate = 0f;
		BaselineVUser1 = Vector3.Zero;
		BaselineVUser2 = Vector3.Zero;
		Frame = 0f;
		FrameRate = 0f;
		Body = 0;
		Skin = 0;
		Effects = 0;
		Scale = 0f;
		RenderMode = RenderMode.Normal;
		RenderAmt = 0f;
		RenderColor = Vector3.Zero;
		RenderFx = RenderFx.None;
	}
}

partial class Client
{
	private const int MaxTempEntities = 500;
	private const int BreakTypeMask = 0x4F;
	private const int BreakSmoke = 0x10;
	private const int BreakTrans = 0x20;

	private static readonly string[] GlassSounds = ["debris/glass1.wav", "debris/glass2.wav", "debris/glass3.wav"];
	private static readonly string[] MetalSounds = ["debris/metal1.wav", "debris/metal2.wav", "debris/metal3.wav"];
	private static readonly string[] FleshSounds = ["debris/flesh1.wav", "debris/flesh2.wav", "debris/flesh3.wav", "debris/flesh5.wav", "debris/flesh6.wav", "debris/flesh7.wav"];
	private static readonly string[] WoodSounds = ["debris/wood1.wav", "debris/wood2.wav", "debris/wood3.wav"];
	private static readonly string[] RicochetSounds = ["weapons/ric1.wav", "weapons/ric2.wav", "weapons/ric3.wav", "weapons/ric4.wav", "weapons/ric5.wav"];
	private static readonly string[] ShellSounds = ["player/pl_shell1.wav", "player/pl_shell2.wav", "player/pl_shell3.wav"];
	private static readonly string[] ConcreteSounds = ["debris/concrete1.wav", "debris/concrete2.wav", "debris/concrete3.wav"];
	private static readonly string[] ShotShellSounds = ["weapons/sshell1.wav", "weapons/sshell2.wav", "weapons/sshell3.wav"];

	private readonly List<TempEnt> _visibleTempEntities = [];
	private readonly List<ClientModelRenderer> _tempEntityRenderers = [];

	private TempEnt _freeTempEntities;
	private Model[] _muzzleFlash;
	private TempEnt _activeTempEntities;

	private void ClearTempEntities()
	{
		_freeTempEntities = null;
		_activeTempEntities = null;

		for ( var i = MaxTempEntities - 1; i >= 0; i-- )
			_freeTempEntities = new TempEnt { Index = i, Next = _freeTempEntities };
	}

	public TempEnt AllocTempEntity( Vector3 origin, Model model )
	{
		if ( _freeTempEntities is not { } temp )
		{
			Log.Trace( $"Overflow {MaxTempEntities} temporary ents!\n" );
			return null;
		}

		if ( model is null )
		{
			Log.Info( "efx.CL_TempEntAlloc: No model" );
			return null;
		}

		_freeTempEntities = temp.Next;

		temp.Prepare( model, ClientTime );
		temp.Priority = 0;
		temp.Origin = origin;
		temp.Next = _activeTempEntities;
		_activeTempEntities = temp;

		return temp;
	}

	public TempEnt AllocTempEntityNoModel( Vector3 origin )
	{
		if ( _freeTempEntities is not { } temp )
		{
			Log.Trace( $"Overflow {MaxTempEntities} temporary ents!\n" );
			return null;
		}

		_freeTempEntities = temp.Next;

		temp.Prepare( null, ClientTime );
		temp.Flags |= TempEntityFlags.NoModel;
		temp.Priority = 0;
		temp.Origin = origin;
		temp.Next = _activeTempEntities;
		_activeTempEntities = temp;

		return temp;
	}

	public TempEnt TempModel( Vector3 position, Vector3 direction, Vector3 angles, float life, int modelIndex, int soundType )
	{
		if ( modelIndex == 0 )
			return null;

		if ( GetModelByIndex( modelIndex ) is not { } model )
		{
			Log.Trace( $"No model {modelIndex}!" );
			return null;
		}

		var frameCount = ModelFrames( modelIndex );

		if ( AllocTempEntity( position, model ) is not { } temp )
			return null;

		temp.Angles = angles;
		temp.Flags |= TempEntityFlags.CollideWorld | TempEntityFlags.Gravity;
		temp.FrameMax = frameCount;

		switch ( soundType )
		{
			case 0:
				temp.HitSound = 0;
				break;

			case 1:
				temp.HitSound = TempEnt.BounceShell;
				temp.AngularVelocity.x = Server.RandomFloat( -512f, 511f );
				temp.AngularVelocity.y = Server.RandomFloat( -256f, 255f );
				temp.AngularVelocity.z = Server.RandomFloat( -256f, 255f );
				temp.Flags |= TempEntityFlags.Rotate;
				break;

			case 2:
				temp.HitSound = TempEnt.BounceShotShell;
				temp.AngularVelocity.x = Server.RandomFloat( -512f, 511f );
				temp.AngularVelocity.y = Server.RandomFloat( -256f, 255f );
				temp.AngularVelocity.z = Server.RandomFloat( -256f, 255f );
				temp.Flags |= TempEntityFlags.Rotate | TempEntityFlags.SlowGravity;
				break;
		}

		temp.Velocity = direction;
		temp.Die = (float)(life + ClientTime);

		if ( model.Type == ModelType.Sprite )
			temp.Frame = Server.RandomLong( 0, frameCount - 1 );
		else
			temp.Body = Server.RandomLong( 0, frameCount - 1 );

		return temp;
	}

	public void BreakModel( Vector3 position, Vector3 size, Vector3 direction, float random, float life, int count, int modelIndex, int flags )
	{
		if ( modelIndex == 0 )
			return;

		if ( GetModelByIndex( modelIndex ) is not { } model )
			return;

		var frameCount = ModelFrames( modelIndex );
		var type = flags & BreakTypeMask;

		if ( count == 0 )
			count = (int)(((size.x * size.y) + (size.y * size.z) + (size.x * size.z)) / 432f);

		if ( count > 100 )
			count = 100;

		for ( var i = 0; i < count; i++ )
		{
			var x = (Server.RandomFloat( -0.5f, 0.5f ) * size.x) + position.x;
			var y = (Server.RandomFloat( -0.5f, 0.5f ) * size.y) + position.y;
			var z = (Server.RandomFloat( -0.5f, 0.5f ) * size.z) + position.z;
			var spot = new Vector3( x, y, z );

			if ( AllocTempEntity( spot, model ) is not { } temp )
				return;

			temp.HitSound = type;

			if ( model.Type == ModelType.Sprite )
				temp.Frame = Server.RandomLong( 0, frameCount - 1 );
			else if ( model.Type == ModelType.Studio )
				temp.Body = Server.RandomLong( 0, frameCount - 1 );

			temp.Flags |= TempEntityFlags.CollideWorld | TempEntityFlags.FadeOut | TempEntityFlags.SlowGravity;

			if ( Server.RandomLong( 0, 255 ) < 200 )
			{
				temp.Flags |= TempEntityFlags.Rotate;
				temp.AngularVelocity.x = Server.RandomFloat( -256f, 255f );
				temp.AngularVelocity.y = Server.RandomFloat( -256f, 255f );
				temp.AngularVelocity.z = Server.RandomFloat( -256f, 255f );
			}

			if ( Server.RandomLong( 0, 255 ) < 100 && (flags & BreakSmoke) != 0 )
				temp.Flags |= TempEntityFlags.SmokeTrail;

			if ( type == TempEnt.BounceGlass || (flags & BreakTrans) != 0 )
			{
				temp.RenderMode = RenderMode.TransTexture;
				temp.RenderAmt = 128f;
				temp.BaselineRenderAmt = 128f;
			}
			else
			{
				temp.RenderMode = RenderMode.Normal;
				temp.BaselineRenderAmt = 255f;
			}

			temp.Velocity.x = Server.RandomFloat( -random, random ) + direction.x;
			temp.Velocity.y = Server.RandomFloat( -random, random ) + direction.y;
			temp.Velocity.z = Server.RandomFloat( 0f, random ) + direction.z;

			temp.Die = (float)(life + ClientTime + Server.RandomFloat( 0f, 1f ));
		}
	}

	public void MuzzleFlash( Vector3 position, int type )
	{
		_muzzleFlash ??=
		[
			ModelCache.ForName( "sprites/muzzleflash1.spr" ),
			ModelCache.ForName( "sprites/muzzleflash2.spr" ),
			ModelCache.ForName( "sprites/muzzleflash3.spr" )
		];

		var index = type % 10 % 3;
		var scale = type / 10 * 0.1f;

		if ( scale == 0f )
			scale = 0.5f;

		var model = (SpriteModel)_muzzleFlash[index];
		var frameCount = model.Frames.Length;

		if ( AllocTempEntity( position, model ) is not { } temp )
			return;

		temp.Scale = scale;
		temp.RenderMode = RenderMode.TransAdd;
		temp.RenderAmt = 255f;
		temp.RenderFx = RenderFx.None;
		temp.RenderColor = new Vector3( 255f, 255f, 255f );
		temp.Origin = position;
		temp.Die = (float)(ClientTime + 0.01);
		temp.Frame = Server.RandomLong( 0, frameCount - 1 );
		temp.FrameMax = frameCount;
		temp.Angles = new Vector3( 0f, 0f, Server.RandomLong( 0, index == 0 ? 20 : 359 ) );

		_visibleTempEntities.Add( temp );

		DrawTempEntity( _visibleTempEntities.Count - 1 );
	}

	public void PlaySoundByNameAtLocation( string name, float volume, Vector3 origin )
	{
		volume = volume <= 0f ? 0f : volume;
		volume = volume >= 1f ? 1f : volume;

		Sound.StartDynamicSound( _viewEntity, (int)SoundChannel.Auto, Sound.PrecacheSound( name ), origin, volume, 1f, 0, Server.PitchNormal );
	}

	internal void StudioClientEvents( Studio.File header, in EntityState state, in Studio.LatchedVars latched, ref Effects effects, Vector3[] attachments )
	{
		if ( ClientTime == _oldTime )
			return;

		if ( (effects & Effects.MuzzleFlash) == Effects.MuzzleFlash )
		{
			var light = AllocElight( 0 );

			light.Origin = attachments[0];
			light.Radius = 16f;
			light.Decay = 320f;
			light.R = 255;
			light.G = 192;
			light.B = 64;
			light.Die = (float)(ClientTime + 0.05);

			effects &= ~Effects.MuzzleFlash;
		}

		ref readonly var sequence = ref header.Sequences[state.Sequence];
		var events = header.Events[state.Sequence];

		if ( events.Length == 0 )
			return;

		var end = (float)Studio.BoneSetup.EstimateFrame( sequence, state, ClientTime, StudioInterpolation );
		var start = (float)(end - (state.FrameRate * (double)Sandbox.Time.Delta * sequence.Fps));

		if ( latched.SequenceTime == state.AnimTime && (sequence.Flags & Studio.SequenceFlags.Looping) != Studio.SequenceFlags.Looping )
			start = -0.01f;

		foreach ( ref readonly var studioEvent in events.AsSpan() )
		{
			if ( studioEvent.Index >= 5000 && start < studioEvent.Frame && studioEvent.Frame <= end )
				_clientDll.StudioEvent( studioEvent, attachments );
		}
	}

	private void TempEntUpdate()
	{
		_visibleTempEntities.Clear();

		MarkViewLeaves( _viewOrigin );

		_clientDll.TempEntUpdate( ClientTime - _oldTime, ClientTime, _moveVars.Gravity, ref _freeTempEntities, ref _activeTempEntities, AddVisibleEntity, TempEntPlaySound );
	}

	private bool AddVisibleEntity( TempEnt temp )
	{
		if ( temp.Model is not { } model )
			return false;

		if ( !_clientDll.AddEntity( ClientDll.EntityTypeTempEntity, null, 0, model.Name ) )
			return false;

		if ( !BoxInViewPvs( temp.Origin + model.Mins, temp.Origin + model.Maxs ) )
			return false;

		_visibleTempEntities.Add( temp );

		return true;
	}

	private void TempEntPlaySound( TempEnt temp, float damp )
	{
		string[] sounds;
		var volume = 0.8f;
		var shell = false;
		var chance = 5;

		switch ( temp.HitSound )
		{
			case TempEnt.BounceGlass:
				sounds = GlassSounds;
				break;
			case TempEnt.BounceMetal:
				sounds = MetalSounds;
				break;
			case TempEnt.BounceFlesh:
				sounds = FleshSounds;
				break;
			case TempEnt.BounceWood:
				sounds = WoodSounds;
				break;
			case TempEnt.BounceShrap:
				sounds = RicochetSounds;
				break;
			case TempEnt.BounceShell:
				sounds = ShellSounds;
				shell = true;
				break;
			case TempEnt.BounceConcrete:
				sounds = ConcreteSounds;
				break;
			case TempEnt.BounceShotShell:
				sounds = ShotShellSounds;
				shell = true;
				volume = 0.5f;
				break;
			default:
				return;
		}

		var speed = Math.Abs( (int)temp.Velocity.z );

		if ( shell )
		{
			chance = 3;

			if ( speed >= 200 )
				chance = 0;
		}

		if ( chance != 0 && Server.RandomLong( 0, chance ) != 0 )
			return;

		if ( damp <= 0f )
			return;

		volume *= MathF.Min( 1f, speed / (shell ? 350f : 450f) );

		var pitch = Server.RandomLong( 0, 3 ) != 0 || shell ? 100 : Server.RandomLong( 90, 124 );

		Sound.StartDynamicSound( -1, (int)SoundChannel.Auto, Sound.PrecacheSound( sounds[Server.RandomLong( 0, sounds.Length - 1 )] ), temp.Origin, volume, 1f, 0, pitch );
	}

	private void DrawTempEntities()
	{
		for ( var i = 0; i < _visibleTempEntities.Count; i++ )
			DrawTempEntity( i );

		for ( var i = _visibleTempEntities.Count; i < _tempEntityRenderers.Count; i++ )
			_tempEntityRenderers[i].Hide();
	}

	private void DrawTempEntity( int index )
	{
		while ( _tempEntityRenderers.Count <= index )
		{
			var renderer = new GameObject( true, "tempent" ).AddComponent<ClientModelRenderer>();

			renderer.GameObject.NetworkMode = NetworkMode.Never;

			_tempEntityRenderers.Add( renderer );
		}

		_tempEntityRenderers[index].Draw( this, _visibleTempEntities[index] );
	}
}
