using GoldSrc;

namespace HalfLife;

public static class GruntSchedule
{
	public const int Suppress = ScheduleType.LastCommon + 1;
	public const int EstablishLineOfFire = Suppress + 1;
	public const int CoverAndReload = Suppress + 2;
	public const int Sweep = Suppress + 3;
	public const int FoundEnemy = Suppress + 4;
	public const int Repel = Suppress + 5;
	public const int RepelAttack = Suppress + 6;
	public const int RepelLand = Suppress + 7;
	public const int WaitFaceEnemy = Suppress + 8;
	public const int TakeCoverFailed = Suppress + 9;
	public const int ElofFail = Suppress + 10;
}

public static class GruntTask
{
	public const int FaceTossDir = TaskId.LastCommon + 1;
	public const int SpeakSentence = FaceTossDir + 1;
	public const int CheckFire = FaceTossDir + 2;
}

[SkipHotload]
public static class HGruntAi
{
	public static readonly Schedule GruntFail = new( "Grunt Fail",
		BaseMonster.ConditionCanRangeAttack1 | BaseMonster.ConditionCanRangeAttack2 | BaseMonster.ConditionCanMeleeAttack1 | BaseMonster.ConditionCanMeleeAttack2,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.Wait, 2f ),
		new( TaskId.WaitPvs ) );

	public static readonly Schedule GruntCombatFail = new( "Grunt Combat Fail",
		BaseMonster.ConditionCanRangeAttack1 | BaseMonster.ConditionCanRangeAttack2,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.WaitFaceEnemy, 2f ),
		new( TaskId.WaitPvs ) );

	public static readonly Schedule GruntVictoryDance = new( "GruntVictoryDance",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.FaceEnemy ),
		new( TaskId.Wait, 1.5f ),
		new( TaskId.GetPathToEnemyCorpse ),
		new( TaskId.WalkPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.FaceEnemy ),
		new( TaskId.PlaySequence, (float)Activity.VictoryDance ) );

	public static readonly Schedule GruntEstablishLineOfFire = new( "GruntEstablishLineOfFire",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionEnemyDead | BaseMonster.ConditionCanRangeAttack1 | BaseMonster.ConditionCanMeleeAttack1 | BaseMonster.ConditionCanRangeAttack2 | BaseMonster.ConditionCanMeleeAttack2 | BaseMonster.ConditionHearSound,
		WorldSound.Danger,
		new( TaskId.SetFailSchedule, GruntSchedule.ElofFail ),
		new( TaskId.GetPathToEnemy ),
		new( GruntTask.SpeakSentence ),
		new( TaskId.RunPath ),
		new( TaskId.WaitForMovement ) );

	public static readonly Schedule GruntFoundEnemy = new( "GruntFoundEnemy",
		BaseMonster.ConditionHearSound,
		WorldSound.Danger,
		new( TaskId.StopMoving ),
		new( TaskId.FaceEnemy ),
		new( TaskId.PlaySequenceFaceEnemy, (float)Activity.Signal1 ) );

	public static readonly Schedule GruntCombatFace = new( "Combat Face",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionEnemyDead | BaseMonster.ConditionCanRangeAttack1 | BaseMonster.ConditionCanRangeAttack2,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.FaceEnemy ),
		new( TaskId.Wait, 1.5f ),
		new( TaskId.SetSchedule, GruntSchedule.Sweep ) );

	public static readonly Schedule GruntSignalSuppress = new( "SignalSuppress",
		BaseMonster.ConditionEnemyDead | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionHearSound | HGrunt.ConditionNoFire | BaseMonster.ConditionNoAmmoLoaded,
		WorldSound.Danger,
		new( TaskId.StopMoving ),
		new( TaskId.FaceIdeal ),
		new( TaskId.PlaySequenceFaceEnemy, (float)Activity.Signal2 ),
		new( TaskId.FaceEnemy ),
		new( GruntTask.CheckFire ),
		new( TaskId.RangeAttack1 ),
		new( TaskId.FaceEnemy ),
		new( GruntTask.CheckFire ),
		new( TaskId.RangeAttack1 ),
		new( TaskId.FaceEnemy ),
		new( GruntTask.CheckFire ),
		new( TaskId.RangeAttack1 ),
		new( TaskId.FaceEnemy ),
		new( GruntTask.CheckFire ),
		new( TaskId.RangeAttack1 ),
		new( TaskId.FaceEnemy ),
		new( GruntTask.CheckFire ),
		new( TaskId.RangeAttack1 ) );

	public static readonly Schedule GruntSuppress = new( "Suppress",
		BaseMonster.ConditionEnemyDead | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionHearSound | HGrunt.ConditionNoFire | BaseMonster.ConditionNoAmmoLoaded,
		WorldSound.Danger,
		new( TaskId.StopMoving ),
		new( TaskId.FaceEnemy ),
		new( GruntTask.CheckFire ),
		new( TaskId.RangeAttack1 ),
		new( TaskId.FaceEnemy ),
		new( GruntTask.CheckFire ),
		new( TaskId.RangeAttack1 ),
		new( TaskId.FaceEnemy ),
		new( GruntTask.CheckFire ),
		new( TaskId.RangeAttack1 ),
		new( TaskId.FaceEnemy ),
		new( GruntTask.CheckFire ),
		new( TaskId.RangeAttack1 ),
		new( TaskId.FaceEnemy ),
		new( GruntTask.CheckFire ),
		new( TaskId.RangeAttack1 ) );

	public static readonly Schedule GruntWaitInCover = new( "GruntWaitInCover",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionHearSound | BaseMonster.ConditionCanRangeAttack1 | BaseMonster.ConditionCanRangeAttack2 | BaseMonster.ConditionCanMeleeAttack1 | BaseMonster.ConditionCanMeleeAttack2,
		WorldSound.Danger,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.WaitFaceEnemy, 1f ) );

	public static readonly Schedule GruntTakeCover = new( "TakeCover",
		0,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.SetFailSchedule, GruntSchedule.TakeCoverFailed ),
		new( TaskId.Wait, 0.2f ),
		new( TaskId.FindCoverFromEnemy ),
		new( GruntTask.SpeakSentence ),
		new( TaskId.RunPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.Remember, BaseMonster.MemoryInCover ),
		new( TaskId.SetSchedule, GruntSchedule.WaitFaceEnemy ) );

	public static readonly Schedule GruntGrenadeCover = new( "GrenadeCover",
		0,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.FindCoverFromEnemy, 99f ),
		new( TaskId.FindFarNodeCoverFromEnemy, 384f ),
		new( TaskId.PlaySequence, (float)Activity.SpecialAttack1 ),
		new( TaskId.ClearMoveWait ),
		new( TaskId.RunPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.SetSchedule, GruntSchedule.WaitFaceEnemy ) );

	public static readonly Schedule GruntTossGrenadeCover = new( "TossGrenadeCover",
		0,
		0,
		new( TaskId.FaceEnemy ),
		new( TaskId.RangeAttack2 ),
		new( TaskId.SetSchedule, ScheduleType.TakeCoverFromEnemy ) );

	public static readonly Schedule GruntTakeCoverFromBestSound = new( "GruntTakeCoverFromBestSound",
		0,
		0,
		new( TaskId.SetFailSchedule, ScheduleType.Cower ),
		new( TaskId.StopMoving ),
		new( TaskId.FindCoverFromBestSound ),
		new( TaskId.RunPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.Remember, BaseMonster.MemoryInCover ),
		new( TaskId.TurnLeft, 179f ) );

	public static readonly Schedule GruntHideReload = new( "GruntHideReload",
		BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionHearSound,
		WorldSound.Danger,
		new( TaskId.StopMoving ),
		new( TaskId.SetFailSchedule, ScheduleType.Reload ),
		new( TaskId.FindCoverFromEnemy ),
		new( TaskId.RunPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.Remember, BaseMonster.MemoryInCover ),
		new( TaskId.FaceEnemy ),
		new( TaskId.PlaySequence, (float)Activity.Reload ) );

	public static readonly Schedule GruntSweep = new( "Grunt Sweep",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionCanRangeAttack1 | BaseMonster.ConditionCanRangeAttack2 | BaseMonster.ConditionHearSound,
		WorldSound.World | WorldSound.Danger | WorldSound.Player,
		new( TaskId.TurnLeft, 179f ),
		new( TaskId.Wait, 1f ),
		new( TaskId.TurnLeft, 179f ),
		new( TaskId.Wait, 1f ) );

	public static readonly Schedule GruntRangeAttack1A = new( "Range Attack1A",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionEnemyDead | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionEnemyOccluded | BaseMonster.ConditionHearSound | HGrunt.ConditionNoFire | BaseMonster.ConditionNoAmmoLoaded,
		WorldSound.Danger,
		new( TaskId.StopMoving ),
		new( TaskId.PlaySequenceFaceEnemy, (float)Activity.Crouch ),
		new( GruntTask.CheckFire ),
		new( TaskId.RangeAttack1 ),
		new( TaskId.FaceEnemy ),
		new( GruntTask.CheckFire ),
		new( TaskId.RangeAttack1 ),
		new( TaskId.FaceEnemy ),
		new( GruntTask.CheckFire ),
		new( TaskId.RangeAttack1 ),
		new( TaskId.FaceEnemy ),
		new( GruntTask.CheckFire ),
		new( TaskId.RangeAttack1 ) );

	public static readonly Schedule GruntRangeAttack1B = new( "Range Attack1B",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionEnemyDead | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionEnemyOccluded | BaseMonster.ConditionNoAmmoLoaded | HGrunt.ConditionNoFire | BaseMonster.ConditionHearSound,
		WorldSound.Danger,
		new( TaskId.StopMoving ),
		new( TaskId.PlaySequenceFaceEnemy, (float)Activity.IdleAngry ),
		new( GruntTask.CheckFire ),
		new( TaskId.RangeAttack1 ),
		new( TaskId.FaceEnemy ),
		new( GruntTask.CheckFire ),
		new( TaskId.RangeAttack1 ),
		new( TaskId.FaceEnemy ),
		new( GruntTask.CheckFire ),
		new( TaskId.RangeAttack1 ),
		new( TaskId.FaceEnemy ),
		new( GruntTask.CheckFire ),
		new( TaskId.RangeAttack1 ) );

	public static readonly Schedule GruntRangeAttack2 = new( "RangeAttack2",
		0,
		0,
		new( TaskId.StopMoving ),
		new( GruntTask.FaceTossDir ),
		new( TaskId.PlaySequence, (float)Activity.RangeAttack2 ),
		new( TaskId.SetSchedule, GruntSchedule.WaitFaceEnemy ) );

	public static readonly Schedule GruntRepel = new( "Repel",
		BaseMonster.ConditionSeeEnemy | BaseMonster.ConditionNewEnemy | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionHearSound,
		WorldSound.Danger | WorldSound.Combat | WorldSound.Player,
		new( TaskId.StopMoving ),
		new( TaskId.FaceIdeal ),
		new( TaskId.PlaySequence, (float)Activity.Glide ) );

	public static readonly Schedule GruntRepelAttack = new( "Repel Attack",
		BaseMonster.ConditionEnemyOccluded,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.FaceEnemy ),
		new( TaskId.PlaySequence, (float)Activity.Fly ) );

	public static readonly Schedule GruntRepelLand = new( "Repel Land",
		BaseMonster.ConditionSeeEnemy | BaseMonster.ConditionNewEnemy | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionHearSound,
		WorldSound.Danger | WorldSound.Combat | WorldSound.Player,
		new( TaskId.StopMoving ),
		new( TaskId.PlaySequence, (float)Activity.Land ),
		new( TaskId.GetPathToLastPosition ),
		new( TaskId.RunPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.ClearLastPosition ) );

	public static readonly Schedule[] List =
	[
		GruntFail,
		GruntCombatFail,
		GruntVictoryDance,
		GruntEstablishLineOfFire,
		GruntFoundEnemy,
		GruntCombatFace,
		GruntSignalSuppress,
		GruntSuppress,
		GruntWaitInCover,
		GruntTakeCover,
		GruntGrenadeCover,
		GruntTossGrenadeCover,
		GruntTakeCoverFromBestSound,
		GruntHideReload,
		GruntSweep,
		GruntRangeAttack1A,
		GruntRangeAttack1B,
		GruntRangeAttack2,
		GruntRepel,
		GruntRepelAttack,
		GruntRepelLand
	];
}

