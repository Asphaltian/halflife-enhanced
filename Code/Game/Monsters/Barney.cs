using GoldSrc;

namespace HalfLife;

[SkipHotload]
public static class BarneyAi
{
	public static readonly Schedule BaFollow = new( "Follow",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionHearSound | BaseMonster.ConditionProvoked,
		WorldSound.Danger,
		new( TaskId.MoveToTargetRange, 128f ),
		new( TaskId.SetSchedule, ScheduleType.TargetFace ) );

	public static readonly Schedule BarneyEnemyDraw = new( "Barney Enemy Draw",
		0,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.FaceEnemy ),
		new( TaskId.PlaySequenceFaceEnemy, (float)Activity.Arm ) );

	public static readonly Schedule BaFaceTarget = new( "FaceTarget",
		TalkMonster.ConditionClientPush | BaseMonster.ConditionNewEnemy | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionHearSound | BaseMonster.ConditionProvoked,
		WorldSound.Danger,
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.FaceTarget ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.SetSchedule, ScheduleType.TargetChase ) );

	public static readonly Schedule IdleBaStand = new( "IdleStand",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionHearSound | BaseMonster.ConditionSmell | BaseMonster.ConditionProvoked,
		WorldSound.Combat | WorldSound.Danger | WorldSound.Meat | WorldSound.Carcass | WorldSound.Garbage,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.Wait, 2f ),
		new( TalkTask.HeadReset ) );

	public static readonly Schedule[] List =
	[
		BaFollow,
		BarneyEnemyDraw,
		BaFaceTarget,
		IdleBaStand
	];
}

[LinkEntityToClass( "monster_barney" )]
public class Barney : TalkMonster
{
	private const int AnimEventDraw = 2;
	private const int AnimEventShoot = 3;
	private const int AnimEventHolster = 4;

	private const int BodyGunHolstered = 0;
	private const int BodyGunDrawn = 1;
	private const int BodyGunGone = 2;

	[Save( FieldType.Boolean )]
	private bool _gunDrawn;
	[Save( FieldType.Time )]
	private float _painTime;
	[Save( FieldType.Time )]
	private float _checkAttackTime;
	[Save( FieldType.Boolean )]
	private bool _lastAttackCheck;
	[Save( FieldType.Float )]
	private float _playerDamage;

	public override ObjectCaps ObjectCaps => base.ObjectCaps | ObjectCaps.ImpulseUse;

	public override Schedule ScheduleFromName( string name ) => ScheduleInList( name, BarneyAi.List ) ?? base.ScheduleFromName( name );

	public override void RunTask( Task task )
	{
		if ( task.Id == TaskId.RangeAttack1 && EnemyEntity is { IsPlayer: true } )
			FrameRate = 1.5f;

		base.RunTask( task );
	}

	public override int SoundMask()
	{
		return WorldSound.World | WorldSound.Combat | WorldSound.Carcass | WorldSound.Meat | WorldSound.Garbage | WorldSound.Danger | WorldSound.Player;
	}

	public override int Classify() => ClassPlayerAlly;

	public override void AlertSound()
	{
		if ( EnemyEntity is not null && OkToSpeak() )
			PlaySentence( "BA_ATTACK", Server.RandomFloat( 2.8f, 3.2f ), Server.VolumeNormal, Server.AttenuationIdle );
	}

	public override void SetYawSpeed() => YawSpeed = Activity == Activity.Run ? 90f : 70f;

	public override bool CheckRangeAttack1( float dot, float distance )
	{
		if ( distance > 1024f || dot < 0.5 )
			return false;

		if ( Server.Globals.Time > _checkAttackTime )
		{
			var shootOrigin = Origin + new Vector3( 0f, 0f, 55f );
			var enemy = EnemyEntity;
			var shootTarget = enemy.BodyTarget( shootOrigin ) - enemy.Origin + EnemyLastKnownPosition;
			var trace = Util.TraceLine( shootOrigin, shootTarget, DontIgnoreMonsters, this );

			_lastAttackCheck = trace.Fraction == 1f || (trace.Ent is not null && trace.Ent == enemy);
			_checkAttackTime = (float)(Server.Globals.Time + 1.5);
		}

		return _lastAttackCheck;
	}

