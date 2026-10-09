using System;
using GoldSrc;

namespace HalfLife;

public enum TankBullet
{
	None,
	Bullet9mm,
	Mp5,
	Bullet12mm
}

public class FuncTank : BaseEntity
{
	private const int SpawnFlagActive = 0x0001;
	private const int SpawnFlagLineOfSight = 0x0010;
	private const int SpawnFlagCanControl = 0x0020;
	private const int SpawnFlagSoundOn = 0x8000;

	protected static readonly Vector3[] TankSpread =
	[
		new( 0f, 0f, 0f ),
		new( 0.025f, 0.025f, 0.025f ),
		new( 0.05f, 0.05f, 0.05f ),
		new( 0.1f, 0.1f, 0.1f ),
		new( 0.25f, 0.25f, 0.25f )
	];

	[Save( FieldType.ClassPtr )]
	private Player _controller;
	[Save( FieldType.Time )]
	private float _nextAttack;
	[Save( FieldType.Vector )]
	private Vector3 _controllerUsePosition;

	[Save( FieldType.Float )]
	private float _yawCenter;
	[Save( FieldType.Float )]
	private float _yawRate;
	[Save( FieldType.Float )]
	private float _yawRange;
	[Save( FieldType.Float )]
	private float _yawTolerance;

	[Save( FieldType.Float )]
	private float _pitchCenter;
	[Save( FieldType.Float )]
	private float _pitchRate;
	[Save( FieldType.Float )]
	private float _pitchRange;
	[Save( FieldType.Float )]
	private float _pitchTolerance;

	[Save( FieldType.Time )]
	protected float FireLast;
	[Save( FieldType.Float )]
	protected float FireRate;
	[Save( FieldType.Time )]
	private float _lastSightTime;
	[Save( FieldType.Float )]
	private float _persist;
	[Save( FieldType.Float )]
	private float _minRange;
	[Save( FieldType.Float )]
	private float _maxRange;

	[Save( FieldType.Vector )]
	private Vector3 _barrelPosition;
	[Save( FieldType.Float )]
	private float _spriteScale;
	[Save( FieldType.String )]
	private string _spriteSmoke;
	[Save( FieldType.String )]
	private string _spriteFlash;
	[Save( FieldType.Integer )]
	protected TankBullet BulletType;
	[Save( FieldType.Integer )]
	protected int BulletDamage;

	[Save( FieldType.Vector )]
	private Vector3 _sightOrigin;
	[Save( FieldType.Integer )]
	protected int Spread;
	[Save( FieldType.String )]
	private string _master;

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	private bool IsActive => (SpawnFlags & SpawnFlagActive) != 0;

	private bool CanFire => (Server.Globals.Time - _lastSightTime) < _persist;

	public override void Spawn()
	{
		Precache();

		MoveType = MoveType.Push;
		Solid = Solid.Bsp;
		SetModel( ModelName );

		_yawCenter = Angles.y;
		_pitchCenter = Angles.x;

		if ( IsActive )
			NextThink = (float)(LTime + 1.0);

		_sightOrigin = BarrelPosition();

		if ( FireRate <= 0f )
			FireRate = 1f;

		if ( Spread > TankSpread.Length )
			Spread = 0;

		OldOrigin = Origin;
	}

