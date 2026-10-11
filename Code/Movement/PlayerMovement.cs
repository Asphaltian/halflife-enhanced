using System;
using GoldSrc;

namespace HalfLife;

public sealed partial class PlayerMovement
{
	private const int MaxClients = 32;
	private const int DeadViewHeight = -8;
	private const int ObserverRoaming = 3;

	private PlayerMove _pmove;
	private bool _onLadder;

	public PlayerMovement()
	{
		CreateStuckTable();
	}

	public void Init( PlayerMove pmove )
	{
		_pmove = pmove;

		InitTextureTypes();
	}

	public void Move( PlayerMove pmove, bool server )
	{
		_pmove = pmove;

		InitTextureTypes();
		MovePlayer( server );

		if ( _pmove.OnGround != -1 )
			_pmove.Flags |= EntityFlags.OnGround;
		else
			_pmove.Flags &= ~EntityFlags.OnGround;

		if ( !_pmove.Multiplayer && _pmove.MoveType == MoveType.Walk )
			_pmove.Friction = 1f;
	}

	private void MovePlayer( bool server )
	{
		PhysEnt ladder = null;

		_pmove.Server = server;

		CheckParameters();

		_pmove.OutWishVel = Vector3.Zero;

		_pmove.TouchIndex.Clear();
		_pmove.FrameTime = (float)(_pmove.Cmd.Msec * 0.001);

		ReduceTimers();

		MathLib.AngleVectors( _pmove.Angles, out _pmove.Forward, out _pmove.Right, out _pmove.Up );

		if ( _pmove.Spectator != 0 || _pmove.User1 > 0 )
		{
			SpectatorMove();
			CatagorizePosition();
			return;
		}

		if ( _pmove.MoveType != MoveType.NoClip && _pmove.MoveType != MoveType.None )
		{
			if ( CheckStuck() )
			{
				Duck();

				if ( CheckStuck() )
					return;
			}
		}

		CatagorizePosition();

		_pmove.OldWaterLevel = _pmove.WaterLevel;

		if ( _pmove.OnGround == -1 )
			_pmove.FallVelocity = -_pmove.Velocity.z;

		_onLadder = false;

		if ( !_pmove.Dead && (_pmove.Flags & EntityFlags.OnTrain) != EntityFlags.OnTrain )
		{
			ladder = Ladder();
			_onLadder = ladder is not null;
		}

		UpdateStepSound();

		Duck();

		if ( !_pmove.Dead && (_pmove.Flags & EntityFlags.OnTrain) != EntityFlags.OnTrain )
		{
			if ( ladder is not null )
				LadderMove( ladder );
			else if ( _pmove.MoveType != MoveType.Walk && _pmove.MoveType != MoveType.NoClip )
				_pmove.MoveType = MoveType.Walk;
		}

		switch ( _pmove.MoveType )
		{
			default:
				Con.DPrintf( $"Bogus pmove player movetype {(int)_pmove.MoveType} on ({(_pmove.Server ? 1 : 0)}) 0=cl 1=sv\n" );
				break;

			case MoveType.None:
				break;

			case MoveType.NoClip:
				NoClip();
				break;

			case MoveType.Toss:
			case MoveType.Bounce:
				PhysicsToss();
				break;

			case MoveType.Fly:
				CheckWater();

				if ( HasButton( InButtons.Jump ) )
				{
					if ( ladder is null )
						Jump();
				}
				else
				{
					_pmove.OldButtons &= ~(int)InButtons.Jump;
				}

				_pmove.Velocity += _pmove.BaseVelocity;
				FlyMove();
				_pmove.Velocity -= _pmove.BaseVelocity;
				break;

			case MoveType.Walk:
				if ( !InWater() )
					AddCorrectGravity();

				if ( _pmove.WaterJumpTime != 0f )
				{
					WaterJump();
					FlyMove();
					CheckWater();
					return;
				}

				if ( _pmove.WaterLevel >= 2 )
				{
					if ( _pmove.WaterLevel == 2 )
						CheckWaterJump();

					if ( _pmove.Velocity.z < 0f && _pmove.WaterJumpTime != 0f )
						_pmove.WaterJumpTime = 0f;

					if ( HasButton( InButtons.Jump ) )
						Jump();
					else
						_pmove.OldButtons &= ~(int)InButtons.Jump;

					WaterMove();

					_pmove.Velocity -= _pmove.BaseVelocity;

					CatagorizePosition();
				}
				else
				{
					if ( HasButton( InButtons.Jump ) )
					{
						if ( ladder is null )
							Jump();
					}
					else
					{
						_pmove.OldButtons &= ~(int)InButtons.Jump;
					}

					if ( _pmove.OnGround != -1 )
					{
						_pmove.Velocity.z = 0f;
						Friction();
					}

					CheckVelocity();

					if ( _pmove.OnGround != -1 )
						WalkMove();
					else
						AirMove();

					CatagorizePosition();

					_pmove.Velocity -= _pmove.BaseVelocity;

					CheckVelocity();

					if ( !InWater() )
						FixupGravityVelocity();

					if ( _pmove.OnGround != -1 )
						_pmove.Velocity.z = 0f;

					CheckFalling();
				}

				PlayWaterSounds();
				break;
		}
	}

