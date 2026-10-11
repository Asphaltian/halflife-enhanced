using System;
using GoldSrc;

namespace HalfLife;

partial class BaseMonster
{
	public Schedule CurrentSchedule { get; private set; }

	void IHotloadManaged.Created( IReadOnlyDictionary<string, object> state )
	{
		if ( CurrentSchedule is null )
			return;

		CurrentSchedule = ScheduleFromName( CurrentSchedule.Name );

		if ( CurrentSchedule is null || ScheduleIndex > CurrentSchedule.Tasks.Length )
			ClearSchedule();
	}

	public bool HaveSchedule => CurrentSchedule is not null;

	public bool ScheduleDone => ScheduleIndex == CurrentSchedule.Tasks.Length;

	public bool TaskIsComplete => TaskStatus == TaskState.Complete;

	public bool MovementIsComplete => MovementGoal == MoveGoalType.None;

	public void ClearSchedule()
	{
		TaskStatus = TaskState.New;
		CurrentSchedule = null;
		ScheduleIndex = 0;
	}

	public void ChangeSchedule( Schedule schedule )
	{
		CurrentSchedule = schedule;
		ScheduleIndex = 0;
		TaskStatus = TaskState.New;
		_conditions = 0;
		FailSchedule = ScheduleType.None;

		if ( (schedule.InterruptMask & ConditionHearSound) != 0 && schedule.SoundMask == 0 )
			Util.Alert( AlertType.AiConsole, "COND_HEAR_SOUND with no sound mask!" );
		else if ( schedule.SoundMask != 0 && (schedule.InterruptMask & ConditionHearSound) == 0 )
			Util.Alert( AlertType.AiConsole, "Sound mask without COND_HEAR_SOUND!" );
	}

	public void NextScheduledTask()
	{
		TaskStatus = TaskState.New;
		ScheduleIndex++;

		if ( ScheduleDone )
			SetConditions( ConditionScheduleDone );
	}

	public int ScheduleFlags() => CurrentSchedule is null ? 0 : _conditions & CurrentSchedule.InterruptMask;

	public bool ScheduleValid()
	{
		if ( CurrentSchedule is null )
			return false;

		return !HasConditions( CurrentSchedule.InterruptMask | ConditionScheduleDone | ConditionTaskFailed );
	}

	public void TaskComplete()
	{
		if ( !HasConditions( ConditionTaskFailed ) )
			TaskStatus = TaskState.Complete;
	}

	public void TaskFail() => SetConditions( ConditionTaskFailed );

	public void TaskBegin() => TaskStatus = TaskState.Running;

	public bool TaskIsRunning() => TaskStatus is not (TaskState.Complete or TaskState.RunningMovement);

	public Task? GetTask()
	{
		if ( ScheduleIndex < 0 || ScheduleIndex >= CurrentSchedule.Tasks.Length )
			return null;

		return CurrentSchedule.Tasks[ScheduleIndex];
	}

	public void MaintainSchedule()
	{
		for ( var i = 0; i < 10; i++ )
		{
			if ( CurrentSchedule is not null && TaskIsComplete )
				NextScheduledTask();

			if ( !ScheduleValid() || MonsterState != IdealMonsterState )
			{
				ScheduleChange();

				if ( IdealMonsterState != MonsterState.Dead && (IdealMonsterState != MonsterState.Script || IdealMonsterState == MonsterState) )
				{
					if ( (_conditions != 0 && !HasConditions( ConditionScheduleDone ))
						|| (CurrentSchedule is not null && (CurrentSchedule.InterruptMask & ConditionScheduleDone) != 0)
						|| (MonsterState == MonsterState.Combat && EnemyEntity is null) )
					{
						GetIdealState();
					}
				}

				if ( HasConditions( ConditionTaskFailed ) && MonsterState == IdealMonsterState )
				{
					var schedule = GetScheduleOfType( FailSchedule != ScheduleType.None ? FailSchedule : ScheduleType.Fail );

					Util.Alert( AlertType.AiConsole, $"Schedule Failed at {ScheduleIndex}!" );
					ChangeSchedule( schedule );
				}
				else
				{
					SetState( IdealMonsterState );

					ChangeSchedule( MonsterState is MonsterState.Script or MonsterState.Dead ? GetBaseSchedule() : GetSchedule() );
				}
			}

			if ( TaskStatus == TaskState.New )
			{
				TaskBegin();
				StartTask( GetTask().Value );
			}

			if ( Activity != IdealActivity )
				SetActivity( IdealActivity );

			if ( !TaskIsComplete && TaskStatus != TaskState.New )
				break;
		}

		if ( TaskIsRunning() )
			RunTask( GetTask().Value );

		if ( Activity != IdealActivity )
			SetActivity( IdealActivity );
	}

