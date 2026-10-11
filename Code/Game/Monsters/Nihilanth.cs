using System;
using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "monster_nihilanth" )]
public class Nihilanth : BaseMonster
{
	public const int NihilanthScale = 15;

	private const int SphereCount = 20;

	private static readonly string[] AttackSounds = ["X/x_attack1.wav", "X/x_attack2.wav", "X/x_attack3.wav"];
	private static readonly string[] BallSounds = ["X/x_ballattack1.wav"];
	private static readonly string[] ShootSounds = ["X/x_shoot1.wav"];
	private static readonly string[] RechargeSounds = ["X/x_recharge1.wav", "X/x_recharge2.wav", "X/x_recharge3.wav"];
	private static readonly string[] LaughSounds = ["X/x_laugh1.wav", "X/x_laugh2.wav"];
	private static readonly string[] PainSounds = ["X/x_pain1.wav", "X/x_pain2.wav"];
	private static readonly string[] DeathSounds = ["X/x_die1.wav"];

	[Save( FieldType.Float )]
	private float _force;

	[Save( FieldType.Time )]
	private float _nextPainSound;

	[Save( FieldType.Vector )]
	private Vector3 _velocity;
	[Save( FieldType.Vector )]
	private Vector3 _angularVelocity;

	[Save( FieldType.Vector )]
	private Vector3 _targetDirection;
	[Save( FieldType.PositionVector )]
	private Vector3 _targetPosition;

	[Save( FieldType.Vector )]
	private Vector3 _desiredDirection;
	[Save( FieldType.PositionVector )]
	private Vector3 _desiredPosition;

	[Save( FieldType.Float )]
	private float _minZ;
	[Save( FieldType.Float )]
	private float _maxZ;

	[Save( FieldType.Time )]
	private float _lastSeen;
	[Save( FieldType.Time )]
	private float _previousSeen;

	[Save( FieldType.Integer )]
	private int _irritation;

	[Save( FieldType.Integer )]
	private int _level;
	[Save( FieldType.Integer )]
	private int _teleport;

	[Save( FieldType.EHandle )]
	private BaseEntity _recharger;

	[Save( FieldType.EHandle )]
	private readonly BaseEntity[] _spheres = new BaseEntity[SphereCount];
	[Save( FieldType.Integer )]
	private int _activeSpheres;

	[Save( FieldType.Float )]
	private float _adjust;

	[Save( FieldType.ClassPtr )]
	private Sprite _ball;

	[Save( FieldType.Character )]
	private string _rechargerTarget;
	[Save( FieldType.Character )]
	private string _drawUse;
	[Save( FieldType.Character )]
	private string _teleportUse;
	[Save( FieldType.Character )]
	private string _teleportTouch;
	[Save( FieldType.Character )]
	private string _deadUse;
	[Save( FieldType.Character )]
	private string _deadTouch;

	[Save( FieldType.Time )]
	private float _shootEnd;
	[Save( FieldType.Time )]
	private float _shootTime;

	[Save( FieldType.EHandle )]
	private readonly BaseEntity[] _friends = new BaseEntity[3];

	public override int BloodColor => BloodColorYellow;

	public override int Classify() => ClassAlienMilitary;

	private BaseEntity Recharger => _recharger.IsValid() ? _recharger : null;

	public override void SetObjectCollisionBox()
	{
		AbsMin = Origin + new Vector3( -16 * NihilanthScale, -16 * NihilanthScale, -48 * NihilanthScale );
		AbsMax = Origin + new Vector3( 16 * NihilanthScale, 16 * NihilanthScale, 28 * NihilanthScale );
	}

	public override void Spawn()
	{
		Precache();

		MoveType = MoveType.Fly;
		Solid = Solid.BBox;

		SetModel( "models/nihilanth.mdl" );
		SetSize( new Vector3( -32f, -32f, 0f ), new Vector3( 32f, 32f, 64f ) );
		SetOrigin( Origin );

		EntityFlags |= EntityFlags.Monster;
		DamageMode = DamageAim;
		Health = HalfLifeDll.SkillData.NihilanthHealth;
		ViewOffset = new Vector3( 0f, 0f, 300f );

		FieldOfView = -1f;

		Sequence = 0;
		ResetSequenceInfo();

		InitBoneControllers();

		SetThink( StartupThink );
		NextThink = (float)(Server.Globals.Time + 0.1);

		_desiredDirection = new Vector3( 1f, 0f, 0f );
		_desiredPosition = new Vector3( Origin.x, Origin.y, 512f );

		_level = 1;
		_teleport = 1;

		if ( string.IsNullOrEmpty( _rechargerTarget ) )
			_rechargerTarget = "n_recharger";

		if ( string.IsNullOrEmpty( _drawUse ) )
			_drawUse = "n_draw";

		if ( string.IsNullOrEmpty( _teleportUse ) )
			_teleportUse = "n_leaving";

		if ( string.IsNullOrEmpty( _teleportTouch ) )
			_teleportTouch = "n_teleport";

		if ( string.IsNullOrEmpty( _deadUse ) )
			_deadUse = "n_dead";

		if ( string.IsNullOrEmpty( _deadTouch ) )
			_deadTouch = "n_ending";
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/nihilanth.mdl" );
		Server.PrecacheModel( "sprites/lgtning.spr" );
		Util.PrecacheOther( "nihilanth_energy_ball" );
		Util.PrecacheOther( "monster_alien_controller" );
		Util.PrecacheOther( "monster_alien_slave" );

		PrecacheSounds( AttackSounds );
		PrecacheSounds( BallSounds );
		PrecacheSounds( ShootSounds );
		PrecacheSounds( RechargeSounds );
		PrecacheSounds( LaughSounds );
		PrecacheSounds( PainSounds );
		PrecacheSounds( DeathSounds );
		Server.PrecacheSound( "debris/beamstart7.wav" );
	}

