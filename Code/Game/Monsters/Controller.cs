using System;
using GoldSrc;

namespace HalfLife;

[SkipHotload]
public static class ControllerAi
{
	public static readonly Schedule ControllerChaseEnemy = new( "ControllerChaseEnemy",
		BaseMonster.ConditionNewEnemy | BaseMonster.ConditionTaskFailed,
		0,
		new( TaskId.GetPathToEnemy, 128f ),
		new( TaskId.WaitForMovement ) );

	public static readonly Schedule ControllerStrafe = new( "ControllerStrafe",
		BaseMonster.ConditionNewEnemy,
		0,
		new( TaskId.Wait, 0.2f ),
		new( TaskId.GetPathToEnemy, 128f ),
		new( TaskId.WaitForMovement ),
		new( TaskId.Wait, 1f ) );

	public static readonly Schedule ControllerTakeCover = new( "ControllerTakeCover",
		BaseMonster.ConditionNewEnemy,
		0,
		new( TaskId.Wait, 0.2f ),
		new( TaskId.FindCoverFromEnemy ),
		new( TaskId.WaitForMovement ),
		new( TaskId.Wait, 1f ) );

	public static readonly Schedule ControllerFail = new( "ControllerFail",
		0,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.SetActivity, (float)Activity.Idle ),
		new( TaskId.Wait, 2f ),
		new( TaskId.WaitPvs ) );

	public static readonly Schedule[] List =
	[
		ControllerChaseEnemy,
		ControllerStrafe,
		ControllerTakeCover,
		ControllerFail
	];
}

[LinkEntityToClass( "monster_alien_controller" )]
public class Controller : SquadMonster
{
	private const int AnimEventHeadOpen = 1;
	private const int AnimEventBallShoot = 2;
	private const int AnimEventSmallShoot = 3;
	private const int AnimEventPowerUpFull = 4;
	private const int AnimEventPowerUpHalf = 5;

	private const float DistanceToCheck = 200f;
	private const int MoveStrafe = 1;

	private static readonly string[] AttackSounds = ["controller/con_attack1.wav", "controller/con_attack2.wav", "controller/con_attack3.wav"];
	private static readonly string[] IdleSounds = ["controller/con_idle1.wav", "controller/con_idle2.wav", "controller/con_idle3.wav", "controller/con_idle4.wav", "controller/con_idle5.wav"];
	private static readonly string[] AlertSounds = ["controller/con_alert1.wav", "controller/con_alert2.wav", "controller/con_alert3.wav"];
	private static readonly string[] PainSounds = ["controller/con_pain1.wav", "controller/con_pain2.wav", "controller/con_pain3.wav"];
	private static readonly string[] DeathSounds = ["controller/con_die1.wav", "controller/con_die2.wav"];

	[Save( FieldType.ClassPtr )]
	private readonly Sprite[] _balls = new Sprite[2];
	[Save( FieldType.Integer )]
	private readonly int[] _ballBrightness = new int[2];
	[Save( FieldType.Time )]
	private readonly float[] _ballTime = new float[2];
	[Save( FieldType.Integer )]
	private readonly int[] _ballCurrent = new int[2];
	[Save( FieldType.Vector )]
	private Vector3 _estimatedVelocity;

	private float _shootTime;
	private float _shootEnd;
	private Vector3 _velocity;
	private bool _inCombat;

	public override Schedule ScheduleFromName( string name ) => ScheduleInList( name, ControllerAi.List ) ?? base.ScheduleFromName( name );

	public override int Classify() => ClassAlienMilitary;

	public override bool CanBecomeRagdoll() => true;

	public override void SetYawSpeed() => YawSpeed = 120f;

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		if ( IsAlive )
			PainSound();

