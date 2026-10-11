using System;
using GoldSrc.Studio;

namespace GoldSrc;

public sealed partial class ClientEntity : Component
{
	private const Effects StickyEffects = Effects.MuzzleFlash | Effects.NoInterp;
	private const float TeleportDistance = 128f;

	public int Index { get; internal set; }

	public bool Player { get; internal set; }

	public EntityState PrevState;
	public EntityState CurState;
	public EntityState PacketState;

	public Vector3 Origin;
	public Vector3 Angles;

	public Model Model;

	internal bool ClientOwned { get; set; }

	public float LastMove;

	public Vector3Int FloorColor;

	public byte MouthOpen { get; internal set; }
	public byte MouthSoundCount { get; internal set; }
	public int MouthSoundAverage { get; internal set; }

	public Effects PacketEffects { get; internal set; }

	internal bool Visible { get; set; }

	private readonly Vector3[] _attachments = new Vector3[4];
	private readonly BoneOutput _boneOutput = new();
	private readonly SpriteNextView _spriteNextView = new();

	private LatchedVars _latched = new() { PrevController = new byte[4] };
	private ModelRenderer _renderer;
	private Model _renderedModel;
	private Vector3 _previousOrigin;
	private Vector3 _previousAngles;
	private Vector3 _renderAngles;
	private bool _hasPrevious;

	public int ModelIndex => CurState.ModelIndex;

	public int EFlags => CurState.EFlags;

	public RenderMode RenderMode => (RenderMode)CurState.RenderMode;

	public RenderFx RenderFx => (RenderFx)CurState.RenderFx;

	internal Vector3 RenderAngles => _renderAngles;

	internal Vector3 TrailOrigin => _latched.PrevOrigin;

	internal bool Drawn => _renderer is { Enabled: true };

	public Vector3 GetAttachmentPoint( int attachment )
	{
		return attachment != 0 ? _attachments[attachment - 1] : WorldPosition;
	}

	internal void CopyAttachments( Vector3[] attachments ) => attachments.CopyTo( _attachments, 0 );

	internal void SetAttachments( Vector3 origin ) => Array.Fill( _attachments, origin );

	public void Clear()
	{
		PrevState = default;
		CurState = default;
		Origin = default;
		Angles = default;
		Model = null;
	}

	internal void ClearStickyEffects() => PacketEffects &= ~StickyEffects;

	internal void ProcessUpdate( in EntityState state, bool player, bool newEntity, bool continuous, Studio.File model, bool brush, double time )
	{
		_previousOrigin = Origin;
		_previousAngles = Angles;
		_hasPrevious = continuous && !newEntity;

		Player = player;
		PacketState = state;
		PrevState = CurState;
		CurState = state;
		Index = state.Number;

		PacketEffects = (Effects)state.Effects | (PacketEffects & StickyEffects);

		CurState.Angles = Common.NormalizeAngles( CurState.Angles );

		if ( brush || ((MoveType)CurState.MoveType is not (MoveType.Step or MoveType.None or MoveType.Walk or MoveType.Fly) && CurState.StartTime == 0f && CurState.ImpactTime == 0f) )
			CurState.AnimTime = CurState.MsgTime;

		if ( Math.Abs( (int)(PrevState.AnimTime * -1000.0) - (int)(CurState.AnimTime * -1000.0) ) > 1 || CurState.MoveType == (int)MoveType.None )
		{
			UpdateLatchedVars();
			UpdatePositions();
		}

		if ( player )
			CurState.Angles.x = (float)(CurState.Angles.x / -3.0);

		Origin = CurState.Origin;

		if ( CurState.StartTime != 0f && CurState.ImpactTime != 0f )
			ParametricMove( time );

		Angles = CurState.Angles;

		SetAttachments( Origin );

		if ( !newEntity )
			return;

		_latched.PrevOrigin = Origin;
		_latched.PrevAngles = Angles;

		ResetLatched( model, CurState.MsgTime, true );
		ResetPositions();

		if ( CurState.StartTime != 0f && CurState.ImpactTime != 0f )
			LastMove = (float)time;
	}

