using System;
using GoldSrc;
using GoldSrc.Bsp;

namespace HalfLife;

partial class PlayerMovement
{
	private const double TimeToDuck = 0.4;
	private const int DuckHullMin = -18;
	private const int HullMin = -36;
	private const int DuckView = 12;
	private const int View = 28;
	private const int MaxClimbSpeed = 200;
	private const int StuckMoveUp = 1;
	private const int ContentsLadder = -16;
	private const double CheckStuckMinTime = 0.05;
	private const float BunnyJumpMaxSpeedFactor = 1.7f;
	private const int WaterJumpHeight = 8;
	private const int LongJumpSpeed = 350;
	private const double DuckingMultiplier = 0.333;
	private const int DeadDiscardBody = 4;

	private const float FallPunchThreshold = 350f;
	private const int MaxSafeFallSpeed = 580;
	private const int MinBounceSpeed = 200;

	private readonly Vector3[] _stuckTable = new Vector3[54];
	private readonly int[,] _stuckLast = new int[MaxClients, 2];
	private readonly float[,] _stuckCheckTime = new float[MaxClients, 2];

	private int GetRandomStuckOffsets( int index, int server, out Vector3 offset )
	{
		var idx = _stuckLast[index, server]++;
		offset = _stuckTable[idx % 54];

		return idx % 54;
	}

	private void ResetStuckOffsets( int index, int server )
	{
		_stuckLast[index, server] = 0;
	}

	private bool CheckStuck()
	{
		var server = _pmove.Server ? 1 : 0;

		var hitEnt = _pmove.TestPlayerPosition( _pmove.Origin, out var traceResult );
		if ( hitEnt == -1 )
		{
			ResetStuckOffsets( _pmove.PlayerIndex, server );
			return false;
		}

		var basePosition = _pmove.Origin;
		Vector3 offset;
		Vector3 test;
		int i;

		if ( !_pmove.Server || !_pmove.Multiplayer )
		{
			if ( hitEnt == 0 || _pmove.PhysEnts[hitEnt].Model is not null )
			{
				var reps = 0;
				ResetStuckOffsets( _pmove.PlayerIndex, server );

				do
				{
					GetRandomStuckOffsets( _pmove.PlayerIndex, server, out offset );
					test = basePosition + offset;

					if ( _pmove.TestPlayerPosition( test, out traceResult ) == -1 )
					{
						ResetStuckOffsets( _pmove.PlayerIndex, server );
						_pmove.Origin = test;
						return false;
					}

					reps++;
				}
				while ( reps < 54 );
			}
		}

		var idx = _pmove.Server ? 0 : 1;
		var time = (float)_pmove.FloatTime();

		if ( _stuckCheckTime[_pmove.PlayerIndex, idx] >= time - CheckStuckMinTime )
			return true;

		_stuckCheckTime[_pmove.PlayerIndex, idx] = time;

		_pmove.StuckTouch( hitEnt, traceResult );

		i = GetRandomStuckOffsets( _pmove.PlayerIndex, server, out offset );
		test = basePosition + offset;

		hitEnt = _pmove.TestPlayerPosition( test, out _ );
		if ( hitEnt == -1 )
		{
			ResetStuckOffsets( _pmove.PlayerIndex, server );

			if ( i >= 27 )
				_pmove.Origin = test;

			return false;
		}

		if ( (HasButton( InButtons.Jump ) || HasButton( InButtons.Duck ) || HasButton( InButtons.Attack )) && _pmove.PhysEnts[hitEnt].Player != 0 )
		{
			const float xyStep = 8f;
			const float zStep = 18f;
			const float xyMinMax = xyStep;
			const float zMinMax = 4f * zStep;

			for ( var z = 0f; z <= zMinMax; z += zStep )
			{
				for ( var x = -xyMinMax; x <= xyMinMax; x += xyStep )
				{
					for ( var y = -xyMinMax; y <= xyMinMax; y += xyStep )
					{
						test = basePosition + new Vector3( x, y, z );

						if ( _pmove.TestPlayerPosition( test, out _ ) == -1 )
						{
							_pmove.Origin = test;
							return false;
						}
					}
				}
			}
		}

		return true;
	}

