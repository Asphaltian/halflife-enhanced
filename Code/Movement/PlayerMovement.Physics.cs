using System;
using GoldSrc;
using GoldSrc.Bsp;

namespace HalfLife;

partial class PlayerMovement
{
	private const int MaxClipPlanes = 5;
	private const double StopEpsilon = 0.1;

	private static readonly Vector3[] CurrentTable =
	[
		new( 1f, 0f, 0f ),
		new( 0f, 1f, 0f ),
		new( -1f, 0f, 0f ),
		new( 0f, -1f, 0f ),
		new( 0f, 0f, 1f ),
		new( 0f, 0f, -1f )
	];

	private bool AddToTouched( PmTrace trace, Vector3 impactVelocity )
	{
		foreach ( var touched in _pmove.TouchIndex )
		{
			if ( touched.Ent == trace.Ent )
				return false;
		}

		trace.DeltaVelocity = impactVelocity;

		if ( _pmove.TouchIndex.Count >= PlayerMove.MaxPhysEnts )
			Log.Trace( "Too many entities were touched!" );

		_pmove.TouchIndex.Add( trace );
		return true;
	}

	private void CheckVelocity()
	{
		var maxVelocity = _pmove.MoveVars.MaxVelocity;

		for ( var i = 0; i < 3; i++ )
		{
			if ( !float.IsFinite( _pmove.Velocity[i] ) )
			{
				Log.Info( $"PM  Got a NaN velocity {i}" );
				_pmove.Velocity[i] = 0f;
			}

			if ( !float.IsFinite( _pmove.Origin[i] ) )
			{
				Log.Info( $"PM  Got a NaN origin on {i}" );
				_pmove.Origin[i] = 0f;
			}

			if ( _pmove.Velocity[i] > maxVelocity )
			{
				Log.Trace( $"PM  Got a velocity too high on {i}" );
				_pmove.Velocity[i] = maxVelocity;
			}
			else if ( _pmove.Velocity[i] < -maxVelocity )
			{
				Log.Trace( $"PM  Got a velocity too low on {i}" );
				_pmove.Velocity[i] = -maxVelocity;
			}
		}
	}

	private static int ClipVelocity( Vector3 input, Vector3 normal, out Vector3 output, float overbounce )
	{
		var angle = normal.z;
		var blocked = 0;

		if ( angle > 0f )
			blocked |= 0x01;

		if ( angle == 0f )
			blocked |= 0x02;

		var backoff = Vector3.Dot( input, normal ) * overbounce;

		output = default;

		for ( var i = 0; i < 3; i++ )
		{
			var change = normal[i] * backoff;
			output[i] = input[i] - change;

			if ( output[i] > -StopEpsilon && output[i] < StopEpsilon )
				output[i] = 0f;
		}

		return blocked;
	}

	private void AddCorrectGravity()
	{
		if ( _pmove.WaterJumpTime != 0f )
			return;

		var entGravity = _pmove.Gravity != 0f ? _pmove.Gravity : 1f;

		_pmove.Velocity.z = (float)(_pmove.Velocity.z - (entGravity * _pmove.MoveVars.Gravity * 0.5 * _pmove.FrameTime));
		_pmove.Velocity.z += _pmove.BaseVelocity.z * _pmove.FrameTime;
		_pmove.BaseVelocity.z = 0f;

		CheckVelocity();
	}

	private void FixupGravityVelocity()
	{
		if ( _pmove.WaterJumpTime != 0f )
			return;

		var entGravity = _pmove.Gravity != 0f ? _pmove.Gravity : 1f;

		_pmove.Velocity.z = (float)(_pmove.Velocity.z - (entGravity * _pmove.MoveVars.Gravity * _pmove.FrameTime * 0.5));

		CheckVelocity();
	}

