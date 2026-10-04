using System;

namespace GoldSrc.Bsp;

partial class File
{
	public const int SurfaceDrawSky = 4;
	public const int SurfaceDrawTurb = 0x10;
	public const int SurfaceDrawTiled = 0x20;

	private const int LuxelSize = 16;

	public (short MinS, short MinT, short ExtentS, short ExtentT)[] SurfaceExtents { get; private set; }
	public int[] SurfaceFlags { get; private set; }
	private int[] _nodeParents;
	private int[] _leafParents;

	private void LoadModelData()
	{
		CalcSurfaceExtents();
		CalcSurfaceFlags();

		_nodeParents = new int[Nodes.Length];
		_leafParents = new int[Leafs.Length];

		if ( Nodes.Length != 0 )
			SetParent( 0, -1 );
	}

	private void CalcSurfaceExtents()
	{
		SurfaceExtents = new (short, short, short, short)[Faces.Length];

		for ( var i = 0; i < SurfaceExtents.Length; i++ )
		{
			var face = Faces[i];
			var texInfo = TexInfos[face.TexInfo];

			var mins = new Vector2( 999999f );
			var maxs = new Vector2( -99999f );

			for ( var e = 0; e < face.NumEdges; e++ )
			{
				var vertex = GetVertex( face, e );

				var s = (float)(((double)vertex.y * texInfo.S.y) + ((double)vertex.x * texInfo.S.x) + ((double)vertex.z * texInfo.S.z) + texInfo.OffsetS);
				var t = (float)(((double)vertex.y * texInfo.T.y) + ((double)vertex.x * texInfo.T.x) + ((double)vertex.z * texInfo.T.z) + texInfo.OffsetT);

				mins = new Vector2( MathF.Min( mins.x, s ), MathF.Min( mins.y, t ) );
				maxs = new Vector2( MathF.Max( maxs.x, s ), MathF.Max( maxs.y, t ) );
			}

			var minS = (int)MathF.Floor( mins.x / LuxelSize );
			var minT = (int)MathF.Floor( mins.y / LuxelSize );
			var maxS = (int)MathF.Ceiling( maxs.x / LuxelSize );
			var maxT = (int)MathF.Ceiling( maxs.y / LuxelSize );

			SurfaceExtents[i] = ((short)(minS * LuxelSize), (short)(minT * LuxelSize), (short)((maxS - minS) * LuxelSize), (short)((maxT - minT) * LuxelSize));
		}
	}

	private void CalcSurfaceFlags()
	{
		SurfaceFlags = new int[Faces.Length];

		for ( var i = 0; i < SurfaceFlags.Length; i++ )
		{
			var face = Faces[i];
			var name = GetTextureName( face ) ?? string.Empty;

			if ( name.StartsWith( "sky", StringComparison.Ordinal ) )
				SurfaceFlags[i] = SurfaceDrawSky | SurfaceDrawTiled;
			else if ( name.StartsWith( "scroll", StringComparison.Ordinal ) )
				SurfaceFlags[i] = SurfaceDrawTiled;
			else if ( name.StartsWith( '!' ) || name.StartsWith( "laser", StringComparison.OrdinalIgnoreCase ) || name.StartsWith( "water", StringComparison.OrdinalIgnoreCase ) )
				SurfaceFlags[i] = SurfaceDrawTurb;
			else if ( (TexInfos[face.TexInfo].Flags & TexInfoFlags.Special) == TexInfoFlags.Special )
				SurfaceFlags[i] = SurfaceDrawTiled;
		}
	}

	private void SetParent( int node, int parent )
	{
		if ( node < 0 )
		{
			_leafParents[-1 - node] = parent;
			return;
		}

		_nodeParents[node] = parent;

		SetParent( Nodes[node].Child0, node );
		SetParent( Nodes[node].Child1, node );
	}

	public int PointInLeaf( Vector3 point )
	{
		var node = 0;

		while ( node >= 0 )
		{
			var plane = Planes[Nodes[node].PlaneNum];
			var distance = (point.y * plane.Normal.y) + (point.x * plane.Normal.x) + (point.z * plane.Normal.z) - plane.Dist;

			node = distance <= 0f ? Nodes[node].Child1 : Nodes[node].Child0;
		}

		return -1 - node;
	}

