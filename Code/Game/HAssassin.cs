using System;
using GoldSrc;

namespace HalfLife;

public static class AssassinSchedule
{
	public const int Exposed = ScheduleType.LastCommon + 1;
	public const int Jump = Exposed + 1;
	public const int JumpAttack = Exposed + 2;
	public const int JumpLand = Exposed + 3;
}

public static class AssassinTask
{
	public const int FallToGround = TaskId.LastCommon + 1;
}

[SkipHotload]
public static class HAssassinAi
{
	public static readonly Schedule AssassinFail = new( "AssassinFail",
		BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionProvoked | BaseMonster.ConditionCanRangeAttack1 | BaseMonster.ConditionCanRangeAttack2 | BaseMonster.ConditionCanMeleeAttack1 | BaseMonster.ConditionHearSound,
		WorldSound.Danger | WorldSound.Player,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.WaitFaceEnemy, 2f ),
		new( TaskId.SetSchedule, ScheduleType.ChaseEnemy ) );

	public static readonly Schedule AssassinExposed = new( "AssassinExposed",
		BaseMonster.ConditionCanMeleeAttack1,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.RangeAttack1 ),
		new( TaskId.SetFailSchedule, AssassinSchedule.Jump ),
		new( TaskId.SetSchedule, ScheduleType.TakeCoverFromEnemy ) );

	public static readonly Schedule AssassinTakeCoverFromEnemy = new( "AssassinTakeCoverFromEnemy",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionCanMeleeAttack1 | BaseMonster.ConditionHearSound,
		WorldSound.Danger,
		new( TaskId.StopMoving ),
		new( TaskId.Wait, 0.2f ),
		new( TaskId.SetFailSchedule, ScheduleType.RangeAttack1 ),
		new( TaskId.FindCoverFromEnemy ),
		new( TaskId.RunPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.Remember, BaseMonster.MemoryInCover ),
		new( TaskId.FaceEnemy ) );

	public static readonly Schedule AssassinTakeCoverFromEnemy2 = new( "AssassinTakeCoverFromEnemy2",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionCanMeleeAttack2 | BaseMonster.ConditionHearSound,
		WorldSound.Danger,
		new( TaskId.StopMoving ),
		new( TaskId.Wait, 0.2f ),
		new( TaskId.FaceEnemy ),
		new( TaskId.RangeAttack1 ),
		new( TaskId.SetFailSchedule, ScheduleType.RangeAttack2 ),
		new( TaskId.FindFarNodeCoverFromEnemy, 384f ),
		new( TaskId.RunPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.Remember, BaseMonster.MemoryInCover ),
		new( TaskId.FaceEnemy ) );

	public static readonly Schedule AssassinTakeCoverFromBestSound = new( "AssassinTakeCoverFromBestSound",
		BaseMonster.ConditionNewEnemy,
		0,
		new( TaskId.SetFailSchedule, ScheduleType.MeleeAttack1 ),
		new( TaskId.StopMoving ),
		new( TaskId.FindCoverFromBestSound ),
		new( TaskId.RunPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.Remember, BaseMonster.MemoryInCover ),
		new( TaskId.TurnLeft, 179f ) );

	public static readonly Schedule AssassinHide = new( "AssassinHide",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionSeeEnemy | BaseMonster.ConditionSeeFear | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionProvoked | BaseMonster.ConditionHearSound,
		WorldSound.Danger,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.Wait, 2f ),
		new( TaskId.SetSchedule, ScheduleType.ChaseEnemy ) );

	public static readonly Schedule AssassinHunt = new( "AssassinHunt",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionCanRangeAttack1 | BaseMonster.ConditionHearSound,
		WorldSound.Danger,
		new( TaskId.GetPathToEnemy ),
		new( TaskId.RunPath ),
		new( TaskId.WaitForMovement ) );

	public static readonly Schedule AssassinJump = new( "AssassinJump",
		0,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.PlaySequence, (float)Activity.Hop ),
		new( TaskId.SetSchedule, AssassinSchedule.JumpAttack ) );

	public static readonly Schedule AssassinJumpAttack = new( "AssassinJumpAttack",
		0,
		0,
		new( TaskId.SetFailSchedule, AssassinSchedule.JumpLand ),
		new( AssassinTask.FallToGround ) );

	public static readonly Schedule AssassinJumpLand = new( "AssassinJumpLand",
		0,
		0,
		new( TaskId.SetFailSchedule, AssassinSchedule.Exposed ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.Remember, HAssassin.MemoryBadJump ),
		new( TaskId.FindNodeCoverFromEnemy ),
		new( TaskId.RunPath ),
		new( TaskId.Forget, HAssassin.MemoryBadJump ),
		new( TaskId.WaitForMovement ),
		new( TaskId.Remember, BaseMonster.MemoryInCover ),
		new( TaskId.FaceEnemy ),
		new( TaskId.SetFailSchedule, ScheduleType.RangeAttack1 ) );

	public static readonly Schedule[] List =
	[
		AssassinFail,
		AssassinExposed,
		AssassinTakeCoverFromEnemy,
		AssassinTakeCoverFromEnemy2,
		AssassinTakeCoverFromBestSound,
		AssassinHide,
		AssassinHunt,
		AssassinJump,
		AssassinJumpAttack,
		AssassinJumpLand
	];
}

