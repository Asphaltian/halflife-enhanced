using GoldSrc;

namespace HalfLife;

public static class ScientistSchedule
{
	public const int Hide = TalkSchedule.LastTalkMonster + 1;
	public const int Fear = Hide + 1;
	public const int Panic = Hide + 2;
	public const int Startle = Hide + 3;
	public const int TargetChaseScared = Hide + 4;
	public const int TargetFaceScared = Hide + 5;
}

public static class ScientistTask
{
	public const int SayHeal = TalkTask.LastTalkMonster + 1;
	public const int Heal = SayHeal + 1;
	public const int SayFear = SayHeal + 2;
	public const int RunPathScared = SayHeal + 3;
	public const int Scream = SayHeal + 4;
	public const int RandomScream = SayHeal + 5;
	public const int MoveToTargetRangeScared = SayHeal + 6;
}

[SkipHotload]
public static class ScientistAi
{
	public static readonly Schedule Follow = new( "Follow",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionHearSound,
		WorldSound.Combat | WorldSound.Danger,
		new( TaskId.SetFailSchedule, TalkSchedule.CantFollow ),
		new( TaskId.MoveToTargetRange, 128f ) );

	public static readonly Schedule FollowScared = new( "FollowScared",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionHearSound | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage,
		WorldSound.Danger,
		new( TaskId.SetFailSchedule, ScheduleType.TargetChase ),
		new( ScientistTask.MoveToTargetRangeScared, 128f ) );

	public static readonly Schedule FaceTargetScared = new( "FaceTargetScared",
		BaseMonster.ConditionHearSound | BaseMonster.ConditionNewEnemy,
		WorldSound.Danger,
		new( TaskId.FaceTarget ),
		new( TaskId.SetActivity, (float)Activity.CrouchIdle ),
		new( TaskId.SetSchedule, ScientistSchedule.TargetChaseScared ) );

	public static readonly Schedule StopFollowing = new( "StopFollowing",
		0,
		0,
		new Task( TalkTask.CantFollow ) );

	public static readonly Schedule Heal = new( "Heal",
		0,
		0,
		new( TaskId.MoveToTargetRange, 50f ),
		new( TaskId.SetFailSchedule, ScheduleType.TargetChase ),
		new( TaskId.FaceIdeal ),
		new( ScientistTask.SayHeal ),
		new( TaskId.PlaySequenceFaceTarget, (float)Activity.Arm ),
		new( ScientistTask.Heal ),
		new( TaskId.PlaySequenceFaceTarget, (float)Activity.Disarm ) );

	public static readonly Schedule FaceTarget = new( "FaceTarget",
		TalkMonster.ConditionClientPush | BaseMonster.ConditionNewEnemy | BaseMonster.ConditionHearSound,
		WorldSound.Combat | WorldSound.Danger,
		new( TaskId.StopMoving ),
		new( TaskId.FaceTarget ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.SetSchedule, ScheduleType.TargetChase ) );

