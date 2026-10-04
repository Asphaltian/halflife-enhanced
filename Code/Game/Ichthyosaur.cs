using System;
using GoldSrc;

namespace HalfLife;

public static class IchthyosaurTask
{
	public const int CircleEnemy = TaskId.LastCommon + 1;
	public const int Swim = CircleEnemy + 1;
	public const int Float = CircleEnemy + 2;
}

[SkipHotload]
public static class IchthyosaurAi
{
	public static readonly Schedule SwimAround = new( "SwimAround",
		BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionSeeEnemy | BaseMonster.ConditionNewEnemy | BaseMonster.ConditionHearSound,
		WorldSound.Player | WorldSound.Combat,
		new( TaskId.SetActivity, (float)Activity.Walk ),
		new( IchthyosaurTask.Swim ) );

	public static readonly Schedule SwimAgitated = new( "SwimAgitated",
		0,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Run ),
		new( TaskId.Wait, 2.0f ) );

	public static readonly Schedule CircleEnemy = new( "CircleEnemy",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionCanMeleeAttack1 | BaseMonster.ConditionCanRangeAttack1,
		0,
		new( TaskId.SetActivity, (float)Activity.Walk ),
		new( IchthyosaurTask.CircleEnemy ) );

	public static readonly Schedule TwitchDie = new( "Die",
		0,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.SoundDie ),
		new( TaskId.Die ),
		new( IchthyosaurTask.Float ) );

	public static readonly Schedule[] List =
	[
		SwimAround,
		SwimAgitated,
		CircleEnemy,
		TwitchDie
	];
}

[LinkEntityToClass( "monster_ichthyosaur" )]
public class Ichthyosaur : FlyingMonster
{
	private const float SwimSpeed = 150f;
	private const int ProbeLength = 150;

	private const int EyeMad = 0;
	private const int EyeBase = 1;
	private const int EyeClosed = 2;

	private const int AnimEventShakeRight = 1;
	private const int AnimEventShakeLeft = 2;

	private static readonly string[] IdleSounds = ["ichy/ichy_idle1.wav", "ichy/ichy_idle2.wav", "ichy/ichy_idle3.wav", "ichy/ichy_idle4.wav"];
	private static readonly string[] AlertSounds = ["ichy/ichy_alert2.wav", "ichy/ichy_alert3.wav"];
	private static readonly string[] AttackSounds = ["ichy/ichy_attack1.wav", "ichy/ichy_attack2.wav"];
	private static readonly string[] BiteSounds = ["ichy/ichy_bite1.wav", "ichy/ichy_bite2.wav"];
	private static readonly string[] PainSounds = ["ichy/ichy_pain2.wav", "ichy/ichy_pain3.wav", "ichy/ichy_pain5.wav"];
	private static readonly string[] DieSounds = ["ichy/ichy_die2.wav", "ichy/ichy_die4.wav"];

	private static float _yawAdjust;

	[Save( FieldType.Vector )]
	private Vector3 _saveVelocity;
	[Save( FieldType.Float )]
	private float _idealDistance;
	[Save( FieldType.Float )]
	private float _blink;
	[Save( FieldType.Float )]
	private float _enemyTouched;
	[Save( FieldType.Boolean )]
	private bool _onAttack;
	[Save( FieldType.Float )]
	private float _maxSpeed;
	[Save( FieldType.Float )]
	private float _minSpeed;
	[Save( FieldType.Float )]
	private float _maxDistance;
	[Save( FieldType.Time )]
	private float _nextAlert;

	private float _lastZYawTime;

	public override Schedule ScheduleFromName( string name ) => ScheduleInList( name, IchthyosaurAi.List ) ?? base.ScheduleFromName( name );

	private void IckySound( SoundChannel channel, string[] sounds )
	{
		Sound.EmitDyn( this, channel, RandomSound( sounds ), 1f, 0.6f, 0, Server.RandomLong( 95, 105 ) );
	}

	public override void IdleSound() => IckySound( SoundChannel.Voice, IdleSounds );

	public override void AlertSound() => IckySound( SoundChannel.Voice, AlertSounds );

