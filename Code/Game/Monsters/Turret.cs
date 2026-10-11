using System;
using GoldSrc;

namespace HalfLife;

public abstract class BaseTurret : BaseMonster
{
	public const int SpawnFlagAutoActivate = 32;
	public const int SpawnFlagStartInactive = 64;

	protected const int Range = 100 * 12;
	protected const float MachineVolume = 0.5f;

	protected const int AnimNone = 0;
	protected const int AnimFire = 1;
	protected const int AnimSpin = 2;
	protected const int AnimDeploy = 3;
	protected const int AnimRetire = 4;
	protected const int AnimDie = 5;

	private const int DefaultTurnRate = 30;
	private const float DefaultMaxWait = 15f;

	[Save( FieldType.Float )]
	protected float MaxSpin;
	[Save( FieldType.Integer )]
	protected int Spin;
	[Save( FieldType.ClassPtr )]
	protected Sprite EyeGlow;
	[Save( FieldType.Integer )]
	protected int EyeBrightness;
	[Save( FieldType.Integer )]
	protected int DeployHeight;
	[Save( FieldType.Integer )]
	protected int RetractHeight;
	[Save( FieldType.Integer )]
	protected int MinPitch;
	[Save( FieldType.Integer )]
	private int _baseTurnRate;
	[Save( FieldType.Float )]
	private float _turnRate;
	[Save( FieldType.Integer )]
	protected int Orientation;
	[Save( FieldType.Integer )]
	protected int On;
	[Save( FieldType.Integer )]
	private int _berserk;
	[Save( FieldType.Integer )]
	private int _autoStart;
	[Save( FieldType.PositionVector )]
	private Vector3 _lastSightPosition;
	[Save( FieldType.Time )]
	private float _lastSight;
	[Save( FieldType.Float )]
	protected float MaxWait;
	[Save( FieldType.Integer )]
	private int _searchSpeed;
	[Save( FieldType.Float )]
	private float _startYaw;
	[Save( FieldType.Vector )]
	private Vector3 _currentAngles;
	[Save( FieldType.Vector )]
	protected Vector3 GoalAngles;
	[Save( FieldType.Time )]
	private float _pingTime;
	[Save( FieldType.Time )]
	private float _spinUpTime;

	public override int BloodColor => DontBleed;

	public override void GibMonster()
	{
	}

	public virtual void SpinDownCall() => Spin = 0;

	public virtual void SpinUpCall() => Spin = 1;

	public virtual void Shoot( Vector3 source, Vector3 directionToEnemy )
	{
	}

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "maxsleep":
				MaxWait = Util.Atof( value );
				return true;
			case "orientation":
				Orientation = Util.Atoi( value );
				return true;
			case "searchspeed":
				_searchSpeed = Util.Atoi( value );
				return true;
			case "turnrate":
				_baseTurnRate = Util.Atoi( value );
				return true;
			case "style":
			case "height":
			case "value1":
			case "value2":
			case "value3":
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Spawn()
	{
		Precache();

		NextThink = Server.Globals.Time + 1f;
		MoveType = MoveType.Fly;
		Sequence = 0;
		Frame = 0f;
		Solid = Solid.SlideBox;
		DamageMode = DamageAim;

		EntityFlags |= EntityFlags.Monster;
		SetUse( TurretUse );

		if ( (SpawnFlags & SpawnFlagAutoActivate) != 0 && (SpawnFlags & SpawnFlagStartInactive) == 0 )
			_autoStart = 1;

		ResetSequenceInfo();
		SetBoneController( 0, 0f );
		SetBoneController( 1, 0f );
		FieldOfView = ViewFieldFull;
	}

	public override void Precache()
	{
		Server.PrecacheSound( "turret/tu_fire1.wav" );
		Server.PrecacheSound( "turret/tu_ping.wav" );
		Server.PrecacheSound( "turret/tu_active2.wav" );
		Server.PrecacheSound( "turret/tu_die.wav" );
		Server.PrecacheSound( "turret/tu_die2.wav" );
		Server.PrecacheSound( "turret/tu_die3.wav" );
		Server.PrecacheSound( "turret/tu_deploy.wav" );
		Server.PrecacheSound( "turret/tu_spinup.wav" );
		Server.PrecacheSound( "turret/tu_spindown.wav" );
		Server.PrecacheSound( "turret/tu_search.wav" );
		Server.PrecacheSound( "turret/tu_alert.wav" );
	}

