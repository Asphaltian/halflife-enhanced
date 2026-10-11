using System;
using GoldSrc;

namespace HalfLife;

[SkipHotload]
public static class DefaultAi
{
	public static readonly Schedule Fail = new( "Fail",
		BaseMonster.ConditionCanAttack,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.Wait, 2f ),
		new( TaskId.WaitPvs ) );

	public static readonly Schedule IdleStand = new( "IdleStand",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionSeeFear | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionHearSound | BaseMonster.ConditionSmellFood | BaseMonster.ConditionSmell | BaseMonster.ConditionProvoked,
		WorldSound.Combat | WorldSound.World | WorldSound.Player | WorldSound.Danger | WorldSound.Meat | WorldSound.Carcass | WorldSound.Garbage,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.Wait, 5f ) );

	public static readonly Schedule IdleTrigger = new( "Idle Trigger",
		BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.Wait, 5f ) );

	public static readonly Schedule IdleWalk = new( "Idle Walk",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionHearSound | BaseMonster.ConditionSmellFood | BaseMonster.ConditionSmell | BaseMonster.ConditionProvoked,
		WorldSound.Combat | WorldSound.Meat | WorldSound.Carcass | WorldSound.Garbage,
		new( TaskId.WalkPath, 9999f ),
		new( TaskId.WaitForMovement ) );

	public static readonly Schedule Ambush = new( "Ambush",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionProvoked,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.WaitIndefinite ) );

	public static readonly Schedule ActiveIdle = new( "Active Idle",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionProvoked | BaseMonster.ConditionHearSound,
		WorldSound.Combat | WorldSound.World | WorldSound.Player | WorldSound.Danger,
		new( TaskId.FindHintNode ),
		new( TaskId.GetPathToHintNode ),
		new( TaskId.StoreLastPosition ),
		new( TaskId.WalkPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.FaceHintNode ),
		new( TaskId.PlayActiveIdle ),
		new( TaskId.GetPathToLastPosition ),
		new( TaskId.WalkPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.ClearLastPosition ),
		new( TaskId.ClearHintNode ) );

	public static readonly Schedule WakeAngry = new( "Wake Angry",
		0,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.SoundWake ),
		new( TaskId.FaceIdeal ) );

	public static readonly Schedule AlertFace = new( "Alert Face",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionSeeFear | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionProvoked,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.FaceIdeal ) );

	public static readonly Schedule AlertSmallFlinch = new( "Alert Small Flinch",
		0,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.Remember, BaseMonster.MemoryFlinched ),
		new( TaskId.SmallFlinch ),
		new( TaskId.SetSchedule, ScheduleType.AlertFace ) );

	public static readonly Schedule AlertStand = new( "Alert Stand",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionSeeEnemy | BaseMonster.ConditionSeeFear | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionProvoked | BaseMonster.ConditionSmell | BaseMonster.ConditionSmellFood | BaseMonster.ConditionHearSound,
		WorldSound.Combat | WorldSound.World | WorldSound.Player | WorldSound.Danger | WorldSound.Meat | WorldSound.Carcass | WorldSound.Garbage,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.Wait, 20f ),
		new( TaskId.SuggestState, (float)MonsterState.Idle ) );

	public static readonly Schedule InvestigateSound = new( "InvestigateSound",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionSeeFear | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionHearSound,
		WorldSound.Danger,
		new( TaskId.StopMoving ),
		new( TaskId.StoreLastPosition ),
		new( TaskId.GetPathToBestSound ),
		new( TaskId.FaceIdeal ),
		new( TaskId.WalkPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.PlaySequence, (float)Activity.Idle ),
		new( TaskId.Wait, 10f ),
		new( TaskId.GetPathToLastPosition ),
		new( TaskId.WalkPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.ClearLastPosition ) );

	public static readonly Schedule CombatStand = new( "Combat Stand",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionEnemyDead | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionCanAttack,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.WaitIndefinite ) );

	public static readonly Schedule CombatFace = new( "Combat Face",
		BaseMonster.ConditionCanAttack | BaseMonster.ConditionNewEnemy | BaseMonster.ConditionEnemyDead,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.FaceEnemy ) );

	public static readonly Schedule Standoff = new( "Standoff",
		BaseMonster.ConditionCanRangeAttack1 | BaseMonster.ConditionCanRangeAttack2 | BaseMonster.ConditionEnemyDead | BaseMonster.ConditionNewEnemy | BaseMonster.ConditionHearSound,
		WorldSound.Danger,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.WaitFaceEnemy, 2f ) );

	public static readonly Schedule ArmWeapon = new( "Arm Weapon",
		0,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.PlaySequence, (float)Activity.Arm ) );

	public static readonly Schedule Reload = new( "Reload",
		BaseMonster.ConditionHeavyDamage,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.PlaySequence, (float)Activity.Reload ) );

	public static readonly Schedule RangeAttack1 = new( "Range Attack1",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionEnemyDead | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionEnemyOccluded | BaseMonster.ConditionNoAmmoLoaded | BaseMonster.ConditionHearSound,
		WorldSound.Danger,
		new( TaskId.StopMoving ),
		new( TaskId.FaceEnemy ),
		new( TaskId.RangeAttack1 ) );

	public static readonly Schedule RangeAttack2 = new( "Range Attack2",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionEnemyDead | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionEnemyOccluded | BaseMonster.ConditionHearSound,
		WorldSound.Danger,
		new( TaskId.StopMoving ),
		new( TaskId.FaceEnemy ),
		new( TaskId.RangeAttack2 ) );

	public static readonly Schedule PrimaryMeleeAttack = new( "Primary Melee Attack",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionEnemyDead | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionEnemyOccluded,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.FaceEnemy ),
		new( TaskId.MeleeAttack1 ) );

	public static readonly Schedule SecondaryMeleeAttack = new( "Secondary Melee Attack",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionEnemyDead | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionEnemyOccluded,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.FaceEnemy ),
		new( TaskId.MeleeAttack2 ) );

	public static readonly Schedule SpecialAttack1 = new( "Special Attack1",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionEnemyDead | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionEnemyOccluded | BaseMonster.ConditionNoAmmoLoaded | BaseMonster.ConditionHearSound,
		WorldSound.Danger,
		new( TaskId.StopMoving ),
		new( TaskId.FaceEnemy ),
		new( TaskId.SpecialAttack1 ) );

	public static readonly Schedule SpecialAttack2 = new( "Special Attack2",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionEnemyDead | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionEnemyOccluded | BaseMonster.ConditionNoAmmoLoaded | BaseMonster.ConditionHearSound,
		WorldSound.Danger,
		new( TaskId.StopMoving ),
		new( TaskId.FaceEnemy ),
		new( TaskId.SpecialAttack2 ) );

	public static readonly Schedule ChaseEnemy = new( "Chase Enemy",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionCanRangeAttack1 | BaseMonster.ConditionCanMeleeAttack1 | BaseMonster.ConditionCanRangeAttack2 | BaseMonster.ConditionCanMeleeAttack2 | BaseMonster.ConditionTaskFailed | BaseMonster.ConditionHearSound,
		WorldSound.Danger,
		new( TaskId.SetFailSchedule, ScheduleType.ChaseEnemyFailed ),
		new( TaskId.GetPathToEnemy ),
		new( TaskId.RunPath ),
		new( TaskId.WaitForMovement ) );

	public static readonly Schedule ChaseEnemyFailed = new( "tlChaseEnemyFailed",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionCanRangeAttack1 | BaseMonster.ConditionCanMeleeAttack1 | BaseMonster.ConditionCanRangeAttack2 | BaseMonster.ConditionCanMeleeAttack2 | BaseMonster.ConditionHearSound,
		WorldSound.Danger,
		new( TaskId.StopMoving ),
		new( TaskId.Wait, 0.2f ),
		new( TaskId.FindCoverFromEnemy ),
		new( TaskId.RunPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.Remember, BaseMonster.MemoryInCover ),
		new( TaskId.FaceEnemy ),
		new( TaskId.Wait, 1f ) );

	public static readonly Schedule SmallFlinch = new( "Small Flinch",
		0,
		0,
		new( TaskId.Remember, BaseMonster.MemoryFlinched ),
		new( TaskId.StopMoving ),
		new( TaskId.SmallFlinch ) );

	public static readonly Schedule Die = new( "Die",
		0,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.SoundDie ),
		new( TaskId.Die ) );

	public static readonly Schedule VictoryDance = new( "Victory Dance",
		0,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.PlaySequence, (float)Activity.VictoryDance ),
		new( TaskId.Wait ) );

	public static readonly Schedule BarnacleVictimGrab = new( "Barnacle Victim",
		0,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.PlaySequence, (float)Activity.BarnacleHit ),
		new( TaskId.SetActivity, (float)Activity.BarnaclePull ),
		new( TaskId.WaitIndefinite ) );

	public static readonly Schedule BarnacleVictimChomp = new( "Barnacle Chomp",
		0,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.PlaySequence, (float)Activity.BarnacleChomp ),
		new( TaskId.SetActivity, (float)Activity.BarnacleChew ),
		new( TaskId.WaitIndefinite ) );

	public static readonly Schedule Error = new( "Error",
		0,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.WaitIndefinite ) );

	public static readonly Schedule WalkToScript = new( "WalkToScript",
		CineMonster.BreakConditions,
		0,
		new( TaskId.WalkToTarget, TargetMove.Scripted ),
		new( TaskId.WaitForMovement ),
		new( TaskId.PlantOnScript ),
		new( TaskId.FaceScript ),
		new( TaskId.FaceIdeal ),
		new( TaskId.EnableScript ),
		new( TaskId.WaitForScript ),
		new( TaskId.PlayScript ) );

	public static readonly Schedule RunToScript = new( "RunToScript",
		CineMonster.BreakConditions,
		0,
		new( TaskId.RunToTarget, TargetMove.Scripted ),
		new( TaskId.WaitForMovement ),
		new( TaskId.PlantOnScript ),
		new( TaskId.FaceScript ),
		new( TaskId.FaceIdeal ),
		new( TaskId.EnableScript ),
		new( TaskId.WaitForScript ),
		new( TaskId.PlayScript ) );

	public static readonly Schedule WaitScript = new( "WaitForScript",
		CineMonster.BreakConditions,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.WaitForScript ),
		new( TaskId.PlayScript ) );

	public static readonly Schedule FaceScript = new( "FaceScript",
		CineMonster.BreakConditions,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.FaceScript ),
		new( TaskId.FaceIdeal ),
		new( TaskId.WaitForScript ),
		new( TaskId.PlayScript ) );

	public static readonly Schedule Cower = new( "Cower",
		0,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.PlaySequence, (float)Activity.Cower ) );

	public static readonly Schedule TakeCoverFromOrigin = new( "TakeCoverFromOrigin",
		BaseMonster.ConditionNewEnemy,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.FindCoverFromOrigin ),
		new( TaskId.RunPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.Remember, BaseMonster.MemoryInCover ),
		new( TaskId.TurnLeft, 179f ) );

	public static readonly Schedule TakeCoverFromBestSound = new( "TakeCoverFromBestSound",
		BaseMonster.ConditionNewEnemy,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.FindCoverFromBestSound ),
		new( TaskId.RunPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.Remember, BaseMonster.MemoryInCover ),
		new( TaskId.TurnLeft, 179f ) );

	public static readonly Schedule TakeCoverFromEnemy = new( "tlTakeCoverFromEnemy",
		BaseMonster.ConditionNewEnemy,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.Wait, 0.2f ),
		new( TaskId.FindCoverFromEnemy ),
		new( TaskId.RunPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.Remember, BaseMonster.MemoryInCover ),
		new( TaskId.FaceEnemy ),
		new( TaskId.Wait, 1f ) );

	public static readonly Schedule[] List =
	[
		IdleStand,
		IdleTrigger,
		IdleWalk,
		Ambush,
		ActiveIdle,
		WakeAngry,
		AlertFace,
		AlertSmallFlinch,
		AlertStand,
		InvestigateSound,
		CombatStand,
		CombatFace,
		Standoff,
		ArmWeapon,
		Reload,
		RangeAttack1,
		RangeAttack2,
		PrimaryMeleeAttack,
		SecondaryMeleeAttack,
		SpecialAttack1,
		SpecialAttack2,
		ChaseEnemy,
		ChaseEnemyFailed,
		SmallFlinch,
		Die,
		VictoryDance,
		BarnacleVictimGrab,
		BarnacleVictimChomp,
		Error,
		WalkToScript,
		RunToScript,
		WaitScript,
		FaceScript,
		Cower,
		TakeCoverFromOrigin,
		TakeCoverFromBestSound,
		TakeCoverFromEnemy,
		Fail
	];
}

