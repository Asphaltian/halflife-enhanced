using System;
using GoldSrc;

namespace HalfLife;

public static class TalkGroup
{
	public const int Answer = 0;
	public const int Question = 1;
	public const int Idle = 2;
	public const int Stare = 3;
	public const int Use = 4;
	public const int UnUse = 5;
	public const int Stop = 6;
	public const int NoShoot = 7;
	public const int Hello = 8;
	public const int PreHello = 9;
	public const int PreIdle = 10;
	public const int PreQuestion = 11;
	public const int PlayerHurt1 = 12;
	public const int PlayerHurt2 = 13;
	public const int PlayerHurt3 = 14;
	public const int Smell = 15;
	public const int Wound = 16;
	public const int Mortal = 17;
	public const int Count = 18;
}

public static class TalkSchedule
{
	public const int CantFollow = ScheduleType.LastCommon + 1;
	public const int MoveAway = CantFollow + 1;
	public const int MoveAwayFollow = CantFollow + 2;
	public const int MoveAwayFail = CantFollow + 3;
	public const int LastTalkMonster = CantFollow + 4;
}

public static class TalkTask
{
	public const int CantFollow = TaskId.LastCommon + 1;
	public const int MoveAwayPath = CantFollow + 1;
	public const int WalkPathForUnits = CantFollow + 2;
	public const int Respond = CantFollow + 3;
	public const int Speak = CantFollow + 4;
	public const int Hello = CantFollow + 5;
	public const int HeadReset = CantFollow + 6;
	public const int StopShooting = CantFollow + 7;
	public const int Stare = CantFollow + 8;
	public const int LookAtClient = CantFollow + 9;
	public const int ClientStare = CantFollow + 10;
	public const int EyeContact = CantFollow + 11;
	public const int IdealYaw = CantFollow + 12;
	public const int FacePlayer = CantFollow + 13;
	public const int LastTalkMonster = CantFollow + 14;
}