	private int FlyMove()
	{
		const int numBumps = 4;

		var blocked = 0;
		var numPlanes = 0;
		var planes = new Vector3[MaxClipPlanes];

		var originalVelocity = _pmove.Velocity;
		var primalVelocity = _pmove.Velocity;
		var newVelocity = Vector3.Zero;

		var allFraction = 0f;
		var timeLeft = _pmove.FrameTime;

		for ( var bumpCount = 0; bumpCount < numBumps; bumpCount++ )
		{
			if ( _pmove.Velocity.x == 0f && _pmove.Velocity.y == 0f && _pmove.Velocity.z == 0f )
				break;

			var end = _pmove.Origin + (timeLeft * _pmove.Velocity);
			var trace = _pmove.PlayerTrace( _pmove.Origin, end, TraceFlags.Normal, -1 );

			allFraction += trace.Fraction;

			if ( trace.AllSolid )
			{
				_pmove.Velocity = Vector3.Zero;
				return 4;
			}

			if ( trace.Fraction > 0f )
			{
				_pmove.Origin = trace.EndPos;
				originalVelocity = _pmove.Velocity;
				numPlanes = 0;
			}

			if ( trace.Fraction == 1f )
				break;

			AddToTouched( trace, _pmove.Velocity );

			if ( trace.PlaneNormal.z > 0.7 )
				blocked |= 1;

			if ( trace.PlaneNormal.z == 0f )
				blocked |= 2;

			timeLeft -= timeLeft * trace.Fraction;

			if ( numPlanes >= MaxClipPlanes )
			{
				_pmove.Velocity = Vector3.Zero;
				break;
			}

			planes[numPlanes] = trace.PlaneNormal;
			numPlanes++;

			if ( numPlanes == 1 && _pmove.MoveType == MoveType.Walk && (_pmove.OnGround == -1 || _pmove.Friction != 1f) )
			{
				for ( var i = 0; i < numPlanes; i++ )
				{
					if ( planes[i].z > 0.7 )
					{
						ClipVelocity( originalVelocity, planes[i], out newVelocity, 1f );
						originalVelocity = newVelocity;
					}
					else
					{
						ClipVelocity( originalVelocity, planes[i], out newVelocity, (float)(1.0 + (_pmove.MoveVars.Bounce * (1 - _pmove.Friction))) );
					}
				}

				_pmove.Velocity = newVelocity;
				originalVelocity = newVelocity;
			}
			else
			{
				int i;

				for ( i = 0; i < numPlanes; i++ )
				{
					ClipVelocity( originalVelocity, planes[i], out _pmove.Velocity, 1f );

					int j;

					for ( j = 0; j < numPlanes; j++ )
					{
						if ( j != i && Vector3.Dot( _pmove.Velocity, planes[j] ) < 0f )
							break;
					}

					if ( j == numPlanes )
						break;
				}

				if ( i == numPlanes )
				{
					if ( numPlanes != 2 )
					{
						_pmove.Velocity = Vector3.Zero;
						break;
					}

					var dir = MathLib.CrossProduct( planes[0], planes[1] );
					var d = Vector3.Dot( dir, _pmove.Velocity );
					_pmove.Velocity = dir * d;
				}

				if ( Vector3.Dot( _pmove.Velocity, primalVelocity ) <= 0f )
				{
					_pmove.Velocity = Vector3.Zero;
					break;
				}
			}
		}

		if ( allFraction == 0f )
			_pmove.Velocity = Vector3.Zero;

		return blocked;
	}

	private void Accelerate( Vector3 wishDir, float wishSpeed, float accel )
	{
		if ( _pmove.Dead )
			return;

		if ( _pmove.WaterJumpTime != 0f )
			return;

		var currentSpeed = Vector3.Dot( _pmove.Velocity, wishDir );
		var addSpeed = wishSpeed - currentSpeed;
		if ( addSpeed <= 0f )
			return;

		var accelSpeed = accel * _pmove.FrameTime * wishSpeed * _pmove.Friction;
		if ( accelSpeed > addSpeed )
			accelSpeed = addSpeed;

		_pmove.Velocity += accelSpeed * wishDir;
	}

