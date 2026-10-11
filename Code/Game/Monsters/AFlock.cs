using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "monster_flyer_flock" )]
public class FlockingFlyerFlock : BaseMonster
{
	[Save( FieldType.Integer )]
	private int _flockSize;
	[Save( FieldType.Float )]
	private float _flockRadius;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "iFlockSize":
				_flockSize = Util.Atoi( value );
				return true;
			case "flFlockRadius":
				_flockRadius = Util.Atof( value );
				return true;
			default:
				return false;
		}
	}

	public override void Spawn()
	{
		Precache();
		SpawnFlock();

		Server.FreeEdict( this );
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/boid.mdl" );

		PrecacheFlockSounds( Server );
	}

	public static void PrecacheFlockSounds( Server server )
	{
		server.PrecacheSound( "boid/boid_alert1.wav" );
		server.PrecacheSound( "boid/boid_alert2.wav" );

		server.PrecacheSound( "boid/boid_idle1.wav" );
		server.PrecacheSound( "boid/boid_idle2.wav" );
	}

	private void SpawnFlock()
	{
		var radius = _flockRadius;
		FlockingFlyer leader = null;

		for ( var count = 0; count < _flockSize; count++ )
		{
			var boid = Server.CreateEntity<FlockingFlyer>();

			if ( leader is null )
			{
				leader = boid;

				leader.SquadLeader = leader;
				leader.SquadNext = null;
			}

			var spot = new Vector3
			{
				x = Server.RandomFloat( -radius, radius ),
				y = Server.RandomFloat( -radius, radius ),
				z = Server.RandomFloat( 0f, 16f )
			};

			spot = Origin + spot;

			Server.SetOrigin( boid, spot );
			boid.MoveType = MoveType.Fly;
			boid.SpawnCommonCode();
			boid.EntityFlags &= ~EntityFlags.OnGround;
			boid.Velocity = Vector3.Zero;
			boid.Angles = Angles;

			boid.Frame = 0f;
			boid.NextThink = (float)(Server.Globals.Time + 0.2);
			boid.SetThink( boid.IdleThink );

			if ( boid != leader )
				leader.SquadAdd( boid );
		}
	}
}

[LinkEntityToClass( "monster_flyer" )]
public class FlockingFlyer : BaseMonster
{
	private const int FlySpeed = 125;
	private const int TurnRate = 75;
	private const int Accelerate = 10;
	private const int CheckDistance = 192;
	private const int TooClose = 100;
	private const int TooFar = 256;

	[Save( FieldType.ClassPtr )]
	public FlockingFlyer SquadLeader;
	[Save( FieldType.ClassPtr )]
	public FlockingFlyer SquadNext;
	[Save( FieldType.Boolean )]
	private bool _turning;
	[Save( FieldType.Float )]
	private float _goalSpeed;
	[Save( FieldType.Time )]
	private float _lastBlockedTime;
	[Save( FieldType.Time )]
	private float _fakeBlockedTime;
	[Save( FieldType.Time )]
	private float _alertTime;
	private float _flockNextSoundTime;

	private bool IsLeader => SquadLeader == this;

	private bool InSquad => SquadLeader is not null;

	public override void Spawn()
	{
		Precache();
		SpawnCommonCode();

		Frame = 0f;
		NextThink = (float)(Server.Globals.Time + 0.1);
		SetThink( IdleThink );
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/boid.mdl" );
		FlockingFlyerFlock.PrecacheFlockSounds( Server );
	}

	private void MakeSound()
	{
		if ( _alertTime > Server.Globals.Time )
		{
			switch ( Server.RandomLong( 0, 1 ) )
			{
				case 0:
					Sound.Emit( this, SoundChannel.Weapon, "boid/boid_alert1.wav", 1f, Server.AttenuationNormal );
					break;
				case 1:
					Sound.Emit( this, SoundChannel.Weapon, "boid/boid_alert2.wav", 1f, Server.AttenuationNormal );
					break;
			}

			return;
		}

		switch ( Server.RandomLong( 0, 1 ) )
		{
			case 0:
				Sound.Emit( this, SoundChannel.Weapon, "boid/boid_idle1.wav", 1f, Server.AttenuationNormal );
				break;
			case 1:
				Sound.Emit( this, SoundChannel.Weapon, "boid/boid_idle2.wav", 1f, Server.AttenuationNormal );
				break;
		}
	}

	public override void Killed( BaseEntity attacker, int gib )
	{
		var squad = SquadLeader;

		while ( squad is not null )
		{
			squad._alertTime = Server.Globals.Time + 15f;
			squad = squad.SquadNext;
		}

		SquadLeader?.SquadRemove( this );

		DeadFlag = DeadDead;

		FrameRate = 0f;
		Effects = Effects.NoInterp;

		SetSize( Vector3.Zero, Vector3.Zero );
		MoveType = MoveType.Toss;

		SetThink( FallHack );
		NextThink = (float)(Server.Globals.Time + 0.1);
	}