[SkipHotload]
public static class TalkAi
{
	public static readonly Schedule IdleResponse = new( "Idle Response",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage,
		0,
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.Wait, 0.5f ),
		new( TalkTask.EyeContact ),
		new( TalkTask.Respond ),
		new( TalkTask.IdealYaw ),
		new( TaskId.FaceIdeal ),
		new( TaskId.SetActivity, (float)Activity.Signal3 ),
		new( TalkTask.EyeContact ) );

	public static readonly Schedule IdleSpeak = new( "Idle Speak",
		BaseMonster.ConditionNewEnemy | TalkMonster.ConditionClientPush | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage,
		0,
		new( TalkTask.Speak ),
		new( TalkTask.IdealYaw ),
		new( TaskId.FaceIdeal ),
		new( TaskId.SetActivity, (float)Activity.Signal3 ),
		new( TalkTask.EyeContact ),
		new( TaskId.WaitRandom, 0.5f ) );

	public static readonly Schedule IdleSpeakWait = new( "Idle Speak Wait",
		BaseMonster.ConditionNewEnemy | TalkMonster.ConditionClientPush | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage,
		0,
		new( TaskId.SetActivity, (float)Activity.Signal3 ),
		new( TalkTask.Speak ),
		new( TalkTask.EyeContact ),
		new( TaskId.Wait, 2f ) );

	public static readonly Schedule IdleHello = new( "Idle Hello",
		BaseMonster.ConditionNewEnemy | TalkMonster.ConditionClientPush | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionHearSound | BaseMonster.ConditionProvoked,
		WorldSound.Combat,
		new( TaskId.SetActivity, (float)Activity.Signal3 ),
		new( TalkTask.Hello ),
		new( TalkTask.EyeContact ),
		new( TaskId.Wait, 0.5f ),
		new( TalkTask.Hello ),
		new( TalkTask.EyeContact ),
		new( TaskId.Wait, 0.5f ),
		new( TalkTask.Hello ),
		new( TalkTask.EyeContact ),
		new( TaskId.Wait, 0.5f ),
		new( TalkTask.Hello ),
		new( TalkTask.EyeContact ),
		new( TaskId.Wait, 0.5f ) );

	public static readonly Schedule IdleStopShooting = new( "Idle Stop Shooting",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionHearSound,
		0,
		new Task( TalkTask.StopShooting ) );

	public static readonly Schedule MoveAway = new( "MoveAway",
		0,
		0,
		new( TaskId.SetFailSchedule, TalkSchedule.MoveAwayFail ),
		new( TaskId.StoreLastPosition ),
		new( TalkTask.MoveAwayPath, 100f ),
		new( TalkTask.WalkPathForUnits, 100f ),
		new( TaskId.StopMoving ),
		new( TalkTask.FacePlayer, 0.5f ) );

	public static readonly Schedule MoveAwayFail = new( "MoveAwayFail",
		0,
		0,
		new( TaskId.StopMoving ),
		new( TalkTask.FacePlayer, 0.5f ) );

	public static readonly Schedule MoveAwayFollow = new( "MoveAwayFollow",
		0,
		0,
		new( TaskId.SetFailSchedule, ScheduleType.TargetFace ),
		new( TaskId.StoreLastPosition ),
		new( TalkTask.MoveAwayPath, 100f ),
		new( TalkTask.WalkPathForUnits, 100f ),
		new( TaskId.StopMoving ),
		new( TaskId.SetSchedule, ScheduleType.TargetFace ) );

	public static readonly Schedule TlkIdleEyecontact = new( "TlkIdleEyecontact",
		BaseMonster.ConditionNewEnemy | TalkMonster.ConditionClientPush | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage,
		0,
		new( TalkTask.IdealYaw ),
		new( TaskId.FaceIdeal ),
		new( TaskId.SetActivity, (float)Activity.Signal3 ),
		new( TalkTask.EyeContact ) );

	private const int WatchClientInterrupts = BaseMonster.ConditionNewEnemy | BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionHearSound | BaseMonster.ConditionSmell | TalkMonster.ConditionClientPush | TalkMonster.ConditionClientUnseen | BaseMonster.ConditionProvoked;
	private const int WatchClientSounds = WorldSound.Combat | WorldSound.Danger | WorldSound.Meat | WorldSound.Carcass | WorldSound.Garbage;

	public static readonly Schedule TlkIdleWatchClient = new( "TlkIdleWatchClient",
		WatchClientInterrupts,
		WatchClientSounds,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TalkTask.LookAtClient, 6f ) );

	public static readonly Schedule TlkIdleWatchClientStare = new( "TlkIdleWatchClientStare",
		WatchClientInterrupts,
		WatchClientSounds,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TalkTask.ClientStare, 6f ),
		new( TalkTask.Stare ),
		new( TalkTask.IdealYaw ),
		new( TaskId.FaceIdeal ),
		new( TaskId.SetActivity, (float)Activity.Signal3 ),
		new( TalkTask.EyeContact ) );

	public static readonly Schedule[] List =
	[
		IdleResponse,
		IdleSpeak,
		IdleHello,
		IdleSpeakWait,
		IdleStopShooting,
		MoveAway,
		MoveAwayFollow,
		MoveAwayFail,
		TlkIdleWatchClient,
		TlkIdleWatchClientStare,
		TlkIdleEyecontact
	];
}

public abstract class TalkMonster : BaseMonster
{
	public const float TalkRangeMin = 500f;
	public const int StareDistance = 128;

	public const int SaidDamageLight = 1 << 0;
	public const int SaidDamageMedium = 1 << 1;
	public const int SaidDamageHeavy = 1 << 2;
	public const int SaidHelloPlayer = 1 << 3;
	public const int SaidWoundLight = 1 << 4;
	public const int SaidWoundHeavy = 1 << 5;
	public const int SaidHeard = 1 << 6;
	public const int SaidSmelled = 1 << 7;

	public const int FriendCount = 3;