	private void SpectatorMove()
	{
		if ( _pmove.User1 == ObserverRoaming )
		{
			if ( !_pmove.Server && ClientDll.Loaded is HalfLifeClientDll { JumpSpectator: true } dll )
			{
				_pmove.Origin = dll.JumpOrigin;
				_pmove.Angles = dll.JumpAngles;
				_pmove.Velocity = Vector3.Zero;

				dll.JumpSpectator = false;

				return;
			}

			var speed = MathLib.Length( _pmove.Velocity );

			if ( speed < 1f )
			{
				_pmove.Velocity = Vector3.Zero;
			}
			else
			{
				var friction = (float)(_pmove.MoveVars.Friction * 1.5);
				var control = speed < _pmove.MoveVars.StopSpeed ? _pmove.MoveVars.StopSpeed : speed;
				var drop = control * friction * _pmove.FrameTime;

				var newSpeed = speed - drop;
				if ( newSpeed < 0f )
					newSpeed = 0f;

				newSpeed /= speed;

				_pmove.Velocity *= newSpeed;
			}

			MathLib.VectorNormalize( ref _pmove.Forward );
			MathLib.VectorNormalize( ref _pmove.Right );

			var wishVelocity = (_pmove.Forward * _pmove.Cmd.ForwardMove) + (_pmove.Right * _pmove.Cmd.SideMove);
			wishVelocity.z += _pmove.Cmd.UpMove;

			var wishDir = wishVelocity;
			var wishSpeed = MathLib.VectorNormalize( ref wishDir );

			if ( wishSpeed > _pmove.MoveVars.SpectatorMaxSpeed )
				wishSpeed = _pmove.MoveVars.SpectatorMaxSpeed;

			var currentSpeed = Vector3.Dot( _pmove.Velocity, wishDir );
			var addSpeed = wishSpeed - currentSpeed;
			if ( addSpeed <= 0f )
				return;

			var accelSpeed = _pmove.MoveVars.Accelerate * _pmove.FrameTime * wishSpeed;
			if ( accelSpeed > addSpeed )
				accelSpeed = addSpeed;

			_pmove.Velocity += accelSpeed * wishDir;
			_pmove.Origin += _pmove.FrameTime * _pmove.Velocity;
		}
		else
		{
			if ( _pmove.User2 <= 0 )
				return;

			var target = _pmove.PhysEnts.Find( x => x.Info == _pmove.User2 );
			if ( target is null )
				return;

			_pmove.Angles = target.Angles;
			_pmove.Origin = target.Origin;
			_pmove.Velocity = Vector3.Zero;
		}
	}

	private static float SplineFraction( float value, float scale )
	{
		value = scale * value;
		var valueSquared = value * value;

		return (3f * valueSquared) - (2f * valueSquared * value);
	}

	private void FixPlayerCrouchStuck( int direction )
	{
		if ( _pmove.TestPlayerPosition( _pmove.Origin, out _ ) == -1 )
			return;

		var test = _pmove.Origin;

		for ( var i = 0; i < 36; i++ )
		{
			_pmove.Origin.z += direction;

			if ( _pmove.TestPlayerPosition( _pmove.Origin, out _ ) == -1 )
				return;
		}

		_pmove.Origin = test;
	}

	private void UnDuck()
	{
		var newOrigin = _pmove.Origin;

		if ( _pmove.OnGround != -1 )
			newOrigin += PlayerMove.PlayerMins[1] - PlayerMove.PlayerMins[0];

		var trace = _pmove.PlayerTrace( newOrigin, newOrigin, TraceFlags.Normal, -1 );
		if ( trace.StartSolid )
			return;

		_pmove.UseHull = 0;

		trace = _pmove.PlayerTrace( newOrigin, newOrigin, TraceFlags.Normal, -1 );
		if ( trace.StartSolid )
		{
			_pmove.UseHull = 1;
			return;
		}

		_pmove.Flags &= ~EntityFlags.Ducking;
		_pmove.InDuck = false;
		_pmove.ViewOffset.z = View;
		_pmove.DuckTime = 0f;

		_pmove.Origin = newOrigin;

		CatagorizePosition();
	}

