using System;
using GoldSrc;

namespace HalfLife;

public static class SquidSchedule
{
	public const int HurtHop = ScheduleType.LastCommon + 1;
	public const int SmellFood = HurtHop + 1;
	public const int SeeCrab = HurtHop + 2;
	public const int Eat = HurtHop + 3;
	public const int SniffAndEat = HurtHop + 4;
	public const int Wallow = HurtHop + 5;
}

public static class SquidTask
{
	public const int HopTurn = TaskId.LastCommon + 1;
}

[LinkEntityToClass( "squidspit" )]
public class SquidSpit : BaseEntity
{
	[Save( FieldType.Integer )]
	private int _maxFrame;

	public override void Spawn()
	{
		MoveType = MoveType.Fly;
		ClassName = "squidspit";

		Solid = Solid.BBox;
		RenderMode = RenderMode.TransAlpha;
		RenderAmt = 255f;

		SetModel( "sprites/bigspit.spr" );
		Frame = 0f;
		Scale = 0.5f;

		SetSize( Vector3.Zero, Vector3.Zero );

		_maxFrame = Server.ModelFrames( ModelIndex ) - 1;
	}

	public void Animate()
	{
		NextThink = (float)(Server.Globals.Time + 0.1);

		var previous = Frame;

		Frame++;

		if ( previous != 0f && Frame > _maxFrame )
			Frame = 0f;
	}

	public static void Shoot( BaseEntity owner, Vector3 start, Vector3 velocity )
	{
		var spit = owner.Server.CreateEntity<SquidSpit>();

		spit.Spawn();

		spit.SetOrigin( start );
		spit.Velocity = velocity;
		spit.Owner = owner;

		spit.SetThink( spit.Animate );
		spit.NextThink = (float)(spit.Server.Globals.Time + 0.1);
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

		if ( other.DamageMode == DamageNo )
		{
			var trace = Util.TraceLine( Origin, Origin + (Velocity * 10f), DontIgnoreMonsters, this );

			Util.DecalTrace( trace, Decals.Spit1 + Server.RandomLong( 0, 1 ) );

			Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, trace.EndPos );
			Server.WriteByte( TempEntity.SpriteSpray );
			Server.WriteCoord( trace.EndPos.x );
			Server.WriteCoord( trace.EndPos.y );
			Server.WriteCoord( trace.EndPos.z );
			Server.WriteCoord( trace.PlaneNormal.x );
			Server.WriteCoord( trace.PlaneNormal.y );
			Server.WriteCoord( trace.PlaneNormal.z );
			Server.WriteShort( Bullsquid.SpitSprite );
			Server.WriteByte( 5 );
			Server.WriteByte( 30 );
			Server.WriteByte( 80 );
			Server.MessageEnd();
		}
		else
		{
			other.TakeDamage( this, this, HalfLifeDll.SkillData.BullsquidDmgSpit, DamageGeneric );
		}

		SetThink( SubRemove );
		NextThink = Server.Globals.Time;
	}
}