	public void Initialize()
	{
		On = 0;
		_berserk = 0;
		Spin = 0;

		SetBoneController( 0, 0f );
		SetBoneController( 1, 0f );

		if ( _baseTurnRate == 0 )
			_baseTurnRate = DefaultTurnRate;

		if ( MaxWait == 0f )
			MaxWait = DefaultMaxWait;

		_startYaw = Angles.y;

		if ( Orientation == 1 )
		{
			IdealPitch = 180f;
			ViewOffset = ViewOffset with { z = -ViewOffset.z };
			Effects |= Effects.InvLight;

			var yaw = Angles.y + 180f;

			if ( yaw > 360f )
				yaw -= 360f;

			Angles = new Vector3( 180f, yaw, Angles.z );
		}

		GoalAngles.x = 0f;

		if ( _autoStart != 0 )
		{
			_lastSight = Server.Globals.Time + MaxWait;
			SetThink( AutoSearchThink );
			NextThink = (float)(Server.Globals.Time + .1);
		}
		else
		{
			SetThink( SubDoNothing );
		}
	}

	public void TurretUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( !ShouldToggle( useType, On != 0 ) )
			return;

		NextThink = (float)(Server.Globals.Time + 0.1);

		if ( On != 0 )
		{
			EnemyEntity = null;
			_autoStart = 0;
			SetThink( Retire );
		}
		else
		{
			if ( (SpawnFlags & SpawnFlagAutoActivate) != 0 )
				_autoStart = 1;

			SetThink( Deploy );
		}
	}

	public virtual void Ping()
	{
		if ( _pingTime == 0f )
		{
			_pingTime = Server.Globals.Time + 1f;
		}
		else if ( _pingTime <= Server.Globals.Time )
		{
			_pingTime = Server.Globals.Time + 1f;

			Sound.Emit( this, SoundChannel.Item, "turret/tu_ping.wav", 1f, Server.AttenuationNormal );
			EyeOn();
		}
		else if ( EyeBrightness > 0 )
		{
			EyeOff();
		}
	}

	public virtual void EyeOn()
	{
		if ( EyeGlow is null )
			return;

		if ( EyeBrightness != 255 )
			EyeBrightness = 255;

		EyeGlow.SetBrightness( EyeBrightness );
	}

	public virtual void EyeOff()
	{
		if ( EyeGlow is null || EyeBrightness <= 0 )
			return;

		EyeBrightness = Math.Max( 0, EyeBrightness - 30 );
		EyeGlow.SetBrightness( EyeBrightness );
	}

	private bool LoseEnemy()
	{
		if ( _lastSight == 0f )
		{
			_lastSight = (float)(Server.Globals.Time + 0.5);
			return false;
		}

		if ( Server.Globals.Time <= _lastSight )
			return false;

		StartSearch();

		return true;
	}

	private void StartSearch()
	{
		EnemyEntity = null;
		_lastSight = Server.Globals.Time + MaxWait;
		SetThink( SearchThink );
	}

	public void ActiveThink()
	{
		NextThink = (float)(Server.Globals.Time + 0.1);
		StudioFrameAdvance();

		if ( On == 0 || EnemyEntity is not { } enemy )
		{
			StartSearch();
			return;
		}

		if ( !enemy.IsAlive && LoseEnemy() )
			return;

		var mid = Origin + ViewOffset;
		var midEnemy = enemy.BodyTarget( mid );
		var enemyVisible = Ai.BoxVisible( this, enemy, ref midEnemy, 0f );

		var directionToEnemy = midEnemy - mid;
		var distanceToEnemy = MathLib.Length( directionToEnemy );
		var angles = MathLib.VectorAngles( midEnemy - mid );

		if ( !enemyVisible || distanceToEnemy > Range )
		{
			if ( LoseEnemy() )
				return;

			enemyVisible = false;
		}
		else
		{
			_lastSightPosition = midEnemy;
		}

		Util.MakeAimVectors( _currentAngles, Server.Globals );

		var attack = Vector3.Dot( Util.Normalize( directionToEnemy ), Server.Globals.Forward ) > 0.866;

		if ( Spin != 0 && (attack || _berserk != 0) )
		{
			var source = Server.GetAttachment( this, 0 );

			SetTurretAnim( AnimFire );
			Shoot( source, Server.Globals.Forward );
		}
		else
		{
			SetTurretAnim( AnimSpin );
		}

		if ( _berserk != 0 )
		{
			if ( Server.RandomLong( 0, 9 ) == 0 )
			{
				GoalAngles.y = Server.RandomFloat( 0f, 360f );
				GoalAngles.x = Server.RandomFloat( 0f, 90f ) - (90 * Orientation);

				TakeDamage( this, this, 1f, DamageGeneric );
				return;
			}
		}
		else if ( enemyVisible )
		{
			if ( angles.y > 360f )
				angles.y -= 360f;

			if ( angles.y < 0f )
				angles.y += 360f;

			if ( angles.x < -180f )
				angles.x += 360f;

			if ( angles.x > 180f )
				angles.x -= 360f;

			if ( Orientation == 0 )
			{
				if ( angles.x > 90f )
					angles.x = 90f;
				else if ( angles.x < MinPitch )
					angles.x = MinPitch;
			}
			else
			{
				if ( angles.x < -90f )
					angles.x = -90f;
				else if ( angles.x > -MinPitch )
					angles.x = -MinPitch;
			}

			GoalAngles.y = angles.y;
			GoalAngles.x = angles.x;
		}

		SpinUpCall();
		MoveTurret();
	}

	private void SetHeight( int height )
	{
		Maxs = Maxs with { z = height };
		Mins = Mins with { z = -height };

		SetSize( Mins, Maxs );
	}

	public void Deploy()
	{
		NextThink = (float)(Server.Globals.Time + 0.1);
		StudioFrameAdvance();

		if ( Sequence != AnimDeploy )
		{
			On = 1;
			SetTurretAnim( AnimDeploy );

			Sound.Emit( this, SoundChannel.Body, "turret/tu_deploy.wav", MachineVolume, Server.AttenuationNormal );
			UseTargets( this, UseType.On, 0f );
		}

		if ( SequenceFinished )
		{
			SetHeight( DeployHeight );

			_currentAngles.x = 0f;
			_currentAngles.y = Util.AngleMod( Orientation == 1 ? Angles.y + 180f : Angles.y );

			SetTurretAnim( AnimSpin );
			FrameRate = 0f;
			SetThink( SearchThink );
		}

		_lastSight = Server.Globals.Time + MaxWait;
	}

	public void Retire()
	{
		GoalAngles.x = 0f;
		GoalAngles.y = _startYaw;

		NextThink = (float)(Server.Globals.Time + 0.1);

		StudioFrameAdvance();

		EyeOff();

		if ( MoveTurret() )
		{
			SetTurretAnim( AnimSpin );
			return;
		}

		if ( Spin != 0 )
		{
			SpinDownCall();
		}
		else if ( Sequence != AnimRetire )
		{
			SetTurretAnim( AnimRetire );

			Sound.EmitDyn( this, SoundChannel.Body, "turret/tu_deploy.wav", MachineVolume, Server.AttenuationNormal, 0, 120 );
			UseTargets( this, UseType.Off, 0f );
		}
		else if ( SequenceFinished )
		{
			On = 0;
			_lastSight = 0f;

			SetTurretAnim( AnimNone );
			SetHeight( RetractHeight );

			if ( _autoStart != 0 )
			{
				SetThink( AutoSearchThink );
				NextThink = (float)(Server.Globals.Time + .1);
			}
			else
			{
				SetThink( SubDoNothing );
			}
		}
	}

	public void SetTurretAnim( int anim )
	{
		if ( Sequence == anim )
			return;

		if ( anim is not (AnimFire or AnimSpin) || Sequence is not (AnimFire or AnimSpin) )
			Frame = 0f;

		Sequence = anim;
		ResetSequenceInfo();

		switch ( anim )
		{
			case AnimRetire:
				Frame = 255f;
				FrameRate = -1f;
				break;

			case AnimDie:
				FrameRate = 1f;
				break;
		}
	}

	private void FindEnemy()
	{
		if ( EnemyEntity is { IsAlive: false } )
			EnemyEntity = null;

		if ( EnemyEntity is not null )
			return;

		Look( Range );
		EnemyEntity = BestVisibleEnemy();
	}

	public void SearchThink()
	{
		SetTurretAnim( AnimSpin );
		StudioFrameAdvance();
		NextThink = (float)(Server.Globals.Time + 0.1);

		if ( _spinUpTime == 0f && MaxSpin != 0f )
			_spinUpTime = Server.Globals.Time + MaxSpin;

		Ping();

		FindEnemy();

		if ( EnemyEntity is not null )
		{
			_lastSight = 0f;
			_spinUpTime = 0f;
			SetThink( ActiveThink );
			return;
		}

		if ( Server.Globals.Time > _lastSight )
		{
			_lastSight = 0f;
			_spinUpTime = 0f;
			SetThink( Retire );
		}
		else if ( _spinUpTime != 0f && Server.Globals.Time > _spinUpTime )
		{
			SpinDownCall();
		}

		GoalAngles.y = (float)(GoalAngles.y + (0.1 * _turnRate));

		if ( GoalAngles.y >= 360f )
			GoalAngles.y -= 360f;

		MoveTurret();
	}

	public void AutoSearchThink()
	{
		StudioFrameAdvance();
		NextThink = (float)(Server.Globals.Time + 0.3);

		FindEnemy();

		if ( EnemyEntity is null )
			return;

		SetThink( Deploy );

		Sound.Emit( this, SoundChannel.Body, "turret/tu_alert.wav", MachineVolume, Server.AttenuationNormal );
	}

	protected void DeathSounds()
	{
		var random = Server.RandomFloat( 0f, 1f );

		if ( random <= 0.33 )
			Sound.Emit( this, SoundChannel.Body, "turret/tu_die.wav", 1f, Server.AttenuationNormal );
		else if ( random <= 0.66 )
			Sound.Emit( this, SoundChannel.Body, "turret/tu_die2.wav", 1f, Server.AttenuationNormal );
		else
			Sound.Emit( this, SoundChannel.Body, "turret/tu_die3.wav", 1f, Server.AttenuationNormal );

		Sound.EmitDyn( this, SoundChannel.Static, "turret/tu_active2.wav", 0f, 0f, Server.SoundStop, 100 );
	}

	public void TurretDeath()
	{
		StudioFrameAdvance();
		NextThink = (float)(Server.Globals.Time + 0.1);

		if ( DeadFlag != DeadDead )
		{
			DeadFlag = DeadDead;

			DeathSounds();

			GoalAngles.x = Orientation == 0 ? -15f : -90f;

			SetTurretAnim( AnimDie );

			EyeOn();
		}

		EyeOff();

		if ( DamageTime + Server.RandomFloat( 0f, 2f ) > Server.Globals.Time )
		{
			Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
			Server.WriteByte( TempEntity.Smoke );
			Server.WriteCoord( Server.RandomFloat( AbsMin.x, AbsMax.x ) );
			Server.WriteCoord( Server.RandomFloat( AbsMin.y, AbsMax.y ) );
			Server.WriteCoord( Origin.z - (Orientation * 64) );
			Server.WriteShort( GameModels.Smoke );
			Server.WriteByte( 25 );
			Server.WriteByte( 10 - (Orientation * 5) );
			Server.MessageEnd();
		}

		if ( DamageTime + Server.RandomFloat( 0f, 5f ) > Server.Globals.Time )
		{
			var source = new Vector3( Server.RandomFloat( AbsMin.x, AbsMax.x ), Server.RandomFloat( AbsMin.y, AbsMax.y ), 0f );

			source.z += Orientation == 0 ? Server.RandomFloat( Origin.z, AbsMax.z ) : Server.RandomFloat( AbsMin.z, Origin.z );

			Util.Sparks( source );
		}

		if ( SequenceFinished && !MoveTurret() && DamageTime + 5f < Server.Globals.Time )
		{
			FrameRate = 0f;
			SetThink( null );
		}
	}

	public override void TraceAttack( BaseEntity attacker, float damage, Vector3 direction, in Trace trace, int damageType )
	{
		if ( trace.HitGroup == 10 )
		{
			if ( DamageTime != Server.Globals.Time || Server.RandomLong( 0, 10 ) < 1 )
			{
				Util.Ricochet( trace.EndPos, Server.RandomFloat( 1f, 2f ) );
				DamageTime = Server.Globals.Time;
			}

			damage = 0.1f;
		}

		if ( DamageMode == DamageNo )
			return;

		Combat.AddMultiDamage( attacker, this, damage, damageType );
	}

	protected bool Die( Action deathThink )
	{
		if ( Health > 0f )
			return false;

		Health = 0f;
		DamageMode = DamageNo;
		DamageTime = Server.Globals.Time;

		EntityFlags &= ~EntityFlags.Monster;

		SetUse( null );
		SetThink( deathThink );
		UseTargets( this, UseType.On, 0f );
		NextThink = (float)(Server.Globals.Time + 0.1);

		return true;
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		if ( DamageMode == DamageNo )
			return false;

		if ( On == 0 )
			damage = (float)(damage / 10.0);

		Health -= damage;

		if ( Die( TurretDeath ) )
			return false;

		if ( Health <= 10f && On != 0 )
		{
			_berserk = 1;
			SetThink( SearchThink );
		}

		return true;
	}

	public bool MoveTurret()
	{
		var moved = false;

		if ( _currentAngles.x != GoalAngles.x )
		{
			var direction = GoalAngles.x > _currentAngles.x ? 1f : -1f;

			_currentAngles.x = (float)(_currentAngles.x + (0.1 * _turnRate * direction));

			if ( direction == 1f )
			{
				if ( _currentAngles.x > GoalAngles.x )
					_currentAngles.x = GoalAngles.x;
			}
			else if ( _currentAngles.x < GoalAngles.x )
			{
				_currentAngles.x = GoalAngles.x;
			}

			SetBoneController( 1, Orientation == 0 ? -_currentAngles.x : _currentAngles.x );

			moved = true;
		}

		if ( _currentAngles.y != GoalAngles.y )
		{
			var direction = GoalAngles.y > _currentAngles.y ? 1f : -1f;
			var distance = Math.Abs( GoalAngles.y - _currentAngles.y );

			if ( distance > 180f )
			{
				distance = 360f - distance;
				direction = -direction;
			}

			if ( distance > 30f )
			{
				if ( _turnRate < _baseTurnRate * 10 )
					_turnRate += _baseTurnRate;
			}
			else if ( _turnRate > 45f )
			{
				_turnRate -= _baseTurnRate;
			}
			else
			{
				_turnRate += _baseTurnRate;
			}

			_currentAngles.y = (float)(_currentAngles.y + (0.1 * _turnRate * direction));

			if ( _currentAngles.y < 0f )
				_currentAngles.y += 360f;
			else if ( _currentAngles.y >= 360f )
				_currentAngles.y -= 360f;

			if ( distance < 0.05 * _baseTurnRate )
				_currentAngles.y = GoalAngles.y;

			SetBoneController( 0, Orientation == 0 ? _currentAngles.y - Angles.y : Angles.y - 180f - _currentAngles.y );

			moved = true;
		}

		if ( !moved )
			_turnRate = _baseTurnRate;

		return moved;
	}

	public override int Classify() => On != 0 || _autoStart != 0 ? ClassMachine : ClassNone;

	protected void ShootSound()
	{
		switch ( Server.RandomLong( 0, 2 ) )
		{
			case 0:
				Sound.Emit( this, SoundChannel.Weapon, "weapons/hks1.wav", 1f, Server.AttenuationNormal );
				break;
			case 1:
				Sound.Emit( this, SoundChannel.Weapon, "weapons/hks2.wav", 1f, Server.AttenuationNormal );
				break;
			case 2:
				Sound.Emit( this, SoundChannel.Weapon, "weapons/hks3.wav", 1f, Server.AttenuationNormal );
				break;
		}
	}
}