	public const int ConditionClientPush = ConditionSpecial1;
	public const int ConditionClientUnseen = ConditionSpecial2;

	public static float TalkWaitTime;

	protected static string[] Friends { get; private set; } =
	[
		"monster_barney",
		"monster_scientist",
		"monster_sitting_scientist"
	];

	[Save( FieldType.Integer )]
	public int Said;
	[Save( FieldType.Integer )]
	public int SpeakCount;
	[Save( FieldType.Time )]
	public float UseTime;
	[Save( FieldType.String )]
	public string UseSentence;
	[Save( FieldType.String )]
	public string UnUseSentence;
	[Save( FieldType.Time )]
	public float LastSaidSmelled;
	[Save( FieldType.Time )]
	public float StopTalkTime;
	[Save( FieldType.EHandle )]
	private BaseEntity _talkTarget;

	public int VoicePitch;
	public readonly string[] Groups = new string[TalkGroup.Count];

	public BaseEntity TalkTarget
	{
		get => _talkTarget.IsValid() ? _talkTarget : null;
		set => _talkTarget = value;
	}

	public override Schedule ScheduleFromName( string name ) => ScheduleInList( name, TalkAi.List ) ?? base.ScheduleFromName( name );

	public void StopTalking() => SentenceStop();

	public bool IsFollowing => TargetEntity is { IsPlayer: true };

	public virtual void DeclineFollowing()
	{
	}

	public virtual int FriendNumber( int arrayNumber ) => arrayNumber;

	public override void SetActivity( Activity activity )
	{
		if ( activity == Activity.Idle && IsTalking )
			activity = Activity.Signal3;

		if ( activity == Activity.Signal3 && LookupActivity( (int)Activity.Signal3 ) == Animation.ActivityNotAvailable )
			activity = Activity.Idle;

		base.SetActivity( activity );
	}