[SkipHotload]
public static class BullsquidAi
{
	public static readonly Schedule SquidRangeAttack1 = new( "Squid Range Attack1",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionEnemyDead | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionEnemyOccluded | BaseMonster.ConditionNoAmmoLoaded,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.FaceIdeal ),
		new( TaskId.RangeAttack1 ),
		new( TaskId.SetActivity, (float)Activity.Idle ) );

	public static readonly Schedule SquidChaseEnemy = new( "Squid Chase Enemy",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionEnemyDead | BaseMonster.ConditionSmellFood | BaseMonster.ConditionCanRangeAttack1 | BaseMonster.ConditionCanMeleeAttack1 | BaseMonster.ConditionCanMeleeAttack2 | BaseMonster.ConditionTaskFailed | BaseMonster.ConditionHearSound,
		WorldSound.Danger | WorldSound.Meat,
		new( TaskId.SetFailSchedule, ScheduleType.RangeAttack1 ),
		new( TaskId.GetPathToEnemy ),
		new( TaskId.RunPath ),
		new( TaskId.WaitForMovement ) );

	public static readonly Schedule SquidHurtHop = new( "SquidHurtHop",
		0,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.SoundWake ),
		new( SquidTask.HopTurn ),
		new( TaskId.FaceEnemy ) );

	public static readonly Schedule SquidSeeCrab = new( "SquidSeeCrab",
		BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.SoundWake ),
		new( TaskId.PlaySequence, (float)Activity.Excited ),
		new( TaskId.FaceEnemy ) );

	public static readonly Schedule SquidEat = new( "SquidEat",
		BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionNewEnemy,
		WorldSound.Meat | WorldSound.Carcass,
		new( TaskId.StopMoving ),
		new( TaskId.Eat, 10f ),
		new( TaskId.StoreLastPosition ),
		new( TaskId.GetPathToBestScent ),
		new( TaskId.WalkPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.PlaySequence, (float)Activity.Eat ),
		new( TaskId.PlaySequence, (float)Activity.Eat ),
		new( TaskId.PlaySequence, (float)Activity.Eat ),
		new( TaskId.Eat, 50f ),
		new( TaskId.GetPathToLastPosition ),
		new( TaskId.WalkPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.ClearLastPosition ) );

	public static readonly Schedule SquidSniffAndEat = new( "SquidSniffAndEat",
		BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionNewEnemy,
		WorldSound.Meat | WorldSound.Carcass,
		new( TaskId.StopMoving ),
		new( TaskId.Eat, 10f ),
		new( TaskId.PlaySequence, (float)Activity.DetectScent ),
		new( TaskId.StoreLastPosition ),
		new( TaskId.GetPathToBestScent ),
		new( TaskId.WalkPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.PlaySequence, (float)Activity.Eat ),
		new( TaskId.PlaySequence, (float)Activity.Eat ),
		new( TaskId.PlaySequence, (float)Activity.Eat ),
		new( TaskId.Eat, 50f ),
		new( TaskId.GetPathToLastPosition ),
		new( TaskId.WalkPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.ClearLastPosition ) );

	public static readonly Schedule SquidWallow = new( "SquidWallow",
		BaseMonster.ConditionLightDamage | BaseMonster.ConditionHeavyDamage | BaseMonster.ConditionNewEnemy,
		WorldSound.Garbage,
		new( TaskId.StopMoving ),
		new( TaskId.Eat, 10f ),
		new( TaskId.StoreLastPosition ),
		new( TaskId.GetPathToBestScent ),
		new( TaskId.WalkPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.PlaySequence, (float)Activity.InspectFloor ),
		new( TaskId.Eat, 50f ),
		new( TaskId.GetPathToLastPosition ),
		new( TaskId.WalkPath ),
		new( TaskId.WaitForMovement ),
		new( TaskId.ClearLastPosition ) );

	public static readonly Schedule[] List =
	[
		SquidRangeAttack1,
		SquidChaseEnemy,
		SquidHurtHop,
		SquidSeeCrab,
		SquidEat,
		SquidSniffAndEat,
		SquidWallow
	];
}

[LinkEntityToClass( "monster_bullchicken" )]
public class Bullsquid : BaseMonster
{
	private const float SprintDistance = 256f;
	private const float AttenuationIdle = 1.5f;

	private const int AnimEventSpit = 1;
	private const int AnimEventBite = 2;
	private const int AnimEventBlink = 3;
	private const int AnimEventTailWhip = 4;
	private const int AnimEventHop = 5;
	private const int AnimEventThrow = 6;

	private static readonly short[] SquidHints = [Hint.WorldHumanBlood];

	private static readonly string[] AttackSounds = ["bullchicken/bc_attack2.wav", "bullchicken/bc_attack3.wav"];
	private static readonly string[] DeathSounds = ["bullchicken/bc_die1.wav", "bullchicken/bc_die2.wav", "bullchicken/bc_die3.wav"];
	private static readonly string[] IdleSounds = ["bullchicken/bc_idle1.wav", "bullchicken/bc_idle2.wav", "bullchicken/bc_idle3.wav", "bullchicken/bc_idle4.wav", "bullchicken/bc_idle5.wav"];
	private static readonly string[] PainSounds = ["bullchicken/bc_pain1.wav", "bullchicken/bc_pain2.wav", "bullchicken/bc_pain3.wav", "bullchicken/bc_pain4.wav"];
	private static readonly string[] GrowlSounds = ["bullchicken/bc_attackgrowl.wav", "bullchicken/bc_attackgrowl2.wav", "bullchicken/bc_attackgrowl3.wav"];
	private static readonly string[] BiteSounds = ["bullchicken/bc_bite2.wav", "bullchicken/bc_bite3.wav"];

	public static int SpitSprite { get; private set; }

	[Save( FieldType.Boolean )]
	private bool _canThreatDisplay;
	[Save( FieldType.Time )]
	private float _lastHurtTime;
	[Save( FieldType.Time )]
	private float _nextSpitTime;

	public override Schedule ScheduleFromName( string name ) => ScheduleInList( name, BullsquidAi.List ) ?? base.ScheduleFromName( name );

