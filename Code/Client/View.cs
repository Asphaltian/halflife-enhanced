using System;
using GoldSrc;
using GoldSrc.Bsp;

namespace HalfLife;

partial class HalfLifeClientDll
{
	[ConVar( "scr_ofsx" )]
	public static float OffsetX { get; set; }

	[ConVar( "scr_ofsy" )]
	public static float OffsetY { get; set; }

	[ConVar( "scr_ofsz" )]
	public static float OffsetZ { get; set; }

	[ConVar( "v_centermove" )]
	public static float CenterMove { get; set; } = 0.15f;

	[ConVar( "v_centerspeed" )]
	public static float CenterSpeed { get; set; } = 500f;

	[ConVar( "cl_chasedist" )]
	public static float ChaseDist { get; set; } = 112f;

	[ConVar( "cl_bobcycle" )]
	public static float BobCycle { get; set; } = 0.8f;

	[ConVar( "cl_vsmoothing", ConVarFlags.Saved )]
	public static float ViewSmoothing { get; set; } = 0.05f;

	[ConVar( "cl_bob", ConVarFlags.Saved )]
	public static float Bob { get; set; } = 0.01f;

	[ConVar( "cl_bobup" )]
	public static float BobUp { get; set; } = 0.5f;

	[ConVar( "cl_waterdist" )]
	public static float WaterDist { get; set; } = 4f;

	private const float IdleYawCycle = 2f;
	private const float IdleRollCycle = 0.5f;
	private const float IdlePitchCycle = 1f;
	private const float IdleYawLevel = 0.3f;
	private const float IdleRollLevel = 0.1f;
	private const float IdlePitchLevel = 0.3f;
	private const float RagdollViewHeight = 14f;
	private const float RagdollViewDistance = 96f;
	private const float RagdollViewWallOffset = 6f;

	public float IdleScale { get; set; }

	private Vector3 _eventPunchAngle;
	private double _bobTime;
	private float _bob;
	private float _bobLastTime;
	private const int OriginBackup = 64;
	private const int OriginMask = OriginBackup - 1;

	private float _oldZ;
	private float _lastTime;
	private readonly Vector3[] _viewInterpOrigins = new Vector3[OriginBackup];
	private readonly float[] _viewInterpOriginTime = new float[OriginBackup];
	private int _viewInterpCurrentOrigin;
	private Vector3 _viewInterpLastOrigin;

	public override void CalcRefdef( ref RefParams refParams )
	{
		if ( refParams.Intermission )
			CalcIntermissionRefdef( ref refParams );
		else if ( refParams.Spectator || User1 != 0 )
			CalcSpectatorRefdef( ref refParams );
		else if ( !refParams.Paused )
			CalcNormalRefdef( ref refParams );
	}

	private void CalcIntermissionRefdef( ref RefParams refParams )
	{
		refParams.ViewOrigin = refParams.SimOrigin;
		refParams.ViewAngles = refParams.ClientViewAngles;

		Engine.ViewModel.NoModel = true;

		var old = IdleScale;

		IdleScale = 1f;

		AddIdle( ref refParams );

		if ( Engine.IsSpectateOnly )
		{
			refParams.ViewOrigin = _hud.Spectator.CameraOrigin;
			refParams.ViewAngles = _hud.Spectator.CameraAngles;
		}

		IdleScale = old;

		LastClientAngles = refParams.ClientViewAngles;
		LastViewOrigin = refParams.ViewOrigin;
		LastViewAngles = refParams.ViewAngles;
	}

	private float CalcBob( in RefParams refParams )
	{
		if ( refParams.Time == _bobLastTime )
			return _bob;

		_bobLastTime = refParams.Time;
		_bobTime += refParams.FrameTime;

		var cycle = (float)(_bobTime - ((int)(_bobTime / BobCycle) * BobCycle));
		cycle /= BobCycle;

		if ( cycle < BobUp )
			cycle = (float)(Math.PI * cycle / BobUp);
		else
			cycle = (float)(Math.PI + (Math.PI * (cycle - BobUp) / (1.0 - BobUp)));

		var velocity = refParams.SimVelocity;

		_bob = (float)Math.Sqrt( (velocity.x * velocity.x) + (velocity.y * velocity.y) ) * Bob;
		_bob = (float)((_bob * 0.3) + (_bob * 0.7 * Math.Sin( cycle )));
		_bob = MathF.Min( _bob, 4f );
		_bob = MathF.Max( _bob, -7f );

		return _bob;
	}