	public override void PainSound()
	{
		if ( _nextPainSound > Server.Globals.Time )
			return;

		_nextPainSound = Server.Globals.Time + Server.RandomFloat( 2f, 5f );

		if ( Health > HalfLifeDll.SkillData.NihilanthHealth / 2 )
			Sound.Emit( this, SoundChannel.Voice, RandomSound( LaughSounds ), 1f, 0.2f );
		else if ( _irritation >= 2 )
			Sound.Emit( this, SoundChannel.Voice, RandomSound( PainSounds ), 1f, 0.2f );
	}

	public override void DeathSound()
	{
		Sound.Emit( this, SoundChannel.Voice, RandomSound( DeathSounds ), 1f, 0.1f );
	}

	public void StartupThink()
	{
		_irritation = 0;
		_adjust = 512f;

		_minZ = Util.FindEntityByTargetname( null, "n_min" ) is { } min ? min.Origin.z : -4096f;
		_maxZ = Util.FindEntityByTargetname( null, "n_max" ) is { } max ? max.Origin.z : 4096f;

		_recharger = this;

		for ( var i = 0; i < SphereCount; i++ )
			EmitSphere();

		_recharger = null;

		SetThink( HuntThink );
		SetUse( CommandUse );
		NextThink = (float)(Server.Globals.Time + 0.1);
	}

	public void DyingThink()
	{
		var globals = Server.Globals;

		NextThink = (float)(globals.Time + 0.1);
		DispatchAnimEvents();
		StudioFrameAdvance();

		if ( DeadFlag == DeadNo )
		{
			DeathSound();
			DeadFlag = DeadDying;

			_desiredPosition.z = _maxZ;
		}

		if ( DeadFlag == DeadDying )
		{
			Flight();

			if ( MathF.Abs( Origin.z - _maxZ ) < 16f )
			{
				Velocity = Vector3.Zero;
				FireTargets( _deadUse, this, this, UseType.On, 1f );
				DeadFlag = DeadDead;
			}
		}

		if ( SequenceFinished )
		{
			var yaw = AVelocity.y + Server.RandomFloat( -100f, 100f );

			if ( yaw < -100f )
				yaw = -100f;

			if ( yaw > 100f )
				yaw = 100f;

			AVelocity = AVelocity with { y = yaw };

			Sequence = LookupSequence( "die1" );
		}

		if ( _ball is not null )
		{
			if ( _ball.RenderAmt > 0f )
			{
				_ball.RenderAmt = Math.Max( 0f, _ball.RenderAmt - 2f );
			}
			else
			{
				_ball.Remove();
				_ball = null;
			}
		}

		Util.MakeAimVectors( Angles, globals );

		var attachment = Server.RandomLong( 1, 4 );
		Vector3 direction;

		do
		{
			direction = new Vector3( Server.RandomFloat( -1f, 1f ), Server.RandomFloat( -1f, 1f ), Server.RandomFloat( -1f, 1f ) );
		}
		while ( Vector3.Dot( direction, direction ) > 1.0 );

		switch ( Server.RandomLong( 1, 4 ) )
		{
			case 1:
				direction.z = (float)(Math.Abs( direction.z ) * 0.5);
				direction += 2f * globals.Up;
				break;

			case 2:
				if ( Vector3.Dot( direction, globals.Forward ) < 0f )
					direction *= -1f;

				direction += 2f * globals.Forward;
				break;

			case 3:
				if ( Vector3.Dot( direction, globals.Right ) > 0f )
					direction *= -1f;

				direction -= 2f * globals.Right;
				break;

			case 4:
				if ( Vector3.Dot( direction, globals.Right ) < 0f )
					direction *= -1f;

				direction += 2f * globals.Right;
				break;
		}

		var source = Server.GetAttachment( this, attachment - 1 );
		var trace = Util.TraceLine( source, source + (direction * 4096f), IgnoreMonsters, this );

		Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
		Server.WriteByte( TempEntity.BeamEntPoint );
		Server.WriteShort( Index + (0x1000 * attachment) );
		Server.WriteCoord( trace.EndPos.x );
		Server.WriteCoord( trace.EndPos.y );
		Server.WriteCoord( trace.EndPos.z );
		Server.WriteShort( GameModels.Laser );
		Server.WriteByte( 0 );
		Server.WriteByte( 10 );
		Server.WriteByte( 5 );
		Server.WriteByte( 100 );
		Server.WriteByte( 120 );
		Server.WriteByte( 64 );
		Server.WriteByte( 128 );
		Server.WriteByte( 255 );
		Server.WriteByte( 255 );
		Server.WriteByte( 10 );
		Server.MessageEnd();

		source = Server.GetAttachment( this, 0 );

		var ball = (NihilanthHvr)Create( "nihilanth_energy_ball", source, Angles, this );

		ball.Velocity = new Vector3( Server.RandomFloat( -0.7f, 0.7f ), Server.RandomFloat( -0.7f, 0.7f ), 1f ) * 600f;
		ball.GreenBallInit();
	}