	public void BarneyFirePistol()
	{
		Server.Globals.MakeVectors( Angles );

		var shootOrigin = Origin + new Vector3( 0f, 0f, 55f );
		var shootDirection = ShootAtEnemy( shootOrigin );

		SetBlending( 0, MathLib.VectorAngles( shootDirection ).x );
		Effects = Effects.MuzzleFlash;

		FireBullets( 1, shootOrigin, shootDirection, BasePlayerWeapon.Cone2Degrees, 1024f, Bullet.Monster9mm );

		var pitchShift = Server.RandomLong( 0, 20 );

		if ( pitchShift > 10 )
			pitchShift = 0;
		else
			pitchShift -= 5;

		Sound.EmitDyn( this, SoundChannel.Weapon, "barney/ba_attack2.wav", 1f, Server.AttenuationNormal, 0, 100 + pitchShift );

		SoundEnt.InsertSound( WorldSound.Combat, Origin, 384, 0.3f );

		AmmoLoaded--;
	}

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		switch ( monsterEvent.Event )
		{
			case AnimEventShoot:
				BarneyFirePistol();
				break;

			case AnimEventDraw:
				Body = BodyGunDrawn;
				_gunDrawn = true;
				break;

			case AnimEventHolster:
				Body = BodyGunHolstered;
				_gunDrawn = false;
				break;

			default:
				base.HandleAnimEvent( monsterEvent );
				break;
		}
	}

	public override void Spawn()
	{
		Precache();

		SetModel( "models/barney.mdl" );
		SetSize( HumanHullMin, HumanHullMax );

		Solid = Solid.SlideBox;
		MoveType = MoveType.Step;
		MonsterBloodColor = BloodColorRed;
		Health = HalfLifeDll.SkillData.BarneyHealth;
		ViewOffset = new Vector3( 0f, 0f, 50f );
		FieldOfView = ViewFieldWide;
		MonsterState = MonsterState.None;

		Body = 0;
		_gunDrawn = false;

		Capability = CapabilityHear | CapabilityTurnHead | CapabilityDoorsGroup;

		MonsterInit();
		SetUse( FollowerUse );
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/barney.mdl" );

		Server.PrecacheSound( "barney/ba_attack1.wav" );
		Server.PrecacheSound( "barney/ba_attack2.wav" );

		Server.PrecacheSound( "barney/ba_pain1.wav" );
		Server.PrecacheSound( "barney/ba_pain2.wav" );
		Server.PrecacheSound( "barney/ba_pain3.wav" );

		Server.PrecacheSound( "barney/ba_die1.wav" );
		Server.PrecacheSound( "barney/ba_die2.wav" );
		Server.PrecacheSound( "barney/ba_die3.wav" );

		TalkInit();

		base.Precache();
	}

	public new void TalkInit()
	{
		base.TalkInit();

		Groups[TalkGroup.Answer] = "BA_ANSWER";
		Groups[TalkGroup.Question] = "BA_QUESTION";
		Groups[TalkGroup.Idle] = "BA_IDLE";
		Groups[TalkGroup.Stare] = "BA_STARE";
		Groups[TalkGroup.Use] = "BA_OK";
		Groups[TalkGroup.UnUse] = "BA_WAIT";
		Groups[TalkGroup.Stop] = "BA_STOP";

		Groups[TalkGroup.NoShoot] = "BA_SCARED";
		Groups[TalkGroup.Hello] = "BA_HELLO";

		Groups[TalkGroup.PlayerHurt1] = "!BA_CUREA";
		Groups[TalkGroup.PlayerHurt2] = "!BA_CUREB";
		Groups[TalkGroup.PlayerHurt3] = "!BA_CUREC";

		Groups[TalkGroup.PreHello] = null;
		Groups[TalkGroup.PreIdle] = null;
		Groups[TalkGroup.PreQuestion] = "BA_PQUEST";

		Groups[TalkGroup.Smell] = "BA_SMELL";

		Groups[TalkGroup.Wound] = "BA_WOUND";
		Groups[TalkGroup.Mortal] = "BA_MORTAL";

		VoicePitch = 100;
	}

	private static bool IsFacing( BaseEntity test, Vector3 reference )
	{
		var direction = Util.Normalize( (reference - test.Origin) with { z = 0f } );

		MathLib.AngleVectors( test.ViewAngle with { x = 0f }, out var forward, out _, out _ );

		return Vector3.Dot( forward, direction ) > 0.96;
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		var result = base.TakeDamage( inflictor, attacker, damage, damageType );

		if ( !IsAlive || DeadFlag == DeadDying )
			return result;

		if ( MonsterState == MonsterState.Prone || (attacker.EntityFlags & EntityFlags.Client) != EntityFlags.Client )
			return result;

		_playerDamage += damage;

		if ( EnemyEntity is not { } enemy )
		{
			if ( HasMemory( MemorySuspicious ) || IsFacing( attacker, Origin ) )
			{
				PlaySentence( "BA_MAD", 4f, Server.VolumeNormal, Server.AttenuationNormal );

				Remember( MemoryProvoked );
				StopFollowing( true );
			}
			else
			{
				PlaySentence( "BA_SHOT", 4f, Server.VolumeNormal, Server.AttenuationNormal );
				Remember( MemorySuspicious );
			}
		}
		else if ( !enemy.IsPlayer && DeadFlag == DeadNo )
		{
			PlaySentence( "BA_SHOT", 4f, Server.VolumeNormal, Server.AttenuationNormal );
		}

		return result;
	}

	public override void PainSound()
	{
		if ( Server.Globals.Time < _painTime )
			return;

		_painTime = Server.Globals.Time + Server.RandomFloat( 0.5f, 0.75f );

		Sound.EmitDyn( this, SoundChannel.Voice, $"barney/ba_pain{Server.RandomLong( 0, 2 ) + 1}.wav", 1f, Server.AttenuationNormal, 0, GetVoicePitch() );
	}

	public override void DeathSound()
	{
		Sound.EmitDyn( this, SoundChannel.Voice, $"barney/ba_die{Server.RandomLong( 0, 2 ) + 1}.wav", 1f, Server.AttenuationNormal, 0, GetVoicePitch() );
	}

	public override void TraceAttack( BaseEntity attacker, float damage, Vector3 direction, in Trace trace, int damageType )
	{
		var hit = trace;

		switch ( hit.HitGroup )
		{
			case HitGroupChest:
			case HitGroupStomach:
				if ( (damageType & (DamageBullet | DamageSlash | DamageBlast)) != 0 )
					damage /= 2f;

				break;

			case 10:
				if ( (damageType & (DamageBullet | DamageSlash | DamageClub)) != 0 )
				{
					damage -= 20f;

					if ( damage <= 0f )
					{
						Util.Ricochet( hit.EndPos, 1f );
						damage = 0.01f;
					}
				}

				hit.HitGroup = HitGroupHead;
				break;
		}

		base.TraceAttack( attacker, damage, direction, hit, damageType );
	}

	public override void Killed( BaseEntity attacker, int gib )
	{
		if ( Body < BodyGunGone )
		{
			Body = BodyGunGone;

			DropItem( "weapon_9mmhandgun", Server.GetAttachment( this, 0 ), Vector3.Zero );
		}

		SetUse( null );

		base.Killed( attacker, gib );
	}

	public override Schedule GetScheduleOfType( int type )
	{
		Schedule schedule;

		switch ( type )
		{
			case ScheduleType.ArmWeapon:
				if ( EnemyEntity is not null )
					return BarneyAi.BarneyEnemyDraw;

				break;

			case ScheduleType.TargetFace:
				schedule = base.GetScheduleOfType( type );

				return schedule == DefaultAi.IdleStand ? BarneyAi.BaFaceTarget : schedule;

			case ScheduleType.TargetChase:
				return BarneyAi.BaFollow;

			case ScheduleType.IdleStand:
				schedule = base.GetScheduleOfType( type );

				return schedule == DefaultAi.IdleStand ? BarneyAi.IdleBaStand : schedule;
		}

		return base.GetScheduleOfType( type );
	}

	public override Schedule GetSchedule()
	{
		if ( HasConditions( ConditionHearSound ) && BestSound() is { } sound && (sound.Type & WorldSound.Danger) != 0 )
			return GetScheduleOfType( ScheduleType.TakeCoverFromBestSound );

		if ( HasConditions( ConditionEnemyDead ) && OkToSpeak() )
			PlaySentence( "BA_KILL", 4f, Server.VolumeNormal, Server.AttenuationNormal );

		switch ( MonsterState )
		{
			case MonsterState.Combat:
				if ( HasConditions( ConditionEnemyDead ) )
					return GetBaseSchedule();

				if ( HasConditions( ConditionNewEnemy ) && HasConditions( ConditionLightDamage ) )
					return GetScheduleOfType( ScheduleType.SmallFlinch );

				if ( !_gunDrawn )
					return GetScheduleOfType( ScheduleType.ArmWeapon );

				if ( HasConditions( ConditionHeavyDamage ) )
					return GetScheduleOfType( ScheduleType.TakeCoverFromEnemy );

				break;

			case MonsterState.Alert:
			case MonsterState.Idle:
				if ( HasConditions( ConditionLightDamage | ConditionHeavyDamage ) )
					return GetScheduleOfType( ScheduleType.SmallFlinch );

				if ( EnemyEntity is null && IsFollowing )
				{
					if ( !TargetEntity.IsAlive )
					{
						StopFollowing( false );
						break;
					}

					return GetScheduleOfType( HasConditions( ConditionClientPush ) ? TalkSchedule.MoveAwayFollow : ScheduleType.TargetFace );
				}

				if ( HasConditions( ConditionClientPush ) )
					return GetScheduleOfType( TalkSchedule.MoveAway );

				TrySmellTalk();
				break;
		}

		return base.GetSchedule();
	}

	public override void DeclineFollowing()
	{
		PlaySentence( "BA_POK", 2f, Server.VolumeNormal, Server.AttenuationNormal );
	}
}

[LinkEntityToClass( "monster_barney_dead" )]
public class DeadBarney : BaseMonster
{
	private static readonly string[] Poses = ["lying_on_back", "lying_on_side", "lying_on_stomach"];

	private int _pose;

	public override int Classify() => ClassPlayerAlly;

	public override bool KeyValue( string key, string value )
	{
		if ( key != "pose" )
			return base.KeyValue( key, value );

		_pose = Util.Atoi( value );

		return true;
	}

	public override void Spawn()
	{
		Server.PrecacheModel( "models/barney.mdl" );
		SetModel( "models/barney.mdl" );

		Effects = 0;
		YawSpeed = 8f;
		Sequence = 0;
		MonsterBloodColor = BloodColorRed;

		Sequence = LookupSequence( Poses[_pose] );

		if ( Sequence == -1 )
			Util.Alert( AlertType.Console, "Dead barney with bad pose" );

		Health = 8f;

		MonsterInitDead();
	}
}