	private void Duck()
	{
		var buttonsChanged = _pmove.OldButtons ^ _pmove.Cmd.Buttons;
		var buttonPressed = buttonsChanged & _pmove.Cmd.Buttons;

		if ( HasButton( InButtons.Duck ) )
			_pmove.OldButtons |= (int)InButtons.Duck;
		else
			_pmove.OldButtons &= ~(int)InButtons.Duck;

		if ( _pmove.User3 != 0 || _pmove.Dead )
		{
			if ( (_pmove.Flags & EntityFlags.Ducking) == EntityFlags.Ducking )
				UnDuck();

			return;
		}

		if ( (_pmove.Flags & EntityFlags.Ducking) == EntityFlags.Ducking )
		{
			_pmove.Cmd.ForwardMove = (float)(_pmove.Cmd.ForwardMove * DuckingMultiplier);
			_pmove.Cmd.SideMove = (float)(_pmove.Cmd.SideMove * DuckingMultiplier);
			_pmove.Cmd.UpMove = (float)(_pmove.Cmd.UpMove * DuckingMultiplier);
		}

		if ( !HasButton( InButtons.Duck ) && !_pmove.InDuck && (_pmove.Flags & EntityFlags.Ducking) != EntityFlags.Ducking )
			return;

		if ( !HasButton( InButtons.Duck ) )
		{
			UnDuck();
			return;
		}

		if ( (buttonPressed & (int)InButtons.Duck) != 0 && (_pmove.Flags & EntityFlags.Ducking) != EntityFlags.Ducking )
		{
			_pmove.DuckTime = 1000f;
			_pmove.InDuck = true;
		}

		var time = (float)Math.Max( 0.0, 1.0 - (_pmove.DuckTime / 1000.0) );

		if ( !_pmove.InDuck )
			return;

		if ( _pmove.DuckTime / 1000.0 <= 1.0 - TimeToDuck || _pmove.OnGround == -1 )
		{
			_pmove.UseHull = 1;
			_pmove.ViewOffset.z = DuckView;
			_pmove.Flags |= EntityFlags.Ducking;
			_pmove.InDuck = false;

			if ( _pmove.OnGround != -1 )
			{
				_pmove.Origin -= PlayerMove.PlayerMins[1] - PlayerMove.PlayerMins[0];

				FixPlayerCrouchStuck( StuckMoveUp );
				CatagorizePosition();
			}
		}
		else
		{
			float more = DuckHullMin - HullMin;
			var duckFraction = SplineFraction( time, (float)(1.0 / TimeToDuck) );

			_pmove.ViewOffset.z = ((DuckView - more) * duckFraction) + (View * (1 - duckFraction));
		}
	}

	private void LadderMove( PhysEnt ladder )
	{
		if ( _pmove.MoveType == MoveType.NoClip )
			return;

		var ladderCenter = (ladder.Model.Mins + ladder.Model.Maxs) * 0.5f;

		_pmove.MoveType = MoveType.Fly;

		var floor = _pmove.Origin;
		floor.z += PlayerMove.PlayerMins[_pmove.UseHull].z - 1f;

		var onFloor = _pmove.PointContents( floor ) == (int)Contents.Solid;

		_pmove.Gravity = 0f;

		var trace = new Trace();
		_pmove.TraceModel( ladder, _pmove.Origin, ladderCenter, ref trace );

		if ( trace.Fraction == 1f )
			return;

		var forward = 0f;
		var right = 0f;

		float speed = MaxClimbSpeed;
		if ( speed > _pmove.MaxSpeed )
			speed = _pmove.MaxSpeed;

		MathLib.AngleVectors( _pmove.Angles, out var viewForward, out var viewRight, out _ );

		if ( (_pmove.Flags & EntityFlags.Ducking) == EntityFlags.Ducking )
			speed = (float)(speed * DuckingMultiplier);

		if ( HasButton( InButtons.Back ) )
			forward -= speed;

		if ( HasButton( InButtons.Forward ) )
			forward += speed;

		if ( HasButton( InButtons.MoveLeft ) )
			right -= speed;

		if ( HasButton( InButtons.MoveRight ) )
			right += speed;

		if ( HasButton( InButtons.Jump ) )
		{
			_pmove.MoveType = MoveType.Walk;
			_pmove.Velocity = trace.PlaneNormal * 270f;
			return;
		}

		if ( forward == 0f && right == 0f )
		{
			_pmove.Velocity = Vector3.Zero;
			return;
		}

		var velocity = (viewForward * forward) + (right * viewRight);

		var perp = MathLib.CrossProduct( new Vector3( 0f, 0f, 1f ), trace.PlaneNormal );
		MathLib.VectorNormalize( ref perp );

		var normal = Vector3.Dot( velocity, trace.PlaneNormal );
		var cross = trace.PlaneNormal * normal;
		var lateral = velocity - cross;

		var climb = MathLib.CrossProduct( trace.PlaneNormal, perp );
		_pmove.Velocity = lateral + (-normal * climb);

		if ( onFloor && normal > 0f )
			_pmove.Velocity += MaxClimbSpeed * trace.PlaneNormal;
	}