[LinkEntityToClass( "monster_turret" )]
public class Turret : BaseTurret
{
	private const string GlowSprite = "sprites/flare3.spr";

	[Save( FieldType.Integer )]
	private int _startSpin;

	public override void Spawn()
	{
		Precache();

		SetModel( "models/turret.mdl" );

		Health = HalfLifeDll.SkillData.TurretHealth;
		HackedGunPos = new Vector3( 0f, 0f, 12.75f );
		MaxSpin = 5f;
		ViewOffset = ViewOffset with { z = 12.75f };

		base.Spawn();

		RetractHeight = 16;
		DeployHeight = 32;
		MinPitch = -15;

		SetSize( new Vector3( -32f, -32f, -RetractHeight ), new Vector3( 32f, 32f, RetractHeight ) );

		SetThink( Initialize );

		EyeGlow = Sprite.SpriteCreate( Server, GlowSprite, Origin, false );
		EyeGlow.SetTransparency( RenderMode.Glow, 255, 0, 0, 0, RenderFx.NoDissipation );
		EyeGlow.SetAttachment( this, 2 );
		EyeBrightness = 0;

		NextThink = (float)(Server.Globals.Time + 0.3);
	}

	public override void Precache()
	{
		base.Precache();

		Server.PrecacheModel( "models/turret.mdl" );
		Server.PrecacheModel( GlowSprite );
	}