	public void AttackSound() => IckySound( SoundChannel.Voice, AttackSounds );

	public void BiteSound() => IckySound( SoundChannel.Weapon, BiteSounds );

	public override void DeathSound() => IckySound( SoundChannel.Voice, DieSounds );

	public override void PainSound() => IckySound( SoundChannel.Voice, PainSounds );

	public override int Classify() => ClassAlienMonster;

	public override bool CheckMeleeAttack1( float dot, float distance ) => dot >= 0.7 && _enemyTouched > Server.Globals.Time - 0.2;

	public void BiteTouch( BaseEntity other )
	{
		if ( other != EnemyEntity )
			return;

		_enemyTouched = Server.Globals.Time;
		_onAttack = true;
	}

	public void CombatUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( ShouldToggle( useType, _onAttack ) )
			_onAttack = !_onAttack;
	}

	public override bool CheckRangeAttack1( float dot, float distance )
	{
		return dot > -0.7 && (_onAttack || (distance <= 192f && _idealDistance <= 192f));
	}

	public override void SetYawSpeed() => YawSpeed = 100f;

	public override void Killed( BaseEntity attacker, int gib )
	{
		MonsterKilled( attacker, gib );

		Velocity = Vector3.Zero;
	}

	public override void BecomeDead()
	{
		DamageMode = DamageYes;

		Health = MaxHealth / 2f;
		MaxHealth = 5f;
	}

	private void Shake( BaseEntity hurt )
	{
		var globals = Server.Globals;
		var shootDirection = ShootAtEnemy( Origin );

		Util.MakeAimVectors( Angles, globals );

		if ( Vector3.Dot( shootDirection, globals.Forward ) <= 0.707 )
			return;

		_onAttack = true;

		hurt.PunchAngle = hurt.PunchAngle with { z = -18f, x = 5f };
		hurt.Velocity -= globals.Right * 300f;

		if ( hurt.IsPlayer )
		{
			var angles = hurt.Angles;

			angles.x += Server.RandomFloat( -35f, 35f );
			angles.y += Server.RandomFloat( -90f, 90f );
			angles.z = 0f;

			hurt.Angles = angles;
			hurt.FixAngle = 1;
		}

		hurt.TakeDamage( this, this, HalfLifeDll.SkillData.IchthyosaurDmgShake, DamageSlash );
	}

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		if ( monsterEvent.Event is not (AnimEventShakeRight or AnimEventShakeLeft) )
		{
			base.HandleAnimEvent( monsterEvent );
			return;
		}

		if ( EnemyEntity is { } enemy && IsVisible( enemy ) )
		{
			if ( _enemyTouched < Server.Globals.Time - 0.2 && MathLib.Length( enemy.BodyTarget( Origin ) - Origin ) > 32 + 16 + 32 )
				return;

			Shake( enemy );
		}

		BiteSound();

		var source = Origin + (Server.Globals.Forward * 32f);

		Util.Bubbles( source - new Vector3( 8f, 8f, 8f ), source + new Vector3( 8f, 8f, 8f ), 16 );
	}

	public override void Spawn()
	{
		Precache();

		SetModel( "models/icky.mdl" );
		SetSize( new Vector3( -32f, -32f, -32f ), new Vector3( 32f, 32f, 32f ) );

		Solid = Solid.BBox;
		MoveType = MoveType.Fly;
		MonsterBloodColor = BloodColorGreen;
		Health = HalfLifeDll.SkillData.IchthyosaurHealth;
		ViewOffset = new Vector3( 0f, 0f, 16f );
		FieldOfView = ViewFieldWide;
		MonsterState = MonsterState.None;
		EntityFlags |= EntityFlags.Swim;

		SetFlyingSpeed( SwimSpeed );
		SetFlyingMomentum( 2.5f );

		Capability = CapabilityRangeAttack1 | CapabilitySwim;

		MonsterInit();

		SetTouch( BiteTouch );
		SetUse( CombatUse );

		_idealDistance = 384f;
		_minSpeed = 80f;
		_maxSpeed = 300f;
		_maxDistance = 384f;

		MathLib.AngleVectors( Angles, out var forward, out _, out _ );

		Velocity = FlightSpeed * Util.Normalize( forward );
		_saveVelocity = Velocity;
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/icky.mdl" );

		PrecacheSounds( IdleSounds );
		PrecacheSounds( AlertSounds );
		PrecacheSounds( AttackSounds );
		PrecacheSounds( BiteSounds );
		PrecacheSounds( DieSounds );
		PrecacheSounds( PainSounds );
	}

	public override Schedule GetSchedule()
	{
		switch ( MonsterState )
		{
			case MonsterState.Idle:
				FlightSpeed = 80f;
				return GetScheduleOfType( ScheduleType.IdleWalk );

			case MonsterState.Alert:
				FlightSpeed = 150f;
				return GetScheduleOfType( ScheduleType.IdleWalk );

			case MonsterState.Combat:
				_maxSpeed = 400f;

				if ( HasConditions( ConditionCanMeleeAttack1 ) )
					return GetScheduleOfType( ScheduleType.MeleeAttack1 );

				if ( HasConditions( ConditionCanRangeAttack1 ) )
					return GetScheduleOfType( ScheduleType.ChaseEnemy );

				if ( HasConditions( ConditionHeavyDamage ) )
					_onAttack = true;

				if ( Health < MaxHealth - 20f )
					_onAttack = true;

				return GetScheduleOfType( ScheduleType.Standoff );
		}

		return base.GetSchedule();
	}

	public override Schedule GetScheduleOfType( int type )
	{
		switch ( type )
		{
			case ScheduleType.IdleWalk:
				return IchthyosaurAi.SwimAround;

			case ScheduleType.Standoff:
				return IchthyosaurAi.CircleEnemy;

			case ScheduleType.Fail:
				return IchthyosaurAi.SwimAgitated;

			case ScheduleType.Die:
				return IchthyosaurAi.TwitchDie;

			case ScheduleType.ChaseEnemy:
				AttackSound();
				break;
		}

		return base.GetScheduleOfType( type );
	}

	public override void StartTask( Task task )
	{
		switch ( task.Id )
		{
			case IchthyosaurTask.CircleEnemy:
			case IchthyosaurTask.Swim:
				break;

			case TaskId.SmallFlinch:
				if ( _idealDistance > 128f )
				{
					_maxDistance = 512f;
					_idealDistance = 512f;
				}
				else
				{
					_onAttack = true;
				}

				base.StartTask( task );
				break;

			case IchthyosaurTask.Float:
				Skin = EyeBase;
				SetSequenceByName( "bellyup" );
				break;

			default:
				base.StartTask( task );
				break;
		}
	}

	private void CircleEnemy()
	{
		var globals = Server.Globals;

		if ( EnemyEntity is not { } enemy )
		{
			TaskComplete();
		}
		else if ( IsVisible( enemy ) )
		{
			var from = enemy.EyePosition;
			var delta = Util.Normalize( Origin - from );
			var swim = Util.Normalize( MathLib.CrossProduct( delta, new Vector3( 0f, 0f, 1f ) ) );

			if ( Vector3.Dot( swim, _saveVelocity ) < 0f )
				swim *= -1f;

			var position = from + (delta * _idealDistance) + (swim * 32f);
			var trace = Util.TraceHull( from, position, IgnoreMonsters, LargeHull, enemy );

			if ( trace.Fraction > 0.5 )
				position = trace.EndPos;

			_saveVelocity = (_saveVelocity * 0.8f) + (0.2f * Util.Normalize( position - Origin ) * FlightSpeed);

			if ( HasConditions( ConditionEnemyFacingMe ) && enemy.IsVisible( this ) )
			{
				_nextAlert = (float)(_nextAlert - 0.1);

				if ( _idealDistance < _maxDistance )
					_idealDistance += 4f;

				if ( FlightSpeed > _minSpeed )
					FlightSpeed -= 2f;
				else if ( FlightSpeed < _minSpeed )
					FlightSpeed += 2f;

				if ( _minSpeed < _maxSpeed )
					_minSpeed += 0.5f;
			}
			else
			{
				_nextAlert = (float)(_nextAlert + 0.1);

				if ( _idealDistance > 128f )
					_idealDistance -= 4f;

				if ( FlightSpeed < _maxSpeed )
					FlightSpeed += 4f;
			}
		}
		else
		{
			_nextAlert = (float)(globals.Time + 0.2);
		}

		if ( _nextAlert < globals.Time )
		{
			AlertSound();
			_nextAlert = globals.Time + Server.RandomFloat( 3f, 5f );
		}
	}

	public override void RunTask( Task task )
	{
		switch ( task.Id )
		{
			case IchthyosaurTask.CircleEnemy:
				CircleEnemy();
				break;

			case IchthyosaurTask.Swim:
				if ( SequenceFinished )
					TaskComplete();

				break;

			case TaskId.Die:
				if ( SequenceFinished )
				{
					DeadFlag = DeadDead;
					TaskComplete();
				}

				break;

			case IchthyosaurTask.Float:
			{
				Angles = Angles with { x = Util.ApproachAngle( 0f, Angles.x, 20f ) };

				var velocity = Velocity * 0.8f;

				if ( WaterLevel > 1 && velocity.z < 64f )
					velocity.z += 8f;
				else
					velocity.z -= 8f;

				Velocity = velocity;
				break;
			}

			default:
				base.RunTask( task );
				break;
		}
	}

	public override float ChangeYaw( int yawSpeed )
	{
		if ( MoveType == MoveType.Fly )
		{
			var globals = Server.Globals;
			var difference = YawDiff();
			var target = 0f;

			if ( _lastZYawTime == 0f )
				_lastZYawTime = globals.Time - globals.FrameTime;

			if ( IdealActivity != GetStoppedActivity() )
			{
				if ( difference < -20f )
					target = 20f;
				else if ( difference > 20f )
					target = -20f;
			}

			var delta = globals.Time - _lastZYawTime;

			_lastZYawTime = globals.Time;

			if ( delta > 0.25f )
				delta = 0.25f;

			Angles = Angles with { z = Util.Approach( target, Angles.z, 220f * delta ) };
		}

		return base.ChangeYaw( yawSpeed );
	}

	public override Activity GetStoppedActivity() => MoveType != MoveType.Fly ? Activity.Idle : Activity.Walk;

	public override void MoveExecute( BaseEntity target, Vector3 direction, float interval ) => _saveVelocity = direction * FlightSpeed;

	public override void MonsterThink()
	{
		base.MonsterThink();

		if ( DeadFlag != DeadNo || MonsterState == MonsterState.Script )
			return;

		Swim();

		if ( _blink >= Server.Globals.Time )
			return;

		Skin = EyeClosed;

		if ( _blink + 0.2 < Server.Globals.Time )
		{
			_blink = Server.Globals.Time + Server.RandomFloat( 3f, 4f );
			Skin = _onAttack ? EyeMad : EyeBase;
		}
	}

	public override void Stop()
	{
		if ( !_onAttack )
			FlightSpeed = 80f;
	}

	private static float ShortestTurn( float delta )
	{
		var turn = 360f;

		if ( Math.Abs( delta ) < Math.Abs( turn ) )
			turn = delta;

		if ( Math.Abs( delta + 360f ) < Math.Abs( turn ) )
			turn = delta + 360f;

		if ( Math.Abs( delta - 360f ) < Math.Abs( turn ) )
			turn = delta - 360f;

		return turn;
	}

	public void Swim()
	{
		var start = Origin;
		Vector3 forward;
		Vector3 right;
		Vector3 up;

		if ( (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround )
		{
			Angles = Angles with { x = 0f, y = Angles.y + Server.RandomFloat( -45f, 45f ) };
			EntityFlags &= ~EntityFlags.OnGround;

			MathLib.AngleVectors( Angles with { x = -Angles.x }, out forward, out _, out up );

			Velocity = (forward * 200f) + (up * 200f);
			return;
		}

		if ( _onAttack && FlightSpeed < _maxSpeed )
			FlightSpeed += 40f;

		if ( FlightSpeed < 180f )
		{
			if ( IdealActivity == Activity.Run )
				SetActivity( Activity.Walk );

			if ( IdealActivity == Activity.Walk )
				FrameRate = (float)(FlightSpeed / 150.0);
		}
		else
		{
			if ( IdealActivity == Activity.Walk )
				SetActivity( Activity.Run );

			if ( IdealActivity == Activity.Run )
				FrameRate = (float)(FlightSpeed / 150.0);
		}

		var swimAngles = MathLib.VectorAngles( _saveVelocity );

		swimAngles.x = -swimAngles.x;

		MathLib.AngleVectors( swimAngles, out forward, out right, out up );

		var side = start + (ProbeLength / 3 * forward);
		var steering = DoProbe( start + (ProbeLength * forward) );

		steering += DoProbe( side + right );
		steering += DoProbe( side - right );
		steering += DoProbe( side + up );
		steering += DoProbe( side - up );

		_saveVelocity = Util.Normalize( _saveVelocity + (steering / 2f) );

		MathLib.AngleVectors( Angles with { x = -Angles.x }, out forward, out _, out _ );

		var dot = Vector3.Dot( forward, _saveVelocity );

		if ( dot > 0.5 )
			_saveVelocity *= FlightSpeed;
		else if ( dot > 0f )
			_saveVelocity = _saveVelocity * FlightSpeed * (float)(dot + 0.5);
		else
			_saveVelocity *= 80f;

		Velocity = _saveVelocity;

		swimAngles = MathLib.VectorAngles( _saveVelocity );

		if ( swimAngles.x > 180f )
			swimAngles.x -= 360f;

		var angles = Angles;

		angles.x = Util.Approach( swimAngles.x, angles.x, (float)(50 * 0.1) );

		if ( angles.x < -80f )
			angles.x = -80f;

		if ( angles.x > 80f )
			angles.x = 80f;

		var turn = ShortestTurn( swimAngles.y - angles.y );
		var speed = (float)(FlightSpeed * 0.1);

		if ( Math.Abs( turn ) > speed )
			turn = turn < 0.0 ? -speed : speed;

		angles.y += turn;
		angles.z -= turn;
		angles.y = (float)((angles.y + 360.0) % 360.0);

		_yawAdjust = (float)((_yawAdjust * 0.8) + turn);

		SetBoneController( 0, (float)(-_yawAdjust / 4.0) );

		turn = ShortestTurn( swimAngles.z - angles.z );
		speed = (float)(FlightSpeed / 2f * 0.1);

		if ( Math.Abs( turn ) < speed )
			angles.z += turn;
		else if ( turn < 0.0 )
			angles.z -= speed;
		else
			angles.z += speed;

		if ( angles.z < -20f )
			angles.z = -20f;

		if ( angles.z > 20f )
			angles.z = 20f;

		Angles = angles;
	}

	public Vector3 DoProbe( Vector3 probe )
	{
		var wallNormal = new Vector3( 0f, 0f, -1f );
		var bumpedSomething = ProbeZ( Origin, probe, out var fraction );
		var trace = Server.TraceMonsterHull( this, Origin, probe, DontIgnoreMonsters, this );

		if ( trace.AllSolid || trace.Fraction < 0.99 )
		{
			if ( trace.Fraction < 0.0 )
				trace.Fraction = 0f;

			if ( trace.Fraction > 1.0 )
				trace.Fraction = 1f;

			if ( trace.Fraction < fraction )
			{
				fraction = trace.Fraction;
				bumpedSomething = true;
				wallNormal = trace.PlaneNormal;
			}
		}

		if ( !bumpedSomething || (EnemyEntity is { } enemy && trace.Ent == enemy) )
			return Vector3.Zero;

		var probeDirection = probe - Origin;
		var normalToProbeAndWall = MathLib.CrossProduct( probeDirection, wallNormal );
		var steering = MathLib.CrossProduct( normalToProbeAndWall, probeDirection );
		var steeringForce = FlightSpeed * (1f - fraction) * Vector3.Dot( Util.Normalize( wallNormal ), Util.Normalize( _saveVelocity ) );

		if ( steeringForce < 0.0 )
			steeringForce = -steeringForce;

		return steeringForce * Util.Normalize( steering );
	}
}