	public override int IgnoreConditions()
	{
		var ignore = base.IgnoreConditions();

		if ( Server.Globals.Time - _lastHurtTime <= 20f )
			ignore = ConditionSmell | ConditionSmellFood;

		if ( EnemyEntity is { ClassName: "monster_headcrab" } )
			ignore = ConditionSmell | ConditionSmellFood;

		return ignore;
	}

	public override int Relationship( BaseEntity target )
	{
		if ( Server.Globals.Time - _lastHurtTime < 5f && target.ClassName == "monster_headcrab" )
			return RelationshipNone;

		return base.Relationship( target );
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		if ( EnemyEntity is { } enemy && IsMoving && attacker == enemy && Server.Globals.Time - _lastHurtTime > 3f )
		{
			var distance = MathLib.Length2D( Origin - enemy.Origin );

			if ( distance > SprintDistance )
			{
				distance = MathLib.Length2D( Origin - Route[RouteIndex].Location );

				if ( Triangulate( Origin, Route[RouteIndex].Location, (float)(distance * 0.5), enemy, out var apex ) )
					InsertWaypoint( apex, MoveFlag.ToDetour | MoveFlag.DontSimplify );
			}
		}

		if ( attacker.ClassName != "monster_headcrab" )
			_lastHurtTime = Server.Globals.Time;

		return base.TakeDamage( inflictor, attacker, damage, damageType );
	}

	public override bool CheckRangeAttack1( float dot, float distance )
	{
		if ( IsMoving && distance >= 512f )
			return false;

		if ( distance > 64f && distance <= 784f && dot >= 0.5 && Server.Globals.Time >= _nextSpitTime )
		{
			if ( EnemyEntity is { } enemy && Math.Abs( Origin.z - enemy.Origin.z ) > 256f )
				return false;

			_nextSpitTime = IsMoving ? Server.Globals.Time + 5f : (float)(Server.Globals.Time + 0.5);

			return true;
		}

		return false;
	}

	public override bool CheckMeleeAttack1( float dot, float distance )
	{
		return EnemyEntity.Health <= HalfLifeDll.SkillData.BullsquidDmgWhip && distance <= 85f && dot >= 0.7;
	}

	public override bool CheckMeleeAttack2( float dot, float distance )
	{
		return distance <= 85f && dot >= 0.7 && !HasConditions( ConditionCanMeleeAttack1 );
	}

	public override bool ValidateHintType( short hint )
	{
		for ( var i = 0; i < SquidHints.Length; i++ )
		{
			if ( SquidHints[i] == hint )
				return true;
		}

		Util.Alert( AlertType.AiConsole, "Couldn't validate hint type" );
		return false;
	}

	public override int SoundMask()
	{
		return WorldSound.World | WorldSound.Combat | WorldSound.Carcass | WorldSound.Meat | WorldSound.Garbage | WorldSound.Player;
	}

	public override int Classify() => ClassAlienPredator;

	public override void IdleSound()
	{
		Sound.Emit( this, SoundChannel.Voice, RandomSound( IdleSounds ), 1f, AttenuationIdle );
	}

	public override void PainSound()
	{
		var pitch = Server.RandomLong( 85, 120 );

		Sound.EmitDyn( this, SoundChannel.Voice, RandomSound( PainSounds ), 1f, Server.AttenuationNormal, 0, pitch );
	}

	public override void AlertSound()
	{
		var pitch = Server.RandomLong( 140, 160 );

		Sound.EmitDyn( this, SoundChannel.Voice, IdleSounds[Server.RandomLong( 0, 1 )], 1f, Server.AttenuationNormal, 0, pitch );
	}

	public override void SetYawSpeed() => YawSpeed = 90f;

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		var skill = HalfLifeDll.SkillData;
		var globals = Server.Globals;

		switch ( monsterEvent.Event )
		{
			case AnimEventSpit:
				{
					var enemy = EnemyEntity;

					globals.MakeVectors( Angles );

					var spitOffset = Origin + ((globals.Right * 8f) + (globals.Forward * 37f) + (globals.Up * 23f));
					var spitDirection = Util.Normalize( enemy.Origin + enemy.ViewOffset - spitOffset );

					spitDirection.x += Server.RandomFloat( -0.05f, 0.05f );
					spitDirection.y += Server.RandomFloat( -0.05f, 0.05f );
					spitDirection.z += Server.RandomFloat( -0.05f, 0f );

					AttackSound();

					Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, spitOffset );
					Server.WriteByte( TempEntity.SpriteSpray );
					Server.WriteCoord( spitOffset.x );
					Server.WriteCoord( spitOffset.y );
					Server.WriteCoord( spitOffset.z );
					Server.WriteCoord( spitDirection.x );
					Server.WriteCoord( spitDirection.y );
					Server.WriteCoord( spitDirection.z );
					Server.WriteShort( SpitSprite );
					Server.WriteByte( 15 );
					Server.WriteByte( 210 );
					Server.WriteByte( 25 );
					Server.MessageEnd();

					SquidSpit.Shoot( this, spitOffset, spitDirection * 900f );
					break;
				}