partial class BaseMonster
{
	protected Schedule ScheduleInList( string name, Schedule[] list )
	{
		if ( name is null )
		{
			Util.Alert( AlertType.Console, $"{ClassName} set to unnamed schedule!\n" );
			return null;
		}

		foreach ( var schedule in list )
		{
			if ( schedule.Name is null )
			{
				Util.Alert( AlertType.Console, "Unnamed schedule!\n" );
				continue;
			}

			if ( name.Equals( schedule.Name, StringComparison.OrdinalIgnoreCase ) )
				return schedule;
		}

		return null;
	}

	public virtual Schedule ScheduleFromName( string name ) => ScheduleInList( name, DefaultAi.List );

	public virtual Schedule GetScheduleOfType( int type )
	{
		switch ( type )
		{
			case ScheduleType.AiScript:
				if ( Cine is null )
				{
					Util.Alert( AlertType.AiConsole, $"Script failed for {ClassName}\n" );
					CineCleanup();
					return GetScheduleOfType( ScheduleType.IdleStand );
				}

				switch ( Cine.MoveTo )
				{
					case 0:
					case 4:
						return DefaultAi.WaitScript;
					case 1:
						return DefaultAi.WalkToScript;
					case 2:
						return DefaultAi.RunToScript;
					case 5:
						return DefaultAi.FaceScript;
				}

				return null;

			case ScheduleType.IdleStand:
				return Server.RandomLong( 0, 14 ) == 0 && CanActiveIdle() ? DefaultAi.ActiveIdle : DefaultAi.IdleStand;

			case ScheduleType.IdleWalk:
				return DefaultAi.IdleWalk;

			case ScheduleType.WaitTrigger:
				return DefaultAi.IdleTrigger;

			case ScheduleType.WakeAngry:
				return DefaultAi.WakeAngry;

			case ScheduleType.AlertFace:
				return DefaultAi.AlertFace;

			case ScheduleType.AlertStand:
				return DefaultAi.AlertStand;

			case ScheduleType.CombatStand:
				return DefaultAi.CombatStand;

			case ScheduleType.CombatFace:
				return DefaultAi.CombatFace;

			case ScheduleType.ChaseEnemy:
				return DefaultAi.ChaseEnemy;

			case ScheduleType.ChaseEnemyFailed:
				return DefaultAi.Fail;

			case ScheduleType.SmallFlinch:
				return DefaultAi.SmallFlinch;

			case ScheduleType.AlertSmallFlinch:
				return DefaultAi.AlertSmallFlinch;

			case ScheduleType.Reload:
				return DefaultAi.Reload;

			case ScheduleType.ArmWeapon:
				return DefaultAi.ArmWeapon;

			case ScheduleType.Standoff:
				return DefaultAi.Standoff;

			case ScheduleType.RangeAttack1:
				return DefaultAi.RangeAttack1;

			case ScheduleType.RangeAttack2:
				return DefaultAi.RangeAttack2;

			case ScheduleType.MeleeAttack1:
				return DefaultAi.PrimaryMeleeAttack;

			case ScheduleType.MeleeAttack2:
				return DefaultAi.SecondaryMeleeAttack;

			case ScheduleType.SpecialAttack1:
				return DefaultAi.SpecialAttack1;

			case ScheduleType.SpecialAttack2:
				return DefaultAi.SpecialAttack2;

			case ScheduleType.TakeCoverFromBestSound:
				return DefaultAi.TakeCoverFromBestSound;

			case ScheduleType.TakeCoverFromEnemy:
				return DefaultAi.TakeCoverFromEnemy;

			case ScheduleType.Cower:
				return DefaultAi.Cower;

			case ScheduleType.Ambush:
				return DefaultAi.Ambush;

			case ScheduleType.BarnacleVictimGrab:
				return DefaultAi.BarnacleVictimGrab;

			case ScheduleType.BarnacleVictimChomp:
				return DefaultAi.BarnacleVictimChomp;

			case ScheduleType.InvestigateSound:
				return DefaultAi.InvestigateSound;

			case ScheduleType.Die:
				return DefaultAi.Die;

			case ScheduleType.TakeCoverFromOrigin:
				return DefaultAi.TakeCoverFromOrigin;

			case ScheduleType.VictoryDance:
				return DefaultAi.VictoryDance;

			case ScheduleType.Fail:
				return DefaultAi.Fail;

			default:
				Util.Alert( AlertType.Console, $"GetScheduleOfType()\nNo CASE for Schedule Type {type}!\n" );
				return DefaultAi.IdleStand;
		}
	}
}