	internal void Link( Studio.File model, bool brush, double time, double messageTime, bool fixModelInterpolation )
	{
		CurState = PacketState;

		var parametric = CurState.ImpactTime != 0f && CurState.StartTime != 0f;

		if ( !parametric )
		{
			if ( CurState.AnimTime == PrevState.AnimTime && !MathLib.VectorCompare( CurState.Origin, PrevState.Origin ) )
				LastMove = (float)(time + 0.2);

			if ( (CurState.EFlags & EntityState.EFlagSlerp) != 0 && CurState.AnimTime != 0f && model is not null )
			{
				if ( time <= LastMove )
				{
					var animTime = CurState.AnimTime;

					CurState.MoveType = (int)MoveType.None;

					ResetLatched( model, messageTime, true );

					if ( fixModelInterpolation )
					{
						CurState.AnimTime = animTime;
						_latched.PrevAnimTime = animTime;
					}

					_latched.PrevOrigin = CurState.Origin;
					_latched.PrevAngles = CurState.Angles;
				}
				else
				{
					CurState.MoveType = (int)MoveType.Step;
				}
			}
		}

		if ( !brush && parametric )
			ParametricMove( time );

		Origin = CurState.Origin;
		Angles = CurState.Angles;

		if ( (CurState.EntityType & EntityState.EntityNormal) == 0 )
		{
			CurState.MoveType = CurState.ModelIndex;

			_latched.PrevSequence = CurState.Sequence;

			PrevState = CurState;

			_latched.PrevOrigin = Origin;
			_latched.PrevAngles = Angles;

			return;
		}

		if ( CurState.AimEnt != 0 )
			CurState.MoveType = (int)MoveType.Follow;

		if ( (((Effects)CurState.Effects | PacketEffects) & Effects.NoInterp) == Effects.NoInterp )
			ResetLatched( model, messageTime, false );

		if ( Teleported() )
		{
			_latched.PrevOrigin = CurState.Origin;
			_latched.PrevAngles = CurState.Angles;
		}
	}

	private bool Teleported()
	{
		var delta = CurState.Origin - PrevState.Origin;

		return delta.x > TeleportDistance || delta.y > TeleportDistance || delta.z > TeleportDistance;
	}

	private void ParametricMove( double time )
	{
		var delta = CurState.EndPos - CurState.StartPos;
		var duration = CurState.ImpactTime - CurState.StartTime;

		if ( duration != 0f )
		{
			var now = time < LastMove ? LastMove : (float)time;
			var fraction = Math.Clamp( (now - CurState.StartTime) / duration, 0f, 1f );

			CurState.Origin = CurState.StartPos + (delta * fraction);

			LastMove = now;
		}

		Origin = CurState.Origin;

		MathLib.VectorNormalize( ref delta );

		if ( MathLib.Length( delta ) > 0f )
			CurState.Angles = MathLib.VectorAngles( delta );
	}

	internal Vector3 InterpolatedOrigin( float fraction )
	{
		return !_hasPrevious || (PacketEffects & Effects.NoInterp) == Effects.NoInterp ? Origin : Vector3.Lerp( _previousOrigin, Origin, fraction );
	}

	internal void Interpolate( float fraction )
	{
		if ( !_hasPrevious || (PacketEffects & Effects.NoInterp) == Effects.NoInterp )
			fraction = 1f;

		_renderAngles = Edict.LerpAngles( _previousAngles, Angles, fraction );

		WorldPosition = Vector3.Lerp( _previousOrigin, Origin, fraction );
		WorldRotation = Edict.ToRotation( _renderAngles );
	}

	private void UpdateLatchedVars()
	{
		_latched.PrevOrigin = PrevState.Origin;
		_latched.PrevAngles = PrevState.Angles;
		_latched.PrevAnimTime = PrevState.AnimTime;

		if ( CurState.Sequence != PrevState.Sequence )
		{
			_latched.SequenceTime = CurState.AnimTime;
			_latched.PrevSequence = PrevState.Sequence;
			_latched.PrevSeqBlending0 = PrevState.Blending0;
			_latched.PrevSeqBlending1 = PrevState.Blending1;
		}

		CopyController( PrevState, _latched.PrevController );

		_latched.PrevBlending0 = PrevState.Blending0;
		_latched.PrevBlending1 = PrevState.Blending1;
	}