	public static readonly Schedule SciPanic = new( "SciPanic",
		0,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.FaceEnemy ),
		new( ScientistTask.Scream ),
		new( TaskId.PlaySequenceFaceEnemy, (float)Activity.Excited ),
		new( TaskId.SetActivity, (float)Activity.Idle ) );

	public static readonly Schedule IdleSciStand = new( "IdleSciStand",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionHearSound | BaseMonster.ConditionSmell | TalkMonster.ConditionClientPush | BaseMonster.ConditionProvoked,
		WorldSound.Combat | WorldSound.Danger | WorldSound.Meat | WorldSound.Carcass | WorldSound.Garbage,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.Wait, 2f ),
		new( TalkTask.HeadReset ) );

	public static readonly Schedule ScientistCover = new( "ScientistCover",
		BaseMonster.ConditionNewEnemy,
		0,
		new( TaskId.SetFailSchedule, ScientistSchedule.Panic ),
		new( TaskId.StopMoving ),
		new( TaskId.FindCoverFromEnemy ),
		new( ScientistTask.RunPathScared ),
		new( TaskId.TurnLeft, 179f ),
		new( TaskId.SetSchedule, ScientistSchedule.Hide ) );

	public static readonly Schedule ScientistHide = new( "ScientistHide",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionHearSound | BaseMonster.ConditionSeeEnemy | BaseMonster.ConditionSeeHate | BaseMonster.ConditionSeeFear | BaseMonster.ConditionSeeDislike,
		WorldSound.Danger,
		new( TaskId.SetFailSchedule, ScientistSchedule.Panic ),
		new( TaskId.StopMoving ),
		new( TaskId.PlaySequence, (float)Activity.Crouch ),
		new( TaskId.SetActivity, (float)Activity.CrouchIdle ),
		new( TaskId.WaitRandom, 10.0f ) );

	public static readonly Schedule ScientistStartle = new( "ScientistStartle",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionSeeEnemy | BaseMonster.ConditionSeeHate | BaseMonster.ConditionSeeFear | BaseMonster.ConditionSeeDislike,
		0,
		new( TaskId.SetFailSchedule, ScientistSchedule.Panic ),
		new( ScientistTask.RandomScream, 0.3f ),
		new( TaskId.StopMoving ),
		new( TaskId.PlaySequenceFaceEnemy, (float)Activity.Crouch ),
		new( ScientistTask.RandomScream, 0.1f ),
		new( TaskId.PlaySequenceFaceEnemy, (float)Activity.CrouchIdle ),
		new( TaskId.WaitRandom, 1.0f ) );

	public static readonly Schedule Fear = new( "Fear",
		BaseMonster.ConditionNewEnemy,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.FaceEnemy ),
		new( ScientistTask.SayFear ) );

	public static readonly Schedule[] List =
	[
		Follow,
		FaceTarget,
		IdleSciStand,
		Fear,
		ScientistCover,
		ScientistHide,
		ScientistStartle,
		Heal,
		StopFollowing,
		SciPanic,
		FollowScared,
		FaceTargetScared
	];
}

[LinkEntityToClass( "monster_scientist" )]
public class Scientist : TalkMonster
{
	public const int HeadCount = 4;
	public const int HeadGlasses = 0;
	public const int HeadEinstein = 1;
	public const int HeadLuther = 2;
	public const int HeadSlick = 3;

	private const int AnimEventHeal = 1;
	private const int AnimEventNeedleOn = 2;
	private const int AnimEventNeedleOff = 3;

	private static readonly int[] FriendOrder = [1, 2, 0];

	[Save( FieldType.Time )]
	private float _painTime;
	[Save( FieldType.Time )]
	private float _healTime;
	[Save( FieldType.Time )]
	private float _fearTime;

	public override ObjectCaps ObjectCaps => base.ObjectCaps | ObjectCaps.ImpulseUse;

	public override float CoverRadius => 1200f;

	public override Schedule ScheduleFromName( string name ) => ScheduleInList( name, ScientistAi.List ) ?? base.ScheduleFromName( name );

	public static string ScientistModel
	{
		get
		{
			var modelOverride = Cvar.GetString( "_sv_override_scientist_mdl" );

			return modelOverride.Length > 5 ? modelOverride : "models/scientist.mdl";
		}
	}

	private bool DisregardEnemy( BaseEntity enemy ) => !enemy.IsAlive || Server.Globals.Time - _fearTime > 15.0;

	public override void DeclineFollowing()
	{
		Talk( 10f );
		TalkTarget = EnemyEntity;
		PlaySentence( "SC_POK", 2f, Server.VolumeNormal, Server.AttenuationNormal );
	}

	public void Scream()
	{
		if ( !OkToSpeak() )
			return;

		Talk( 10f );
		TalkTarget = EnemyEntity;
		PlaySentence( "SC_SCREAM", Server.RandomFloat( 3f, 6f ), Server.VolumeNormal, Server.AttenuationNormal );
	}

	public override Activity GetStoppedActivity() => EnemyEntity is not null ? Activity.Excited : base.GetStoppedActivity();

