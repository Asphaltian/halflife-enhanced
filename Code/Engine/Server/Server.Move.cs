using System;
using GoldSrc.Bsp;

namespace GoldSrc;

public enum MoveKind
{
	Normal,
	NoMonsters,
	Missile
}

partial class Server
{
	private sealed class MoveClip
	{
		public Vector3 BoxMins;
		public Vector3 BoxMaxs;
		public Vector3 Mins;
		public Vector3 Maxs;
		public Vector3 Mins2;
		public Vector3 Maxs2;
		public Vector3 Start;
		public Vector3 End;
		public Trace Trace;
		public MoveKind Type;
		public bool IgnoreTransparent;
		public Edict PassEdict;
		public bool MonsterClipBrush;
	}

	private readonly Hull _boxHull = Hull.CreateBox();

	public Trace Move( Vector3 start, Vector3 mins, Vector3 maxs, Vector3 end, int type, Edict passEdict, bool monsterClipBrush )
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
			Type = (MoveKind)(type & 0xff),
			IgnoreTransparent = (type >> 8) != 0,
			PassEdict = passEdict,
			MonsterClipBrush = monsterClipBrush,
			Trace = worldTrace
		};

		if ( type == (int)MoveKind.Missile )
		{
			clip.Mins2 = new Vector3( -15f );
			clip.Maxs2 = new Vector3( 15f );
		}
		else
		{
			clip.Mins2 = mins;
			clip.Maxs2 = maxs;
		}

		clip.BoxMins = Vector3.Min( start, clip.End ) + clip.Mins2 - 1f;
		clip.BoxMaxs = Vector3.Max( start, clip.End ) + clip.Maxs2 + 1f;

		ClipToLinks( _areaNodes, clip );

		if ( ScenePhysics.Sweep( Scene, start, clip.End, mins, maxs, out var fraction, out var hit ) && fraction < clip.Trace.Fraction )
		{
			clip.Trace.Fraction = fraction;
			clip.Trace.EndPos = start + (fraction * (clip.End - start));
			clip.Trace.PlaneNormal = hit.Normal;
			clip.Trace.PlaneDist = Vector3.Dot( hit.Normal, hit.HitPosition );
			clip.Trace.Ent = _edicts[0];
			clip.Trace.Body = hit.Collider?.Rigidbody;

			if ( hit.StartedSolid )
			{
				clip.Trace.StartSolid = true;
				clip.Trace.AllSolid = start == clip.End;
			}
		}

		clip.Trace.Fraction *= worldFraction;
		Globals.TraceEnt = clip.Trace.Ent;

		return clip.Trace;
	}

	private static readonly Vector3[] HullMins = [Vector3.Zero, new( -16f, -16f, -36f ), new( -32f, -32f, -32f ), new( -16f, -16f, -18f )];
	private static readonly Vector3[] HullMaxs = [Vector3.Zero, new( 16f, 16f, 36f ), new( 32f, 32f, 32f ), new( 16f, 16f, 18f )];

	public Trace TraceLine( Vector3 start, Vector3 end, int noMonsters, Edict passEdict )
	{
		var trace = Move( start, Vector3.Zero, Vector3.Zero, end, noMonsters, passEdict ?? _edicts[0], false );

		Globals.TraceFlags = 0;

		trace.Ent ??= _edicts[0];

		Globals.Trace = trace;

		return trace;
	}

	public Trace TraceHull( Vector3 start, Vector3 end, int noMonsters, int hullNumber, Edict passEdict )
	{
		if ( hullNumber < 0 || hullNumber > 3 )
			hullNumber = 0;

		return Move( start, HullMins[hullNumber], HullMaxs[hullNumber], end, noMonsters, passEdict, false );
	}

	public Trace TraceMonsterHull( Edict edict, Vector3 start, Vector3 end, int noMonsters, Edict passEdict )
	{
		return Move( start, edict.Mins, edict.Maxs, end, noMonsters, passEdict, (edict.EntityFlags & EntityFlags.MonsterClip) == EntityFlags.MonsterClip );
	}

	public Trace TraceModel( Vector3 start, Vector3 end, int hullNumber, Edict edict )
	{
		if ( hullNumber < 0 || hullNumber > 3 )
			hullNumber = 0;

		if ( GetModel( edict.ModelIndex ) is null )
			return ClipMoveToEntity( edict, start, HullMins[hullNumber], HullMaxs[hullNumber], end );

		var solid = edict.Solid;
		var moveType = edict.MoveType;

		edict.Solid = Solid.Bsp;
		edict.MoveType = MoveType.Push;

		var trace = ClipMoveToEntity( edict, start, HullMins[hullNumber], HullMaxs[hullNumber], end );

		edict.Solid = solid;
		edict.MoveType = moveType;

		return trace;
	}

	public Edict TestEntityPosition( Edict edict )
	{
		var trace = Move( edict.Origin, edict.Mins, edict.Maxs, edict.Origin, 0, edict, (edict.EntityFlags & EntityFlags.MonsterClip) == EntityFlags.MonsterClip );

		if ( !trace.StartSolid )
			return null;

		SetGlobalTrace( trace );

		return trace.Ent;
	}

	private Trace ClipMoveToEntity( Edict edict, Vector3 start, Vector3 mins, Vector3 maxs, Vector3 end )
	{
		var trace = new Trace { Fraction = 1f, AllSolid = true, EndPos = end };

		Hull hull;
		Vector3 offset;

		var numHulls = 1;

		if ( GetStudioModel( edict.ModelIndex ) is { } studio )
			numHulls = HullForStudioModel( edict, studio, mins, maxs, out hull, out offset );
		else
			hull = HullForEntity( edict, mins, maxs, out offset );

		var startLocal = start - offset;
		var endLocal = end - offset;

		var rotated = edict.Solid == Solid.Bsp && (edict.Angles.x != 0f || edict.Angles.y != 0f || edict.Angles.z != 0f);

		if ( rotated )
		{
			MathLib.AngleVectors( edict.Angles, out var forward, out var right, out var up );

			startLocal = new Vector3( Vector3.Dot( startLocal, forward ), -Vector3.Dot( startLocal, right ), Vector3.Dot( startLocal, up ) );
			endLocal = new Vector3( Vector3.Dot( endLocal, forward ), -Vector3.Dot( endLocal, right ), Vector3.Dot( endLocal, up ) );
		}

		if ( numHulls == 1 )
		{
			hull.ServerRecursiveCheck( hull.FirstClipNode, 0f, 1f, startLocal, endLocal, ref trace );
		}
		else
		{
			var closest = 0;

			for ( var i = 0; i < numHulls; i++ )
			{
				var test = new Trace { Fraction = 1f, AllSolid = true, EndPos = end };

				_studioHulls[i].ServerRecursiveCheck( _studioHulls[i].FirstClipNode, 0f, 1f, startLocal, endLocal, ref test );

				if ( i != 0 && !test.AllSolid && !test.StartSolid && test.Fraction >= trace.Fraction )
					continue;

				var startSolid = trace.StartSolid;

				trace = test;
				closest = i;

				if ( startSolid )
					trace.StartSolid = true;
			}

			trace.HitGroup = _studioHulls.HitGroup( closest );
		}

		if ( trace.Fraction != 1f )
		{
			if ( rotated )
			{
				MathLib.AngleVectorsTranspose( edict.Angles, out var forward, out var right, out var up );

				var normal = trace.PlaneNormal;
				trace.PlaneNormal = new Vector3( Vector3.Dot( normal, forward ), Vector3.Dot( normal, right ), Vector3.Dot( normal, up ) );
			}

			trace.EndPos = start + (trace.Fraction * (end - start));
		}

		if ( trace.Fraction < 1f || trace.StartSolid )
			trace.Ent = edict;

		return trace;
	}

	private Hull HullForEntity( Edict edict, Vector3 mins, Vector3 maxs, out Vector3 offset )
	{
		if ( edict.Solid != Solid.Bsp )
		{
			_boxHull.SetBox( edict.Mins - maxs, edict.Maxs - mins );

			offset = edict.Origin;
			return _boxHull;
		}

		if ( edict.MoveType != MoveType.Push && edict.MoveType != MoveType.PushStep )
			throw new InvalidOperationException( "SOLID_BSP without MOVETYPE_PUSH" );

		return HullForBsp( edict, mins, maxs, out offset );
	}

	private Hull HullForBsp( Edict edict, Vector3 mins, Vector3 maxs, out Vector3 offset )
	{
		var model = GetModel( edict.ModelIndex );
		if ( model is null )
			throw new InvalidOperationException( $"Hit a {edict.ClassName} with no model ({edict.ModelName})" );

		var width = maxs.x - mins.x;
		Hull hull;

		if ( width <= 8f )
		{
			hull = model.Hulls[0];
			offset = hull.ClipMins;
		}
		else
		{
			if ( width > 36f )
				hull = model.Hulls[2];
			else if ( maxs.z - mins.z > 36f )
				hull = model.Hulls[1];
			else
				hull = model.Hulls[3];

			offset = hull.ClipMins - mins;
		}

		offset += edict.Origin;
		return hull;
	}

	private bool CheckSphereIntersection( Edict edict, Vector3 start, Vector3 end )
	{
		if ( (edict.EntityFlags & EntityFlags.Client) != EntityFlags.Client )
			return true;

		var sequence = GetModelPtr( edict ).Sequences[edict.Sequence];
		var traceOrigin = start - edict.Origin;
		var traceDirection = end - start;

		var maxDim = new Vector3(
			(float)Math.Max( Math.Abs( (double)sequence.BBMin.x ), Math.Abs( (double)sequence.BBMax.x ) ),
			(float)Math.Max( Math.Abs( (double)sequence.BBMin.y ), Math.Abs( (double)sequence.BBMax.y ) ),
			(float)Math.Max( Math.Abs( (double)sequence.BBMin.z ), Math.Abs( (double)sequence.BBMax.z ) ) );

		var radiusSquared = (maxDim.x * maxDim.x) + (maxDim.y * maxDim.y) + (maxDim.z * maxDim.z);
		var b = (traceOrigin.y * traceDirection.y) + (traceOrigin.x * traceDirection.x) + (traceOrigin.z * traceDirection.z);

		b += b;

		var c = (traceOrigin.y * traceOrigin.y) + (traceOrigin.x * traceOrigin.x) + (traceOrigin.z * traceOrigin.z) - radiusSquared;
		var a = (traceDirection.x * traceDirection.x) + (traceDirection.y * traceDirection.y) + (traceDirection.z * traceDirection.z);

		return (b * b) - (c * a * 4f) > 0.000001f;
	}

	private void ClipToLinks( AreaNode node, MoveClip clip )
	{
		foreach ( var touch in node.SolidEdicts )
		{
			if ( touch.Solid == Solid.Not || touch == clip.PassEdict )
				continue;

			if ( touch.Solid == Solid.Trigger )
				throw new InvalidOperationException( "Trigger in clipping list" );

			if ( touch.Solid == Solid.Bsp )
			{
				if ( (touch.EntityFlags & EntityFlags.MonsterClip) == EntityFlags.MonsterClip && !clip.MonsterClipBrush )
					continue;
			}
			else if ( clip.Type == MoveKind.NoMonsters && touch.MoveType != MoveType.PushStep )
			{
				continue;
			}

			if ( clip.IgnoreTransparent && touch.RenderMode != RenderMode.Normal && (touch.EntityFlags & EntityFlags.WorldBrush) != EntityFlags.WorldBrush )
				continue;

			if ( clip.BoxMins.x > touch.AbsMax.x || clip.BoxMins.y > touch.AbsMax.y || clip.BoxMins.z > touch.AbsMax.z )
				continue;

			if ( clip.BoxMaxs.x < touch.AbsMin.x || clip.BoxMaxs.y < touch.AbsMin.y || clip.BoxMaxs.z < touch.AbsMin.z )
				continue;

			if ( touch.Solid != Solid.SlideBox && !CheckSphereIntersection( touch, clip.Start, clip.End ) )
				continue;

			if ( clip.PassEdict is not null && clip.PassEdict.Size.x != 0f && touch.Size.x == 0f )
				continue;

			if ( clip.Trace.AllSolid )
				return;

			if ( clip.PassEdict is not null && (touch.Owner == clip.PassEdict || clip.PassEdict.Owner == touch) )
				continue;

			var trace = (touch.EntityFlags & EntityFlags.Monster) == EntityFlags.Monster
				? ClipMoveToEntity( touch, clip.Start, clip.Mins2, clip.Maxs2, clip.End )
				: ClipMoveToEntity( touch, clip.Start, clip.Mins, clip.Maxs, clip.End );

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
			ClipToLinks( node.Children[0], clip );

		if ( clip.BoxMins[node.Axis] < node.Dist )
			ClipToLinks( node.Children[1], clip );
	}

	private void TouchLink( Edict edict, Edict touch )
	{
		if ( touch == edict || touch.Solid != Solid.Trigger )
			return;

		if ( edict.AbsMin.x > touch.AbsMax.x || edict.AbsMin.y > touch.AbsMax.y || edict.AbsMin.z > touch.AbsMax.z )
			return;

		if ( edict.AbsMax.x < touch.AbsMin.x || edict.AbsMax.y < touch.AbsMin.y || edict.AbsMax.z < touch.AbsMin.z )
			return;

		if ( GetModel( touch.ModelIndex ) is not null )
		{
			var hull = HullForBsp( touch, edict.Mins, edict.Maxs, out var offset );

			if ( hull.ServerPointContents( hull.FirstClipNode, edict.Origin - offset ) != (int)Contents.Solid )
				return;
		}

		Globals.Time = (float)Time;
		touch.DispatchTouch( edict );
	}

	private void TouchLinks( Edict edict, AreaNode node )
	{
		for ( var link = node.TriggerEdicts.First; link is not null; )
		{
			var next = link.Next;

			TouchLink( edict, link.Value );

			link = next;
		}

		if ( node.Axis == -1 )
			return;

		if ( edict.AbsMax[node.Axis] > node.Dist )
			TouchLinks( edict, node.Children[0] );

		if ( edict.AbsMin[node.Axis] < node.Dist )
			TouchLinks( edict, node.Children[1] );
	}
}
