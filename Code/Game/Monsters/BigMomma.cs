using System;
using GoldSrc;

namespace HalfLife;

public static class BigMommaSchedule
{
	public const int BigNode = ScheduleType.LastCommon + 1;
	public const int NodeFail = ScheduleType.LastCommon + 2;
}

public static class BigMommaTask
{
	public const int MoveToNodeRange = TaskId.LastCommon + 1;
	public const int FindNode = TaskId.LastCommon + 2;
	public const int PlayNodePresequence = TaskId.LastCommon + 3;
	public const int PlayNodeSequence = TaskId.LastCommon + 4;
	public const int ProcessNode = TaskId.LastCommon + 5;
	public const int WaitNode = TaskId.LastCommon + 6;
	public const int NodeDelay = TaskId.LastCommon + 7;
	public const int NodeYaw = TaskId.LastCommon + 8;
}

[LinkEntityToClass( "info_bigmomma" )]
public class InfoBigMomma : PointEntity
{
	public const int SpawnFlagRun = 0x0001;
	public const int SpawnFlagWait = 0x0002;

	[Save( FieldType.String )]
	public string PreSequence;

	public override void Spawn()
	{
	}

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "radius":
				Scale = Util.Atof( value );
				return true;
			case "reachdelay":
				Speed = Util.Atof( value );
				return true;
			case "reachtarget":
				Message = Server.AllocString( value );
				return true;
			case "reachsequence":
				NetName = Server.AllocString( value );
				return true;
			case "presequence":
				PreSequence = Server.AllocString( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}
}

[LinkEntityToClass( "bmortar" )]
public class BigMommaMortar : BaseEntity
{
	[Save( FieldType.Integer )]
	private int _maxFrame;

	public override void Spawn()
	{
		MoveType = MoveType.Toss;
		ClassName = "bmortar";

		Solid = Solid.BBox;
		RenderMode = RenderMode.TransAlpha;
		RenderAmt = 255f;

		SetModel( "sprites/mommaspit.spr" );
		Frame = 0f;
		Scale = 0.5f;

		SetSize( Vector3.Zero, Vector3.Zero );

		_maxFrame = Server.ModelFrames( ModelIndex ) - 1;
		DamageTime = (float)(Server.Globals.Time + 0.4);
	}

	public void Animate()
	{
		NextThink = (float)(Server.Globals.Time + 0.1);

		if ( Server.Globals.Time > DamageTime )
		{
			DamageTime = (float)(Server.Globals.Time + 0.2);
			BigMomma.MortarSpray( Origin, -Util.Normalize( Velocity ), BigMomma.SpitSprite, 3 );
		}

		var previous = Frame;

		Frame++;

		if ( previous != 0f && Frame > _maxFrame )
			Frame = 0f;
	}

	public static BigMommaMortar Shoot( BaseEntity owner, Vector3 start, Vector3 velocity )
	{
		var spit = owner.Server.CreateEntity<BigMommaMortar>();

		spit.Spawn();

		spit.SetOrigin( start );
		spit.Velocity = velocity;
		spit.Owner = owner;
		spit.Scale = 2.5f;
		spit.SetThink( spit.Animate );
		spit.NextThink = (float)(spit.Server.Globals.Time + 0.1);

		return spit;
	}