	private bool HasActivity( Activity activity ) => LookupActivity( (int)activity ) != Animation.ActivityNotAvailable;

	public virtual void RunTask( Task task )
	{
		switch ( task.Id )
		{
			case TaskId.TurnRight:
			case TaskId.TurnLeft:
			case TaskId.FaceHintNode:
			case TaskId.FaceLastPosition:
			case TaskId.FaceTarget:
			case TaskId.FaceIdeal:
			case TaskId.FaceRoute:
				ChangeYaw( (int)YawSpeed );

				if ( FacingIdeal() )
					TaskComplete();

				break;

			case TaskId.PlaySequenceFaceEnemy:
			case TaskId.PlaySequenceFaceTarget:
				{
					var target = task.Id == TaskId.PlaySequenceFaceTarget ? TargetEntity : EnemyEntity;

					if ( target is not null )
					{
						IdealYaw = MathLib.VecToYaw( target.Origin - Origin );
						ChangeYaw( (int)YawSpeed );
					}

					if ( SequenceFinished )
						TaskComplete();

					break;
				}

			case TaskId.PlaySequence:
			case TaskId.PlayActiveIdle:
			case TaskId.SmallFlinch:
				if ( SequenceFinished )
					TaskComplete();

				break;

			case TaskId.FaceEnemy:
				MakeIdealYaw( EnemyLastKnownPosition );
				ChangeYaw( (int)YawSpeed );

				if ( FacingIdeal() )
					TaskComplete();

				break;

			case TaskId.WaitPvs:
				if ( !Util.IsNullEnt( Server.FindClientInPvs( this ) ) )
					TaskComplete();

				break;

			case TaskId.WaitIndefinite:
				break;

			case TaskId.Wait:
			case TaskId.WaitRandom:
				if ( Server.Globals.Time >= WaitFinished )
					TaskComplete();

				break;

			case TaskId.WaitFaceEnemy:
				MakeIdealYaw( EnemyLastKnownPosition );
				ChangeYaw( (int)YawSpeed );

				if ( Server.Globals.Time >= WaitFinished )
					TaskComplete();

				break;

			case TaskId.MoveToTargetRange:
				{
					if ( TargetEntity is not { } target )
					{
						TaskFail();
						break;
					}

					var distance = MathLib.Length2D( MoveGoal - Origin );

					if ( distance < task.Data || MathLib.Length( MoveGoal - target.Origin ) > task.Data * 0.5 )
					{
						MoveGoal = target.Origin;
						distance = MathLib.Length2D( MoveGoal - Origin );
						RefreshRoute();
					}

					if ( distance < task.Data )
					{
						TaskComplete();
						RouteClear();
					}
					else if ( distance < 190f && MovementActivity != Activity.Walk )
					{
						MovementActivity = Activity.Walk;
					}
					else if ( distance >= 270f && MovementActivity != Activity.Run )
					{
						MovementActivity = Activity.Run;
					}

					break;
				}

			case TaskId.WaitForMovement:
				if ( MovementIsComplete )
				{
					TaskComplete();
					RouteClear();
				}

				break;

			case TaskId.Die:
				if ( SequenceFinished && Frame >= 255f )
				{
					DeadFlag = DeadDead;

					SetThink( null );
					StopAnimation();

					if ( !BBoxFlat() )
						SetSize( new Vector3( -4f, -4f, 0f ), new Vector3( 4f, 4f, 1f ) );
					else
						SetSize( Mins, new Vector3( Maxs.x, Maxs.y, Mins.z + 1f ) );

					if ( ShouldFadeOnDeath() )
						SubStartFadeOut();
					else
						SoundEnt.InsertSound( WorldSound.Carcass, Origin, 384, 30f );
				}

				break;

			case TaskId.RangeAttack1NoTurn:
			case TaskId.MeleeAttack1NoTurn:
			case TaskId.MeleeAttack2NoTurn:
			case TaskId.RangeAttack2NoTurn:
			case TaskId.ReloadNoTurn:
				if ( SequenceFinished )
				{
					Activity = Activity.Reset;
					TaskComplete();
				}

				break;

			case TaskId.RangeAttack1:
			case TaskId.MeleeAttack1:
			case TaskId.MeleeAttack2:
			case TaskId.RangeAttack2:
			case TaskId.SpecialAttack1:
			case TaskId.SpecialAttack2:
			case TaskId.Reload:
				MakeIdealYaw( EnemyLastKnownPosition );
				ChangeYaw( (int)YawSpeed );

				if ( SequenceFinished )
				{
					Activity = Activity.Reset;
					TaskComplete();
				}

				break;

			case TaskId.WaitForScript:
				if ( Cine.DelayCount <= 0 && Server.Globals.Time >= Cine.StartTime )
				{
					TaskComplete();
					Cine.StartSequence( this, Cine.Play, true );

					if ( SequenceFinished )
						ClearSchedule();

					FrameRate = 1f;
				}

				break;

			case TaskId.PlayScript:
				if ( SequenceFinished )
					Cine.SequenceDone( this );

				break;
		}
	}

