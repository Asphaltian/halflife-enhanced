using System;
using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "monster_apache" )]
public class Apache : BaseMonster
{
	private const int SpawnFlagWaitForTrigger = 0x04 | 0x40;
	private const int SpawnFlagNoWreckage = 0x08;

	private static float _rocketSide = 1f;

	[Save( FieldType.Integer )]
	private int _rockets;
	[Save( FieldType.Float )]
	private float _force;
	[Save( FieldType.Time )]
	private float _nextRocket;

	[Save( FieldType.Vector )]
	private Vector3 _targetDirection;
	[Save( FieldType.PositionVector )]
	private Vector3 _targetPosition;

	[Save( FieldType.Vector )]
	private Vector3 _desiredDirection;
	[Save( FieldType.PositionVector )]
	private Vector3 _desiredPosition;

	[Save( FieldType.Vector )]
	private Vector3 _goalDirection;

	[Save( FieldType.Vector )]
	private Vector3 _gunAngles;
	[Save( FieldType.Time )]
	private float _lastSeen;
	[Save( FieldType.Time )]
	private float _previousSeen;

	private int _soundState;

	private int _spriteTexture;
	private int _explode;
	private int _bodyGibs;

	[Save( FieldType.Float )]
	private float _goalSpeed;

	[Save( FieldType.Integer )]
	private int _doSmokePuff;

	public override int BloodColor => DontBleed;

	public override int Classify() => ClassHumanMilitary;

	public override void SetObjectCollisionBox()
	{
		AbsMin = Origin + new Vector3( -300f, -300f, -172f );
		AbsMax = Origin + new Vector3( 300f, 300f, 8f );
	}

	public override void Spawn()
	{
		Precache();

		MoveType = MoveType.Fly;
		Solid = Solid.BBox;

		SetModel( "models/apache.mdl" );
		SetSize( new Vector3( -32f, -32f, -64f ), new Vector3( 32f, 32f, 0f ) );
		SetOrigin( Origin );

		EntityFlags |= EntityFlags.Monster;
		DamageMode = DamageAim;
		Health = HalfLifeDll.SkillData.ApacheHealth;

		FieldOfView = -0.707f;

		Sequence = 0;
		ResetSequenceInfo();
		Frame = Server.RandomLong( 0, 0xFF );

		InitBoneControllers();

		if ( (SpawnFlags & SpawnFlagWaitForTrigger) != 0 )
		{
			SetUse( StartupUse );
		}
		else
		{
			SetThink( HuntThink );
			SetTouch( FlyTouch );
			NextThink = (float)(Server.Globals.Time + 1.0);
		}

		_rockets = 10;
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/apache.mdl" );

		Server.PrecacheSound( "apache/ap_rotor1.wav" );
		Server.PrecacheSound( "apache/ap_rotor2.wav" );
		Server.PrecacheSound( "apache/ap_rotor3.wav" );
		Server.PrecacheSound( "apache/ap_whine1.wav" );

		Server.PrecacheSound( "weapons/mortarhit.wav" );

		_spriteTexture = Server.PrecacheModel( "sprites/white.spr" );

		Server.PrecacheSound( "turret/tu_fire1.wav" );

		Server.PrecacheModel( "sprites/lgtning.spr" );

		_explode = Server.PrecacheModel( "sprites/fexplo.spr" );
		_bodyGibs = Server.PrecacheModel( "models/metalplategibs_green.mdl" );

		Util.PrecacheOther( "hvr_rocket" );
	}