	public override void GibMonster()
	{
	}

	private void FloatSequence()
	{
		if ( _irritation >= 2 )
			Sequence = LookupSequence( "float_open" );
		else if ( _angularVelocity.y > 30f )
			Sequence = LookupSequence( "walk_r" );
		else if ( _angularVelocity.y < -30f )
			Sequence = LookupSequence( "walk_l" );
		else if ( _velocity.z > 30f )
			Sequence = LookupSequence( "walk_u" );
		else if ( _velocity.z < -30f )
			Sequence = LookupSequence( "walk_d" );
		else
			Sequence = LookupSequence( "float" );
	}

	private void ShootBalls()
	{
		if ( _shootEnd <= Server.Globals.Time )
			return;

		while ( _shootTime < _shootEnd && _shootTime < Server.Globals.Time )
		{
			if ( EnemyEntity is { } enemy )
			{
				ShootBall( 2, enemy );
				ShootBall( 3, enemy );
			}

			_shootTime = (float)(_shootTime + 0.2);
		}
	}

	private void ShootBall( int attachment, BaseEntity enemy )
	{
		var hand = Server.GetAttachment( this, attachment );
		var source = hand + (Velocity * (_shootTime - Server.Globals.Time));
		var direction = Util.Normalize( _targetPosition - Origin );

		source += direction * (Server.Globals.Time - _shootTime);

		var ball = (NihilanthHvr)Create( "nihilanth_energy_ball", source, Angles, this );

		ball.Velocity = direction * 200f;
		ball.ZapInit( enemy );
	}

	public void MakeFriend( Vector3 start )
	{
		for ( var i = 0; i < 3; i++ )
		{
			if ( _friends[i].IsValid() && !_friends[i].IsAlive )
			{
				if ( RenderMode == RenderMode.Normal )
					((BaseMonster)_friends[i]).FadeMonster();

				_friends[i] = null;
			}

			if ( _friends[i].IsValid() )
				continue;

			var graph = HalfLifeDll.WorldGraph;

			if ( Server.RandomLong( 0, 1 ) == 0 )
			{
				var node = graph.FindNearestNode( start, GraphNode.Air );

				if ( node != Graph.NoNode )
				{
					var origin = graph.Nodes[node].Origin;
					var trace = Util.TraceHull( origin + new Vector3( 0f, 0f, 32f ), origin + new Vector3( 0f, 0f, 32f ), DontIgnoreMonsters, LargeHull, null );

					if ( !trace.StartSolid )
						_friends[i] = Create( "monster_alien_controller", origin, Angles, null );
				}
			}
			else
			{
				var node = graph.FindNearestNode( start, GraphNode.Land | GraphNode.Water );

				if ( node != Graph.NoNode )
				{
					var origin = graph.Nodes[node].Origin;
					var trace = Util.TraceHull( origin + new Vector3( 0f, 0f, 36f ), origin + new Vector3( 0f, 0f, 36f ), DontIgnoreMonsters, HumanHull, null );

					if ( !trace.StartSolid )
						_friends[i] = Create( "monster_alien_slave", origin, Angles, null );
				}
			}

			if ( _friends[i].IsValid() )
				Sound.Emit( _friends[i], SoundChannel.Weapon, "debris/beamstart7.wav", 1f, Server.AttenuationNormal );

			return;
		}
	}