	public void SetTurnActivity()
	{
		var yawDifference = YawDiff();

		if ( yawDifference <= -45f && HasActivity( Activity.TurnRight ) )
			IdealActivity = Activity.TurnRight;
		else if ( yawDifference > 45f && HasActivity( Activity.TurnLeft ) )
			IdealActivity = Activity.TurnLeft;
	}

	private void CompleteIf( bool succeeded )
	{
		if ( succeeded )
			TaskComplete();
		else
			TaskFail();
	}

	private void CompleteIf( bool succeeded, string failure )
	{
		if ( succeeded )
		{
			TaskComplete();
			return;
		}

		Util.Alert( AlertType.AiConsole, failure );
		TaskFail();
	}

	public virtual void StartTask( Task task )
	{
		switch ( task.Id )
		{
			case TaskId.TurnRight:
				IdealYaw = Util.AngleMod( Util.AngleMod( Angles.y ) - task.Data );
				SetTurnActivity();
				break;

			case TaskId.TurnLeft:
				IdealYaw = Util.AngleMod( Util.AngleMod( Angles.y ) + task.Data );
				SetTurnActivity();
				break;

			case TaskId.Remember:
				Remember( (int)task.Data );
				TaskComplete();
				break;

			case TaskId.Forget:
				Forget( (int)task.Data );
				TaskComplete();
				break;

			case TaskId.FindHintNode:
				HintNode = FindHintNode();
				CompleteIf( HintNode != Graph.NoNode );
				break;

			case TaskId.StoreLastPosition:
				LastPosition = Origin;
				TaskComplete();
				break;

			case TaskId.ClearLastPosition:
				LastPosition = Vector3.Zero;
				TaskComplete();
				break;

			case TaskId.ClearHintNode:
				HintNode = Graph.NoNode;
				TaskComplete();
				break;

			case TaskId.StopMoving:
				if ( IdealActivity == MovementActivity )
					IdealActivity = GetStoppedActivity();

				RouteClear();
				TaskComplete();
				break;

			case TaskId.PlaySequenceFaceEnemy:
			case TaskId.PlaySequenceFaceTarget:
			case TaskId.PlaySequence:
				IdealActivity = (Activity)(int)task.Data;
				break;

			case TaskId.PlayActiveIdle:
				IdealActivity = (Activity)HalfLifeDll.WorldGraph.Nodes[HintNode].HintActivity;
				break;

			case TaskId.SetSchedule:
				if ( GetScheduleOfType( (int)task.Data ) is { } schedule )
					ChangeSchedule( schedule );
				else
					TaskFail();

				break;

			case TaskId.FindNearNodeCoverFromEnemy:
				if ( EnemyEntity is not { } nearEnemy )
				{
					TaskFail();
					return;
				}

				CompleteIf( FindCover( nearEnemy.Origin, nearEnemy.ViewOffset, 0f, task.Data ) );
				break;

			case TaskId.FindFarNodeCoverFromEnemy:
				if ( EnemyEntity is not { } farEnemy )
				{
					TaskFail();
					return;
				}

				CompleteIf( FindCover( farEnemy.Origin, farEnemy.ViewOffset, task.Data, CoverRadius ) );
				break;

			case TaskId.FindNodeCoverFromEnemy:
				if ( EnemyEntity is not { } nodeEnemy )
				{
					TaskFail();
					return;
				}

				CompleteIf( FindCover( nodeEnemy.Origin, nodeEnemy.ViewOffset, 0f, CoverRadius ) );
				break;

			case TaskId.FindCoverFromEnemy:
				{
					var cover = EnemyEntity ?? this;

					if ( FindLateralCover( cover.Origin, cover.ViewOffset ) || FindCover( cover.Origin, cover.ViewOffset, 0f, CoverRadius ) )
					{
						MoveWaitFinished = Server.Globals.Time + task.Data;
						TaskComplete();
					}
					else
					{
						TaskFail();
					}

					break;
				}

			case TaskId.FindCoverFromOrigin:
				if ( FindCover( Origin, ViewOffset, 0f, CoverRadius ) )
				{
					MoveWaitFinished = Server.Globals.Time + task.Data;
					TaskComplete();
				}
				else
				{
					TaskFail();
				}

				break;

			case TaskId.FindCoverFromBestSound:
				if ( BestSound() is { } bestSound && FindCover( bestSound.Origin, Vector3.Zero, bestSound.Volume, CoverRadius ) )
				{
					MoveWaitFinished = Server.Globals.Time + task.Data;
					TaskComplete();
				}
				else
				{
					TaskFail();
				}

				break;

			case TaskId.FaceHintNode:
				IdealYaw = HalfLifeDll.WorldGraph.Nodes[HintNode].HintYaw;
				SetTurnActivity();
				break;

			case TaskId.FaceLastPosition:
				MakeIdealYaw( LastPosition );
				SetTurnActivity();
				break;

			case TaskId.FaceTarget:
				if ( TargetEntity is { } faceTarget )
				{
					MakeIdealYaw( faceTarget.Origin );
					SetTurnActivity();
				}
				else
				{
					TaskFail();
				}

				break;

			case TaskId.FaceEnemy:
				MakeIdealYaw( EnemyLastKnownPosition );
				SetTurnActivity();
				break;

			case TaskId.FaceIdeal:
				SetTurnActivity();
				break;

			case TaskId.FaceRoute:
				if ( RouteIsClear() )
				{
					Util.Alert( AlertType.AiConsole, "No route to face!" );
					TaskFail();
				}
				else
				{
					MakeIdealYaw( Route[RouteIndex].Location );
					SetTurnActivity();
				}

				break;

			case TaskId.WaitPvs:
			case TaskId.WaitIndefinite:
				break;

			case TaskId.Wait:
			case TaskId.WaitFaceEnemy:
				WaitFinished = Server.Globals.Time + task.Data;
				break;

			case TaskId.WaitRandom:
				WaitFinished = Server.Globals.Time + Server.RandomFloat( 0.1f, task.Data );
				break;

			case TaskId.MoveToTargetRange:
				if ( MathLib.Length( TargetEntity.Origin - Origin ) < 1f )
				{
					TaskComplete();
				}
				else
				{
					MoveGoal = TargetEntity.Origin;

					if ( !MoveToTarget( Activity.Walk, 2f ) )
						TaskFail();
				}

				break;

			case TaskId.RunToTarget:
			case TaskId.WalkToTarget:
				if ( MathLib.Length( TargetEntity.Origin - Origin ) < 1f )
				{
					TaskComplete();
				}
				else
				{
					var activity = task.Id == TaskId.WalkToTarget ? Activity.Walk : Activity.Run;

					if ( !HasActivity( activity ) )
					{
						TaskComplete();
					}
					else if ( TargetEntity is null || !MoveToTarget( activity, 2f ) )
					{
						TaskFail();
						Util.Alert( AlertType.AiConsole, $"{ClassName} Failed to reach target!!!" );
						RouteClear();
					}
				}

				TaskComplete();
				break;

			case TaskId.ClearMoveWait:
				MoveWaitFinished = Server.Globals.Time;
				TaskComplete();
				break;

			case TaskId.MeleeAttack1NoTurn:
			case TaskId.MeleeAttack1:
				IdealActivity = Activity.MeleeAttack1;
				break;

			case TaskId.MeleeAttack2NoTurn:
			case TaskId.MeleeAttack2:
				IdealActivity = Activity.MeleeAttack2;
				break;

			case TaskId.RangeAttack1NoTurn:
			case TaskId.RangeAttack1:
				IdealActivity = Activity.RangeAttack1;
				break;

			case TaskId.RangeAttack2NoTurn:
			case TaskId.RangeAttack2:
				IdealActivity = Activity.RangeAttack2;
				break;

			case TaskId.ReloadNoTurn:
			case TaskId.Reload:
				IdealActivity = Activity.Reload;
				break;

			case TaskId.SpecialAttack1:
				IdealActivity = Activity.SpecialAttack1;
				break;

			case TaskId.SpecialAttack2:
				IdealActivity = Activity.SpecialAttack2;
				break;

			case TaskId.SetActivity:
				IdealActivity = (Activity)(int)task.Data;
				TaskComplete();
				break;

			case TaskId.GetPathToEnemyLkp:
				CompleteIf( BuildRoute( EnemyLastKnownPosition, MoveFlag.ToLocation, null )
					|| BuildNearestRoute( EnemyLastKnownPosition, ViewOffset, 0f, MathLib.Length( EnemyLastKnownPosition - Origin ) ), "GetPathToEnemyLKP failed!!" );
				break;

			case TaskId.GetPathToEnemy:
				if ( EnemyEntity is not { } pathEnemy )
				{
					TaskFail();
					return;
				}

				CompleteIf( BuildRoute( pathEnemy.Origin, MoveFlag.ToEnemy, pathEnemy )
					|| BuildNearestRoute( pathEnemy.Origin, pathEnemy.ViewOffset, 0f, MathLib.Length( pathEnemy.Origin - Origin ) ), "GetPathToEnemy failed!!" );
				break;

			case TaskId.GetPathToEnemyCorpse:
				Server.Globals.MakeVectors( Angles );
				CompleteIf( BuildRoute( EnemyLastKnownPosition - (Server.Globals.Forward * 64f), MoveFlag.ToLocation, null ), "GetPathToEnemyCorpse failed!!" );
				break;

			case TaskId.GetPathToSpot:
				CompleteIf( BuildRoute( MoveGoal, MoveFlag.ToLocation, Server.FindEntityByString( null, "classname", "player" ) as BaseEntity ), "GetPathToSpot failed!!" );
				break;

			case TaskId.GetPathToTarget:
				RouteClear();
				CompleteIf( TargetEntity is not null && MoveToTarget( MovementActivity, 1f ), "GetPathToSpot failed!!" );
				break;

			case TaskId.GetPathToHintNode:
				CompleteIf( MoveToLocation( MovementActivity, 2f, HalfLifeDll.WorldGraph.Nodes[HintNode].Origin ), "GetPathToHintNode failed!!" );
				break;

			case TaskId.GetPathToLastPosition:
				MoveGoal = LastPosition;
				CompleteIf( MoveToLocation( MovementActivity, 2f, MoveGoal ), "GetPathToLastPosition failed!!" );
				break;

			case TaskId.GetPathToBestSound:
				CompleteIf( BestSound() is { } pathSound && MoveToLocation( MovementActivity, 2f, pathSound.Origin ), "GetPathToBestSound failed!!" );
				break;

			case TaskId.GetPathToBestScent:
				CompleteIf( BestScent() is { } pathScent && MoveToLocation( MovementActivity, 2f, pathScent.Origin ), "GetPathToBestScent failed!!" );
				break;

			case TaskId.RunPath:
				MovementActivity = HasActivity( Activity.Run ) ? Activity.Run : Activity.Walk;
				TaskComplete();
				break;

			case TaskId.WalkPath:
				if ( MoveType == MoveType.Fly )
					MovementActivity = Activity.Fly;

				MovementActivity = HasActivity( Activity.Walk ) ? Activity.Walk : Activity.Run;
				TaskComplete();
				break;

			case TaskId.StrafePath:
				{
					Server.Globals.MakeVectors( Angles );

					var toPoint = MathLib.Normalize2D( Route[0].Location - Origin );
					var rightSide = MathLib.Normalize2D( Server.Globals.Right );

					MovementActivity = Vector2.Dot( toPoint, rightSide ) > 0f ? Activity.StrafeRight : Activity.StrafeLeft;
					TaskComplete();
					break;
				}

			case TaskId.WaitForMovement:
				if ( RouteIsClear() )
					TaskComplete();

				break;

			case TaskId.Eat:
				Eat( task.Data );
				TaskComplete();
				break;

			case TaskId.SmallFlinch:
				IdealActivity = GetSmallFlinchActivity();
				break;

			case TaskId.Die:
				RouteClear();
				IdealActivity = GetDeathActivity();
				DeadFlag = DeadDying;
				break;

			case TaskId.SoundWake:
				AlertSound();
				TaskComplete();
				break;

			case TaskId.SoundDie:
			case TaskId.SoundDeath:
				DeathSound();
				TaskComplete();
				break;

			case TaskId.SoundIdle:
				IdleSound();
				TaskComplete();
				break;

			case TaskId.SoundPain:
				PainSound();
				TaskComplete();
				break;

			case TaskId.SoundAngry:
				Util.Alert( AlertType.AiConsole, "SOUND" );
				TaskComplete();
				break;

			case TaskId.WaitForScript:
				if ( !string.IsNullOrEmpty( Cine.Idle ) )
				{
					Cine.StartSequence( this, Cine.Idle, false );

					if ( Cine.Idle == Cine.Play )
						FrameRate = 0f;
				}
				else
				{
					IdealActivity = Activity.Idle;
				}

				break;

			case TaskId.PlayScript:
				MoveType = MoveType.Fly;
				EntityFlags &= ~EntityFlags.OnGround;
				ScriptState = ScriptState.Playing;
				break;

			case TaskId.EnableScript:
				Cine.DelayStart( 0 );
				TaskComplete();
				break;

			case TaskId.PlantOnScript:
				if ( TargetEntity is { } plant )
					Origin = plant.Origin;

				TaskComplete();
				break;

			case TaskId.FaceScript:
				if ( TargetEntity is { } script )
					IdealYaw = Util.AngleMod( script.Angles.y );

				TaskComplete();
				IdealActivity = Activity.Idle;
				RouteClear();
				break;

			case TaskId.SuggestState:
				IdealMonsterState = (MonsterState)(int)task.Data;
				TaskComplete();
				break;

			case TaskId.SetFailSchedule:
				FailSchedule = (int)task.Data;
				TaskComplete();
				break;

			case TaskId.ClearFailSchedule:
				FailSchedule = ScheduleType.None;
				TaskComplete();
				break;

			default:
				Util.Alert( AlertType.AiConsole, $"No StartTask entry for {task.Id}" );
				break;
		}
	}

