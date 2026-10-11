using GoldSrc;

namespace HalfLife;

public static class AGruntSchedule
{
	public const int Suppress = ScheduleType.LastCommon + 1;
	public const int ThreatDisplay = Suppress + 1;
}

public static class AGruntTask
{
	public const int SetupHideAttack = TaskId.LastCommon + 1;
	public const int GetPathToEnemyCorpse = SetupHideAttack + 1;
}

[SkipHotload]
public static class AGruntAi
{
	public static readonly Schedule AGruntFail = new( "AGrunt Fail",
		BaseMonster.ConditionCanRangeAttack1 | BaseMonster.ConditionCanMeleeAttack1,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.Wait, 2f ),
		new( TaskId.WaitPvs ) );

	public static readonly Schedule AGruntCombatFail = new( "AGrunt Combat Fail",
		BaseMonster.ConditionCanRangeAttack1 | BaseMonster.ConditionCanMeleeAttack1,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.WaitFaceEnemy, 2f ),
		new( TaskId.WaitPvs ) );

	public static readonly Schedule AGruntStandoff = new( "Agrunt Standoff",
		BaseMonster.ConditionCanRangeAttack1 | BaseMonster.ConditionCanMeleeAttack1 | BaseMonster.ConditionSeeEnemy | BaseMonster.ConditionNewEnemy | BaseMonster.ConditionHearSound,
		WorldSound.Danger,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.WaitFaceEnemy, 2f ) );

	public static readonly Schedule AGruntSuppress = new( "AGrunt Suppress Hornet",
		0,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.RangeAttack1 ) );

	public static readonly Schedule AGruntRangeAttack1 = new( "AGrunt Range Attack1",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionEnemyDead | BaseMonster.ConditionHeavyDamage,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.FaceEnemy ),
		new( TaskId.RangeAttack1 ) );

	public static readonly Schedule AGruntHiddenRangeAttack = new( "AGrunt Hidden Range Attack1",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionHearSound,
		WorldSound.Danger,
		new( TaskId.SetFailSchedule, ScheduleType.Standoff ),
		new( AGruntTask.SetupHideAttack ),
		new( TaskId.StopMoving ),
		new( TaskId.FaceIdeal ),
		new( TaskId.RangeAttack1NoTurn ) );

	public static readonly Schedule AGruntTakeCoverFromEnemy = new( "AGruntTakeCoverFromEnemy",
		BaseMonster.ConditionNewEnemy,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.Wait, 0.2f ),
		new( TaskId.FindCoverFromEnemy ),
		new( TaskId.RunPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.Remember, BaseMonster.MemoryInCover ),
		new( TaskId.FaceEnemy ) );

	public static readonly Schedule AGruntVictoryDance = new( "AGruntVictoryDance",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.SetFailSchedule, AGruntSchedule.ThreatDisplay ),
		new( TaskId.Wait, 0.2f ),
		new( AGruntTask.GetPathToEnemyCorpse ),
		new( TaskId.WalkPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.FaceEnemy ),
		new( TaskId.PlaySequence, (float)Activity.Crouch ),
		new( TaskId.PlaySequence, (float)Activity.VictoryDance ),
		new( TaskId.PlaySequence, (float)Activity.VictoryDance ),
		new( TaskId.PlaySequence, (float)Activity.Stand ),
		new( TaskId.PlaySequence, (float)Activity.ThreatDisplay ),
		new( TaskId.PlaySequence, (float)Activity.Crouch ),
		new( TaskId.PlaySequence, (float)Activity.VictoryDance ),
		new( TaskId.PlaySequence, (float)Activity.VictoryDance ),
		new( TaskId.PlaySequence, (float)Activity.VictoryDance ),
		new( TaskId.PlaySequence, (float)Activity.VictoryDance ),
		new( TaskId.PlaySequence, (float)Activity.VictoryDance ),
		new( TaskId.PlaySequence, (float)Activity.Stand ) );

	public static readonly Schedule AGruntThreatDisplay = new( "AGruntThreatDisplay",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage,
		WorldSound.Player | WorldSound.Combat | WorldSound.World,
		new( TaskId.StopMoving ),
		new( TaskId.FaceEnemy ),
		new( TaskId.PlaySequence, (float)Activity.ThreatDisplay ) );

	public static readonly Schedule[] List =
	[
		AGruntFail,
		AGruntCombatFail,
		AGruntStandoff,
		AGruntSuppress,
		AGruntRangeAttack1,
		AGruntHiddenRangeAttack,
		AGruntTakeCoverFromEnemy,
		AGruntVictoryDance,
		AGruntThreatDisplay
	];
}