	private void ResetLatched( Studio.File model, double messageTime, bool full )
	{
		if ( full )
		{
			CopyController( CurState, _latched.PrevController );

			_latched.PrevBlending0 = CurState.Blending0;
			_latched.PrevBlending1 = CurState.Blending1;
			_latched.SequenceTime = CurState.AnimTime;

			_latched.PrevFrame = model is not null && CurState.Sequence < model.Sequences.Length
				? (float)BoneSetup.EstimateFrame( model.Sequences[CurState.Sequence], CurState, Client.Current.ClientTime, Client.StudioInterpolation )
				: 0f;

			PrevState = CurState;
		}

		_latched.PrevSequence = CurState.Sequence;
		_latched.PrevOrigin = CurState.Origin;
		_latched.PrevAngles = CurState.Angles;
		CurState.AnimTime = (float)messageTime;
		_latched.PrevAnimTime = (float)messageTime;
	}

	private static void CopyController( in EntityState state, byte[] controller )
	{
		controller[0] = state.Controller0;
		controller[1] = state.Controller1;
		controller[2] = state.Controller2;
		controller[3] = state.Controller3;
	}

	protected override void OnPreRender()
	{
		if ( !Visible )
		{
			Draw();
			return;
		}

		_boneOutput.UpdateBounds( _renderer as SkinnedModelRenderer );
		_weaponBoneOutput.UpdateBounds( _weaponRenderer );
	}

	internal void Draw()
	{
		var client = Client.Current;

		if ( !client.InLevel || !Scene.Camera.IsValid() )
			return;

		var model = Model ?? client.GetModelByIndex( ModelIndex );
		var deadPlayer = RenderFx == RenderFx.DeadPlayer;
		var playerIndex = deadPlayer ? CurState.RenderAmt - 1 : Index - 1;
		var player = (deadPlayer || Player) && playerIndex >= 0 && playerIndex < client.MaxClients;

		if ( deadPlayer && !player )
			model = null;

		if ( player && model is not null )
			model = client.SetupPlayerModel( playerIndex, model );

		var studio = (model as StudioModel)?.File;
		var sprite = model as SpriteModel;

		if ( model != _renderedModel )
		{
			_renderedModel = model;

			if ( _renderer is not null && _renderer is SkinnedModelRenderer != studio is not null )
			{
				_renderer.Destroy();
				_renderer = null;
			}

			_renderer ??= studio is null ? AddComponent<ModelRenderer>() : AddComponent<SkinnedModelRenderer>();
			_renderer.Model = sprite is null ? model?.RenderModel : client.SpriteQuad;
			_renderer.MaterialOverride = null;

			WorldScale = Vector3.One;

			GameObject.Tags.Set( Client.FirstViewTag, sprite is not null );
		}

		if ( _renderer is null )
			return;

		var blend = RenderMode == RenderMode.Normal ? 255 : FxBlend();

		var enabled = ((Index == 0 && !ClientOwned) || Visible) && (ModelIndex != 0 || Model is not null) && blend > 0 && model is not null;

		if ( sprite is null || !enabled )
		{
			_renderer.Enabled = enabled;
			_spriteNextView.Hide();
		}

		if ( Index != 0 )
			client.SetDecalState( Index, enabled, RenderMode, blend / 255f, WorldTransform );

		if ( _weaponRenderer is not null && (!enabled || !player) )
			_weaponRenderer.Enabled = false;

		if ( !enabled )
			return;

		if ( RenderMode == RenderMode.Glow && sprite is null )
			Log.Info( "Non-sprite set to glow!" );

		if ( sprite is not null )
		{
			var spriteState = SpriteDrawState();

			_spriteNextView.Draw( client, sprite, spriteState, blend * 0.003921569f );

			_renderer.Enabled = SpriteDraw.DrawModel( Scene.Camera, sprite, _renderer, spriteState, blend * 0.003921569f );
			_renderer.Attributes.SetCombo( "D_RENDER_MODE", (int)RenderMode );

			if ( _renderer.SceneObject is { } spriteObject )
			{
				spriteObject.Flags.IsTranslucent = true;
				spriteObject.Flags.IsOpaque = false;
				spriteObject.Flags.WantsPrePass = false;
			}

			return;
		}

		_renderer.Attributes.SetCombo( "D_RENDER_MODE", (int)RenderMode );
		_renderer.Attributes.Set( "Blend", blend / 255f );
		_renderer.Attributes.Set( "RenderColor", new Vector3( CurState.RenderColorR, CurState.RenderColorG, CurState.RenderColorB ) );
		_renderer.Attributes.Set( "Frame", CurState.Frame );
		_renderer.Attributes.Set( "WaveHeight", CurState.Scale );

		if ( client.GetBrushModel( ModelIndex ) is { } brush )
		{
			var angles = _renderAngles;

			_renderer.Attributes.Set( "TurbMins", angles.x == 0f && angles.y == 0f && angles.z == 0f ? WorldPosition.z + brush.Mins.z : WorldPosition.z - brush.Radius );
		}

		var drawn = true;

		if ( studio is not null && deadPlayer )
			drawn = StudioDrawDeadPlayer( studio, model == client.GetModelByIndex( ModelIndex ), (SkinnedModelRenderer)_renderer, playerIndex );
		else if ( studio is not null && player )
			drawn = StudioDrawPlayer( studio, model == client.GetModelByIndex( ModelIndex ), (SkinnedModelRenderer)_renderer, playerIndex, ref client.CurrentFrame.PlayerState[playerIndex], Client.StudioInterpolation, Origin, Angles );
		else if ( studio is not null )
			drawn = StudioDrawModel( studio, (SkinnedModelRenderer)_renderer );

		if ( _renderer.SceneObject is not { } sceneObject )
			return;

		if ( studio is not null )
			sceneObject.RenderingEnabled = drawn;

		var translucent = RenderMode is RenderMode.TransColor or RenderMode.TransTexture or RenderMode.Glow or RenderMode.TransAdd;

		sceneObject.Flags.IsTranslucent = translucent;
		sceneObject.Flags.IsOpaque = !translucent;
		sceneObject.Flags.WantsPrePass = !translucent;
	}

