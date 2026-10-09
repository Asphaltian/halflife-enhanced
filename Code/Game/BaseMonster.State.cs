using GoldSrc;

namespace HalfLife;

partial class BaseMonster
{
	public void SetState( MonsterState state )
	{
		if ( state == MonsterState.Idle && EnemyEntity is not null )
		{
			EnemyEntity = null;
			Util.Alert( AlertType.AiConsole, "Stripped" );
		}

		MonsterState = state;
		IdealMonsterState = state;
	}

	public virtual void RunAi()
	{
		if ( MonsterState is MonsterState.Idle or MonsterState.Alert && Server.RandomLong( 0, 99 ) == 0 && ((int)EntityFlags & SpawnFlagGag) == 0 )
			IdleSound();

		if ( MonsterState is not (MonsterState.None or MonsterState.Prone or MonsterState.Dead) )
		{
			if ( !Util.IsNullEnt( Server.FindClientInPvs( this ) ) || MonsterState == MonsterState.Combat )
			{
				Look( (int)DistLook );
				Listen();

				ClearConditions( IgnoreConditions() );

				GetEnemy();
			}

			if ( EnemyEntity is { } enemy )
				CheckEnemy( enemy );

			CheckAmmo();
		}

		CheckAiTrigger();

		PrescheduleThink();

		MaintainSchedule();

		ClearConditions( ConditionLightDamage | ConditionHeavyDamage );
	}

	public virtual MonsterState GetIdealState()
	{
		var conditions = ScheduleFlags();

		switch ( MonsterState )
		{
			case MonsterState.Idle:
				if ( (conditions & ConditionNewEnemy) != 0 )
				{
					IdealMonsterState = MonsterState.Combat;
				}
				else if ( (conditions & (ConditionLightDamage | ConditionHeavyDamage)) != 0 )
				{
					MakeIdealYaw( EnemyLastKnownPosition );
					IdealMonsterState = MonsterState.Alert;
				}
				else if ( (conditions & ConditionHearSound) != 0 )
				{
					if ( BestSound() is { } sound )
					{
						MakeIdealYaw( sound.Origin );

						if ( (sound.Type & (WorldSound.Combat | WorldSound.Danger)) != 0 )
							IdealMonsterState = MonsterState.Alert;
					}
				}
				else if ( (conditions & (ConditionSmell | ConditionSmellFood)) != 0 )
				{
					IdealMonsterState = MonsterState.Alert;
				}

				break;

			case MonsterState.Alert:
				if ( (conditions & (ConditionNewEnemy | ConditionSeeEnemy)) != 0 )
				{
					IdealMonsterState = MonsterState.Combat;
				}
				else if ( (conditions & ConditionHearSound) != 0 )
				{
					IdealMonsterState = MonsterState.Alert;

					if ( BestSound() is { } sound )
						MakeIdealYaw( sound.Origin );
				}

				break;

			case MonsterState.Combat:
				if ( EnemyEntity is null )
				{
					IdealMonsterState = MonsterState.Alert;
					Util.Alert( AlertType.AiConsole, "***Combat state with no enemy!" );
				}

				break;

			case MonsterState.Script:
				if ( (conditions & (ConditionTaskFailed | ConditionLightDamage | ConditionHeavyDamage)) != 0 )
					ExitScriptedSequence();

				break;

			case MonsterState.Dead:
				IdealMonsterState = MonsterState.Dead;
				break;
		}

		return IdealMonsterState;
	}
}
