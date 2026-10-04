using System;
using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "func_wall" )]
public class FuncWall : BaseEntity
{
	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override void Spawn()
	{
		Angles = Vector3.Zero;
		MoveType = MoveType.Push;
		Solid = Solid.Bsp;
		SetModel( ModelName );

		EntityFlags |= EntityFlags.WorldBrush;
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( ShouldToggle( useType, (int)Frame != 0 ) )
			Frame = 1 - Frame;
	}
}

[LinkEntityToClass( "func_wall_toggle" )]
public class FuncWallToggle : FuncWall
{
	private const int SpawnFlagStartOff = 0x0001;

	public bool IsOn => Solid != Solid.Not;

	public override void Spawn()
	{
		base.Spawn();

		if ( (SpawnFlags & SpawnFlagStartOff) != 0 )
			TurnOff();
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		var status = IsOn;

		if ( !ShouldToggle( useType, status ) )
			return;

		if ( status )
			TurnOff();
		else
			TurnOn();
	}

	public void TurnOff()
	{
		Solid = Solid.Not;
		Effects |= Effects.NoDraw;
		SetOrigin( Origin );
	}

	public void TurnOn()
	{
		Solid = Solid.Bsp;
		Effects &= ~Effects.NoDraw;
		SetOrigin( Origin );
	}
}

[LinkEntityToClass( "func_conveyor" )]
public class FuncConveyor : FuncWall
{
	private const int SpawnFlagVisual = 0x0001;
	private const int SpawnFlagNotSolid = 0x0002;

	public override void Spawn()
	{
		SetMoveDir();
		base.Spawn();

		if ( (SpawnFlags & SpawnFlagVisual) == 0 )
			EntityFlags |= EntityFlags.Conveyor;

		if ( (SpawnFlags & SpawnFlagNotSolid) != 0 )
		{
			Solid = Solid.Not;
			Skin = 0;
		}

		if ( Speed == 0f )
			Speed = 100f;

		UpdateSpeed( Speed );
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		Speed = -Speed;
		UpdateSpeed( Speed );
	}

	public void UpdateSpeed( float speed )
	{
		var speedCode = (int)(MathF.Abs( speed ) * 16f);

		RenderColor = new Vector3( speed < 0f ? 1f : 0f, speedCode >> 8, speedCode & 0xFF );
	}
}

[LinkEntityToClass( "func_monsterclip" )]
public class FuncMonsterClip : FuncWall
{
	public override void Spawn()
	{
		base.Spawn();

		if ( BaseTrigger.ShowTriggers == 0f )
			Effects = Effects.NoDraw;

		EntityFlags |= EntityFlags.MonsterClip;
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
	}
}

[LinkEntityToClass( "func_illusionary" )]
public class FuncIllusionary : BaseToggle
{
	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override bool KeyValue( string key, string value )
	{
		if ( key == "skin" )
		{
			Skin = (int)Util.Atof( value );
			return true;
		}

		return base.KeyValue( key, value );
	}

	public override void Spawn()
	{
		Angles = Vector3.Zero;
		MoveType = MoveType.None;
		Solid = Solid.Not;
		SetModel( ModelName );
	}
}

[LinkEntityToClass( "func_rotating" )]
public class FuncRotating : BaseEntity
{
	private const int SpawnFlagInstant = 1;
	private const int SpawnFlagBackwards = 2;
	private const int SpawnFlagZAxis = 4;
	private const int SpawnFlagXAxis = 8;
	private const int SpawnFlagAccelerate = 16;
	private const int SpawnFlagHurt = 32;
	private const int SpawnFlagNotSolid = 64;
	private const int SpawnFlagSmallRadius = 128;
	private const int SpawnFlagMediumRadius = 256;
	private const int SpawnFlagLargeRadius = 512;

	private const int FanPitchMin = 30;
	private const int FanPitchMax = 100;

