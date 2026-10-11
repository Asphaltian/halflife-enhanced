using GoldSrc;
using GoldSrc.Bsp;

namespace HalfLife;

public abstract class FlyingMonster : BaseMonster
{
	private const int AnimEventFlap = 8;
	private const int AnimEventFlapSound = 9;

	private const int MoveStrafe = 1;

	protected Vector3 Travel;
	protected float FlightSpeed;
	protected float StopTime;
	protected float Momentum;
	protected string FlapSound;
	protected float LastZYawTime;

	public void SetFlyingMomentum( float momentum ) => Momentum = momentum;

	public void SetFlyingFlapSound( string flapSound ) => FlapSound = flapSound;

	public void SetFlyingSpeed( float speed ) => FlightSpeed = speed;

	public override LocalMove CheckLocalMove( Vector3 start, Vector3 end, BaseEntity target, out float distanceMoved )
	{
		distanceMoved = 0f;

		if ( (EntityFlags & EntityFlags.Swim) == EntityFlags.Swim && Util.PointContents( end ) != (int)Contents.Water )
			return LocalMove.Invalid;

		var lift = new Vector3( 0f, 0f, 32f );
		var trace = Util.TraceHull( start + lift, end + lift, DontIgnoreMonsters, LargeHull, this );

		distanceMoved = MathLib.Length( trace.EndPos - lift - start );

		if ( trace.StartSolid || trace.Fraction < 1.0 )
			return target is not null && target == Server.Globals.TraceEnt ? LocalMove.Valid : LocalMove.Invalid;

		return LocalMove.Valid;
	}

	public override Activity GetStoppedActivity() => MoveType != MoveType.Fly ? Activity.Idle : Activity.Hover;

	public override void Stop()
	{
		var stopped = GetStoppedActivity();

		if ( IdealActivity != stopped )
		{
			FlightSpeed = 0f;
			IdealActivity = stopped;
		}

		Angles = Angles with { z = 0f, x = 0f };
		Travel = Vector3.Zero;
	}

	public override float ChangeYaw( int yawSpeed )
	{
		if ( MoveType == MoveType.Fly )
		{
			var globals = Server.Globals;
			var difference = YawDiff();
			var target = 0f;

			if ( LastZYawTime == 0f )
				LastZYawTime = globals.Time - globals.FrameTime;

			if ( IdealActivity != GetStoppedActivity() )
			{
				if ( difference < -20f )
					target = 90f;
				else if ( difference > 20f )
					target = -90f;
			}

			var delta = globals.Time - LastZYawTime;

			LastZYawTime = globals.Time;

			if ( delta > 0.25f )
				delta = 0.25f;

			Angles = Angles with { z = Util.Approach( target, Angles.z, 220f * delta ) };
		}

		return base.ChangeYaw( yawSpeed );
	}

	protected void MonsterKilled( BaseEntity attacker, int gib ) => base.Killed( attacker, gib );

	public override void Killed( BaseEntity attacker, int gib )
	{
		MoveType = MoveType.Step;
		EntityFlags &= ~EntityFlags.OnGround;
		Angles = Angles with { z = 0f, x = 0f };

		base.Killed( attacker, gib );
	}

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		switch ( monsterEvent.Event )
		{
			case AnimEventFlap:
				FlightSpeed = 400f;
				break;

			case AnimEventFlapSound:
				if ( FlapSound is not null )
					Sound.Emit( this, SoundChannel.Body, FlapSound, 1f, Server.AttenuationNormal );

				break;

			default:
				base.HandleAnimEvent( monsterEvent );
				break;
		}
	}

	public override void Move( float interval = 0.1f )
	{
		if ( MoveType == MoveType.Fly )
			GroundSpeed = FlightSpeed;

		base.Move( interval );
	}

	public override bool ShouldAdvanceRoute( float waypointDistance )
	{
		if ( (Route[RouteIndex].Type & MoveFlag.IsGoal) != 0 )
			waypointDistance = MathLib.Length( Route[RouteIndex].Location - Origin );

		return waypointDistance <= 64f + (GroundSpeed * Server.Globals.FrameTime);
	}

	public override void MoveExecute( BaseEntity target, Vector3 direction, float interval )
	{
		if ( MoveType != MoveType.Fly )
		{
			base.MoveExecute( target, direction, interval );
			return;
		}

		var globals = Server.Globals;

		if ( globals.Time - StopTime > 1.0 && IdealActivity != MovementActivity )
		{
			IdealActivity = MovementActivity;
			GroundSpeed = FlightSpeed = 200f;
		}

		var move = Origin + (Util.Normalize( direction + (Travel * Momentum) ) * (GroundSpeed * interval));

		if ( IdealActivity != MovementActivity )
		{
			FlightSpeed = Util.Approach( 100f, FlightSpeed, 75f * globals.FrameTime );

			if ( FlightSpeed < 100f )
				StopTime = globals.Time;
		}
		else
		{
			FlightSpeed = Util.Approach( 20f, FlightSpeed, 300f * globals.FrameTime );
		}

		if ( CheckLocalMove( Origin, move, target, out _ ) != LocalMove.Invalid )
		{
			Travel = Util.Normalize( move - Origin );

			Server.MoveToOrigin( this, move, GroundSpeed * interval, MoveStrafe );
		}
		else
		{
			IdealActivity = GetStoppedActivity();
			StopTime = globals.Time;
			Travel = Vector3.Zero;
		}
	}

	public float CeilingZ( Vector3 position )
	{
		var minUp = position;
		var maxUp = position;

		maxUp.z += 4096f;

		var trace = Util.TraceLine( position, maxUp, IgnoreMonsters, null );

		if ( trace.Fraction != 1.0 )
			maxUp.z = trace.EndPos.z;

		return (EntityFlags & EntityFlags.Swim) == EntityFlags.Swim ? Util.WaterLevel( position, minUp.z, maxUp.z ) : maxUp.z;
	}

	public bool ProbeZ( Vector3 position, Vector3 probe, out float fraction )
	{
		var positionContents = Util.PointContents( position );

		if ( (EntityFlags & EntityFlags.Swim) == EntityFlags.Swim ^ (positionContents == (int)Contents.Water) )
		{
			fraction = 0f;
			return true;
		}

		if ( Util.PointContents( probe ) == positionContents )
		{
			fraction = 1f;
			return false;
		}

		var probeUnit = Util.Normalize( probe - position );
		var probeLength = MathLib.Length( probe - position );
		var maxProbeLength = probeLength;
		var minProbeLength = 0f;
		var difference = maxProbeLength - minProbeLength;

		while ( difference > 1.0 )
		{
			var midProbeLength = (float)(minProbeLength + (difference / 2.0));

			if ( Util.PointContents( position + (midProbeLength * probeUnit) ) == positionContents )
				minProbeLength = midProbeLength;
			else
				maxProbeLength = midProbeLength;

			difference = maxProbeLength - minProbeLength;
		}

		fraction = minProbeLength / probeLength;
		return true;
	}

	public float FloorZ( Vector3 position )
	{
		var down = position;

		down.z -= 2048f;

		var trace = Util.TraceLine( position, down, IgnoreMonsters, null );

		return trace.Fraction != 1.0 ? trace.EndPos.z : down.z;
	}
}