	public override void Shoot( Vector3 source, Vector3 directionToEnemy )
	{
		FireBullets( 1, source, directionToEnemy, Vector3.Zero, Range, Bullet.Monster12mm, 1 );

		Sound.Emit( this, SoundChannel.Weapon, "turret/tu_fire1.wav", 1f, 0.6f );

		Effects |= Effects.MuzzleFlash;
	}

	public override void SpinUpCall()
	{
		StudioFrameAdvance();
		NextThink = (float)(Server.Globals.Time + 0.1);

		if ( Spin == 0 )
		{
			SetTurretAnim( AnimSpin );

			if ( _startSpin == 0 )
			{
				NextThink = (float)(Server.Globals.Time + 1.0);

				Sound.Emit( this, SoundChannel.Body, "turret/tu_spinup.wav", MachineVolume, Server.AttenuationNormal );

				_startSpin = 1;
				FrameRate = 0.1f;
			}
			else if ( FrameRate >= 1.0 )
			{
				NextThink = (float)(Server.Globals.Time + 0.1);

				Sound.Emit( this, SoundChannel.Static, "turret/tu_active2.wav", MachineVolume, Server.AttenuationNormal );

				SetThink( ActiveThink );
				_startSpin = 0;
				Spin = 1;
			}
			else
			{
				FrameRate = (float)(FrameRate + 0.075);
			}
		}

		if ( Spin != 0 )
			SetThink( ActiveThink );
	}