[LinkEntityToClass( "monster_alien_grunt" )]
public class AGrunt : SquadMonster
{
	private const int AnimEventHornet1 = 1;
	private const int AnimEventHornet2 = 2;
	private const int AnimEventHornet3 = 3;
	private const int AnimEventHornet4 = 4;
	private const int AnimEventHornet5 = 5;
	private const int AnimEventLeftFoot = 10;
	private const int AnimEventRightFoot = 11;
	private const int AnimEventLeftPunch = 12;
	private const int AnimEventRightPunch = 13;

	private const float MeleeDistance = 100f;

	private static readonly string[] AttackHitSounds = ["zombie/claw_strike1.wav", "zombie/claw_strike2.wav", "zombie/claw_strike3.wav"];
	private static readonly string[] AttackMissSounds = ["zombie/claw_miss1.wav", "zombie/claw_miss2.wav"];
	private static readonly string[] AttackSounds = ["agrunt/ag_attack1.wav", "agrunt/ag_attack2.wav", "agrunt/ag_attack3.wav"];
	private static readonly string[] DieSounds = ["agrunt/ag_die1.wav", "agrunt/ag_die4.wav", "agrunt/ag_die5.wav"];
	private static readonly string[] PainSounds = ["agrunt/ag_pain1.wav", "agrunt/ag_pain2.wav", "agrunt/ag_pain3.wav", "agrunt/ag_pain4.wav", "agrunt/ag_pain5.wav"];
	private static readonly string[] IdleSounds = ["agrunt/ag_idle1.wav", "agrunt/ag_idle2.wav", "agrunt/ag_idle3.wav", "agrunt/ag_idle4.wav"];
	private static readonly string[] AlertSounds = ["agrunt/ag_alert1.wav", "agrunt/ag_alert3.wav", "agrunt/ag_alert4.wav", "agrunt/ag_alert5.wav"];
	private static readonly string[] FireSounds = ["agrunt/ag_fire1.wav", "agrunt/ag_fire2.wav", "agrunt/ag_fire3.wav"];

	private static int _muzzleFlash;

	[Save( FieldType.Boolean )]
	private bool _canHornetAttack;
	[Save( FieldType.Time )]
	private float _nextHornetAttackCheck;
	[Save( FieldType.Time )]
	private float _nextPainTime;
	[Save( FieldType.Time )]
	private float _nextSpeakTime;
	[Save( FieldType.Time )]
	private float _nextWordTime;
	[Save( FieldType.Integer )]
	private int _lastWord;

	public override Schedule ScheduleFromName( string name ) => ScheduleInList( name, AGruntAi.List ) ?? base.ScheduleFromName( name );

	public override void SetObjectCollisionBox()
	{
		AbsMin = Origin + new Vector3( -32f, -32f, 0f );
		AbsMax = Origin + new Vector3( 32f, 32f, 85f );
	}

	public override int Relationship( BaseEntity target )
	{
		return target.ClassName == "monster_human_grunt" ? RelationshipNemesis : base.Relationship( target );
	}

	public override int SoundMask() => WorldSound.World | WorldSound.Combat | WorldSound.Player | WorldSound.Danger;

	public override void TraceAttack( BaseEntity attacker, float damage, Vector3 direction, in Trace trace, int damageType )
	{
		if ( trace.HitGroup == 10 && (damageType & (DamageBullet | DamageSlash | DamageClub)) != 0 )
		{
			if ( DamageTime != Server.Globals.Time || Server.RandomLong( 0, 10 ) < 1 )
			{
				Util.Ricochet( trace.EndPos, Server.RandomFloat( 1f, 2f ) );
				DamageTime = Server.Globals.Time;
			}

			if ( Server.RandomLong( 0, 1 ) == 0 )
			{
				var tracerDirection = direction;

				tracerDirection.x += Server.RandomFloat( -0.3f, 0.3f );
				tracerDirection.y += Server.RandomFloat( -0.3f, 0.3f );
				tracerDirection.z += Server.RandomFloat( -0.3f, 0.3f );

				tracerDirection *= -512f;

				Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, trace.EndPos );
				Server.WriteByte( TempEntity.Tracer );
				Server.WriteCoord( trace.EndPos.x );
				Server.WriteCoord( trace.EndPos.y );
				Server.WriteCoord( trace.EndPos.z );
				Server.WriteCoord( tracerDirection.x );
				Server.WriteCoord( tracerDirection.y );
				Server.WriteCoord( tracerDirection.z );
				Server.MessageEnd();
			}

			damage -= 20f;

			if ( damage <= 0f )
				damage = 0.1f;
		}
		else
		{
			Combat.SpawnBlood( trace.EndPos, BloodColor, damage );
			TraceBleed( damage, direction, trace, damageType );
		}