	public override void Touch( BaseEntity other )
	{
		var pitch = (int)Server.RandomFloat( 90f, 110f );

		Sound.EmitDyn( this, SoundChannel.Voice, "bullchicken/bc_acid1.wav", 1f, Server.AttenuationNormal, 0, pitch );

		switch ( Server.RandomLong( 0, 1 ) )
		{
			case 0:
				Sound.EmitDyn( this, SoundChannel.Weapon, "bullchicken/bc_spithit1.wav", 1f, Server.AttenuationNormal, 0, pitch );
				break;
			case 1:
				Sound.EmitDyn( this, SoundChannel.Weapon, "bullchicken/bc_spithit2.wav", 1f, Server.AttenuationNormal, 0, pitch );
				break;
		}

		Trace trace;

		if ( other.IsBspModel )
		{
			trace = Util.TraceLine( Origin, Origin + (Velocity * 10f), DontIgnoreMonsters, this );
			Util.DecalTrace( trace, Decals.MommaSplat );
		}
		else
		{
			trace = default;
			trace.EndPos = Origin;
			trace.PlaneNormal = -1f * Util.Normalize( Velocity );
		}

		BigMomma.MortarSpray( trace.EndPos, trace.PlaneNormal, BigMomma.SpitSprite, 24 );

		Combat.RadiusDamage( Origin, this, Owner as BaseEntity, HalfLifeDll.SkillData.BigmommaDmgBlast, HalfLifeDll.SkillData.BigmommaRadiusBlast, ClassNone, DamageAcid );
		Remove();
	}
}

[SkipHotload]
public static class BigMommaAi
{
	public static readonly Schedule BigNode = new( "Big Node",
		0,
		0,
		new( TaskId.SetFailSchedule, BigMommaSchedule.NodeFail ),
		new( TaskId.StopMoving ),
		new( BigMommaTask.FindNode ),
		new( BigMommaTask.PlayNodePresequence ),
		new( BigMommaTask.MoveToNodeRange ),
		new( TaskId.StopMoving ),
		new( BigMommaTask.NodeYaw ),
		new( TaskId.FaceIdeal ),
		new( BigMommaTask.WaitNode ),
		new( BigMommaTask.PlayNodeSequence ),
		new( BigMommaTask.ProcessNode ),
		new( TaskId.SetActivity, (float)Activity.Idle ) );

	public static readonly Schedule NodeFail = new( "NodeFail",
		0,
		0,
		new( BigMommaTask.NodeDelay, 10f ),
		new( TaskId.SetActivity, (float)Activity.Idle ) );

	public static readonly Schedule[] List =
	[
		BigNode,
		NodeFail
	];
}

[LinkEntityToClass( "monster_bigmomma" )]
public class BigMomma : BaseMonster
{
	private const int EventStep1 = 1;
	private const int EventStep2 = 2;
	private const int EventStep3 = 3;
	private const int EventStep4 = 4;
	private const int EventSack = 5;
	private const int EventDeathSound = 6;
	private const int EventMeleeAttackBackRight = 8;
	private const int EventMeleeAttackBackLeft = 9;
	private const int EventMeleeAttack1 = 10;
	private const int EventMortarAttack1 = 11;
	private const int EventLayCrab = 12;
	private const int EventJumpForward = 13;
	private const int EventScream = 14;
	private const int EventPainSound = 15;
	private const int EventAttackSound = 16;
	private const int EventBirthSound = 17;
	private const int EventEarlyTarget = 50;

	private const int AttackDistance = 170;
	private const int MortarDistance = 800;
	private const int MaxChildren = 20;

	private const int MemoryChildPair = MemoryCustom1;
	private const int MemoryAdvanceNode = MemoryCustom2;
	private const int MemoryCompletedNode = MemoryCustom3;
	private const int MemoryFiredNode = MemoryCustom4;

	private const string ChildClass = "monster_babycrab";