		return base.TakeDamage( inflictor, attacker, damage, damageType );
	}

	public override void Killed( BaseEntity attacker, int gib )
	{
		for ( var i = 0; i < _balls.Length; i++ )
		{
			_balls[i]?.SubStartFadeOut();
			_balls[i] = null;
		}

		base.Killed( attacker, gib );
	}

	public override void GibMonster()
	{
		for ( var i = 0; i < _balls.Length; i++ )
		{
			_balls[i]?.Remove();
			_balls[i] = null;
		}

		base.GibMonster();
	}

	private void EmitSoundArray( string[] sounds )
	{
		Sound.EmitDyn( this, SoundChannel.Voice, RandomSound( sounds ), 1f, Server.AttenuationNormal, 0, Server.RandomLong( 95, 105 ) );
	}

	public override void PainSound()
	{
		if ( Server.RandomLong( 0, 5 ) < 2 )
			EmitSoundArray( PainSounds );
	}

	public override void AlertSound() => EmitSoundArray( AlertSounds );

	public override void IdleSound() => EmitSoundArray( IdleSounds );

	public void AttackSound() => EmitSoundArray( AttackSounds );

	public override void DeathSound() => EmitSoundArray( DeathSounds );

	private void WriteHeadLight( Vector3 origin, float radius, int life, float decay )
	{
		Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
		Server.WriteByte( TempEntity.ELight );
		Server.WriteShort( Index + 0x1000 );
		Server.WriteCoord( origin.x );
		Server.WriteCoord( origin.y );
		Server.WriteCoord( origin.z );
		Server.WriteCoord( radius );
		Server.WriteByte( 255 );
		Server.WriteByte( 192 );
		Server.WriteByte( 64 );
		Server.WriteByte( life );
		Server.WriteCoord( decay );
		Server.MessageEnd();
	}

	private void SetBalls( int left, int right, string options )
	{
		_ballBrightness[0] = left;
		_ballTime[0] = (float)(Server.Globals.Time + (Util.Atoi( options ) / 15.0));
		_ballBrightness[1] = right;
		_ballTime[1] = (float)(Server.Globals.Time + (Util.Atoi( options ) / 15.0));
	}

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		switch ( monsterEvent.Event )
		{
			case AnimEventHeadOpen:
				WriteHeadLight( Server.GetAttachment( this, 0 ), 1f, 20, -32f );

				SetBalls( 192, 255, monsterEvent.Options );
				break;

			case AnimEventBallShoot:
				{
					var start = Server.GetAttachment( this, 0 );

					WriteHeadLight( Vector3.Zero, 32f, 10, 32f );

					var ball = (BaseMonster)Create( "controller_head_ball", start, Angles, this );

					ball.Velocity = new Vector3( 0f, 0f, 32f );
					ball.EnemyEntity = EnemyEntity;

					_ballBrightness[0] = 0;
					_ballBrightness[1] = 0;
					break;
				}

			case AnimEventSmallShoot:
				AttackSound();

				_shootTime = Server.Globals.Time;
				_shootEnd = (float)(_shootTime + (Util.Atoi( monsterEvent.Options ) / 15.0));
				break;

			case AnimEventPowerUpFull:
				SetBalls( 255, 255, monsterEvent.Options );
				break;

			case AnimEventPowerUpHalf:
				SetBalls( 192, 192, monsterEvent.Options );
				break;

			default:
				base.HandleAnimEvent( monsterEvent );
				break;
		}
	}

	public override void Spawn()
	{
		Precache();

		SetModel( "models/controller.mdl" );
		SetSize( new Vector3( -32f, -32f, 0f ), new Vector3( 32f, 32f, 64f ) );

		Solid = Solid.SlideBox;
		MoveType = MoveType.Fly;
		EntityFlags |= EntityFlags.Fly;
		MonsterBloodColor = BloodColorGreen;
		Health = HalfLifeDll.SkillData.ControllerHealth;
		ViewOffset = new Vector3( 0f, 0f, -2f );
		FieldOfView = ViewFieldFull;
		MonsterState = MonsterState.None;

		MonsterInit();
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/controller.mdl" );

		PrecacheSounds( AttackSounds );
		PrecacheSounds( IdleSounds );
		PrecacheSounds( AlertSounds );
		PrecacheSounds( PainSounds );
		PrecacheSounds( DeathSounds );

		Server.PrecacheModel( "sprites/xspark4.spr" );

		Util.PrecacheOther( "controller_energy_ball" );
		Util.PrecacheOther( "controller_head_ball" );
	}

	public override void StartTask( Task task )
	{
		switch ( task.Id )
		{
			case TaskId.GetPathToEnemyLkp:
				if ( BuildNearestRoute( EnemyLastKnownPosition, ViewOffset, task.Data, MathLib.Length( EnemyLastKnownPosition - Origin ) + 1024f ) )
				{
					TaskComplete();
				}
				else
				{
					Util.Alert( AlertType.AiConsole, "GetPathToEnemyLKP failed!!\n" );
					TaskFail();
				}

				break;

			case TaskId.GetPathToEnemy:
				if ( EnemyEntity is not { } enemy )
				{
					TaskFail();
					return;
				}

				if ( BuildNearestRoute( enemy.Origin, enemy.ViewOffset, task.Data, MathLib.Length( enemy.Origin - Origin ) + 1024f ) )
				{
					TaskComplete();
				}
				else
				{
					Util.Alert( AlertType.AiConsole, "GetPathToEnemy failed!!\n" );
					TaskFail();
				}

				break;

			default:
				base.StartTask( task );
				break;
		}
	}

	private static Vector3 Intersect( Vector3 source, Vector3 destination, Vector3 move, float speed )
	{
		var to = destination - source;

		var a = Vector3.Dot( move, move ) - (speed * speed);
		var b = 0f * Vector3.Dot( to, move );
		var c = Vector3.Dot( to, to );

		float t;

		if ( a == 0f )
		{
			t = c / (speed * speed);
		}
		else
		{
			t = (b * b) - (4f * a * c);
			t = (float)(Math.Sqrt( t ) / (2.0 * a));

			var t1 = -b + t;
			var t2 = -b - t;

			t = t1 < 0f || t2 < t1 ? t2 : t1;
		}

		if ( t < 0.1 )
			t = 0.1f;

		if ( t > 10.0 )
			t = 10f;

		return Util.Normalize( to + (move * t) ) * speed;
	}

	public int LookupFloat()
	{
		if ( MathLib.Length( _velocity ) < 32.0 )
			return LookupSequence( "up" );

		var globals = Server.Globals;

		Util.MakeAimVectors( Angles, globals );

		var x = Vector3.Dot( globals.Forward, _velocity );
		var y = Vector3.Dot( globals.Right, _velocity );
		var z = Vector3.Dot( globals.Up, _velocity );

		if ( Math.Abs( x ) > Math.Abs( y ) && Math.Abs( x ) > Math.Abs( z ) )
			return LookupSequence( x > 0f ? "forward" : "backward" );

		if ( Math.Abs( y ) > Math.Abs( z ) )
			return LookupSequence( y > 0f ? "right" : "left" );

		return LookupSequence( z > 0f ? "up" : "down" );
	}

	private void ShootBalls()
	{
		var globals = Server.Globals;
		var speedBall = HalfLifeDll.SkillData.ControllerSpeedBall;
		var hand = Server.GetAttachment( this, 2 );

		while ( _shootTime < _shootEnd && _shootTime < globals.Time )
		{
			var source = hand + (Velocity * (_shootTime - globals.Time));

			if ( EnemyEntity is { } enemy )
			{
				_estimatedVelocity = HasConditions( ConditionSeeEnemy ) ? (_estimatedVelocity * 0.5f) + (enemy.Velocity * 0.5f) : _estimatedVelocity * 0.8f;

				var direction = Intersect( source, enemy.BodyTarget( Origin ), _estimatedVelocity, speedBall );
				var delta = 0.03490f;

				direction += new Vector3( Server.RandomFloat( -delta, delta ), Server.RandomFloat( -delta, delta ), Server.RandomFloat( -delta, delta ) ) * speedBall;

				source += direction * (globals.Time - _shootTime);

				var ball = Create( "controller_energy_ball", source, Angles, this );

				ball.Velocity = direction;
			}

			_shootTime = (float)(_shootTime + 0.2);
		}

		if ( _shootTime > _shootEnd )
		{
			_ballBrightness[0] = 64;
			_ballTime[0] = _shootEnd;
			_ballBrightness[1] = 64;
			_ballTime[1] = _shootEnd;
			_inCombat = false;
		}
	}

	private void StartSequence( int sequence )
	{
		Sequence = sequence;
		Frame = 0f;
		ResetSequenceInfo();
	}

	public override void RunTask( Task task )
	{
		if ( _shootEnd > Server.Globals.Time )
			ShootBalls();

		if ( task.Id is not (TaskId.WaitForMovement or TaskId.Wait or TaskId.WaitFaceEnemy or TaskId.WaitPvs) )
		{
			base.RunTask( task );
			return;
		}

		MakeIdealYaw( EnemyLastKnownPosition );
		ChangeYaw( (int)YawSpeed );

		if ( SequenceFinished )
			_inCombat = false;

		base.RunTask( task );

		if ( _inCombat )
			return;

		if ( HasConditions( ConditionCanRangeAttack1 ) )
		{
			StartSequence( LookupActivity( (int)Activity.RangeAttack1 ) );
			_inCombat = true;
		}
		else if ( HasConditions( ConditionCanRangeAttack2 ) )
		{
			StartSequence( LookupActivity( (int)Activity.RangeAttack2 ) );
			_inCombat = true;
		}
		else
		{
			var floatSequence = LookupFloat();

			if ( SequenceFinished || floatSequence != Sequence )
				StartSequence( floatSequence );
		}
	}

	public override Schedule GetScheduleOfType( int type )
	{
		return type switch
		{
			ScheduleType.ChaseEnemy => ControllerAi.ControllerChaseEnemy,
			ScheduleType.RangeAttack1 => ControllerAi.ControllerStrafe,
			ScheduleType.RangeAttack2 or ScheduleType.MeleeAttack1 or ScheduleType.MeleeAttack2 or ScheduleType.TakeCoverFromEnemy => ControllerAi.ControllerTakeCover,
			ScheduleType.Fail => ControllerAi.ControllerFail,
			_ => MonsterScheduleOfType( type )
		};
	}

	public override bool CheckRangeAttack1( float dot, float distance ) => dot > 0.5 && distance > 256f && distance <= 2048f;

	public override bool CheckRangeAttack2( float dot, float distance ) => dot > 0.5 && distance > 64f && distance <= 2048f;

	public override bool CheckMeleeAttack1( float dot, float distance ) => false;

	public override void SetActivity( Activity activity )
	{
		base.SetActivity( activity );

		GroundSpeed = 100f;
	}

	public override void RunAi()
	{
		base.RunAi();

		if ( HasMemory( MemoryKilled ) )
			return;

		for ( var i = 0; i < 2; i++ )
		{
			if ( _balls[i] is null )
			{
				_balls[i] = Sprite.SpriteCreate( Server, "sprites/xspark4.spr", Origin, true );
				_balls[i].SetTransparency( RenderMode.Glow, 255, 255, 255, 255, RenderFx.NoDissipation );
				_balls[i].SetAttachment( this, i + 3 );
				_balls[i].SetScale( 1f );
			}

			var t = _ballTime[i] - Server.Globals.Time;

			t = t > 0.1 ? (float)(0.1 / t) : 1f;

			_ballCurrent[i] = (int)(_ballCurrent[i] + ((_ballBrightness[i] - _ballCurrent[i]) * t));

			_balls[i].SetBrightness( _ballCurrent[i] );

			var start = Server.GetAttachment( this, i + 2 );

			Server.SetOrigin( _balls[i], start );

			Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
			Server.WriteByte( TempEntity.ELight );
			Server.WriteShort( Index + (0x1000 * (i + 3)) );
			Server.WriteCoord( start.x );
			Server.WriteCoord( start.y );
			Server.WriteCoord( start.z );
			Server.WriteCoord( _ballCurrent[i] / 8 );
			Server.WriteByte( 255 );
			Server.WriteByte( 192 );
			Server.WriteByte( 64 );
			Server.WriteByte( 5 );
			Server.WriteCoord( 0f );
			Server.MessageEnd();
		}
	}

	public override void Move( float interval = 0.1f )
	{
		var globals = Server.Globals;

		if ( RouteIsClear() )
		{
			Util.Alert( AlertType.AiConsole, "Tried to move with no route!\n" );
			TaskFail();
			return;
		}

		if ( MoveWaitFinished > globals.Time )
			return;

		BaseEntity target = null;

		if ( GroundSpeed == 0f )
			GroundSpeed = 100f;

		var moveDistance = GroundSpeed * interval;
		float waypointDistance;
		float checkDistance;

		do
		{
			var direction = Util.Normalize( Route[RouteIndex].Location - Origin );

			waypointDistance = MathLib.Length( Route[RouteIndex].Location - Origin );
			checkDistance = waypointDistance < DistanceToCheck ? waypointDistance : DistanceToCheck;

			switch ( Route[RouteIndex].Type & ~MoveFlag.NotToMask )
			{
				case MoveFlag.ToEnemy:
					target = EnemyEntity;
					break;

				case MoveFlag.ToTargetEntity:
					target = TargetEntity;
					break;
			}

			if ( CheckLocalMove( Origin, Origin + (direction * checkDistance), target, out var distance ) != LocalMove.Valid )
			{
				Stop();

				var blocker = (globals.TraceEnt ?? Server.GetEdict( 0 )) as BaseEntity;

				if ( blocker is not null )
					DispatchBlocked( blocker );

				if ( blocker is not null && MoveWaitTime > 0f && blocker.IsMoving && !blocker.IsPlayer && globals.Time - MoveWaitFinished > 3.0 )
				{
					if ( distance < GroundSpeed )
					{
						MoveWaitFinished = globals.Time + MoveWaitTime;
						return;
					}
				}
				else if ( Triangulate( Origin, Route[RouteIndex].Location, distance, target, out var apex ) )
				{
					InsertWaypoint( apex, MoveFlag.ToDetour );
					RouteSimplify( target );
				}
				else
				{
					Util.Alert( AlertType.AiConsole, "Couldn't Triangulate\n" );
					Stop();

					if ( MoveWaitTime > 0f )
					{
						RefreshRoute();
						MoveWaitFinished = (float)(globals.Time + (MoveWaitTime * 0.5));
					}
					else
					{
						TaskFail();
						Util.Alert( AlertType.AiConsole, "Failed to move!\n" );
					}

					return;
				}
			}

			if ( checkDistance < moveDistance )
			{
				MoveExecute( target, direction, checkDistance / GroundSpeed );

				AdvanceRoute( waypointDistance );
				moveDistance -= checkDistance;
			}
			else
			{
				MoveExecute( target, direction, moveDistance / GroundSpeed );

				if ( ShouldAdvanceRoute( waypointDistance - moveDistance ) )
					AdvanceRoute( waypointDistance );

				moveDistance = 0f;
			}

			if ( MovementIsComplete )
			{
				Stop();
				RouteClear();
			}
		}
		while ( moveDistance > 0f && checkDistance > 0f );

		if ( waypointDistance < 128f )
		{
			RouteSimplify( MovementGoal == MoveGoalType.Enemy ? EnemyEntity : TargetEntity );
			RefreshRoute();

			if ( GroundSpeed > 100f )
				GroundSpeed -= 40f;
		}
		else if ( GroundSpeed < 400f )
		{
			GroundSpeed += 10f;
		}
	}

	public override bool ShouldAdvanceRoute( float waypointDistance ) => waypointDistance <= 32f;

	public override LocalMove CheckLocalMove( Vector3 start, Vector3 end, BaseEntity target, out float distanceMoved )
	{
		var lift = new Vector3( 0f, 0f, 32f );
		var trace = Util.TraceHull( start + lift, end + lift, DontIgnoreMonsters, LargeHull, this );

		distanceMoved = MathLib.Length( trace.EndPos - lift - start );

		if ( trace.StartSolid || trace.Fraction < 1.0 )
			return target is not null && target == Server.Globals.TraceEnt ? LocalMove.Valid : LocalMove.Invalid;

		return LocalMove.Valid;
	}

	public override void MoveExecute( BaseEntity target, Vector3 direction, float interval )
	{
		if ( IdealActivity != MovementActivity )
			IdealActivity = MovementActivity;

		_velocity = (_velocity * 0.8f) + (GroundSpeed * direction * 0.2f);

		Server.MoveToOrigin( this, Origin + _velocity, MathLib.Length( _velocity ) * interval, MoveStrafe );
	}
}