	[Save( FieldType.Float )]
	public float FanFriction { get; set; }
	[Save( FieldType.Float )]
	public float Attenuation { get; set; }
	[Save( FieldType.Float )]
	public float Volume { get; set; }
	[Save( FieldType.Float )]
	public float Pitch { get; set; }
	[Save( FieldType.Integer )]
	public int Sounds { get; set; }

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "fanfriction":
				FanFriction = Util.Atof( value ) / 100f;
				return true;

			case "Volume":
				Volume = (float)(Util.Atof( value ) / 10.0);

				if ( Volume > 1f )
					Volume = 1f;

				if ( Volume < 0f )
					Volume = 0f;

				return true;

			case "spawnorigin":
				var origin = Util.StringToVector( value );

				if ( origin.x != 0f || origin.y != 0f || origin.z != 0f )
					Origin = origin;

				return false;

			case "sounds":
				Sounds = Util.Atoi( value );
				return true;

			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Spawn()
	{
		Pitch = Server.PitchNormal - 1;

		if ( Volume == 0f )
			Volume = 1f;

		Attenuation = Server.AttenuationNormal;

		if ( (SpawnFlags & SpawnFlagSmallRadius) != 0 )
			Attenuation = Server.AttenuationIdle;
		else if ( (SpawnFlags & SpawnFlagMediumRadius) != 0 )
			Attenuation = Server.AttenuationStatic;
		else if ( (SpawnFlags & SpawnFlagLargeRadius) != 0 )
			Attenuation = Server.AttenuationNormal;

		if ( FanFriction == 0f )
			FanFriction = 1f;

		if ( (SpawnFlags & SpawnFlagZAxis) != 0 )
			MoveDir = new Vector3( 0f, 0f, 1f );
		else if ( (SpawnFlags & SpawnFlagXAxis) != 0 )
			MoveDir = new Vector3( 1f, 0f, 0f );
		else
			MoveDir = new Vector3( 0f, 1f, 0f );

		if ( (SpawnFlags & SpawnFlagBackwards) != 0 )
			MoveDir *= -1f;

		if ( (SpawnFlags & SpawnFlagNotSolid) != 0 )
		{
			Solid = Solid.Not;
			Skin = (int)GoldSrc.Bsp.Contents.Empty;
			MoveType = MoveType.Push;
		}
		else
		{
			Solid = Solid.Bsp;
			MoveType = MoveType.Push;
		}

		SetOrigin( Origin );
		SetModel( ModelName );

		SetUse( RotatingUse );

		if ( Speed <= 0f )
			Speed = 0f;

		if ( (SpawnFlags & SpawnFlagInstant) != 0 )
		{
			SetThink( CallUseToggle );
			NextThink = (float)(LTime + 1.5);
		}

		if ( (SpawnFlags & SpawnFlagHurt) != 0 )
			SetTouch( HurtTouch );

		Precache();
	}

	public override void Precache()
	{
		if ( !string.IsNullOrEmpty( Message ) )
		{
			Server.PrecacheSound( Message );
			Noise3 = Server.AllocString( Message );
		}
		else if ( Sounds is >= 1 and <= 5 )
		{
			Noise3 = $"fans/fan{Sounds}.wav";
			Server.PrecacheSound( Noise3 );
		}
		else
		{
			Noise3 = "common/null.wav";
		}

		if ( AVelocity.x != 0f || AVelocity.y != 0f || AVelocity.z != 0f )
		{
			SetThink( SpinUp );
			NextThink = (float)(LTime + 1.5);
		}
	}

	public override void Blocked( BaseEntity other )
	{
		other.TakeDamage( this, this, Damage, DamageCrush );
	}

	private void HurtTouch( BaseEntity other )
	{
		if ( other.DamageMode == DamageNo )
			return;

		Damage = MathLib.Length( AVelocity ) / 10f;

		other.TakeDamage( this, this, Damage, DamageCrush );

		other.Velocity = Util.Normalize( other.Origin - BModelOrigin ) * Damage;
	}