	private PhysEnt Ladder()
	{
		foreach ( var physEnt in _pmove.MoveEnts )
		{
			if ( physEnt.Model is null || physEnt.Skin != ContentsLadder )
				continue;

			var hull = _pmove.HullForBsp( physEnt, out var offset );

			if ( hull.PointContents( hull.FirstClipNode, _pmove.Origin - offset ) == (int)Contents.Empty )
				continue;

			return physEnt;
		}

		return null;
	}

	private void WaterJump()
	{
		if ( _pmove.WaterJumpTime > 10000f )
			_pmove.WaterJumpTime = 10000f;

		if ( _pmove.WaterJumpTime == 0f )
			return;

		_pmove.WaterJumpTime -= _pmove.Cmd.Msec;

		if ( _pmove.WaterJumpTime < 0f || _pmove.WaterLevel == 0 )
		{
			_pmove.WaterJumpTime = 0f;
			_pmove.Flags &= ~EntityFlags.WaterJump;
		}

		_pmove.Velocity.x = _pmove.MoveDir.x;
		_pmove.Velocity.y = _pmove.MoveDir.y;
	}

	private void AddGravity()
	{
		var entGravity = _pmove.Gravity != 0f ? _pmove.Gravity : 1f;

		_pmove.Velocity.z -= entGravity * _pmove.MoveVars.Gravity * _pmove.FrameTime;
		_pmove.Velocity.z += _pmove.BaseVelocity.z * _pmove.FrameTime;
		_pmove.BaseVelocity.z = 0f;

		CheckVelocity();
	}

	private PmTrace PushEntity( Vector3 push )
	{
		var end = _pmove.Origin + push;

		var trace = _pmove.PlayerTrace( _pmove.Origin, end, TraceFlags.Normal, -1 );
		_pmove.Origin = trace.EndPos;

		if ( trace.Fraction < 1f && !trace.AllSolid )
			AddToTouched( trace, _pmove.Velocity );

		return trace;
	}

	private void PhysicsToss()
	{
		CheckWater();

		if ( _pmove.Velocity.z > 0f )
			_pmove.OnGround = -1;

		if ( _pmove.OnGround != -1 && IsZero( _pmove.BaseVelocity ) && IsZero( _pmove.Velocity ) )
			return;

		CheckVelocity();

		if ( _pmove.MoveType != MoveType.Fly && _pmove.MoveType != MoveType.BounceMissile && _pmove.MoveType != MoveType.FlyMissile )
			AddGravity();

		_pmove.Velocity += _pmove.BaseVelocity;

		CheckVelocity();

		var move = _pmove.Velocity * _pmove.FrameTime;
		_pmove.Velocity -= _pmove.BaseVelocity;

		var trace = PushEntity( move );

		CheckVelocity();

		if ( trace.AllSolid )
		{
			_pmove.OnGround = trace.Ent;
			_pmove.Velocity = Vector3.Zero;
			return;
		}

		if ( trace.Fraction == 1f )
		{
			CheckWater();
			return;
		}

		float backoff;

		if ( _pmove.MoveType == MoveType.Bounce )
			backoff = (float)(2.0 - _pmove.Friction);
		else if ( _pmove.MoveType == MoveType.BounceMissile )
			backoff = 2f;
		else
			backoff = 1f;

		ClipVelocity( _pmove.Velocity, trace.PlaneNormal, out _pmove.Velocity, backoff );

		if ( trace.PlaneNormal.z > 0.7 )
		{
			if ( _pmove.Velocity.z < _pmove.MoveVars.Gravity * _pmove.FrameTime )
			{
				_pmove.OnGround = trace.Ent;
				_pmove.Velocity.z = 0f;
			}

			var speedSquared = Vector3.Dot( _pmove.Velocity, _pmove.Velocity );

			if ( speedSquared < 30f * 30f || (_pmove.MoveType != MoveType.Bounce && _pmove.MoveType != MoveType.BounceMissile) )
			{
				_pmove.OnGround = trace.Ent;
				_pmove.Velocity = Vector3.Zero;
			}
			else
			{
				move = _pmove.Velocity * (float)((1.0 - trace.Fraction) * _pmove.FrameTime * 0.9);
				PushEntity( move );
			}
		}

		CheckWater();
	}