	public override void StartTask( Task task )
	{
		switch ( task.Id )
		{
			case ScientistTask.SayHeal:
				Talk( 2f );
				TalkTarget = TargetEntity;
				PlaySentence( "SC_HEAL", 2f, Server.VolumeNormal, Server.AttenuationIdle );

				TaskComplete();
				break;

			case ScientistTask.Scream:
				Scream();
				TaskComplete();
				break;

			case ScientistTask.RandomScream:
				if ( Server.RandomFloat( 0f, 1f ) < task.Data )
					Scream();

				TaskComplete();
				break;

			case ScientistTask.SayFear:
				if ( OkToSpeak() )
				{
					Talk( 2f );
					TalkTarget = EnemyEntity;
					PlaySentence( EnemyEntity.IsPlayer ? "SC_PLFEAR" : "SC_FEAR", 5f, Server.VolumeNormal, Server.AttenuationNormal );
				}

				TaskComplete();
				break;

			case ScientistTask.Heal:
				IdealActivity = Activity.MeleeAttack1;
				break;

			case ScientistTask.RunPathScared:
				MovementActivity = Activity.RunScared;
				break;

			case ScientistTask.MoveToTargetRangeScared:
				if ( MathLib.Length( TargetEntity.Origin - Origin ) < 1f )
				{
					TaskComplete();
				}
				else
				{
					MoveGoal = TargetEntity.Origin;

					if ( !MoveToTarget( Activity.WalkScared, 0.5f ) )
						TaskFail();
				}

				break;

			default:
				base.StartTask( task );
				break;
		}
	}

	public override void RunTask( Task task )
	{
		switch ( task.Id )
		{
			case ScientistTask.RunPathScared:
				if ( MovementIsComplete )
					TaskComplete();

				if ( Server.RandomLong( 0, 31 ) < 8 )
					Scream();

				break;

			case ScientistTask.MoveToTargetRangeScared:
			{
				if ( Server.RandomLong( 0, 63 ) < 8 )
					Scream();

				if ( EnemyEntity is null )
				{
					TaskFail();
					break;
				}

				var distance = MathLib.Length2D( MoveGoal - Origin );

				if ( distance < task.Data || MathLib.Length( MoveGoal - TargetEntity.Origin ) > task.Data * 0.5 )
				{
					MoveGoal = TargetEntity.Origin;
					distance = MathLib.Length2D( MoveGoal - Origin );
					RefreshRoute();
				}

				if ( distance < task.Data )
				{
					TaskComplete();
					RouteClear();
				}
				else if ( distance < 190f && MovementActivity != Activity.WalkScared )
				{
					MovementActivity = Activity.WalkScared;
				}
				else if ( distance >= 270f && MovementActivity != Activity.RunScared )
				{
					MovementActivity = Activity.RunScared;
				}

				break;
			}

			case ScientistTask.Heal:
				if ( SequenceFinished )
				{
					TaskComplete();
				}
				else
				{
					if ( TargetDistance() > 90f )
						TaskComplete();

					IdealYaw = MathLib.VecToYaw( TargetEntity.Origin - Origin );
					ChangeYaw( (int)YawSpeed );
				}

				break;

			default:
				base.RunTask( task );
				break;
		}
	}

	public override int Classify() => ClassHumanPassive;