[LinkEntityToClass( "monster_human_grunt" )]
public class HGrunt : SquadMonster
{
	public const int ConditionNoFire = ConditionSpecial1;

	public const int Weapon9mmAR = 1 << 0;
	public const int WeaponHandGrenade = 1 << 1;
	public const int WeaponGrenadeLauncher = 1 << 2;
	public const int WeaponShotgun = 1 << 3;

	public const int HeadGroup = 1;
	public const int HeadGrunt = 0;
	public const int HeadCommander = 1;
	public const int HeadShotgun = 2;
	public const int HeadM203 = 3;

	public const int GunGroup = 2;
	public const int GunMp5 = 0;
	public const int GunShotgun = 1;
	public const int GunNone = 2;

	private const int ClipSize = 36;
	private const float LimpHealth = 20f;
	private const float SentenceVolume = 0.35f;

	private const int AnimEventReload = 2;
	private const int AnimEventKick = 3;
	private const int AnimEventBurst1 = 4;
	private const int AnimEventBurst2 = 5;
	private const int AnimEventBurst3 = 6;
	private const int AnimEventGrenadeToss = 7;
	private const int AnimEventGrenadeLaunch = 8;
	private const int AnimEventGrenadeDrop = 9;
	private const int AnimEventCaughtEnemy = 10;
	private const int AnimEventDropGun = 11;