	public override void Precache()
	{
		if ( !string.IsNullOrEmpty( _spriteSmoke ) )
			Server.PrecacheModel( _spriteSmoke );

		if ( !string.IsNullOrEmpty( _spriteFlash ) )
			Server.PrecacheModel( _spriteFlash );

		if ( !string.IsNullOrEmpty( Noise ) )
			Server.PrecacheSound( Noise );
	}

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "yawrate":
				_yawRate = Util.Atof( value );
				return true;
			case "yawrange":
				_yawRange = Util.Atof( value );
				return true;
			case "yawtolerance":
				_yawTolerance = Util.Atof( value );
				return true;
			case "pitchrange":
				_pitchRange = Util.Atof( value );
				return true;
			case "pitchrate":
				_pitchRate = Util.Atof( value );
				return true;
			case "pitchtolerance":
				_pitchTolerance = Util.Atof( value );
				return true;
			case "firerate":
				FireRate = Util.Atof( value );
				return true;
			case "barrel":
				_barrelPosition.x = Util.Atof( value );
				return true;
			case "barrely":
				_barrelPosition.y = Util.Atof( value );
				return true;
			case "barrelz":
				_barrelPosition.z = Util.Atof( value );
				return true;
			case "spritescale":
				_spriteScale = Util.Atof( value );
				return true;
			case "spritesmoke":
				_spriteSmoke = Server.AllocString( value );
				return true;
			case "spriteflash":
				_spriteFlash = Server.AllocString( value );
				return true;
			case "rotatesound":
				Noise = Server.AllocString( value );
				return true;
			case "persistence":
				_persist = Util.Atof( value );
				return true;
			case "bullet":
				BulletType = (TankBullet)Util.Atoi( value );
				return true;
			case "bullet_damage":
				BulletDamage = Util.Atoi( value );
				return true;
			case "firespread":
				Spread = Util.Atoi( value );
				return true;
			case "minRange":
				_minRange = Util.Atof( value );
				return true;
			case "maxRange":
				_maxRange = Util.Atof( value );
				return true;
			case "master":
				_master = Server.AllocString( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	private void TankActivate()
	{
		SpawnFlags |= SpawnFlagActive;
		NextThink = (float)(LTime + 0.1);
		FireLast = 0f;
	}

	private void TankDeactivate()
	{
		SpawnFlags &= ~SpawnFlagActive;
		FireLast = 0f;
		StopRotSound();
	}

	protected Vector3 BarrelPosition()
	{
		MathLib.AngleVectors( Angles, out var forward, out var right, out var up );

		return Origin + (forward * _barrelPosition.x) + (right * _barrelPosition.y) + (up * _barrelPosition.z);
	}

	public override bool OnControls( BaseEntity entity )
	{
		if ( (SpawnFlags & SpawnFlagCanControl) == 0 )
			return false;

		return MathLib.Length( _controllerUsePosition - entity.Origin ) < 30f;
	}

	private bool StartControl( Player controller )
	{
		if ( _controller is not null )
			return false;

		if ( !string.IsNullOrEmpty( _master ) && !IsMasterTriggered( _master, controller ) )
			return false;

		Util.Alert( AlertType.Console, "using TANK!" );

		_controller = controller;

		if ( _controller.ActiveItem is { } item )
		{
			item.Holster();
			_controller.WeaponModel = null;
			_controller.ViewModel = null;
		}

		_controller.HideHud |= Player.HideHudWeapons;
		_controllerUsePosition = _controller.Origin;

		NextThink = (float)(LTime + 0.1);

		return true;
	}

	private void StopControl()
	{
		if ( _controller is null )
			return;

		_controller.ActiveItem?.Deploy();

		Util.Alert( AlertType.Console, "stopped using TANK" );

		_controller.HideHud &= ~Player.HideHudWeapons;

		NextThink = 0f;
		_controller = null;

		if ( IsActive )
			NextThink = (float)(LTime + 1.0);
	}

	private void ControllerPostFrame()
	{
		if ( Server.Globals.Time < _nextAttack )
			return;

		if ( (_controller.Button & (int)InButtons.Attack) == 0 )
			return;

		MathLib.AngleVectors( Angles, out var forward, out _, out _ );

		FireLast = (float)(Server.Globals.Time - (1f / FireRate) - 0.01);

		Fire( BarrelPosition(), forward, _controller );

		if ( _controller is not null && _controller.IsPlayer )
			_controller.WeaponVolume = BasePlayerWeapon.LoudGunVolume;

		_nextAttack = Server.Globals.Time + (1f / FireRate);
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( (SpawnFlags & SpawnFlagCanControl) != 0 )
		{
			if ( activator.Classify() != ClassPlayer )
				return;

			if ( value == 2f && useType == UseType.Set )
			{
				ControllerPostFrame();
			}
			else if ( _controller is null && useType != UseType.Off )
			{
				((Player)activator).Tank = this;
				StartControl( (Player)activator );
			}
			else
			{
				StopControl();
			}

			return;
		}

		if ( !ShouldToggle( useType, IsActive ) )
			return;

		if ( IsActive )
			TankDeactivate();
		else
			TankActivate();
	}

	private bool InRange( float range )
	{
		if ( range < _minRange )
			return false;

		if ( _maxRange > 0f && range > _maxRange )
			return false;

		return true;
	}

	public override void Think()
	{
		AVelocity = Vector3.Zero;
		TrackTarget();

		if ( MathF.Abs( AVelocity.x ) > 1f || MathF.Abs( AVelocity.y ) > 1f )
			StartRotSound();
		else
			StopRotSound();
	}

	private void TrackTarget()
	{
		var player = Server.FindClientInPvs( this );
		var updateTime = false;
		var direction = Vector3.Zero;
		var barrelEnd = Vector3.Zero;
		Vector3 angles;

		if ( _controller is not null )
		{
			angles = _controller.ViewAngle;
			angles.x = 0f - angles.x;
			NextThink = (float)(LTime + 0.05);
		}
		else
		{
			if ( IsActive )
				NextThink = (float)(LTime + 0.1);
			else
				return;

			if ( Util.IsNullEnt( player ) )
			{
				if ( IsActive )
					NextThink = LTime + 2f;

				return;
			}

			barrelEnd = BarrelPosition();

			var targetPosition = player.Origin + player.ViewOffset;
			var range = MathLib.Length( targetPosition - barrelEnd );

			if ( !InRange( range ) )
				return;

			var trace = Util.TraceLine( barrelEnd, targetPosition, DontIgnoreMonsters, this );

			if ( trace.Fraction == 1.0 || trace.Ent == player )
			{
				if ( InRange( range ) && player is BaseEntity { IsAlive: true } instance )
				{
					updateTime = true;
					_sightOrigin = instance.BodyTarget( Origin );
				}
			}

			direction = _sightOrigin - Origin;
			angles = MathLib.VectorAngles( direction );

			AdjustAnglesForBarrel( ref angles, MathLib.Length( direction ) );
		}

		angles.x = -angles.x;

		angles.y = _yawCenter + Util.AngleDistance( angles.y, _yawCenter );
		angles.x = _pitchCenter + Util.AngleDistance( angles.x, _pitchCenter );

		if ( angles.y > _yawCenter + _yawRange )
		{
			angles.y = _yawCenter + _yawRange;
			updateTime = false;
		}
		else if ( angles.y < _yawCenter - _yawRange )
		{
			angles.y = _yawCenter - _yawRange;
			updateTime = false;
		}

		if ( updateTime )
			_lastSightTime = Server.Globals.Time;

		var distanceY = Util.AngleDistance( angles.y, Angles.y );
		var angularVelocity = AVelocity;

		angularVelocity.y = distanceY * 10f;

		if ( angularVelocity.y > _yawRate )
			angularVelocity.y = _yawRate;
		else if ( angularVelocity.y < -_yawRate )
			angularVelocity.y = -_yawRate;

		if ( angles.x > _pitchCenter + _pitchRange )
			angles.x = _pitchCenter + _pitchRange;
		else if ( angles.x < _pitchCenter - _pitchRange )
			angles.x = _pitchCenter - _pitchRange;

		var distanceX = Util.AngleDistance( angles.x, Angles.x );

		angularVelocity.x = distanceX * 10f;

		if ( angularVelocity.x > _pitchRate )
			angularVelocity.x = _pitchRate;
		else if ( angularVelocity.x < -_pitchRate )
			angularVelocity.x = -_pitchRate;

		AVelocity = angularVelocity;

		if ( _controller is not null )
			return;

		if ( !CanFire || !((MathF.Abs( distanceX ) < _pitchTolerance && MathF.Abs( distanceY ) < _yawTolerance) || (SpawnFlags & SpawnFlagLineOfSight) != 0) )
		{
			FireLast = 0f;
			return;
		}

		var fire = false;

		MathLib.AngleVectors( Angles, out var forward, out _, out _ );

		if ( (SpawnFlags & SpawnFlagLineOfSight) != 0 )
		{
			var length = MathLib.Length( direction );
			var trace = Util.TraceLine( barrelEnd, barrelEnd + (forward * length), DontIgnoreMonsters, this );

			if ( trace.Ent == player )
				fire = true;
		}
		else
		{
			fire = true;
		}

		if ( fire )
			Fire( BarrelPosition(), forward, this );
		else
			FireLast = 0f;
	}

	private void AdjustAnglesForBarrel( ref Vector3 angles, float distance )
	{
		if ( _barrelPosition.y == 0f && _barrelPosition.z == 0f )
			return;

		distance -= _barrelPosition.z;

		var d2 = distance * distance;

		if ( _barrelPosition.y != 0f )
		{
			var r2 = _barrelPosition.y * _barrelPosition.y;

			angles.y += (float)((180.0 / Math.PI) * Math.Atan2( _barrelPosition.y, Math.Sqrt( d2 - r2 ) ));
		}

		if ( _barrelPosition.z != 0f )
		{
			var r2 = _barrelPosition.z * _barrelPosition.z;

			angles.x += (float)((180.0 / Math.PI) * Math.Atan2( -_barrelPosition.z, Math.Sqrt( d2 - r2 ) ));
		}
	}

	protected virtual void Fire( Vector3 barrelEnd, Vector3 forward, BaseEntity attacker )
	{
		if ( FireLast != 0f )
		{
			if ( !string.IsNullOrEmpty( _spriteSmoke ) )
			{
				var sprite = Sprite.SpriteCreate( Server, _spriteSmoke, barrelEnd, true );

				sprite.AnimateAndDie( Server.RandomFloat( 15f, 20f ) );
				sprite.SetTransparency( RenderMode.TransAlpha, (int)RenderColor.x, (int)RenderColor.y, (int)RenderColor.z, 255, RenderFx.None );
				sprite.Velocity = sprite.Velocity with { z = Server.RandomFloat( 40f, 80f ) };
				sprite.SetScale( _spriteScale );
			}

			if ( !string.IsNullOrEmpty( _spriteFlash ) )
			{
				var sprite = Sprite.SpriteCreate( Server, _spriteFlash, barrelEnd, true );

				sprite.AnimateAndDie( 60f );
				sprite.SetTransparency( RenderMode.TransAdd, 255, 255, 255, 255, RenderFx.NoDissipation );
				sprite.SetScale( _spriteScale );

				sprite.NextThink = (float)(sprite.NextThink + 0.1);
			}

			UseTargets( this, UseType.Toggle, 0f );
		}

		FireLast = Server.Globals.Time;
	}

	protected Trace TankTrace( Vector3 start, Vector3 forward, Vector3 spread )
	{
		float x;
		float y;
		float z;

		do
		{
			x = Server.RandomFloat( -0.5f, 0.5f ) + Server.RandomFloat( -0.5f, 0.5f );
			y = Server.RandomFloat( -0.5f, 0.5f ) + Server.RandomFloat( -0.5f, 0.5f );
			z = (x * x) + (y * y);
		}
		while ( z > 1f );

		var direction = forward
			+ (x * spread.x * Server.Globals.Right)
			+ (y * spread.y * Server.Globals.Up);

		return Util.TraceLine( start, start + (direction * 4096f), DontIgnoreMonsters, this );
	}

	private void StartRotSound()
	{
		if ( string.IsNullOrEmpty( Noise ) || (SpawnFlags & SpawnFlagSoundOn) != 0 )
			return;

		SpawnFlags |= SpawnFlagSoundOn;
		Sound.Emit( this, SoundChannel.Static, Noise, 0.85f, Server.AttenuationNormal );
	}

	protected void StopRotSound()
	{
		if ( (SpawnFlags & SpawnFlagSoundOn) != 0 )
			Sound.Stop( this, SoundChannel.Static, Noise );

		SpawnFlags &= ~SpawnFlagSoundOn;
	}
}

[LinkEntityToClass( "func_tank" )]
public class FuncTankGun : FuncTank
{
	protected override void Fire( Vector3 barrelEnd, Vector3 forward, BaseEntity attacker )
	{
		if ( FireLast == 0f )
		{
			base.Fire( barrelEnd, forward, attacker );
			return;
		}

		Util.MakeAimVectors( Angles, Server.Globals );

		var bulletCount = (int)((Server.Globals.Time - FireLast) * FireRate);

		if ( bulletCount <= 0 )
			return;

		for ( var i = 0; i < bulletCount; i++ )
		{
			switch ( BulletType )
			{
				case TankBullet.Bullet9mm:
					FireBullets( 1, barrelEnd, forward, TankSpread[Spread], 4096f, Bullet.Monster9mm, 1, BulletDamage, attacker );
					break;
				case TankBullet.Mp5:
					FireBullets( 1, barrelEnd, forward, TankSpread[Spread], 4096f, Bullet.MonsterMp5, 1, BulletDamage, attacker );
					break;
				case TankBullet.Bullet12mm:
					FireBullets( 1, barrelEnd, forward, TankSpread[Spread], 4096f, Bullet.Monster12mm, 1, BulletDamage, attacker );
					break;
			}
		}

		base.Fire( barrelEnd, forward, attacker );
	}
}

[LinkEntityToClass( "func_tanklaser" )]
public class FuncTankLaser : FuncTank
{
	[Save( FieldType.ClassPtr )]
	private Laser _laser;
	[Save( FieldType.Time )]
	private float _laserTime;