	public override void StartTask( Task task )
	{
		switch ( task.Id )
		{
			case TalkTask.Speak:
				IdleSpeak();
				TaskComplete();
				break;

			case TalkTask.Respond:
				IdleRespond();
				TaskComplete();
				break;

			case TalkTask.Hello:
				IdleHello();
				TaskComplete();
				break;

			case TalkTask.Stare:
				IdleStare();
				TaskComplete();
				break;

			case TalkTask.FacePlayer:
			case TalkTask.LookAtClient:
			case TalkTask.ClientStare:
				WaitFinished = Server.Globals.Time + task.Data;
				break;

			case TalkTask.EyeContact:
				break;

			case TalkTask.IdealYaw:
				if ( TalkTarget is { } talkTarget )
				{
					YawSpeed = 60f;

					var yaw = VecToYaw( talkTarget.Origin - Origin ) - Angles.y;

					if ( yaw > 180f )
						yaw -= 360f;

					if ( yaw < -180f )
						yaw += 360f;

					if ( yaw < 0f )
						IdealYaw = Math.Min( yaw + 45f, 0f ) + Angles.y;
					else
						IdealYaw = Math.Max( yaw - 45f, 0f ) + Angles.y;
				}

				TaskComplete();
				break;

			case TalkTask.HeadReset:
				TalkTarget = null;
				TaskComplete();
				break;

			case TalkTask.StopShooting:
				PlaySentence( Groups[TalkGroup.NoShoot], Server.RandomFloat( 2.8f, 3.2f ), Server.VolumeNormal, Server.AttenuationNormal );
				TaskComplete();
				break;

			case TalkTask.CantFollow:
				StopFollowing( false );
				PlaySentence( Groups[TalkGroup.Stop], Server.RandomFloat( 2f, 2.5f ), Server.VolumeNormal, Server.AttenuationNormal );
				TaskComplete();
				break;

			case TalkTask.WalkPathForUnits:
				MovementActivity = Activity.Walk;
				break;

			case TalkTask.MoveAwayPath:
				MathLib.AngleVectors( Angles with { y = IdealYaw + 180f }, out var move, out _, out _ );

				if ( MoveToLocation( Activity.Walk, 2f, Origin + (move * task.Data) ) )
				{
					TaskComplete();
				}
				else if ( FindCover( Origin, ViewOffset, 0f, CoverRadius ) )
				{
					MoveWaitFinished = (float)(Server.Globals.Time + 2.0);
					TaskComplete();
				}
				else
				{
					TaskFail();
				}

				break;

			case TaskId.PlayScript:
				TalkTarget = null;
				base.StartTask( task );
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
			case TalkTask.ClientStare:
			case TalkTask.LookAtClient:
				{
					if ( MonsterState != MonsterState.Idle || IsMoving || IsTalking )
					{
						TaskFail();
						return;
					}

					var player = Server.EntityOfEntIndex( 1 );

					if ( player is not null )
						IdleHeadTurn( player.Origin );

					if ( task.Id == TalkTask.ClientStare )
					{
						if ( MathLib.Length2D( player.Origin - Origin ) > StareDistance )
							TaskFail();

						Server.Globals.MakeVectors( player.Angles );

						if ( Util.DotPoints( player.Origin, Origin, Server.Globals.Forward ) < FieldOfView )
							TaskFail();
					}

					if ( Server.Globals.Time > WaitFinished )
						TaskComplete();

					break;
				}

			case TalkTask.FacePlayer:
				{
					if ( Server.EntityOfEntIndex( 1 ) is not { } player )
					{
						TaskFail();
						break;
					}

					MakeIdealYaw( player.Origin );
					ChangeYaw( (int)YawSpeed );
					IdleHeadTurn( player.Origin );

					if ( Server.Globals.Time > WaitFinished && YawDiff() < 10f )
						TaskComplete();

					break;
				}

			case TalkTask.EyeContact:
				if ( !IsMoving && IsTalking && TalkTarget is { } eyeTarget )
					IdleHeadTurn( eyeTarget.Origin );
				else
					TaskComplete();

				break;

			case TalkTask.WalkPathForUnits:
				if ( MathLib.Length2D( LastPosition - Origin ) > task.Data || MovementIsComplete )
				{
					TaskComplete();
					RouteClear();
				}

				break;

			case TaskId.WaitForMovement:
				if ( IsTalking && TalkTarget is { } walkTarget )
				{
					IdleHeadTurn( walkTarget.Origin );
				}
				else
				{
					IdleHeadTurn( Origin );
					IdleHello();

					if ( Server.RandomLong( 0, SpeakCount * 20 ) == 0 )
						IdleSpeak();
				}

				base.RunTask( task );

				if ( TaskIsComplete )
					IdleHeadTurn( Origin );

				break;

			default:
				if ( IsTalking && TalkTarget is { } target )
					IdleHeadTurn( target.Origin );
				else
					SetBoneController( 0, 0f );

				base.RunTask( task );
				break;
		}
	}

	public override void Killed( BaseEntity attacker, int gib )
	{
		if ( (attacker.EntityFlags & EntityFlags.Client) == EntityFlags.Client && MonsterState != MonsterState.Prone )
		{
			AlertFriends();
			LimitFollowers( attacker, 0 );
		}

		TargetEntity = null;
		StopTalking();
		SetUse( null );

		base.Killed( attacker, gib );
	}

	public BaseEntity EnumFriends( BaseEntity previous, int listNumber, bool trace )
	{
		var friend = previous;
		var friendName = Friends[FriendNumber( listNumber )];

		while ( (friend = Util.FindEntityByClassname( friend, friendName )) is not null )
		{
			if ( friend == this || !friend.IsAlive )
				continue;

			if ( !trace )
				return friend;

			var check = friend.Origin with { z = friend.AbsMax.z };

			if ( Util.TraceLine( Origin, check, IgnoreMonsters, this ).Fraction == 1f )
				return friend;
		}

		return null;
	}