	private const int SentenceNone = -1;
	private const int SentenceCover = 3;
	private const int SentenceCharge = 5;

	private static readonly string[] GruntSentences = ["HG_GREN", "HG_ALERT", "HG_MONSTER", "HG_COVER", "HG_THROW", "HG_CHARGE", "HG_TAUNT"];

	private static int _gruntQuestion;

	[Save( FieldType.Time )]
	private float _nextGrenadeCheck;
	[Save( FieldType.Time )]
	private float _nextPainTime;
	[Save( FieldType.Vector )]
	private Vector3 _tossVelocity;
	[Save( FieldType.Boolean )]
	private bool _throwGrenade;
	[Save( FieldType.Boolean )]
	private bool _standing;
	[Save( FieldType.Boolean )]
	private bool _firstEncounter;
	[Save( FieldType.Integer )]
	private int _clipSize;
	[Save( FieldType.Integer )]
	private int _voicePitch;
	[Save( FieldType.Integer )]
	private int _sentence;

	private int _brassShell;
	private int _shotgunShell;

	public override Schedule ScheduleFromName( string name ) => ScheduleInList( name, HGruntAi.List ) ?? base.ScheduleFromName( name );

	private void Speak( string group )
	{
		Sentences.PlayRandom( this, group, SentenceVolume, Server.AttenuationNormal, 0, _voicePitch );
	}

	public void SpeakSentence()
	{
		if ( _sentence == SentenceNone || !OkToSpeak() )
			return;

		Speak( GruntSentences[_sentence] );
		JustSpoke();
	}

	public override int Relationship( BaseEntity target )
	{
		if ( target.ClassName is "monster_alien_grunt" or "monster_gargantua" )
			return RelationshipNemesis;

		return base.Relationship( target );
	}

	private void TossGun( BaseEntity gun )
	{
		if ( gun is null )
			return;

		gun.Velocity = new Vector3( Server.RandomFloat( -100f, 100f ), Server.RandomFloat( -100f, 100f ), Server.RandomFloat( 200f, 300f ) );
		gun.AVelocity = new Vector3( 0f, Server.RandomFloat( 200f, 400f ), 0f );
	}

	public override void GibMonster()
	{
		if ( GetBodygroup( 2 ) != 2 )
		{
			var gunPosition = Server.GetAttachment( this, 0 );

			TossGun( DropItem( (Weapons & WeaponShotgun) != 0 ? "weapon_shotgun" : "weapon_9mmAR", gunPosition, Vector3.Zero ) );

			if ( (Weapons & WeaponGrenadeLauncher) != 0 )
				TossGun( DropItem( "ammo_ARgrenades", gunPosition, Vector3.Zero ) );
		}

		base.GibMonster();
	}

	public override int SoundMask() => WorldSound.World | WorldSound.Combat | WorldSound.Player | WorldSound.Danger;

	public bool OkToSpeak()
	{
		if ( Server.Globals.Time <= TalkMonster.TalkWaitTime )
			return false;

		return (SpawnFlags & SpawnFlagGag) == 0 || MonsterState == MonsterState.Combat;
	}

	public void JustSpoke()
	{
		TalkMonster.TalkWaitTime = Server.Globals.Time + Server.RandomFloat( 1.5f, 2f );
		_sentence = SentenceNone;
	}

	public override void PrescheduleThink()
	{
		if ( !InSquad || EnemyEntity is null )
			return;

		if ( HasConditions( ConditionSeeEnemy ) )
			MySquadLeader().LastEnemySightTime = Server.Globals.Time;
		else if ( Server.Globals.Time - MySquadLeader().LastEnemySightTime > 5f )
			MySquadLeader().EnemyEluded = true;
	}

	public override bool CanCheckAttacks() => !HasConditions( ConditionEnemyTooFar );

	public override bool CheckMeleeAttack1( float dot, float distance )
	{
		if ( EnemyEntity is not BaseMonster enemy )
			return false;

		return distance <= 64f && dot >= 0.7 && enemy.Classify() != ClassAlienBioWeapon && enemy.Classify() != ClassPlayerBioWeapon;
	}