	private void NoClip()
	{
		MathLib.VectorNormalize( ref _pmove.Forward );
		MathLib.VectorNormalize( ref _pmove.Right );

		var wishVelocity = (_pmove.Forward * _pmove.Cmd.ForwardMove) + (_pmove.Right * _pmove.Cmd.SideMove);
		wishVelocity.z += _pmove.Cmd.UpMove;

		_pmove.Origin += _pmove.FrameTime * wishVelocity;
		_pmove.Velocity = Vector3.Zero;
	}

	private void PreventMegaBunnyJumping()
	{
		var maxScaledSpeed = BunnyJumpMaxSpeedFactor * _pmove.MaxSpeed;
		if ( maxScaledSpeed <= 0f )
			return;

		var speed = MathLib.Length( _pmove.Velocity );
		if ( speed <= maxScaledSpeed )
			return;

		var fraction = (float)(maxScaledSpeed / speed * 0.65);
		_pmove.Velocity *= fraction;
	}

	private void Jump()
	{
		if ( _pmove.Dead )
		{
			_pmove.OldButtons |= (int)InButtons.Jump;
			return;
		}

		var tfc = Util.Atoi( _pmove.InfoValueForKey( "tfc" ) ) == 1;

		if ( tfc && _pmove.DeadFlag == DeadDiscardBody + 1 )
			return;

		if ( _pmove.WaterJumpTime != 0f )
		{
			_pmove.WaterJumpTime -= _pmove.Cmd.Msec;
			if ( _pmove.WaterJumpTime < 0f )
				_pmove.WaterJumpTime = 0f;

			return;
		}

		if ( _pmove.WaterLevel >= 2 )
		{
			_pmove.OnGround = -1;

			if ( _pmove.WaterType == (int)Contents.Water )
				_pmove.Velocity.z = 100f;
			else if ( _pmove.WaterType == (int)Contents.Slime )
				_pmove.Velocity.z = 80f;
			else
				_pmove.Velocity.z = 50f;

			if ( _pmove.SwimTime <= 0f )
			{
				_pmove.SwimTime = 1000f;
				PlayWadeSound();
			}

			return;
		}

		if ( _pmove.OnGround == -1 )
		{
			_pmove.OldButtons |= (int)InButtons.Jump;
			return;
		}

		if ( (_pmove.OldButtons & (int)InButtons.Jump) != 0 )
			return;

		_pmove.OnGround = -1;

		PreventMegaBunnyJumping();

		if ( tfc )
			_pmove.PlaySound( SoundChannel.Body, "player/plyrjmp8.wav", 0.5f, Server.AttenuationNormal, 0, Server.PitchNormal );
		else
			PlayStepSound( MapTextureTypeStepType( _pmove.TextureType ), 1f );

		var canSuperJump = Util.Atoi( _pmove.InfoValueForKey( "slj" ) ) == 1;

		if ( (_pmove.InDuck || (_pmove.Flags & EntityFlags.Ducking) == EntityFlags.Ducking)
			&& canSuperJump
			&& HasButton( InButtons.Duck )
			&& _pmove.DuckTime > 0f
			&& MathLib.Length( _pmove.Velocity ) > 50f )
		{
			_pmove.PunchAngle.x = -5f;

			_pmove.Velocity.x = (float)(_pmove.Forward.x * LongJumpSpeed * 1.6);
			_pmove.Velocity.y = (float)(_pmove.Forward.y * LongJumpSpeed * 1.6);
			_pmove.Velocity.z = (float)Math.Sqrt( 2 * 800 * 56.0 );
		}
		else
		{
			_pmove.Velocity.z = (float)Math.Sqrt( 2 * 800 * 45.0 );
		}

		_pmove.OutStepHeight += 0.15f;

		FixupGravityVelocity();

		_pmove.OldButtons |= (int)InButtons.Jump;
	}

