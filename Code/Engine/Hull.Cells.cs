using System;
using GoldSrc.Bsp;

namespace GoldSrc;

partial class Hull
{
	private const float CellPadding = 4096f;
	private const float CellSplitEpsilon = 0.01f;
	private const float CellMinExtent = 0.005f;
	private const float CellMergeDistance = 0.005f;
	private const int CellMinVertices = 4;

	private List<List<Vector3>> SolidCells( BBox bounds )
	{
		var mins = bounds.Mins - CellPadding;
		var maxs = bounds.Maxs + CellPadding;
		var corners = new Vector3[8];

		for ( var i = 0; i < corners.Length; i++ )
			corners[i] = new Vector3( (i & 1) == 0 ? mins.x : maxs.x, (i & 2) == 0 ? mins.y : maxs.y, (i & 4) == 0 ? mins.z : maxs.z );

		var faces = new List<List<Vector3>>
		{
			new() { corners[0], corners[2], corners[6], corners[4] },
			new() { corners[1], corners[5], corners[7], corners[3] },
			new() { corners[0], corners[4], corners[5], corners[1] },
			new() { corners[2], corners[3], corners[7], corners[6] },
			new() { corners[0], corners[1], corners[3], corners[2] },
			new() { corners[4], corners[6], corners[7], corners[5] }
		};

		var cells = new List<List<Vector3>>();

		AddCells( cells, FirstClipNode, faces );

		return cells;
	}

	private void AddCells( List<List<Vector3>> cells, int nodeIndex, List<List<Vector3>> faces )
	{
		if ( nodeIndex < 0 )
		{
			if ( nodeIndex != (int)Contents.Solid )
				return;

			var vertices = new List<Vector3>();

			foreach ( var face in faces )
			{
				foreach ( var point in face )
				{
					if ( !Contains( vertices, point, CellMergeDistance ) )
						vertices.Add( point );
				}
			}

			if ( vertices.Count < CellMinVertices )
				return;

			var size = BBox.FromPoints( vertices ).Size;

			if ( MathF.Min( size.x, MathF.Min( size.y, size.z ) ) < CellMinExtent )
				return;

			cells.Add( vertices );

			return;
		}

		var node = ClipNodes[nodeIndex];
		var plane = Planes[node.PlaneNum];

		SplitFaces( faces, plane.Normal, plane.Dist, out var front, out var back );

		if ( front.Count >= CellMinVertices )
			AddCells( cells, node.Child0, front );

		if ( back.Count >= CellMinVertices )
			AddCells( cells, node.Child1, back );
	}

	private static bool Contains( List<Vector3> points, Vector3 point, float distance )
	{
		foreach ( var other in points )
		{
			if ( other.Distance( point ) < distance )
				return true;
		}

		return false;
	}

	private static void SplitFaces( List<List<Vector3>> faces, Vector3 normal, float dist, out List<List<Vector3>> front, out List<List<Vector3>> back )
	{
		front = [];
		back = [];

		var cap = new List<Vector3>();

		foreach ( var face in faces )
		{
			var frontFace = new List<Vector3>();
			var backFace = new List<Vector3>();

			for ( var i = 0; i < face.Count; i++ )
			{
				var a = face[i];
				var b = face[(i + 1) % face.Count];
				var da = Vector3.Dot( normal, a ) - dist;
				var db = Vector3.Dot( normal, b ) - dist;

				if ( MathF.Abs( da ) <= CellSplitEpsilon )
				{
					frontFace.Add( a );
					backFace.Add( a );

					if ( !Contains( cap, a, CellSplitEpsilon ) )
						cap.Add( a );

					continue;
				}

				if ( da > 0f )
					frontFace.Add( a );
				else
					backFace.Add( a );

				if ( MathF.Abs( db ) <= CellSplitEpsilon || da > 0f == db > 0f )
					continue;

				var point = a + ((b - a) * (da / (da - db)));

				frontFace.Add( point );
				backFace.Add( point );

				if ( !Contains( cap, point, CellSplitEpsilon ) )
					cap.Add( point );
			}

			if ( frontFace.Count >= 3 && !OnPlane( frontFace, normal, dist ) )
				front.Add( frontFace );

			if ( backFace.Count >= 3 && !OnPlane( backFace, normal, dist ) )
				back.Add( backFace );
		}

		if ( cap.Count < 3 )
			return;

		var center = Vector3.Zero;

		foreach ( var point in cap )
			center += point;

		center /= cap.Count;

		var axis = MathF.Abs( normal.z ) < 0.9f ? Vector3.Up : Vector3.Forward;
		var u = Vector3.Cross( normal, axis ).Normal;
		var v = Vector3.Cross( normal, u );
		var angles = new float[cap.Count];

		for ( var i = 0; i < angles.Length; i++ )
			angles[i] = MathF.Atan2( Vector3.Dot( cap[i] - center, v ), Vector3.Dot( cap[i] - center, u ) );

		var sorted = cap.ToArray();

		Array.Sort( angles, sorted );

		if ( front.Count > 0 )
			front.Add( [.. sorted] );

		if ( back.Count > 0 )
			back.Add( [.. sorted] );
	}

	private static bool OnPlane( List<Vector3> face, Vector3 normal, float dist )
	{
		foreach ( var point in face )
		{
			if ( MathF.Abs( Vector3.Dot( normal, point ) - dist ) > CellSplitEpsilon )
				return false;
		}

		return true;
	}
}