	public override bool CheckRangeAttack1( float dot, float distance )
	{
		if ( HasConditions( ConditionEnemyOccluded ) || distance > 2048f || dot < 0.5 || !NoFriendlyFire() )
			return false;

		var enemy = EnemyEntity;

		if ( !enemy.IsPlayer && distance <= 64f )
			return false;

		var source = GetGunPosition();
		var trace = Util.TraceLine( source, enemy.BodyTarget( source ), IgnoreMonsters | IgnoreGlass, this );

		return trace.Fraction == 1.0;
	}

	public override bool CheckRangeAttack2( float dot, float distance )
	{
		if ( (Weapons & (WeaponHandGrenade | WeaponGrenadeLauncher)) == 0 )
			return false;

		if ( GroundSpeed != 0f )
		{
			_throwGrenade = false;
			return _throwGrenade;
		}

		if ( Server.Globals.Time < _nextGrenadeCheck )
			return _throwGrenade;

		var enemy = EnemyEntity;

		if ( (enemy.EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround && enemy.WaterLevel == 0 && EnemyLastKnownPosition.z > AbsMax.z )
		{
			_throwGrenade = false;
			return _throwGrenade;
		}

		Vector3 target;

		if ( (Weapons & WeaponHandGrenade) != 0 )
		{
			target = Server.RandomLong( 0, 1 ) != 0 ? new Vector3( enemy.Origin.x, enemy.Origin.y, enemy.AbsMin.z ) : EnemyLastKnownPosition;
		}
		else
		{
			target = EnemyLastKnownPosition + (enemy.BodyTarget( Origin ) - enemy.Origin);

			if ( HasConditions( ConditionSeeEnemy ) )
				target += MathLib.Length( target - Origin ) / HalfLifeDll.SkillData.HgruntGrenadeSpeed * enemy.Velocity;
		}

		if ( InSquad && SquadMemberInRange( target, 256f ) )
		{
			_nextGrenadeCheck = Server.Globals.Time + 1f;
			_throwGrenade = false;
		}

		if ( MathLib.Length2D( target - Origin ) <= 256f )
		{
			_nextGrenadeCheck = Server.Globals.Time + 1f;
			_throwGrenade = false;
			return _throwGrenade;
		}

		var handGrenade = (Weapons & WeaponHandGrenade) != 0;
		var toss = handGrenade
			? Ai.VecCheckToss( this, GetGunPosition(), target, 0.5f )
			: Ai.VecCheckThrow( this, GetGunPosition(), target, HalfLifeDll.SkillData.HgruntGrenadeSpeed, 0.5f );

		if ( !MathLib.VectorCompare( toss, Vector3.Zero ) )
		{
			_tossVelocity = toss;
			_throwGrenade = true;
			_nextGrenadeCheck = handGrenade ? Server.Globals.Time : (float)(Server.Globals.Time + 0.3);
		}
		else
		{
			_throwGrenade = false;
			_nextGrenadeCheck = Server.Globals.Time + 1f;
		}

		return _throwGrenade;
	}

	public override void TraceAttack( BaseEntity attacker, float damage, Vector3 direction, in Trace trace, int damageType )
	{
		var hit = trace;

		if ( hit.HitGroup == 11 )
		{
			if ( GetBodygroup( 1 ) == HeadGrunt && (damageType & (DamageBullet | DamageSlash | DamageBlast | DamageClub)) != 0 )
			{
				damage -= 20f;

				if ( damage <= 0f )
				{
					Util.Ricochet( hit.EndPos, 1f );
					damage = 0.01f;
				}
			}

			hit.HitGroup = HitGroupHead;
		}

		base.TraceAttack( attacker, damage, direction, hit, damageType );
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		Forget( MemoryInCover );

		return base.TakeDamage( inflictor, attacker, damage, damageType );
	}

	public override void SetYawSpeed()
	{
		YawSpeed = Activity switch
		{
			Activity.Idle or Activity.Run => 150f,
			Activity.Walk or Activity.TurnLeft or Activity.TurnRight => 180f,
			Activity.RangeAttack1 or Activity.RangeAttack2 or Activity.MeleeAttack1 or Activity.MeleeAttack2 => 120f,
			Activity.Glide or Activity.Fly => 30f,
			_ => 90f
		};
	}

	public override void IdleSound()
	{
		if ( !OkToSpeak() || (_gruntQuestion == 0 && Server.RandomLong( 0, 1 ) == 0) )
			return;

		if ( _gruntQuestion == 0 )
		{
			switch ( Server.RandomLong( 0, 2 ) )
			{
				case 0:
					Speak( "HG_CHECK" );
					_gruntQuestion = 1;
					break;
				case 1:
					Speak( "HG_QUEST" );
					_gruntQuestion = 2;
					break;
				case 2:
					Speak( "HG_IDLE" );
					break;
			}
		}
		else
		{
			switch ( _gruntQuestion )
			{
				case 1:
					Speak( "HG_CLEAR" );
					break;
				case 2:
					Speak( "HG_ANSWER" );
					break;
			}

			_gruntQuestion = 0;
		}

		JustSpoke();
	}

	public override void CheckAmmo()
	{
		if ( AmmoLoaded <= 0 )
			SetConditions( ConditionNoAmmoLoaded );
	}

	public override int Classify() => ClassHumanMilitary;

	public override void Killed( BaseEntity attacker, int gib )
	{
		if ( ShouldBecomeRagdoll( gib ) && GetBodygroup( GunGroup ) != GunNone )
			DropGun();

		base.Killed( attacker, gib );
	}

	private void DropGun()
	{
		var gunPosition = Server.GetAttachment( this, 0 );

		SetBodygroup( GunGroup, GunNone );

		DropItem( (Weapons & WeaponShotgun) != 0 ? "weapon_shotgun" : "weapon_9mmAR", gunPosition, Vector3.Zero );

		if ( (Weapons & WeaponGrenadeLauncher) != 0 )
			DropItem( "ammo_ARgrenades", BodyTarget( Origin ), Vector3.Zero );
	}

	public BaseEntity Kick()
	{
		Server.Globals.MakeVectors( Angles );

		var start = Origin;

		start.z = (float)(start.z + (Size.z * 0.5));

		var end = start + (Server.Globals.Forward * 70f);

		return Util.TraceHull( start, end, DontIgnoreMonsters, HeadHull, this ).Ent as BaseEntity;
	}

	public override Vector3 GetGunPosition() => Origin + new Vector3( 0f, 0f, _standing ? 60f : 48f );

	private void Fire( uint shots, Vector3 spread, Bullet bulletType, int tracerFrequency, int shell, int shellSound )
	{
		if ( EnemyEntity is null )
			return;

		var globals = Server.Globals;
		var shootOrigin = GetGunPosition();
		var shootDirection = ShootAtEnemy( shootOrigin );

		globals.MakeVectors( Angles );

		var shellVelocity = (globals.Right * Server.RandomFloat( 40f, 90f )) + (globals.Up * Server.RandomFloat( 75f, 200f )) + (globals.Forward * Server.RandomFloat( -40f, 40f ));

		Combat.EjectBrass( shootOrigin - (shootDirection * 24f), shellVelocity, Angles.y, shell, shellSound );
		FireBullets( shots, shootOrigin, shootDirection, spread, 2048f, bulletType, tracerFrequency );

		Effects |= Effects.MuzzleFlash;

		AmmoLoaded--;

		SetBlending( 0, MathLib.VectorAngles( shootDirection ).x );
	}

	public void Shoot() => Fire( 1, BasePlayerWeapon.Cone10Degrees, Bullet.MonsterMp5, 4, _brassShell, TempEntity.BounceShell );

	public void Shotgun()
	{
		Fire( (uint)HalfLifeDll.SkillData.HgruntShotgunPellets, BasePlayerWeapon.Cone15Degrees, Bullet.PlayerBuckshot, 0, _shotgunShell, TempEntity.BounceShotShell );
	}

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		var globals = Server.Globals;

		switch ( monsterEvent.Event )
		{
			case AnimEventDropGun:
				DropGun();
				break;

			case AnimEventReload:
				Sound.Emit( this, SoundChannel.Weapon, "hgrunt/gr_reload1.wav", 1f, Server.AttenuationNormal );
				AmmoLoaded = _clipSize;
				ClearConditions( ConditionNoAmmoLoaded );
				break;

			case AnimEventGrenadeToss:
				globals.MakeVectors( Angles );
				Grenade.ShootTimed( this, GetGunPosition(), _tossVelocity, 3.5f );

				_throwGrenade = false;
				_nextGrenadeCheck = globals.Time + 6f;
				break;

			case AnimEventGrenadeLaunch:
				Sound.Emit( this, SoundChannel.Weapon, "weapons/glauncher.wav", 0.8f, Server.AttenuationNormal );
				Grenade.ShootContact( this, GetGunPosition(), _tossVelocity );

				_throwGrenade = false;
				_nextGrenadeCheck = HalfLifeDll.SkillLevel == SkillData.Hard ? globals.Time + Server.RandomFloat( 2f, 5f ) : globals.Time + 6f;
				break;

			case AnimEventGrenadeDrop:
				globals.MakeVectors( Angles );
				Grenade.ShootTimed( this, Origin + (globals.Forward * 17f) - (globals.Right * 27f) + (globals.Up * 6f), Vector3.Zero, 3f );
				break;

			case AnimEventBurst1:
				if ( (Weapons & Weapon9mmAR) != 0 )
				{
					Shoot();

					Sound.Emit( this, SoundChannel.Weapon, Server.RandomLong( 0, 1 ) != 0 ? "hgrunt/gr_mgun1.wav" : "hgrunt/gr_mgun2.wav", 1f, Server.AttenuationNormal );
				}
				else
				{
					Shotgun();

					Sound.Emit( this, SoundChannel.Weapon, "weapons/sbarrel1.wav", 1f, Server.AttenuationNormal );
				}

				SoundEnt.InsertSound( WorldSound.Combat, Origin, 384, 0.3f );
				break;

			case AnimEventBurst2:
			case AnimEventBurst3:
				Shoot();
				break;

			case AnimEventKick:
				if ( Kick() is { } hurt )
				{
					globals.MakeVectors( Angles );

					hurt.PunchAngle = hurt.PunchAngle with { x = 15f };
					hurt.Velocity = hurt.Velocity + (globals.Forward * 100f) + (globals.Up * 50f);
					hurt.TakeDamage( this, this, HalfLifeDll.SkillData.HgruntDmgKick, DamageClub );
				}

				break;

			case AnimEventCaughtEnemy:
				if ( OkToSpeak() )
				{
					Speak( "HG_ALERT" );
					JustSpoke();
				}

				goto default;

			default:
				base.HandleAnimEvent( monsterEvent );
				break;
		}
	}

