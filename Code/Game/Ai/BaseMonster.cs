using GoldSrc;

namespace HalfLife;

public enum ScriptState
{
	Playing,
	Wait,
	Cleanup,
	WalkToMark,
	RunToMark
}

public enum AiTrigger
{
	None,
	SeePlayerAngryAtPlayer,
	TakeDamage,
	HalfHealth,
	Death,
	SquadMemberDie,
	SquadLeaderDie,
	HearWorld,
	HearPlayer,
	HearCombat,
	SeePlayerUnconditional,
	SeePlayerNotInCombat
}

public partial class BaseMonster : BaseToggle, IHotloadManaged
{
	public const int MaxOldEnemies = 4;

	public const float ViewFieldFull = -1f;
	public const float ViewFieldWide = -0.7f;
	public const float ViewFieldNarrow = 0.7f;
	public const float ViewFieldUltraNarrow = 0.9f;
	public const int TimeBasedDamageCount = 8;

	public const int SpawnFlagWaitTillSeen = 1;
	public const int SpawnFlagGag = 2;
	public const int SpawnFlagHitMonsterClip = 4;
	public const int SpawnFlagPrisoner = 16;
	public const int SpawnFlagWaitUntilProvoked = 64;
	public const int SpawnFlagWaitForScript = 128;
	public const int SpawnFlagPreDisaster = 256;
	public const int SpawnFlagFadeCorpse = 512;

	public const int ConditionNoAmmoLoaded = 1 << 0;
	public const int ConditionSeeHate = 1 << 1;
	public const int ConditionSeeFear = 1 << 2;
	public const int ConditionSeeDislike = 1 << 3;
	public const int ConditionSeeEnemy = 1 << 4;
	public const int ConditionEnemyOccluded = 1 << 5;
	public const int ConditionSmellFood = 1 << 6;
	public const int ConditionEnemyTooFar = 1 << 7;
	public const int ConditionLightDamage = 1 << 8;
	public const int ConditionHeavyDamage = 1 << 9;
	public const int ConditionCanRangeAttack1 = 1 << 10;
	public const int ConditionCanMeleeAttack1 = 1 << 11;
	public const int ConditionCanRangeAttack2 = 1 << 12;
	public const int ConditionCanMeleeAttack2 = 1 << 13;
	public const int ConditionProvoked = 1 << 15;
	public const int ConditionNewEnemy = 1 << 16;
	public const int ConditionHearSound = 1 << 17;
	public const int ConditionSmell = 1 << 18;
	public const int ConditionEnemyFacingMe = 1 << 19;
	public const int ConditionEnemyDead = 1 << 20;
	public const int ConditionSeeClient = 1 << 21;
	public const int ConditionSeeNemesis = 1 << 22;
	public const int ConditionSpecial1 = 1 << 28;
	public const int ConditionSpecial2 = 1 << 29;
	public const int ConditionTaskFailed = 1 << 30;
	public const int ConditionScheduleDone = 1 << 31;
	public const int ConditionAllSpecial = ConditionSpecial1 | ConditionSpecial2;
	public const int ConditionCanAttack = ConditionCanRangeAttack1 | ConditionCanMeleeAttack1 | ConditionCanRangeAttack2 | ConditionCanMeleeAttack2;

	public const int MemoryProvoked = 1 << 0;
	public const int MemoryInCover = 1 << 1;
	public const int MemorySuspicious = 1 << 2;
	public const int MemoryPathFinished = 1 << 3;
	public const int MemoryOnPath = 1 << 4;
	public const int MemoryMoveFailed = 1 << 5;
	public const int MemoryFlinched = 1 << 6;
	public const int MemoryKilled = 1 << 7;
	public const int MemoryCustom4 = 1 << 28;
	public const int MemoryCustom3 = 1 << 29;
	public const int MemoryCustom2 = 1 << 30;
	public const int MemoryCustom1 = 1 << 31;

	public const int CapabilityDuck = 1 << 0;
	public const int CapabilityJump = 1 << 1;
	public const int CapabilityStrafe = 1 << 2;
	public const int CapabilitySquad = 1 << 3;
	public const int CapabilitySwim = 1 << 4;
	public const int CapabilityClimb = 1 << 5;
	public const int CapabilityUse = 1 << 6;
	public const int CapabilityHear = 1 << 7;
	public const int CapabilityAutoDoors = 1 << 8;
	public const int CapabilityOpenDoors = 1 << 9;
	public const int CapabilityTurnHead = 1 << 10;
	public const int CapabilityRangeAttack1 = 1 << 11;
	public const int CapabilityRangeAttack2 = 1 << 12;
	public const int CapabilityMeleeAttack1 = 1 << 13;
	public const int CapabilityMeleeAttack2 = 1 << 14;
	public const int CapabilityFly = 1 << 15;
	public const int CapabilityDoorsGroup = CapabilityUse | CapabilityAutoDoors | CapabilityOpenDoors;