[LinkEntityToClass( "controller_head_ball" )]
public class ControllerHeadBall : BaseMonster
{
	private Vector3 _ideal;
	private BaseEntity _creator;

	private BaseEntity Creator => _creator.IsValid() ? _creator : null;

	public override void Spawn()
	{
		Precache();

		MoveType = MoveType.Fly;
		Solid = Solid.BBox;

		SetModel( "sprites/xspark4.spr" );
		RenderMode = RenderMode.TransAdd;
		RenderColor = new Vector3( 255f, 255f, 255f );
		RenderAmt = 255f;
		Scale = 2f;

		SetSize( Vector3.Zero, Vector3.Zero );
		SetOrigin( Origin );

		SetThink( HuntThink );
		SetTouch( BounceTouch );

		_ideal = Vector3.Zero;

		NextThink = (float)(Server.Globals.Time + 0.1);

		_creator = Owner as BaseEntity ?? World;

		DamageTime = Server.Globals.Time;
	}

	public override void Precache()
	{
		Server.PrecacheModel( "sprites/xspark1.spr" );
		Server.PrecacheSound( "debris/zap4.wav" );
		Server.PrecacheSound( "weapons/electro4.wav" );
	}

	public void HuntThink()
	{
		var globals = Server.Globals;

		NextThink = (float)(globals.Time + 0.1);

		RenderAmt -= 5f;

		Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
		Server.WriteByte( TempEntity.ELight );
		Server.WriteShort( Index );
		Server.WriteCoord( Origin.x );
		Server.WriteCoord( Origin.y );
		Server.WriteCoord( Origin.z );
		Server.WriteCoord( RenderAmt / 16f );
		Server.WriteByte( 255 );
		Server.WriteByte( 255 );
		Server.WriteByte( 255 );
		Server.WriteByte( 2 );
		Server.WriteCoord( 0f );
		Server.MessageEnd();

		if ( globals.Time - DamageTime > 5f || RenderAmt < 64f || EnemyEntity is not { } enemy || Creator is not { } creator
			|| Origin.x < -4096f || Origin.x > 4096f || Origin.y < -4096f || Origin.y > 4096f || Origin.z < -4096f || Origin.z > 4096f )
		{
			SetTouch( null );
			Remove();
			return;
		}

		MoveToTarget( enemy.Center );

		if ( MathLib.Length( enemy.Center - Origin ) >= 64f )
			return;

		var trace = Util.TraceLine( Origin, enemy.Center, DontIgnoreMonsters, this );

		if ( trace.Ent is BaseEntity entity && entity.DamageMode != DamageNo )
		{
			Combat.ClearMultiDamage();
			entity.TraceAttack( creator, HalfLifeDll.SkillData.ControllerDmgZap, Velocity, trace, DamageShock );
			Combat.ApplyMultiDamage( this, creator );
		}

		Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
		Server.WriteByte( TempEntity.BeamEntPoint );
		Server.WriteShort( Index );
		Server.WriteCoord( trace.EndPos.x );
		Server.WriteCoord( trace.EndPos.y );
		Server.WriteCoord( trace.EndPos.z );
		Server.WriteShort( GameModels.Laser );
		Server.WriteByte( 0 );
		Server.WriteByte( 10 );
		Server.WriteByte( 3 );
		Server.WriteByte( 20 );
		Server.WriteByte( 0 );
		Server.WriteByte( 255 );
		Server.WriteByte( 255 );
		Server.WriteByte( 255 );
		Server.WriteByte( 255 );
		Server.WriteByte( 10 );
		Server.MessageEnd();

		Sound.EmitAmbient( this, trace.EndPos, "weapons/electro4.wav", 0.5f, Server.AttenuationNormal, 0, Server.RandomLong( 140, 160 ) );

		SetThink( DieThink );
		NextThink = (float)(globals.Time + 0.3);
	}