	private void RampPitchVol()
	{
		var current = MathF.Abs( AVelocity.x != 0f ? AVelocity.x : (AVelocity.y != 0f ? AVelocity.y : AVelocity.z) );

		var final = MoveDir.x != 0f ? MoveDir.x : (MoveDir.y != 0f ? MoveDir.y : MoveDir.z);
		final *= Speed;
		final = MathF.Abs( final );

		var fraction = current / final;
		var volume = Volume * fraction;

		var pitch = (int)(FanPitchMin + ((FanPitchMax - FanPitchMin) * fraction));
		if ( pitch == Server.PitchNormal )
			pitch = Server.PitchNormal - 1;

		Server.EmitSound( this, SoundChannel.Static, Noise3, volume, Attenuation, Server.SoundChangePitch | Server.SoundChangeVolume, pitch );
	}

	private void SpinUp()
	{
		NextThink = (float)(LTime + 0.1);
		AVelocity += MoveDir * (Speed * FanFriction);

		var velocity = AVelocity;

		if ( MathF.Abs( velocity.x ) >= MathF.Abs( MoveDir.x * Speed )
			&& MathF.Abs( velocity.y ) >= MathF.Abs( MoveDir.y * Speed )
			&& MathF.Abs( velocity.z ) >= MathF.Abs( MoveDir.z * Speed ) )
		{
			AVelocity = MoveDir * Speed;

			Server.EmitSound( this, SoundChannel.Static, Noise3, Volume, Attenuation, Server.SoundChangePitch | Server.SoundChangeVolume, FanPitchMax );

			SetThink( Rotate );
			Rotate();
		}
		else
		{
			RampPitchVol();
		}
	}

	private void SpinDown()
	{
		NextThink = (float)(LTime + 0.1);

		AVelocity -= MoveDir * (Speed * FanFriction);

		var velocity = AVelocity;
		var direction = MoveDir.x != 0f ? MoveDir.x : (MoveDir.y != 0f ? MoveDir.y : MoveDir.z);

		if ( (direction > 0f && velocity.x <= 0f && velocity.y <= 0f && velocity.z <= 0f)
			|| (direction < 0f && velocity.x >= 0f && velocity.y >= 0f && velocity.z >= 0f) )
		{
			AVelocity = Vector3.Zero;

			Server.EmitSound( this, SoundChannel.Static, Noise3, 0f, 0f, Server.SoundStop, (int)Pitch );

			SetThink( Rotate );
			Rotate();
		}
		else
		{
			RampPitchVol();
		}
	}

	private void Rotate()
	{
		NextThink = LTime + 10f;
	}

	private void RotatingUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		var spinning = AVelocity.x != 0f || AVelocity.y != 0f || AVelocity.z != 0f;

		if ( spinning )
		{
			SetThink( SpinDown );
			NextThink = (float)(LTime + 0.1);
		}
		else if ( (SpawnFlags & SpawnFlagAccelerate) != 0 )
		{
			SetThink( SpinUp );
			Server.EmitSound( this, SoundChannel.Static, Noise3, 0.01f, Attenuation, 0, FanPitchMin );

			NextThink = (float)(LTime + 0.1);
		}
		else
		{
			Server.EmitSound( this, SoundChannel.Static, Noise3, Volume, Attenuation, 0, FanPitchMax );
			AVelocity = MoveDir * Speed;

			SetThink( Rotate );
			Rotate();
		}
	}
}

[LinkEntityToClass( "func_pendulum" )]
public class Pendulum : BaseEntity
{
	private const int SpawnFlagInstant = 1;
	private const int SpawnFlagSwing = 2;
	private const int SpawnFlagPassable = 8;
	private const int SpawnFlagAutoReturn = 16;

	[Save( FieldType.Float )]
	public float Distance { get; set; }
	[Save( FieldType.Float )]
	public float Damp { get; set; }