	private void NextActivity()
	{
		var globals = Server.Globals;
		var skill = HalfLifeDll.SkillData;

		Util.MakeAimVectors( Angles, globals );

		if ( _irritation >= 2 )
		{
			if ( _ball is null )
			{
				_ball = Sprite.SpriteCreate( Server, "sprites/tele1.spr", Origin, true );
				_ball.SetTransparency( RenderMode.TransAdd, 255, 255, 255, 255, RenderFx.NoDissipation );
				_ball.SetAttachment( this, 1 );
				_ball.SetScale( 4f );
				_ball.FrameRate = 10f;
				_ball.TurnOn();
			}

			Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
			Server.WriteByte( TempEntity.ELight );
			Server.WriteShort( Index + 0x1000 );
			Server.WriteCoord( Origin.x );
			Server.WriteCoord( Origin.y );
			Server.WriteCoord( Origin.z );
			Server.WriteCoord( 256f );
			Server.WriteByte( 255 );
			Server.WriteByte( 192 );
			Server.WriteByte( 64 );
			Server.WriteByte( 200 );
			Server.WriteCoord( 0f );
			Server.MessageEnd();
		}

		if ( (Health < skill.NihilanthHealth / 2 || _activeSpheres < SphereCount / 2) && Recharger is null && _level <= 9 )
		{
			var name = $"{_rechargerTarget}{_level}";

			BaseEntity entity = null;
			BaseEntity recharger = null;
			var nearest = 8192f;

			while ( (entity = Util.FindEntityByTargetname( entity, name )) is not null )
			{
				var local = MathLib.Length( entity.Origin - Origin );

				if ( local >= nearest )
					continue;

				nearest = local;
				recharger = entity;
			}

			if ( recharger is not null )
			{
				_recharger = recharger;
				_desiredPosition = new Vector3( Origin.x, Origin.y, recharger.Origin.z );
				_desiredDirection = Util.Normalize( recharger.Origin - _desiredPosition );
				_desiredDirection.z = 0f;
				_desiredDirection = Util.Normalize( _desiredDirection );
			}
			else
			{
				_recharger = null;
				Util.Alert( AlertType.AiConsole, $"nihilanth can't find {name}" );
				_level++;

				if ( _level > 9 )
					_irritation = 2;
			}
		}

		var distance = MathLib.Length( _desiredPosition - Origin );
		var dot = Vector3.Dot( _desiredDirection, globals.Forward );

		if ( Recharger is not null )
		{
			if ( distance < 128.0 )
			{
				if ( LookupSequence( "recharge" ) != Sequence )
				{
					var text = $"{_drawUse}{_level}";

					FireTargets( text, this, this, UseType.On, 1f );

					Util.Alert( AlertType.Console, $"fireing {text}" );
				}

				Sequence = LookupSequence( "recharge" );
			}
			else
			{
				FloatSequence();
			}

			return;
		}

		if ( EnemyEntity is { IsAlive: false } )
			EnemyEntity = null;

		if ( _lastSeen + 15f < globals.Time )
			EnemyEntity = null;

		if ( EnemyEntity is null )
		{
			Look( 4096 );
			EnemyEntity = BestVisibleEnemy();
		}

		if ( EnemyEntity is not null && _irritation != 0 && _lastSeen + 5f > globals.Time && distance < 256f && dot > 0f )
		{
			if ( _irritation >= 2 && Health < skill.NihilanthHealth / 2.0 )
			{
				Sequence = LookupSequence( "attack1_open" );
			}
			else if ( Server.RandomLong( 0, 1 ) == 0 )
			{
				Sequence = LookupSequence( "attack1" );
			}
			else
			{
				var touch = Util.FindEntityByTargetname( null, $"{_teleportTouch}{_teleport}" );
				var trigger = Util.FindEntityByTargetname( null, $"{_teleportUse}{_teleport}" );

				if ( trigger is not null || touch is not null )
				{
					Sequence = LookupSequence( "attack2" );
				}
				else
				{
					_teleport++;
					Sequence = LookupSequence( "attack1" );
				}
			}

			return;
		}

		FloatSequence();
	}

	public void HuntThink()
	{
		var globals = Server.Globals;
		var skill = HalfLifeDll.SkillData;

		NextThink = (float)(globals.Time + 0.1);
		DispatchAnimEvents();
		StudioFrameAdvance();

		ShootBalls();

		if ( Health <= 0f )
		{
			SetThink( DyingThink );
			SequenceFinished = true;
			return;
		}

		if ( Health < skill.NihilanthHealth && AbsorbSphere() )
			Health += skill.NihilanthHealth / SphereCount;

		if ( SequenceFinished )
		{
			Frame = 0f;
			NextActivity();
			ResetSequenceInfo();
			FrameRate = (float)(2.0 - (1.0 * (Health / skill.NihilanthHealth)));
		}

		if ( EnemyEntity is { } enemy && Recharger is null )
		{
			if ( IsVisible( enemy ) )
			{
				if ( _lastSeen < globals.Time - 5f )
					_previousSeen = globals.Time;

				_lastSeen = globals.Time;
				_targetPosition = enemy.Origin;
				_targetDirection = Util.Normalize( _targetPosition - Origin );
				_desiredDirection = _targetDirection;
				_desiredPosition = new Vector3( Origin.x, Origin.y, _targetPosition.z + _adjust );
			}
			else
			{
				_adjust = Math.Min( _adjust + 10f, 1000f );
			}
		}

		if ( _desiredPosition.z > _maxZ )
			_desiredPosition.z = _maxZ;

		if ( _desiredPosition.z < _minZ )
			_desiredPosition.z = _minZ;

		Flight();
	}

