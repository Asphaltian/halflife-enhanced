using System;
using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "func_vehicle" )]
public class FuncVehicle : BaseEntity
{
	private const int StartPitch = 60;
	private const int MaxPitch = 200;
	private const int PitchMaxSpeed = 1500;

	public Player Driver;

	[Save( FieldType.ClassPtr )]
	private PathTrack _path;
	[Save( FieldType.Float )]
	private float _length;
	private float _width;
	[Save( FieldType.Float )]
	private float _height;
	[Save( FieldType.Float )]
	private float _speed;
	[Save( FieldType.Float )]
	private float _dir;
	[Save( FieldType.Float )]
	private float _startSpeed;
	[Save( FieldType.Vector )]
	private Vector3 _controlMins;
	[Save( FieldType.Vector )]
	private Vector3 _controlMaxs;
	private bool _soundPlaying;
	[Save( FieldType.Integer )]
	private int _sounds;
	private int _acceleration;
	[Save( FieldType.Float )]
	private float _volume;
	private int _turnAngle;
	private float _steeringWheelDecay;
	private float _acceleratorDecay;
	private float _turnStartTime;
	private float _launchTime;
	private float _canTurnNow;
	private float _updateSound;
	private Vector3 _frontLeft;
	private Vector3 _front;
	private Vector3 _frontRight;
	private Vector3 _backLeft;
	private Vector3 _back;
	private Vector3 _backRight;
	private Vector3 _surfaceNormal;
	private Vector3 _vehicleDirection;

	private int _adjustPitch;

	public override ObjectCaps ObjectCaps => (base.ObjectCaps & ~ObjectCaps.AcrossTransition) | ObjectCaps.DirectionalUse;

	public override int Classify() => ClassVehicle;

	private static float Fix2( float angle )
	{
		while ( angle < 0f )
			angle += 360f;

		while ( angle > 360f )
			angle -= 360f;

		return angle;
	}

	private static Vector3 FixupAngles2( Vector3 angles ) => new( Fix2( angles.x ), Fix2( angles.y ), Fix2( angles.z ) );