	private static readonly string[] ChildDieSounds = ["gonarch/gon_childdie1.wav", "gonarch/gon_childdie2.wav", "gonarch/gon_childdie3.wav"];
	private static readonly string[] SackSounds = ["gonarch/gon_sack1.wav", "gonarch/gon_sack2.wav", "gonarch/gon_sack3.wav"];
	private static readonly string[] DeathSounds = ["gonarch/gon_die1.wav"];
	private static readonly string[] AttackSounds = ["gonarch/gon_attack1.wav", "gonarch/gon_attack2.wav", "gonarch/gon_attack3.wav"];
	private static readonly string[] AttackHitSounds = ["zombie/claw_strike1.wav", "zombie/claw_strike2.wav", "zombie/claw_strike3.wav"];
	private static readonly string[] BirthSounds = ["gonarch/gon_birth1.wav", "gonarch/gon_birth2.wav", "gonarch/gon_birth3.wav"];
	private static readonly string[] AlertSounds = ["gonarch/gon_alert1.wav", "gonarch/gon_alert2.wav", "gonarch/gon_alert3.wav"];
	private static readonly string[] PainSounds = ["gonarch/gon_pain2.wav", "gonarch/gon_pain4.wav", "gonarch/gon_pain5.wav"];
	private static readonly string[] FootSounds = ["gonarch/gon_step1.wav", "gonarch/gon_step2.wav", "gonarch/gon_step3.wav"];

	public static int SpitSprite { get; private set; }

	[Save( FieldType.Time )]
	private float _nodeTime;
	[Save( FieldType.Time )]
	private float _crabTime;
	[Save( FieldType.Time )]
	private float _mortarTime;
	[Save( FieldType.Time )]
	private float _painSoundTime;
	[Save( FieldType.Integer )]
	private int _crabCount;

	public override Schedule ScheduleFromName( string name ) => ScheduleInList( name, BigMommaAi.List ) ?? base.ScheduleFromName( name );

	public override int Classify() => ClassAlienMonster;

	public override void SetYawSpeed() => YawSpeed = Activity == Activity.Idle ? 100f : 90f;

	public override void SetObjectCollisionBox()
	{
		AbsMin = Origin + new Vector3( -95f, -95f, 0f );
		AbsMax = Origin + new Vector3( 95f, 95f, 190f );
	}

	public override void OverrideReset() => _crabCount = 0;

	private string NodeSequence => TargetEntity?.NetName;

	private string NodePresequence => (TargetEntity as InfoBigMomma)?.PreSequence;

	private float NodeDelay => TargetEntity?.Speed ?? 0f;

	private float NodeRange => TargetEntity?.Scale ?? 1e6f;

	private float NodeYaw => TargetEntity is { } target && target.Angles.y != 0f ? target.Angles.y : Angles.y;

	private void EmitSoundArray( SoundChannel channel, string[] sounds )
	{
		Sound.EmitDyn( this, channel, RandomSound( sounds ), 1f, Server.AttenuationNormal, 0, Server.RandomLong( 95, 105 ) );
	}