	public override void SetYawSpeed()
	{
		YawSpeed = Activity switch
		{
			Activity.Idle => 120f,
			Activity.Walk => 180f,
			Activity.Run => 150f,
			Activity.TurnLeft or Activity.TurnRight => 120f,
			_ => 90f
		};
	}

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		switch ( monsterEvent.Event )
		{
			case AnimEventHeal:
				Heal();
				break;
			case AnimEventNeedleOn:
				Body = (Body % HeadCount) + HeadCount;
				break;
			case AnimEventNeedleOff:
				Body %= HeadCount;
				break;
			default:
				base.HandleAnimEvent( monsterEvent );
				break;
		}
	}

	public override void Spawn()
	{
		Precache();

		SetModel( ScientistModel );
		SetSize( HumanHullMin, HumanHullMax );

		Solid = Solid.SlideBox;
		MoveType = MoveType.Step;
		MonsterBloodColor = BloodColorRed;
		Health = HalfLifeDll.SkillData.ScientistHealth;
		ViewOffset = new Vector3( 0f, 0f, 50f );
		FieldOfView = ViewFieldWide;
		MonsterState = MonsterState.None;

		Capability = CapabilityHear | CapabilityTurnHead | CapabilityOpenDoors | CapabilityAutoDoors | CapabilityUse;

		Skin = 0;

		if ( Body == -1 )
			Body = Server.RandomLong( 0, HeadCount - 1 );

		if ( Body == HeadLuther )
			Skin = 1;

		MonsterInit();
		SetUse( FollowerUse );
	}

	public override void Precache()
	{
		Server.PrecacheModel( ScientistModel );
		Server.PrecacheSound( "scientist/sci_pain1.wav" );
		Server.PrecacheSound( "scientist/sci_pain2.wav" );
		Server.PrecacheSound( "scientist/sci_pain3.wav" );
		Server.PrecacheSound( "scientist/sci_pain4.wav" );
		Server.PrecacheSound( "scientist/sci_pain5.wav" );

		TalkInit();

		base.Precache();
	}

	public new void TalkInit()
	{
		base.TalkInit();

		Friends[0] = "monster_scientist";
		Friends[1] = "monster_sitting_scientist";
		Friends[2] = "monster_barney";

		Groups[TalkGroup.Answer] = "SC_ANSWER";
		Groups[TalkGroup.Question] = "SC_QUESTION";
		Groups[TalkGroup.Idle] = "SC_IDLE";
		Groups[TalkGroup.Stare] = "SC_STARE";
		Groups[TalkGroup.Use] = "SC_OK";
		Groups[TalkGroup.UnUse] = "SC_WAIT";
		Groups[TalkGroup.Stop] = "SC_STOP";
		Groups[TalkGroup.NoShoot] = "SC_SCARED";
		Groups[TalkGroup.Hello] = "SC_HELLO";

		Groups[TalkGroup.PlayerHurt1] = "!SC_CUREA";
		Groups[TalkGroup.PlayerHurt2] = "!SC_CUREB";
		Groups[TalkGroup.PlayerHurt3] = "!SC_CUREC";

		Groups[TalkGroup.PreHello] = "SC_PHELLO";
		Groups[TalkGroup.PreIdle] = "SC_PIDLE";
		Groups[TalkGroup.PreQuestion] = "SC_PQUEST";
		Groups[TalkGroup.Smell] = "SC_SMELL";

		Groups[TalkGroup.Wound] = "SC_WOUND";
		Groups[TalkGroup.Mortal] = "SC_MORTAL";

		VoicePitch = (Body % 3) switch
		{
			HeadEinstein => 100,
			HeadLuther => 95,
			_ => 105
		};
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		if ( inflictor is not null && (inflictor.EntityFlags & EntityFlags.Client) == EntityFlags.Client )
		{
			Remember( MemoryProvoked );
			StopFollowing( true );
		}

		return base.TakeDamage( inflictor, attacker, damage, damageType );
	}

	public override int SoundMask() => WorldSound.World | WorldSound.Combat | WorldSound.Danger | WorldSound.Player;

	public override void PainSound()
	{
		if ( Server.Globals.Time < _painTime )
			return;

		_painTime = Server.Globals.Time + Server.RandomFloat( 0.5f, 0.75f );

		Sound.EmitDyn( this, SoundChannel.Voice, $"scientist/sci_pain{Server.RandomLong( 0, 4 ) + 1}.wav", 1f, Server.AttenuationNormal, 0, GetVoicePitch() );
	}

	public override void DeathSound() => PainSound();

	public override void Killed( BaseEntity attacker, int gib )
	{
		SetUse( null );

		base.Killed( attacker, gib );
	}

	public override void SetActivity( Activity activity )
	{
		if ( LookupActivity( (int)activity ) == Animation.ActivityNotAvailable )
			activity = Activity.Idle;

		base.SetActivity( activity );
	}

	public override Schedule GetScheduleOfType( int type )
	{
		Schedule schedule;

		switch ( type )
		{
			case ScheduleType.TargetFace:
				schedule = base.GetScheduleOfType( type );

				return schedule == DefaultAi.IdleStand ? ScientistAi.FaceTarget : schedule;

			case ScheduleType.TargetChase:
				return ScientistAi.Follow;

			case TalkSchedule.CantFollow:
				return ScientistAi.StopFollowing;

			case ScientistSchedule.Panic:
				return ScientistAi.SciPanic;

			case ScientistSchedule.TargetChaseScared:
				return ScientistAi.FollowScared;

			case ScientistSchedule.TargetFaceScared:
				return ScientistAi.FaceTargetScared;

			case ScheduleType.IdleStand:
				schedule = base.GetScheduleOfType( type );

				return schedule == DefaultAi.IdleStand ? ScientistAi.IdleSciStand : schedule;

			case ScientistSchedule.Hide:
				return ScientistAi.ScientistHide;

			case ScientistSchedule.Startle:
				return ScientistAi.ScientistStartle;

			case ScientistSchedule.Fear:
				return ScientistAi.Fear;
		}

		return base.GetScheduleOfType( type );
	}

	public override Schedule GetSchedule()
	{
		var enemy = EnemyEntity;

		if ( HasConditions( ConditionHearSound ) && BestSound() is { } dangerSound && (dangerSound.Type & WorldSound.Danger) != 0 )
			return GetScheduleOfType( ScheduleType.TakeCoverFromBestSound );

		switch ( MonsterState )
		{
			case MonsterState.Alert:
			case MonsterState.Idle:
				if ( enemy is not null )
				{
					if ( HasConditions( ConditionSeeEnemy ) )
					{
						_fearTime = (float)Server.Globals.Time;
					}
					else if ( DisregardEnemy( enemy ) )
					{
						EnemyEntity = null;
						enemy = null;
					}
				}

				if ( HasConditions( ConditionLightDamage | ConditionHeavyDamage ) )
					return GetScheduleOfType( ScheduleType.SmallFlinch );

				if ( HasConditions( ConditionHearSound ) && BestSound() is { } sound && (sound.Type & (WorldSound.Danger | WorldSound.Combat)) != 0
					&& Server.Globals.Time - _fearTime > 3.0 )
				{
					_fearTime = (float)Server.Globals.Time;

					return GetScheduleOfType( ScientistSchedule.Startle );
				}

				if ( IsFollowing )
				{
					if ( !TargetEntity.IsAlive )
					{
						StopFollowing( false );
						break;
					}

					var relationship = enemy is not null ? Relationship( enemy ) : RelationshipNone;

					if ( relationship is RelationshipDislike or RelationshipHate )
						return GetScheduleOfType( HasConditions( ConditionNewEnemy ) ? ScientistSchedule.Fear : ScientistSchedule.TargetFaceScared );

					if ( TargetDistance() <= 128f )
					{
						if ( CanHeal )
							return ScientistAi.Heal;

						if ( HasConditions( ConditionClientPush ) )
							return GetScheduleOfType( TalkSchedule.MoveAwayFollow );
					}

					return GetScheduleOfType( ScheduleType.TargetFace );
				}

				if ( HasConditions( ConditionClientPush ) )
					return GetScheduleOfType( TalkSchedule.MoveAway );

				TrySmellTalk();
				break;

			case MonsterState.Combat:
				if ( HasConditions( ConditionNewEnemy ) )
					return ScientistAi.Fear;

				if ( HasConditions( ConditionSeeEnemy ) )
					return ScientistAi.ScientistCover;

				if ( HasConditions( ConditionHearSound ) )
					return DefaultAi.TakeCoverFromBestSound;

				return ScientistAi.ScientistCover;
		}

		return base.GetSchedule();
	}

	public override MonsterState GetIdealState()
	{
		switch ( MonsterState )
		{
			case MonsterState.Alert:
			case MonsterState.Idle:
				if ( HasConditions( ConditionNewEnemy ) )
				{
					if ( IsFollowing )
					{
						var relationship = Relationship( EnemyEntity );

						if ( relationship != RelationshipFear || (relationship != RelationshipHate && !HasConditions( ConditionLightDamage | ConditionHeavyDamage )) )
						{
							IdealMonsterState = MonsterState.Alert;
							return IdealMonsterState;
						}

						StopFollowing( true );
					}
				}
				else if ( HasConditions( ConditionLightDamage | ConditionHeavyDamage ) )
				{
					if ( IsFollowing )
						StopFollowing( true );
				}

				break;

			case MonsterState.Combat:
				if ( EnemyEntity is { } enemy )
				{
					if ( DisregardEnemy( enemy ) )
					{
						IdealMonsterState = MonsterState.Alert;
						EnemyEntity = null;
						return IdealMonsterState;
					}

					if ( TargetEntity is not null )
					{
						IdealMonsterState = MonsterState.Alert;
						return IdealMonsterState;
					}

					if ( HasConditions( ConditionSeeEnemy ) )
					{
						_fearTime = (float)Server.Globals.Time;
						IdealMonsterState = MonsterState.Combat;
						return IdealMonsterState;
					}
				}

				break;
		}

		return base.GetIdealState();
	}

	public bool CanHeal => _healTime <= Server.Globals.Time && TargetEntity is { } target && target.Health <= target.MaxHealth * 0.5;

	public void Heal()
	{
		if ( !CanHeal )
			return;

		if ( MathLib.Length( TargetEntity.Origin - Origin ) > 100f )
			return;

		TargetEntity.TakeHealth( HalfLifeDll.SkillData.ScientistHeal, DamageGeneric );

		_healTime = (float)(Server.Globals.Time + 60.0);
	}

	public override int FriendNumber( int arrayNumber ) => arrayNumber < 3 ? FriendOrder[arrayNumber] : arrayNumber;
}