	private void WalkMove()
	{
		var forwardMove = _pmove.Cmd.ForwardMove;
		var sideMove = _pmove.Cmd.SideMove;

		_pmove.Forward.z = 0f;
		_pmove.Right.z = 0f;

		MathLib.VectorNormalize( ref _pmove.Forward );
		MathLib.VectorNormalize( ref _pmove.Right );

		var wishVelocity = new Vector3(
			(_pmove.Forward.x * forwardMove) + (_pmove.Right.x * sideMove),
			(_pmove.Forward.y * forwardMove) + (_pmove.Right.y * sideMove),
			0f );

		var wishDir = wishVelocity;
		var wishSpeed = MathLib.VectorNormalize( ref wishDir );

		if ( wishSpeed > _pmove.MaxSpeed )
		{
			wishVelocity *= _pmove.MaxSpeed / wishSpeed;
			wishSpeed = _pmove.MaxSpeed;
		}

		_pmove.Velocity.z = 0f;
		Accelerate( wishDir, wishSpeed, _pmove.MoveVars.Accelerate );
		_pmove.Velocity.z = 0f;

		_pmove.Velocity += _pmove.BaseVelocity;

		var speed = MathLib.Length( _pmove.Velocity );

		if ( speed < 1f )
		{
			_pmove.Velocity = Vector3.Zero;
			return;
		}

		var oldOnGround = _pmove.OnGround;

		var dest = new Vector3(
			_pmove.Origin.x + (_pmove.Velocity.x * _pmove.FrameTime),
			_pmove.Origin.y + (_pmove.Velocity.y * _pmove.FrameTime),
			_pmove.Origin.z );

		var trace = _pmove.PlayerTrace( _pmove.Origin, dest, TraceFlags.Normal, -1 );

		_pmove.OutWishVel += wishDir * wishSpeed;

		if ( trace.Fraction == 1f )
		{
			_pmove.Origin = trace.EndPos;
			return;
		}

		if ( oldOnGround == -1 && _pmove.WaterLevel == 0 )
			return;

		if ( _pmove.WaterJumpTime != 0f )
			return;

		var original = _pmove.Origin;
		var originalVelocity = _pmove.Velocity;

		FlyMove();

		var down = _pmove.Origin;
		var downVelocity = _pmove.Velocity;

		_pmove.Origin = original;
		_pmove.Velocity = originalVelocity;

		dest = _pmove.Origin;
		dest.z += _pmove.MoveVars.StepSize;

		trace = _pmove.PlayerTrace( _pmove.Origin, dest, TraceFlags.Normal, -1 );

		if ( !trace.StartSolid && !trace.AllSolid )
			_pmove.Origin = trace.EndPos;

		FlyMove();

		dest = _pmove.Origin;
		dest.z -= _pmove.MoveVars.StepSize;

		trace = _pmove.PlayerTrace( _pmove.Origin, dest, TraceFlags.Normal, -1 );

		var useDown = trace.PlaneNormal.z < 0.7;

		if ( !useDown )
		{
			if ( !trace.StartSolid && !trace.AllSolid )
				_pmove.Origin = trace.EndPos;

			_pmove.Up = _pmove.Origin;

			var downDist = ((down.x - original.x) * (down.x - original.x)) + ((down.y - original.y) * (down.y - original.y));
			var upDist = ((_pmove.Up.x - original.x) * (_pmove.Up.x - original.x)) + ((_pmove.Up.y - original.y) * (_pmove.Up.y - original.y));

			useDown = downDist > upDist;
		}

		if ( useDown )
		{
			_pmove.Origin = down;
			_pmove.Velocity = downVelocity;
		}
		else
		{
			_pmove.Velocity.z = downVelocity.z;

			var stepDist = _pmove.Origin.z - original.z;

			if ( stepDist > 0f )
				_pmove.OutStepHeight += stepDist;
		}
	}

