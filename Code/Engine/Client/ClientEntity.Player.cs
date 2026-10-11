using System;
using GoldSrc.Studio;

namespace GoldSrc;

partial class ClientEntity
{
	private const int HistoryMax = 64;
	private const int HistoryMask = HistoryMax - 1;
	private const double MaxInterpolationFraction = 1.2;
	private const byte CenterController = 127;

	private struct PositionHistory
	{
		public float AnimTime;
		public Vector3 Origin;
		public Vector3 Angles;
	}

	private readonly PositionHistory[] _positionHistory = new PositionHistory[HistoryMax];
	private readonly BoneOutput _weaponBoneOutput = new();
	private readonly Vector3[] _weaponAttachments = new Vector3[4];

	private int _currentPosition;
	private SkinnedModelRenderer _weaponRenderer;
	private Model _weaponModel;
	private float _gaitMovement;

	private void UpdatePositions()
	{
		_currentPosition = (_currentPosition + 1) & HistoryMask;

		_positionHistory[_currentPosition] = new PositionHistory { AnimTime = CurState.AnimTime, Origin = CurState.Origin, Angles = CurState.Angles };
	}

	private void ResetPositions()
	{
		var store = _positionHistory[_currentPosition];

		_currentPosition = 1;

		Array.Clear( _positionHistory );

		_positionHistory[1] = store;
		_positionHistory[0] = store;
	}

	internal void SetLocalPlayerOrigin( Vector3 origin )
	{
		Origin = origin;
		Angles.x = CurState.Angles.x;
		Angles.y = CurState.Angles.y;
		CurState.Origin = origin;
		PrevState.Origin = origin;
		Angles.z = CurState.Angles.z;
	}

	internal void LinkPlayer( double messageTime )
	{
		if ( (CurState.Effects & (int)Effects.NoInterp) != 0 )
			ResetLatched( null, messageTime, false );

		if ( !Teleported() )
			return;

		_latched.PrevOrigin = CurState.Origin;
		_latched.PrevAngles = CurState.Angles;

		ResetPositions();
	}

	private void FindInterpolationUpdates( float targetTime, out int newer, out int older )
	{
		newer = _currentPosition & HistoryMask;

		var index = _currentPosition - 1;

		older = index & HistoryMask;

		if ( targetTime > _positionHistory[newer].AnimTime )
			return;

		for ( var i = 1; i < HistoryMax - 1; i++, index-- )
		{
			var animTime = _positionHistory[index & HistoryMask].AnimTime;

			if ( animTime == 0f )
				break;

			if ( animTime < targetTime )
			{
				newer = (index + 1) & HistoryMask;
				older = index & HistoryMask;
				break;
			}
		}
	}

	internal bool ComputeInterpolated( bool brush, bool brushInterpolation, int maxClients, bool movingGround, float targetTime )
	{
		Origin = CurState.Origin;
		Angles = CurState.Angles;

		if ( (brush && !brushInterpolation) || maxClients == 1 || movingGround )
			return true;

		FindInterpolationUpdates( targetTime, out var newer, out var older );

		ref var t1 = ref _positionHistory[newer];
		ref var t0 = ref _positionHistory[older];

		var elapsed = targetTime - t0.AnimTime;

		if ( elapsed < 0f )
			return false;

		if ( t0.AnimTime == 0f || (MathLib.VectorCompare( t0.Origin, Vector3.Zero ) && !MathLib.VectorCompare( t1.Origin, Vector3.Zero )) )
		{
			Origin = t1.Origin;
			Angles = t1.Angles;

			return false;
		}

		if ( t1.AnimTime == t0.AnimTime )
		{
			Origin = t1.Origin;
			Angles = t1.Angles;

			return true;
		}

		var fraction = elapsed / (t1.AnimTime - t0.AnimTime);

		if ( fraction < 0f )
			return false;

		if ( fraction > 1f )
			fraction = 1f;

		var angles = new Vector3();

		for ( var i = 0; i < 3; i++ )
		{
			var d = t1.Angles[i] - t0.Angles[i];

			if ( d > 180f )
				d -= 360f;
			else if ( d < -180f )
				d += 360f;

			angles[i] = (d * fraction) + t0.Angles[i];
		}

		Origin = t0.Origin + ((t1.Origin - t0.Origin) * fraction);
		Angles = Common.NormalizeAngles( angles );

		return true;
	}