	private void Flight()
	{
		var globals = Server.Globals;

		Util.MakeAimVectors( Angles + _angularVelocity, globals );

		var side = Vector3.Dot( _desiredDirection, globals.Right );

		if ( side < 0f )
		{
			if ( _angularVelocity.y < 180f )
				_angularVelocity.y += 6f;
		}
		else
		{
			if ( _angularVelocity.y > -180f )
				_angularVelocity.y -= 6f;
		}

		_angularVelocity.y = (float)(_angularVelocity.y * 0.98);

		var estimate = Origin + (_velocity * 2f) + (globals.Up * _force * 20f);

		Util.MakeAimVectors( Angles, globals );
		_velocity.x += globals.Up.x * _force;
		_velocity.y += globals.Up.y * _force;
		_velocity.z += globals.Up.z * _force;

		_velocity.x = (float)(_velocity.x * (1.0 - (Math.Abs( globals.Right.x ) * 0.05)));
		_velocity.y = (float)(_velocity.y * (1.0 - (Math.Abs( globals.Right.y ) * 0.05)));
		_velocity.z = (float)(_velocity.z * (1.0 - (Math.Abs( globals.Right.z ) * 0.05)));

		_velocity *= 0.995f;

		if ( _force < 100f && estimate.z < _desiredPosition.z )
			_force += 10f;
		else if ( _force > -100f && estimate.z > _desiredPosition.z )
			_force -= 10f;

		SetOrigin( Origin + (_velocity * 0.1f) );
		Angles += _angularVelocity * 0.1f;
	}

	private bool AbsorbSphere()
	{
		for ( var i = 0; i < SphereCount; i++ )
		{
			if ( !_spheres[i].IsValid() )
				continue;

			((NihilanthHvr)_spheres[i]).AbsorbInit();
			_spheres[i] = null;
			_activeSpheres--;

			return true;
		}

		return false;
	}

	private bool EmitSphere()
	{
		_activeSpheres = 0;

		var empty = 0;

		for ( var i = 0; i < SphereCount; i++ )
		{
			if ( _spheres[i].IsValid() )
				_activeSpheres++;
			else
				empty = i;
		}

		if ( _activeSpheres >= SphereCount )
			return false;

		var source = Recharger.Origin;
		var sphere = (NihilanthHvr)Create( "nihilanth_energy_ball", source, Angles, this );

		sphere.Velocity = Origin - source;
		sphere.CircleInit( this );

		_spheres[empty] = sphere;

		return true;
	}

