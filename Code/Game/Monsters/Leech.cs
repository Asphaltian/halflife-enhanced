using System;
using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "monster_leech" )]
public class Leech : BaseMonster
{
	private const int AnimEventAttack = 1;
	private const int AnimEventFlop = 2;

	private const float Accelerate = 10f;
	private const float CheckDistance = 45f;
	private const float SwimSpeed = 50f;
	private const float SwimAcceleration = 80f;
	private const float SwimDeceleration = 10f;
	private const float TurnRate = 90f;
	private const float SizeX = 10f;
	private const float FrameTime = 0.1f;

	private static readonly string[] AttackSounds = ["leech/leech_bite1.wav", "leech/leech_bite2.wav", "leech/leech_bite3.wav"];
	private static readonly string[] AlertSounds = ["leech/leech_alert1.wav", "leech/leech_alert2.wav"];

	[Save( FieldType.Float )]
	private float _turning;
	[Save( FieldType.Boolean )]
	private bool _pathBlocked;
	[Save( FieldType.Float )]
	private float _accelerate;
	[Save( FieldType.Float )]
	private float _obstacle;
	[Save( FieldType.Float )]
	private float _top;
	[Save( FieldType.Float )]
	private float _bottom;
	[Save( FieldType.Float )]
	private float _height;
	[Save( FieldType.Time )]
	private float _waterTime;
	[Save( FieldType.Time )]
	private float _sideTime;
	[Save( FieldType.Time )]
	private float _zTime;
	[Save( FieldType.Time )]
	private float _stateTime;
	[Save( FieldType.Time )]
	private float _attackSoundTime;

	public override int BloodColor => DontBleed;

	public override int Classify() => ClassInsect;

	public override void Touch( BaseEntity other )
	{
		if ( !other.IsPlayer || Server.Globals.TraceEnt != this )
			return;

		BaseVelocity = other.Velocity;
		EntityFlags |= EntityFlags.BaseVelocity;
	}

	public override void SetObjectCollisionBox()
	{
		AbsMin = Origin + new Vector3( -8f, -8f, 0f );
		AbsMax = Origin + new Vector3( 8f, 8f, 2f );
	}

	public override void Spawn()
	{
		Precache();

		SetModel( "models/leech.mdl" );
		SetSize( new Vector3( -1f, -1f, 0f ), new Vector3( 1f, 1f, 2f ) );

		Solid = Solid.SlideBox;
		MoveType = MoveType.Fly;
		EntityFlags |= EntityFlags.Swim;
		Health = HalfLifeDll.SkillData.LeechHealth;

		FieldOfView = -0.5f;
		DistLook = 750f;

		MonsterInit();

		SetThink( SwimThink );
		SetUse( null );
		SetTouch( null );

		ViewOffset = Vector3.Zero;

		_turning = 0f;
		_pathBlocked = false;

		SetActivity( Activity.Swim );
		SetState( MonsterState.Idle );

		_stateTime = Server.Globals.Time + Server.RandomFloat( 1f, 5f );
	}

	public override void Activate() => RecalculateWaterlevel();

	public void RecalculateWaterlevel()
	{
		var test = Origin - new Vector3( 0f, 0f, 400f );
		var trace = Util.TraceLine( Origin, test, Missile, this );

		_bottom = trace.Fraction != 1.0 ? trace.EndPos.z + 1f : test.z;
		_top = Util.WaterLevel( Origin, Origin.z, Origin.z + 400f ) - 1f;

		var newBottom = (float)((_bottom * 0.8) + (_top * 0.2));

		_top = (float)((_bottom * 0.2) + (_top * 0.8));
		_bottom = newBottom;
		_height = Server.RandomFloat( _bottom, _top );
		_waterTime = Server.Globals.Time + Server.RandomFloat( 5f, 7f );
	}

	public void SwitchLeechState()
	{
		_stateTime = Server.Globals.Time + Server.RandomFloat( 3f, 6f );

		if ( MonsterState == MonsterState.Combat )
		{
			EnemyEntity = null;
			SetState( MonsterState.Idle );
			_sideTime = 0f;
			return;
		}

		Look( (int)DistLook );

		if ( BestVisibleEnemy() is not { } enemy || enemy.WaterLevel == 0 )
			return;

		EnemyEntity = enemy;
		SetState( MonsterState.Combat );
		_stateTime = Server.Globals.Time + Server.RandomFloat( 18f, 25f );

		AlertSound();
	}

	public override int Relationship( BaseEntity target ) => target.IsPlayer ? RelationshipDislike : base.Relationship( target );

	public void AttackSound()
	{
		if ( Server.Globals.Time <= _attackSoundTime )
			return;

		Sound.EmitDyn( this, SoundChannel.Voice, RandomSound( AttackSounds ), 1f, Server.AttenuationNormal, 0, Server.PitchNormal );

		_attackSoundTime = (float)(Server.Globals.Time + 0.5);
	}

