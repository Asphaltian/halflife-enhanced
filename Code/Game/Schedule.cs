using GoldSrc;

namespace HalfLife;

public static class TaskState
{
	public const int New = 0;
	public const int Running = 1;
	public const int RunningMovement = 2;
	public const int RunningTask = 3;
	public const int Complete = 4;
}

public static class ScheduleType
{
	public const int None = 0;
	public const int IdleStand = 1;
	public const int IdleWalk = 2;
	public const int WakeAngry = 3;
	public const int WakeCalled = 4;
	public const int AlertFace = 5;
	public const int AlertSmallFlinch = 6;
	public const int AlertBigFlinch = 7;
	public const int AlertStand = 8;
	public const int InvestigateSound = 9;
	public const int CombatFace = 10;
	public const int CombatStand = 11;
	public const int ChaseEnemy = 12;
	public const int ChaseEnemyFailed = 13;
	public const int VictoryDance = 14;
	public const int TargetFace = 15;
	public const int TargetChase = 16;
	public const int SmallFlinch = 17;
	public const int TakeCoverFromEnemy = 18;
	public const int TakeCoverFromBestSound = 19;
	public const int TakeCoverFromOrigin = 20;
	public const int Cower = 21;
	public const int MeleeAttack1 = 22;
	public const int MeleeAttack2 = 23;
	public const int RangeAttack1 = 24;
	public const int RangeAttack2 = 25;
	public const int SpecialAttack1 = 26;
	public const int SpecialAttack2 = 27;
	public const int Standoff = 28;
	public const int ArmWeapon = 29;
	public const int Reload = 30;
	public const int Guard = 31;
	public const int Ambush = 32;
	public const int Die = 33;
	public const int WaitTrigger = 34;
	public const int Follow = 35;
	public const int Sleep = 36;
	public const int Wake = 37;
	public const int BarnacleVictimGrab = 38;
	public const int BarnacleVictimChomp = 39;
	public const int AiScript = 40;
	public const int Fail = 41;
	public const int LastCommon = 42;
}

public static class TaskId
{
	public const int Invalid = 0;
	public const int Wait = 1;
	public const int WaitFaceEnemy = 2;
	public const int WaitPvs = 3;
	public const int SuggestState = 4;
	public const int WalkToTarget = 5;
	public const int RunToTarget = 6;
	public const int MoveToTargetRange = 7;
	public const int GetPathToEnemy = 8;
	public const int GetPathToEnemyLkp = 9;
	public const int GetPathToEnemyCorpse = 10;
	public const int GetPathToLeader = 11;
	public const int GetPathToSpot = 12;
	public const int GetPathToTarget = 13;
	public const int GetPathToHintNode = 14;
	public const int GetPathToLastPosition = 15;
	public const int GetPathToBestSound = 16;
	public const int GetPathToBestScent = 17;
	public const int RunPath = 18;
	public const int WalkPath = 19;
	public const int StrafePath = 20;
	public const int ClearMoveWait = 21;
	public const int StoreLastPosition = 22;
	public const int ClearLastPosition = 23;
	public const int PlayActiveIdle = 24;
	public const int FindHintNode = 25;
	public const int ClearHintNode = 26;
	public const int SmallFlinch = 27;
	public const int FaceIdeal = 28;
	public const int FaceRoute = 29;
	public const int FaceEnemy = 30;
	public const int FaceHintNode = 31;
	public const int FaceTarget = 32;
	public const int FaceLastPosition = 33;
	public const int RangeAttack1 = 34;
	public const int RangeAttack2 = 35;
	public const int MeleeAttack1 = 36;
	public const int MeleeAttack2 = 37;
	public const int Reload = 38;
	public const int RangeAttack1NoTurn = 39;
	public const int RangeAttack2NoTurn = 40;
	public const int MeleeAttack1NoTurn = 41;
	public const int MeleeAttack2NoTurn = 42;
	public const int ReloadNoTurn = 43;
	public const int SpecialAttack1 = 44;
	public const int SpecialAttack2 = 45;
	public const int Crouch = 46;
	public const int Stand = 47;
	public const int Guard = 48;
	public const int StepLeft = 49;
	public const int StepRight = 50;
	public const int StepForward = 51;
	public const int StepBack = 52;
	public const int DodgeLeft = 53;
	public const int DodgeRight = 54;
	public const int SoundAngry = 55;
	public const int SoundDeath = 56;
	public const int SetActivity = 57;
	public const int SetSchedule = 58;
	public const int SetFailSchedule = 59;
	public const int ClearFailSchedule = 60;
	public const int PlaySequence = 61;
	public const int PlaySequenceFaceEnemy = 62;
	public const int PlaySequenceFaceTarget = 63;
	public const int SoundIdle = 64;
	public const int SoundWake = 65;
	public const int SoundPain = 66;
	public const int SoundDie = 67;
	public const int FindCoverFromBestSound = 68;
	public const int FindCoverFromEnemy = 69;
	public const int FindLateralCoverFromEnemy = 70;
	public const int FindNodeCoverFromEnemy = 71;
	public const int FindNearNodeCoverFromEnemy = 72;
	public const int FindFarNodeCoverFromEnemy = 73;
	public const int FindCoverFromOrigin = 74;
	public const int Eat = 75;
	public const int Die = 76;
	public const int WaitForScript = 77;
	public const int PlayScript = 78;
	public const int EnableScript = 79;
	public const int PlantOnScript = 80;
	public const int FaceScript = 81;
	public const int WaitRandom = 82;
	public const int WaitIndefinite = 83;
	public const int StopMoving = 84;
	public const int TurnLeft = 85;
	public const int TurnRight = 86;
	public const int Remember = 87;
	public const int Forget = 88;
	public const int WaitForMovement = 89;
	public const int LastCommon = 90;
}

public static class TargetMove
{
	public const int Normal = 0;
	public const int Scripted = 1;
}

public static class Goal
{
	public const int AttackEnemy = 0;
	public const int Move = 1;
	public const int TakeCover = 2;
	public const int MoveTarget = 3;
	public const int Eat = 4;
}

public static class MoveFlag
{
	public const int ToTargetEntity = 1 << 0;
	public const int ToEnemy = 1 << 1;
	public const int ToCover = 1 << 2;
	public const int ToDetour = 1 << 3;
	public const int ToPathCorner = 1 << 4;
	public const int ToNode = 1 << 5;
	public const int ToLocation = 1 << 6;
	public const int IsGoal = 1 << 7;
	public const int DontSimplify = 1 << 8;
	public const int NotToMask = IsGoal | DontSimplify;
}

public static class MoveGoalType
{
	public const int None = 0;
	public const int TargetEntity = MoveFlag.ToTargetEntity;
	public const int Enemy = MoveFlag.ToEnemy;
	public const int PathCorner = MoveFlag.ToPathCorner;
	public const int Location = MoveFlag.ToLocation;
	public const int Node = MoveFlag.ToNode;
}

public readonly record struct Task( int Id, float Data = 0f );

public sealed class Schedule
{
	public readonly string Name;
	public readonly int InterruptMask;
	public readonly int SoundMask;
	public readonly Task[] Tasks;

	public Schedule( string name, int interruptMask, int soundMask, params Task[] tasks )
	{
		Name = name;
		InterruptMask = interruptMask;
		SoundMask = soundMask;
		Tasks = tasks;
	}
}

public struct WayPoint
{
	public Vector3 Location;
	public int Type;
}