	public override void Activate()
	{
		if ( GetLaser() is null )
		{
			Remove();
			Util.Alert( AlertType.Error, "Laser tank with no env_laser!" );
			return;
		}

		_laser.TurnOff();
	}

	public override bool KeyValue( string key, string value )
	{
		if ( key == "laserentity" )
		{
			Message = Server.AllocString( value );
			return true;
		}

		return base.KeyValue( key, value );
	}

	private Laser GetLaser()
	{
		if ( _laser is not null )
			return _laser;

		BaseEntity found = null;

		while ( (found = Util.FindEntityByTargetname( found, Message )) is not null )
		{
			if ( found.ClassName != "env_laser" )
				continue;

			_laser = (Laser)found;
			break;
		}

		return _laser;
	}

	public override void Think()
	{
		if ( _laser is not null && Server.Globals.Time > _laserTime )
			_laser.TurnOff();

		base.Think();
	}

	protected override void Fire( Vector3 barrelEnd, Vector3 forward, BaseEntity attacker )
	{
		if ( FireLast == 0f || GetLaser() is null )
		{
			base.Fire( barrelEnd, forward, this );
			return;
		}

		Util.MakeAimVectors( Angles, Server.Globals );

		var bulletCount = (int)((Server.Globals.Time - FireLast) * FireRate);

		if ( bulletCount == 0 )
			return;

		for ( var i = 0; i < bulletCount; i++ )
		{
			_laser.Origin = barrelEnd;

			var trace = TankTrace( barrelEnd, forward, TankSpread[Spread] );

			_laserTime = Server.Globals.Time;
			_laser.TurnOn();
			_laser.DamageTime = (float)(Server.Globals.Time - 1.0);
			_laser.FireAtPoint( trace );
			_laser.NextThink = 0f;
		}

		base.Fire( barrelEnd, forward, this );
	}
}

[LinkEntityToClass( "func_tankrocket" )]
public class FuncTankRocket : FuncTank
{
	public override void Precache()
	{
		Util.PrecacheOther( "rpg_rocket" );
		base.Precache();
	}