	public override void Spawn()
	{
		Precache();

		SetModel( "models/hgrunt.mdl" );
		SetSize( HumanHullMin, HumanHullMax );

		Solid = Solid.SlideBox;
		MoveType = MoveType.Step;
		MonsterBloodColor = BloodColorRed;
		Effects = 0;
		Health = HalfLifeDll.SkillData.HgruntHealth;
		FieldOfView = 0.2f;
		MonsterState = MonsterState.None;
		_nextGrenadeCheck = Server.Globals.Time + 1f;
		_nextPainTime = Server.Globals.Time;
		_sentence = SentenceNone;

		Capability = CapabilitySquad | CapabilityTurnHead | CapabilityDoorsGroup;

		EnemyEluded = false;
		_firstEncounter = true;

		HackedGunPos = new Vector3( 0f, 0f, 55f );

		if ( Weapons == 0 )
			Weapons = Weapon9mmAR | WeaponHandGrenade;

		if ( (Weapons & WeaponShotgun) != 0 )
		{
			SetBodygroup( GunGroup, GunShotgun );
			_clipSize = 8;
		}
		else
		{
			_clipSize = ClipSize;
		}

		AmmoLoaded = _clipSize;

		Skin = Server.RandomLong( 0, 99 ) < 80 ? 0 : 1;

		if ( (Weapons & WeaponShotgun) != 0 )
		{
			SetBodygroup( HeadGroup, HeadShotgun );
		}
		else if ( (Weapons & WeaponGrenadeLauncher) != 0 )
		{
			SetBodygroup( HeadGroup, HeadM203 );
			Skin = 1;
		}

		TalkMonster.TalkWaitTime = 0f;

		MonsterInit();
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/hgrunt.mdl" );

		Server.PrecacheSound( "hgrunt/gr_mgun1.wav" );
		Server.PrecacheSound( "hgrunt/gr_mgun2.wav" );

		Server.PrecacheSound( "hgrunt/gr_die1.wav" );
		Server.PrecacheSound( "hgrunt/gr_die2.wav" );
		Server.PrecacheSound( "hgrunt/gr_die3.wav" );

		Server.PrecacheSound( "hgrunt/gr_pain1.wav" );
		Server.PrecacheSound( "hgrunt/gr_pain2.wav" );
		Server.PrecacheSound( "hgrunt/gr_pain3.wav" );
		Server.PrecacheSound( "hgrunt/gr_pain4.wav" );
		Server.PrecacheSound( "hgrunt/gr_pain5.wav" );

		Server.PrecacheSound( "hgrunt/gr_reload1.wav" );

		Server.PrecacheSound( "weapons/glauncher.wav" );

		Server.PrecacheSound( "weapons/sbarrel1.wav" );

		Server.PrecacheSound( "zombie/claw_miss2.wav" );

		_voicePitch = Server.RandomLong( 0, 1 ) != 0 ? 109 + Server.RandomLong( 0, 7 ) : 100;

		_brassShell = Server.PrecacheModel( "models/shell.mdl" );
		_shotgunShell = Server.PrecacheModel( "models/shotgunshell.mdl" );
	}