			case AnimEventBite:
				if ( CheckTraceHullAttack( 70f, (int)skill.BullsquidDmgBite, DamageSlash ) is { } bitten )
				{
					bitten.Velocity -= globals.Forward * 100f;
					bitten.Velocity += globals.Up * 100f;
				}

				break;

			case AnimEventTailWhip:
				if ( CheckTraceHullAttack( 70f, (int)skill.BullsquidDmgWhip, DamageClub | DamageAlwaysGib ) is { } whipped )
				{
					whipped.PunchAngle = whipped.PunchAngle with { z = -20f, x = 20f };
					whipped.Velocity += globals.Right * 200f;
					whipped.Velocity += globals.Up * 100f;
				}

				break;

			case AnimEventBlink:
				Skin = 1;
				break;

			case AnimEventHop:
				{
					var gravity = Cvar.GetFloat( "sv_gravity" );

					EntityFlags &= ~EntityFlags.OnGround;
					Velocity = Velocity with { z = (float)(Velocity.z + (0.625 * gravity * 0.5)) };
					break;
				}

			case AnimEventThrow:
				if ( CheckTraceHullAttack( 70f, 0, 0 ) is { } thrown )
				{
					var pitch = (int)Server.RandomFloat( 90f, 110f );

					Sound.EmitDyn( this, SoundChannel.Weapon, RandomSound( BiteSounds ), 1f, Server.AttenuationNormal, 0, pitch );

					Util.ScreenShake( thrown.Origin, 25f, 1.5f, 0.7f, 2f );

					if ( thrown.IsPlayer )
					{
						globals.MakeVectors( Angles );
						thrown.Velocity = thrown.Velocity + (globals.Forward * 300f) + (globals.Up * 300f);
					}
				}

				break;