	public override void SpinDownCall()
	{
		if ( Spin == 0 )
			return;

		SetTurretAnim( AnimSpin );

		if ( FrameRate == 1.0 )
		{
			Sound.EmitDyn( this, SoundChannel.Static, "turret/tu_active2.wav", 0f, 0f, Server.SoundStop, 100 );
			Sound.Emit( this, SoundChannel.Item, "turret/tu_spindown.wav", MachineVolume, Server.AttenuationNormal );
		}

		FrameRate = (float)(FrameRate - 0.02);

		if ( FrameRate <= 0f )
		{
			FrameRate = 0f;
			Spin = 0;
		}
	}
}

[LinkEntityToClass( "monster_miniturret" )]
public class MiniTurret : BaseTurret
{
	public override void Spawn()
	{
		Precache();

		SetModel( "models/miniturret.mdl" );

		Health = HalfLifeDll.SkillData.MiniturretHealth;
		HackedGunPos = new Vector3( 0f, 0f, 12.75f );
		MaxSpin = 0f;
		ViewOffset = ViewOffset with { z = 12.75f };

		base.Spawn();

		RetractHeight = 16;
		DeployHeight = 32;
		MinPitch = -15;

		SetSize( new Vector3( -16f, -16f, -RetractHeight ), new Vector3( 16f, 16f, RetractHeight ) );

		SetThink( Initialize );
		NextThink = (float)(Server.Globals.Time + 0.3);
	}