	public void StartupUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		SetThink( HuntThink );
		SetTouch( FlyTouch );
		NextThink = (float)(Server.Globals.Time + 0.1);
		SetUse( null );
	}

	public override void Killed( BaseEntity attacker, int gib )
	{
		MoveType = MoveType.Toss;
		Gravity = 0.3f;

		Sound.Stop( this, SoundChannel.Static, "apache/ap_rotor2.wav" );

		SetSize( new Vector3( -32f, -32f, -64f ), new Vector3( 32f, 32f, 0f ) );
		SetThink( DyingThink );
		SetTouch( CrashTouch );
		NextThink = (float)(Server.Globals.Time + 0.1);
		Health = 0f;
		DamageMode = DamageNo;

		if ( (SpawnFlags & SpawnFlagNoWreckage) != 0 )
			_nextRocket = Server.Globals.Time + 4f;
		else
			_nextRocket = Server.Globals.Time + 15f;
	}

	public void DyingThink()
	{
		StudioFrameAdvance();
		NextThink = (float)(Server.Globals.Time + 0.1);

		AVelocity *= 1.02f;

		if ( _nextRocket > Server.Globals.Time )
		{
			Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, Origin );
			Server.WriteByte( TempEntity.Explosion );
			Server.WriteCoord( Origin.x + Server.RandomFloat( -150f, 150f ) );
			Server.WriteCoord( Origin.y + Server.RandomFloat( -150f, 150f ) );
			Server.WriteCoord( Origin.z + Server.RandomFloat( -150f, -50f ) );
			Server.WriteShort( GameModels.Fireball );
			Server.WriteByte( Server.RandomLong( 0, 29 ) + 30 );
			Server.WriteByte( 12 );
			Server.WriteByte( TempEntity.ExplosionNone );
			Server.MessageEnd();

			Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, Origin );
			Server.WriteByte( TempEntity.Smoke );
			Server.WriteCoord( Origin.x + Server.RandomFloat( -150f, 150f ) );
			Server.WriteCoord( Origin.y + Server.RandomFloat( -150f, 150f ) );
			Server.WriteCoord( Origin.z + Server.RandomFloat( -150f, -50f ) );
			Server.WriteShort( GameModels.Smoke );
			Server.WriteByte( 100 );
			Server.WriteByte( 10 );
			Server.MessageEnd();

			var fallingSpot = Origin + ((Mins + Maxs) * 0.5f);

			Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, fallingSpot );
			Server.WriteByte( TempEntity.BreakModel );
			Server.WriteCoord( fallingSpot.x );
			Server.WriteCoord( fallingSpot.y );
			Server.WriteCoord( fallingSpot.z );
			Server.WriteCoord( 400f );
			Server.WriteCoord( 400f );
			Server.WriteCoord( 132f );
			Server.WriteCoord( Velocity.x );
			Server.WriteCoord( Velocity.y );
			Server.WriteCoord( Velocity.z );
			Server.WriteByte( 50 );
			Server.WriteShort( _bodyGibs );
			Server.WriteByte( 4 );
			Server.WriteByte( 30 );
			Server.WriteByte( Breakable.BreakMetal );
			Server.MessageEnd();

			EntityFlags &= ~EntityFlags.OnGround;
			NextThink = (float)(Server.Globals.Time + 0.2);
			return;
		}

		var spot = Origin + ((Mins + Maxs) * 0.5f);

		Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, spot );
		Server.WriteByte( TempEntity.Sprite );
		Server.WriteCoord( spot.x );
		Server.WriteCoord( spot.y );
		Server.WriteCoord( spot.z + 256f );
		Server.WriteShort( _explode );
		Server.WriteByte( 120 );
		Server.WriteByte( 255 );
		Server.MessageEnd();

		Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, spot );
		Server.WriteByte( TempEntity.Smoke );
		Server.WriteCoord( spot.x );
		Server.WriteCoord( spot.y );
		Server.WriteCoord( spot.z + 512f );
		Server.WriteShort( GameModels.Smoke );
		Server.WriteByte( 250 );
		Server.WriteByte( 5 );
		Server.MessageEnd();

		Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, Origin );
		Server.WriteByte( TempEntity.BeamCylinder );
		Server.WriteCoord( Origin.x );
		Server.WriteCoord( Origin.y );
		Server.WriteCoord( Origin.z );
		Server.WriteCoord( Origin.x );
		Server.WriteCoord( Origin.y );
		Server.WriteCoord( Origin.z + 2000f );
		Server.WriteShort( _spriteTexture );
		Server.WriteByte( 0 );
		Server.WriteByte( 0 );
		Server.WriteByte( 4 );
		Server.WriteByte( 32 );
		Server.WriteByte( 0 );
		Server.WriteByte( 255 );
		Server.WriteByte( 255 );
		Server.WriteByte( 192 );
		Server.WriteByte( 128 );
		Server.WriteByte( 0 );
		Server.MessageEnd();

		Sound.Emit( this, SoundChannel.Static, "weapons/mortarhit.wav", 1f, 0.3f );

		RadiusDamage( Origin, this, this, 300f, ClassNone, DamageBlast );

		if ( (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround )
		{
			var wreckage = Create( "cycler_wreckage", Origin, Angles, null );

			Server.SetSize( wreckage, new Vector3( -200f, -200f, -128f ), new Vector3( 200f, 200f, -32f ) );
			wreckage.Frame = Frame;
			wreckage.Sequence = Sequence;
			wreckage.FrameRate = 0f;
			wreckage.DamageTime = Server.Globals.Time + 5f;
		}

		spot = Origin + ((Mins + Maxs) * 0.5f);

		Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, spot );
		Server.WriteByte( TempEntity.BreakModel );
		Server.WriteCoord( spot.x );
		Server.WriteCoord( spot.y );
		Server.WriteCoord( spot.z + 64f );
		Server.WriteCoord( 400f );
		Server.WriteCoord( 400f );
		Server.WriteCoord( 128f );
		Server.WriteCoord( 0f );
		Server.WriteCoord( 0f );
		Server.WriteCoord( 200f );
		Server.WriteByte( 30 );
		Server.WriteShort( _bodyGibs );
		Server.WriteByte( 200 );
		Server.WriteByte( 200 );
		Server.WriteByte( Breakable.BreakMetal );
		Server.MessageEnd();

		SetThink( SubRemove );
		NextThink = (float)(Server.Globals.Time + 0.1);
	}

	public void FlyTouch( BaseEntity other )
	{
		if ( other.Solid != Solid.Bsp )
			return;

		var trace = Server.Globals.Trace;

		Velocity += trace.PlaneNormal * (MathLib.Length( Velocity ) + 200f);
	}

	public void CrashTouch( BaseEntity other )
	{
		if ( other.Solid != Solid.Bsp )
			return;

		SetTouch( null );
		_nextRocket = Server.Globals.Time;
		NextThink = Server.Globals.Time;
	}

	public override void GibMonster()
	{
	}

	public void HuntThink()
	{
		var globals = Server.Globals;

		StudioFrameAdvance();
		NextThink = (float)(globals.Time + 0.1);

		ShowDamage();

		if ( GoalEntity is null && !string.IsNullOrEmpty( Target ) )
		{
			GoalEntity = Util.FindEntityByTargetname( null, Target );

			if ( GoalEntity is not null )
			{
				_desiredPosition = GoalEntity.Origin;
				Util.MakeAimVectors( GoalEntity.Angles, globals );
				_goalDirection = globals.Forward;
			}
		}

		Look( 4092 );
		EnemyEntity = BestVisibleEnemy();

		if ( _goalSpeed < 800f )
			_goalSpeed += 5f;

		if ( EnemyEntity is { } enemy )
		{
			if ( IsVisible( enemy ) )
			{
				if ( _lastSeen < globals.Time - 5f )
					_previousSeen = globals.Time;

				_lastSeen = globals.Time;
				_targetPosition = enemy.Center;
			}
			else
			{
				EnemyEntity = null;
			}
		}

		_targetDirection = Util.Normalize( _targetPosition - Origin );

		var length = MathLib.Length( Origin - _desiredPosition );

		if ( GoalEntity is not null )
		{
			if ( length < 128f )
			{
				GoalEntity = Util.FindEntityByTargetname( null, GoalEntity.Target );

				if ( GoalEntity is not null )
				{
					_desiredPosition = GoalEntity.Origin;
					Util.MakeAimVectors( GoalEntity.Angles, globals );
					_goalDirection = globals.Forward;
					length = MathLib.Length( Origin - _desiredPosition );
				}
			}
		}
		else
		{
			_desiredPosition = Origin;
		}

		if ( length > 250f )
		{
			if ( _lastSeen + 90f > globals.Time && Vector3.Dot( Util.Normalize( _targetPosition - Origin ), Util.Normalize( _desiredPosition - Origin ) ) > 0.25 )
				_desiredDirection = Util.Normalize( _targetPosition - Origin );
			else
				_desiredDirection = Util.Normalize( _desiredPosition - Origin );
		}
		else
		{
			_desiredDirection = _goalDirection;
		}

		Flight();

		if ( _lastSeen + 1f > globals.Time && _previousSeen + 2f < globals.Time )
		{
			if ( FireGun() && _goalSpeed > 400f )
				_goalSpeed = 400f;

			if ( HalfLifeDll.SkillLevel == SkillData.Easy )
				_nextRocket = globals.Time + 10f;
		}

		Util.MakeAimVectors( Angles, globals );

		var estimate = Util.Normalize( (globals.Forward * 800f) + Velocity );

		if ( _rockets % 2 == 1 )
		{
			FireRocket();
			_nextRocket = (float)(globals.Time + 0.5);

			if ( _rockets <= 0 )
			{
				_nextRocket = globals.Time + 10f;
				_rockets = 10;
			}
		}
		else if ( Angles.x < 0f && Vector3.Dot( Velocity, globals.Forward ) > -100f && _nextRocket < globals.Time )
		{
			if ( _lastSeen + 60f <= globals.Time )
				return;

			if ( EnemyEntity is not null )
			{
				if ( Vector3.Dot( _targetDirection, estimate ) > .965 )
				{
					var trace = Util.TraceLine( Origin, Origin + (estimate * 4096f), IgnoreMonsters, this );

					if ( MathLib.Length( trace.EndPos - _targetPosition ) < 512f )
						FireRocket();
				}
			}
			else
			{
				var trace = Util.TraceLine( Origin, Origin + (estimate * 4096f), DontIgnoreMonsters, this );

				if ( MathLib.Length( trace.EndPos - _targetPosition ) < 512f )
					FireRocket();
			}
		}
	}

	private void Flight()
	{
		var globals = Server.Globals;
		var adjust = new Vector3( 5f, 0f, 0f );
		var angularVelocity = AVelocity;
		var velocity = Velocity;

		Util.MakeAimVectors( Angles + (angularVelocity * 2f) + adjust, globals );

		var side = Vector3.Dot( _desiredDirection, globals.Right );

		if ( side < 0f )
		{
			if ( angularVelocity.y < 60f )
				angularVelocity.y += 8f;
		}
		else
		{
			if ( angularVelocity.y > -60f )
				angularVelocity.y -= 8f;
		}

		angularVelocity.y = (float)(angularVelocity.y * 0.98);

		Util.MakeAimVectors( Angles + (angularVelocity * 1f) + adjust, globals );

		var estimate = Origin + (velocity * 2f) + (globals.Up * _force * 20f) - new Vector3( 0f, 0f, 384 * 2 );

		Util.MakeAimVectors( Angles + adjust, globals );
		velocity.x += globals.Up.x * _force;
		velocity.y += globals.Up.y * _force;
		velocity.z += globals.Up.z * _force;
		velocity.z = (float)(velocity.z - 38.4);

		var speed = MathLib.Length( velocity );
		var direction = Vector3.Dot( new Vector3( globals.Forward.x, globals.Forward.y, 0f ), new Vector3( velocity.x, velocity.y, 0f ) );

		if ( direction < 0f )
			speed = -speed;

		var distance = Vector3.Dot( _desiredPosition - estimate, globals.Forward );
		var slip = -Vector3.Dot( _desiredPosition - estimate, globals.Right );

		if ( slip > 0f )
		{
			if ( Angles.z > -30f && angularVelocity.z > -15f )
				angularVelocity.z -= 4f;
			else
				angularVelocity.z += 2f;
		}
		else
		{
			if ( Angles.z < 30f && angularVelocity.z < 15f )
				angularVelocity.z += 4f;
			else
				angularVelocity.z -= 2f;
		}

		velocity.x = (float)(velocity.x * (1.0 - (Math.Abs( globals.Right.x ) * 0.05)));
		velocity.y = (float)(velocity.y * (1.0 - (Math.Abs( globals.Right.y ) * 0.05)));
		velocity.z = (float)(velocity.z * (1.0 - (Math.Abs( globals.Right.z ) * 0.05)));

		velocity *= 0.995f;

		if ( _force < 80f && estimate.z < _desiredPosition.z )
		{
			_force += 12f;
		}
		else if ( _force > 30f )
		{
			if ( estimate.z > _desiredPosition.z )
				_force -= 8f;
		}

		if ( distance > 0f && speed < _goalSpeed && Angles.x + angularVelocity.x > -40f )
			angularVelocity.x -= 12f;
		else if ( distance < 0f && speed > -50f && Angles.x + angularVelocity.x < 20f )
			angularVelocity.x += 12f;
		else if ( Angles.x + angularVelocity.x > 0f )
			angularVelocity.x -= 4f;
		else if ( Angles.x + angularVelocity.x < 0f )
			angularVelocity.x += 4f;

		AVelocity = angularVelocity;
		Velocity = velocity;

		if ( _soundState == 0 )
		{
			Sound.EmitDyn( this, SoundChannel.Static, "apache/ap_rotor2.wav", 1f, 0.3f, 0, 110 );

			_soundState = Server.SoundChangePitch;
			return;
		}

		if ( Util.FindEntityByClassname( null, "player" ) is not { } player )
			return;

		var pitch = Vector3.Dot( Velocity - player.Velocity, Util.Normalize( player.Origin - Origin ) );

		pitch = (int)(100 + (pitch / 50.0));

		if ( pitch > 250f )
			pitch = 250f;

		if ( pitch < 50f )
			pitch = 50f;

		if ( pitch == 100f )
			pitch = 101f;

		Sound.EmitDyn( this, SoundChannel.Static, "apache/ap_rotor2.wav", 1f, 0.3f, Server.SoundChangePitch | Server.SoundChangeVolume, (int)pitch );
	}

	private void FireRocket()
	{
		if ( _rockets <= 0 )
			return;

		var globals = Server.Globals;

		Util.MakeAimVectors( Angles, globals );

		var source = Origin + (1.5f * ((globals.Forward * 21f) + (globals.Right * 70f * _rocketSide) + (globals.Up * -79f)));

		switch ( _rockets % 5 )
		{
			case 0:
				source += globals.Right * 10f;
				break;
			case 1:
				source -= globals.Right * 10f;
				break;
			case 2:
				source += globals.Up * 10f;
				break;
			case 3:
				source -= globals.Up * 10f;
				break;
		}

		Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, source );
		Server.WriteByte( TempEntity.Smoke );
		Server.WriteCoord( source.x );
		Server.WriteCoord( source.y );
		Server.WriteCoord( source.z );
		Server.WriteShort( GameModels.Smoke );
		Server.WriteByte( 20 );
		Server.WriteByte( 12 );
		Server.MessageEnd();

		if ( Create( "hvr_rocket", source, Angles, this ) is { } rocket )
			rocket.Velocity = Velocity + (globals.Forward * 100f);

		_rockets--;

		_rocketSide = -_rocketSide;
	}

	private bool FireGun()
	{
		var globals = Server.Globals;

		Util.MakeAimVectors( Angles, globals );

		var gunPosition = Server.GetAttachment( this, 1 );
		var target = Util.Normalize( _targetPosition - gunPosition );

		var localTarget = new Vector3(
			Vector3.Dot( globals.Forward, target ),
			-Vector3.Dot( globals.Right, target ),
			Vector3.Dot( globals.Up, target ) );

		var angles = MathLib.VectorAngles( localTarget );

		angles.x = -angles.x;

		if ( angles.y > 180f )
			angles.y -= 360f;

		if ( angles.y < -180f )
			angles.y += 360f;

		if ( angles.x > 180f )
			angles.x -= 360f;

		if ( angles.x < -180f )
			angles.x += 360f;

		if ( angles.x > _gunAngles.x )
			_gunAngles.x = Math.Min( angles.x, _gunAngles.x + 12f );

		if ( angles.x < _gunAngles.x )
			_gunAngles.x = Math.Max( angles.x, _gunAngles.x - 12f );

		if ( angles.y > _gunAngles.y )
			_gunAngles.y = Math.Min( angles.y, _gunAngles.y + 12f );

		if ( angles.y < _gunAngles.y )
			_gunAngles.y = Math.Max( angles.y, _gunAngles.y - 12f );

		_gunAngles.y = SetBoneController( 0, _gunAngles.y );
		_gunAngles.x = SetBoneController( 1, _gunAngles.x );

		var barrelPosition = Server.GetAttachment( this, 0 );
		var gunDirection = Util.Normalize( barrelPosition - gunPosition );

		if ( Vector3.Dot( gunDirection, target ) <= 0.98 )
			return false;

		FireBullets( 1, gunPosition, gunDirection, BasePlayerWeapon.Cone4Degrees, 8192f, Bullet.Monster12mm, 1 );
		Sound.Emit( this, SoundChannel.Weapon, "turret/tu_fire1.wav", 1f, 0.3f );

		return true;
	}

	private void ShowDamage()
	{
		if ( _doSmokePuff > 0 || Server.RandomLong( 0, 99 ) > Health )
		{
			Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, Origin );
			Server.WriteByte( TempEntity.Smoke );
			Server.WriteCoord( Origin.x );
			Server.WriteCoord( Origin.y );
			Server.WriteCoord( Origin.z - 32f );
			Server.WriteShort( GameModels.Smoke );
			Server.WriteByte( Server.RandomLong( 0, 9 ) + 20 );
			Server.WriteByte( 12 );
			Server.MessageEnd();
		}

		if ( _doSmokePuff > 0 )
			_doSmokePuff--;
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		if ( inflictor.Owner == this )
			return false;

		if ( (damageType & DamageBlast) != 0 )
			damage *= 2f;

		return EntityTakeDamage( inflictor, attacker, damage, damageType );
	}

	public override void TraceAttack( BaseEntity attacker, float damage, Vector3 direction, in Trace trace, int damageType )
	{
		if ( trace.HitGroup == 6 && (damageType & (DamageEnergyBeam | DamageBullet | DamageClub)) != 0 )
			return;

		if ( damage > 50f || trace.HitGroup == 1 || trace.HitGroup == 2 )
		{
			Combat.AddMultiDamage( attacker, this, damage, damageType );
			_doSmokePuff = (int)(3 + (damage / 5.0));
		}
		else
		{
			Util.Ricochet( trace.EndPos, 2f );
		}
	}
}