	private void HandLights()
	{
		for ( var attachment = 0x3000; attachment <= 0x4000; attachment += 0x1000 )
		{
			Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
			Server.WriteByte( TempEntity.ELight );
			Server.WriteShort( Index + attachment );
			Server.WriteCoord( Origin.x );
			Server.WriteCoord( Origin.y );
			Server.WriteCoord( Origin.z );
			Server.WriteCoord( 256f );
			Server.WriteByte( 128 );
			Server.WriteByte( 128 );
			Server.WriteByte( 255 );
			Server.WriteByte( 10 );
			Server.WriteCoord( 128f );
			Server.MessageEnd();
		}
	}

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		switch ( monsterEvent.Event )
		{
			case 2:
				if ( EnemyEntity is null )
					break;

				if ( Server.RandomLong( 0, 4 ) == 0 )
					Sound.Emit( this, SoundChannel.Voice, RandomSound( AttackSounds ), 1f, 0.2f );

				Sound.Emit( this, SoundChannel.Weapon, RandomSound( BallSounds ), 1f, 0.2f );

				HandLights();

				_shootTime = Server.Globals.Time;
				_shootEnd = (float)(Server.Globals.Time + 1.0);
				break;

			case 3:
				{
					if ( EnemyEntity is not { } enemy )
						break;

					var touch = Util.FindEntityByTargetname( null, $"{_teleportTouch}{_teleport}" );
					var triggerName = $"{_teleportUse}{_teleport}";
					var trigger = Util.FindEntityByTargetname( null, triggerName );

					if ( trigger is not null || touch is not null )
					{
						Sound.Emit( this, SoundChannel.Voice, RandomSound( AttackSounds ), 1f, 0.2f );

						var source = Server.GetAttachment( this, 2 );
						var ball = (NihilanthHvr)Create( "nihilanth_energy_ball", source, Angles, this );

						ball.Velocity = Origin - source;
						ball.TeleportInit( this, enemy, trigger, touch );
					}
					else
					{
						_teleport++;

						Sound.Emit( this, SoundChannel.Weapon, RandomSound( BallSounds ), 1f, 0.2f );

						Util.Alert( AlertType.AiConsole, $"nihilanth can't target {triggerName}" );

						HandLights();

						_shootTime = Server.Globals.Time;
						_shootEnd = (float)(Server.Globals.Time + 1.0);
					}

					break;
				}

			case 4:
				if ( Recharger is not null && !EmitSphere() )
					_recharger = null;

				break;

			case 5:
				Sound.Emit( this, SoundChannel.Voice, RandomSound( RechargeSounds ), 1f, 0.2f );
				break;

			case 6:
				{
					if ( EnemyEntity is not { } enemy )
						break;

					var source = Server.GetAttachment( this, 2 );
					var ball = (NihilanthHvr)Create( "nihilanth_energy_ball", source, Angles, this );

					ball.Velocity = Origin - source;
					ball.ZapInit( enemy );
					break;
				}
		}
	}

	public void CommandUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		switch ( useType )
		{
			case UseType.Off:
				if ( Util.FindEntityByTargetname( null, _deadTouch ) is not { } touch )
					break;

				if ( EnemyEntity is { } enemy )
					touch.Touch( enemy );
				else if ( Util.FindEntityByClassname( null, "player" ) is { IsAlive: true } player )
					touch.Touch( player );

				break;

			case UseType.On:
				if ( _irritation == 0 )
					_irritation = 1;

				break;
		}
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		if ( inflictor.Owner == this )
			return false;

		if ( damage >= Health )
		{
			Health = 1f;

			if ( _irritation != 3 )
				return false;
		}

		PainSound();

		Health -= damage;

		return false;
	}

	public override void TraceAttack( BaseEntity attacker, float damage, Vector3 direction, in Trace trace, int damageType )
	{
		if ( _irritation == 3 )
			_irritation = 2;

		if ( _irritation == 2 && trace.HitGroup == 2 && damage > 2f )
			_irritation = 3;

		if ( _irritation != 3 )
		{
			var blood = Util.Normalize( trace.EndPos - Origin );

			Util.BloodStream( trace.EndPos, blood, BloodColor, (int)(damage + (100 - (100 * (Health / HalfLifeDll.SkillData.NihilanthHealth)))) );
		}

		Combat.AddMultiDamage( attacker, this, damage, damageType );
	}
}

[LinkEntityToClass( "nihilanth_energy_ball" )]
public class NihilanthHvr : BaseMonster
{
	[Save( FieldType.Vector )]
	private Vector3 _ideal;
	[Save( FieldType.ClassPtr )]
	private Nihilanth _nihilanth;
	[Save( FieldType.EHandle )]
	private BaseEntity _touch;
	[Save( FieldType.Integer )]
	private int _frames;

	public override void Spawn()
	{
		Precache();

		RenderMode = RenderMode.TransAdd;
		RenderAmt = 255f;
		Scale = 3f;
	}

	public override void Precache()
	{
		Server.PrecacheModel( "sprites/flare6.spr" );
		Server.PrecacheModel( "sprites/nhth1.spr" );
		Server.PrecacheModel( "sprites/exit1.spr" );
		Server.PrecacheModel( "sprites/tele1.spr" );
		Server.PrecacheModel( "sprites/animglow01.spr" );
		Server.PrecacheModel( "sprites/xspark4.spr" );
		Server.PrecacheModel( "sprites/muzzleflash3.spr" );
		Server.PrecacheSound( "debris/zap4.wav" );
		Server.PrecacheSound( "weapons/electro4.wav" );
		Server.PrecacheSound( "x/x_teleattack1.wav" );
	}

	public void CircleInit( BaseEntity target )
	{
		MoveType = MoveType.NoClip;
		Solid = Solid.Not;

		SetModel( "sprites/muzzleflash3.spr" );
		RenderColor = new Vector3( 255f, 224f, 192f );
		Scale = 2f;
		_frames = 1;
		RenderAmt = 255f;

		SetSize( Vector3.Zero, Vector3.Zero );
		SetOrigin( Origin );

		SetThink( HoverThink );
		SetTouch( BounceTouch );
		NextThink = (float)(Server.Globals.Time + 0.1);

		TargetEntity = target;
	}

	public void HoverThink()
	{
		NextThink = (float)(Server.Globals.Time + 0.1);

		if ( TargetEntity is { } target )
			CircleTarget( target.Origin + new Vector3( 0f, 0f, 16 * Nihilanth.NihilanthScale ) );
		else
			Remove();

		Server.RandomLong( 0, 99 );

		Frame = ((int)Frame + 1) % _frames;
	}

