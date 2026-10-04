using System;
using System.Text;
using GoldSrc;

namespace HalfLife;

partial class Graph
{
	private const string ReportRule = "----------------------------------------------------------------------------";
	private const int EntryStateEmpty = -1;
	private const int UnnumberedNode = -1;

	private static readonly int[] Primes =
	[
		1, 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67,
		71, 73, 79, 83, 89, 97, 101, 103, 107, 109, 113, 127, 131, 137, 139, 149, 151,
		157, 163, 167, 173, 179, 181, 191, 193, 197, 199, 211, 223, 227, 229, 233, 239,
		241, 251, 257, 263, 269, 271, 277, 281, 283, 293, 307, 311, 313, 317, 331, 337,
		347, 349, 353, 359, 367, 373, 379, 383, 389, 397, 401, 409, 419, 421, 431, 433,
		439, 443, 449, 457, 461, 463, 467, 479, 487, 491, 499, 503, 509, 521, 523, 541,
		547, 557, 563, 569, 571, 577, 587, 593, 599, 601, 607, 613, 617, 619, 631, 641,
		643, 647, 653, 659, 661, 673, 677, 683, 691, 701, 709, 719, 727, 733, 739, 743,
		751, 757, 761, 769, 773, 787, 797, 809, 811, 821, 823, 827, 829, 839, 853, 857,
		859, 863, 877, 881, 883, 887, 907, 911, 919, 929, 937, 941, 947, 953, 967, 971,
		977, 983, 991, 997, 1009, 1013, 1019, 1021, 1031, 1033, 1039, 0
	];

	public int LinkVisibleNodes( GraphLink[] linkPool, StringBuilder report, out int badNode )
	{
		badNode = 0;

		if ( NodeCount <= 0 )
		{
			Util.AiAlert( "No Nodes!" );
			return 0;
		}

		report.Append( ReportRule ).Append( '\n' );
		report.Append( "LinkVisibleNodes - Initial Connections" ).Append( '\n' );
		report.Append( ReportRule ).Append( '\n' );

		var totalLinks = 0;
		var maxInitialLinks = 0;

		for ( var i = 0; i < NodeCount; i++ )
		{
			var linksThisNode = 0;

			report.Append( $"Node #{i,4}:\n\n" );

			for ( var z = 0; z < MaxNodeInitialLinks; z++ )
			{
				linkPool[totalLinks + z].SourceNode = i;
				linkPool[totalLinks + z].DestinationNode = 0;
				linkPool[totalLinks + z].LinkEntity = null;
			}

			Nodes[i].FirstLink = totalLinks;

			for ( var j = 0; j < NodeCount; j++ )
			{
				if ( j == i )
					continue;

				if ( (Nodes[i].NodeInfo & GraphNode.GroupRealm) != (Nodes[j].NodeInfo & GraphNode.GroupRealm) )
					continue;

				Edict traceEntity = null;
				var trace = Util.TraceLine( Nodes[i].Origin, Nodes[j].Origin, BaseEntity.IgnoreMonsters, HalfLifeDll.BodyQueueHead );

				if ( trace.StartSolid )
					continue;

				if ( trace.Fraction != 1f )
				{
					traceEntity = trace.Ent;
					trace = Util.TraceLine( Nodes[j].Origin, Nodes[i].Origin, BaseEntity.IgnoreMonsters, HalfLifeDll.BodyQueueHead );

					if ( trace.Ent != traceEntity || trace.Ent.ClassName == "worldspawn" )
						continue;

					var modelName = trace.Ent.ModelName ?? "";

					linkPool[totalLinks].LinkEntity = (BaseEntity)trace.Ent;
					linkPool[totalLinks].LinkEntityModelName = modelName.Length > 4 ? modelName[..4] : modelName;

					trace.Ent.EntityFlags |= EntityFlags.Graphed;
				}

				report.Append( $"{j,4}" );

				if ( !Util.IsNullEnt( linkPool[totalLinks].LinkEntity ) )
					report.Append( $"  Entity on connection: {traceEntity.ClassName}, name: {traceEntity.TargetName}  Model: {trace.Ent.ModelName}" );

				report.Append( '\n' );

				linkPool[totalLinks].DestinationNode = j;
				linksThisNode++;
				totalLinks++;

				if ( linksThisNode == MaxNodeInitialLinks )
				{
					Util.AiAlert( $"**LinkVisibleNodes:\nNode {i} has NodeLinks > MAX_NODE_INITIAL_LINKS" );
					report.Append( $"** NODE {i} HAS NodeLinks > MAX_NODE_INITIAL_LINKS **\n" );
					badNode = i;
					return 0;
				}

				if ( totalLinks > MaxNodeInitialLinks * NodeCount )
				{
					Util.AiAlert( "**LinkVisibleNodes:\nTotalLinks > MAX_NODE_INITIAL_LINKS * NUMNODES" );
					badNode = i;
					return 0;
				}

				Nodes[i].NumLinks = linksThisNode;

				if ( linksThisNode > maxInitialLinks )
					maxInitialLinks = linksThisNode;
			}

			report.Append( ReportRule ).Append( '\n' );
		}

		report.Append( $"\n{totalLinks,4} Total Initial Connections - {maxInitialLinks,4} Maximum connections for a single node.\n" );
		report.Append( ReportRule ).Append( "\n\n\n" );

		return totalLinks;
	}