	private void Friction()
	{
		if ( _pmove.WaterJumpTime != 0f )
			return;

		var velocity = _pmove.Velocity;
		var speed = MathF.Sqrt( (velocity.x * velocity.x) + (velocity.y * velocity.y) + (velocity.z * velocity.z) );

		if ( speed < 0.1f )
			return;

		var drop = 0f;

		if ( _pmove.OnGround != -1 )
		{
			var start = new Vector3(
				_pmove.Origin.x + (velocity.x / speed * 16f),
				_pmove.Origin.y + (velocity.y / speed * 16f),
				_pmove.Origin.z + PlayerMove.PlayerMins[_pmove.UseHull].z );

			var stop = start;
			stop.z = start.z - 34f;

			var trace = _pmove.PlayerTrace( start, stop, TraceFlags.Normal, -1 );

			var friction = trace.Fraction == 1f
				? _pmove.MoveVars.Friction * _pmove.MoveVars.EdgeFriction
				: _pmove.MoveVars.Friction;

			friction *= _pmove.Friction;

			var control = speed < _pmove.MoveVars.StopSpeed ? _pmove.MoveVars.StopSpeed : speed;
			drop += control * friction * _pmove.FrameTime;
		}

		var newSpeed = speed - drop;
		if ( newSpeed < 0f )
			newSpeed = 0f;

		newSpeed /= speed;

		_pmove.Velocity = new Vector3( velocity.x * newSpeed, velocity.y * newSpeed, velocity.z * newSpeed );

		_pmove.OutWishVel -= (1f - newSpeed) * _pmove.Velocity;
	}

	private void AirAccelerate( Vector3 wishDir, float wishSpeed, float accel )
	{
		if ( _pmove.Dead )
			return;

		if ( _pmove.WaterJumpTime != 0f )
			return;

		var wishSpd = wishSpeed;
		if ( wishSpd > 30f )
			wishSpd = 30f;

		var currentSpeed = Vector3.Dot( _pmove.Velocity, wishDir );
		var addSpeed = wishSpd - currentSpeed;
		if ( addSpeed <= 0f )
			return;

		var accelSpeed = accel * wishSpeed * _pmove.FrameTime * _pmove.Friction;
		if ( accelSpeed > addSpeed )
			accelSpeed = addSpeed;

		_pmove.Velocity += accelSpeed * wishDir;
		_pmove.OutWishVel += accelSpeed * wishDir;
	}

	private void WaterMove()
	{
		var wishVelocity = (_pmove.Forward * _pmove.Cmd.ForwardMove) + (_pmove.Right * _pmove.Cmd.SideMove);

		if ( _pmove.Cmd.ForwardMove == 0f && _pmove.Cmd.SideMove == 0f && _pmove.Cmd.UpMove == 0f )
			wishVelocity.z -= 60f;
		else
			wishVelocity.z += _pmove.Cmd.UpMove;

		var wishDir = wishVelocity;
		var wishSpeed = MathLib.VectorNormalize( ref wishDir );

		if ( wishSpeed > _pmove.MaxSpeed )
		{
			wishVelocity *= _pmove.MaxSpeed / wishSpeed;
			wishSpeed = _pmove.MaxSpeed;
		}

		wishSpeed = (float)(wishSpeed * 0.8);

		_pmove.Velocity += _pmove.BaseVelocity;

		var temp = _pmove.Velocity;
		var speed = MathLib.VectorNormalize( ref temp );
		float newSpeed;

		if ( speed != 0f )
		{
			newSpeed = speed - (_pmove.FrameTime * speed * _pmove.MoveVars.Friction * _pmove.Friction);
			if ( newSpeed < 0f )
				newSpeed = 0f;

			_pmove.Velocity *= newSpeed / speed;
		}
		else
		{
			newSpeed = 0f;
		}

		if ( wishSpeed < 0.1f )
			return;

		var addSpeed = wishSpeed - newSpeed;

		if ( addSpeed > 0f )
		{
			MathLib.VectorNormalize( ref wishVelocity );

			var accelSpeed = _pmove.MoveVars.Accelerate * wishSpeed * _pmove.FrameTime * _pmove.Friction;
			if ( accelSpeed > addSpeed )
				accelSpeed = addSpeed;

			_pmove.Velocity += accelSpeed * wishVelocity;
			_pmove.OutWishVel += accelSpeed * wishVelocity;
		}

		var dest = _pmove.Origin + (_pmove.FrameTime * _pmove.Velocity);
		var start = dest;
		start.z += _pmove.MoveVars.StepSize + 1f;

		var trace = _pmove.PlayerTrace( start, dest, TraceFlags.Normal, -1 );

		if ( !trace.StartSolid && !trace.AllSolid )
		{
			_pmove.OutStepHeight += trace.EndPos.z - _pmove.Origin.z;
			_pmove.Origin = trace.EndPos;
			return;
		}

		FlyMove();
	}