	public void ZapInit( BaseEntity enemy )
	{
		MoveType = MoveType.Fly;
		Solid = Solid.BBox;

		SetModel( "sprites/nhth1.spr" );

		RenderColor = new Vector3( 255f, 255f, 255f );
		Scale = 2f;

		Velocity = Util.Normalize( enemy.Origin - Origin ) * 200f;

		EnemyEntity = enemy;
		SetThink( ZapThink );
		SetTouch( ZapTouch );
		NextThink = (float)(Server.Globals.Time + 0.1);

		Sound.EmitDyn( this, SoundChannel.Weapon, "debris/zap4.wav", 1f, Server.AttenuationNormal, 0, 100 );
	}

	public void ZapThink()
	{
		NextThink = (float)(Server.Globals.Time + 0.05);

		if ( EnemyEntity is not { } enemy || Origin.x < -4096f || Origin.x > 4096f || Origin.y < -4096f || Origin.y > 4096f || Origin.z < -4096f || Origin.z > 4096f )
		{
			SetTouch( null );
			Remove();
			return;
		}

		if ( MathLib.Length( Velocity ) < 2000f )
			Velocity *= 1.2f;

		if ( MathLib.Length( enemy.Center - Origin ) < 256f )
		{
			var trace = Util.TraceLine( Origin, enemy.Center, DontIgnoreMonsters, this );

			if ( trace.Ent is BaseEntity hit && hit.DamageMode != DamageNo )
			{
				Combat.ClearMultiDamage();
				hit.TraceAttack( this, HalfLifeDll.SkillData.NihilanthZap, Velocity, trace, DamageShock );
				Combat.ApplyMultiDamage( this, this );
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
			Server.WriteByte( 20 );
			Server.WriteByte( 64 );
			Server.WriteByte( 196 );
			Server.WriteByte( 255 );
			Server.WriteByte( 255 );
			Server.WriteByte( 10 );
			Server.MessageEnd();

			Sound.EmitAmbient( this, trace.EndPos, "weapons/electro4.wav", 0.5f, Server.AttenuationNormal, 0, Server.RandomLong( 140, 160 ) );

			SetTouch( null );
			Remove();
			NextThink = (float)(Server.Globals.Time + 0.2);
			return;
		}

		Frame = (int)(Frame + 1f) % 11;

		Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
		Server.WriteByte( TempEntity.ELight );
		Server.WriteShort( Index );
		Server.WriteCoord( Origin.x );
		Server.WriteCoord( Origin.y );
		Server.WriteCoord( Origin.z );
		Server.WriteCoord( 128f );
		Server.WriteByte( 128 );
		Server.WriteByte( 128 );
		Server.WriteByte( 255 );
		Server.WriteByte( 10 );
		Server.WriteCoord( 128f );
		Server.MessageEnd();
	}

	public void ZapTouch( BaseEntity other )
	{
		Sound.EmitAmbient( this, Origin, "weapons/electro4.wav", 1f, Server.AttenuationNormal, 0, Server.RandomLong( 90, 95 ) );

		RadiusDamage( this, this, 50f, ClassNone, DamageShock );
		Velocity *= 0f;

		SetTouch( null );
		Remove();
		NextThink = (float)(Server.Globals.Time + 0.2);
	}

	public void TeleportInit( Nihilanth owner, BaseEntity enemy, BaseEntity target, BaseEntity touch )
	{
		MoveType = MoveType.Fly;
		Solid = Solid.BBox;

		RenderColor = new Vector3( 255f, 255f, 255f );
		Velocity = Velocity with { z = (float)(Velocity.z * 0.2) };

		SetModel( "sprites/exit1.spr" );

		_nihilanth = owner;
		EnemyEntity = enemy;
		TargetEntity = target;
		_touch = touch;

		SetThink( TeleportThink );
		SetTouch( TeleportTouch );
		NextThink = (float)(Server.Globals.Time + 0.1);

		Sound.EmitDyn( this, SoundChannel.Weapon, "x/x_teleattack1.wav", 1f, 0.2f, 0, 100 );
	}

	public void GreenBallInit()
	{
		MoveType = MoveType.Fly;
		Solid = Solid.BBox;

		RenderColor = new Vector3( 255f, 255f, 255f );
		Scale = 1f;

		SetModel( "sprites/exit1.spr" );

		SetTouch( RemoveTouch );
	}

	public void TeleportThink()
	{
		NextThink = (float)(Server.Globals.Time + 0.1);

		if ( EnemyEntity is not { IsAlive: true } enemy || Origin.x < -4096f || Origin.x > 4096f || Origin.y < -4096f || Origin.y > 4096f || Origin.z < -4096f || Origin.z > 4096f )
		{
			Sound.Stop( this, SoundChannel.Weapon, "x/x_teleattack1.wav" );
			Remove();
			return;
		}

		if ( MathLib.Length( enemy.Center - Origin ) < 128f )
		{
			Sound.Stop( this, SoundChannel.Weapon, "x/x_teleattack1.wav" );
			Remove();

			TargetEntity?.Use( enemy, enemy, UseType.On, 1f );

			if ( _touch.IsValid() && EnemyEntity is { } touched )
				_touch.Touch( touched );
		}
		else
		{
			MoveToTarget( enemy.Center );
		}

		Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
		Server.WriteByte( TempEntity.ELight );
		Server.WriteShort( Index );
		Server.WriteCoord( Origin.x );
		Server.WriteCoord( Origin.y );
		Server.WriteCoord( Origin.z );
		Server.WriteCoord( 256f );
		Server.WriteByte( 0 );
		Server.WriteByte( 255 );
		Server.WriteByte( 0 );
		Server.WriteByte( 10 );
		Server.WriteCoord( 256f );
		Server.MessageEnd();

		Frame = (int)(Frame + 1f) % 20;
	}

	public void AbsorbInit()
	{
		SetThink( DissipateThink );
		RenderAmt = 255f;

		Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
		Server.WriteByte( TempEntity.BeamEnts );
		Server.WriteShort( Index );
		Server.WriteShort( TargetEntity.Index + 0x1000 );
		Server.WriteShort( GameModels.Laser );
		Server.WriteByte( 0 );
		Server.WriteByte( 0 );
		Server.WriteByte( 50 );
		Server.WriteByte( 80 );
		Server.WriteByte( 80 );
		Server.WriteByte( 255 );
		Server.WriteByte( 128 );
		Server.WriteByte( 64 );
		Server.WriteByte( 255 );
		Server.WriteByte( 30 );
		Server.MessageEnd();
	}

	public void TeleportTouch( BaseEntity other )
	{
		var enemy = EnemyEntity;

		if ( other == enemy )
		{
			TargetEntity?.Use( enemy, enemy, UseType.On, 1f );

			if ( _touch.IsValid() && enemy is not null )
				_touch.Touch( enemy );
		}
		else
		{
			_nihilanth.MakeFriend( Origin );
		}

		SetTouch( null );
		Sound.Stop( this, SoundChannel.Weapon, "x/x_teleattack1.wav" );
		Remove();
	}

	public void DissipateThink()
	{
		NextThink = (float)(Server.Globals.Time + 0.1);

		if ( Scale > 5.0 )
			Remove();

		RenderAmt -= 2f;
		Scale = (float)(Scale + 0.1);

		if ( TargetEntity is { } target )
			CircleTarget( target.Origin + new Vector3( 0f, 0f, 4096f ) );
		else
			Remove();

		Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
		Server.WriteByte( TempEntity.ELight );
		Server.WriteShort( Index );
		Server.WriteCoord( Origin.x );
		Server.WriteCoord( Origin.y );
		Server.WriteCoord( Origin.z );
		Server.WriteCoord( RenderAmt );
		Server.WriteByte( 255 );
		Server.WriteByte( 192 );
		Server.WriteByte( 64 );
		Server.WriteByte( 2 );
		Server.WriteCoord( 0f );
		Server.MessageEnd();
	}

	private void CircleTarget( Vector3 target )
	{
		var destination = target;
		var estimate = Origin + (Velocity * 0.5f);
		var source = Origin;

		destination.z = 0f;
		estimate.z = 0f;
		source.z = 0f;

		var d1 = MathLib.Length( destination - source ) - (24 * Nihilanth.NihilanthScale);
		var d2 = MathLib.Length( destination - estimate ) - (24 * Nihilanth.NihilanthScale);

		if ( MathLib.VectorCompare( _ideal, Vector3.Zero ) )
			_ideal = Velocity;

		if ( d1 < 0f && d2 <= d1 )
			_ideal -= Util.Normalize( destination - source ) * 50f;
		else if ( d1 > 0f && d2 >= d1 )
			_ideal += Util.Normalize( destination - source ) * 50f;

		AVelocity = AVelocity with { z = d1 * 20f };

		_ideal += new Vector3( Server.RandomFloat( -2f, 2f ), Server.RandomFloat( -2f, 2f ), Server.RandomFloat( -2f, 2f ) );
		_ideal = (Util.Normalize( new Vector3( _ideal.x, _ideal.y, 0f ) ) * 200f) + new Vector3( 0f, 0f, _ideal.z );

		d1 = target.z - Origin.z;

		if ( d1 > 0f && _ideal.z < 200f )
			_ideal.z += 20f;
		else if ( d1 < 0f && _ideal.z > -200f )
			_ideal.z -= 20f;

		Velocity = _ideal;
	}

	private void MoveToTarget( Vector3 target )
	{
		if ( MathLib.VectorCompare( _ideal, Vector3.Zero ) )
			_ideal = Velocity;

		if ( MathLib.Length( _ideal ) > 300f )
			_ideal = Util.Normalize( _ideal ) * 300f;

		_ideal += Util.Normalize( target - Origin ) * 300f;
		Velocity = _ideal;
	}

	public void RemoveTouch( BaseEntity other )
	{
		Sound.Stop( this, SoundChannel.Weapon, "x/x_teleattack1.wav" );
		Remove();
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