	public int RejectInlineLinks( GraphLink[] linkPool, StringBuilder report )
	{
		report.Append( ReportRule ).Append( '\n' );
		report.Append( "InLine Rejection:" ).Append( '\n' );
		report.Append( ReportRule ).Append( '\n' );

		var rejectedLinks = 0;

		for ( var i = 0; i < NodeCount; i++ )
		{
			var source = Nodes[i];

			report.Append( $"Node {i,3}:\n" );

			for ( var j = 0; j < source.NumLinks; j++ )
			{
				var checkNode = Nodes[linkPool[source.FirstLink + j].DestinationNode];
				var checkDistance = MathLib.Length2D( checkNode.Origin - source.Origin );
				var checkDirection = MathLib.Normalize2D( checkNode.Origin - source.Origin );

				linkPool[source.FirstLink + j].Weight = checkDistance;

				var restartLoop = false;

				for ( var k = 0; k < source.NumLinks && !restartLoop; k++ )
				{
					if ( k == j )
						continue;

					var testNode = Nodes[linkPool[source.FirstLink + k].DestinationNode];
					var testDistance = MathLib.Length2D( testNode.Origin - source.Origin );
					var testDirection = MathLib.Normalize2D( testNode.Origin - source.Origin );
					var dot = (checkDirection.x * testDirection.x) + (checkDirection.y * testDirection.y);

					if ( dot < 0.998 || testDistance >= checkDistance )
						continue;

					report.Append( $"REJECTED NODE {linkPool[source.FirstLink + j].DestinationNode,3} through Node {linkPool[source.FirstLink + k].DestinationNode,3}, Dot = {dot,8:F6}\n" );

					linkPool[source.FirstLink + j] = linkPool[source.FirstLink + (source.NumLinks - 1)];
					source.NumLinks--;
					j--;

					rejectedLinks++;
					restartLoop = true;
				}
			}

			report.Append( ReportRule ).Append( "\n\n" );
		}

		return rejectedLinks;
	}

	private int LinkHashStart( int sourceNode, int destinationNode, out int step )
	{
		var hash = Crc32.Compute( (short)sourceNode, (short)destinationNode );

		step = _hashPrimes[hash & 15];

		return (int)((hash >> 4) % (uint)_hashLinkCount);
	}

	private void HashInsert( int sourceNode, int destinationNode, int key )
	{
		var i = LinkHashStart( sourceNode, destinationNode, out var step );

		while ( _hashLinks[i] != EntryStateEmpty )
		{
			i += step;

			if ( i >= _hashLinkCount )
				i -= _hashLinkCount;
		}

		_hashLinks[i] = (short)key;
	}

	public void HashSearch( int sourceNode, int destinationNode, out int key )
	{
		var i = LinkHashStart( sourceNode, destinationNode, out var step );

		while ( _hashLinks[i] != EntryStateEmpty )
		{
			var link = LinkPool[_hashLinks[i]];

			if ( sourceNode == link.SourceNode && destinationNode == link.DestinationNode )
				break;

			i += step;

			if ( i >= _hashLinkCount )
				i -= _hashLinkCount;
		}

		key = _hashLinks[i];
	}

	private void HashChoosePrimes( int tableSize )
	{
		var largestPrime = Math.Min( tableSize / 2, Primes[^2] );
		var spacing = largestPrime / 16;

		for ( int zone = 1, prime = 0; prime < 16; zone += spacing )
		{
			var lower = Primes[0];

			for ( var j = 0; Primes[j] != 0; j++ )
			{
				if ( j != 0 && tableSize % Primes[j] == 0 )
					continue;

				var upper = Primes[j];

				if ( lower <= zone && zone <= upper )
				{
					_hashPrimes[prime++] = zone - lower <= upper - zone ? lower : upper;
					break;
				}

				lower = upper;
			}
		}

		for ( var prime = 0; prime < 16; prime += 2 )
			_hashPrimes[prime] = tableSize - _hashPrimes[prime];

		for ( var prime = 0; prime < 16 - 1; prime++ )
		{
			var pick = Server.RandomLong( 0, 15 - prime );

			(_hashPrimes[pick], _hashPrimes[15 - prime]) = (_hashPrimes[15 - prime], _hashPrimes[pick]);
		}
	}

