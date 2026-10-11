using GoldSrc;

namespace HalfLife;

public static class HoundTask
{
	public const int CloseEye = TaskId.LastCommon + 1;
	public const int OpenEye = CloseEye + 1;
	public const int ThreatDisplay = CloseEye + 2;
	public const int FallAsleep = CloseEye + 3;
	public const int WakeUp = CloseEye + 4;
	public const int HopBack = CloseEye + 5;
}

public static class HoundSchedule
{
	public const int Agitated = ScheduleType.LastCommon + 1;
	public const int HopRetreat = Agitated + 1;
	public const int Fail = Agitated + 2;
}

[SkipHotload]
public static class HoundeyeAi
{
	public static readonly Schedule HoundGuardPack = new( "GuardPack",
		BaseMonster.ConditionSeeHate | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionProvoked | BaseMonster.ConditionHearSound,
		WorldSound.Combat | WorldSound.World | WorldSound.Meat | WorldSound.Player,
		new( TaskId.StopMoving ),
		new( TaskId.Guard ) );

	public static readonly Schedule HoundRangeAttack = new( "HoundRangeAttack1",
		BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.FaceIdeal ),
		new( TaskId.RangeAttack1 ),
		new( TaskId.SetSchedule, HoundSchedule.Agitated ) );

	public static readonly Schedule HoundRangeAttack2 = new( "HoundRangeAttack2",
		BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.FaceIdeal ),
		new( TaskId.RangeAttack1 ) );

	public static readonly Schedule HoundSleep = new( "Hound Sleep",
		BaseMonster.ConditionHearSound | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionNewEnemy,
		WorldSound.Combat | WorldSound.Player | WorldSound.World,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.WaitRandom, 5f ),
		new( TaskId.PlaySequence, (float)Activity.Crouch ),
		new( TaskId.SetActivity, (float)Activity.CrouchIdle ),
		new( HoundTask.FallAsleep ),
		new( TaskId.WaitRandom, 25f ),
		new( HoundTask.CloseEye ) );

	public static readonly Schedule HoundWakeLazy = new( "WakeLazy",
		0,
		0,
		new( TaskId.StopMoving ),
		new( HoundTask.OpenEye ),
		new( TaskId.WaitRandom, 2.5f ),
		new( TaskId.PlaySequence, (float)Activity.Stand ),
		new( HoundTask.WakeUp ) );

	public static readonly Schedule HoundWakeUrgent = new( "WakeUrgent",
		0,
		0,
		new( HoundTask.OpenEye ),
		new( TaskId.PlaySequence, (float)Activity.Hop ),
		new( TaskId.FaceIdeal ),
		new( HoundTask.WakeUp ) );

	public static readonly Schedule HoundSpecialAttack1 = new( "Hound Special Attack1",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionEnemyOccluded,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.FaceIdeal ),
		new( TaskId.SpecialAttack1 ),
		new( TaskId.PlaySequence, (float)Activity.IdleAngry ) );

	public static readonly Schedule HoundAgitated = new( "Hound Agitated",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage,
		0,
		new( TaskId.StopMoving ),
		new( HoundTask.ThreatDisplay ) );

	public static readonly Schedule HoundHopRetreat = new( "Hound Hop Retreat",
		0,
		0,
		new( TaskId.StopMoving ),
		new( HoundTask.HopBack ),
		new( TaskId.SetSchedule, ScheduleType.TakeCoverFromEnemy ) );

	public static readonly Schedule HoundCombatFailPVS = new( "HoundCombatFailPVS",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage,
		0,
		new( TaskId.StopMoving ),
		new( HoundTask.ThreatDisplay ),
		new( TaskId.WaitFaceEnemy, 1f ) );

	public static readonly Schedule HoundCombatFailNoPVS = new( "HoundCombatFailNoPVS",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage,
		0,
		new( TaskId.StopMoving ),
		new( HoundTask.ThreatDisplay ),
		new( TaskId.WaitFaceEnemy, 2f ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.WaitPvs ) );

	public static readonly Schedule[] List =
	[
		HoundGuardPack,
		HoundRangeAttack,
		HoundRangeAttack2,
		HoundSleep,
		HoundWakeLazy,
		HoundWakeUrgent,
		HoundSpecialAttack1,
		HoundAgitated,
		HoundHopRetreat,
		HoundCombatFailPVS,
		HoundCombatFailNoPVS
	];
}

