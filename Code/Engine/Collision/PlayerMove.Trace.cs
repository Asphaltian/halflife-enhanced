using System;
using GoldSrc.Bsp;

namespace GoldSrc;

partial class PlayerMove
{
	private readonly Hull _boxHull = Hull.CreateBox();
	private readonly StudioHulls _studioHulls = new();

	public int PointContents( Vector3 point, out int trueContents )
	{
		var hull = PhysEnts[0].Model.Hulls[0];

		var contents = hull.PointContents( hull.FirstClipNode, point );
		trueContents = contents;

		if ( contents is <= (int)Contents.Current0 and >= (int)Contents.CurrentDown )
			contents = (int)Contents.Water;
		else if ( contents == (int)Contents.Solid )
			return contents;

		var entityContents = WaterEntity( point, out _ );
		return entityContents == -1 ? contents : entityContents;
	}

	public int PointContents( Vector3 point ) => PointContents( point, out _ );

	public int TruePointContents( Vector3 point )
	{
		var hull = PhysEnts[0].Model.Hulls[0];
		return hull.PointContents( hull.FirstClipNode, point );
	}

	public int WaterEntity( Vector3 point, out int info )
	{
		info = -1;

		for ( var i = 1; i < PhysEnts.Count; i++ )
		{
			var physEnt = PhysEnts[i];
			if ( physEnt.Solid != Solid.Not || physEnt.Model is null )
				continue;

			var hull = physEnt.Model.Hulls[0];
			if ( hull.PointContents( hull.FirstClipNode, point - physEnt.Origin ) == (int)Contents.Empty )
				continue;

			info = physEnt.Info;
			return physEnt.Skin;
		}

		return -1;
	}

	public Hull HullForBsp( PhysEnt physEnt, out Vector3 offset )
	{
		var hull = physEnt.Model.Hulls[UseHull switch
		{
			1 => 3,
			2 => 0,
			3 => 2,
			_ => 1
		}];

		offset = hull.ClipMins - PlayerMins[UseHull] + physEnt.Origin;
		return hull;
	}

	public float TraceModel( PhysEnt physEnt, Vector3 start, Vector3 end, ref Trace trace )
	{
		var saveHull = UseHull;
		UseHull = 2;
		var hull = HullForBsp( physEnt, out var offset );
		UseHull = saveHull;

		hull.ServerRecursiveCheck( hull.FirstClipNode, 0f, 1f, start - offset, end - offset, ref trace );
		trace.Ent = null;

		return trace.Fraction;
	}

	public PmTrace PlayerTrace( Vector3 start, Vector3 end, TraceFlags traceFlags, int ignore )
	{
		return PlayerTrace( start, end, traceFlags, PhysEnts, ignore, null );
	}

	public PmTrace PlayerTrace( Vector3 start, Vector3 end, TraceFlags traceFlags, Func<PhysEnt, bool> ignore )
	{
		return PlayerTrace( start, end, traceFlags, PhysEnts, -1, ignore );
	}

	private PmTrace _traceLine;

	public PmTrace TraceLine( Vector3 start, Vector3 end, int flags, int useHull, int ignore )
	{
		var oldHull = UseHull;

		UseHull = useHull;

		if ( flags == TraceLinePhysEntsOnly )
			_traceLine = PlayerTrace( start, end, TraceFlags.Normal, PhysEnts, ignore, null );
		else if ( flags == TraceLineAnyVisible )
			_traceLine = PlayerTrace( start, end, TraceFlags.Normal, VisEnts, ignore, null );

		UseHull = oldHull;

		return _traceLine;
	}

	public int TestPlayerPosition( Vector3 position, out PmTrace trace )
	{
		return TestPlayerPosition( position, out trace, null );
	}