	public void DieThink() => Remove();

	public void MoveToTarget( Vector3 target )
	{
		var speed = MathLib.Length( _ideal );

		if ( speed == 0f )
		{
			_ideal = Velocity;
			speed = MathLib.Length( _ideal );
		}

		if ( speed > 400f )
			_ideal = Util.Normalize( _ideal ) * 400f;

		_ideal += Util.Normalize( target - Origin ) * 100f;

		Velocity = _ideal;
	}

	public void BounceTouch( BaseEntity other )
	{
		var direction = Util.Normalize( _ideal );
		var trace = Server.Globals.Trace;
		var n = -Vector3.Dot( trace.PlaneNormal, direction );

		direction = (2f * trace.PlaneNormal * n) + direction;

		_ideal = direction * MathLib.Length( _ideal );
	}
}

[LinkEntityToClass( "controller_energy_ball" )]
public class ControllerZapBall : BaseMonster
{
	private BaseEntity _creator;

	public override void Spawn()
	{
		Precache();

		MoveType = MoveType.Fly;
		Solid = Solid.BBox;

		SetModel( "sprites/xspark4.spr" );
		RenderMode = RenderMode.TransAdd;
		RenderColor = new Vector3( 255f, 255f, 255f );
		RenderAmt = 255f;
		Scale = 0.5f;

		SetSize( Vector3.Zero, Vector3.Zero );
		SetOrigin( Origin );

		SetThink( AnimateThink );
		SetTouch( ExplodeTouch );

		_creator = Owner as BaseEntity ?? World;

		DamageTime = Server.Globals.Time;
		NextThink = (float)(Server.Globals.Time + 0.1);
	}

	public override void Precache() => Server.PrecacheModel( "sprites/xspark4.spr" );

	public void AnimateThink()
	{
		NextThink = (float)(Server.Globals.Time + 0.1);

		Frame = ((int)Frame + 1) % 11;

		if ( Server.Globals.Time - DamageTime > 5f || MathLib.Length( Velocity ) < 10f )
		{
			SetTouch( null );
			Remove();
		}
	}

	public void ExplodeTouch( BaseEntity other )
	{
		if ( other.DamageMode != DamageNo )
		{
			var trace = Server.Globals.Trace;
			var owner = _creator.IsValid() ? _creator : this;

			Combat.ClearMultiDamage();
			other.TraceAttack( owner, HalfLifeDll.SkillData.ControllerDmgBall, Util.Normalize( Velocity ), trace, DamageEnergyBeam );
			Combat.ApplyMultiDamage( owner, owner );

			Sound.EmitAmbient( this, trace.EndPos, "weapons/electro4.wav", 0.3f, Server.AttenuationNormal, 0, Server.RandomLong( 90, 99 ) );
		}

		Remove();
	}
}