[LinkEntityToClass( "monster_houndeye" )]
public class Houndeye : SquadMonster
{
	private const int MaxAttackRadius = 384;
	private const float SquadBonus = 1.1f;
	private const int EyeFrames = 4;
	private const int SoundStartleVolume = 128;

	private const int AnimEventWarn = 1;
	private const int AnimEventStartAttack = 2;
	private const int AnimEventThump = 3;
	private const int AnimEventAngerSound1 = 4;
	private const int AnimEventAngerSound2 = 5;
	private const int AnimEventHopBack = 6;
	private const int AnimEventCloseEye = 7;

	private static readonly short[] HoundHints = [Hint.WorldMachinery, Hint.WorldBlinkingLight, Hint.WorldHumanBlood, Hint.WorldAlienBlood];

	private static readonly string[] AlertSounds = ["houndeye/he_alert1.wav", "houndeye/he_alert2.wav", "houndeye/he_alert3.wav"];
	private static readonly string[] DeathSounds = ["houndeye/he_die1.wav", "houndeye/he_die2.wav", "houndeye/he_die3.wav"];
	private static readonly string[] IdleSounds = ["houndeye/he_idle1.wav", "houndeye/he_idle2.wav", "houndeye/he_idle3.wav"];
	private static readonly string[] WarnSounds = ["houndeye/he_hunt1.wav", "houndeye/he_hunt2.wav", "houndeye/he_hunt3.wav"];
	private static readonly string[] PainSounds = ["houndeye/he_pain3.wav", "houndeye/he_pain4.wav", "houndeye/he_pain5.wav"];
	private static readonly string[] WarmUpSounds = ["houndeye/he_attack1.wav", "houndeye/he_attack3.wav"];
	private static readonly string[] BlastSounds = ["houndeye/he_blast1.wav", "houndeye/he_blast2.wav", "houndeye/he_blast3.wav"];

	[Save( FieldType.Integer )]
	private int _spriteTexture;
	[Save( FieldType.Boolean )]
	private bool _asleep;
	[Save( FieldType.Boolean )]
	private bool _dontBlink;
	[Save( FieldType.PositionVector )]
	private Vector3 _packCenter;

	public override Schedule ScheduleFromName( string name ) => ScheduleInList( name, HoundeyeAi.List ) ?? base.ScheduleFromName( name );

	public override int Classify() => ClassAlienMonster;

	public override bool CanBecomeRagdoll() => true;

	public override bool ValidateHintType( short hint )
	{
		for ( var i = 0; i < HoundHints.Length; i++ )
		{
			if ( HoundHints[i] == hint )
				return true;
		}

		Util.Alert( AlertType.AiConsole, "Couldn't validate hint type" );
		return false;
	}

	public override bool CanActiveIdle()
	{
		if ( !InSquad )
			return true;

		var squadLeader = MySquadLeader();

		for ( var i = 0; i < MaxSquadMembers; i++ )
		{
			var member = squadLeader.MySquadMember( i );

			if ( member is not null && member != this && member.HintNode != Graph.NoNode )
				return false;
		}

		return true;
	}

	public override bool CheckRangeAttack1( float dot, float distance ) => distance <= MaxAttackRadius * 0.5 && dot >= 0.3;

	public override void SetYawSpeed()
	{
		YawSpeed = Activity switch
		{
			Activity.CrouchIdle => 0f,
			Activity.Idle => 60f,
			_ => 90f
		};
	}