	private void CalcViewRoll( ref RefParams refParams )
	{
		var side = CalcRoll( refParams.EntityAngles, refParams.SimVelocity, refParams.MoveVars.RollAngle, refParams.MoveVars.RollSpeed );

		refParams.ViewAngles.z += side;

		if ( refParams.Health <= 0 && refParams.ViewHeight.z != 0f && Engine.LocalRagdollOrigin is null )
			refParams.ViewAngles.z = 80f;
	}

	private static float CalcRoll( Vector3 angles, Vector3 velocity, float rollAngle, float rollSpeed )
	{
		MathLib.AngleVectors( angles, out _, out var right, out _ );

		var side = Vector3.Dot( velocity, right );
		var sign = side < 0f ? -1f : 1f;
		side = MathF.Abs( side );

		side = side < rollSpeed ? side * rollAngle / rollSpeed : rollAngle;

		return side * sign;
	}

	private void AddIdle( ref RefParams refParams )
	{
		refParams.ViewAngles.z += (float)(IdleScale * Math.Sin( refParams.Time * IdleRollCycle ) * IdleRollLevel);
		refParams.ViewAngles.x += (float)(IdleScale * Math.Sin( refParams.Time * IdlePitchCycle ) * IdlePitchLevel);
		refParams.ViewAngles.y += (float)(IdleScale * Math.Sin( refParams.Time * IdleYawCycle ) * IdleYawLevel);
	}

	private void CalcGunAngle( ref RefParams refParams )
	{
		refParams.ViewModelAngles.y = refParams.ViewAngles.y + refParams.CrosshairAngle.y;
		refParams.ViewModelAngles.x = (float)(-refParams.ViewAngles.x + (refParams.CrosshairAngle.x * 0.25));

		refParams.ViewModelAngles.z -= (float)(IdleScale * Math.Sin( refParams.Time * IdleRollCycle ) * IdleRollLevel);
		refParams.ViewModelAngles.x -= (float)(IdleScale * Math.Sin( refParams.Time * IdlePitchCycle ) * (IdlePitchLevel * 0.5));
		refParams.ViewModelAngles.y -= (float)(IdleScale * Math.Sin( refParams.Time * IdleYawCycle ) * IdleYawLevel);
	}

	private float _pitchVelocity;
	private bool _noDrift;
	private float _driftMove;
	private double _lastStop = -1.0;
	private float _lastForwardMove;

	private void StartPitchDrift()
	{
		if ( _lastStop == Engine.ClientTime )
			return;

		if ( _noDrift || _pitchVelocity == 0f )
		{
			_pitchVelocity = CenterSpeed;
			_noDrift = false;
			_driftMove = 0f;
		}
	}

	private void StopPitchDrift()
	{
		_lastStop = Engine.ClientTime;
		_noDrift = true;
		_pitchVelocity = 0f;
	}

	private void DriftPitch( ref RefParams refParams )
	{
		if ( refParams.NoClip || !refParams.OnGround || refParams.DemoPlayback || refParams.Spectator )
		{
			_driftMove = 0f;
			_pitchVelocity = 0f;
			return;
		}

		if ( _noDrift )
		{
			if ( CenterMove > 0f && (_mouseLook.State & 1) == 0 )
			{
				if ( MathF.Abs( _lastForwardMove ) < ForwardSpeed )
					_driftMove = 0f;
				else
					_driftMove += refParams.FrameTime;

				if ( _driftMove > CenterMove )
					StartPitchDrift();
				else
					return;
			}

			return;
		}

		var delta = refParams.IdealPitch - refParams.ClientViewAngles.x;

		if ( delta == 0f )
		{
			_pitchVelocity = 0f;
			return;
		}

		var move = refParams.FrameTime * _pitchVelocity;

		_pitchVelocity *= 1f + (refParams.FrameTime * 0.25f);

		if ( delta > 0f )
		{
			if ( move > delta )
			{
				_pitchVelocity = 0f;
				move = delta;
			}

			refParams.ClientViewAngles.x += move;
		}
		else if ( delta < 0f )
		{
			if ( move > -delta )
			{
				_pitchVelocity = 0f;
				move = -delta;
			}

			refParams.ClientViewAngles.x -= move;
		}
	}