[LinkEntityToClass( "monster_scientist_dead" )]
public class DeadScientist : BaseMonster
{
	private static readonly string[] Poses = ["lying_on_back", "lying_on_stomach", "dead_sitting", "dead_hang", "dead_table1", "dead_table2", "dead_table3"];

	private int _pose;

	public override int Classify() => ClassHumanPassive;

	public override bool KeyValue( string key, string value )
	{
		if ( key != "pose" )
			return base.KeyValue( key, value );

		_pose = Util.Atoi( value );

		return true;
	}

	public override void Spawn()
	{
		Server.PrecacheModel( Scientist.ScientistModel );
		SetModel( Scientist.ScientistModel );

		Effects = 0;
		Sequence = 0;
		Health = 8f;

		MonsterBloodColor = BloodColorRed;

		if ( Body == -1 )
			Body = Server.RandomLong( 0, Scientist.HeadCount - 1 );

		Skin = Body == Scientist.HeadLuther ? 1 : 0;

		Sequence = LookupSequence( Poses[_pose] );

		if ( Sequence == -1 )
			Util.Alert( AlertType.Console, "Dead scientist with bad pose" );

		MonsterInitDead();
	}
}

[LinkEntityToClass( "monster_sitting_scientist" )]
public class SittingScientist : Scientist
{
	private const int AnimSitLookLeft = 0;
	private const int AnimSitLookRight = 1;
	private const int AnimSitScared = 2;
	private const int AnimSitting2 = 3;
	private const int AnimSitting3 = 4;