	public byte[] LeafPvs( int leaf )
	{
		var row = (Models[0].VisLeafs + 7) >> 3;
		var decompressed = new byte[row];

		var offset = leaf == 0 ? -1 : Leafs[leaf].VisOffset;

		if ( offset < 0 || Visibility.Length == 0 )
		{
			Array.Fill( decompressed, (byte)0xff );
			return decompressed;
		}

		var position = 0;

		while ( position < row )
		{
			if ( Visibility[offset] != 0 )
			{
				decompressed[position++] = Visibility[offset++];
				continue;
			}

			var count = Visibility[offset + 1];
			offset += 2;

			for ( ; count > 0 && position < row; count-- )
				decompressed[position++] = 0;
		}

		return decompressed;
	}

	public void MarkLeaves( byte[] pvs, int[] nodeFrames, int[] leafFrames, int frame )
	{
		for ( var i = 0; i < Models[0].VisLeafs; i++ )
		{
			if ( (pvs[i >> 3] & (1 << (i & 7))) == 0 )
				continue;

			if ( leafFrames[i + 1] == frame )
				continue;

			leafFrames[i + 1] = frame;

			for ( var node = _leafParents[i + 1]; node != -1 && nodeFrames[node] != frame; node = _nodeParents[node] )
				nodeFrames[node] = frame;
		}
	}

	public bool PvsNode( int node, Vector3 mins, Vector3 maxs, int[] nodeFrames, int[] leafFrames, int frame )
	{
		if ( node < 0 )
		{
			var leaf = -1 - node;
			return leafFrames[leaf] == frame && Leafs[leaf].Contents != Contents.Solid;
		}

		if ( nodeFrames[node] != frame )
			return false;

		var plane = Planes[Nodes[node].PlaneNum];
		int sides;

		if ( (int)plane.Type < 3 )
		{
			var axis = (int)plane.Type;

			if ( mins[axis] < plane.Dist )
				sides = plane.Dist < maxs[axis] ? 3 : 2;
			else
				sides = 1;
		}
		else
		{
			sides = BoxOnPlaneSide( mins, maxs, plane );
		}

		if ( (sides & 1) != 0 && PvsNode( Nodes[node].Child0, mins, maxs, nodeFrames, leafFrames, frame ) )
			return true;

		return (sides & 2) != 0 && PvsNode( Nodes[node].Child1, mins, maxs, nodeFrames, leafFrames, frame );
	}

	public int SurfaceAtPoint( int nodeIndex, Vector3 start, Vector3 end )
	{
		if ( nodeIndex < 0 )
			return -1;

		var node = Nodes[nodeIndex];
		var plane = Planes[node.PlaneNum];

		var front = Vector3.Dot( start, plane.Normal ) - plane.Dist;
		var back = Vector3.Dot( end, plane.Normal ) - plane.Dist;
		var side = front < 0f ? 1 : 0;

		if ( (back < 0f) == (front < 0f) )
			return SurfaceAtPoint( node.GetChild( side ), start, end );

		var frac = front / (front - back);
		var mid = start + ((end - start) * frac);

		var face = SurfaceAtPoint( node.GetChild( side ), start, mid );
		if ( face >= 0 )
			return face;

		for ( var i = 0; i < node.NumFaces; i++ )
		{
			var index = node.FirstFace + i;
			var texInfo = TexInfos[Faces[index].TexInfo];
			var extents = SurfaceExtents[index];

			var s = (int)(Vector3.Dot( mid, texInfo.S ) + texInfo.OffsetS);
			var t = (int)(Vector3.Dot( mid, texInfo.T ) + texInfo.OffsetT);

			if ( s >= extents.MinS && t >= extents.MinT && s - extents.MinS <= extents.ExtentS && t - extents.MinT <= extents.ExtentT )
				return index;
		}

		return SurfaceAtPoint( node.GetChild( side ^ 1 ), mid, end );
	}

	public static int BoxOnPlaneSide( Vector3 mins, Vector3 maxs, Plane plane )
	{
		var dist1 = 0f;
		var dist2 = 0f;

		for ( var i = 0; i < 3; i++ )
		{
			if ( plane.Normal[i] < 0f )
			{
				dist1 += plane.Normal[i] * mins[i];
				dist2 += plane.Normal[i] * maxs[i];
			}
			else
			{
				dist1 += plane.Normal[i] * maxs[i];
				dist2 += plane.Normal[i] * mins[i];
			}
		}

		var sides = 0;

		if ( dist1 >= plane.Dist )
			sides = 1;

		if ( dist2 < plane.Dist )
			sides |= 2;

		return sides;
	}
}