	public override void StartTask( Task task )
	{
		TaskStatus = TaskState.Running;

		switch ( task.Id )
		{
			case GruntTask.CheckFire:
				if ( !NoFriendlyFire() )
					SetConditions( ConditionNoFire );

				TaskComplete();
				break;

			case GruntTask.SpeakSentence:
				SpeakSentence();
				TaskComplete();
				break;

			case TaskId.WalkPath:
			case TaskId.RunPath:
				Forget( MemoryInCover );
				base.StartTask( task );
				break;

			case TaskId.Reload:
				IdealActivity = Activity.Reload;
				break;

			case GruntTask.FaceTossDir:
				break;

			case TaskId.FaceIdeal:
			case TaskId.FaceEnemy:
				base.StartTask( task );

				if ( MoveType == MoveType.Fly )
					IdealActivity = Activity.Glide;

				break;

			default:
				base.StartTask( task );
				break;
		}
	}

	public override void RunTask( Task task )
	{
		if ( task.Id != GruntTask.FaceTossDir )
		{
			base.RunTask( task );
			return;
		}

		MakeIdealYaw( Origin + (_tossVelocity * 64f) );
		ChangeYaw( (int)YawSpeed );

		if ( FacingIdeal() )
			TaskStatus = TaskState.Complete;
	}

	public override void PainSound()
	{
		if ( Server.Globals.Time <= _nextPainTime )
			return;

		switch ( Server.RandomLong( 0, 6 ) )
		{
			case 0:
				Sound.Emit( this, SoundChannel.Voice, "hgrunt/gr_pain3.wav", 1f, Server.AttenuationNormal );
				break;
			case 1:
				Sound.Emit( this, SoundChannel.Voice, "hgrunt/gr_pain4.wav", 1f, Server.AttenuationNormal );
				break;
			case 2:
				Sound.Emit( this, SoundChannel.Voice, "hgrunt/gr_pain5.wav", 1f, Server.AttenuationNormal );
				break;
			case 3:
				Sound.Emit( this, SoundChannel.Voice, "hgrunt/gr_pain1.wav", 1f, Server.AttenuationNormal );
				break;
			case 4:
				Sound.Emit( this, SoundChannel.Voice, "hgrunt/gr_pain2.wav", 1f, Server.AttenuationNormal );
				break;
		}

		_nextPainTime = Server.Globals.Time + 1f;
	}

	public override void DeathSound()
	{
		switch ( Server.RandomLong( 0, 2 ) )
		{
			case 0:
				Sound.Emit( this, SoundChannel.Voice, "hgrunt/gr_die1.wav", 1f, Server.AttenuationIdle );
				break;
			case 1:
				Sound.Emit( this, SoundChannel.Voice, "hgrunt/gr_die2.wav", 1f, Server.AttenuationIdle );
				break;
			case 2:
				Sound.Emit( this, SoundChannel.Voice, "hgrunt/gr_die3.wav", 1f, Server.AttenuationIdle );
				break;
		}
	}

	public override void SetActivity( Activity activity )
	{
		int sequence;

		switch ( activity )
		{
			case Activity.RangeAttack1:
				if ( (Weapons & Weapon9mmAR) != 0 )
					sequence = LookupSequence( _standing ? "standing_mp5" : "crouching_mp5" );
				else
					sequence = LookupSequence( _standing ? "standing_shotgun" : "crouching_shotgun" );

				break;

			case Activity.RangeAttack2:
				sequence = LookupSequence( (Weapons & WeaponHandGrenade) != 0 ? "throwgrenade" : "launchgrenade" );
				break;

			case Activity.Run:
				sequence = LookupActivity( (int)(Health <= LimpHealth ? Activity.RunHurt : activity) );
				break;

			case Activity.Walk:
				sequence = LookupActivity( (int)(Health <= LimpHealth ? Activity.WalkHurt : activity) );
				break;

			case Activity.Idle:
				if ( MonsterState == MonsterState.Combat )
					activity = Activity.IdleAngry;

				sequence = LookupActivity( (int)activity );
				break;

			default:
				sequence = LookupActivity( (int)activity );
				break;
		}

		Activity = activity;

		if ( sequence > Animation.ActivityNotAvailable )
		{
			if ( Sequence != sequence || !SequenceLoops )
				Frame = 0f;

			Sequence = sequence;
			ResetSequenceInfo();
			SetYawSpeed();
		}
		else
		{
			Util.Alert( AlertType.Console, $"{ClassName} has no sequence for act:{(int)activity}\n" );
			Sequence = 0;
		}
	}