[LinkEntityToClass( "monster_human_assassin" )]
public class HAssassin : BaseMonster
{
	public const int MemoryBadJump = MemoryCustom1;

	private const int AnimEventShoot1 = 1;
	private const int AnimEventToss1 = 2;
	private const int AnimEventJump = 3;

	private static bool _step;

	[Save( FieldType.Time )]
	private float _lastShot;
	[Save( FieldType.Float )]
	private float _deviation;
	[Save( FieldType.Time )]
	private float _nextJump;
	[Save( FieldType.Vector )]
	private Vector3 _jumpVelocity;
	[Save( FieldType.Time )]
	private float _nextGrenadeCheck;
	[Save( FieldType.Vector )]
	private Vector3 _tossVelocity;
	[Save( FieldType.Boolean )]
	private bool _throwGrenade;
	[Save( FieldType.Integer )]
	private int _targetRenderAmt;
	[Save( FieldType.Integer )]
	private int _frustration;

	private int _shell;

	public override Schedule ScheduleFromName( string name ) => ScheduleInList( name, HAssassinAi.List ) ?? base.ScheduleFromName( name );

	public override void DeathSound()
	{
	}

	public override void IdleSound()
	{
	}

	public override int SoundMask() => WorldSound.World | WorldSound.Combat | WorldSound.Danger | WorldSound.Player;

	public override int Classify() => ClassHumanMilitary;

	public override void SetYawSpeed() => YawSpeed = 360f;

	public void Shoot()
	{
		if ( EnemyEntity is null )
			return;

		var globals = Server.Globals;
		var shootOrigin = GetGunPosition();
		var shootDirection = ShootAtEnemy( shootOrigin );

		if ( _lastShot + 2f < globals.Time )
		{
			_deviation = 0.10f;
		}
		else
		{
			_deviation = (float)(_deviation - 0.01);

			if ( _deviation < 0.02 )
				_deviation = 0.02f;
		}

		_lastShot = globals.Time;

		globals.MakeVectors( Angles );

		var shellVelocity = (globals.Right * Server.RandomFloat( 40f, 90f )) + (globals.Up * Server.RandomFloat( 75f, 200f )) + (globals.Forward * Server.RandomFloat( -40f, 40f ));

		Combat.EjectBrass( Origin + (globals.Up * 32f) + (globals.Forward * 12f), shellVelocity, Angles.y, _shell, TempEntity.BounceShell );
		FireBullets( 1, shootOrigin, shootDirection, new Vector3( _deviation, _deviation, _deviation ), 2048f, Bullet.Monster9mm );

		var sample = Server.RandomLong( 0, 1 ) == 0 ? "weapons/pl_gun1.wav" : "weapons/pl_gun2.wav";

		Sound.Emit( this, SoundChannel.Weapon, sample, Server.RandomFloat( 0.6f, 0.8f ), Server.AttenuationNormal );

		Effects |= Effects.MuzzleFlash;

		SetBlending( 0, MathLib.VectorAngles( shootDirection ).x );

		AmmoLoaded--;
	}

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		var globals = Server.Globals;