	protected override void Fire( Vector3 barrelEnd, Vector3 forward, BaseEntity attacker )
	{
		if ( FireLast == 0f )
		{
			base.Fire( barrelEnd, forward, this );
			return;
		}

		var bulletCount = (int)((Server.Globals.Time - FireLast) * FireRate);

		if ( bulletCount <= 0 )
			return;

		for ( var i = 0; i < bulletCount; i++ )
			Create( "rpg_rocket", barrelEnd, Angles, this );

		base.Fire( barrelEnd, forward, this );
	}
}

[LinkEntityToClass( "func_tankmortar" )]
public class FuncTankMortar : FuncTank
{
	public override bool KeyValue( string key, string value )
	{
		if ( key == "iMagnitude" )
		{
			Impulse = Util.Atoi( value );
			return true;
		}

		return base.KeyValue( key, value );
	}

	protected override void Fire( Vector3 barrelEnd, Vector3 forward, BaseEntity attacker )
	{
		if ( FireLast == 0f )
		{
			base.Fire( barrelEnd, forward, this );
			return;
		}

		var bulletCount = (int)((Server.Globals.Time - FireLast) * FireRate);

		if ( bulletCount <= 0 )
			return;

		Util.MakeAimVectors( Angles, Server.Globals );

		var trace = TankTrace( barrelEnd, forward, TankSpread[Spread] );

		ExplosionCreate( trace.EndPos, Angles, this, Impulse, true );

		base.Fire( barrelEnd, forward, this );
	}
}

[LinkEntityToClass( "func_tankcontrols" )]
public class FuncTankControls : BaseEntity
{
	[Save( FieldType.ClassPtr )]
	private FuncTank _tank;

	public override ObjectCaps ObjectCaps => (base.ObjectCaps & ~ObjectCaps.AcrossTransition) | ObjectCaps.ImpulseUse;

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		_tank?.Use( activator, caller, useType, value );
	}

	public override void Think()
	{
		BaseEntity target = null;

		do
		{
			target = Util.FindEntityByTargetname( target, Target );
		}
		while ( target is not null && !target.ClassName.StartsWith( "func_tank", StringComparison.Ordinal ) );

		if ( target is null )
		{
			Util.Alert( AlertType.Console, $"No tank {Target}" );
			return;
		}

		_tank = target as FuncTank;
	}

	public override void Spawn()
	{
		Solid = Solid.Trigger;
		MoveType = MoveType.None;
		Effects |= Effects.NoDraw;
		SetModel( ModelName );

		SetSize( Mins, Maxs );
		SetOrigin( Origin );

		NextThink = (float)(Server.Globals.Time + 0.3);
	}
}