	private Schedule GetCombatSchedule()
	{
		if ( HasConditions( ConditionEnemyDead ) )
			return GetBaseSchedule();

		if ( HasConditions( ConditionNewEnemy ) )
		{
			if ( InSquad )
			{
				MySquadLeader().EnemyEluded = false;

				if ( !IsLeader )
					return GetScheduleOfType( ScheduleType.TakeCoverFromEnemy );

				if ( OkToSpeak() )
				{
					if ( EnemyEntity is { } enemy )
					{
						if ( enemy.IsPlayer )
							Speak( "HG_ALERT" );
						else if ( enemy.Classify() != ClassPlayerAlly && enemy.Classify() != ClassHumanPassive && enemy.Classify() != ClassMachine )
							Speak( "HG_MONST" );
					}

					JustSpoke();
				}

				return GetScheduleOfType( HasConditions( ConditionCanRangeAttack1 ) ? GruntSchedule.Suppress : GruntSchedule.EstablishLineOfFire );
			}
		}
		else if ( HasConditions( ConditionNoAmmoLoaded ) )
		{
			return GetScheduleOfType( GruntSchedule.CoverAndReload );
		}
		else if ( HasConditions( ConditionLightDamage ) )
		{
			var percent = Server.RandomLong( 0, 99 );

			if ( percent > 90 || EnemyEntity is null )
				return GetScheduleOfType( ScheduleType.SmallFlinch );

			if ( OkToSpeak() )
				_sentence = SentenceCover;

			return GetScheduleOfType( ScheduleType.TakeCoverFromEnemy );
		}
		else if ( HasConditions( ConditionCanMeleeAttack1 ) )
		{
			return GetScheduleOfType( ScheduleType.MeleeAttack1 );
		}
		else if ( (Weapons & WeaponGrenadeLauncher) != 0 && HasConditions( ConditionCanRangeAttack2 ) && OccupySlot( SquadSlot.HumanGruntGrenade ) )
		{
			return GetScheduleOfType( ScheduleType.RangeAttack2 );
		}
		else if ( HasConditions( ConditionCanRangeAttack1 ) )
		{
			if ( InSquad && MySquadLeader().EnemyEluded && !HasConditions( ConditionEnemyFacingMe ) )
			{
				MySquadLeader().EnemyEluded = false;
				return GetScheduleOfType( GruntSchedule.FoundEnemy );
			}

			if ( OccupySlot( SquadSlot.HumanGruntEngage ) )
				return GetScheduleOfType( ScheduleType.RangeAttack1 );

			if ( HasConditions( ConditionCanRangeAttack2 ) && OccupySlot( SquadSlot.HumanGruntGrenade ) )
				return GetScheduleOfType( ScheduleType.RangeAttack2 );

			return GetScheduleOfType( ScheduleType.TakeCoverFromEnemy );
		}
		else if ( HasConditions( ConditionEnemyOccluded ) )
		{
			if ( HasConditions( ConditionCanRangeAttack2 ) && OccupySlot( SquadSlot.HumanGruntGrenade ) )
			{
				if ( OkToSpeak() )
				{
					Speak( "HG_THROW" );
					JustSpoke();
				}

				return GetScheduleOfType( ScheduleType.RangeAttack2 );
			}

			if ( OccupySlot( SquadSlot.HumanGruntEngage ) )
			{
				if ( OkToSpeak() )
					_sentence = SentenceCharge;

				return GetScheduleOfType( GruntSchedule.EstablishLineOfFire );
			}

			if ( OkToSpeak() && Server.RandomLong( 0, 1 ) != 0 )
			{
				Speak( "HG_TAUNT" );
				JustSpoke();
			}

			return GetScheduleOfType( ScheduleType.Standoff );
		}

		if ( HasConditions( ConditionSeeEnemy ) && !HasConditions( ConditionCanRangeAttack1 ) )
			return GetScheduleOfType( GruntSchedule.EstablishLineOfFire );

		return base.GetSchedule();
	}