	public void SortNodes()
	{
		var count = 0;

		Nodes[0].PreviousNode = count++;

		for ( var i = 1; i < NodeCount; i++ )
			Nodes[i].PreviousNode = UnnumberedNode;

		for ( var i = 0; i < NodeCount; i++ )
		{
			for ( var j = 0; j < Nodes[i].NumLinks; j++ )
			{
				var destination = Nodes[NodeLink( i, j ).DestinationNode];

				if ( destination.PreviousNode == UnnumberedNode )
					destination.PreviousNode = count++;
			}
		}

		for ( var i = 0; i < NodeCount; i++ )
		{
			if ( Nodes[i].PreviousNode == UnnumberedNode )
				Nodes[i].PreviousNode = count++;
		}

		for ( var i = 0; i < LinkCount; i++ )
		{
			LinkPool[i].SourceNode = Nodes[LinkPool[i].SourceNode].PreviousNode;
			LinkPool[i].DestinationNode = Nodes[LinkPool[i].DestinationNode].PreviousNode;
		}

		for ( var i = 0; i < NodeCount; i++ )
		{
			while ( Nodes[i].PreviousNode != i )
			{
				var destination = Nodes[i].PreviousNode;

				(Nodes[destination], Nodes[i]) = (Nodes[i], Nodes[destination]);
			}
		}
	}

	public void BuildLinkLookups()
	{
		_hashLinkCount = (3 * LinkCount / 2) + 3;

		HashChoosePrimes( _hashLinkCount );

		_hashLinks = new short[_hashLinkCount];

		Array.Fill( _hashLinks, (short)EntryStateEmpty );

		for ( var i = 0; i < LinkCount; i++ )
			HashInsert( LinkPool[i].SourceNode, LinkPool[i].DestinationNode, i );
	}

	private int RegionCode( int node, int axis )
	{
		var region = Nodes[node].Region;

		return (region[axis] << 16) + (region[(axis + 1) % 3] << 8) + region[(axis + 2) % 3];
	}

	public void BuildRegionTables()
	{
		_sortedBy = new int[NodeCount, 3];
		_checkedEvent = new int[NodeCount];

		for ( var axis = 0; axis < 3; axis++ )
		{
			_regionMin[axis] = 999999999f;
			_regionMax[axis] = -999999999f;
		}

		for ( var i = 0; i < NodeCount; i++ )
		{
			for ( var axis = 0; axis < 3; axis++ )
			{
				if ( Nodes[i].Origin[axis] < _regionMin[axis] )
					_regionMin[axis] = Nodes[i].Origin[axis];

				if ( Nodes[i].Origin[axis] > _regionMax[axis] )
					_regionMax[axis] = Nodes[i].Origin[axis];
			}
		}

		for ( var i = 0; i < NodeCount; i++ )
		{
			for ( var axis = 0; axis < 3; axis++ )
				Nodes[i].Region[axis] = (byte)CalcRange( Nodes[i].Origin[axis], axis );
		}

		for ( var axis = 0; axis < 3; axis++ )
		{
			for ( var j = 0; j < NumRanges; j++ )
			{
				_rangeStart[axis, j] = 255;
				_rangeEnd[axis, j] = 0;
			}

			for ( var j = 0; j < NodeCount; j++ )
				_sortedBy[j, axis] = j;

			for ( var j = 0; j < NodeCount - 1; j++ )
			{
				var code = RegionCode( _sortedBy[j, axis], axis );

				for ( var k = j + 1; k < NodeCount; k++ )
				{
					if ( RegionCode( _sortedBy[k, axis], axis ) < code )
						(_sortedBy[j, axis], _sortedBy[k, axis]) = (_sortedBy[k, axis], _sortedBy[j, axis]);
				}
			}
		}

		for ( var i = 0; i < NodeCount; i++ )
		{
			for ( var axis = 0; axis < 3; axis++ )
			{
				int code = Nodes[_sortedBy[i, axis]].Region[axis];

				if ( i < _rangeStart[axis, code] )
					_rangeStart[axis, code] = i;

				if ( _rangeEnd[axis, code] < i )
					_rangeEnd[axis, code] = i;
			}
		}

		Array.Clear( _cache );
	}
}