	public override void SetActivity( Activity activity )
	{
		if ( activity == Activity )
			return;

		if ( MonsterState == MonsterState.Combat && activity == Activity.Idle && Server.RandomLong( 0, 1 ) != 0 )
		{
			var sequence = LookupSequence( "madidle" );

			Activity = activity;
			IdealActivity = Activity;

			if ( sequence > Animation.ActivityNotAvailable )
			{
				Sequence = sequence;
				Frame = 0f;
				ResetSequenceInfo();
				SetYawSpeed();
			}
		}
		else
		{
			base.SetActivity( activity );
		}
	}

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		switch ( monsterEvent.Event )
		{
			case AnimEventWarn:
				WarnSound();
				break;

			case AnimEventStartAttack:
				WarmUpSound();
				break;

			case AnimEventHopBack:
				{
					var gravity = Cvar.GetFloat( "sv_gravity" );

					EntityFlags &= ~EntityFlags.OnGround;

					var velocity = Server.Globals.Forward * -200f;

					velocity.z = (float)(velocity.z + (0.6 * gravity * 0.5));
					Velocity = velocity;
					break;
				}

			case AnimEventThump:
				SonicAttack();
				break;

			case AnimEventAngerSound1:
				Sound.Emit( this, SoundChannel.Voice, "houndeye/he_pain3.wav", 1f, Server.AttenuationNormal );
				break;

			case AnimEventAngerSound2:
				Sound.Emit( this, SoundChannel.Voice, "houndeye/he_pain1.wav", 1f, Server.AttenuationNormal );
				break;

			case AnimEventCloseEye:
				if ( !_dontBlink )
					Skin = EyeFrames - 1;
				break;

			default:
				base.HandleAnimEvent( monsterEvent );
				break;
		}
	}

	public override void Spawn()
	{
		Precache();

		SetModel( "models/houndeye.mdl" );
		SetSize( new Vector3( -16f, -16f, 0f ), new Vector3( 16f, 16f, 36f ) );

		Solid = Solid.SlideBox;
		MoveType = MoveType.Step;
		MonsterBloodColor = BloodColorYellow;
		Effects = 0;
		Health = HalfLifeDll.SkillData.HoundeyeHealth;
		YawSpeed = 5f;
		FieldOfView = 0.5f;
		MonsterState = MonsterState.None;
		_asleep = false;
		_dontBlink = false;
		Capability |= CapabilitySquad;

		MonsterInit();
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/houndeye.mdl" );

		PrecacheSounds( AlertSounds );
		PrecacheSounds( DeathSounds );
		PrecacheSounds( IdleSounds );
		PrecacheSounds( WarnSounds );
		Server.PrecacheSound( "houndeye/he_pain1.wav" );
		PrecacheSounds( PainSounds );
		PrecacheSounds( WarmUpSounds );
		PrecacheSounds( BlastSounds );

		_spriteTexture = Server.PrecacheModel( "sprites/shockwave.spr" );
	}

	public override void IdleSound()
	{
		Sound.Emit( this, SoundChannel.Voice, RandomSound( IdleSounds ), 1f, Server.AttenuationNormal );
	}

	public void WarmUpSound()
	{
		Sound.Emit( this, SoundChannel.Weapon, RandomSound( WarmUpSounds ), 0.7f, Server.AttenuationNormal );
	}

	public void WarnSound()
	{
		Sound.Emit( this, SoundChannel.Voice, RandomSound( WarnSounds ), 1f, Server.AttenuationNormal );
	}

	public override void AlertSound()
	{
		if ( InSquad && !IsLeader )
			return;

		Sound.Emit( this, SoundChannel.Voice, RandomSound( AlertSounds ), 1f, Server.AttenuationNormal );
	}

	public override void DeathSound()
	{
		Sound.Emit( this, SoundChannel.Voice, RandomSound( DeathSounds ), 1f, Server.AttenuationNormal );
	}

	public override void PainSound()
	{
		Sound.Emit( this, SoundChannel.Voice, RandomSound( PainSounds ), 1f, Server.AttenuationNormal );
	}

	public void WriteBeamColor()
	{
		var (red, green, blue) = (188, 220, 255);

		if ( InSquad )
		{
			switch ( SquadCount() )
			{
				case 2:
					(red, green, blue) = (101, 133, 221);
					break;
				case 3:
					(red, green, blue) = (67, 85, 255);
					break;
				case 4:
					(red, green, blue) = (62, 33, 211);
					break;
				default:
					Util.Alert( AlertType.AiConsole, "Unsupported Houndeye SquadSize!\n" );
					break;
			}
		}

		Server.WriteByte( red );
		Server.WriteByte( green );
		Server.WriteByte( blue );
	}

	private void WriteBeamCylinder( float radius )
	{
		Server.MessageBegin( MessageDest.Pas, Server.TempEntityMessage, Origin );
		Server.WriteByte( TempEntity.BeamCylinder );
		Server.WriteCoord( Origin.x );
		Server.WriteCoord( Origin.y );
		Server.WriteCoord( Origin.z + 16f );
		Server.WriteCoord( Origin.x );
		Server.WriteCoord( Origin.y );
		Server.WriteCoord( (float)(Origin.z + 16f + (radius / .2)) );
		Server.WriteShort( _spriteTexture );
		Server.WriteByte( 0 );
		Server.WriteByte( 0 );
		Server.WriteByte( 2 );
		Server.WriteByte( 16 );
		Server.WriteByte( 0 );
		WriteBeamColor();
		Server.WriteByte( 255 );
		Server.WriteByte( 0 );
		Server.MessageEnd();
	}

	public void SonicAttack()
	{
		Sound.Emit( this, SoundChannel.Weapon, RandomSound( BlastSounds ), 1f, Server.AttenuationNormal );

		WriteBeamCylinder( MaxAttackRadius );
		WriteBeamCylinder( MaxAttackRadius / 2 );

		var blast = HalfLifeDll.SkillData.HoundeyeDmgBlast;
		BaseEntity entity = null;

		while ( (entity = Util.FindEntityInSphere( entity, Origin, MaxAttackRadius )) is not null )
		{
			if ( entity.DamageMode == DamageNo || entity.ClassName == "monster_houndeye" )
				continue;

			var adjustedDamage = SquadCount() > 1 ? blast + (blast * (SquadBonus * (SquadCount() - 1))) : blast;
			var distance = MathLib.Length( entity.Center - Origin );

			adjustedDamage -= distance / MaxAttackRadius * adjustedDamage;

			if ( !IsVisible( entity ) )
			{
				if ( entity.IsPlayer )
					adjustedDamage = (float)(adjustedDamage * 0.5);
				else if ( entity.ClassName != "func_breakable" && entity.ClassName != "func_pushable" )
					adjustedDamage = 0f;
			}

			if ( adjustedDamage > 0f )
				entity.TakeDamage( this, this, adjustedDamage, DamageSonic | DamageAlwaysGib );
		}
	}

	public override void StartTask( Task task )
	{
		TaskStatus = TaskState.Running;

		switch ( task.Id )
		{
			case HoundTask.FallAsleep:
				_asleep = true;
				TaskStatus = TaskState.Complete;
				break;

			case HoundTask.WakeUp:
				_asleep = false;
				TaskStatus = TaskState.Complete;
				break;

			case HoundTask.OpenEye:
				_dontBlink = false;
				TaskStatus = TaskState.Complete;
				break;

			case HoundTask.CloseEye:
				Skin = 0;
				_dontBlink = true;
				break;

			case HoundTask.ThreatDisplay:
				IdealActivity = Activity.IdleAngry;
				break;

			case HoundTask.HopBack:
				IdealActivity = Activity.Leap;
				break;

			case TaskId.RangeAttack1:
				IdealActivity = Activity.RangeAttack1;
				break;

			case TaskId.SpecialAttack1:
				IdealActivity = Activity.SpecialAttack1;
				break;

			case TaskId.Guard:
				IdealActivity = Activity.Guard;
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
			case HoundTask.ThreatDisplay:
				MakeIdealYaw( EnemyLastKnownPosition );
				ChangeYaw( (int)YawSpeed );

				if ( SequenceFinished )
					TaskComplete();
				break;

			case HoundTask.CloseEye:
				if ( Skin < EyeFrames - 1 )
					Skin++;
				break;

			case HoundTask.HopBack:
				if ( SequenceFinished )
					TaskComplete();
				break;

			case TaskId.SpecialAttack1:
				{
					Skin = Server.RandomLong( 0, EyeFrames - 1 );

					MakeIdealYaw( EnemyLastKnownPosition );
					ChangeYaw( (int)YawSpeed );

					var life = (255f - Frame) / (FrameRate * SequenceFrameRate);

					if ( life < 0.1 )
						life = 0.1f;

					Server.MessageBegin( MessageDest.Pas, Server.TempEntityMessage, Origin );
					Server.WriteByte( TempEntity.Implosion );
					Server.WriteCoord( Origin.x );
					Server.WriteCoord( Origin.y );
					Server.WriteCoord( Origin.z + 16f );
					Server.WriteByte( (int)((50f * life) + 100f) );
					Server.WriteByte( (int)(Frame / 25.0) );
					Server.WriteByte( (int)(life * 10f) );
					Server.MessageEnd();

					if ( SequenceFinished )
					{
						SonicAttack();
						TaskComplete();
					}

					break;
				}

			default:
				base.RunTask( task );
				break;
		}
	}

	public override void PrescheduleThink()
	{
		if ( MonsterState == MonsterState.Combat && Activity == Activity.Run && Server.RandomFloat( 0f, 1f ) < 0.2 )
			WarnSound();

		if ( !_dontBlink )
		{
			if ( Skin == 0 && Server.RandomLong( 0, 0x7F ) == 0 )
				Skin = EyeFrames - 1;
			else if ( Skin != 0 )
				Skin--;
		}

		if ( !IsLeader )
			return;

		var squadCount = 0;

		for ( var i = 0; i < MaxSquadMembers; i++ )
		{
			if ( MySquadMember( i ) is not { } member )
				continue;

			squadCount++;
			_packCenter += member.Origin;
		}

		_packCenter /= squadCount;
	}

	public override Schedule GetScheduleOfType( int type )
	{
		if ( _asleep )
		{
			if ( HasConditions( ConditionHearSound ) )
			{
				if ( BestSound() is { } wakeSound )
				{
					MakeIdealYaw( wakeSound.Origin );

					if ( SoundVolume( wakeSound ) >= SoundStartleVolume )
						return HoundeyeAi.HoundWakeUrgent;
				}

				return HoundeyeAi.HoundWakeLazy;
			}

			return HasConditions( ConditionNewEnemy ) ? HoundeyeAi.HoundWakeUrgent : HoundeyeAi.HoundWakeLazy;
		}

		switch ( type )
		{
			case ScheduleType.IdleStand:
				if ( InSquad && !IsLeader && !_asleep && Server.RandomLong( 0, 29 ) < 1 )
					return HoundeyeAi.HoundSleep;

				return base.GetScheduleOfType( type );

			case ScheduleType.RangeAttack1:
				return HoundeyeAi.HoundRangeAttack;

			case ScheduleType.SpecialAttack1:
				return HoundeyeAi.HoundSpecialAttack1;

			case ScheduleType.Guard:
				return HoundeyeAi.HoundGuardPack;

			case HoundSchedule.Agitated:
				return HoundeyeAi.HoundAgitated;

			case HoundSchedule.HopRetreat:
				return HoundeyeAi.HoundHopRetreat;

			case ScheduleType.Fail:
				if ( MonsterState != MonsterState.Combat )
					return base.GetScheduleOfType( type );

				return !Util.IsNullEnt( Server.FindClientInPvs( this ) ) ? HoundeyeAi.HoundCombatFailPVS : HoundeyeAi.HoundCombatFailNoPVS;

			default:
				return base.GetScheduleOfType( type );
		}
	}

	public override Schedule GetSchedule()
	{
		if ( MonsterState != MonsterState.Combat )
			return base.GetSchedule();

		if ( HasConditions( ConditionEnemyDead ) )
			return GetBaseSchedule();

		if ( HasConditions( ConditionLightDamage | ConditionHeavyDamage ) )
		{
			if ( Server.RandomFloat( 0f, 1f ) <= 0.4 )
			{
				Server.Globals.MakeVectors( Angles );

				var trace = Util.TraceHull( Origin, Origin + (Server.Globals.Forward * -128f), DontIgnoreMonsters, HeadHull, this );

				if ( trace.Fraction == 1.0 )
					return GetScheduleOfType( HoundSchedule.HopRetreat );
			}

			return GetScheduleOfType( ScheduleType.TakeCoverFromEnemy );
		}

		if ( HasConditions( ConditionCanRangeAttack1 ) )
			return OccupySlot( SquadSlot.HoundAttack ) ? GetScheduleOfType( ScheduleType.RangeAttack1 ) : GetScheduleOfType( HoundSchedule.Agitated );

		return base.GetSchedule();
	}
}