	public int TestPlayerPosition( Vector3 position, out PmTrace trace, Func<PhysEnt, bool> ignore )
	{
		trace = PlayerTrace( Origin, Origin, TraceFlags.Normal, PhysEnts, -1, null );

		for ( var i = 0; i < PhysEnts.Count; i++ )
		{
			var physEnt = PhysEnts[i];

			if ( ignore is not null && ignore( physEnt ) )
				continue;

			if ( physEnt.Model is not null && physEnt.Solid == Solid.Not && physEnt.Skin != 0 )
				continue;

			var numHulls = 1;
			Hull hull;
			Vector3 offset;

			if ( physEnt.Model is null && physEnt.StudioModel is { } studio && ((studio.Header.Flags & StudioHulls.TraceHitBox) != 0 || UseHull == 2) )
				numHulls = HullForStudioModel( physEnt, studio, out hull, out offset );
			else
				hull = GetHull( physEnt, out offset );

			var test = position - offset;

			if ( physEnt.Solid == Solid.Bsp && HasAngles( physEnt ) )
				test = RotateInto( physEnt.Angles, test );

			if ( numHulls == 1 )
			{
				if ( hull.PointContents( hull.FirstClipNode, test ) == (int)Contents.Solid )
					return i;

				continue;
			}

			for ( var j = 0; j < numHulls; j++ )
			{
				if ( _studioHulls[j].PointContents( _studioHulls[j].FirstClipNode, test ) == (int)Contents.Solid )
					return i;
			}
		}

		return -1;
	}

	private PmTrace PlayerTrace( Vector3 start, Vector3 end, TraceFlags traceFlags, List<PhysEnt> ents, int ignoreIndex, Func<PhysEnt, bool> ignore )
	{
		var trace = new PmTrace { Fraction = 1f, Ent = -1, EndPos = end };

		for ( var i = 0; i < ents.Count; i++ )
		{
			if ( i > 0 && (traceFlags & TraceFlags.WorldOnly) == TraceFlags.WorldOnly )
				break;

			var physEnt = ents[i];

			if ( ignore is not null ? ignore( physEnt ) : ignoreIndex != -1 && i == ignoreIndex )
				continue;

			if ( physEnt.Model is not null && physEnt.Solid == Solid.Not && physEnt.Skin != 0 )
				continue;

			if ( (traceFlags & TraceFlags.GlassIgnore) == TraceFlags.GlassIgnore && physEnt.RenderMode != RenderMode.Normal )
				continue;

			var numHulls = 1;
			Hull hull;
			Vector3 offset;

			if ( physEnt.Model is null && physEnt.StudioModel is { } studio )
			{
				if ( (traceFlags & TraceFlags.StudioIgnore) == TraceFlags.StudioIgnore )
					continue;

				if ( (studio.Header.Flags & StudioHulls.TraceHitBox) != 0 || (UseHull == 2 && (traceFlags & TraceFlags.StudioBox) != TraceFlags.StudioBox) )
					numHulls = HullForStudioModel( physEnt, studio, out hull, out offset );
				else
					hull = GetHull( physEnt, out offset );
			}
			else
			{
				hull = GetHull( physEnt, out offset );
			}

			var startLocal = start - offset;
			var endLocal = end - offset;

			var rotated = physEnt.Solid == Solid.Bsp && HasAngles( physEnt );
			if ( rotated )
			{
				startLocal = RotateInto( physEnt.Angles, startLocal );
				endLocal = RotateInto( physEnt.Angles, endLocal );
			}

			if ( i > 0 && physEnt.Model is { } model && hull == model.Hulls[0] && SegmentOutside( startLocal, endLocal, model.Mins, model.Maxs ) )
				continue;

			var total = new PmTrace { AllSolid = true, Fraction = 1f, EndPos = end };

			if ( numHulls < 1 )
			{
				total.AllSolid = false;
			}
			else if ( numHulls == 1 )
			{
				hull.RecursiveCheck( hull.FirstClipNode, 0f, 1f, startLocal, endLocal, ref total );
			}
			else
			{
				var closest = 0;

				for ( var j = 0; j < numHulls; j++ )
				{
					var test = new PmTrace { AllSolid = true, Fraction = 1f, EndPos = end };

					_studioHulls[j].RecursiveCheck( _studioHulls[j].FirstClipNode, 0f, 1f, startLocal, endLocal, ref test );

					if ( j != 0 && !test.AllSolid && !test.StartSolid && test.Fraction >= total.Fraction )
						continue;

					var startSolid = total.StartSolid;

					total = test;
					closest = j;

					if ( startSolid )
						total.StartSolid = true;
				}

				total.HitGroup = _studioHulls.HitGroup( closest );
			}

			if ( total.AllSolid )
				total.StartSolid = true;

			if ( total.StartSolid )
				total.Fraction = 0f;

			if ( total.Fraction != 1f )
			{
				if ( rotated )
					total.PlaneNormal = RotateOutOf( physEnt.Angles, total.PlaneNormal );

				total.EndPos = start + (total.Fraction * (end - start));
			}

			if ( total.Fraction < trace.Fraction )
			{
				trace = total;
				trace.Ent = i;
			}
		}

		if ( (traceFlags & TraceFlags.WorldOnly) != TraceFlags.WorldOnly && ScenePhysics.Sweep( Scene, start, end, PlayerMins[UseHull], PlayerMaxs[UseHull], out var fraction, out var hit ) && fraction < trace.Fraction )
		{
			trace.Fraction = fraction;
			trace.EndPos = start + (fraction * (end - start));
			trace.PlaneNormal = hit.Normal;
			trace.PlaneDist = Vector3.Dot( hit.Normal, hit.HitPosition );
			trace.Ent = 0;
			trace.HitGroup = 0;

			if ( hit.StartedSolid )
			{
				trace.StartSolid = true;
				trace.AllSolid = start == end;
			}

			SceneContact?.Invoke( hit );
		}

		return trace;
	}