	public override void Precache()
	{
		base.Precache();

		Server.PrecacheModel( "models/miniturret.mdl" );
		Server.PrecacheSound( "weapons/hks1.wav" );
		Server.PrecacheSound( "weapons/hks2.wav" );
		Server.PrecacheSound( "weapons/hks3.wav" );
	}

	public override void Shoot( Vector3 source, Vector3 directionToEnemy )
	{
		FireBullets( 1, source, directionToEnemy, Vector3.Zero, Range, Bullet.Monster9mm, 1 );

		ShootSound();

		Effects |= Effects.MuzzleFlash;
	}
}

[LinkEntityToClass( "monster_sentry" )]
public class Sentry : BaseTurret
{
	public override void Precache()
	{
		base.Precache();

		Server.PrecacheModel( "models/sentry.mdl" );
	}

	public override void Spawn()
	{
		Precache();

		SetModel( "models/sentry.mdl" );

		Health = HalfLifeDll.SkillData.SentryHealth;
		HackedGunPos = new Vector3( 0f, 0f, 48f );
		ViewOffset = ViewOffset with { z = 48f };
		MaxWait = 1E6f;
		MaxSpin = 1E6f;

		base.Spawn();

		RetractHeight = 64;
		DeployHeight = 64;
		MinPitch = -60;

		SetSize( new Vector3( -16f, -16f, -RetractHeight ), new Vector3( 16f, 16f, RetractHeight ) );

		SetTouch( SentryTouch );
		SetThink( Initialize );
		NextThink = (float)(Server.Globals.Time + 0.3);
	}