	public override Schedule GetSchedule()
	{
		_sentence = SentenceNone;

		if ( MoveType == MoveType.Fly && MonsterState != MonsterState.Prone )
		{
			if ( (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround )
			{
				MoveType = MoveType.Step;
				return GetScheduleOfType( GruntSchedule.RepelLand );
			}

			return GetScheduleOfType( MonsterState == MonsterState.Combat ? GruntSchedule.RepelAttack : GruntSchedule.Repel );
		}

		if ( HasConditions( ConditionHearSound ) && BestSound() is { } sound && (sound.Type & WorldSound.Danger) != 0 )
		{
			if ( OkToSpeak() )
			{
				Speak( "HG_GREN" );
				JustSpoke();
			}

			return GetScheduleOfType( ScheduleType.TakeCoverFromBestSound );
		}

		return MonsterState == MonsterState.Combat ? GetCombatSchedule() : base.GetSchedule();
	}

	private void SlowRepel()
	{
		if ( Velocity.z > -128f )
			Velocity = Velocity with { z = Velocity.z - 32f };
	}

	public override Schedule GetScheduleOfType( int type )
	{
		switch ( type )
		{
			case ScheduleType.TakeCoverFromEnemy:
				if ( !InSquad )
					return Server.RandomLong( 0, 1 ) != 0 ? HGruntAi.GruntTakeCover : HGruntAi.GruntGrenadeCover;

				if ( HalfLifeDll.SkillLevel != SkillData.Hard || !HasConditions( ConditionCanRangeAttack2 ) || !OccupySlot( SquadSlot.HumanGruntGrenade ) )
					return HGruntAi.GruntTakeCover;

				if ( OkToSpeak() )
				{
					Speak( "HG_THROW" );
					JustSpoke();
				}

				return HGruntAi.GruntTossGrenadeCover;

			case ScheduleType.TakeCoverFromBestSound:
				return HGruntAi.GruntTakeCoverFromBestSound;

			case GruntSchedule.TakeCoverFailed:
				if ( HasConditions( ConditionCanRangeAttack1 ) && OccupySlot( SquadSlot.HumanGruntEngage ) )
					return GetScheduleOfType( ScheduleType.RangeAttack1 );

				return GetScheduleOfType( ScheduleType.Fail );

			case GruntSchedule.ElofFail:
				return GetScheduleOfType( ScheduleType.TakeCoverFromEnemy );

			case GruntSchedule.EstablishLineOfFire:
				return HGruntAi.GruntEstablishLineOfFire;

			case ScheduleType.RangeAttack1:
				if ( Server.RandomLong( 0, 9 ) == 0 )
					_standing = Server.RandomLong( 0, 1 ) != 0;

				return _standing ? HGruntAi.GruntRangeAttack1B : HGruntAi.GruntRangeAttack1A;

			case ScheduleType.RangeAttack2:
				return HGruntAi.GruntRangeAttack2;

			case ScheduleType.CombatFace:
				return HGruntAi.GruntCombatFace;

			case GruntSchedule.WaitFaceEnemy:
				return HGruntAi.GruntWaitInCover;

			case GruntSchedule.Sweep:
				return HGruntAi.GruntSweep;

			case GruntSchedule.CoverAndReload:
				return HGruntAi.GruntHideReload;

			case GruntSchedule.FoundEnemy:
				return HGruntAi.GruntFoundEnemy;

			case ScheduleType.VictoryDance:
				return InSquad && !IsLeader ? HGruntAi.GruntFail : HGruntAi.GruntVictoryDance;

			case GruntSchedule.Suppress:
				if ( !EnemyEntity.IsPlayer || !_firstEncounter )
					return HGruntAi.GruntSuppress;

				_firstEncounter = false;
				return HGruntAi.GruntSignalSuppress;

			case ScheduleType.Fail:
				return EnemyEntity is not null ? HGruntAi.GruntCombatFail : HGruntAi.GruntFail;

			case GruntSchedule.Repel:
				SlowRepel();
				return HGruntAi.GruntRepel;

			case GruntSchedule.RepelAttack:
				SlowRepel();
				return HGruntAi.GruntRepelAttack;

			case GruntSchedule.RepelLand:
				return HGruntAi.GruntRepelLand;

			default:
				return base.GetScheduleOfType( type );
		}
	}
}

[LinkEntityToClass( "monster_grunt_repel" )]
public class HGruntRepel : BaseMonster
{
	private int _spriteTexture;

	public override void Spawn()
	{
		Precache();

		Solid = Solid.Not;

		SetUse( RepelUse );
	}

	public override void Precache()
	{
		Util.PrecacheOther( "monster_human_grunt" );

		_spriteTexture = Server.PrecacheModel( "sprites/rope.spr" );
	}

	public void RepelUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		var trace = Util.TraceLine( Origin, Origin + new Vector3( 0f, 0f, -4096f ), DontIgnoreMonsters, this );
		var grunt = (BaseMonster)Create( "monster_human_grunt", Origin, Angles, null );

		grunt.MoveType = MoveType.Fly;
		grunt.Velocity = new Vector3( 0f, 0f, Server.RandomFloat( -196f, -128f ) );
		grunt.SetActivity( Activity.Glide );
		grunt.LastPosition = trace.EndPos;

		var beam = Beam.BeamCreate( Server, "sprites/rope.spr", 10 );

		beam.PointEntInit( Origin + new Vector3( 0f, 0f, 112f ), grunt.Index );
		beam.SetBeamFlags( (int)BeamFlags.Solid );
		beam.SetColor( 255, 255, 255 );
		beam.SetThink( beam.SubRemove );
		beam.NextThink = (float)(Server.Globals.Time + (-4096.0 * trace.Fraction / grunt.Velocity.z) + 0.5);

		Remove();
	}
}

[LinkEntityToClass( "monster_hgrunt_dead" )]
public class DeadHGrunt : BaseMonster
{
	private static readonly string[] Poses = ["deadstomach", "deadside", "deadsitting"];

	private int _pose;

	public override int Classify() => ClassHumanMilitary;

	public override bool KeyValue( string key, string value )
	{
		if ( key != "pose" )
			return base.KeyValue( key, value );

		_pose = Util.Atoi( value );
		return true;
	}

	public override void Spawn()
	{
		Server.PrecacheModel( "models/hgrunt.mdl" );
		SetModel( "models/hgrunt.mdl" );

		Effects = 0;
		YawSpeed = 8f;
		Sequence = 0;
		MonsterBloodColor = BloodColorRed;

		Sequence = LookupSequence( Poses[_pose] );

		if ( Sequence == -1 )
			Util.Alert( AlertType.Console, "Dead hgrunt with bad pose\n" );

		Health = 8f;

		var (head, gun) = Body switch
		{
			0 => (HGrunt.HeadGrunt, HGrunt.GunMp5),
			1 => (HGrunt.HeadCommander, HGrunt.GunMp5),
			2 => (HGrunt.HeadGrunt, HGrunt.GunNone),
			3 => (HGrunt.HeadCommander, HGrunt.GunNone),
			_ => (-1, -1)
		};

		if ( head != -1 )
		{
			Body = 0;
			Skin = 0;
			SetBodygroup( HGrunt.HeadGroup, head );
			SetBodygroup( HGrunt.GunGroup, gun );
		}

		MonsterInitDead();
	}
}