	private int HullForStudioModel( PhysEnt physEnt, Studio.File studio, out Hull hull, out Vector3 offset )
	{
		var size = (PlayerMaxs[UseHull] - PlayerMins[UseHull]) * 0.5f;

		offset = Vector3.Zero;
		hull = _studioHulls[0];

		return _studioHulls.Build( studio, physEnt.Frame, physEnt.Sequence, physEnt.Angles, physEnt.Origin, size, physEnt.Controller, physEnt.Blending );
	}

	private Hull GetHull( PhysEnt physEnt, out Vector3 offset )
	{
		if ( physEnt.Model is not null )
			return HullForBsp( physEnt, out offset );

		_boxHull.SetBox( physEnt.Mins - PlayerMaxs[UseHull], physEnt.Maxs - PlayerMins[UseHull] );
		offset = physEnt.Origin;

		return _boxHull;
	}

	private static bool SegmentOutside( Vector3 start, Vector3 end, Vector3 mins, Vector3 maxs )
	{
		return (start.x < mins.x && end.x < mins.x) || (start.x > maxs.x && end.x > maxs.x)
			|| (start.y < mins.y && end.y < mins.y) || (start.y > maxs.y && end.y > maxs.y)
			|| (start.z < mins.z && end.z < mins.z) || (start.z > maxs.z && end.z > maxs.z);
	}

	private static bool HasAngles( PhysEnt physEnt )
	{
		return physEnt.Angles.x != 0f || physEnt.Angles.y != 0f || physEnt.Angles.z != 0f;
	}

	private static Vector3 RotateInto( Vector3 angles, Vector3 point )
	{
		MathLib.AngleVectors( angles, out var forward, out var right, out var up );
		return new Vector3( Vector3.Dot( point, forward ), -Vector3.Dot( point, right ), Vector3.Dot( point, up ) );
	}

	private static Vector3 RotateOutOf( Vector3 angles, Vector3 normal )
	{
		MathLib.AngleVectorsTranspose( angles, out var forward, out var right, out var up );
		return new Vector3( Vector3.Dot( normal, forward ), Vector3.Dot( normal, right ), Vector3.Dot( normal, up ) );
	}
}