	private void CalcNormalRefdef( ref RefParams refParams )
	{
		DriftPitch( ref refParams );

		var bob = CalcBob( refParams );

		refParams.ViewOrigin = refParams.SimOrigin;
		refParams.ViewOrigin.z += bob;
		refParams.ViewOrigin += refParams.ViewHeight;

		refParams.ViewAngles = refParams.ClientViewAngles;

		Engine.CalcShake();
		Engine.ApplyShake( ref refParams.ViewOrigin, ref refParams.ViewAngles, 1f );

		refParams.ViewOrigin += (float)(1.0 / 32);

		var waterOffset = 0f;

		if ( refParams.WaterLevel >= 2 )
		{
			var waterDist = (int)WaterDist;

			if ( refParams.WaterEntityScale( refParams.SimOrigin ) is { } scale )
				waterDist += (int)(scale * 16f);

			var point = refParams.ViewOrigin;

			if ( refParams.WaterLevel == 2 )
			{
				point.z -= waterDist;

				for ( var i = 0; i < waterDist; i++ )
				{
					if ( refParams.PointContents( point ) > (int)Contents.Water )
						break;

					point.z += 1f;
				}

				waterOffset = point.z + waterDist - refParams.ViewOrigin.z;
			}
			else
			{
				point.z += waterDist;

				for ( var i = 0; i < waterDist; i++ )
				{
					if ( refParams.PointContents( point ) <= (int)Contents.Water )
						break;

					point.z -= 1f;
				}

				waterOffset = point.z - waterDist - refParams.ViewOrigin.z;
			}
		}

		refParams.ViewOrigin.z += waterOffset;

		CalcViewRoll( ref refParams );
		AddIdle( ref refParams );

		MathLib.AngleVectors( refParams.ClientViewAngles, out var forward, out var right, out var up );

		refParams.Forward = forward;
		refParams.Right = right;
		refParams.Up = up;

		if ( refParams.MaxClients <= 1 )
			refParams.ViewOrigin += (OffsetX * forward) + (OffsetY * right) + (OffsetZ * up);

		var camAngles = default( Vector3 );

		if ( IsThirdPerson() )
		{
			camAngles = new Vector3( _camOfs.x, _camOfs.y, 0f );

			MathLib.AngleVectors( camAngles, out var camForward, out _, out _ );

			refParams.ViewOrigin += -_camOfs.z * camForward;
		}

		refParams.ViewModelAngles = refParams.ClientViewAngles;

		CalcGunAngle( ref refParams );

		refParams.ViewModelOrigin = refParams.SimOrigin;
		refParams.ViewModelOrigin.z += waterOffset;
		refParams.ViewModelOrigin += refParams.ViewHeight;

		Engine.ApplyShake( ref refParams.ViewModelOrigin, ref refParams.ViewModelAngles, 0.9f );

		refParams.ViewModelOrigin += bob * 0.4f * forward;
		refParams.ViewModelOrigin.z += bob;

		refParams.ViewModelAngles.y -= bob * 0.5f;
		refParams.ViewModelAngles.z -= bob * 1f;
		refParams.ViewModelAngles.x -= bob * 0.3f;

		refParams.ViewModelOrigin.z -= 1f;

		if ( refParams.ViewSize == 110f )
			refParams.ViewModelOrigin.z += 1f;
		else if ( refParams.ViewSize == 100f )
			refParams.ViewModelOrigin.z += 2f;
		else if ( refParams.ViewSize == 90f )
			refParams.ViewModelOrigin.z += 1f;
		else if ( refParams.ViewSize == 80f )
			refParams.ViewModelOrigin.z += 0.5f;

		refParams.ViewAngles += refParams.PunchAngle;
		refParams.ViewAngles += _eventPunchAngle;

		DropPunchAngle( refParams.FrameTime, ref _eventPunchAngle );

		if ( !refParams.Smoothing && refParams.OnGround && refParams.SimOrigin.z - _oldZ > 0f )
		{
			var stepTime = refParams.Time - _lastTime;
			if ( stepTime < 0f )
				stepTime = 0f;

			_oldZ += stepTime * 150f;

			if ( _oldZ > refParams.SimOrigin.z )
				_oldZ = refParams.SimOrigin.z;

			if ( refParams.SimOrigin.z - _oldZ > 18f )
				_oldZ = refParams.SimOrigin.z - 18f;

			refParams.ViewOrigin.z += _oldZ - refParams.SimOrigin.z;
			refParams.ViewModelOrigin.z += _oldZ - refParams.SimOrigin.z;
		}
		else
		{
			_oldZ = refParams.SimOrigin.z;
		}

		if ( MathLib.Length( refParams.SimOrigin - _viewInterpLastOrigin ) != 0f )
		{
			_viewInterpOrigins[_viewInterpCurrentOrigin & OriginMask] = refParams.SimOrigin;
			_viewInterpOriginTime[_viewInterpCurrentOrigin & OriginMask] = refParams.Time;
			_viewInterpCurrentOrigin++;

			_viewInterpLastOrigin = refParams.SimOrigin;
		}

		if ( ViewSmoothing != 0f && refParams.Smoothing && refParams.MaxClients > 1 )
		{
			if ( ViewSmoothing < 0f )
				ViewSmoothing = 0f;

			var t = refParams.Time - ViewSmoothing;
			var foundIndex = 0;
			int i;

			for ( i = 1; i < OriginMask; i++ )
			{
				foundIndex = _viewInterpCurrentOrigin - 1 - i;

				if ( _viewInterpOriginTime[foundIndex & OriginMask] <= t )
					break;
			}

			if ( i < OriginMask && _viewInterpOriginTime[foundIndex & OriginMask] != 0f )
			{
				var dt = _viewInterpOriginTime[(foundIndex + 1) & OriginMask] - _viewInterpOriginTime[foundIndex & OriginMask];

				if ( dt > 0f )
				{
					var frac = MathF.Min( 1f, (t - _viewInterpOriginTime[foundIndex & OriginMask]) / dt );
					var delta = _viewInterpOrigins[(foundIndex + 1) & OriginMask] - _viewInterpOrigins[foundIndex & OriginMask];
					var newOrigin = _viewInterpOrigins[foundIndex & OriginMask] + (frac * delta);

					if ( MathLib.Length( delta ) < 64f )
					{
						delta = newOrigin - refParams.SimOrigin;

						refParams.SimOrigin += delta;
						refParams.ViewOrigin += delta;
						refParams.ViewModelOrigin += delta;
					}
				}
			}
		}

		LastViewAngles = refParams.ViewAngles;
		_lastChaseAngles = refParams.ViewAngles;

		if ( IsThirdPerson() )
		{
			refParams.ViewAngles = camAngles;

			var pitch = camAngles.x;

			if ( pitch > 180f )
				pitch -= 360f;
			else if ( pitch < -180f )
				pitch += 360f;

			pitch /= -3f;

			(Engine.IsSpectateOnly ? Engine.GetEntityByIndex( User2 ) : Engine.GetLocalPlayer())?.SetPitch( pitch );
		}

		if ( refParams.ViewEntity > refParams.MaxClients && Engine.GetEntityByIndex( refParams.ViewEntity ) is { } viewEntity )
		{
			refParams.ViewOrigin = viewEntity.Origin;
			refParams.ViewAngles = viewEntity.Angles;

			LastViewAngles = refParams.ViewAngles;
		}

		if ( refParams.Health <= 0 && Engine.LocalRagdollOrigin is { } ragdollOrigin )
			CalcRagdollRefdef( ref refParams, ragdollOrigin );

		_lastTime = refParams.Time;

		LastViewOrigin = refParams.ViewOrigin;
	}

	private void CalcRagdollRefdef( ref RefParams refParams, Vector3 origin )
	{
		origin.z += RagdollViewHeight;

		MathLib.AngleVectors( refParams.ViewAngles, out var forward, out _, out _ );

		refParams.ViewOrigin = Engine.TraceWorldBox( origin, origin - (forward * RagdollViewDistance), RagdollViewWallOffset );
	}

	private static void DropPunchAngle( float frameTime, ref Vector3 punchAngle )
	{
		var length = MathLib.VectorNormalize( ref punchAngle );

		length = (float)(length - ((10.0 + (length * 0.5)) * frameTime));
		length = MathF.Max( length, 0f );

		punchAngle *= length;
	}

	public void PunchAxis( int axis, float punch )
	{
		_eventPunchAngle[axis] = punch;
	}
}