	private static readonly int[] FriendOrder = [2, 1, 0];

	private int _baseSequence;
	[Save( FieldType.Integer )]
	private int _headTurn;
	[Save( FieldType.Float )]
	private float _responseDelay;

	public override void Spawn()
	{
		Server.PrecacheModel( ScientistModel );
		SetModel( ScientistModel );
		Precache();
		InitBoneControllers();

		SetSize( new Vector3( -14f, -14f, 0f ), new Vector3( 14f, 14f, 36f ) );

		Solid = Solid.SlideBox;
		MoveType = MoveType.Step;
		Effects = 0;
		Health = 50f;

		MonsterBloodColor = BloodColorRed;
		FieldOfView = ViewFieldWide;

		Capability = CapabilityHear | CapabilityTurnHead;

		SpawnFlags |= SpawnFlagPreDisaster;

		if ( Body == -1 )
			Body = Server.RandomLong( 0, HeadCount - 1 );

		if ( Body == HeadLuther )
			Skin = 1;

		_baseSequence = LookupSequence( "sitlookleft" );
		Sequence = _baseSequence + Server.RandomLong( 0, 4 );
		ResetSequenceInfo();

		SetThink( SittingThink );
		NextThink = (float)(Server.Globals.Time + 0.1);

		Server.DropToFloor( this );
	}