		Combat.AddMultiDamage( attacker, this, damage, damageType );
	}

	public void StopTalking()
	{
		_nextWordTime = _nextSpeakTime = Server.Globals.Time + 10f + Server.RandomLong( 0, 10 );
	}

	public bool ShouldSpeak()
	{
		if ( _nextSpeakTime > Server.Globals.Time )
			return false;

		if ( (SpawnFlags & SpawnFlagGag) != 0 && MonsterState != MonsterState.Combat )
		{
			_nextSpeakTime = Server.Globals.Time + 3f;
			return false;
		}

		return true;
	}

	public override void PrescheduleThink()
	{
		if ( !ShouldSpeak() || _nextWordTime >= Server.Globals.Time )
			return;

		int word;

		do
		{
			word = Server.RandomLong( 0, IdleSounds.Length - 1 );
		}
		while ( word == _lastWord );

		_lastWord = word;

		Sound.Emit( this, SoundChannel.Voice, IdleSounds[word], 1f, Server.AttenuationNormal );

		if ( Server.RandomLong( 1, 10 ) <= 1 )
			StopTalking();
		else
			_nextWordTime = Server.Globals.Time + Server.RandomFloat( 0.5f, 1f );
	}

	public override void DeathSound()
	{
		StopTalking();

		Sound.Emit( this, SoundChannel.Voice, RandomSound( DieSounds ), 1f, Server.AttenuationNormal );
	}

	public override void AlertSound()
	{
		StopTalking();

		Sound.Emit( this, SoundChannel.Voice, RandomSound( AlertSounds ), 1f, Server.AttenuationNormal );
	}

	public void AttackSound()
	{
		StopTalking();

		Sound.Emit( this, SoundChannel.Voice, RandomSound( AttackSounds ), 1f, Server.AttenuationNormal );
	}

	public override void PainSound()
	{
		if ( _nextPainTime > Server.Globals.Time )
			return;

		_nextPainTime = (float)(Server.Globals.Time + 0.6);

		StopTalking();

		Sound.Emit( this, SoundChannel.Voice, RandomSound( PainSounds ), 1f, Server.AttenuationNormal );
	}

	public override int Classify() => ClassAlienMilitary;

	public override void SetYawSpeed()
	{
		YawSpeed = Activity is Activity.TurnLeft or Activity.TurnRight ? 110f : 100f;
	}

	private void FireHornet()
	{
		var globals = Server.Globals;
		Vector3 directionToEnemy;
		Vector3 aimAngles;

		if ( HasConditions( ConditionSeeEnemy ) )
		{
			directionToEnemy = EnemyLastKnownPosition - Origin;
			aimAngles = MathLib.VectorAngles( directionToEnemy );
			directionToEnemy = Util.Normalize( directionToEnemy );
		}
		else
		{
			aimAngles = Angles;
			Util.MakeAimVectors( aimAngles, globals );
			directionToEnemy = globals.Forward;
		}

		Effects = Effects.MuzzleFlash;

		if ( aimAngles.x > 180f )
			aimAngles.x -= 360f;

		SetBlending( 0, aimAngles.x );

		var armPosition = Server.GetAttachment( this, 0 ) + (directionToEnemy * 32f);

		Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, armPosition );
		Server.WriteByte( TempEntity.Sprite );
		Server.WriteCoord( armPosition.x );
		Server.WriteCoord( armPosition.y );
		Server.WriteCoord( armPosition.z );
		Server.WriteShort( _muzzleFlash );
		Server.WriteByte( 6 );
		Server.WriteByte( 128 );
		Server.MessageEnd();

		var hornet = Create( "hornet", armPosition, MathLib.VectorAngles( directionToEnemy ), this );

		globals.MakeVectors( hornet.Angles );
		hornet.Velocity = globals.Forward * 300f;

		Sound.EmitDyn( this, SoundChannel.Weapon, RandomSound( FireSounds ), 1f, Server.AttenuationNormal, 0, 100 );

		if ( hornet is BaseMonster hornetMonster )
			hornetMonster.EnemyEntity = EnemyEntity;
	}

	private void Punch( float punchYaw, float push )
	{
		if ( CheckTraceHullAttack( MeleeDistance, (int)HalfLifeDll.SkillData.AgruntDmgPunch, DamageClub ) is { } hurt )
		{
			hurt.PunchAngle = hurt.PunchAngle with { y = punchYaw, x = 8f };

			if ( hurt.IsPlayer )
				hurt.Velocity += Server.Globals.Right * push;

			Sound.EmitDyn( this, SoundChannel.Weapon, RandomSound( AttackHitSounds ), 1f, Server.AttenuationNormal, 0, 100 + Server.RandomLong( -5, 5 ) );

			Combat.SpawnBlood( Server.GetAttachment( this, 0 ), hurt.BloodColor, 25f );
		}
		else
		{
			Sound.EmitDyn( this, SoundChannel.Weapon, RandomSound( AttackMissSounds ), 1f, Server.AttenuationNormal, 0, 100 + Server.RandomLong( -5, 5 ) );
		}
	}

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		switch ( monsterEvent.Event )
		{
			case AnimEventHornet1:
			case AnimEventHornet2:
			case AnimEventHornet3:
			case AnimEventHornet4:
			case AnimEventHornet5:
				FireHornet();
				break;

			case AnimEventLeftFoot:
				Sound.EmitDyn( this, SoundChannel.Body, Server.RandomLong( 0, 1 ) == 0 ? "player/pl_ladder2.wav" : "player/pl_ladder4.wav", 1f, Server.AttenuationNormal, 0, 70 );
				break;

			case AnimEventRightFoot:
				Sound.EmitDyn( this, SoundChannel.Body, Server.RandomLong( 0, 1 ) == 0 ? "player/pl_ladder1.wav" : "player/pl_ladder3.wav", 1f, Server.AttenuationNormal, 0, 70 );
				break;

			case AnimEventLeftPunch:
				Punch( -25f, 250f );
				break;

			case AnimEventRightPunch:
				Punch( 25f, -250f );
				break;

			default:
				base.HandleAnimEvent( monsterEvent );
				break;
		}
	}

	public override void Spawn()
	{
		Precache();

		SetModel( "models/agrunt.mdl" );
		SetSize( new Vector3( -32f, -32f, 0f ), new Vector3( 32f, 32f, 64f ) );

		Solid = Solid.SlideBox;
		MoveType = MoveType.Step;
		MonsterBloodColor = BloodColorGreen;
		Effects = 0;
		Health = HalfLifeDll.SkillData.AgruntHealth;
		FieldOfView = 0.2f;
		MonsterState = MonsterState.None;
		Capability = CapabilitySquad;

		HackedGunPos = new Vector3( 24f, 64f, 48f );

		_nextSpeakTime = _nextWordTime = Server.Globals.Time + 10f + Server.RandomLong( 0, 10 );

		MonsterInit();
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/agrunt.mdl" );

		PrecacheSounds( AttackHitSounds );
		PrecacheSounds( AttackMissSounds );
		PrecacheSounds( IdleSounds );
		PrecacheSounds( DieSounds );
		PrecacheSounds( PainSounds );
		PrecacheSounds( AttackSounds );
		PrecacheSounds( AlertSounds );

		Server.PrecacheSound( "hassault/hw_shoot1.wav" );

		_muzzleFlash = Server.PrecacheModel( "sprites/muz4.spr" );

		Util.PrecacheOther( "hornet" );
	}

	public override bool CanCheckAttacks() => !HasConditions( ConditionEnemyTooFar );

	public override bool CheckMeleeAttack1( float dot, float distance )
	{
		return HasConditions( ConditionSeeEnemy ) && distance <= MeleeDistance && dot >= 0.6 && EnemyEntity is not null;
	}

	public override bool CheckRangeAttack1( float dot, float distance )
	{
		if ( Server.Globals.Time < _nextHornetAttackCheck )
			return _canHornetAttack;

		if ( HasConditions( ConditionSeeEnemy ) && distance >= MeleeDistance && distance <= 1024f && dot >= 0.5 && NoFriendlyFire() )
		{
			var enemy = EnemyEntity;

			Server.Globals.MakeVectors( Angles );

			var armPosition = Server.GetAttachment( this, 0 );
			var trace = Util.TraceLine( armPosition, enemy.BodyTarget( armPosition ), DontIgnoreMonsters, this );

			if ( trace.Fraction == 1.0 || trace.Ent == enemy )
			{
				_nextHornetAttackCheck = Server.Globals.Time + Server.RandomFloat( 2f, 5f );
				_canHornetAttack = true;
				return _canHornetAttack;
			}
		}

		_nextHornetAttackCheck = (float)(Server.Globals.Time + 0.2);
		_canHornetAttack = false;
		return _canHornetAttack;
	}

	private bool TryHideAttackSide( Vector3 offset, Vector3 facing )
	{
		var trace = Util.TraceLine( Center + offset, EnemyLastKnownPosition, IgnoreMonsters, this );

		if ( trace.Fraction != 1.0 )
			return false;

		MakeIdealYaw( Origin + facing );
		TaskComplete();

		return true;
	}

	public override void StartTask( Task task )
	{
		var globals = Server.Globals;

		switch ( task.Id )
		{
			case AGruntTask.GetPathToEnemyCorpse:
				globals.MakeVectors( Angles );

				if ( BuildRoute( EnemyLastKnownPosition - (globals.Forward * 50f), MoveFlag.ToLocation, null ) )
				{
					TaskComplete();
				}
				else
				{
					Util.Alert( AlertType.AiConsole, "AGruntGetPathToEnemyCorpse failed!!" );
					TaskFail();
				}

				break;

			case AGruntTask.SetupHideAttack:
				if ( EnemyEntity is not BaseMonster )
				{
					Util.Alert( AlertType.AiConsole, "AGRunt - no enemy monster ptr!!!" );
					TaskFail();
					break;
				}

				if ( !TryHideAttackSide( globals.Forward * 128f, globals.Right * 128f )
					&& !TryHideAttackSide( globals.Forward * -128f, globals.Right * -128f )
					&& !TryHideAttackSide( globals.Forward * 256f, globals.Right * 256f )
					&& !TryHideAttackSide( globals.Forward * -256f, globals.Right * -256f ) )
				{
					TaskFail();
				}

				break;

			default:
				base.StartTask( task );
				break;
		}
	}

	public override Schedule GetSchedule()
	{
		if ( HasConditions( ConditionHearSound ) && BestSound() is { } sound && (sound.Type & WorldSound.Danger) != 0 )
			return GetScheduleOfType( ScheduleType.TakeCoverFromBestSound );

		if ( MonsterState != MonsterState.Combat )
			return base.GetSchedule();

		if ( HasConditions( ConditionEnemyDead ) )
			return GetBaseSchedule();

		if ( HasConditions( ConditionNewEnemy ) )
			return GetScheduleOfType( ScheduleType.WakeAngry );

		if ( HasConditions( ConditionCanMeleeAttack1 ) )
		{
			AttackSound();
			return GetScheduleOfType( ScheduleType.MeleeAttack1 );
		}

		if ( HasConditions( ConditionHeavyDamage ) )
			return GetScheduleOfType( ScheduleType.SmallFlinch );

		if ( HasConditions( ConditionCanRangeAttack1 ) && OccupySlot( SquadSlot.AlienGruntHornet ) )
			return GetScheduleOfType( ScheduleType.RangeAttack1 );

		if ( OccupySlot( SquadSlot.AlienGruntChase ) )
			return GetScheduleOfType( ScheduleType.ChaseEnemy );

		return GetScheduleOfType( ScheduleType.Standoff );
	}

	public override Schedule GetScheduleOfType( int type )
	{
		return type switch
		{
			ScheduleType.TakeCoverFromEnemy => AGruntAi.AGruntTakeCoverFromEnemy,
			ScheduleType.RangeAttack1 => AGruntAi.AGruntRangeAttack1,
			AGruntSchedule.ThreatDisplay => AGruntAi.AGruntThreatDisplay,
			AGruntSchedule.Suppress => AGruntAi.AGruntSuppress,
			ScheduleType.Standoff => AGruntAi.AGruntStandoff,
			ScheduleType.VictoryDance => AGruntAi.AGruntVictoryDance,
			ScheduleType.Fail => EnemyEntity is not null ? AGruntAi.AGruntCombatFail : AGruntAi.AGruntFail,
			_ => base.GetScheduleOfType( type )
		};
	}
}