	public void SetPitch( float pitch )
	{
		Angles.x = pitch;
		CurState.Angles.x = pitch;
		PrevState.Angles.x = pitch;
		_latched.PrevAngles.x = pitch;
	}

	internal void SetRenderTransform()
	{
		_renderAngles = Angles;

		WorldPosition = Origin;
		WorldRotation = Edict.ToRotation( _renderAngles );
	}

	internal void ComputePlayerOrigin( float targetTime )
	{
		if ( !Player )
			return;

		FindInterpolationUpdates( targetTime, out var newer, out var older );

		ref var t1 = ref _positionHistory[newer];
		ref var t0 = ref _positionHistory[older];

		if ( t1.AnimTime == 0f )
		{
			Origin = t0.Origin;
			Angles = t0.Angles;
			return;
		}

		var delta = t1.Origin - t0.Origin;
		var fraction = 1f;

		if ( t1.AnimTime != t0.AnimTime )
		{
			fraction = (targetTime - t0.AnimTime) / (t1.AnimTime - t0.AnimTime);

			if ( fraction < 0f )
				fraction = 0f;
			else if ( fraction > MaxInterpolationFraction )
				fraction = (float)MaxInterpolationFraction;
		}

		Origin = t0.Origin + (delta * fraction);
		Angles = MathLib.InterpolateAngles( ref t0.Angles, ref t1.Angles, fraction );
	}

	private static void StudioPlayerBlend( in SequenceDesc sequence, out int blend, ref float pitch )
	{
		blend = (int)(pitch * 3f);

		if ( blend < sequence.BlendStart[0] )
		{
			pitch = (float)(pitch - (sequence.BlendStart[0] / 3.0));
			blend = 0;
		}
		else if ( blend > sequence.BlendEnd[0] )
		{
			pitch = (float)(pitch - (sequence.BlendEnd[0] / 3.0));
			blend = 255;
		}
		else
		{
			if ( sequence.BlendEnd[0] - sequence.BlendStart[0] < 0.1 )
				blend = 127;
			else
				blend = (int)(255 * (blend - sequence.BlendStart[0]) / (sequence.BlendEnd[0] - sequence.BlendStart[0]));

			pitch = 0f;
		}
	}

	private void StudioEstimateGait( Client client, PlayerInfo playerInfo )
	{
		var dt = Math.Clamp( client.ClientTime - client.OldClientTime, 0.0, 1.0 );

		if ( dt == 0 || playerInfo.RenderFrame == client.FrameCount )
		{
			_gaitMovement = 0f;
			return;
		}

		var velocity = Origin - playerInfo.PrevGaitOrigin;

		playerInfo.PrevGaitOrigin = Origin;

		_gaitMovement = MathLib.Length( velocity );

		if ( dt <= 0 || _gaitMovement / dt < 5 )
		{
			_gaitMovement = 0f;
			velocity.x = 0f;
			velocity.y = 0f;
		}

		if ( velocity.y == 0f && velocity.x == 0f )
		{
			var yawDiff = Angles.y - playerInfo.GaitYaw;

			yawDiff -= (int)(yawDiff / 360) * 360;

			if ( yawDiff > 180 )
				yawDiff -= 360;

			if ( yawDiff < -180 )
				yawDiff += 360;

			if ( dt < 0.25 )
				yawDiff = (float)(yawDiff * (dt * 4));
			else
				yawDiff = (float)(yawDiff * dt);

			playerInfo.GaitYaw += yawDiff;
			playerInfo.GaitYaw -= (int)(playerInfo.GaitYaw / 360) * 360;

			_gaitMovement = 0f;
		}
		else
		{
			playerInfo.GaitYaw = (float)(Math.Atan2( velocity.y, velocity.x ) * 180 / Math.PI);

			if ( playerInfo.GaitYaw > 180 )
				playerInfo.GaitYaw = 180;

			if ( playerInfo.GaitYaw < -180 )
				playerInfo.GaitYaw = -180;
		}
	}

