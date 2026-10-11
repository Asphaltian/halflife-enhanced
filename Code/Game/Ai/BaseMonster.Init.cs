using System;
using GoldSrc;

namespace HalfLife;

public static class ScriptEvent
{
	public const int Dead = 1000;
	public const int NoInterrupt = 1001;
	public const int CanInterrupt = 1002;
	public const int FireEvent = 1003;
	public const int Sound = 1004;
	public const int Sentence = 1005;
	public const int InAir = 1006;
	public const int EndAnimation = 1007;
	public const int SoundVoice = 1008;
	public const int SentenceRandom1 = 1009;
	public const int NotDead = 1010;
}

public static class MonsterEventId
{
	public const int Specific = 0;
	public const int Scripted = 1000;
	public const int Shared = 2000;
	public const int Client = 5000;

	public const int BodyDropLight = 2001;
	public const int BodyDropHeavy = 2002;
	public const int SwishSound = 2010;
}

partial class BaseMonster
{
	public const int SpawnFlagFallToGround = unchecked((int)0x80000000);

	private const int CoverChecks = 5;
	private const float CoverDelta = 48f;

	[Save( FieldType.ClassPtr )]
	public CineMonster Cine;

	public virtual void SetYawSpeed()
	{
	}

	public virtual void CheckAmmo()
	{
	}

	public virtual void PrescheduleThink()
	{
	}

	public virtual void ScheduleChange()
	{
	}

	public virtual void StopFollowing( bool clearSchedule )
	{
	}

	public override bool IsAllowedToSpeak => IsAlive;

	public virtual bool CanPlaySentence( bool disregardState ) => IsAllowedToSpeak;

	public virtual void MonsterInit()
	{
		if ( !HalfLifeDll.GameRules.AllowMonsters() )
		{
			EntityFlags |= EntityFlags.KillMe;
			return;
		}

		Effects = 0;
		DamageMode = DamageAim;
		IdealYaw = Angles.y;
		MaxHealth = Health;
		DeadFlag = DeadNo;
		IdealMonsterState = MonsterState.Idle;

		IdealActivity = Activity.Idle;

		EntityFlags |= EntityFlags.Monster;

		if ( (SpawnFlags & SpawnFlagHitMonsterClip) != 0 )
			EntityFlags |= EntityFlags.MonsterClip;

		ClearSchedule();
		RouteClear();
		InitBoneControllers();

		HintNode = Graph.NoNode;

		_memory = 0;

		EnemyEntity = null;

		DistTooFar = 1024f;
		DistLook = 2048f;

		SetEyePosition();

		SetThink( MonsterInitThink );
		NextThink = (float)(Server.Globals.Time + 0.1);
		SetUse( MonsterUse );
	}

	public void MonsterInitThink() => StartMonster();

	public virtual void StartMonster()
	{
		if ( HasActivity( Activity.RangeAttack1 ) )
			Capability |= CapabilityRangeAttack1;

		if ( HasActivity( Activity.RangeAttack2 ) )
			Capability |= CapabilityRangeAttack2;

		if ( HasActivity( Activity.MeleeAttack1 ) )
			Capability |= CapabilityMeleeAttack1;

		if ( HasActivity( Activity.MeleeAttack2 ) )
			Capability |= CapabilityMeleeAttack2;

		if ( MoveType != MoveType.Fly && (SpawnFlags & SpawnFlagFallToGround) == 0 )
		{
			Origin += new Vector3( 0f, 0f, 1f );
			Server.DropToFloor( this );

			if ( !Server.WalkMove( this, 0f, 0f, WalkMoveMode.Normal ) )
			{
				Util.Alert( AlertType.Error, $"Monster {ClassName} stuck in wall--level design error" );
				Effects = Effects.BrightField;
			}
		}
		else
		{
			EntityFlags &= ~EntityFlags.OnGround;
		}

		if ( !string.IsNullOrEmpty( Target ) )
		{
			GoalEntity = (Server.FindEntityByString( null, "targetname", Target ) ?? Server.GetEdict( 0 )) as BaseEntity;

			if ( GoalEntity is null )
			{
				Util.Alert( AlertType.Error, $"ReadyMonster()--{ClassName} couldn't find target {Target}" );
			}
			else
			{
				MakeIdealYaw( GoalEntity.Origin );

				MovementGoal = MoveGoalType.PathCorner;
				MovementActivity = MoveType == MoveType.Fly ? Activity.Fly : Activity.Walk;

				if ( !RefreshRoute() )
					Util.Alert( AlertType.AiConsole, "Can't Create Route!" );

				SetState( MonsterState.Idle );
				ChangeSchedule( GetScheduleOfType( ScheduleType.IdleWalk ) );
			}
		}

		SetThink( CallMonsterThink );
		NextThink += Server.RandomFloat( 0.1f, 0.4f );

		if ( !string.IsNullOrEmpty( TargetName ) )
		{
			SetState( MonsterState.Idle );
			SetActivity( Activity.Idle );
			ChangeSchedule( GetScheduleOfType( ScheduleType.WaitTrigger ) );
		}
	}