	public override void Shoot( Vector3 source, Vector3 directionToEnemy )
	{
		FireBullets( 1, source, directionToEnemy, Vector3.Zero, Range, Bullet.MonsterMp5, 1 );

		ShootSound();

		Effects |= Effects.MuzzleFlash;
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		if ( DamageMode == DamageNo )
			return false;

		if ( On == 0 )
		{
			SetThink( Deploy );
			SetUse( null );
			NextThink = (float)(Server.Globals.Time + 0.1);
		}

		Health -= damage;

		return !Die( SentryDeath );
	}

	public void SentryTouch( BaseEntity other )
	{
		if ( other is not null && (other.IsPlayer || (other.EntityFlags & EntityFlags.Monster) == EntityFlags.Monster) )
			TakeDamage( other, other, 0f, 0 );
	}

	public void SentryDeath()
	{
		StudioFrameAdvance();
		NextThink = (float)(Server.Globals.Time + 0.1);

		if ( DeadFlag != DeadDead )
		{
			DeadFlag = DeadDead;

			DeathSounds();

			SetBoneController( 0, 0f );
			SetBoneController( 1, 0f );

			SetTurretAnim( AnimDie );

			Solid = Solid.Not;
			Angles = Angles with { y = Util.AngleMod( Angles.y + (Server.RandomLong( 0, 2 ) * 120) ) };

			EyeOn();
		}

		EyeOff();

		var source = Server.GetAttachment( this, 1 );

		if ( DamageTime + Server.RandomFloat( 0f, 2f ) > Server.Globals.Time )
		{
			Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
			Server.WriteByte( TempEntity.Smoke );
			Server.WriteCoord( source.x + Server.RandomFloat( -16f, 16f ) );
			Server.WriteCoord( source.y + Server.RandomFloat( -16f, 16f ) );
			Server.WriteCoord( source.z - 32f );
			Server.WriteShort( GameModels.Smoke );
			Server.WriteByte( 15 );
			Server.WriteByte( 8 );
			Server.MessageEnd();
		}

		if ( DamageTime + Server.RandomFloat( 0f, 8f ) > Server.Globals.Time )
			Util.Sparks( source );

		if ( SequenceFinished && DamageTime + 5f < Server.Globals.Time )
		{
			FrameRate = 0f;
			SetThink( null );
		}
	}
}