	public void FallHack()
	{
		if ( (EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround )
			return;

		if ( GroundEntity?.ClassName != "worldspawn" )
		{
			EntityFlags &= ~EntityFlags.OnGround;
			NextThink = (float)(Server.Globals.Time + 0.1);
		}
		else
		{
			Velocity = Vector3.Zero;
			SetThink( null );
		}
	}

	public void SpawnCommonCode()
	{
		DeadFlag = DeadNo;
		ClassName = "monster_flyer";
		Solid = Solid.SlideBox;
		MoveType = MoveType.Fly;
		DamageMode = DamageNo;
		Health = 1f;

		FieldOfView = 0.2f;

		SetModel( "models/boid.mdl" );

		SetSize( new Vector3( -5f, -5f, 0f ), new Vector3( 5f, 5f, 2f ) );
	}

	private void BoidAdvanceFrame()
	{
		var flapSpeed = (Speed - ArmorValue) / Accelerate;

		ArmorValue = (float)((ArmorValue * .8) + (Speed * .2));

		if ( flapSpeed < 0f )
			flapSpeed = -flapSpeed;

		if ( flapSpeed < 0.25 )
			flapSpeed = 0.25f;

		if ( flapSpeed > 1.9 )
			flapSpeed = 1.9f;

		FrameRate = flapSpeed;

		var angularVelocity = AVelocity;

		angularVelocity.x = -(Angles.x + (flapSpeed * 5f));
		angularVelocity.z = -(Angles.z + angularVelocity.y);
		AVelocity = angularVelocity;

		StudioFrameAdvance( 0.1f );
	}

	public void IdleThink()
	{
		NextThink = (float)(Server.Globals.Time + 0.2);

		if ( Util.IsNullEnt( Server.FindClientInPvs( this ) ) )
			return;

		SetThink( Start );
		NextThink = (float)(Server.Globals.Time + 0.1);
	}

	public void Start()
	{
		NextThink = (float)(Server.Globals.Time + 0.1);

		if ( IsLeader )
			SetThink( FlockLeaderThink );
		else
			SetThink( FlockFollowerThink );

		SetActivity( Activity.Fly );
		ResetSequenceInfo();
		BoidAdvanceFrame();

		Speed = FlySpeed;
	}

	private void SpreadFlock()
	{
		for ( var member = SquadLeader; member is not null; member = member.SquadNext )
		{
			if ( member == this || MathLib.Length( Origin - member.Origin ) > TooClose )
				continue;

			var direction = Util.Normalize( member.Origin - Origin );
			var speed = MathLib.Length( member.Velocity );

			member.Velocity = Util.Normalize( member.Velocity );
			member.Velocity = (member.Velocity + direction) * 0.5f;
			member.Velocity *= speed;
		}
	}

	private void SpreadFlock2()
	{
		for ( var member = SquadLeader; member is not null; member = member.SquadNext )
		{
			if ( member == this || MathLib.Length( Origin - member.Origin ) > TooClose )
				continue;

			Velocity += Util.Normalize( Origin - member.Origin );
		}
	}

	private bool PathBlocked()
	{
		var globals = Server.Globals;

		if ( _fakeBlockedTime > globals.Time )
		{
			_lastBlockedTime = globals.Time;
			return true;
		}

		globals.MakeVectors( Angles );

		var blocked = false;
		var trace = Util.TraceLine( Origin, Origin + (globals.Forward * CheckDistance), IgnoreMonsters, this );

		if ( trace.Fraction != 1.0 )
		{
			_lastBlockedTime = globals.Time;
			blocked = true;
		}

		trace = Util.TraceLine( Origin + (globals.Right * 12f), Origin + (globals.Right * 12f) + (globals.Forward * CheckDistance), IgnoreMonsters, this );

		if ( trace.Fraction != 1.0 )
		{
			_lastBlockedTime = globals.Time;
			blocked = true;
		}

		trace = Util.TraceLine( Origin - (globals.Right * 12f), Origin - (globals.Right * 12f) + (globals.Forward * CheckDistance), IgnoreMonsters, this );

		if ( trace.Fraction != 1.0 )
		{
			_lastBlockedTime = globals.Time;
			blocked = true;
		}

		if ( !blocked && globals.Time - _lastBlockedTime > 6f )
			_fakeBlockedTime = globals.Time + Server.RandomLong( 1, 3 );

		return blocked;
	}

	public void FlockLeaderThink()
	{
		var globals = Server.Globals;

		NextThink = (float)(globals.Time + 0.1);

		globals.MakeVectors( Angles );

		if ( !PathBlocked() )
		{
			if ( _turning )
			{
				_turning = false;
				AVelocity = AVelocity with { y = 0f };
			}

			if ( Speed <= FlySpeed )
				Speed += 5f;

			Velocity = globals.Forward * Speed;

			BoidAdvanceFrame();

			return;
		}

		Trace trace;

		if ( !_turning )
		{
			trace = Util.TraceLine( Origin, Origin + (globals.Right * CheckDistance), IgnoreMonsters, this );

			var rightSide = MathLib.Length( trace.EndPos - Origin );

			trace = Util.TraceLine( Origin, Origin - (globals.Right * CheckDistance), IgnoreMonsters, this );

			var leftSide = MathLib.Length( trace.EndPos - Origin );

			_turning = true;

			if ( rightSide > leftSide )
				AVelocity = AVelocity with { y = -TurnRate };
			else if ( leftSide > rightSide )
				AVelocity = AVelocity with { y = TurnRate };
			else if ( Server.RandomLong( 0, 1 ) == 0 )
				AVelocity = AVelocity with { y = TurnRate };
			else
				AVelocity = AVelocity with { y = -TurnRate };
		}

		SpreadFlock();

		Velocity = globals.Forward * Speed;

		trace = Util.TraceLine( Origin, Origin - (globals.Up * 16f), IgnoreMonsters, this );

		if ( trace.Fraction != 1.0 && Velocity.z < 0f )
			Velocity = Velocity with { z = 0f };

		if ( (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround )
		{
			SetOrigin( Origin + new Vector3( 0f, 0f, 1f ) );
			Velocity = Velocity with { z = 0f };
		}

		if ( _flockNextSoundTime < globals.Time )
		{
			MakeSound();
			_flockNextSoundTime = globals.Time + Server.RandomFloat( 1f, 3f );
		}

		BoidAdvanceFrame();
	}

	public void FlockFollowerThink()
	{
		NextThink = (float)(Server.Globals.Time + 0.1);

		if ( IsLeader || !InSquad )
		{
			SetThink( FlockLeaderThink );
			return;
		}

		var directionToLeader = SquadLeader.Origin - Origin;
		var distanceToLeader = MathLib.Length( directionToLeader );

		Angles = SquadLeader.Angles;

		if ( IsInViewCone( SquadLeader ) )
		{
			if ( distanceToLeader > TooFar )
				_goalSpeed = (float)(MathLib.Length( SquadLeader.Velocity ) * 1.5);
			else if ( distanceToLeader < TooClose )
				_goalSpeed = (float)(MathLib.Length( SquadLeader.Velocity ) * 0.5);
		}
		else
		{
			_goalSpeed = (float)(MathLib.Length( SquadLeader.Velocity ) * 0.5);
		}

		SpreadFlock2();

		Speed = MathLib.Length( Velocity );
		Velocity = Util.Normalize( Velocity );

		if ( distanceToLeader > TooFar )
			Velocity = (Velocity + Util.Normalize( directionToLeader )) * 0.5f;

		if ( _goalSpeed > FlySpeed * 2 )
			_goalSpeed = FlySpeed * 2;

		if ( Speed < _goalSpeed )
			Speed += Accelerate;
		else if ( Speed > _goalSpeed )
			Speed -= Accelerate;

		Velocity *= Speed;

		BoidAdvanceFrame();
	}

	private void SquadUnlink()
	{
		SquadLeader = null;
		SquadNext = null;
	}

	public void SquadAdd( FlockingFlyer add )
	{
		add.SquadNext = SquadNext;
		SquadNext = add;
		add.SquadLeader = this;
	}

	private void SquadRemove( FlockingFlyer remove )
	{
		if ( SquadCount() <= 2 )
		{
			SquadDisband();
			return;
		}

		if ( remove == this )
		{
			var leader = SquadNext;

			leader.EnemyLastKnownPosition = EnemyLastKnownPosition;

			for ( var member = leader; member is not null; member = member.SquadNext )
				member.SquadLeader = leader;

			SquadUnlink();
			return;
		}

		var previous = this;

		while ( previous.SquadNext != remove )
			previous = previous.SquadNext;

		previous.SquadNext = remove.SquadNext;

		remove.SquadUnlink();
	}

	private int SquadCount()
	{
		var count = 0;

		for ( var member = SquadLeader; member is not null; member = member.SquadNext )
			count++;

		return count;
	}

	private void SquadDisband()
	{
		var member = SquadLeader;

		while ( member is not null )
		{
			var next = member.SquadNext;

			member.SquadUnlink();
			member = next;
		}
	}
}