	public virtual BaseEntity BestVisibleEnemy()
	{
		var nearest = 8192;
		var bestRelationship = RelationshipNone;
		BaseEntity best = null;

		for ( var next = NextLink; next is not null; next = next.NextLink )
		{
			if ( !next.IsAlive )
				continue;

			if ( Relationship( next ) > bestRelationship )
			{
				bestRelationship = Relationship( next );
				nearest = (int)MathLib.Length( next.Origin - Origin );
				best = next;
			}
			else if ( Relationship( next ) == bestRelationship )
			{
				var distance = (int)MathLib.Length( next.Origin - Origin );

				if ( distance <= nearest )
				{
					nearest = distance;
					bestRelationship = Relationship( next );
					best = next;
				}
			}
		}

		return best;
	}

	public float YawDiff()
	{
		var currentYaw = Util.AngleMod( Angles.y );

		return currentYaw == IdealYaw ? 0f : Util.AngleDiff( IdealYaw, currentYaw );
	}

	public virtual float ChangeYaw( int yawSpeed )
	{
		var current = Util.AngleMod( Angles.y );
		var ideal = IdealYaw;

		if ( current == ideal )
			return 0f;

		if ( LastYawTime == 0f )
			LastYawTime = Server.Globals.Time - Server.Globals.FrameTime;

		var delta = Server.Globals.Time - LastYawTime;

		LastYawTime = Server.Globals.Time;

		if ( delta > 0.25f )
			delta = 0.25f;

		var speed = yawSpeed * delta * 2f;
		var move = ideal - current;

		if ( ideal > current )
		{
			if ( move >= 180f )
				move -= 360f;
		}
		else if ( move <= -180f )
		{
			move += 360f;
		}

		if ( move > 0f )
		{
			if ( move > speed )
				move = speed;
		}
		else if ( move < -speed )
		{
			move = -speed;
		}

		Angles = new Vector3( Angles.x, Util.AngleMod( current + move ), Angles.z );

		if ( (Capability & CapabilityTurnHead) != 0 )
		{
			var yaw = IdealYaw - Angles.y;

			if ( yaw > 180f )
				yaw -= 360f;

			if ( yaw < -180f )
				yaw += 360f;

			SetBoneController( 0, yaw );
		}

		return move;
	}

	public float VecToYaw( Vector3 direction ) => MathLib.VectorCompare( direction, Vector3.Zero ) ? Angles.y : MathLib.VecToYaw( direction );

