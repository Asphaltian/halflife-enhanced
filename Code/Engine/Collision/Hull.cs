using System;
using GoldSrc.Bsp;
using Plane = GoldSrc.Bsp.Plane;

namespace GoldSrc;

public struct PmTrace
{
	public bool AllSolid;
	public bool StartSolid;
	public bool InOpen;
	public bool InWater;
	public float Fraction;
	public Vector3 EndPos;
	public Vector3 PlaneNormal;
	public float PlaneDist;
	public int Ent;
	public Vector3 DeltaVelocity;
	public int HitGroup;
}

public struct Trace
{
	public bool AllSolid;
	public bool StartSolid;
	public bool InOpen;
	public bool InWater;
	public float Fraction;
	public Vector3 EndPos;
	public Vector3 PlaneNormal;
	public float PlaneDist;
	public Edict Ent;
	public int HitGroup;
	public int HitBone;
	public Rigidbody Body;
}

public sealed partial class Hull
{
	private const float DistEpsilon = 0.03125f;

	public ClipNode[] ClipNodes { get; init; }
	public Plane[] Planes { get; init; }
	public int FirstClipNode { get; init; }
	public int LastClipNode { get; init; }
	public Vector3 ClipMins { get; init; }
	public Vector3 ClipMaxs { get; init; }

	public static ClipNode[] MakeHull0( File file )
	{
		var clipNodes = new ClipNode[file.Nodes.Length];

		for ( var i = 0; i < clipNodes.Length; i++ )
		{
			var node = file.Nodes[i];
			clipNodes[i] = new ClipNode( node.PlaneNum, GetHull0Child( file, node.Child0 ), GetHull0Child( file, node.Child1 ) );
		}

		return clipNodes;
	}

	public static Hull CreateBox()
	{
		var clipNodes = new ClipNode[6];

		for ( var i = 0; i < clipNodes.Length; i++ )
		{
			var next = (short)(i == 5 ? (int)Contents.Solid : i + 1);
			clipNodes[i] = (i & 1) == 0
				? new ClipNode( i, (short)Contents.Empty, next )
				: new ClipNode( i, next, (short)Contents.Empty );
		}

		return new Hull
		{
			ClipNodes = clipNodes,
			Planes = new Plane[6],
			FirstClipNode = 0,
			LastClipNode = 5
		};
	}

	public void SetBox( Vector3 mins, Vector3 maxs )
	{
		for ( var i = 0; i < 6; i++ )
		{
			var axis = i >> 1;
			var normal = new Vector3();
			normal[axis] = 1f;

			Planes[i] = new Plane( normal, (i & 1) == 0 ? maxs[axis] : mins[axis], (PlaneType)axis );
		}
	}

	public int PointContents( int num, Vector3 point )
	{
		if ( PhysicsBounds is not null )
			return (int)(PhysicsPointSolid( point ) ? Contents.Solid : Contents.Empty);

		if ( LastClipNode <= FirstClipNode )
			return (int)Contents.Empty;

		while ( num >= 0 )
		{
			if ( num < FirstClipNode || num > LastClipNode )
				throw new InvalidOperationException( "PM_HullPointContents: bad node number" );

			var node = ClipNodes[num];
			num = node.GetChild( Distance( Planes[node.PlaneNum], point ) >= 0f ? 0 : 1 );
		}

		return num;
	}

	public bool RecursiveCheck( int num, float p1f, float p2f, Vector3 p1, Vector3 p2, ref PmTrace trace )
	{
		if ( PhysicsBounds is not null )
		{
			PhysicsCheck( p1, p2, ref trace );
			return trace.Fraction == 1f;
		}

		if ( num < 0 )
		{
			if ( num == (int)Contents.Solid )
			{
				trace.StartSolid = true;
				return true;
			}

			trace.AllSolid = false;

			if ( num == (int)Contents.Empty )
				trace.InOpen = true;
			else
				trace.InWater = true;

			return true;
		}

		if ( LastClipNode <= FirstClipNode )
		{
			trace.AllSolid = false;
			trace.InOpen = true;
			return true;
		}

		var node = ClipNodes[num];
		ref readonly var plane = ref Planes[node.PlaneNum];

		var t1 = Distance( plane, p1 );
		var t2 = Distance( plane, p2 );

		if ( t1 >= 0f && t2 >= 0f )
			return RecursiveCheck( node.Child0, p1f, p2f, p1, p2, ref trace );

		if ( t1 < 0f && t2 < 0f )
			return RecursiveCheck( node.Child1, p1f, p2f, p1, p2, ref trace );

		var frac = (t1 < 0f ? t1 + DistEpsilon : t1 - DistEpsilon) / (t1 - t2);
		frac = Math.Clamp( frac, 0f, 1f );

		var midf = p1f + ((p2f - p1f) * frac);
		var mid = p1 + ((p2 - p1) * frac);
		var side = t1 < 0f ? 1 : 0;

		if ( !RecursiveCheck( node.GetChild( side ), p1f, midf, p1, mid, ref trace ) )
			return false;

		if ( PointContents( node.GetChild( side ^ 1 ), mid ) != (int)Contents.Solid )
			return RecursiveCheck( node.GetChild( side ^ 1 ), midf, p2f, mid, p2, ref trace );

		if ( trace.AllSolid )
			return false;

		if ( side == 0 )
		{
			trace.PlaneNormal = plane.Normal;
			trace.PlaneDist = plane.Dist;
		}
		else
		{
			trace.PlaneNormal = Vector3.Zero - plane.Normal;
			trace.PlaneDist = -plane.Dist;
		}

		while ( PointContents( FirstClipNode, mid ) == (int)Contents.Solid )
		{
			frac = (float)(frac - 0.05);

			if ( frac < 0f )
			{
				trace.Fraction = midf;
				trace.EndPos = mid;
				Con.DPrintf( "Trace backed up past 0.0.\n" );
				return false;
			}

			midf = p1f + ((p2f - p1f) * frac);
			mid = p1 + ((p2 - p1) * frac);
		}

		trace.Fraction = midf;
		trace.EndPos = mid;
		return false;
	}

