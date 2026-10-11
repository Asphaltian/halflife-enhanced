using System;
using GoldSrc.Bsp;

namespace GoldSrc;

public enum WalkMoveMode
{
	Normal,
	WorldOnly,
	CheckOnly
}

partial class Server
{
	private const float NoDirection = -1f;

	private Trace MoveNoEntities( Vector3 start, Vector3 mins, Vector3 maxs, Vector3 end, int type, Edict passEdict )
	{
		var worldTrace = ClipMoveToEntity( _edicts[0], start, mins, maxs, end );
		if ( worldTrace.Fraction == 0f )
			return worldTrace;

		var worldFraction = worldTrace.Fraction;
		worldTrace.Fraction = 1f;

		var clip = new MoveClip
		{
			Start = start,
			End = worldTrace.EndPos,
			Mins = mins,
			Maxs = maxs,
			Mins2 = mins,
			Maxs2 = maxs,
			Type = (MoveKind)(type & 0xff),
			IgnoreTransparent = (type >> 8) != 0,
			PassEdict = passEdict,
			Trace = worldTrace
		};

		clip.BoxMins = Vector3.Min( start, clip.End ) + mins - 1f;
		clip.BoxMaxs = Vector3.Max( start, clip.End ) + maxs + 1f;

		ClipToWorldBrush( _areaNodes, clip );

		clip.Trace.Fraction *= worldFraction;
		Globals.TraceEnt = clip.Trace.Ent;

		return clip.Trace;
	}

	private void ClipToWorldBrush( AreaNode node, MoveClip clip )
	{
		foreach ( var touch in node.SolidEdicts )
		{
			if ( touch.Solid != Solid.Bsp || touch == clip.PassEdict || (touch.EntityFlags & EntityFlags.WorldBrush) != EntityFlags.WorldBrush )
				continue;

			if ( clip.BoxMins.x > touch.AbsMax.x || clip.BoxMins.y > touch.AbsMax.y || clip.BoxMins.z > touch.AbsMax.z )
				continue;

			if ( clip.BoxMaxs.x < touch.AbsMin.x || clip.BoxMaxs.y < touch.AbsMin.y || clip.BoxMaxs.z < touch.AbsMin.z )
				continue;

			if ( clip.Trace.AllSolid )
				return;

			var trace = ClipMoveToEntity( touch, clip.Start, clip.Mins, clip.Maxs, clip.End );

			if ( !trace.AllSolid && !trace.StartSolid && trace.Fraction >= clip.Trace.Fraction )
				continue;

			trace.Ent = touch;

			var startSolid = clip.Trace.StartSolid;
			clip.Trace = trace;

			if ( startSolid )
				clip.Trace.StartSolid = true;
		}

		if ( node.Axis == -1 )
			return;

		if ( clip.BoxMaxs[node.Axis] > node.Dist )
			ClipToWorldBrush( node.Children[0], clip );

		if ( clip.BoxMins[node.Axis] < node.Dist )
			ClipToWorldBrush( node.Children[1], clip );
	}

	private bool MoveOnGround( Edict edict, Vector3 move, bool relink, bool worldOnly )
	{
		var oldOrigin = edict.Origin;
		var stepSize = SvStepSize;
		var monsterClip = (edict.EntityFlags & EntityFlags.MonsterClip) == EntityFlags.MonsterClip;

		Trace StepTrace( Vector3 start, Vector3 end )
		{
			return worldOnly
				? MoveNoEntities( start, edict.Mins, edict.Maxs, end, 0, edict )
				: Move( start, edict.Mins, edict.Maxs, end, 0, edict, monsterClip );
		}

		var newOrigin = new Vector3( oldOrigin.x + move.x, oldOrigin.y + move.y, oldOrigin.z + move.z + stepSize );
		var end = new Vector3( newOrigin.x, newOrigin.y, newOrigin.z - (stepSize * 2f) );
		var trace = StepTrace( newOrigin, end );

		if ( trace.AllSolid )
			return false;

		if ( trace.StartSolid )
		{
			newOrigin.z -= stepSize;
			trace = StepTrace( newOrigin, end );

			if ( trace.AllSolid || trace.StartSolid )
				return false;
		}

		if ( trace.Fraction == 1f )
		{
			if ( (edict.EntityFlags & EntityFlags.PartialGround) != EntityFlags.PartialGround )
				return false;

			edict.Origin += move;

			if ( relink )
				LinkEdict( edict, true );

			edict.EntityFlags &= ~EntityFlags.OnGround;

			return true;
		}

		edict.Origin = trace.EndPos;

		if ( !CheckBottom( edict ) )
		{
			if ( (edict.EntityFlags & EntityFlags.PartialGround) != EntityFlags.PartialGround )
			{
				edict.Origin = oldOrigin;
				return false;
			}
		}
		else
		{
			edict.EntityFlags &= ~EntityFlags.PartialGround;
			edict.GroundEntity = trace.Ent;
		}

		if ( relink )
			LinkEdict( edict, true );

		return true;
	}