	public void SetEyePosition()
	{
		var eyePosition = Vector3.Zero;

		Animation.GetEyePosition( Server.GetModelPtr( this ), ref eyePosition );

		ViewOffset = eyePosition;

		if ( MathLib.VectorCompare( ViewOffset, Vector3.Zero ) )
			Util.Alert( AlertType.AiConsole, $"{ClassName} has no view_ofs!" );
	}

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		switch ( monsterEvent.Event )
		{
			case ScriptEvent.Dead:
				if ( MonsterState == MonsterState.Script )
				{
					DeadFlag = DeadDying;
					Health = 0f;
				}

				break;

			case ScriptEvent.NotDead:
				if ( MonsterState == MonsterState.Script )
				{
					DeadFlag = DeadNo;
					Health = MaxHealth;
				}

				break;

			case ScriptEvent.Sound:
				Server.EmitSound( this, SoundChannel.Body, monsterEvent.Options, 1f, Server.AttenuationIdle );
				break;

			case ScriptEvent.SoundVoice:
				Server.EmitSound( this, SoundChannel.Voice, monsterEvent.Options, 1f, Server.AttenuationIdle );
				break;

			case ScriptEvent.SentenceRandom1:
				if ( Server.RandomLong( 0, 2 ) != 0 )
					Sentences.PlayRandom( this, monsterEvent.Options, 1f, Server.AttenuationIdle, 0, 100 );

				break;

			case ScriptEvent.Sentence:
				Sentences.PlayRandom( this, monsterEvent.Options, 1f, Server.AttenuationIdle, 0, 100 );
				break;

			case ScriptEvent.FireEvent:
				FireTargets( monsterEvent.Options, this, this, UseType.Toggle, 0f );
				break;

			case ScriptEvent.NoInterrupt:
				Cine?.AllowInterrupt( false );
				break;

			case ScriptEvent.CanInterrupt:
				Cine?.AllowInterrupt( true );
				break;

			case MonsterEventId.BodyDropHeavy:
				if ( (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround )
					Server.EmitSound( this, SoundChannel.Body, Server.RandomLong( 0, 1 ) == 0 ? "common/bodydrop3.wav" : "common/bodydrop4.wav", 1f, Server.AttenuationNormal, 0, 90 );

				break;

			case MonsterEventId.BodyDropLight:
				if ( (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround )
					Server.EmitSound( this, SoundChannel.Body, Server.RandomLong( 0, 1 ) == 0 ? "common/bodydrop3.wav" : "common/bodydrop4.wav", 1f, Server.AttenuationNormal );

				break;

			case MonsterEventId.SwishSound:
				Server.EmitSound( this, SoundChannel.Body, "zombie/claw_miss2.wav", 1f, Server.AttenuationNormal );
				break;

			default:
				Util.Alert( AlertType.AiConsole, $"Unhandled animation event {monsterEvent.Event} for {ClassName}" );
				break;
		}
	}

	public bool GetNodeRoute( Vector3 destination )
	{
		var graph = HalfLifeDll.WorldGraph;
		var path = new int[Graph.MaxPathSize];
		var sourceNode = graph.FindNearestNode( Origin, this );
		var destinationNode = graph.FindNearestNode( destination, this );

		if ( sourceNode == -1 || destinationNode == -1 )
			return false;

		var result = graph.FindShortestPath( path, sourceNode, destinationNode, graph.HullIndex( this ), Capability );

		if ( result == 0 )
		{
			Util.Alert( AlertType.AiConsole, $"No Path from {sourceNode} to {destinationNode}!" );
			return false;
		}

		var count = Math.Min( result, RouteSize );

		for ( var i = 0; i < count; i++ )
		{
			Route[i].Location = graph.Nodes[path[i]].Origin;
			Route[i].Type = MoveFlag.ToNode;
		}

		if ( count < RouteSize )
		{
			Route[count].Location = destination;
			Route[count].Type |= MoveFlag.IsGoal;
		}

		return true;
	}

	public int FindHintNode()
	{
		var graph = HalfLifeDll.WorldGraph;

		if ( !graph.GraphPresent )
		{
			Util.Alert( AlertType.AiConsole, "find_hintnode: graph not ready!" );
			return Graph.NoNode;
		}

		if ( graph.LastActiveIdleSearch >= graph.NodeCount )
			graph.LastActiveIdleSearch = 0;

		for ( var i = 0; i < graph.NodeCount; i++ )
		{
			var nodeNumber = (i + graph.LastActiveIdleSearch) % graph.NodeCount;
			var node = graph.Nodes[nodeNumber];

			if ( node.HintType == 0 || !ValidateHintType( node.HintType ) )
				continue;

			if ( node.HintActivity != 0 && LookupActivity( node.HintActivity ) == Animation.ActivityNotAvailable )
				continue;

			var trace = Util.TraceLine( Origin + ViewOffset, node.Origin + ViewOffset, IgnoreMonsters, this );

			if ( trace.Fraction == 1f )
			{
				graph.LastActiveIdleSearch = nodeNumber + 1;
				return nodeNumber;
			}
		}

		graph.LastActiveIdleSearch = 0;

		return Graph.NoNode;
	}

	public virtual bool CanPlaySequence( bool disregardMonsterState, int interruptLevel )
	{
		if ( Cine is not null || !IsAlive || MonsterState == MonsterState.Prone )
			return false;

		if ( disregardMonsterState )
			return true;

		if ( MonsterState is MonsterState.None or MonsterState.Idle || IdealMonsterState == MonsterState.Idle )
			return true;

		return MonsterState == MonsterState.Alert && interruptLevel >= CineMonster.InterruptByName;
	}

	private bool TryLateralCover( Vector3 threatEyes, Vector3 test )
	{
		var trace = Util.TraceLine( threatEyes, test + ViewOffset, IgnoreMonsters | IgnoreGlass, this );

		return trace.Fraction != 1f
			&& ValidateCover( test )
			&& CheckLocalMove( Origin, test, null, out _ ) == LocalMove.Valid
			&& MoveToLocation( Activity.Run, 0f, test );
	}

	public bool FindLateralCover( Vector3 threat, Vector3 viewOffset )
	{
		Server.Globals.MakeVectors( Angles );

		var stepRight = Server.Globals.Right * CoverDelta;

		stepRight.z = 0f;

		var leftTest = Origin;
		var rightTest = Origin;

		for ( var i = 0; i < CoverChecks; i++ )
		{
			leftTest -= stepRight;
			rightTest += stepRight;

			if ( TryLateralCover( threat + viewOffset, leftTest ) || TryLateralCover( threat + viewOffset, rightTest ) )
				return true;
		}

		return false;
	}

	public Vector3 ShootAtEnemy( Vector3 shootOrigin )
	{
		if ( EnemyEntity is not { } enemy )
			return Server.Globals.Forward;

		return Util.Normalize( enemy.BodyTarget( shootOrigin ) - enemy.Origin + EnemyLastKnownPosition - shootOrigin );
	}

	public bool FacingIdeal() => MathF.Abs( YawDiff() ) <= 0.006;

	public virtual bool CanActiveIdle() => false;

	public void CorpseFallThink()
	{
		if ( (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround )
		{
			SetThink( null );

			SetSequenceBox();
			SetOrigin( Origin );
		}
		else
		{
			NextThink = (float)(Server.Globals.Time + 0.1);
		}
	}

	public virtual void MonsterInitDead()
	{
		InitBoneControllers();

		Solid = Solid.BBox;
		MoveType = MoveType.Toss;

		Frame = 0f;
		ResetSequenceInfo();
		FrameRate = 0f;

		MaxHealth = Health;
		DeadFlag = DeadDead;

		SetSize( Vector3.Zero, Vector3.Zero );
		SetOrigin( Origin );

		BecomeDead();
		SetThink( CorpseFallThink );
		NextThink = (float)(Server.Globals.Time + 0.5);
	}

	private float FloorDistance( float x, float y )
	{
		var point = new Vector3( x, y, Origin.z );
		var trace = Util.TraceLine( point, point - new Vector3( 0f, 0f, 100f ), IgnoreMonsters, this );

		return MathLib.Length( point - trace.EndPos );
	}

	public bool BBoxFlat()
	{
		var sizeX = Size.x / 2f;
		var sizeY = Size.y / 2f;

		var length = FloorDistance( Origin.x + sizeX, Origin.y + sizeY );
		var length2 = FloorDistance( Origin.x - sizeX, Origin.y - sizeY );

		if ( length2 > length )
			return false;

		length = length2;
		length2 = FloorDistance( Origin.x - sizeX, Origin.y + sizeY );

		if ( length2 > length )
			return false;

		length = length2;
		length2 = FloorDistance( Origin.x + sizeX, Origin.y - sizeY );

		return length2 <= length;
	}

	public bool GetEnemy()
	{
		if ( HasConditions( ConditionSeeHate | ConditionSeeDislike | ConditionSeeNemesis ) )
		{
			var newEnemy = BestVisibleEnemy();

			if ( newEnemy != EnemyEntity && newEnemy is not null && CurrentSchedule is not null )
			{
				if ( (CurrentSchedule.InterruptMask & ConditionNewEnemy) != 0 )
				{
					PushEnemy( EnemyEntity, EnemyLastKnownPosition );
					SetConditions( ConditionNewEnemy );

					EnemyEntity = newEnemy;
					EnemyLastKnownPosition = newEnemy.Origin;
				}

				if ( newEnemy.Owner is BaseMonster owner && (owner.EntityFlags & EntityFlags.Monster) == EntityFlags.Monster && Relationship( owner ) != RelationshipNone )
					PushEnemy( owner, EnemyLastKnownPosition );
			}
		}

		if ( EnemyEntity is null && PopEnemy() && CurrentSchedule is not null && (CurrentSchedule.InterruptMask & ConditionNewEnemy) != 0 )
			SetConditions( ConditionNewEnemy );

		return EnemyEntity is not null;
	}

	private static readonly string[] StateNames = ["None", "Idle", "Combat", "Alert", "Hunt", "Prone", "Scripted", "Dead"];

	public virtual void ReportAiState()
	{
		var report = new System.Text.StringBuilder( $"{ClassName}: " );

		if ( (int)MonsterState < StateNames.Length )
			report.Append( $"State: {StateNames[(int)MonsterState]}, " );

		if ( (int)Activity < ActivityMap.Names.Length && ActivityMap.Names[(int)Activity] is { } activityName )
			report.Append( $"Activity {activityName}, " );

		if ( CurrentSchedule is not null )
		{
			report.Append( $"Schedule {CurrentSchedule.Name ?? "Unknown"}, " );

			if ( GetTask() is { } task )
				report.Append( $"Task {task.Id} (#{ScheduleIndex}), " );
		}
		else
		{
			report.Append( "No Schedule, " );
		}

		if ( EnemyEntity is { } enemy )
			report.Append( $"\nEnemy is {enemy.ClassName}" );
		else
			report.Append( "No enemy" );

		if ( IsMoving )
		{
			report.Append( " Moving " );

			if ( MoveWaitFinished > Server.Globals.Time )
				report.Append( $": Stopped for {MoveWaitFinished - Server.Globals.Time:F2}. " );
			else if ( IdealActivity == GetStoppedActivity() )
				report.Append( ": In stopped anim. " );
		}

		if ( this is SquadMonster squadMonster )
			report.Append( $"{(squadMonster.InSquad ? "" : "not ")}In Squad, {(squadMonster.IsLeader ? "" : "not ")}Leader." );

		report.Append( $"\nYaw speed:{YawSpeed:F1},Health: {Health:F1}\n" );

		if ( (SpawnFlags & SpawnFlagPrisoner) != 0 )
			report.Append( " PRISONER! " );

		if ( (SpawnFlags & SpawnFlagPreDisaster) != 0 )
			report.Append( " Pre-Disaster! " );

		Util.Alert( AlertType.Console, report.ToString() );
	}

	public BaseEntity DropItem( string itemName, Vector3 position, Vector3 angles )
	{
		if ( itemName is null )
		{
			Util.Alert( AlertType.Console, "DropItem() - No item name!" );
			return null;
		}

		if ( Create( itemName, position, angles, this ) is not { } item )
		{
			Util.Alert( AlertType.Console, "DropItem() - Didn't create!" );
			return null;
		}

		item.Velocity = Velocity;
		item.AVelocity = new Vector3( 0f, Server.RandomFloat( 0f, 100f ), 0f );

		return item;
	}
}