	public void AlertFriends()
	{
		BaseEntity friend = null;

		for ( var i = 0; i < FriendCount; i++ )
		{
			while ( (friend = EnumFriends( friend, i, true )) is not null )
			{
				var monster = (BaseMonster)friend;

				if ( monster.IsAlive )
					monster.Remember( MemoryProvoked );
			}
		}
	}

	public void ShutUpFriends()
	{
		BaseEntity friend = null;

		for ( var i = 0; i < FriendCount; i++ )
		{
			while ( (friend = EnumFriends( friend, i, true )) is not null )
				(friend as BaseMonster)?.SentenceStop();
		}
	}

	public void LimitFollowers( BaseEntity player, int maxFollowers )
	{
		BaseEntity friend = null;
		var count = 0;

		for ( var i = 0; i < FriendCount; i++ )
		{
			while ( (friend = EnumFriends( friend, i, false )) is not null )
			{
				if ( friend is not BaseMonster monster || monster.TargetEntity != player )
					continue;

				count++;

				if ( count > maxFollowers )
					monster.StopFollowing( true );
			}
		}
	}

	public float TargetDistance()
	{
		if ( TargetEntity is not { IsAlive: true } target )
			return 1e6f;

		return MathLib.Length( target.Origin - Origin );
	}

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		switch ( monsterEvent.Event )
		{
			case ScriptEvent.SentenceRandom1 when Server.RandomLong( 0, 99 ) < 75:
				break;

			case ScriptEvent.SentenceRandom1:
			case ScriptEvent.Sentence:
				ShutUpFriends();
				PlaySentence( monsterEvent.Options, Server.RandomFloat( 2.8f, 3.4f ), Server.VolumeNormal, Server.AttenuationIdle );
				break;

			default:
				base.HandleAnimEvent( monsterEvent );
				break;
		}
	}

	public void TalkInit()
	{
		TalkWaitTime = 0f;
		VoicePitch = 100;
	}

	public BaseEntity FindNearestFriend( bool player )
	{
		BaseEntity friend = null;
		BaseEntity nearest = null;
		var range = 10000000f;
		var start = Origin with { z = AbsMax.z };

		for ( var i = (player ? 1 : FriendCount) - 1; i > -1; i-- )
		{
			var friendName = player ? "player" : Friends[FriendNumber( i )];

			if ( friendName is null )
				continue;

			while ( (friend = Util.FindEntityByClassname( friend, friendName )) is not null )
			{
				if ( friend == this || !friend.IsAlive )
					continue;

				if ( friend is not BaseMonster { MonsterState: not (MonsterState.Script or MonsterState.Prone) } )
					continue;

				var check = friend.Origin with { z = friend.AbsMax.z };
				var distance = MathLib.Length( start - check );

				if ( range <= distance )
					continue;

				if ( Util.TraceLine( start, check, IgnoreMonsters, this ).Fraction != 1f || distance >= TalkRangeMin )
					continue;

				nearest = friend;
				range = distance;
			}
		}

		return nearest;
	}

	public int GetVoicePitch() => VoicePitch + Server.RandomLong( 0, 3 );

	public override void Touch( BaseEntity other )
	{
		if ( !other.IsPlayer || HasMemory( MemoryProvoked ) || IsTalking )
			return;

		var speed = Math.Abs( other.Velocity.x ) + Math.Abs( other.Velocity.y );

		if ( speed > 50f )
		{
			SetConditions( ConditionClientPush );
			MakeIdealYaw( other.Origin );
		}
	}

	public void IdleRespond()
	{
		GetVoicePitch();

		PlaySentence( Groups[TalkGroup.Answer], Server.RandomFloat( 2.8f, 3.2f ), Server.VolumeNormal, Server.AttenuationIdle );
	}

	public bool OkToSpeak()
	{
		if ( MonsterState == MonsterState.Prone || IdealMonsterState == MonsterState.Prone )
			return false;

		if ( DeadFlag != DeadNo )
			return false;

		if ( Server.Globals.Time <= TalkWaitTime )
			return false;

		if ( (SpawnFlags & SpawnFlagGag) != 0 )
			return false;

		if ( !IsAlive || Util.IsNullEnt( Server.FindClientInPvs( this ) ) )
			return false;

		return EnemyEntity is not { } enemy || !IsVisible( enemy );
	}

	public override bool CanPlaySentence( bool disregardState ) => disregardState ? base.CanPlaySentence( disregardState ) : OkToSpeak();

	public bool IdleStare()
	{
		if ( !OkToSpeak() )
			return false;

		PlaySentence( Groups[TalkGroup.Stare], Server.RandomFloat( 5f, 7.5f ), Server.VolumeNormal, Server.AttenuationIdle );

		TalkTarget = FindNearestFriend( true );

		return true;
	}

	public bool IdleHello()
	{
		if ( !OkToSpeak() || (Said & SaidHelloPlayer) != 0 )
			return false;

		if ( FindNearestFriend( true ) is not { } player || !IsInViewCone( player ) || !IsVisible( player ) )
			return false;

		TalkTarget = player;

		var group = (SpawnFlags & SpawnFlagPreDisaster) != 0 ? TalkGroup.PreHello : TalkGroup.Hello;

		PlaySentence( Groups[group], Server.RandomFloat( 3f, 3.5f ), Server.VolumeNormal, Server.AttenuationIdle );

		Said |= SaidHelloPlayer;

		return true;
	}

	public void IdleHeadTurn( Vector3 friend )
	{
		if ( (Capability & CapabilityTurnHead) == 0 )
			return;

		var yaw = VecToYaw( friend - Origin ) - Angles.y;

		if ( yaw > 180f )
			yaw -= 360f;

		if ( yaw < -180f )
			yaw += 360f;

		SetBoneController( 0, yaw );
	}

	private bool SpeakAboutDamage( int said, int group, float duration )
	{
		PlaySentence( Groups[group], duration, Server.VolumeNormal, Server.AttenuationIdle );

		Said |= said;

		return true;
	}

	public bool IdleSpeak()
	{
		if ( !OkToSpeak() )
			return false;

		string idleGroup;
		string questionGroup;
		float duration;

		if ( (SpawnFlags & SpawnFlagPreDisaster) != 0 )
		{
			idleGroup = Groups[TalkGroup.PreIdle];
			questionGroup = Groups[TalkGroup.PreQuestion];
			duration = Server.RandomFloat( 4.8f, 5.2f );
		}
		else
		{
			idleGroup = Groups[TalkGroup.Idle];
			questionGroup = Groups[TalkGroup.Question];
			duration = Server.RandomFloat( 2.8f, 3.2f );
		}

		GetVoicePitch();

		if ( TargetEntity is { IsPlayer: true, IsAlive: true } target )
		{
			TalkTarget = target;

			if ( (Said & SaidDamageHeavy) == 0 && target.Health <= target.MaxHealth / 8 )
				return SpeakAboutDamage( SaidDamageHeavy, TalkGroup.PlayerHurt3, duration );

			if ( (Said & SaidDamageMedium) == 0 && target.Health <= target.MaxHealth / 4 )
				return SpeakAboutDamage( SaidDamageMedium, TalkGroup.PlayerHurt2, duration );

			if ( (Said & SaidDamageLight) == 0 && target.Health <= target.MaxHealth / 2 )
				return SpeakAboutDamage( SaidDamageLight, TalkGroup.PlayerHurt1, duration );
		}

		var friend = FindNearestFriend( false );

		if ( friend is not null && !friend.IsMoving && Server.RandomLong( 0, 99 ) < 75 )
		{
			PlaySentence( questionGroup, duration, Server.VolumeNormal, Server.AttenuationIdle );

			var talkMonster = (TalkMonster)friend;

			TalkTarget = friend;
			talkMonster.SetAnswerQuestion( this );
			talkMonster.StopTalkTime = StopTalkTime;

			SpeakCount++;

			return true;
		}

		if ( Server.RandomLong( 0, 1 ) != 0 && FindNearestFriend( true ) is { } player )
		{
			TalkTarget = player;
			PlaySentence( idleGroup, duration, Server.VolumeNormal, Server.AttenuationIdle );
			SpeakCount++;

			return true;
		}

		Talk( 0f );
		TalkWaitTime = 0f;

		return false;
	}

	public override void PlayScriptedSentence( string sentence, float duration, float volume, float attenuation, bool concurrent, BaseEntity listener )
	{
		if ( !concurrent )
			ShutUpFriends();

		ClearConditions( ConditionClientPush );
		UseTime = Server.Globals.Time + duration;
		PlaySentence( sentence, duration, volume, attenuation );

		TalkTarget = listener;
	}

	public override void PlaySentence( string sentence, float duration, float volume, float attenuation )
	{
		if ( sentence is null )
			return;

		Talk( duration );

		TalkWaitTime = (float)(Server.Globals.Time + duration + 2.0);

		if ( sentence.StartsWith( '!' ) )
			Sound.EmitDyn( this, SoundChannel.Voice, sentence, volume, attenuation, 0, GetVoicePitch() );
		else
			Sentences.PlayRandom( this, sentence, volume, attenuation, 0, GetVoicePitch() );

		Said |= SaidHelloPlayer;
	}

	public void Talk( float duration )
	{
		StopTalkTime = duration <= 0f ? (float)(Server.Globals.Time + 3.0) : Server.Globals.Time + duration;
	}

	public virtual void SetAnswerQuestion( TalkMonster speaker )
	{
		if ( Cine is null )
			ChangeSchedule( TalkAi.IdleResponse );

		TalkTarget = speaker;
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		if ( IsAlive && attacker is not null && MonsterState != MonsterState.Prone && (attacker.EntityFlags & EntityFlags.Client) == EntityFlags.Client )
		{
			if ( FindNearestFriend( false ) is { IsAlive: true } friend )
				((TalkMonster)friend).ChangeSchedule( TalkAi.IdleStopShooting );
		}

		return base.TakeDamage( inflictor, attacker, damage, damageType );
	}

	public override Schedule GetScheduleOfType( int type )
	{
		switch ( type )
		{
			case TalkSchedule.MoveAway:
				return TalkAi.MoveAway;

			case TalkSchedule.MoveAwayFollow:
				return TalkAi.MoveAwayFollow;

			case TalkSchedule.MoveAwayFail:
				return TalkAi.MoveAwayFail;

			case ScheduleType.TargetFace:
				return Server.RandomLong( 0, 99 ) < 2 ? TalkAi.IdleSpeakWait : DefaultAi.IdleStand;

			case ScheduleType.IdleStand:
				if ( (Said & SaidHelloPlayer) == 0 )
					return TalkAi.IdleHello;

				if ( (Said & SaidWoundLight) == 0 && Health <= MaxHealth * 0.75 )
				{
					PlaySentence( Groups[TalkGroup.Wound], Server.RandomFloat( 2.8f, 3.2f ), Server.VolumeNormal, Server.AttenuationIdle );
					Said |= SaidWoundLight;

					return DefaultAi.IdleStand;
				}

				if ( (Said & SaidWoundHeavy) == 0 && Health <= MaxHealth * 0.5 )
				{
					PlaySentence( Groups[TalkGroup.Mortal], Server.RandomFloat( 2.8f, 3.2f ), Server.VolumeNormal, Server.AttenuationIdle );
					Said |= SaidWoundHeavy;

					return DefaultAi.IdleStand;
				}

				if ( OkToSpeak() && Server.RandomLong( 0, SpeakCount * 2 ) == 0 )
					return TalkAi.IdleSpeak;

				if ( IsTalking || !HasConditions( ConditionSeeClient ) || Server.RandomLong( 0, 6 ) != 0 )
					return IsTalking ? TalkAi.TlkIdleEyecontact : DefaultAi.IdleStand;

				if ( Server.EntityOfEntIndex( 1 ) is { } player )
				{
					Server.Globals.MakeVectors( player.Angles );

					if ( MathLib.Length2D( player.Origin - Origin ) < StareDistance && Util.DotPoints( player.Origin, Origin, Server.Globals.Forward ) >= FieldOfView )
						return TalkAi.TlkIdleWatchClientStare;

					return TalkAi.TlkIdleWatchClient;
				}

				break;
		}

		return base.GetScheduleOfType( type );
	}

	public bool IsTalking => StopTalkTime > Server.Globals.Time;

	public override void PrescheduleThink()
	{
		if ( !HasConditions( ConditionSeeClient ) )
			SetConditions( ConditionClientUnseen );
	}

	public void TrySmellTalk()
	{
		if ( !OkToSpeak() )
			return;

		if ( Server.Globals.Time > LastSaidSmelled )
			Said &= ~SaidSmelled;

		if ( (Said & SaidSmelled) != 0 || !HasConditions( ConditionSmell ) )
			return;

		PlaySentence( Groups[TalkGroup.Smell], Server.RandomFloat( 2.8f, 3.2f ), Server.VolumeNormal, Server.AttenuationIdle );

		LastSaidSmelled = (float)(Server.Globals.Time + 60.0);
		Said |= SaidSmelled;
	}

	public override int Relationship( BaseEntity target )
	{
		if ( target.IsPlayer && HasMemory( MemoryProvoked ) )
			return RelationshipHate;

		return base.Relationship( target );
	}

	public override void StopFollowing( bool clearSchedule )
	{
		if ( !IsFollowing )
			return;

		if ( !HasMemory( MemoryProvoked ) )
		{
			PlaySentence( Groups[TalkGroup.UnUse], Server.RandomFloat( 2.8f, 3.2f ), Server.VolumeNormal, Server.AttenuationIdle );
			TalkTarget = TargetEntity;
		}

		if ( MovementGoal == MoveGoalType.TargetEntity )
			RouteClear();

		TargetEntity = null;

		if ( clearSchedule )
			ClearSchedule();

		if ( EnemyEntity is not null )
			IdealMonsterState = MonsterState.Combat;
	}

	public void StartFollowing( BaseEntity leader )
	{
		Cine?.CancelScript();

		if ( EnemyEntity is not null )
			IdealMonsterState = MonsterState.Alert;

		TargetEntity = leader;
		PlaySentence( Groups[TalkGroup.Use], Server.RandomFloat( 2.8f, 3.2f ), Server.VolumeNormal, Server.AttenuationIdle );
		TalkTarget = TargetEntity;
		ClearConditions( ConditionClientPush );
		ClearSchedule();
	}

	public bool CanFollow()
	{
		if ( MonsterState == MonsterState.Script && Cine is not { CanInterrupt: true } )
			return false;

		return IsAlive && !IsFollowing;
	}

	public void FollowerUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( UseTime > Server.Globals.Time )
			return;

		if ( caller is not { IsPlayer: true } )
			return;

		if ( (SpawnFlags & SpawnFlagPreDisaster) != 0 )
		{
			DeclineFollowing();
		}
		else if ( CanFollow() )
		{
			LimitFollowers( caller, 1 );

			if ( HasMemory( MemoryProvoked ) )
			{
				Util.Alert( AlertType.Console, "I'm not following you, you evil person!" );
			}
			else
			{
				StartFollowing( caller );
				Said |= SaidHelloPlayer;
			}
		}
		else
		{
			StopFollowing( true );
		}
	}

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "UseSentence":
				UseSentence = Server.AllocString( value );
				return true;
			case "UnUseSentence":
				UnUseSentence = Server.AllocString( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Precache()
	{
		if ( !string.IsNullOrEmpty( UseSentence ) )
			Groups[TalkGroup.Use] = UseSentence;

		if ( !string.IsNullOrEmpty( UnUseSentence ) )
			Groups[TalkGroup.UnUse] = UnUseSentence;
	}
}
