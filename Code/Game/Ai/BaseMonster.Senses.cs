using GoldSrc;

namespace HalfLife;

partial class BaseMonster
{
	public void Eat( float fullDuration ) => HungryTime = Server.Globals.Time + fullDuration;

	public bool ShouldEat() => HungryTime <= Server.Globals.Time;

	public virtual void BarnacleVictimBitten( BaseEntity barnacle )
	{
		if ( GetScheduleOfType( ScheduleType.BarnacleVictimChomp ) is { } schedule )
			ChangeSchedule( schedule );
	}

	public virtual void BarnacleVictimReleased()
	{
		IdealMonsterState = MonsterState.Idle;

		Velocity = Vector3.Zero;
		MoveType = MoveType.Step;
	}

	public void Listen()
	{
		AudibleList = SoundEnt.ListEmpty;
		ClearConditions( ConditionHearSound | ConditionSmell | ConditionSmellFood );
		SoundTypes = 0;

		var mySounds = SoundMask();

		if ( CurrentSchedule is not null )
			mySounds &= CurrentSchedule.SoundMask;

		var hearingSensitivity = HearingSensitivity;

		for ( var index = SoundEnt.ActiveList; index != SoundEnt.ListEmpty; )
		{
			var sound = SoundEnt.SoundPointerForIndex( index );

			if ( sound is not null && (sound.Type & mySounds) != 0 && MathLib.Length( sound.Origin - EarPosition ) <= sound.Volume * hearingSensitivity )
			{
				sound.NextAudible = AudibleList;

				if ( sound.IsSound )
				{
					SetConditions( ConditionHearSound );
				}
				else
				{
					if ( (sound.Type & (WorldSound.Meat | WorldSound.Carcass)) != 0 )
						SetConditions( ConditionSmellFood );

					SetConditions( ConditionSmell );
				}

				SoundTypes |= sound.Type;
				AudibleList = index;
			}

			index = sound.Next;
		}
	}

	public float SoundVolume( WorldSound sound ) => sound.Volume - MathLib.Length( sound.Origin - Origin );

	public virtual bool ValidateHintType( short hint ) => false;

	public virtual void Look( int distance )
	{
		var sighted = 0;

		ClearConditions( ConditionSeeHate | ConditionSeeDislike | ConditionSeeEnemy | ConditionSeeFear | ConditionSeeNemesis | ConditionSeeClient );

		NextLink = null;

		if ( (SpawnFlags & SpawnFlagPrisoner) == 0 )
		{
			var list = new BaseEntity[100];
			var delta = new Vector3( distance, distance, distance );
			var count = Util.EntitiesInBox( list, Origin - delta, Origin + delta, EntityFlags.Client | EntityFlags.Monster );

			for ( var i = 0; i < count; i++ )
			{
				var sight = list[i];

				if ( sight == this || (sight.SpawnFlags & SpawnFlagPrisoner) != 0 || sight.Health <= 0f )
					continue;

				if ( Relationship( sight ) == RelationshipNone || !IsInViewCone( sight ) || (sight.EntityFlags & EntityFlags.NoTarget) == EntityFlags.NoTarget || !IsVisible( sight ) )
					continue;

				if ( sight.IsPlayer )
				{
					if ( (SpawnFlags & SpawnFlagWaitTillSeen) != 0 )
					{
						if ( !((BaseMonster)sight).IsInViewCone( this ) )
							continue;

						SpawnFlags &= ~SpawnFlagWaitTillSeen;
					}

					sighted |= ConditionSeeClient;
				}

				sight.NextLink = NextLink;
				NextLink = sight;

				if ( sight == EnemyEntity )
					sighted |= ConditionSeeEnemy;

				switch ( Relationship( sight ) )
				{
					case RelationshipNemesis:
						sighted |= ConditionSeeNemesis;
						break;
					case RelationshipHate:
						sighted |= ConditionSeeHate;
						break;
					case RelationshipDislike:
						sighted |= ConditionSeeDislike;
						break;
					case RelationshipFear:
						sighted |= ConditionSeeFear;
						break;
					case RelationshipAlly:
						break;
					default:
						Util.Alert( AlertType.AiConsole, $"{ClassName} can't assess {sight.ClassName}\n" );
						break;
				}
			}
		}

		SetConditions( sighted );
	}

	public virtual int SoundMask() => WorldSound.World | WorldSound.Combat | WorldSound.Player;

	public virtual float HearingSensitivity => 1f;

	public virtual WorldSound BestSound()
	{
		var best = -1;
		var bestDistance = 8192f;

		if ( AudibleList == SoundEnt.ListEmpty )
		{
			Util.Alert( AlertType.AiConsole, $"ERROR! monster {ClassName} has no audible sounds!\n" );
			return null;
		}

		for ( var index = AudibleList; index != SoundEnt.ListEmpty; )
		{
			var sound = SoundEnt.SoundPointerForIndex( index );

			if ( sound is not null && sound.IsSound )
			{
				var distance = MathLib.Length( sound.Origin - EarPosition );

				if ( distance < bestDistance )
				{
					best = index;
					bestDistance = distance;
				}
			}

			index = sound.NextAudible;
		}

		return best >= 0 ? SoundEnt.SoundPointerForIndex( best ) : null;
	}

	public virtual WorldSound BestScent()
	{
		var best = -1;
		var bestDistance = 8192f;

		if ( AudibleList == SoundEnt.ListEmpty )
		{
			Util.Alert( AlertType.AiConsole, "ERROR! PBestScent() has empty soundlist!\n" );
			return null;
		}

		for ( var index = AudibleList; index != SoundEnt.ListEmpty; )
		{
			var sound = SoundEnt.SoundPointerForIndex( index );

			if ( sound.IsScent )
			{
				var distance = MathLib.Length( sound.Origin - Origin );

				if ( distance < bestDistance )
				{
					best = index;
					bestDistance = distance;
				}
			}

			index = sound.NextAudible;
		}

		return best >= 0 ? SoundEnt.SoundPointerForIndex( best ) : null;
	}

	public virtual void MonsterThink()
	{
		NextThink = (float)(Server.Globals.Time + 0.1);

		RunAi();

		var interval = StudioFrameAdvance();

		if ( MonsterState != MonsterState.Script && MonsterState != MonsterState.Dead && Activity == Activity.Idle && SequenceFinished )
		{
			var sequence = SequenceLoops ? LookupActivity( (int)Activity ) : LookupActivityHeaviest( (int)Activity );

			if ( sequence != Animation.ActivityNotAvailable )
			{
				Sequence = sequence;
				ResetSequenceInfo();
			}
		}

		DispatchAnimEvents( interval );

		if ( !MovementIsComplete )
			Move( interval );
	}

	public void CallMonsterThink() => MonsterThink();

	public void MonsterUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		IdealMonsterState = MonsterState.Alert;
	}

	public virtual int IgnoreConditions()
	{
		var ignore = 0;

		if ( !ShouldEat() )
			ignore |= ConditionSmellFood;

		if ( MonsterState == MonsterState.Script && Cine is not null )
			ignore |= Cine.IgnoreConditions();

		return ignore;
	}
}