	public override void Precache()
	{
		_baseSequence = LookupSequence( "sitlookleft" );
		TalkInit();
	}

	public override int Classify() => ClassHumanPassive;

	public override int FriendNumber( int arrayNumber ) => arrayNumber < 3 ? FriendOrder[arrayNumber] : arrayNumber;

	private int LookSequence( BaseEntity entity )
	{
		var yaw = VecToYaw( entity.Origin - Origin ) - Angles.y;

		if ( yaw > 180f )
			yaw -= 360f;

		if ( yaw < -180f )
			yaw += 360f;

		return _baseSequence + (yaw > 0f ? AnimSitLookLeft : AnimSitLookRight);
	}

	public void SittingThink()
	{
		StudioFrameAdvance();

		if ( IdleHello() )
		{
			if ( FindNearestFriend( true ) is { } player )
			{
				Sequence = LookSequence( player );

				ResetSequenceInfo();
				Frame = 0f;
				SetBoneController( 0, 0f );
			}
		}
		else if ( SequenceFinished )
		{
			var i = Server.RandomLong( 0, 99 );

			_headTurn = 0;

			if ( _responseDelay != 0f && Server.Globals.Time > _responseDelay )
			{
				IdleRespond();
				Sequence = _baseSequence + AnimSitScared;
				_responseDelay = 0f;
			}
			else if ( i < 30 )
			{
				Sequence = _baseSequence + AnimSitting3;

				var friend = FindNearestFriend( (Said & SaidHelloPlayer) == 0 );

				if ( !IdleSpeak() || friend is null )
				{
					_headTurn = (Server.RandomLong( 0, 8 ) * 10) - 40;
					Sequence = _baseSequence + AnimSitting3;
				}
				else
				{
					Sequence = LookSequence( friend );
				}
			}
			else if ( i < 60 )
			{
				Sequence = _baseSequence + AnimSitting3;
				_headTurn = (Server.RandomLong( 0, 8 ) * 10) - 40;

				if ( Server.RandomLong( 0, 99 ) < 5 )
					IdleSpeak();
			}
			else if ( i < 80 )
			{
				Sequence = _baseSequence + AnimSitting2;
			}
			else if ( i < 100 )
			{
				Sequence = _baseSequence + AnimSitScared;
			}

			ResetSequenceInfo();
			Frame = 0f;
			SetBoneController( 0, _headTurn );
		}

		NextThink = (float)(Server.Globals.Time + 0.1);
	}

	public override void SetAnswerQuestion( TalkMonster speaker )
	{
		_responseDelay = Server.Globals.Time + Server.RandomFloat( 3f, 4f );
		TalkTarget = speaker;
	}

	public new bool IdleSpeak()
	{
		if ( !OkToSpeak() )
			return false;

		TalkWaitTime = Server.Globals.Time + Server.RandomFloat( 4.8f, 5.2f );

		var pitch = GetVoicePitch();
		var friend = FindNearestFriend( false );

		if ( friend is not null && Server.RandomLong( 0, 1 ) != 0 )
		{
			((TalkMonster)friend).SetAnswerQuestion( this );

			IdleHeadTurn( friend.Origin );
			Sentences.PlayRandom( this, Groups[TalkGroup.PreQuestion], 1f, Server.AttenuationIdle, 0, pitch );

			TalkWaitTime = Server.Globals.Time + Server.RandomFloat( 4.8f, 5.2f );

			return true;
		}

		if ( Server.RandomLong( 0, 1 ) != 0 )
		{
			Sentences.PlayRandom( this, Groups[TalkGroup.PreIdle], 1f, Server.AttenuationIdle, 0, pitch );

			TalkWaitTime = Server.Globals.Time + Server.RandomFloat( 4.8f, 5.2f );

			return true;
		}

		TalkWaitTime = 0f;

		return false;
	}
}