	public override void AlertSound()
	{
		Sound.EmitDyn( this, SoundChannel.Voice, RandomSound( AlertSounds ), 1f, (float)(Server.AttenuationNormal * 0.5), 0, Server.PitchNormal );
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/leech.mdl" );

		PrecacheSounds( AttackSounds );
		PrecacheSounds( AlertSounds );
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		Velocity = inflictor is null ? Vector3.Zero : Util.Normalize( Origin - inflictor.Origin ) * 25f;

		return base.TakeDamage( inflictor, attacker, damage, damageType );
	}

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		switch ( monsterEvent.Event )
		{
			case AnimEventAttack:
				AttackSound();

				if ( EnemyEntity is { } enemy )
				{
					MathLib.AngleVectors( Angles, out var face, out _, out _ );

					face.z = 0f;

					var direction = enemy.Origin - Origin;

					direction.z = 0f;
					direction = Util.Normalize( direction );
					face = Util.Normalize( face );

					if ( Vector3.Dot( direction, face ) > 0.9 )
						enemy.TakeDamage( this, this, HalfLifeDll.SkillData.LeechDmgBite, DamageSlash );
				}

				_stateTime -= 2f;
				break;

			case AnimEventFlop:
				break;

			default:
				base.HandleAnimEvent( monsterEvent );
				break;
		}
	}

	public void MakeVectors() => Server.Globals.MakeVectors( Angles with { x = -Angles.x } );

	public float ObstacleDistance( BaseEntity target )
	{
		var globals = Server.Globals;

		MakeVectors();

		var test = Origin + (globals.Forward * CheckDistance);
		var trace = Util.TraceLine( Origin, test, Missile, this );

		if ( trace.StartSolid )
			Speed = (float)(-SwimSpeed * 0.5);

		if ( trace.Fraction != 1.0 && (target is null || trace.Ent != target || Math.Abs( _height - Origin.z ) > 10f) )
			return trace.Fraction;

		if ( _sideTime < globals.Time )
		{
			test = Origin + (globals.Right * SizeX * 2f) + (globals.Forward * CheckDistance);
			trace = Util.TraceLine( Origin, test, Missile, this );

			if ( trace.Fraction != 1.0 )
				return trace.Fraction;

			test = Origin - (globals.Right * SizeX * 2f) + (globals.Forward * CheckDistance);
			trace = Util.TraceLine( Origin, test, Missile, this );

			if ( trace.Fraction != 1.0 )
				return trace.Fraction;

			_sideTime = globals.Time + Server.RandomFloat( 0.5f, 1f );
		}

		return 1f;
	}

	public void DeadThink()
	{
		if ( SequenceFinished )
		{
			if ( Activity == Activity.DieForward )
			{
				SetThink( null );
				StopAnimation();
				return;
			}

			if ( (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround )
			{
				Solid = Solid.Not;
				SetActivity( Activity.DieForward );
			}
		}

		StudioFrameAdvance();
		NextThink = (float)(Server.Globals.Time + 0.1);

		if ( Velocity.x == 0f && Velocity.y == 0f )
			return;

		var trace = Util.TraceLine( Origin, Origin + (Velocity * 0.5f), Missile, this );

		if ( trace.Fraction != 1.0 )
			Velocity = Velocity with { x = 0f, y = 0f };
	}

	public void UpdateMotion()
	{
		var flapSpeed = (Speed - _accelerate) / Accelerate;

		_accelerate = (float)((_accelerate * 0.8) + (Speed * 0.2));

		if ( flapSpeed < 0f )
			flapSpeed = -flapSpeed;

		flapSpeed = (float)(flapSpeed + 1.0);

		if ( flapSpeed < 0.5 )
			flapSpeed = 0.5f;

		if ( flapSpeed > 1.9 )
			flapSpeed = 1.9f;

		FrameRate = flapSpeed;

		var angularVelocity = AVelocity;

		angularVelocity.y = _pathBlocked ? IdealYaw * _obstacle : IdealYaw;

		if ( angularVelocity.y > 150f )
			IdealActivity = Activity.TurnLeft;
		else if ( angularVelocity.y < -150f )
			IdealActivity = Activity.TurnRight;
		else
			IdealActivity = Activity.Swim;

		var delta = _height - Origin.z;
		var targetPitch = delta < -10f ? -30f : delta > 10f ? 30f : 0f;

		Angles = Angles with { x = Util.Approach( targetPitch, Angles.x, 60f * FrameTime ) };

		angularVelocity.z = (float)-(Angles.z + (angularVelocity.y * 0.25));
		AVelocity = angularVelocity;

		if ( MonsterState == MonsterState.Combat && HasConditions( ConditionCanMeleeAttack1 ) )
			IdealActivity = Activity.MeleeAttack1;

		if ( WaterLevel == 0 )
		{
			MoveType = MoveType.Toss;
			IdealActivity = Activity.Twitch;
			Velocity = Vector3.Zero;

			Angles = Angles with { z = 0f, x = 0f };

			if ( FrameRate < 1.0 )
				FrameRate = 1f;
		}
		else if ( MoveType == MoveType.Toss )
		{
			MoveType = MoveType.Fly;
			EntityFlags &= ~EntityFlags.OnGround;

			RecalculateWaterlevel();

			_waterTime = Server.Globals.Time + 2f;
		}

		if ( Activity != IdealActivity )
			SetActivity( IdealActivity );

		var interval = StudioFrameAdvance();

		DispatchAnimEvents( interval );
	}

	public void SwimThink()
	{
		var globals = Server.Globals;

		if ( Util.IsNullEnt( Server.FindClientInPvs( this ) ) )
		{
			NextThink = globals.Time + Server.RandomFloat( 1f, 1.5f );
			Velocity = Vector3.Zero;
			return;
		}

		NextThink = (float)(globals.Time + 0.1);

		var targetSpeed = SwimSpeed;
		var targetYaw = 0f;
		BaseEntity target = null;

		if ( _waterTime < globals.Time )
			RecalculateWaterlevel();

		if ( _stateTime < globals.Time )
			SwitchLeechState();

		ClearConditions( ConditionCanMeleeAttack1 );

		if ( MonsterState == MonsterState.Combat )
		{
			target = EnemyEntity;

			if ( target is null )
			{
				SwitchLeechState();
			}
			else
			{
				_height = target.Origin.z + target.ViewOffset.z - 5f;

				if ( _height < _bottom )
					_height = _bottom;
				else if ( _height > _top )
					_height = _top;

				var location = target.Origin - Origin;

				location.z += target.ViewOffset.z;

				if ( MathLib.Length( location ) < 40f )
					SetConditions( ConditionCanMeleeAttack1 );

				targetYaw = MathLib.VecToYaw( location );
				targetYaw = Util.AngleDiff( targetYaw, Util.AngleMod( Angles.y ) );

				if ( targetYaw < -TurnRate * 0.75 )
					targetYaw = (float)(-TurnRate * 0.75);
				else if ( targetYaw > TurnRate * 0.75 )
					targetYaw = (float)(TurnRate * 0.75);
				else
					targetSpeed *= 2f;
			}
		}
		else
		{
			if ( _zTime < globals.Time )
			{
				var newHeight = Server.RandomFloat( _bottom, _top );

				_height = (float)((0.5 * _height) + (0.5 * newHeight));
				_zTime = globals.Time + Server.RandomFloat( 1f, 4f );
			}

			if ( Server.RandomLong( 0, 100 ) < 10 )
				targetYaw = Server.RandomLong( -30, 30 );

			if ( MathLib.Length( Origin - OldOrigin ) < 1f )
				_sideTime = 0f;
		}

		_obstacle = ObstacleDistance( target );
		OldOrigin = Origin;

		if ( _obstacle < 0.1 )
			_obstacle = 0.1f;

		if ( _obstacle == 1.0 )
		{
			if ( _turning != 0f )
				_turning = 0f;

			_pathBlocked = false;

			Speed = Util.Approach( targetSpeed, Speed, SwimAcceleration * FrameTime );
			Velocity = globals.Forward * Speed;
		}
		else
		{
			_obstacle = (float)(1.0 / _obstacle);
			_pathBlocked = true;

			if ( _turning == 0f )
			{
				var test = Origin + (globals.Right * SizeX) + (globals.Forward * CheckDistance);
				var rightSide = Util.TraceLine( Origin, test, Missile, this ).Fraction;

				test = Origin + (globals.Right * -SizeX) + (globals.Forward * CheckDistance);

				var leftSide = Util.TraceLine( Origin, test, Missile, this ).Fraction;
				var delta = rightSide - leftSide;

				_turning = delta > 0.1 || (delta > -0.1 && Server.RandomLong( 0, 100 ) < 50) ? -TurnRate : TurnRate;
			}

			Speed = Util.Approach( (float)-(SwimSpeed * 0.5), Speed, SwimDeceleration * FrameTime * _obstacle );
			Velocity = globals.Forward * Speed;
		}

		IdealYaw = _turning + targetYaw;

		UpdateMotion();
	}

	public override void Killed( BaseEntity attacker, int gib )
	{
		if ( Owner is BaseEntity owner )
			owner.DeathNotice( this );

		if ( WaterLevel != 0 )
		{
			Angles = Angles with { z = 0f, x = 0f };
			Origin = Origin with { z = Origin.z + 1f };

			var angularVelocity = Vector3.Zero;

			if ( Server.RandomLong( 0, 99 ) < 70 )
				angularVelocity.y = Server.RandomLong( -720, 720 );

			AVelocity = angularVelocity;

			Gravity = 0.02f;
			EntityFlags &= ~EntityFlags.OnGround;

			SetActivity( Activity.DieSimple );
		}
		else
		{
			SetActivity( Activity.DieForward );
		}

		MoveType = MoveType.Toss;
		DamageMode = DamageNo;

		SetThink( DeadThink );
	}
}
