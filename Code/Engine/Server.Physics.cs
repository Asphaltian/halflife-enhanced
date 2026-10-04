using System;

namespace GoldSrc;

partial class Server
{
	public double FrameTime { get; private set; }

	public bool KeyDestGame { get; set; } = true;

	public bool ClientActive { get; set; }

	public bool TimeStopped => Paused || (MaxClients <= 1 && (!KeyDestGame || !ClientActive));

	private bool _touchLinksSemaphore;

	public void Frame( double frameTime )
	{
		if ( !Active )
			return;

		Globals.FrameTime = (float)frameTime;

		CheckCommandTimes();
		ReadPackets();

		if ( !TimeStopped )
		{
			Physics( frameTime );

			Time += frameTime;
		}

		CheckMoveVars();
		CheckTimeouts();
		SendClientMessages();
		PublishPlayerData();
		UpdateVoiceTransmitters();
	}

	private void Physics( double frameTime )
	{
		FrameTime = frameTime;
		Globals.FrameTime = (float)frameTime;
		Globals.Time = (float)Time;

		GameDll.StartFrame( this );

		for ( var i = 0; i < _numEdicts; i++ )
		{
			var edict = _edicts[i];
			if ( edict is null )
				continue;

			if ( Globals.ForceRetouch != 0f )
				LinkEdict( edict, true );

			if ( i > 0 && i <= MaxClients )
				continue;

			if ( (edict.EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround && edict.GroundEntity is { } ground && (ground.EntityFlags & EntityFlags.Conveyor) == EntityFlags.Conveyor )
			{
				if ( (edict.EntityFlags & EntityFlags.BaseVelocity) == EntityFlags.BaseVelocity )
					edict.BaseVelocity += ground.Speed * ground.MoveDir;
				else
					edict.BaseVelocity = ground.Speed * ground.MoveDir;

				edict.EntityFlags |= EntityFlags.BaseVelocity;
			}

			if ( (edict.EntityFlags & EntityFlags.BaseVelocity) != EntityFlags.BaseVelocity )
			{
				edict.Velocity += (float)(1.0 + (FrameTime * 0.5)) * edict.BaseVelocity;
				edict.BaseVelocity = Vector3.Zero;
			}

			edict.EntityFlags &= ~EntityFlags.BaseVelocity;

			switch ( edict.MoveType )
			{
				case MoveType.Push:
					PhysicsPusher( edict );
					break;

				case MoveType.None:
					RunThink( edict );
					break;

				case MoveType.Follow:
					PhysicsFollow( edict );
					break;

				case MoveType.NoClip:
					PhysicsNoClip( edict );
					break;

				case MoveType.Step:
				case MoveType.PushStep:
					PhysicsStep( edict );
					break;

				case MoveType.Toss:
				case MoveType.Bounce:
				case MoveType.BounceMissile:
				case MoveType.Fly:
				case MoveType.FlyMissile:
					PhysicsToss( edict );
					break;

				default:
					throw new InvalidOperationException( $"SV_Physics: {edict.ClassName} bad movetype {(int)edict.MoveType}" );
			}

			if ( (edict.EntityFlags & EntityFlags.KillMe) == EntityFlags.KillMe )
				FreeEdict( edict );
		}

		if ( Globals.ForceRetouch != 0f )
			Globals.ForceRetouch -= 1f;
	}

	private bool RunThink( Edict edict )
	{
		if ( (edict.EntityFlags & EntityFlags.KillMe) != EntityFlags.KillMe )
		{
			var thinkTime = edict.NextThink;

			if ( thinkTime <= 0f || thinkTime > Time + FrameTime )
				return true;

			if ( thinkTime < Time )
				thinkTime = (float)Time;

			edict.NextThink = 0f;
			Globals.Time = thinkTime;
			edict.DispatchThink();

			if ( (edict.EntityFlags & EntityFlags.KillMe) != EntityFlags.KillMe )
				return IsInUse( edict );
		}

		FreeEdict( edict );
		return false;
	}

	private void PhysicsPusher( Edict edict )
	{
		var oldLocalTime = edict.LTime;
		var thinkTime = edict.NextThink;
		float moveTime;

		if ( thinkTime < oldLocalTime + FrameTime )
		{
			moveTime = thinkTime - oldLocalTime;
			if ( moveTime < 0f )
				moveTime = 0f;
		}
		else
		{
			moveTime = (float)FrameTime;
		}

		if ( moveTime != 0f )
		{
			if ( !IsZero( edict.AVelocity ) )
			{
				if ( !IsZero( edict.Velocity ) )
				{
					if ( PushRotate( edict, moveTime ) )
					{
						var saveTime = edict.LTime;

						edict.LTime = oldLocalTime;
						PushMove( edict, moveTime );

						if ( edict.LTime < saveTime )
							edict.LTime = saveTime;
					}
				}
				else
				{
					PushRotate( edict, moveTime );
				}
			}
			else
			{
				PushMove( edict, moveTime );
			}
		}

		var angles = edict.Angles;
		var wrapped = false;

		for ( var i = 0; i < 3; i++ )
		{
			if ( angles[i] < -3600f || angles[i] > 3600f )
			{
				angles[i] %= 3600f;
				wrapped = true;
			}
		}

		if ( wrapped )
			edict.Angles = angles;

		if ( thinkTime > oldLocalTime && ((edict.EntityFlags & EntityFlags.AlwaysThink) == EntityFlags.AlwaysThink || thinkTime <= edict.LTime) )
		{
			edict.NextThink = 0f;
			Globals.Time = (float)Time;
			edict.DispatchThink();
		}
	}

	private void PhysicsFollow( Edict edict )
	{
		if ( !RunThink( edict ) )
			return;

		var aim = edict.AimEntity;
		if ( aim is null )
		{
			Log.Info( $"{edict.ClassName} movetype FOLLOW with NULL aiment" );
			edict.MoveType = MoveType.None;
			return;
		}

		edict.Origin = aim.Origin + edict.ViewAngle;
		edict.Angles = aim.Angles;

		LinkEdict( edict, true );
	}

	private void PhysicsNoClip( Edict edict )
	{
		if ( !RunThink( edict ) )
			return;

		edict.Angles += (float)FrameTime * edict.AVelocity;
		edict.Origin += (float)FrameTime * edict.Velocity;

		LinkEdict( edict, false );
	}

	private void PushMove( Edict pusher, float moveTime )
	{
		if ( IsZero( pusher.Velocity ) )
		{
			pusher.LTime += moveTime;
			return;
		}

		var pushOrigin = pusher.Origin;
		var move = pusher.Velocity * moveTime;

		var mins = pusher.AbsMin + move;
		var maxs = pusher.AbsMax + move;

		pusher.Origin += move;
		pusher.LTime += moveTime;

		LinkEdict( pusher, false );

		if ( pusher.Solid == Solid.Not )
			return;

		var moved = new List<(Edict Edict, Vector3 Origin)>();

		for ( var e = 1; e < _numEdicts; e++ )
		{
			var check = _edicts[e];
			if ( check is null || !CanBePushed( check ) )
				continue;

			if ( (check.EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround || check.GroundEntity != pusher )
			{
				if ( check.AbsMin.x >= maxs.x || check.AbsMin.y >= maxs.y || check.AbsMin.z >= maxs.z )
					continue;

				if ( check.AbsMax.x <= mins.x || check.AbsMax.y <= mins.y || check.AbsMax.z <= mins.z )
					continue;

				if ( TestEntityPosition( check ) is null )
					continue;
			}

			if ( check.MoveType != MoveType.Walk )
				check.EntityFlags &= ~EntityFlags.OnGround;

			var entityOrigin = check.Origin;
			moved.Add( (check, entityOrigin) );

			pusher.Solid = Solid.Not;
			PushEntity( check, move );
			pusher.Solid = Solid.Bsp;

			if ( TestEntityPosition( check ) is null || check.Mins.x == check.Maxs.x )
				continue;

			if ( check.Solid is Solid.Not or Solid.Trigger )
			{
				check.Mins = new Vector3( 0f, 0f, check.Mins.z );
				check.Maxs = new Vector3( 0f, 0f, check.Mins.z );
				continue;
			}

			check.Origin = entityOrigin;
			LinkEdict( check, true );

			pusher.Origin = pushOrigin;
			LinkEdict( pusher, false );

			pusher.LTime -= moveTime;

			pusher.DispatchBlocked( check );

			foreach ( var (edict, origin) in moved )
			{
				edict.Origin = origin;
				LinkEdict( edict, false );
			}

			return;
		}
	}

	private bool PushRotate( Edict pusher, float moveTime )
	{
		if ( IsZero( pusher.AVelocity ) )
		{
			pusher.LTime += moveTime;
			return true;
		}

		var angularMove = pusher.AVelocity * moveTime;

		MathLib.AngleVectors( pusher.Angles, out var forwardNow, out var rightNow, out var upNow );

		var pushAngles = pusher.Angles;
		pusher.Angles += angularMove;

		MathLib.AngleVectorsTranspose( pusher.Angles, out var forward, out var right, out var up );

		pusher.LTime += moveTime;

		LinkEdict( pusher, false );

		if ( pusher.Solid == Solid.Not )
			return true;

		var moved = new List<(Edict Edict, Vector3 Origin)>();

		for ( var e = 1; e < _numEdicts; e++ )
		{
			var check = _edicts[e];
			if ( check is null || !CanBePushed( check ) )
				continue;

			if ( (check.EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround || check.GroundEntity != pusher )
			{
				if ( check.AbsMin.x >= pusher.AbsMax.x || check.AbsMin.y >= pusher.AbsMax.y || check.AbsMin.z >= pusher.AbsMax.z )
					continue;

				if ( check.AbsMax.x <= pusher.AbsMin.x || check.AbsMax.y <= pusher.AbsMin.y || check.AbsMax.z <= pusher.AbsMin.z )
					continue;

				if ( TestEntityPosition( check ) is null )
					continue;
			}

			if ( check.MoveType != MoveType.Walk )
				check.EntityFlags &= ~EntityFlags.OnGround;

			var entityOrigin = check.Origin;
			moved.Add( (check, entityOrigin) );

			if ( moved.Count >= _maxEdicts )
				throw new InvalidOperationException( "Out of edicts in simulator!\n" );

			var start = check.MoveType == MoveType.PushStep ? (check.AbsMin + check.AbsMax) * 0.5f : check.Origin;
			var org = start - pusher.Origin;

			var temp = new Vector3( Vector3.Dot( org, forwardNow ), -Vector3.Dot( org, rightNow ), Vector3.Dot( org, upNow ) );
			var push = new Vector3( Vector3.Dot( temp, forward ), Vector3.Dot( temp, right ), Vector3.Dot( temp, up ) ) - org;

			pusher.Solid = Solid.Not;
			PushEntity( check, push );
			pusher.Solid = Solid.Bsp;

			if ( check.MoveType != MoveType.PushStep )
			{
				if ( (check.EntityFlags & EntityFlags.Client) == EntityFlags.Client )
				{
					check.FixAngle = 2;
					check.AVelocity = new Vector3( check.AVelocity.x, check.AVelocity.y + angularMove.y, check.AVelocity.z );
				}
				else
				{
					check.Angles = new Vector3( check.Angles.x, check.Angles.y + angularMove.y, check.Angles.z );
				}
			}

			if ( TestEntityPosition( check ) is null || check.Mins.x == check.Maxs.x )
				continue;

			if ( check.Solid is Solid.Not or Solid.Trigger )
			{
				check.Mins = new Vector3( 0f, 0f, check.Mins.z );
				check.Maxs = new Vector3( 0f, 0f, check.Mins.z );
				continue;
			}

			check.Origin = entityOrigin;
			LinkEdict( check, true );

			pusher.Angles = pushAngles;
			LinkEdict( pusher, false );

			pusher.LTime -= moveTime;

			pusher.DispatchBlocked( check );

			foreach ( var (edict, origin) in moved )
			{
				edict.Origin = origin;

				if ( (edict.EntityFlags & EntityFlags.Client) == EntityFlags.Client )
					edict.AVelocity = new Vector3( edict.AVelocity.x, 0f, edict.AVelocity.z );
				else if ( edict.MoveType != MoveType.PushStep )
					edict.Angles = new Vector3( edict.Angles.x, edict.Angles.y - angularMove.y, edict.Angles.z );

				LinkEdict( edict, false );
			}

			return false;
		}

		return true;
	}

	private Trace PushEntity( Edict edict, Vector3 push )
	{
		var end = edict.Origin + push;

		int moveType;

		if ( edict.MoveType == MoveType.FlyMissile )
			moveType = (int)MoveKind.Missile;
		else if ( edict.Solid is Solid.Trigger or Solid.Not )
			moveType = (int)MoveKind.NoMonsters;
		else
			moveType = (int)MoveKind.Normal;

		var trace = Move( edict.Origin, edict.Mins, edict.Maxs, end, moveType, edict, (edict.EntityFlags & EntityFlags.MonsterClip) == EntityFlags.MonsterClip );

		if ( trace.Fraction != 0f )
			edict.Origin = trace.EndPos;

		LinkEdict( edict, true );

		if ( trace.Ent is not null )
			Impact( edict, trace.Ent, trace );

		return trace;
	}

	private void SetGlobalTrace( Trace trace )
	{
		trace.Ent ??= _edicts[0];

		Globals.Trace = trace;
		Globals.TraceEnt = trace.Ent;
	}

	private void Impact( Edict first, Edict second, in Trace trace )
	{
		Globals.Time = (float)Time;

		if ( ((first.EntityFlags | second.EntityFlags) & EntityFlags.KillMe) == EntityFlags.KillMe )
			return;

		if ( first.Solid != Solid.Not )
		{
			SetGlobalTrace( trace );
			first.DispatchTouch( second );
		}

		if ( second.Solid != Solid.Not )
		{
			SetGlobalTrace( trace );
			second.DispatchTouch( first );
		}
	}

	private static bool CanBePushed( Edict edict )
	{
		return edict.MoveType is not (MoveType.Push or MoveType.None or MoveType.Follow or MoveType.NoClip);
	}

	private bool IsInUse( Edict edict )
	{
		return _edicts[edict.Index] == edict;
	}

	private static bool IsZero( Vector3 v )
	{
		return v.x == 0f && v.y == 0f && v.z == 0f;
	}
}