	private bool MoveStep( Edict edict, Vector3 move, bool relink )
	{
		if ( (edict.EntityFlags & (EntityFlags.Swim | EntityFlags.Fly)) == 0 )
			return MoveOnGround( edict, move, relink, false );

		var monsterClip = (edict.EntityFlags & EntityFlags.MonsterClip) == EntityFlags.MonsterClip;

		for ( var i = 0; i < 2; i++ )
		{
			var newOrigin = edict.Origin + move;
			var enemy = edict.Enemy;

			if ( i == 0 && enemy is not null )
			{
				var deltaZ = edict.Origin.z - enemy.Origin.z;

				if ( deltaZ > 40f )
					newOrigin.z -= 8f;

				if ( deltaZ < 30f )
					newOrigin.z += 8f;
			}

			var trace = Move( edict.Origin, edict.Mins, edict.Maxs, newOrigin, 0, edict, monsterClip );

			if ( trace.Fraction == 1f )
			{
				if ( (edict.EntityFlags & EntityFlags.Swim) == EntityFlags.Swim && PointContents( trace.EndPos ) == (int)Contents.Empty )
					return false;

				edict.Origin = trace.EndPos;

				if ( relink )
					LinkEdict( edict, true );

				return true;
			}

			if ( enemy is null )
				break;
		}

		return false;
	}

	private static Vector3 YawMove( float yaw, float distance )
	{
		var radians = (double)(float)(((yaw * Math.PI) + (yaw * Math.PI)) / 360.0);

		return new Vector3( (float)(Math.Cos( radians ) * distance), (float)(Math.Sin( radians ) * distance), 0f );
	}

	private bool StepDirection( Edict edict, float yaw, float distance )
	{
		var moved = MoveStep( edict, YawMove( yaw, distance ), false );

		LinkEdict( edict, true );

		return moved;
	}

	private void NewChaseDirection( Edict actor, Vector3 goal, float distance )
	{
		var oldDirection = MathLib.AngleMod( (int)(actor.IdealYaw / 45f) * 45 );
		var turnAround = MathLib.AngleMod( oldDirection - 180f );
		var deltaX = goal.x - actor.Origin.x;
		var deltaY = goal.y - actor.Origin.y;

		var directionX = deltaX > 10f ? 0f : deltaX < -10f ? 180f : NoDirection;
		var directionY = deltaY < -10f ? 270f : deltaY > 10f ? 90f : NoDirection;

		if ( directionX != NoDirection && directionY != NoDirection )
		{
			float diagonal = directionX == 0f
				? directionY == 90f ? 45 : 315
				: directionY == 90f ? 135 : 215;

			if ( diagonal != turnAround && StepDirection( actor, diagonal, distance ) )
				return;
		}

		if ( RandomLong( 0, 1 ) != 0 || Math.Abs( (int)deltaY ) > Math.Abs( (int)deltaX ) )
			(directionX, directionY) = (directionY, directionX);

		if ( directionX != NoDirection && directionX != turnAround && StepDirection( actor, directionX, distance ) )
			return;

		if ( directionY != NoDirection && directionY != turnAround && StepDirection( actor, directionY, distance ) )
			return;

		if ( oldDirection != NoDirection && StepDirection( actor, oldDirection, distance ) )
			return;

		if ( RandomLong( 0, 1 ) == 0 )
		{
			for ( var direction = 315f; direction >= 0f; direction -= 45f )
			{
				if ( direction != turnAround && StepDirection( actor, direction, distance ) )
					return;
			}
		}
		else
		{
			for ( var direction = 0f; direction <= 315f; direction += 45f )
			{
				if ( direction != turnAround && StepDirection( actor, direction, distance ) )
					return;
			}
		}

		if ( turnAround != NoDirection && StepDirection( actor, turnAround, distance ) )
			return;

		actor.IdealYaw = oldDirection;

		if ( !CheckBottom( actor ) )
			actor.EntityFlags |= EntityFlags.PartialGround;
	}

	public bool WalkMove( Edict edict, float yaw, float distance, WalkMoveMode mode )
	{
		if ( (edict.EntityFlags & (EntityFlags.OnGround | EntityFlags.Fly | EntityFlags.Swim)) == 0 )
			return false;

		var move = YawMove( yaw, distance );

		return mode == WalkMoveMode.WorldOnly ? MoveOnGround( edict, move, true, true ) : MoveStep( edict, move, mode != WalkMoveMode.CheckOnly );
	}

	public void MoveToOrigin( Edict edict, Vector3 goal, float distance, int moveType )
	{
		if ( (edict.EntityFlags & (EntityFlags.OnGround | EntityFlags.Fly | EntityFlags.Swim)) == 0 )
			return;

		if ( moveType != 0 )
		{
			var delta = goal - edict.Origin;

			if ( (edict.EntityFlags & (EntityFlags.Fly | EntityFlags.Swim)) == 0 )
				delta.z = 0f;

			MathLib.VectorNormalize( ref delta );

			MoveStep( edict, delta * distance, false );
			LinkEdict( edict, true );
			return;
		}

		if ( !StepDirection( edict, edict.IdealYaw, distance ) )
			NewChaseDirection( edict, goal, distance );
	}
}