	[Save( FieldType.EHandle )]
	private BaseEntity _enemy;
	[Save( FieldType.EHandle )]
	private BaseEntity _targetEntity;
	[Save( FieldType.EHandle )]
	protected BaseEntity[] OldEnemy = new BaseEntity[MaxOldEnemies];
	[Save( FieldType.PositionVector )]
	protected Vector3[] OldEnemyPosition = new Vector3[MaxOldEnemies];

	[Save( FieldType.Float )]
	public float FieldOfView;
	[Save( FieldType.Time )]
	public float WaitFinished;
	[Save( FieldType.Time )]
	public float MoveWaitFinished;

	[Save( FieldType.Integer )]
	public Activity Activity;
	[Save( FieldType.Integer )]
	public Activity IdealActivity;
	[Save( FieldType.Integer )]
	public int LastHitGroup;
	[Save( FieldType.Integer )]
	public MonsterState MonsterState;
	[Save( FieldType.Integer )]
	public MonsterState IdealMonsterState;

	[Save( FieldType.Integer )]
	public int TaskStatus;
	[Save( FieldType.Integer )]
	public int ScheduleIndex;
	[Save( FieldType.Integer )]
	private int _conditions;

	[Save( FieldType.PositionVector )]
	public Vector3 MoveGoal;
	[Save( FieldType.Integer )]
	public Activity MovementActivity;

	public int AudibleList;
	[Save( FieldType.Integer )]
	public int SoundTypes;

	[Save( FieldType.PositionVector )]
	public Vector3 LastPosition;
	[Save( FieldType.Integer )]
	public int HintNode;
	[Save( FieldType.Integer )]
	private int _memory;
	[Save( FieldType.PositionVector )]
	public Vector3 EnemyLastKnownPosition;
	[Save( FieldType.Integer )]
	public int AmmoLoaded;
	[Save( FieldType.Integer )]
	public int Capability;
	[Save( FieldType.Time )]
	public float NextAttack;
	[Save( FieldType.Integer )]
	public int DamageTypeBits;
	[Save( FieldType.Character )]
	public byte[] TimeBasedDamage = new byte[TimeBasedDamageCount];

	[Save( FieldType.Integer )]
	public int MonsterBloodColor;
	[Save( FieldType.Integer )]
	public int FailSchedule;
	[Save( FieldType.Time )]
	public float HungryTime;
	[Save( FieldType.Float )]
	public float DistTooFar;
	[Save( FieldType.Float )]
	public float DistLook;
	[Save( FieldType.Integer )]
	public AiTrigger TriggerCondition;
	[Save( FieldType.String )]
	public string TriggerTarget;
	[Save( FieldType.Vector )]
	public Vector3 HackedGunPos;
	[Save( FieldType.Integer )]
	public ScriptState ScriptState;

	public float LastYawTime;

	public BaseEntity EnemyEntity
	{
		get => _enemy.IsValid() ? _enemy : null;
		set => _enemy = value;
	}

	public BaseEntity TargetEntity
	{
		get => _targetEntity.IsValid() ? _targetEntity : null;
		set => _targetEntity = value;
	}

	public override int BloodColor => MonsterBloodColor;

	public override bool IsAlive => DeadFlag != DeadDead;

	public override Vector3 BodyTarget( Vector3 source ) => (Center * 0.75f) + (EyePosition * 0.25f);

	public virtual void PainSound()
	{
	}

	public virtual void DeathSound()
	{
	}

	public virtual void AlertSound()
	{
	}

	public virtual void IdleSound()
	{
	}

	public void StopAnimation() => FrameRate = 0f;

	public void SetConditions( int conditions ) => _conditions |= conditions;

	public void ClearConditions( int conditions ) => _conditions &= ~conditions;

	public bool HasConditions( int conditions ) => (_conditions & conditions) != 0;

	public bool HasAllConditions( int conditions ) => (_conditions & conditions) == conditions;

	public void Remember( int memory ) => _memory |= memory;

	public void Forget( int memory ) => _memory &= ~memory;

	public bool HasMemory( int memory ) => (_memory & memory) != 0;