	protected Schedule GetBaseSchedule()
	{
		switch ( MonsterState )
		{
			case MonsterState.Prone:
				return GetScheduleOfType( ScheduleType.BarnacleVictimGrab );

			case MonsterState.None:
				Util.Alert( AlertType.AiConsole, "MONSTERSTATE IS NONE!" );
				break;

			case MonsterState.Idle:
				if ( HasConditions( ConditionHearSound ) )
					return GetScheduleOfType( ScheduleType.AlertFace );

				return GetScheduleOfType( RouteIsClear() ? ScheduleType.IdleStand : ScheduleType.IdleWalk );

			case MonsterState.Alert:
				if ( HasConditions( ConditionEnemyDead ) && HasActivity( Activity.VictoryDance ) )
					return GetScheduleOfType( ScheduleType.VictoryDance );

				if ( HasConditions( ConditionLightDamage | ConditionHeavyDamage ) )
				{
					return GetScheduleOfType( MathF.Abs( YawDiff() ) < (1.0 - FieldOfView) * 60
						? ScheduleType.TakeCoverFromOrigin
						: ScheduleType.AlertSmallFlinch );
				}

				return GetScheduleOfType( HasConditions( ConditionHearSound ) ? ScheduleType.AlertFace : ScheduleType.AlertStand );

			case MonsterState.Combat:
				if ( HasConditions( ConditionEnemyDead ) )
				{
					EnemyEntity = null;

					if ( GetEnemy() )
						ClearConditions( ConditionEnemyDead );
					else
						SetState( MonsterState.Alert );

					return GetSchedule();
				}

				if ( HasConditions( ConditionNewEnemy ) )
					return GetScheduleOfType( ScheduleType.WakeAngry );

				if ( HasConditions( ConditionLightDamage ) && !HasMemory( MemoryFlinched ) )
					return GetScheduleOfType( ScheduleType.SmallFlinch );

				if ( !HasConditions( ConditionSeeEnemy ) )
					return GetScheduleOfType( HasConditions( ConditionEnemyOccluded ) ? ScheduleType.ChaseEnemy : ScheduleType.CombatFace );

				if ( HasConditions( ConditionCanRangeAttack1 ) )
					return GetScheduleOfType( ScheduleType.RangeAttack1 );

				if ( HasConditions( ConditionCanRangeAttack2 ) )
					return GetScheduleOfType( ScheduleType.RangeAttack2 );

				if ( HasConditions( ConditionCanMeleeAttack1 ) )
					return GetScheduleOfType( ScheduleType.MeleeAttack1 );

				if ( HasConditions( ConditionCanMeleeAttack2 ) )
					return GetScheduleOfType( ScheduleType.MeleeAttack2 );

				if ( !HasConditions( ConditionCanRangeAttack1 | ConditionCanMeleeAttack1 ) )
					return GetScheduleOfType( ScheduleType.ChaseEnemy );

				if ( !FacingIdeal() )
					return GetScheduleOfType( ScheduleType.CombatFace );

				Util.Alert( AlertType.AiConsole, "No suitable combat schedule!" );
				break;

			case MonsterState.Dead:
				return GetScheduleOfType( ScheduleType.Die );

			case MonsterState.Script:
				if ( Cine is null )
				{
					Util.Alert( AlertType.AiConsole, $"Script failed for {ClassName}" );
					CineCleanup();
					return GetScheduleOfType( ScheduleType.IdleStand );
				}

				return GetScheduleOfType( ScheduleType.AiScript );

			default:
				Util.Alert( AlertType.AiConsole, "Invalid State for GetSchedule!" );
				break;
		}

		return DefaultAi.Error;
	}

	public virtual Schedule GetSchedule() => GetBaseSchedule();
}