	private bool StudioDrawModel( Studio.File header, SkinnedModelRenderer renderer )
	{
		var client = Client.Current;
		var time = client.ClientTime;
		var position = WorldPosition;

		StudioSetUpTransform( time, position, _renderAngles, Client.StudioInterpolation );

		if ( !client.StudioCheckBBox( header, ref CurState, Origin, WorldTransform ) )
			return false;

		var light = client.StudioDynamicLight( (Effects)CurState.Effects, RenderFx, CurState.IUser4, CurState.MoveType == (int)MoveType.Step ? Origin : position, header.Header.Flags );

		if ( light.FloorColor is { } floorColor )
			FloorColor = floorColor;

		light.Apply( renderer.Attributes );

		client.StudioEntityLight( Index, position, _attachments, renderer.Attributes );

		var bones = client.BoneSetup;

		bones.SetupBones( header, ref CurState, ref _latched, time, Client.StudioInterpolation, MouthOpen );

		BoneOutput.SetBody( header, renderer, CurState.Body, CurState.Skin );

		_boneOutput.Apply( client, header, renderer, WorldTransform, _attachments, RenderFx, CurState.AnimTime );

		var effects = PacketEffects;

		client.StudioClientEvents( header, CurState, _latched, ref effects, _attachments );

		PacketEffects = effects;

		return true;
	}

	private void StudioSetUpTransform( double time, Vector3 modelPos, Vector3 entityAngles, bool doInterp )
	{
		var angles = CurState.Angles;

		if ( CurState.MoveType == (int)MoveType.Step )
		{
			var f = 0f;

			if ( time < CurState.AnimTime + 1f && CurState.AnimTime != _latched.PrevAnimTime )
				f = (float)((time - CurState.AnimTime) / (CurState.AnimTime - _latched.PrevAnimTime));

			f = doInterp ? (float)(f - 1.0) : 0f;

			modelPos = Origin + ((Origin - _latched.PrevOrigin) * f);

			for ( var i = 0; i < 3; i++ )
			{
				var d = Angles[i] - _latched.PrevAngles[i];

				if ( d > 180f )
					d -= 360f;
				else if ( d < -180f )
					d += 360f;

				angles[i] += d * f;
			}
		}
		else if ( CurState.MoveType != (int)MoveType.None )
		{
			angles = entityAngles;
		}

		angles.x = -angles.x;

		WorldPosition = modelPos;
		WorldRotation = Edict.ToRotation( angles );
	}