	private void AirMove()
	{
		var forwardMove = _pmove.Cmd.ForwardMove;
		var sideMove = _pmove.Cmd.SideMove;

		_pmove.Forward.z = 0f;
		_pmove.Right.z = 0f;

		MathLib.VectorNormalize( ref _pmove.Forward );
		MathLib.VectorNormalize( ref _pmove.Right );

		var wishVelocity = new Vector3(
			(_pmove.Forward.x * forwardMove) + (_pmove.Right.x * sideMove),
			(_pmove.Forward.y * forwardMove) + (_pmove.Right.y * sideMove),
			0f );

		var wishDir = wishVelocity;
		var wishSpeed = MathLib.VectorNormalize( ref wishDir );

		if ( wishSpeed > _pmove.MaxSpeed )
		{
			wishVelocity *= _pmove.MaxSpeed / wishSpeed;
			wishSpeed = _pmove.MaxSpeed;
		}

		AirAccelerate( wishDir, wishSpeed, _pmove.MoveVars.AirAccelerate );

		_pmove.Velocity += _pmove.BaseVelocity;

		FlyMove();
	}

	private bool InWater() => _pmove.WaterLevel > 1;

	private bool CheckWater()
	{
		var mins = PlayerMove.PlayerMins[_pmove.UseHull];
		var maxs = PlayerMove.PlayerMaxs[_pmove.UseHull];

		var point = new Vector3(
			(float)(_pmove.Origin.x + ((mins.x + maxs.x) * 0.5)),
			(float)(_pmove.Origin.y + ((mins.y + maxs.y) * 0.5)),
			_pmove.Origin.z + mins.z + 1f );

		_pmove.WaterLevel = 0;
		_pmove.WaterType = (int)Contents.Empty;

		var contents = _pmove.PointContents( point, out var trueContents );

		if ( IsLiquid( contents ) )
		{
			_pmove.WaterType = contents;
			_pmove.WaterLevel = 1;

			var height = mins.z + maxs.z;
			var heightOver2 = (float)(height * 0.5);

			point.z = _pmove.Origin.z + heightOver2;
			contents = _pmove.PointContents( point );

			if ( IsLiquid( contents ) )
			{
				_pmove.WaterLevel = 2;

				point.z = _pmove.Origin.z + _pmove.ViewOffset.z;
				contents = _pmove.PointContents( point );

				if ( IsLiquid( contents ) )
					_pmove.WaterLevel = 3;
			}

			if ( trueContents is <= (int)Contents.Current0 and >= (int)Contents.CurrentDown )
				_pmove.BaseVelocity += (float)(50.0 * _pmove.WaterLevel) * CurrentTable[(int)Contents.Current0 - trueContents];
		}

		return _pmove.WaterLevel > 1;
	}

	private static bool IsLiquid( int contents )
	{
		return contents is <= (int)Contents.Water and > (int)Contents.Translucent;
	}

	private void CatagorizePosition()
	{
		CheckWater();

		var point = new Vector3( _pmove.Origin.x, _pmove.Origin.y, _pmove.Origin.z - 2f );

		if ( _pmove.Velocity.z > 180f )
		{
			_pmove.OnGround = -1;
			return;
		}

		var trace = _pmove.PlayerTrace( _pmove.Origin, point, TraceFlags.Normal, -1 );

		_pmove.OnGround = trace.PlaneNormal.z < 0.7 ? -1 : trace.Ent;

		if ( _pmove.OnGround != -1 )
		{
			_pmove.WaterJumpTime = 0f;

			if ( _pmove.WaterLevel < 2 && !trace.StartSolid && !trace.AllSolid )
				_pmove.Origin = trace.EndPos;
		}

		if ( trace.Ent > 0 )
			AddToTouched( trace, _pmove.Velocity );
	}
}