	public int ServerPointContents( int num, Vector3 point )
	{
		if ( PhysicsBounds is not null )
			return (int)(PhysicsPointSolid( point ) ? Contents.Solid : Contents.Empty);

		while ( num >= 0 )
		{
			if ( num < FirstClipNode || num > LastClipNode )
				throw new InvalidOperationException( "SV_HullPointContents: bad node number" );

			var node = ClipNodes[num];
			num = node.GetChild( Distance( Planes[node.PlaneNum], point ) >= 0f ? 0 : 1 );
		}

		return num;
	}

	public bool ServerRecursiveCheck( int num, float p1f, float p2f, Vector3 p1, Vector3 p2, ref Trace trace )
	{
		if ( PhysicsBounds is not null )
		{
			PhysicsCheck( p1, p2, ref trace );
			return trace.Fraction == 1f;
		}

		if ( num < 0 )
		{
			if ( num == (int)Contents.Solid )
			{
				trace.StartSolid = true;
				return true;
			}

			trace.AllSolid = false;

			if ( num == (int)Contents.Empty )
				trace.InOpen = true;
			else if ( num != (int)Contents.Translucent )
				trace.InWater = true;

			return true;
		}

		if ( num < FirstClipNode || num > LastClipNode || Planes is null )
			throw new InvalidOperationException( "SV_RecursiveHullCheck: bad node number" );

		var node = ClipNodes[num];
		ref readonly var plane = ref Planes[node.PlaneNum];

		var t1 = Distance( plane, p1 );
		var t2 = Distance( plane, p2 );

		if ( t1 >= 0f && t2 >= 0f )
			return ServerRecursiveCheck( node.Child0, p1f, p2f, p1, p2, ref trace );

		if ( t1 < 0f && t2 < 0f )
			return ServerRecursiveCheck( node.Child1, p1f, p2f, p1, p2, ref trace );

		var frac = (t1 < 0f ? t1 + DistEpsilon : t1 - DistEpsilon) / (t1 - t2);
		frac = Math.Clamp( frac, 0f, 1f );

		if ( !float.IsFinite( frac ) )
			return false;

		var midf = p1f + ((p2f - p1f) * frac);
		var mid = p1 + ((p2 - p1) * frac);
		var side = t1 < 0f ? 1 : 0;

		if ( !ServerRecursiveCheck( node.GetChild( side ), p1f, midf, p1, mid, ref trace ) )
			return false;

		if ( ServerPointContents( node.GetChild( side ^ 1 ), mid ) != (int)Contents.Solid )
			return ServerRecursiveCheck( node.GetChild( side ^ 1 ), midf, p2f, mid, p2, ref trace );

		if ( trace.AllSolid )
			return false;

		if ( side == 0 )
		{
			trace.PlaneNormal = plane.Normal;
			trace.PlaneDist = plane.Dist;
		}
		else
		{
			trace.PlaneNormal = Vector3.Zero - plane.Normal;
			trace.PlaneDist = -plane.Dist;
		}

		while ( ServerPointContents( FirstClipNode, mid ) == (int)Contents.Solid )
		{
			frac = (float)(frac - 0.1);

			if ( frac < 0f )
			{
				trace.Fraction = midf;
				trace.EndPos = mid;
				Con.DPrintf( "backup past 0\n" );
				return false;
			}

			midf = p1f + ((p2f - p1f) * frac);
			mid = p1 + ((p2 - p1) * frac);
		}

		trace.Fraction = midf;
		trace.EndPos = mid;
		return false;
	}

	private static float Distance( in Plane plane, Vector3 point )
	{
		switch ( plane.Type )
		{
			case PlaneType.X:
				return point.x - plane.Dist;
			case PlaneType.Y:
				return point.y - plane.Dist;
			case PlaneType.Z:
				return point.z - plane.Dist;
		}

		return (plane.Normal.y * point.y) + (plane.Normal.x * point.x) + (plane.Normal.z * point.z) - plane.Dist;
	}

	private static short GetHull0Child( File file, short child )
	{
		return child >= 0 ? child : (short)file.Leafs[-1 - child].Contents;
	}
}