	public override bool KeyValue( string key, string value )
	{
		if ( key == "length" )
			_length = Util.Atof( value );

		switch ( key )
		{
			case "width":
				_width = Util.Atof( value );
				return true;
			case "height":
				_height = Util.Atof( value );
				return true;
			case "startspeed":
				_startSpeed = Util.Atof( value );
				return true;
			case "sounds":
				_sounds = Util.Atoi( value );
				return true;
			case "volume":
				_volume = Util.Atoi( value );
				_volume = (float)(_volume * 0.1);
				return true;
			case "bank":
				return true;
			case "acceleration":
				_acceleration = Math.Clamp( Util.Atoi( value ), 1, 10 );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	private void SetNextThink( float thinkTime, bool alwaysThink )
	{
		if ( alwaysThink )
			EntityFlags |= EntityFlags.AlwaysThink;
		else
			EntityFlags &= ~EntityFlags.AlwaysThink;

		NextThink = thinkTime;
	}

	public override void Blocked( BaseEntity other )
	{
		if ( (other.EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround && other.GroundEntity == this )
		{
			other.Velocity = Velocity;
			return;
		}

		other.Velocity = Util.Normalize( other.Origin - Origin ) * Damage;
		other.Velocity += new Vector3( 0f, 0f, 300f );

		Velocity *= 0.85f;

		Util.Alert( AlertType.Console, $"TRAIN({TargetName}): Blocked by {other.ClassName} (dmg:{Damage:F2})" );

		var globals = Server.Globals;

		globals.MakeVectors( Angles );

		var forward = globals.Forward * -1f * (_length / 2f);
		var right = globals.Right * -1f * (_width / 2f);

		var frontLeft = Origin + forward - right;
		var backRight = Origin - forward + right;

		var origin = other.Origin;

		var minX = Math.Min( frontLeft.x, backRight.x );
		var maxX = Math.Max( frontLeft.x, backRight.x );
		var minY = Math.Min( frontLeft.y, backRight.y );
		var maxY = Math.Max( frontLeft.y, backRight.y );
		var minZ = Origin.z;
		var maxZ = Origin.z + (MathF.Abs( Mins.z - Maxs.z ) * 2f);

		if ( origin.x < minX || origin.x > maxX || origin.y < minY || origin.y > maxY || origin.z < minZ || origin.z > maxZ )
			other.TakeDamage( this, this, 150f, DamageCrush );
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( useType != UseType.Set )
		{
			if ( !ShouldToggle( useType, Speed != 0f ) )
				return;

			if ( Speed == 0f )
			{
				Speed = _speed * _dir;

				Next();
			}
			else
			{
				Speed = 0f;
				Velocity = Vector3.Zero;
				AVelocity = Vector3.Zero;
				StopSound();
				SetThink( null );
			}

			return;
		}

		if ( value >= 10f )
		{
			if ( _canTurnNow >= Server.Globals.Time )
				return;

			if ( value == 20f )
			{
				_turnAngle += 1;
				_steeringWheelDecay = (float)(Server.Globals.Time + 0.075);

				if ( _turnAngle > 8 )
					_turnAngle = 8;
			}
			else if ( value == 30f )
			{
				_turnAngle -= 1;
				_steeringWheelDecay = (float)(Server.Globals.Time + 0.075);

				if ( _turnAngle < -8 )
					_turnAngle = -8;
			}

			_canTurnNow = (float)(Server.Globals.Time + 0.05);
			return;
		}

		if ( value < 0f && Speed > 145f )
			StopSound();

		var delta = value;

		if ( delta > 0f )
		{
			var speedRatio = Speed / _speed;

			if ( Speed < 0f )
				delta = (float)(speedRatio + (0.015 / 3) + (_acceleration * 0.0005));
			else if ( Speed < 10f )
				delta = (float)(speedRatio + (0.015 / 7) + (_acceleration * 0.0006));
			else if ( Speed < 20f )
				delta = (float)(speedRatio + (0.02 / 6) + (_acceleration * 0.0007));
			else if ( Speed < 30f )
				delta = (float)(speedRatio + (0.025 / 6) + (_acceleration * 0.0007));
			else if ( Speed < 45f )
				delta = (float)(speedRatio + (0.02 / 5) + (_acceleration * 0.0007));
			else if ( Speed < 60f )
				delta = (float)(speedRatio + (0.019 / 5) + (_acceleration * 0.0008));
			else if ( Speed < 80f )
				delta = (float)(speedRatio + (0.018 / 4) + (_acceleration * 0.0008));
			else if ( Speed < 100f )
				delta = (float)(speedRatio + (0.017 / 4) + (_acceleration * 0.0009));
			else if ( Speed < 150f )
				delta = (float)(speedRatio + (0.016 / 6) + (_acceleration * 0.0008));
			else if ( Speed < 225f )
				delta = (float)(speedRatio + (0.016 / 7) + (_acceleration * 0.0007));
			else if ( Speed < 300f )
				delta = (float)(speedRatio + (0.015 / 8) + (_acceleration * 0.0006));
			else if ( Speed < 400f )
				delta = (float)(speedRatio + (0.013 / 9) + (_acceleration * 0.0005));
			else if ( Speed < 550f )
				delta = (float)(speedRatio + (0.012 / 10) + (_acceleration * 0.0005));
			else if ( Speed < 800f )
				delta = (float)(speedRatio + (0.011 / 12) + (_acceleration * 0.0005));
		}
		else if ( delta < 0f )
		{
			var speedRatio = Speed / _speed;

			if ( speedRatio > 0f )
				delta = (float)(speedRatio - (0.05 / 4));
			else if ( speedRatio <= 0f && speedRatio > -0.05 )
				delta = (float)(speedRatio - (0.03 / 4));
			else if ( speedRatio <= 0.05 && speedRatio > -0.1 )
				delta = (float)(speedRatio - (0.04 / 4));
			else if ( speedRatio <= 0.15 && speedRatio > -0.15 )
				delta = (float)(speedRatio - (0.05 / 4));
			else if ( speedRatio <= 0.15 && speedRatio > -0.22 )
				delta = (float)(speedRatio - (0.055 / 4));
			else if ( speedRatio <= 0.22 && speedRatio > -0.3 )
				delta = (float)(speedRatio - (0.07 / 4));
			else if ( speedRatio <= 0.3 )
				delta = (float)(speedRatio - (0.05 / 4));
		}

		if ( delta > 1f )
			delta = 1f;
		else if ( delta < -0.35 )
			delta = -0.35f;

		Speed = _speed * delta;

		Next();

		_acceleratorDecay = (float)(Server.Globals.Time + 0.25);
	}

	private void StopSound()
	{
		if ( _soundPlaying && !string.IsNullOrEmpty( Noise ) )
		{
			var encodedSound = (ushort)(((ushort)_sounds & 0x0007) << 12);

			Server.PlaybackEvent( EventFlags.Reliable | EventFlags.Update, this, _adjustPitch, 0f, Vector3.Zero, Vector3.Zero, 0f, 0f, encodedSound, 0, 1, 0 );
		}

		_soundPlaying = false;
	}

	private void UpdateSound()
	{
		if ( string.IsNullOrEmpty( Noise ) )
			return;

		var pitch = StartPitch + (MathF.Abs( Speed ) * (MaxPitch - StartPitch) / PitchMaxSpeed);

		if ( pitch > MaxPitch )
			pitch = MaxPitch;

		if ( !_soundPlaying )
		{
			if ( _sounds < 5 )
				Sound.EmitDyn( this, SoundChannel.Item, "plats/vehicle_brake1.wav", _volume, Server.AttenuationNormal, 0, 100 );

			Sound.EmitDyn( this, SoundChannel.Static, Noise, _volume, Server.AttenuationNormal, 0, (int)pitch );
			_soundPlaying = true;
			return;
		}

		var encodedSound = (ushort)(((ushort)_sounds & 0x0007) << 12);
		var encodedPitch = (ushort)(((ushort)(pitch / 10.0) & 0x003f) << 6);
		var encodedVolume = (ushort)((ushort)(_volume * 40.0) & 0x003f);

		Server.PlaybackEvent( EventFlags.Update, this, _adjustPitch, 0f, Vector3.Zero, Vector3.Zero, 0f, 0f, (ushort)(encodedSound | encodedPitch | encodedVolume), 0, 0, 0 );
	}

	private void CheckTurning()
	{
		var right = Server.Globals.Right;

		if ( _turnAngle < 0 )
		{
			if ( Speed > 0f )
			{
				if ( Util.TraceLine( _frontRight, _frontRight - (right * 16f), IgnoreMonsters, this ).Fraction != 1.0 )
					_turnAngle = 1;
			}
			else if ( Speed < 0f )
			{
				if ( Util.TraceLine( _backLeft, _backLeft + (right * 16f), IgnoreMonsters, this ).Fraction != 1.0 )
					_turnAngle = 1;
			}
		}
		else if ( _turnAngle > 0 )
		{
			if ( Speed > 0f )
			{
				if ( Util.TraceLine( _frontLeft, _frontLeft + (right * 16f), IgnoreMonsters, this ).Fraction != 1.0 )
					_turnAngle = -1;
			}
			else if ( Speed < 0f )
			{
				if ( Util.TraceLine( _backRight, _backRight - (right * 16f), IgnoreMonsters, this ).Fraction != 1.0 )
					_turnAngle = -1;
			}
		}

		if ( Speed <= 0f )
			return;

		float maxSpeed;

		if ( Math.Abs( _turnAngle ) > 4 )
		{
			if ( _turnStartTime == -1f )
			{
				_turnStartTime = Server.Globals.Time;
				maxSpeed = _speed;
			}
			else
			{
				maxSpeed = (float)(_speed * 0.98);
			}
		}
		else if ( Math.Abs( _turnAngle ) > 2 )
		{
			_turnStartTime = -1f;
			maxSpeed = (float)(_speed * 0.9);
		}
		else
		{
			maxSpeed = _speed;
			_turnStartTime = -1f;
		}

		if ( Speed > maxSpeed )
			Speed -= _speed / 10f;
	}

	private void HitSurface( in Trace trace, bool reversing )
	{
		var dot = Vector3.Dot( trace.PlaneNormal, Server.Globals.Forward * -1f );

		if ( (reversing ? dot < 0.7 : dot > -0.7) && trace.PlaneNormal.z < 0.1 )
		{
			_surfaceNormal = trace.PlaneNormal;
			_surfaceNormal.z = 0f;
			Speed = (float)(Speed * 0.99);
		}
		else if ( trace.PlaneNormal.z < 0.65 || trace.StartSolid )
		{
			Speed *= -1f;
		}
		else
		{
			_surfaceNormal = trace.PlaneNormal;
		}
	}

	private void CollisionDetection()
	{
		var forward = Server.Globals.Forward;

		if ( Speed < 0f )
		{
			var trace = Util.TraceLine( _backLeft, _backLeft + (forward * 16f), IgnoreMonsters, this );

			if ( trace.Fraction != 1.0 )
			{
				HitSurface( trace, true );

				if ( trace.Ent is BaseEntity hit && hit.Classify() == ClassVehicle )
					Util.Alert( AlertType.Console, "I hit another vehicle" );

				return;
			}

			trace = Util.TraceLine( _backRight, _backRight + (forward * 16f), IgnoreMonsters, this );

			if ( trace.Fraction != 1.0 )
			{
				HitSurface( trace, true );
				return;
			}

			trace = Util.TraceLine( _back, _back + (forward * 16f), IgnoreMonsters, this );

			if ( trace.Fraction != 1.0 )
				HitSurface( trace, true );
		}
		else if ( Speed > 0f )
		{
			var trace = Util.TraceLine( _frontLeft, _frontLeft - (forward * 16f), DontIgnoreMonsters, this );

			if ( trace.Fraction != 1.0 )
			{
				HitSurface( trace, false );
				return;
			}

			trace = Util.TraceLine( _frontRight, _frontRight - (forward * 16f), IgnoreMonsters, this );

			if ( trace.Fraction != 1.0 )
			{
				HitSurface( trace, false );
				return;
			}

			trace = Util.TraceLine( _front, _front - (forward * 16f), IgnoreMonsters, this );

			if ( trace.Fraction != 1.0 )
				HitSurface( trace, false );
		}
	}

	private void TerrainFollowing()
	{
		var trace = Util.TraceLine( Origin, Origin + new Vector3( 0f, 0f, -1f * (_height + 48f) ), IgnoreMonsters, this );

		if ( trace.Fraction != 1.0 )
			_surfaceNormal = trace.PlaneNormal;
		else if ( trace.InWater )
			_surfaceNormal = new Vector3( 0f, 0f, 1f );
	}

	public void Next()
	{
		var globals = Server.Globals;
		var gravity = Vector3.Zero;

		globals.MakeVectors( Angles );

		var forward = globals.Forward * -1f * (_length / 2f);
		var right = globals.Right * -1f * (_width / 2f);
		var up = globals.Up * 16f;

		_frontLeft = Origin + forward - right + up;
		_frontRight = Origin + forward + right + up;
		_front = Origin + forward + up;
		_backLeft = Origin - forward - right + up;
		_backRight = Origin - forward + right + up;
		_back = Origin - forward + up;

		_surfaceNormal = Vector3.Zero;

		CheckTurning();

		if ( _steeringWheelDecay < globals.Time )
		{
			_steeringWheelDecay = (float)(globals.Time + 0.1);

			if ( _turnAngle < 0 )
				_turnAngle++;
			else if ( _turnAngle > 0 )
				_turnAngle--;
		}

		if ( _acceleratorDecay < globals.Time )
		{
			_acceleratorDecay = (float)(globals.Time + 0.1);

			if ( Speed < 0f )
			{
				Speed += 20f;

				if ( Speed > 0f )
					Speed = 0f;
			}
			else if ( Speed > 0f )
			{
				Speed -= 20f;

				if ( Speed < 0f )
					Speed = 0f;
			}
		}

		if ( Speed == 0f )
		{
			_turnAngle = 0;
			AVelocity = Vector3.Zero;
			Velocity = Vector3.Zero;

			SetThink( Next );
			SetNextThink( (float)(LTime + 0.1), true );
			return;
		}

		TerrainFollowing();
		CollisionDetection();

		if ( !MathLib.VectorCompare( _surfaceNormal, Vector3.Zero ) )
		{
			_vehicleDirection = MathLib.CrossProduct( _surfaceNormal, globals.Forward );
			_vehicleDirection = MathLib.CrossProduct( _surfaceNormal, _vehicleDirection );

			var targetAngle = MathLib.VectorAngles( _vehicleDirection );

			targetAngle.y += 180f;

			if ( _turnAngle != 0 )
				targetAngle.y += _turnAngle;

			targetAngle = FixupAngles2( targetAngle );

			var angle = FixupAngles2( Angles );

			var vx = Math.Clamp( Util.AngleDistance( targetAngle.x, angle.x ), -10f, 10f );
			var vy = Math.Clamp( Util.AngleDistance( targetAngle.y, angle.y ), -10f, 10f );

			AVelocity = AVelocity with { y = (int)(vy * 10f), x = (int)(vx * 10f) };

			_launchTime = -1f;
		}
		else
		{
			if ( _launchTime == -1f )
			{
				_launchTime = globals.Time;
				Velocity *= 1.5f;
			}
			else
			{
				gravity = new Vector3( 0f, 0f, (globals.Time - _launchTime) * -35f );

				if ( gravity.z < -400f )
					gravity.z = -400f;
			}

			_vehicleDirection = globals.Forward * -1f;
		}

		if ( _updateSound < globals.Time )
		{
			UpdateSound();
			_updateSound = globals.Time + 1f;
		}

		if ( !MathLib.VectorCompare( _surfaceNormal, Vector3.Zero ) )
			Velocity = Speed * Util.Normalize( _vehicleDirection );
		else
			Velocity += gravity;

		SetThink( Next );
		SetNextThink( (float)(LTime + 0.1), true );
	}

	public void SetControls( BaseEntity controls )
	{
		var offset = controls.Origin - OldOrigin;

		_controlMins = controls.Mins + offset;
		_controlMaxs = controls.Maxs + offset;
	}

	public override bool OnControls( BaseEntity entity )
	{
		var offset = entity.Origin - Origin;

		if ( (SpawnFlags & FuncTrackTrain.SpawnFlagNoControl) != 0 )
			return false;

		Server.Globals.MakeVectors( Angles );

		var local = new Vector3(
			Vector3.Dot( offset, Server.Globals.Forward ),
			-Vector3.Dot( offset, Server.Globals.Right ),
			Vector3.Dot( offset, Server.Globals.Up ) );

		return local.x >= _controlMins.x && local.y >= _controlMins.y && local.z >= _controlMins.z
			&& local.x <= _controlMaxs.x && local.y <= _controlMaxs.y && local.z <= _controlMaxs.z;
	}

	public void Find()
	{
		_path = Util.FindEntityByTargetname( null, Target ) is PathTrack { ClassName: "path_track" } path ? path : null;

		if ( _path is null )
			return;

		var nextPosition = _path.Origin;

		nextPosition.z += _height;

		var look = nextPosition;

		look.z -= _height;
		_path.LookAhead( ref look, _length, false );
		look.z += _height;

		var angles = MathLib.VectorAngles( look - nextPosition );

		angles.y += 180f;

		if ( (SpawnFlags & FuncTrackTrain.SpawnFlagNoPitch) != 0 )
			angles.x = 0f;

		Angles = angles;

		SetOrigin( nextPosition );
		SetNextThink( (float)(LTime + 0.1), false );
		SetThink( Next );
		Speed = _startSpeed;

		UpdateSound();
	}

	public void NearestPath()
	{
		BaseEntity track = null;
		BaseEntity nearest = null;
		var closest = 1024f;

		while ( (track = Util.FindEntityInSphere( track, Origin, 1024f )) is not null )
		{
			if ( (track.EntityFlags & (EntityFlags.Client | EntityFlags.Monster)) != 0 || track.ClassName != "path_track" )
				continue;

			var distance = MathLib.Length( Origin - track.Origin );

			if ( distance < closest )
			{
				closest = distance;
				nearest = track;
			}
		}

		if ( nearest is null )
		{
			Util.Alert( AlertType.Console, "Can't find a nearby track !!!" );
			SetThink( null );
			return;
		}

		Util.Alert( AlertType.AiConsole, $"TRAIN: {TargetName}, Nearest track is {nearest.TargetName}" );

		track = ((PathTrack)nearest).GetNext();

		if ( track is not null && MathLib.Length( Origin - track.Origin ) < MathLib.Length( Origin - nearest.Origin ) )
			nearest = track;

		_path = (PathTrack)nearest;

		if ( Speed != 0f )
		{
			SetNextThink( (float)(LTime + 0.1), false );
			SetThink( Next );
		}
	}

	public override void OverrideReset()
	{
		SetNextThink( (float)(LTime + 0.1), false );
		SetThink( NearestPath );
	}

	public override void Spawn()
	{
		_speed = Speed == 0f ? 165f : Speed;

		if ( _sounds == 0 )
			_sounds = 3;

		Util.Alert( AlertType.Console, $"M_speed = {_speed:F6}" );

		Speed = 0f;
		Velocity = Vector3.Zero;
		AVelocity = Vector3.Zero;
		Impulse = (int)_speed;
		_acceleration = 5;

		_dir = 1f;
		_turnStartTime = -1f;
		_updateSound = -1f;
		Driver = null;

		if ( string.IsNullOrEmpty( Target ) )
			Util.Alert( AlertType.Console, "Vehicle with no target" );

		Solid = (SpawnFlags & FuncTrackTrain.SpawnFlagPassable) != 0 ? Solid.Not : Solid.Bsp;
		MoveType = MoveType.Push;

		SetModel( ModelName );

		SetSize( Mins, Maxs );
		SetOrigin( Origin );

		OldOrigin = Origin;

		_controlMins = Mins;
		_controlMaxs = Maxs;
		_controlMaxs.z += 72f;

		SetNextThink( (float)(LTime + 0.1), false );
		SetThink( Find );
		Precache();
	}

	public override void Precache()
	{
		if ( _volume == 0.0 )
			_volume = 1f;

		Noise = _sounds switch
		{
			1 => "plats/vehicle1.wav",
			2 => "plats/vehicle2.wav",
			3 => "plats/vehicle3.wav",
			4 => "plats/vehicle4.wav",
			5 => "plats/vehicle6.wav",
			_ => "plats/vehicle7.wav"
		};

		Server.PrecacheSound( Noise );

		Server.PrecacheSound( "plats/vehicle_brake1.wav" );
		Server.PrecacheSound( "plats/vehicle_start1.wav" );

		_adjustPitch = Server.PrecacheEvent( 1, "events/vehicle.sc" );
	}
}

[LinkEntityToClass( "func_vehiclecontrols" )]
public class FuncVehicleControls : BaseEntity
{
	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public void Find()
	{
		BaseEntity target = null;

		do
		{
			target = Util.FindEntityByTargetname( target, Target );
		}
		while ( target is not null && target.ClassName != "func_vehicle" );

		if ( target is null )
		{
			Util.Alert( AlertType.Console, $"No vehicle {Target}" );
			return;
		}

		((FuncVehicle)target).SetControls( this );
		Remove();
	}

	public override void Spawn()
	{
		Solid = Solid.Not;
		MoveType = MoveType.None;
		SetModel( ModelName );

		SetSize( Mins, Maxs );
		SetOrigin( Origin );

		SetThink( Find );
		NextThink = Server.Globals.Time;
	}
}