	private void StudioProcessGait( Client client, Studio.File header, PlayerInfo playerInfo, ref EntityState playerState )
	{
		if ( CurState.Sequence >= header.Sequences.Length )
			CurState.Sequence = 0;

		var pitch = Angles.x;

		StudioPlayerBlend( header.Sequences[CurState.Sequence], out var blend, ref pitch );

		Angles.x = pitch;
		_latched.PrevAngles.x = Angles.x;
		CurState.Blending0 = (byte)blend;
		_latched.PrevBlending0 = CurState.Blending0;
		_latched.PrevSeqBlending0 = CurState.Blending0;

		var dt = Math.Clamp( client.ClientTime - client.OldClientTime, 0.0, 1.0 );

		StudioEstimateGait( client, playerInfo );

		var yaw = Angles.y - playerInfo.GaitYaw;

		yaw -= (int)(yaw / 360) * 360;

		if ( yaw < -180 )
			yaw += 360;

		if ( yaw > 180 )
			yaw -= 360;

		if ( yaw > 120 )
		{
			playerInfo.GaitYaw -= 180;
			_gaitMovement = -_gaitMovement;
			yaw -= 180;
		}
		else if ( yaw < -120 )
		{
			playerInfo.GaitYaw += 180;
			_gaitMovement = -_gaitMovement;
			yaw += 180;
		}

		var controller = (byte)(int)(((yaw / 4.0) + 30) / (60.0 / 255.0));

		CurState.Controller0 = controller;
		CurState.Controller1 = controller;
		CurState.Controller2 = controller;
		CurState.Controller3 = controller;

		CopyController( CurState, _latched.PrevController );

		Angles.y = playerInfo.GaitYaw;

		if ( Angles.y < -0 )
			Angles.y += 360;

		_latched.PrevAngles.y = Angles.y;

		if ( playerState.GaitSequence >= header.Sequences.Length )
			playerState.GaitSequence = 0;

		ref readonly var sequence = ref header.Sequences[playerState.GaitSequence];

		if ( sequence.LinearMovement.x > 0 )
			playerInfo.GaitFrame += _gaitMovement / sequence.LinearMovement.x * sequence.NumFrames;
		else
			playerInfo.GaitFrame = (float)(playerInfo.GaitFrame + (sequence.Fps * dt));

		playerInfo.GaitFrame -= (int)(playerInfo.GaitFrame / sequence.NumFrames) * sequence.NumFrames;

		if ( playerInfo.GaitFrame < 0 )
			playerInfo.GaitFrame += sequence.NumFrames;
	}

	private bool StudioDrawDeadPlayer( Studio.File header, bool entityModel, SkinnedModelRenderer renderer, int playerIndex )
	{
		var deadPlayer = Client.Current.CurrentFrame.PlayerState[playerIndex];

		deadPlayer.Number = playerIndex + 1;
		deadPlayer.WeaponModel = 0;
		deadPlayer.GaitSequence = 0;
		deadPlayer.MoveType = (int)MoveType.None;
		deadPlayer.Angles = CurState.Angles;
		deadPlayer.Origin = CurState.Origin;

		return StudioDrawPlayer( header, entityModel, renderer, playerIndex, ref deadPlayer, false, WorldPosition, _renderAngles );
	}