		switch ( monsterEvent.Event )
		{
			case AnimEventShoot1:
				Shoot();
				break;

			case AnimEventToss1:
				globals.MakeVectors( Angles );
				Grenade.ShootTimed( this, Origin + (globals.Forward * 34f) + new Vector3( 0f, 0f, 32f ), _tossVelocity, 2f );

				_nextGrenadeCheck = globals.Time + 6f;
				_throwGrenade = false;
				break;

			case AnimEventJump:
				Util.MakeAimVectors( Angles, globals );

				MoveType = MoveType.Toss;
				EntityFlags &= ~EntityFlags.OnGround;
				Velocity = _jumpVelocity;
				_nextJump = (float)(globals.Time + 3.0);
				break;

			default:
				base.HandleAnimEvent( monsterEvent );
				break;
		}
	}

	public override void Spawn()
	{
		Precache();

		SetModel( "models/hassassin.mdl" );
		SetSize( HumanHullMin, HumanHullMax );

		Solid = Solid.SlideBox;
		MoveType = MoveType.Step;
		MonsterBloodColor = BloodColorRed;
		Effects = 0;
		Health = HalfLifeDll.SkillData.HassassinHealth;
		FieldOfView = ViewFieldWide;
		MonsterState = MonsterState.None;
		Capability = CapabilityMeleeAttack1 | CapabilityDoorsGroup;
		Friction = 1f;

		HackedGunPos = new Vector3( 0f, 24f, 48f );

		_targetRenderAmt = 20;
		RenderAmt = 20f;
		RenderMode = RenderMode.TransTexture;

		MonsterInit();
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/hassassin.mdl" );

		Server.PrecacheSound( "weapons/pl_gun1.wav" );
		Server.PrecacheSound( "weapons/pl_gun2.wav" );

		Server.PrecacheSound( "debris/beamstart1.wav" );

		_shell = Server.PrecacheModel( "models/shell.mdl" );
	}

	public override bool CheckMeleeAttack1( float dot, float distance )
	{
		if ( _nextJump >= Server.Globals.Time || (distance > 128f && !HasMemory( MemoryBadJump )) || EnemyEntity is null )
			return false;

		var lift = new Vector3( 0f, 0f, 36f );
		var destination = Origin + new Vector3( Server.RandomFloat( -64f, 64f ), Server.RandomFloat( -64f, 64f ), 160f );
		var trace = Util.TraceHull( Origin + lift, destination + lift, DontIgnoreMonsters, HumanHull, this );

		if ( trace.StartSolid || trace.Fraction < 1.0 )
			return false;

		var gravity = Cvar.GetFloat( "sv_gravity" );
		var time = (float)Math.Sqrt( 160 / (0.5 * gravity) );
		var speed = gravity * time / 160f;

		_jumpVelocity = (destination - Origin) * speed;

		return true;
	}

	public override bool CheckRangeAttack1( float dot, float distance )
	{
		if ( HasConditions( ConditionEnemyOccluded ) || distance <= 64f || distance > 2048f )
			return false;

		var enemy = EnemyEntity;
		var source = GetGunPosition();
		var trace = Util.TraceLine( source, enemy.BodyTarget( source ), DontIgnoreMonsters, this );

		return trace.Fraction == 1f || trace.Ent == enemy;
	}

	public override bool CheckRangeAttack2( float dot, float distance )
	{
		_throwGrenade = false;

		var enemy = EnemyEntity;

		if ( (enemy.EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround )
			return false;

		if ( _frustration <= 2 )
			return false;

		if ( _nextGrenadeCheck >= Server.Globals.Time || HasConditions( ConditionEnemyOccluded ) || distance > 512f )
			return false;

		var toss = Ai.VecCheckThrow( this, GetGunPosition(), enemy.Center, distance, 0.5f );

		if ( MathLib.VectorCompare( toss, Vector3.Zero ) )
			return false;

		_tossVelocity = toss;
		_throwGrenade = true;

		return true;
	}

	public override void RunAi()
	{
		base.RunAi();

		var moving = Activity is Activity.Run or Activity.Walk;

		if ( HalfLifeDll.SkillLevel != SkillData.Hard || EnemyEntity is null || DeadFlag != DeadNo || moving || (EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround )
			_targetRenderAmt = 255;
		else
			_targetRenderAmt = 20;

		if ( RenderAmt > _targetRenderAmt )
		{
			if ( RenderAmt == 255f )
				Sound.Emit( this, SoundChannel.Body, "debris/beamstart1.wav", 0.2f, Server.AttenuationNormal );

			RenderAmt = Math.Max( RenderAmt - 50f, _targetRenderAmt );
			RenderMode = RenderMode.TransTexture;
		}
		else if ( RenderAmt < _targetRenderAmt )
		{
			RenderAmt = Math.Min( RenderAmt + 50f, _targetRenderAmt );

			if ( RenderAmt == 255f )
				RenderMode = RenderMode.Normal;
		}

		if ( !moving )
			return;

		_step = !_step;

		if ( !_step )
			return;

		switch ( Server.RandomLong( 0, 3 ) )
		{
			case 0:
				Sound.Emit( this, SoundChannel.Body, "player/pl_step1.wav", 0.5f, Server.AttenuationNormal );
				break;
			case 1:
				Sound.Emit( this, SoundChannel.Body, "player/pl_step3.wav", 0.5f, Server.AttenuationNormal );
				break;
			case 2:
				Sound.Emit( this, SoundChannel.Body, "player/pl_step2.wav", 0.5f, Server.AttenuationNormal );
				break;
			case 3:
				Sound.Emit( this, SoundChannel.Body, "player/pl_step4.wav", 0.5f, Server.AttenuationNormal );
				break;
		}
	}

	public override void StartTask( Task task )
	{
		switch ( task.Id )
		{
			case TaskId.RangeAttack2:
				if ( !_throwGrenade )
					TaskComplete();
				else
					base.StartTask( task );

				break;

			case AssassinTask.FallToGround:
				break;

			default:
				base.StartTask( task );
				break;
		}
	}

	public override void RunTask( Task task )
	{
		if ( task.Id != AssassinTask.FallToGround )
		{
			base.RunTask( task );
			return;
		}

		MakeIdealYaw( EnemyLastKnownPosition );
		ChangeYaw( (int)YawSpeed );

		if ( SequenceFinished )
		{
			if ( Velocity.z > 0f )
			{
				Sequence = LookupSequence( "fly_up" );
			}
			else if ( HasConditions( ConditionSeeEnemy ) )
			{
				Sequence = LookupSequence( "fly_attack" );
				Frame = 0f;
			}
			else
			{
				Sequence = LookupSequence( "fly_down" );
				Frame = 0f;
			}

			ResetSequenceInfo();
			SetYawSpeed();
		}

		if ( (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround )
			TaskComplete();
	}

	private Schedule GetCombatSchedule()
	{
		if ( HasConditions( ConditionEnemyDead ) )
			return GetBaseSchedule();

		if ( MoveType == MoveType.Toss )
		{
			if ( (EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround )
				return GetScheduleOfType( AssassinSchedule.Jump );

			MoveType = MoveType.Step;
			return GetScheduleOfType( AssassinSchedule.JumpLand );
		}

		if ( HasConditions( ConditionHearSound ) && BestSound() is { } sound && (sound.Type & WorldSound.Danger) != 0 )
			return GetScheduleOfType( ScheduleType.TakeCoverFromBestSound );

		if ( HasConditions( ConditionLightDamage ) )
			_frustration++;

		if ( HasConditions( ConditionHeavyDamage ) )
			_frustration++;

		if ( HasConditions( ConditionCanMeleeAttack1 ) )
			return GetScheduleOfType( ScheduleType.MeleeAttack1 );

		if ( HasConditions( ConditionCanRangeAttack2 ) )
			return GetScheduleOfType( ScheduleType.RangeAttack2 );

		if ( HasConditions( ConditionSeeEnemy ) && HasConditions( ConditionEnemyFacingMe ) )
		{
			_frustration++;
			return GetScheduleOfType( AssassinSchedule.Exposed );
		}

		if ( HasConditions( ConditionCanRangeAttack1 ) )
		{
			_frustration = 0;
			return GetScheduleOfType( ScheduleType.RangeAttack1 );
		}

		if ( HasConditions( ConditionSeeEnemy ) )
			return GetScheduleOfType( ScheduleType.CombatFace );

		if ( HasConditions( ConditionNewEnemy ) )
			return GetScheduleOfType( ScheduleType.TakeCoverFromEnemy );

		return GetScheduleOfType( ScheduleType.AlertStand );
	}

	public override Schedule GetSchedule()
	{
		switch ( MonsterState )
		{
			case MonsterState.Idle:
			case MonsterState.Alert:
				if ( HasConditions( ConditionHearSound ) && BestSound() is { } sound )
				{
					if ( (sound.Type & WorldSound.Danger) != 0 )
						return GetScheduleOfType( ScheduleType.TakeCoverFromBestSound );

					if ( (sound.Type & WorldSound.Combat) != 0 )
						return GetScheduleOfType( ScheduleType.InvestigateSound );
				}

				break;

			case MonsterState.Combat:
				return GetCombatSchedule();
		}

		return GetBaseSchedule();
	}

	public override Schedule GetScheduleOfType( int type )
	{
		switch ( type )
		{
			case ScheduleType.TakeCoverFromEnemy:
				return Health > 30f ? HAssassinAi.AssassinTakeCoverFromEnemy : HAssassinAi.AssassinTakeCoverFromEnemy2;

			case ScheduleType.TakeCoverFromBestSound:
				return HAssassinAi.AssassinTakeCoverFromBestSound;

			case AssassinSchedule.Exposed:
				return HAssassinAi.AssassinExposed;

			case ScheduleType.Fail when MonsterState == MonsterState.Combat:
				return HAssassinAi.AssassinFail;

			case ScheduleType.AlertStand when MonsterState == MonsterState.Combat:
				return HAssassinAi.AssassinHide;

			case ScheduleType.ChaseEnemy:
				return HAssassinAi.AssassinHunt;

			case ScheduleType.MeleeAttack1:
				if ( (EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround )
					return HAssassinAi.AssassinJumpAttack;

				return _nextJump > Server.Globals.Time ? HAssassinAi.AssassinFail : HAssassinAi.AssassinJump;

			case AssassinSchedule.Jump:
			case AssassinSchedule.JumpAttack:
				return HAssassinAi.AssassinJumpAttack;

			case AssassinSchedule.JumpLand:
				return HAssassinAi.AssassinJumpLand;
		}

		return base.GetScheduleOfType( type );
	}
}