			default:
				base.HandleAnimEvent( monsterEvent );
				break;
		}
	}

	public override void Spawn()
	{
		Precache();

		SetModel( "models/bullsquid.mdl" );
		SetSize( new Vector3( -32f, -32f, 0f ), new Vector3( 32f, 32f, 64f ) );

		Solid = Solid.SlideBox;
		MoveType = MoveType.Step;
		MonsterBloodColor = BloodColorGreen;
		Effects = 0;
		Health = HalfLifeDll.SkillData.BullsquidHealth;
		FieldOfView = 0.2f;
		MonsterState = MonsterState.None;

		_canThreatDisplay = true;
		_nextSpitTime = Server.Globals.Time;

		MonsterInit();
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/bullsquid.mdl" );
		Server.PrecacheModel( "sprites/bigspit.spr" );

		SpitSprite = Server.PrecacheModel( "sprites/tinyspit.spr" );

		Server.PrecacheSound( "zombie/claw_miss2.wav" );

		PrecacheSounds( AttackSounds );
		PrecacheSounds( DeathSounds );
		PrecacheSounds( IdleSounds );
		PrecacheSounds( PainSounds );
		PrecacheSounds( GrowlSounds );

		Server.PrecacheSound( "bullchicken/bc_acid1.wav" );

		PrecacheSounds( BiteSounds );

		Server.PrecacheSound( "bullchicken/bc_spithit1.wav" );
		Server.PrecacheSound( "bullchicken/bc_spithit2.wav" );
	}

	public override void DeathSound()
	{
		Sound.Emit( this, SoundChannel.Voice, RandomSound( DeathSounds ), 1f, Server.AttenuationNormal );
	}

	public void AttackSound()
	{
		Sound.Emit( this, SoundChannel.Weapon, RandomSound( AttackSounds ), 1f, Server.AttenuationNormal );
	}

	public override void RunAi()
	{
		base.RunAi();

		if ( Skin != 0 )
			Skin = 0;

		if ( Server.RandomLong( 0, 39 ) == 0 )
			Skin = 1;

		if ( EnemyEntity is { } enemy && Activity == Activity.Run && MathLib.Length2D( Origin - enemy.Origin ) < SprintDistance )
			FrameRate = 1.25f;
	}

	private Schedule GetFoodSchedule()
	{
		if ( BestScent() is { } scent && (!IsInViewCone( scent.Origin ) || !IsVisible( scent.Origin )) )
			return GetScheduleOfType( SquidSchedule.SniffAndEat );

		return GetScheduleOfType( SquidSchedule.Eat );
	}

	public override Schedule GetSchedule()
	{
		switch ( MonsterState )
		{
			case MonsterState.Alert:
				if ( HasConditions( ConditionLightDamage | ConditionHeavyDamage ) )
					return GetScheduleOfType( SquidSchedule.HurtHop );

				if ( HasConditions( ConditionSmellFood ) )
					return GetFoodSchedule();

				if ( HasConditions( ConditionSmell ) && BestScent() is not null )
					return GetScheduleOfType( SquidSchedule.Wallow );

				break;

			case MonsterState.Combat:
				if ( HasConditions( ConditionEnemyDead ) )
					return GetBaseSchedule();

				if ( HasConditions( ConditionNewEnemy ) )
				{
					if ( _canThreatDisplay && Relationship( EnemyEntity ) == RelationshipHate )
					{
						_canThreatDisplay = false;
						return GetScheduleOfType( SquidSchedule.SeeCrab );
					}

					return GetScheduleOfType( ScheduleType.WakeAngry );
				}

				if ( HasConditions( ConditionSmellFood ) )
					return GetFoodSchedule();

				if ( HasConditions( ConditionCanRangeAttack1 ) )
					return GetScheduleOfType( ScheduleType.RangeAttack1 );

				if ( HasConditions( ConditionCanMeleeAttack1 ) )
					return GetScheduleOfType( ScheduleType.MeleeAttack1 );

				if ( HasConditions( ConditionCanMeleeAttack2 ) )
					return GetScheduleOfType( ScheduleType.MeleeAttack2 );

				return GetScheduleOfType( ScheduleType.ChaseEnemy );
		}

		return GetBaseSchedule();
	}

	public override Schedule GetScheduleOfType( int type )
	{
		return type switch
		{
			ScheduleType.RangeAttack1 => BullsquidAi.SquidRangeAttack1,
			SquidSchedule.HurtHop => BullsquidAi.SquidHurtHop,
			SquidSchedule.SeeCrab => BullsquidAi.SquidSeeCrab,
			SquidSchedule.Eat => BullsquidAi.SquidEat,
			SquidSchedule.SniffAndEat => BullsquidAi.SquidSniffAndEat,
			SquidSchedule.Wallow => BullsquidAi.SquidWallow,
			ScheduleType.ChaseEnemy => BullsquidAi.SquidChaseEnemy,
			_ => base.GetScheduleOfType( type )
		};
	}

	public override void StartTask( Task task )
	{
		TaskStatus = TaskState.Running;

		switch ( task.Id )
		{
			case TaskId.MeleeAttack2:
				Sound.Emit( this, SoundChannel.Voice, RandomSound( GrowlSounds ), 1f, Server.AttenuationNormal );

				base.StartTask( task );
				break;

			case SquidTask.HopTurn:
				SetActivity( Activity.Hop );
				MakeIdealYaw( EnemyLastKnownPosition );
				break;

			case TaskId.GetPathToEnemy:
				if ( BuildRoute( EnemyEntity.Origin, MoveFlag.ToEnemy, EnemyEntity ) )
				{
					TaskStatus = TaskState.Complete;
				}
				else
				{
					Util.Alert( AlertType.AiConsole, "GetPathToEnemy failed!!" );
					TaskFail();
				}

				break;

			default:
				base.StartTask( task );
				break;
		}
	}

	public override void RunTask( Task task )
	{
		if ( task.Id != SquidTask.HopTurn )
		{
			base.RunTask( task );
			return;
		}

		MakeIdealYaw( EnemyLastKnownPosition );
		ChangeYaw( (int)YawSpeed );

		if ( SequenceFinished )
			TaskStatus = TaskState.Complete;
	}

	public override MonsterState GetIdealState()
	{
		var conditions = ScheduleFlags();

		if ( MonsterState == MonsterState.Combat
			&& EnemyEntity is { ClassName: "monster_headcrab" }
			&& (conditions & (ConditionLightDamage | ConditionHeavyDamage)) != 0 )
		{
			EnemyEntity = null;
			IdealMonsterState = MonsterState.Alert;
		}

		IdealMonsterState = base.GetIdealState();

		return IdealMonsterState;
	}
}