	private SpriteState SpriteDrawState()
	{
		return new SpriteState
		{
			Origin = CurState.Body != 0 && Client.Current.GetEntity( CurState.Skin ) is { } followed ? followed.GetAttachmentPoint( CurState.Body ) : WorldPosition,
			Angles = _renderAngles,
			Scale = CurState.Scale,
			Frame = CurState.Frame,
			RenderMode = RenderMode,
			RenderFx = RenderFx,
			RenderAmt = CurState.RenderAmt,
			RenderColor = new Vector3( CurState.RenderColorR, CurState.RenderColorG, CurState.RenderColorB )
		};
	}

	protected override void OnDestroy() => _spriteNextView.Destroy();

	public int FxBlend()
	{
		var time = Client.Current.ClientTime;
		var offset = Index * 363f;
		var renderAmt = CurState.RenderAmt;
		int blend;

		switch ( RenderFx )
		{
			case RenderFx.PulseSlow:
				blend = (int)(renderAmt + (0x10 * Math.Sin( (time * 2) + offset )));
				break;
			case RenderFx.PulseFast:
				blend = (int)(renderAmt + (0x10 * Math.Sin( (time * 8) + offset )));
				break;
			case RenderFx.PulseSlowWide:
				blend = (int)(renderAmt + (0x40 * Math.Sin( (time * 2) + offset )));
				break;
			case RenderFx.PulseFastWide:
				blend = (int)(renderAmt + (0x40 * Math.Sin( (time * 8) + offset )));
				break;
			case RenderFx.FadeSlow:
				blend = renderAmt > 0 ? renderAmt - 1 : 0;
				break;
			case RenderFx.FadeFast:
				blend = renderAmt > 3 ? renderAmt - 4 : 0;
				break;
			case RenderFx.SolidSlow:
				blend = renderAmt < 255 ? renderAmt + 1 : 255;
				break;
			case RenderFx.SolidFast:
				blend = renderAmt < 252 ? renderAmt + 4 : 255;
				break;
			case RenderFx.StrobeSlow:
				blend = (int)(20 * Math.Sin( (time * 4) + offset )) < 0 ? 0 : renderAmt;
				break;
			case RenderFx.StrobeFast:
				blend = (int)(20 * Math.Sin( (time * 16) + offset )) < 0 ? 0 : renderAmt;
				break;
			case RenderFx.StrobeFaster:
				blend = (int)(20 * Math.Sin( (time * 36) + offset )) < 0 ? 0 : renderAmt;
				break;
			case RenderFx.FlickerSlow:
				blend = (int)(20 * (Math.Sin( time * 2 ) + Math.Sin( (time * 17) + offset ))) < 0 ? 0 : renderAmt;
				break;
			case RenderFx.FlickerFast:
				blend = (int)(20 * (Math.Sin( time * 16 ) + Math.Sin( (time * 23) + offset ))) < 0 ? 0 : renderAmt;
				break;
			case RenderFx.Distort:
			case RenderFx.Hologram:
				var distance = 1f;

				if ( RenderFx != RenderFx.Distort )
				{
					var camera = Scene.Camera;
					var offsetFromView = WorldPosition - camera.WorldPosition;
					var forward = camera.WorldRotation.Forward;

					distance = (offsetFromView.y * forward.y) + (offsetFromView.x * forward.x) + (offsetFromView.z * forward.z);

					if ( distance <= 0f )
						return 0;
				}

				blend = distance > 100f ? (int)((1.0 - ((double)(distance - 100f) * 0.0025)) * 180.0) : 180;
				blend += Server.RandomLong( -32, 31 );
				break;
			default:
				blend = renderAmt;
				break;
		}

		return Math.Clamp( blend, 0, 255 );
	}
}