	private bool CanLayCrab()
	{
		if ( _crabTime >= Server.Globals.Time || _crabCount >= MaxChildren )
			return false;

		var mins = Origin - new Vector3( 32f, 32f, 0f );
		var maxs = Origin + new Vector3( 32f, 32f, 0f );

		var list = new BaseEntity[2];
		var count = Util.EntitiesInBox( list, mins, maxs, EntityFlags.Monster );

		for ( var i = 0; i < count; i++ )
		{
			if ( list[i] != this )
				return false;
		}

		return true;
	}

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		switch ( monsterEvent.Event )
		{
			case EventMeleeAttackBackRight:
			case EventMeleeAttackBackLeft:
			case EventMeleeAttack1:
				{
					MathLib.AngleVectors( Angles, out var forward, out var right, out _ );

					var center = Origin + (forward * 128f);
					var mins = center - new Vector3( 64f, 64f, 0f );
					var maxs = center + new Vector3( 64f, 64f, 64f );

					var list = new BaseEntity[8];
					var count = Util.EntitiesInBox( list, mins, maxs, EntityFlags.Monster | EntityFlags.Client );
					BaseEntity hurt = null;

					for ( var i = 0; i < count && hurt is null; i++ )
					{
						if ( list[i] != this && list[i].Owner != this )
							hurt = list[i];
					}

					if ( hurt is null )
						break;

					hurt.TakeDamage( this, this, HalfLifeDll.SkillData.BigmommaDmgSlash, DamageCrush | DamageSlash );
					hurt.PunchAngle = hurt.PunchAngle with { x = 15f };

					switch ( monsterEvent.Event )
					{
						case EventMeleeAttackBackRight:
							hurt.Velocity = hurt.Velocity + (forward * 150f) + new Vector3( 0f, 0f, 250f ) - (right * 200f);
							break;
						case EventMeleeAttackBackLeft:
							hurt.Velocity = hurt.Velocity + (forward * 150f) + new Vector3( 0f, 0f, 250f ) + (right * 200f);
							break;
						case EventMeleeAttack1:
							hurt.Velocity = hurt.Velocity + (forward * 220f) + new Vector3( 0f, 0f, 200f );
							break;
					}

					hurt.EntityFlags &= ~EntityFlags.OnGround;
					Sound.EmitDyn( this, SoundChannel.Weapon, RandomSound( AttackHitSounds ), 1f, Server.AttenuationNormal, 0, 100 + Server.RandomLong( -5, 5 ) );
					break;
				}

			case EventScream:
				EmitSoundArray( SoundChannel.Voice, AlertSounds );
				break;

			case EventPainSound:
				EmitSoundArray( SoundChannel.Voice, PainSounds );
				break;

			case EventAttackSound:
				EmitSoundArray( SoundChannel.Weapon, AttackSounds );
				break;

			case EventBirthSound:
				EmitSoundArray( SoundChannel.Body, BirthSounds );
				break;

			case EventSack:
				if ( Server.RandomLong( 0, 100 ) < 30 )
					EmitSoundArray( SoundChannel.Body, SackSounds );

				break;

			case EventDeathSound:
				EmitSoundArray( SoundChannel.Voice, DeathSounds );
				break;

			case EventStep1:
			case EventStep3:
				EmitSoundArray( SoundChannel.Item, FootSounds );
				break;

			case EventStep4:
			case EventStep2:
				EmitSoundArray( SoundChannel.Body, FootSounds );
				break;

			case EventMortarAttack1:
				LaunchMortar();
				break;

			case EventLayCrab:
				LayHeadcrab();
				break;

			case EventJumpForward:
				EntityFlags &= ~EntityFlags.OnGround;

				SetOrigin( Origin + new Vector3( 0f, 0f, 1f ) );
				Server.Globals.MakeVectors( Angles );

				Velocity = (Server.Globals.Forward * 200f) + (Server.Globals.Up * 500f);
				break;

			case EventEarlyTarget:
				if ( TargetEntity is { } target && !string.IsNullOrEmpty( target.Message ) )
					FireTargets( target.Message, this, this, UseType.Toggle, 0f );

				Remember( MemoryFiredNode );
				break;

			default:
				base.HandleAnimEvent( monsterEvent );
				break;
		}
	}

	public override void TraceAttack( BaseEntity attacker, float damage, Vector3 direction, in Trace trace, int damageType )
	{
		if ( trace.HitGroup != 1 )
		{
			if ( DamageTime != Server.Globals.Time || Server.RandomLong( 0, 10 ) < 1 )
			{
				Util.Ricochet( trace.EndPos, Server.RandomFloat( 1f, 2f ) );
				DamageTime = Server.Globals.Time;
			}

			damage = 0.1f;
		}
		else if ( Server.Globals.Time > _painSoundTime )
		{
			_painSoundTime = Server.Globals.Time + Server.RandomLong( 1, 3 );
			EmitSoundArray( SoundChannel.Voice, PainSounds );
		}

		base.TraceAttack( attacker, damage, direction, trace, damageType );
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		if ( (damageType & DamageAcid) != 0 )
			damage = 0f;

		if ( !HasMemory( MemoryPathFinished ) && Health <= damage )
		{
			Health = damage + 1f;
			Remember( MemoryAdvanceNode | MemoryCompletedNode );
			Util.Alert( AlertType.AiConsole, "BM: Finished node health!!!" );
		}

		return base.TakeDamage( inflictor, attacker, damage, damageType );
	}

	private void LayHeadcrab()
	{
		var child = Create( ChildClass, Origin, Angles, this );

		child.SpawnFlags |= SpawnFlagFallToGround;

		if ( HasMemory( MemoryChildPair ) )
		{
			_crabTime = Server.Globals.Time + Server.RandomFloat( 5f, 10f );
			Forget( MemoryChildPair );
		}
		else
		{
			_crabTime = Server.Globals.Time + Server.RandomFloat( 0.5f, 2.5f );
			Remember( MemoryChildPair );
		}

		var trace = Util.TraceLine( Origin, Origin - new Vector3( 0f, 0f, 100f ), IgnoreMonsters, this );

		Util.DecalTrace( trace, Decals.MommaBirth );

		Sound.EmitDyn( this, SoundChannel.Weapon, RandomSound( BirthSounds ), 1f, Server.AttenuationNormal, 0, 100 + Server.RandomLong( -5, 5 ) );
		_crabCount++;
	}

	public override void DeathNotice( BaseEntity child )
	{
		if ( _crabCount > 0 )
			_crabCount--;

		if ( IsAlive )
			EmitSoundArray( SoundChannel.Weapon, ChildDieSounds );
	}

	private void LaunchMortar()
	{
		_mortarTime = Server.Globals.Time + Server.RandomFloat( 2f, 15f );

		var start = Origin;

		start.z += 180f;

		Sound.EmitDyn( this, SoundChannel.Weapon, RandomSound( SackSounds ), 1f, Server.AttenuationNormal, 0, 100 + Server.RandomLong( -5, 5 ) );

		var bomb = BigMommaMortar.Shoot( this, start, MoveDir );

		bomb.Gravity = 1f;
		MortarSpray( start, new Vector3( 0f, 0f, 1f ), SpitSprite, 24 );
	}

	public override void Spawn()
	{
		Precache();

		SetModel( "models/big_mom.mdl" );
		SetSize( new Vector3( -32f, -32f, 0f ), new Vector3( 32f, 32f, 64f ) );

		Solid = Solid.SlideBox;
		MoveType = MoveType.Step;
		MonsterBloodColor = BloodColorGreen;
		Health = 150f * HalfLifeDll.SkillData.BigmommaHealthFactor;
		ViewOffset = new Vector3( 0f, 0f, 128f );
		FieldOfView = 0.3f;
		MonsterState = MonsterState.None;

		MonsterInit();
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/big_mom.mdl" );

		PrecacheSounds( ChildDieSounds );
		PrecacheSounds( SackSounds );
		PrecacheSounds( DeathSounds );
		PrecacheSounds( AttackSounds );
		PrecacheSounds( AttackHitSounds );
		PrecacheSounds( BirthSounds );
		PrecacheSounds( AlertSounds );
		PrecacheSounds( PainSounds );
		PrecacheSounds( FootSounds );

		Util.PrecacheOther( ChildClass );

		Server.PrecacheModel( "sprites/mommaspit.spr" );
		SpitSprite = Server.PrecacheModel( "sprites/mommaspout.spr" );
		Server.PrecacheModel( "sprites/mommablob.spr" );

		Server.PrecacheSound( "bullchicken/bc_acid1.wav" );
		Server.PrecacheSound( "bullchicken/bc_spithit1.wav" );
		Server.PrecacheSound( "bullchicken/bc_spithit2.wav" );
	}

	public override void Activate()
	{
		if ( TargetEntity is null )
			Remember( MemoryAdvanceNode );
	}

	private void NodeStart( string nextNode )
	{
		NetName = nextNode;

		BaseEntity target = null;

		if ( !string.IsNullOrEmpty( NetName ) )
			target = Util.FindEntityByTargetname( null, NetName );

		if ( target is null )
		{
			Util.Alert( AlertType.AiConsole, "BM: Finished the path!!" );
			Remember( MemoryPathFinished );
			return;
		}

		Remember( MemoryOnPath );
		TargetEntity = target;
	}

	private void NodeReach()
	{
		var target = TargetEntity;

		Forget( MemoryAdvanceNode );

		if ( target is null )
			return;

		if ( target.Health != 0f )
			MaxHealth = Health = target.Health * HalfLifeDll.SkillData.BigmommaHealthFactor;

		if ( !HasMemory( MemoryFiredNode ) && !string.IsNullOrEmpty( target.Message ) )
			FireTargets( target.Message, this, this, UseType.Toggle, 0f );

		Forget( MemoryFiredNode );

		NetName = target.Target;

		if ( target.Health == 0f )
			Remember( MemoryAdvanceNode );
	}

	public override bool CheckMeleeAttack1( float dot, float distance ) => dot >= 0.7 && distance <= AttackDistance;

	public override bool CheckMeleeAttack2( float dot, float distance ) => CanLayCrab();

	public override bool CheckRangeAttack1( float dot, float distance )
	{
		if ( distance > MortarDistance || _mortarTime >= Server.Globals.Time )
			return false;

		if ( EnemyEntity is not { } enemy )
			return false;

		var start = Origin;

		start.z += 180f;
		MoveDir = VecCheckSplatToss( start, enemy.BodyTarget( Origin ), Server.RandomFloat( 150f, 500f ) );

		return !MathLib.VectorCompare( MoveDir, Vector3.Zero );
	}

	public override Schedule GetScheduleOfType( int type )
	{
		return type switch
		{
			BigMommaSchedule.BigNode => BigMommaAi.BigNode,
			BigMommaSchedule.NodeFail => BigMommaAi.NodeFail,
			_ => base.GetScheduleOfType( type )
		};
	}

	private bool ShouldGoToNode() => HasMemory( MemoryAdvanceNode ) && _nodeTime < Server.Globals.Time;

	public override Schedule GetSchedule()
	{
		if ( ShouldGoToNode() )
			return GetScheduleOfType( BigMommaSchedule.BigNode );

		return base.GetSchedule();
	}

	public override void StartTask( Task task )
	{
		switch ( task.Id )
		{
			case BigMommaTask.FindNode:
				if ( !HasMemory( MemoryAdvanceNode ) && TargetEntity is { } current )
					NetName = current.Target;

				NodeStart( NetName );
				TaskComplete();
				Util.Alert( AlertType.AiConsole, $"BM: Found node {NetName}" );
				break;

			case BigMommaTask.NodeDelay:
				_nodeTime = Server.Globals.Time + task.Data;
				TaskComplete();
				Util.Alert( AlertType.AiConsole, $"BM: FAIL! Delay {task.Data:F2}" );
				break;

			case BigMommaTask.ProcessNode:
				Util.Alert( AlertType.AiConsole, $"BM: Reached node {NetName}" );
				NodeReach();
				TaskComplete();
				break;

			case BigMommaTask.PlayNodePresequence:
			case BigMommaTask.PlayNodeSequence:
				{
					var sequenceName = task.Id == BigMommaTask.PlayNodeSequence ? NodeSequence : NodePresequence;

					Util.Alert( AlertType.AiConsole, $"BM: Playing node sequence {sequenceName}" );

					if ( !string.IsNullOrEmpty( sequenceName ) )
					{
						var sequence = LookupSequence( sequenceName );

						if ( sequence != -1 )
						{
							Sequence = sequence;
							Frame = 0f;
							ResetSequenceInfo();
							Util.Alert( AlertType.AiConsole, $"BM: Sequence {NodeSequence}" );
							return;
						}
					}

					TaskComplete();
					break;
				}

			case BigMommaTask.NodeYaw:
				IdealYaw = NodeYaw;
				TaskComplete();
				break;

			case BigMommaTask.WaitNode:
				Wait = Server.Globals.Time + NodeDelay;

				if ( (TargetEntity.SpawnFlags & InfoBigMomma.SpawnFlagWait) != 0 )
					Util.Alert( AlertType.AiConsole, $"BM: Wait at node {NetName} forever" );
				else
					Util.Alert( AlertType.AiConsole, $"BM: Wait at node {NetName} for {NodeDelay:F2}" );

				break;

			case BigMommaTask.MoveToNodeRange:
				if ( TargetEntity is not { } target )
				{
					TaskFail();
				}
				else if ( MathLib.Length( target.Origin - Origin ) < NodeRange )
				{
					TaskComplete();
				}
				else
				{
					var activity = (target.SpawnFlags & InfoBigMomma.SpawnFlagRun) != 0 ? Activity.Run : Activity.Walk;

					MoveGoal = target.Origin;

					if ( !MoveToTarget( activity, 2f ) )
						TaskFail();
				}

				Util.Alert( AlertType.AiConsole, $"BM: Moving to node {NetName}" );
				break;

			case TaskId.MeleeAttack1:
				Sound.EmitDyn( this, SoundChannel.Voice, RandomSound( AttackSounds ), 1f, Server.AttenuationNormal, 0, Server.PitchNormal );
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
			case BigMommaTask.MoveToNodeRange:
				if ( TargetEntity is null )
				{
					TaskFail();
				}
				else if ( MathLib.Length2D( MoveGoal - Origin ) < NodeRange || MovementIsComplete )
				{
					Util.Alert( AlertType.AiConsole, "BM: Reached node!" );
					TaskComplete();
					RouteClear();
				}

				break;

			case BigMommaTask.WaitNode:
				if ( TargetEntity is { } target && (target.SpawnFlags & InfoBigMomma.SpawnFlagWait) != 0 )
					return;

				if ( Server.Globals.Time > WaitFinished )
					TaskComplete();

				Util.Alert( AlertType.AiConsole, "BM: The WAIT is over!" );
				break;

			case BigMommaTask.PlayNodePresequence:
			case BigMommaTask.PlayNodeSequence:
				if ( SequenceFinished )
				{
					Activity = Activity.Reset;
					TaskComplete();
				}

				break;

			default:
				base.RunTask( task );
				break;
		}
	}

	private Vector3 VecCheckSplatToss( Vector3 spot1, Vector3 spot2, float maxHeight )
	{
		var gravity = Cvar.GetFloat( "sv_gravity" );

		var midPoint = spot1 + ((spot2 - spot1) * 0.5f);
		var trace = Util.TraceLine( midPoint, midPoint + new Vector3( 0f, 0f, maxHeight ), IgnoreMonsters, this );
		var apex = trace.EndPos;

		trace = Util.TraceLine( spot1, apex, DontIgnoreMonsters, this );

		if ( trace.Fraction != 1.0 )
			return Vector3.Zero;

		var height = apex.z - spot1.z - 15f;
		var speed = MathF.Sqrt( 2f * gravity * height );
		var time = speed / gravity;
		var velocity = spot2 - spot1;

		velocity.z = 0f;
		velocity *= (float)(0.5 / time);
		velocity.z = speed;

		return velocity;
	}

	public static void MortarSpray( Vector3 position, Vector3 direction, int spriteModel, int count )
	{
		var server = Server.Current;

		server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, position );
		server.WriteByte( TempEntity.SpriteSpray );
		server.WriteCoord( position.x );
		server.WriteCoord( position.y );
		server.WriteCoord( position.z );
		server.WriteCoord( direction.x );
		server.WriteCoord( direction.y );
		server.WriteCoord( direction.z );
		server.WriteShort( spriteModel );
		server.WriteByte( count );
		server.WriteByte( 130 );
		server.WriteByte( 80 );
		server.MessageEnd();
	}
}