	private bool StudioDrawPlayer( Studio.File header, bool entityModel, SkinnedModelRenderer renderer, int playerIndex, ref EntityState playerState, bool doInterp, Vector3 basePosition, Vector3 baseAngles )
	{
		var client = Client.Current;
		var time = client.ClientTime;
		var playerInfo = client.GetPlayerInfo( playerIndex );

		if ( playerState.GaitSequence != 0 )
		{
			var originalAngles = Angles;

			StudioProcessGait( client, header, playerInfo, ref playerState );

			playerInfo.GaitSequence = playerState.GaitSequence;

			StudioSetUpTransform( time, Origin, Angles, doInterp );

			Angles = originalAngles;
		}
		else
		{
			CurState.Controller0 = CenterController;
			CurState.Controller1 = CenterController;
			CurState.Controller2 = CenterController;
			CurState.Controller3 = CenterController;

			CopyController( CurState, _latched.PrevController );

			playerInfo.GaitSequence = 0;

			StudioSetUpTransform( time, basePosition, baseAngles, doInterp );
		}

		if ( doInterp )
			_renderAngles = Angles;

		if ( !client.StudioCheckBBox( header, ref CurState, Origin, WorldTransform ) )
		{
			if ( _weaponRenderer is not null )
				_weaponRenderer.Enabled = false;

			return false;
		}

		var bones = client.BoneSetup;

		bones.SetupBones( header, ref CurState, ref _latched, time, doInterp, MouthOpen, playerInfo );

		playerInfo.RenderFrame = client.FrameCount;

		if ( Client.HighModels != 0f && !entityModel )
			CurState.Body = 255;

		if ( !(Host.Developer == 0f && client.MaxClients == 1) && entityModel )
			CurState.Body = 1;

		BoneOutput.SetBody( header, renderer, CurState.Body, CurState.Skin );

		_boneOutput.Apply( client, header, renderer, WorldTransform, _attachments, RenderFx, CurState.AnimTime );

		client.SaveBones( header );

		var effects = PacketEffects;

		client.StudioClientEvents( header, CurState, _latched, ref effects, _attachments );

		PacketEffects = effects;

		var light = client.StudioDynamicLight( (Effects)CurState.Effects, RenderFx, CurState.IUser4, Origin, header.Header.Flags );

		if ( light.FloorColor is { } floorColor )
			FloorColor = floorColor;

		StudioSetupLighting( client, renderer, light );

		StudioDrawWeapon( client, client.GetModelByIndex( playerState.WeaponModel ) as StudioModel, light, time );

		return true;
	}

	private void StudioSetupLighting( Client client, SkinnedModelRenderer renderer, in StudioLight light )
	{
		light.Apply( renderer.Attributes );

		client.StudioEntityLight( Index, WorldPosition, _attachments, renderer.Attributes );
	}

	private void StudioDrawWeapon( Client client, StudioModel weapon, in StudioLight light, double time )
	{
		if ( weapon is null )
		{
			if ( _weaponRenderer is not null )
				_weaponRenderer.Enabled = false;

			return;
		}

		if ( _weaponRenderer is null )
		{
			var gameObject = new GameObject( true, "weapon model" );

			gameObject.NetworkMode = NetworkMode.Never;
			gameObject.SetParent( GameObject, false );

			_weaponRenderer = gameObject.AddComponent<SkinnedModelRenderer>();
		}

		if ( weapon != _weaponModel )
		{
			_weaponModel = weapon;
			_weaponRenderer.Model = weapon.RenderModel;
		}

		_weaponRenderer.Enabled = true;
		_weaponRenderer.UseAnimGraph = false;

		var state = CurState;

		client.BoneSetup.MergeBones( weapon.File, ref state, time );

		for ( var i = 0; i < weapon.File.BodyParts.Length; i++ )
		{
			ref readonly var bodyPart = ref weapon.File.BodyParts[i];

			_weaponRenderer.SetBodyGroup( i, CurState.Body / bodyPart.Base % bodyPart.NumModels );
		}

		if ( _weaponRenderer.HasMaterialGroups )
			_weaponRenderer.MaterialGroup = $"Skin {CurState.Skin}";

		_weaponRenderer.Attributes.SetCombo( "D_RENDER_MODE", (int)RenderMode );
		_weaponRenderer.Attributes.Set( "Blend", _renderer.Attributes.GetFloat( "Blend" ) );
		_weaponRenderer.Attributes.Set( "RenderColor", new Vector3( CurState.RenderColorR, CurState.RenderColorG, CurState.RenderColorB ) );

		StudioSetupLighting( client, _weaponRenderer, light );

		_weaponBoneOutput.Apply( client, weapon.File, _weaponRenderer, WorldTransform, _weaponAttachments, RenderFx, CurState.AnimTime, true );
	}
}
