using GoldSrc;

namespace HalfLife;

partial class BaseMonster
{
	public override bool BecomeProne()
	{
		EntityFlags &= ~EntityFlags.OnGround;
		IdealMonsterState = MonsterState.Prone;

		return true;
	}

	public virtual bool CheckRangeAttack1( float dot, float distance ) => distance > 64f && distance <= 784f && dot >= 0.5;

	public virtual bool CheckRangeAttack2( float dot, float distance ) => distance > 64f && distance <= 512f && dot >= 0.5;

	public virtual bool CheckMeleeAttack1( float dot, float distance )
	{
		return distance <= 64f && dot >= 0.7 && EnemyEntity is { } enemy && (enemy.EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround;
	}

	public virtual bool CheckMeleeAttack2( float dot, float distance ) => distance <= 64f && dot >= 0.7;

	public void CheckAttacks( BaseEntity target, float distance )
	{
		Server.Globals.MakeVectors( Angles );

		var lineOfSight = MathLib.Normalize2D( target.Origin - Origin );
		var forward = Server.Globals.Forward;
		var dot = (lineOfSight.x * forward.x) + (lineOfSight.y * forward.y);

		ClearConditions( ConditionCanAttack );

		if ( (Capability & CapabilityRangeAttack1) != 0 && CheckRangeAttack1( dot, distance ) )
			SetConditions( ConditionCanRangeAttack1 );

		if ( (Capability & CapabilityRangeAttack2) != 0 && CheckRangeAttack2( dot, distance ) )
			SetConditions( ConditionCanRangeAttack2 );

		if ( (Capability & CapabilityMeleeAttack1) != 0 && CheckMeleeAttack1( dot, distance ) )
			SetConditions( ConditionCanMeleeAttack1 );

		if ( (Capability & CapabilityMeleeAttack2) != 0 && CheckMeleeAttack2( dot, distance ) )
			SetConditions( ConditionCanMeleeAttack2 );
	}

	public virtual bool CanCheckAttacks() => HasConditions( ConditionSeeEnemy ) && !HasConditions( ConditionEnemyTooFar );

	public virtual bool CheckEnemy( BaseEntity enemy )
	{
		var updatedLastKnownPosition = false;

		ClearConditions( ConditionEnemyFacingMe );

		if ( !IsVisible( enemy ) )
			SetConditions( ConditionEnemyOccluded );
		else
			ClearConditions( ConditionEnemyOccluded );

		if ( !enemy.IsAlive )
		{
			SetConditions( ConditionEnemyDead );
			ClearConditions( ConditionSeeEnemy | ConditionEnemyOccluded );
			return false;
		}

		var enemyPosition = enemy.Origin;
		var distance = MathLib.Length( enemyPosition - Origin );

		enemyPosition.z += enemy.Size.z * 0.5f;

		var distance2 = MathLib.Length( enemyPosition - Origin );

		if ( distance2 < distance )
		{
			distance = distance2;
		}
		else
		{
			enemyPosition.z -= enemy.Size.z;
			distance2 = MathLib.Length( enemyPosition - Origin );

			if ( distance2 < distance )
				distance = distance2;
		}

		if ( HasConditions( ConditionSeeEnemy ) )
		{
			updatedLastKnownPosition = true;
			EnemyLastKnownPosition = enemy.Origin;

			if ( enemy is BaseMonster enemyMonster )
			{
				if ( enemyMonster.IsInViewCone( this ) )
					SetConditions( ConditionEnemyFacingMe );
				else
					ClearConditions( ConditionEnemyFacingMe );
			}

			if ( !MathLib.VectorCompare( enemy.Velocity, Vector3.Zero ) )
				EnemyLastKnownPosition -= enemy.Velocity * Server.RandomFloat( -0.05f, 0f );
		}
		else if ( !HasConditions( ConditionEnemyOccluded | ConditionSeeEnemy ) && distance <= 256f )
		{
			updatedLastKnownPosition = true;
			EnemyLastKnownPosition = enemy.Origin;
		}

		if ( distance >= DistTooFar )
			SetConditions( ConditionEnemyTooFar );
		else
			ClearConditions( ConditionEnemyTooFar );

		if ( CanCheckAttacks() )
			CheckAttacks( EnemyEntity, distance );

		if ( MovementGoal != MoveGoalType.Enemy )
			return updatedLastKnownPosition;

		for ( var i = RouteIndex; i < RouteSize; i++ )
		{
			if ( Route[i].Type == (MoveFlag.IsGoal | MoveFlag.ToEnemy) && MathLib.Length( Route[i].Location - EnemyLastKnownPosition ) > 80f )
			{
				RefreshRoute();
				return updatedLastKnownPosition;
			}
		}

		return updatedLastKnownPosition;
	}

	public void PushEnemy( BaseEntity enemy, Vector3 lastKnownPosition )
	{
		if ( enemy is null )
			return;

		int i;

		for ( i = 0; i < MaxOldEnemies; i++ )
		{
			if ( OldEnemy[i] == enemy )
				return;

			if ( !OldEnemy[i].IsValid() )
				break;
		}

		if ( i >= MaxOldEnemies )
			return;

		OldEnemy[i] = enemy;
		OldEnemyPosition[i] = lastKnownPosition;
	}

	public bool PopEnemy()
	{
		for ( var i = MaxOldEnemies - 1; i >= 0; i-- )
		{
			if ( !OldEnemy[i].IsValid() )
				continue;

			if ( OldEnemy[i].IsAlive )
			{
				EnemyEntity = OldEnemy[i];
				EnemyLastKnownPosition = OldEnemyPosition[i];
				return true;
			}

			OldEnemy[i] = null;
		}

		return false;
	}

	public virtual void SetActivity( Activity activity )
	{
		var sequence = LookupActivity( (int)activity );

		if ( sequence > Animation.ActivityNotAvailable )
		{
			if ( Sequence != sequence || !SequenceLoops )
			{
				if ( !(Activity is Activity.Walk or Activity.Run) || !(activity is Activity.Walk or Activity.Run) )
					Frame = 0f;
			}

			Sequence = sequence;
			ResetSequenceInfo();
			SetYawSpeed();
		}
		else
		{
			Util.Alert( AlertType.AiConsole, $"{ClassName} has no sequence for act:{(int)activity}\n" );
			Sequence = 0;
		}

		Activity = activity;
		IdealActivity = Activity;
	}

	public void SetSequenceByName( string name )
	{
		var sequence = LookupSequence( name );

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
			Util.Alert( AlertType.AiConsole, $"{ClassName} has no sequence named:{name}\n" );
			Sequence = 0;
		}
	}
}