[LinkEntityToClass( "hvr_rocket" )]
public class ApacheHvr : Grenade
{
	private int _trail;
	[Save( FieldType.Vector )]
	private Vector3 _forward;

	public override void Spawn()
	{
		Precache();

		MoveType = MoveType.Fly;
		Solid = Solid.BBox;

		SetModel( "models/HVR.mdl" );
		SetSize( Vector3.Zero, Vector3.Zero );
		SetOrigin( Origin );

		SetThink( IgniteThink );
		SetTouch( ExplodeTouch );

		Util.MakeAimVectors( Angles, Server.Globals );
		_forward = Server.Globals.Forward;
		Gravity = 0.5f;

		NextThink = (float)(Server.Globals.Time + 0.1);

		Damage = 150f;
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/HVR.mdl" );
		_trail = Server.PrecacheModel( "sprites/smoke.spr" );
		Server.PrecacheSound( "weapons/rocket1.wav" );
	}

	public void IgniteThink()
	{
		Effects |= Effects.Light;

		Sound.Emit( this, SoundChannel.Voice, "weapons/rocket1.wav", 1f, 0.5f );

		Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
		Server.WriteByte( TempEntity.BeamFollow );
		Server.WriteShort( Index );
		Server.WriteShort( _trail );
		Server.WriteByte( 15 );
		Server.WriteByte( 5 );
		Server.WriteByte( 224 );
		Server.WriteByte( 224 );
		Server.WriteByte( 255 );
		Server.WriteByte( 255 );
		Server.MessageEnd();

		SetThink( AccelerateThink );
		NextThink = (float)(Server.Globals.Time + 0.1);
	}

	public void AccelerateThink()
	{
		if ( Origin.x < -4096f || Origin.x > 4096f || Origin.y < -4096f || Origin.y > 4096f || Origin.z < -4096f || Origin.z > 4096f )
		{
			Remove();
			return;
		}

		if ( MathLib.Length( Velocity ) < 1800f )
			Velocity += _forward * 200f;

		Angles = MathLib.VectorAngles( Velocity );

		NextThink = (float)(Server.Globals.Time + 0.1);
	}
}