	private void CheckWaterJump()
	{
		if ( _pmove.WaterJumpTime != 0f )
			return;

		if ( _pmove.Velocity.z < -180f )
			return;

		var flatVelocity = new Vector3( _pmove.Velocity.x, _pmove.Velocity.y, 0f );
		var currentSpeed = MathLib.VectorNormalize( ref flatVelocity );

		var flatForward = new Vector3( _pmove.Forward.x, _pmove.Forward.y, 0f );
		MathLib.VectorNormalize( ref flatForward );

		if ( currentSpeed != 0f && Vector3.Dot( flatVelocity, flatForward ) < 0f )
			return;

		var start = _pmove.Origin;
		start.z += WaterJumpHeight;

		var end = start + (24f * flatForward);

		var saveHull = _pmove.UseHull;
		_pmove.UseHull = 2;

		var trace = _pmove.PlayerTrace( start, end, TraceFlags.Normal, -1 );

		if ( trace.Fraction < 1f && MathF.Abs( trace.PlaneNormal.z ) < 0.1f )
		{
			start.z += PlayerMove.PlayerMaxs[saveHull].z - WaterJumpHeight;
			end = start + (24f * flatForward);

			_pmove.MoveDir = -50f * trace.PlaneNormal;

			trace = _pmove.PlayerTrace( start, end, TraceFlags.Normal, -1 );

			if ( trace.Fraction == 1f )
			{
				_pmove.WaterJumpTime = 2000f;
				_pmove.Velocity.z = 225f;
				_pmove.OldButtons |= (int)InButtons.Jump;
				_pmove.Flags |= EntityFlags.WaterJump;
			}
		}

		_pmove.UseHull = saveHull;
	}

	private void CheckFalling()
	{
		if ( _pmove.OnGround != -1 && !_pmove.Dead && _pmove.FallVelocity >= FallPunchThreshold )
		{
			var volume = 0.5f;

			if ( _pmove.WaterLevel <= 0 )
			{
				if ( _pmove.FallVelocity > MaxSafeFallSpeed )
				{
					_pmove.PlaySound( SoundChannel.Voice, "player/pl_fallpain3.wav", 1f, Server.AttenuationNormal, 0, Server.PitchNormal );
					volume = 1f;
				}
				else if ( _pmove.FallVelocity > MaxSafeFallSpeed / 2 )
				{
					if ( Util.Atoi( _pmove.InfoValueForKey( "tfc" ) ) == 1 )
						_pmove.PlaySound( SoundChannel.Voice, "player/pl_fallpain3.wav", 1f, Server.AttenuationNormal, 0, Server.PitchNormal );

					volume = 0.85f;
				}
				else if ( _pmove.FallVelocity < MinBounceSpeed )
				{
					volume = 0f;
				}
			}

			if ( volume > 0f )
			{
				_pmove.TimeStepSound = 0;
				UpdateStepSound();
				PlayStepSound( MapTextureTypeStepType( _pmove.TextureType ), volume );

				_pmove.PunchAngle.z = (float)(_pmove.FallVelocity * 0.013);

				if ( _pmove.PunchAngle.x > 8f )
					_pmove.PunchAngle.x = 8f;
			}
		}

		if ( _pmove.OnGround != -1 )
			_pmove.FallVelocity = 0f;
	}

	private void CreateStuckTable()
	{
		var idx = 0;

		for ( var z = -0.125f; z <= 0.125f; z += 0.125f )
			_stuckTable[idx++] = new Vector3( 0f, 0f, z );

		for ( var y = -0.125f; y <= 0.125f; y += 0.125f )
			_stuckTable[idx++] = new Vector3( 0f, y, 0f );

		for ( var x = -0.125f; x <= 0.125f; x += 0.125f )
			_stuckTable[idx++] = new Vector3( x, 0f, 0f );

		for ( var x = -0.125f; x <= 0.125f; x += 0.250f )
		{
			for ( var y = -0.125f; y <= 0.125f; y += 0.250f )
			{
				for ( var z = -0.125f; z <= 0.125f; z += 0.250f )
					_stuckTable[idx++] = new Vector3( x, y, z );
			}
		}

		float[] zi = [0f, 1f, 6f];

		for ( var i = 0; i < 3; i++ )
			_stuckTable[idx++] = new Vector3( 0f, 0f, zi[i] );

		for ( var y = -2f; y <= 2f; y += 2f )
			_stuckTable[idx++] = new Vector3( 0f, y, 0f );

		for ( var x = -2f; x <= 2f; x += 2f )
			_stuckTable[idx++] = new Vector3( x, 0f, 0f );

		for ( var i = 0; i < 3; i++ )
		{
			for ( var x = -2f; x <= 2f; x += 2f )
			{
				for ( var y = -2f; y <= 2f; y += 2f )
					_stuckTable[idx++] = new Vector3( x, y, zi[i] );
			}
		}
	}

	private static bool IsZero( Vector3 v )
	{
		return v.x == 0f && v.y == 0f && v.z == 0f;
	}
}