	public bool HasAllMemories( int memory ) => (_memory & memory) == memory;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "TriggerTarget":
				TriggerTarget = Server.AllocString( value );
				return true;
			case "TriggerCondition":
				TriggerCondition = (AiTrigger)Util.Atoi( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public const int RelationshipAlly = -2;
	public const int RelationshipFear = -1;
	public const int RelationshipNone = 0;
	public const int RelationshipDislike = 1;
	public const int RelationshipHate = 2;
	public const int RelationshipNemesis = 3;

	private const int AL = RelationshipAlly;
	private const int FR = RelationshipFear;
	private const int NO = RelationshipNone;
	private const int DL = RelationshipDislike;
	private const int HT = RelationshipHate;

	private static readonly int[,] Relationships =
	{
		{ NO, NO, NO, NO, NO, NO, NO, NO, NO, NO, NO, NO, NO, NO },
		{ NO, NO, DL, DL, NO, DL, DL, DL, DL, DL, NO, DL, DL, DL },
		{ NO, DL, NO, NO, DL, DL, DL, DL, DL, DL, NO, NO, DL, DL },
		{ NO, NO, AL, AL, HT, FR, NO, HT, DL, FR, NO, AL, NO, NO },
		{ NO, NO, HT, DL, NO, HT, DL, DL, DL, DL, NO, HT, NO, NO },
		{ NO, DL, HT, DL, HT, NO, NO, NO, NO, NO, NO, DL, NO, NO },
		{ NO, NO, NO, NO, NO, NO, NO, NO, NO, NO, NO, NO, NO, NO },
		{ NO, DL, DL, DL, DL, NO, NO, NO, NO, NO, NO, DL, NO, NO },
		{ NO, NO, DL, DL, DL, NO, NO, NO, NO, FR, NO, DL, NO, NO },
		{ NO, NO, DL, DL, DL, NO, NO, NO, HT, DL, NO, DL, NO, NO },
		{ FR, FR, FR, FR, FR, NO, FR, FR, FR, FR, NO, FR, NO, NO },
		{ NO, DL, AL, AL, DL, DL, DL, DL, DL, DL, NO, NO, NO, NO },
		{ NO, NO, DL, DL, DL, DL, DL, DL, DL, DL, NO, DL, NO, DL },
		{ NO, NO, DL, DL, DL, AL, NO, DL, DL, NO, NO, DL, DL, NO }
	};

	public virtual int Relationship( BaseEntity target )
	{
		var mine = Classify();
		var theirs = target.Classify();

		return mine < 14 && theirs < 14 ? Relationships[mine, theirs] : RelationshipNone;
	}

	public virtual bool ShouldFadeOnDeath()
	{
		return (SpawnFlags & SpawnFlagFadeCorpse) != 0 || !Util.IsNullEnt( Owner );
	}

	public virtual Vector3 GetGunPosition()
	{
		Server.Globals.MakeVectors( Angles );

		return Origin
			+ (Server.Globals.Forward * HackedGunPos.y)
			+ (Server.Globals.Right * HackedGunPos.x)
			+ (Server.Globals.Up * HackedGunPos.z);
	}

	public void MakeIdealYaw( Vector3 target )
	{
		if ( MovementActivity == Activity.StrafeLeft )
			IdealYaw = MathLib.VecToYaw( new Vector3( -target.y, target.x, 0f ) - Origin );
		else if ( MovementActivity == Activity.StrafeRight )
			IdealYaw = MathLib.VecToYaw( new Vector3( target.y, target.x, 0f ) - Origin );
		else
			IdealYaw = MathLib.VecToYaw( target - Origin );
	}

	public bool CheckAiTrigger()
	{
		if ( TriggerCondition == AiTrigger.None )
			return false;

		var fireTarget = false;

		switch ( TriggerCondition )
		{
			case AiTrigger.SeePlayerAngryAtPlayer:
				fireTarget = EnemyEntity is { IsPlayer: true } && HasConditions( ConditionSeeEnemy );
				break;

			case AiTrigger.SeePlayerUnconditional:
				fireTarget = HasConditions( ConditionSeeClient );
				break;

			case AiTrigger.SeePlayerNotInCombat:
				fireTarget = HasConditions( ConditionSeeClient )
					&& MonsterState != MonsterState.Combat
					&& MonsterState != MonsterState.Prone
					&& MonsterState != MonsterState.Script;
				break;

			case AiTrigger.TakeDamage:
				fireTarget = HasConditions( ConditionLightDamage | ConditionHeavyDamage );
				break;

			case AiTrigger.Death:
				fireTarget = DeadFlag != DeadNo;
				break;

			case AiTrigger.HalfHealth:
				fireTarget = IsAlive && Health <= MaxHealth / 2f;
				break;

			case AiTrigger.HearWorld:
				fireTarget = HasConditions( ConditionHearSound ) && (SoundTypes & WorldSound.World) != 0;
				break;

			case AiTrigger.HearPlayer:
				fireTarget = HasConditions( ConditionHearSound ) && (SoundTypes & WorldSound.Player) != 0;
				break;

			case AiTrigger.HearCombat:
				fireTarget = HasConditions( ConditionHearSound ) && (SoundTypes & WorldSound.Combat) != 0;
				break;
		}

		if ( !fireTarget )
			return false;

		Util.Alert( AlertType.AiConsole, "AI Trigger Fire Target\n" );

		FireTargets( TriggerTarget, this, this, UseType.Toggle, 0f );
		TriggerCondition = AiTrigger.None;

		return true;
	}
}