	[Save( FieldType.Float )]
	private float _accel;
	[Save( FieldType.Time )]
	private float _time;
	[Save( FieldType.Float )]
	private float _maxSpeed;
	[Save( FieldType.Float )]
	private float _dampSpeed;
	[Save( FieldType.Vector )]
	private Vector3 _center;
	[Save( FieldType.Vector )]
	private Vector3 _start;

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "distance":
				Distance = Util.Atof( value );
				return true;
			case "damp":
				Damp = (float)(Util.Atof( value ) * 0.001);
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Spawn()
	{
		BaseToggle.AxisDir( this );

		Solid = (SpawnFlags & SpawnFlagPassable) != 0 ? Solid.Not : Solid.Bsp;
		MoveType = MoveType.Push;

		SetOrigin( Origin );
		SetModel( ModelName );

		if ( Distance == 0f )
			return;

		if ( Speed == 0f )
			Speed = 100f;

		_accel = (Speed * Speed) / (2f * MathF.Abs( Distance ));
		_maxSpeed = Speed;
		_start = Angles;
		_center = Angles + ((float)(Distance * 0.5) * MoveDir);

		if ( (SpawnFlags & SpawnFlagInstant) != 0 )
		{
			SetThink( CallUseToggle );
			NextThink = (float)(Server.Globals.Time + 0.1);
		}

		Speed = 0f;
		SetUse( PendulumUse );

		if ( (SpawnFlags & SpawnFlagSwing) != 0 )
			SetTouch( RopeTouch );
	}

	public override void Blocked( BaseEntity other )
	{
		_time = Server.Globals.Time;
	}

	public override void Touch( BaseEntity other )
	{
		if ( Damage <= 0f )
			return;

		if ( other.DamageMode == DamageNo )
			return;

		var damage = (float)(Damage * Speed * 0.01);

		if ( damage < 0f )
			damage = -damage;

		other.TakeDamage( this, this, damage, DamageCrush );

		other.Velocity = Util.Normalize( other.Origin - BModelOrigin ) * damage;
	}

	private void PendulumUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( Speed != 0f )
		{
			if ( (SpawnFlags & SpawnFlagAutoReturn) != 0 )
			{
				var delta = BaseToggle.AxisDelta( SpawnFlags, Angles, _start );

				AVelocity = _maxSpeed * MoveDir;
				NextThink = LTime + (delta / _maxSpeed);
				SetThink( Stop );
			}
			else
			{
				Speed = 0f;
				SetThink( null );
				AVelocity = Vector3.Zero;
			}
		}
		else
		{
			NextThink = (float)(LTime + 0.1);
			_time = Server.Globals.Time;
			SetThink( Swing );
			_dampSpeed = _maxSpeed;
		}
	}

	private void Stop()
	{
		Angles = _start;
		Speed = 0f;
		SetThink( null );
		AVelocity = Vector3.Zero;
	}

	private void Swing()
	{
		var delta = BaseToggle.AxisDelta( SpawnFlags, Angles, _center );
		var dt = Server.Globals.Time - _time;

		_time = Server.Globals.Time;

		if ( delta > 0f && _accel > 0f )
			Speed -= _accel * dt;
		else
			Speed += _accel * dt;

		if ( Speed > _maxSpeed )
			Speed = _maxSpeed;
		else if ( Speed < -_maxSpeed )
			Speed = -_maxSpeed;

		AVelocity = Speed * MoveDir;

		NextThink = (float)(LTime + 0.1);

		if ( Damp == 0f )
			return;

		_dampSpeed -= Damp * _dampSpeed * dt;

		if ( _dampSpeed < 30f )
		{
			Angles = _center;
			Speed = 0f;
			SetThink( null );
			AVelocity = Vector3.Zero;
		}
		else if ( Speed > _dampSpeed )
		{
			Speed = _dampSpeed;
		}
		else if ( Speed < -_dampSpeed )
		{
			Speed = -_dampSpeed;
		}
	}

	private void RopeTouch( BaseEntity other )
	{
		if ( !other.IsPlayer )
		{
			Log.Info( "Not a client" );
			return;
		}

		if ( other == Enemy )
			return;

		Enemy = other;
		other.Velocity = Vector3.Zero;
		other.MoveType = MoveType.None;
	}
}