	private void CheckParameters()
	{
		var speed = (_pmove.Cmd.ForwardMove * _pmove.Cmd.ForwardMove)
			+ (_pmove.Cmd.SideMove * _pmove.Cmd.SideMove)
			+ (_pmove.Cmd.UpMove * _pmove.Cmd.UpMove);

		speed = MathF.Sqrt( speed );

		var maxSpeed = _pmove.ClientMaxSpeed;
		if ( maxSpeed != 0f )
			_pmove.MaxSpeed = MathF.Min( maxSpeed, _pmove.MaxSpeed );

		if ( _pmove.OnGround != -1 && HasButton( InButtons.Use ) )
			_pmove.MaxSpeed *= 1f / 3f;

		if ( speed != 0f && speed > _pmove.MaxSpeed )
		{
			var ratio = _pmove.MaxSpeed / speed;

			_pmove.Cmd.ForwardMove *= ratio;
			_pmove.Cmd.SideMove *= ratio;
			_pmove.Cmd.UpMove *= ratio;
		}

		if ( (_pmove.Flags & EntityFlags.Frozen) == EntityFlags.Frozen || (_pmove.Flags & EntityFlags.OnTrain) == EntityFlags.OnTrain || _pmove.Dead )
		{
			_pmove.Cmd.ForwardMove = 0f;
			_pmove.Cmd.SideMove = 0f;
			_pmove.Cmd.UpMove = 0f;
		}

		DropPunchAngle( ref _pmove.PunchAngle );

		if ( !_pmove.Dead )
		{
			var viewAngle = _pmove.Cmd.ViewAngles + _pmove.PunchAngle;

			_pmove.Angles.z = CalcRoll( viewAngle, _pmove.Velocity, _pmove.MoveVars.RollAngle, _pmove.MoveVars.RollSpeed ) * 4f;
			_pmove.Angles.x = viewAngle.x;
			_pmove.Angles.y = viewAngle.y;
		}
		else
		{
			_pmove.Angles = _pmove.OldAngles;
		}

		if ( _pmove.Dead )
			_pmove.ViewOffset.z = DeadViewHeight;

		if ( _pmove.Angles.y > 180f )
			_pmove.Angles.y -= 360f;
	}

	private void ReduceTimers()
	{
		if ( _pmove.TimeStepSound > 0 )
		{
			_pmove.TimeStepSound -= _pmove.Cmd.Msec;
			if ( _pmove.TimeStepSound < 0 )
				_pmove.TimeStepSound = 0;
		}

		if ( _pmove.DuckTime > 0f )
		{
			_pmove.DuckTime -= _pmove.Cmd.Msec;
			if ( _pmove.DuckTime < 0f )
				_pmove.DuckTime = 0f;
		}

		if ( _pmove.SwimTime > 0f )
		{
			_pmove.SwimTime -= _pmove.Cmd.Msec;
			if ( _pmove.SwimTime < 0f )
				_pmove.SwimTime = 0f;
		}
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

	private void DropPunchAngle( ref Vector3 punchAngle )
	{
		var length = MathLib.VectorNormalize( ref punchAngle );

		length = (float)(length - ((10.0 + (length * 0.5)) * _pmove.FrameTime));
		length = MathF.Max( length, 0f );

		punchAngle *= length;
	}

	private bool HasButton( InButtons button )
	{
		return (_pmove.Cmd.Buttons & (ushort)button) != 0;
	}
}
