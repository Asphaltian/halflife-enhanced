using System;
using GoldSrc;

namespace HalfLife;

partial class Graph
{
	private sealed class PriorityQueue
	{
		private const int MaxStackNodes = 100;

		private readonly (int Id, float Priority)[] _heap = new (int, float)[MaxStackNodes];
		private int _size;

		public bool Full => _size == MaxStackNodes;
		public bool Empty => _size == 0;

		public void Insert( int value, float priority )
		{
			if ( Full )
			{
				Log.Info( "Queue is full!" );
				return;
			}

			_heap[_size] = (value, priority);
			_size++;

			SiftUp();
		}

		public int Remove( out float priority )
		{
			var value = _heap[0].Id;

			priority = _heap[0].Priority;

			_size--;
			_heap[0] = _heap[_size];

			SiftDown( 0 );

			return value;
		}

		private void SiftDown( int subRoot )
		{
			var parent = subRoot;
			var child = (2 * parent) + 1;
			var reference = _heap[parent];

			while ( child < _size )
			{
				var rightChild = (2 * parent) + 2;

				if ( rightChild < _size && _heap[rightChild].Priority < _heap[child].Priority )
					child = rightChild;

				if ( reference.Priority <= _heap[child].Priority )
					break;

				_heap[parent] = _heap[child];
				parent = child;
				child = (2 * parent) + 1;
			}

			_heap[parent] = reference;
		}

		private void SiftUp()
		{
			var child = _size - 1;

			while ( child != 0 )
			{
				var parent = (child - 1) / 2;

				if ( _heap[parent].Priority <= _heap[child].Priority )
					break;

				(_heap[child], _heap[parent]) = (_heap[parent], _heap[child]);

				child = parent;
			}
		}
	}

	private void WriteRouteOffset( sbyte[] route, ref int length, int lastNode, int from )
	{
		var a = lastNode - from;
		var b = lastNode - from + NodeCount;
		var c = lastNode - from - NodeCount;

		if ( a is >= -128 and <= 127 )
			route[length++] = (sbyte)a;
		else if ( b is >= -128 and <= 127 )
			route[length++] = (sbyte)b;
		else if ( c is >= -128 and <= 127 )
			route[length++] = (sbyte)c;
		else
			Util.AiAlert( $"Nodes need sorting ({lastNode},{from})!" );
	}

	private int CompressRoute( ushort[] bestNextNodes, sbyte[] route, int from, out int compressedSize )
	{
		var lastNode = 9999999;
		var sequence = 0;
		var repeats = 0;
		var length = 0;

		compressedSize = 0;

		for ( var i = 0; i < NodeCount; i++ )
		{
			var canRepeat = bestNextNodes[i] == lastNode && repeats < 127;
			var canSequence = bestNextNodes[i] == i && sequence < 128;

			if ( repeats != 0 )
			{
				if ( canRepeat )
				{
					repeats++;
				}
				else
				{
					compressedSize += 2;
					route[length++] = (sbyte)(repeats - 1);
					WriteRouteOffset( route, ref length, lastNode, from );
					repeats = 0;

					if ( canSequence )
						sequence++;
					else
						repeats++;
				}
			}
			else if ( sequence != 0 )
			{
				if ( canSequence )
				{
					sequence++;
				}
				else if ( sequence == 1 && canRepeat )
				{
					repeats = 2;
					sequence = 0;
				}
				else
				{
					compressedSize += 1;
					route[length++] = (sbyte)-sequence;
					sequence = 0;
					repeats++;
				}
			}
			else if ( canSequence )
			{
				sequence++;
			}
			else
			{
				repeats++;
			}

			lastNode = bestNextNodes[i];
		}

		if ( repeats != 0 )
		{
			compressedSize += 2;
			route[length++] = (sbyte)(repeats - 1);
			WriteRouteOffset( route, ref length, lastNode, from );
		}

		if ( sequence != 0 )
		{
			compressedSize += 1;
			route[length++] = (sbyte)-sequence;
		}

		return length;
	}

	private int StoreRoute( ReadOnlySpan<sbyte> route, int compressedSize, ref int totalCompressedSize )
	{
		if ( _routeInfo is null )
		{
			_routeInfoSize = route.Length;
			_routeInfo = route.ToArray();
			totalCompressedSize += compressedSize;

			return 0;
		}

		for ( var i = 0; i < _routeInfoSize - route.Length; i++ )
		{
			if ( _routeInfo.AsSpan( i, route.Length ).SequenceEqual( route ) )
				return i;
		}

		var offset = _routeInfoSize;

		Array.Resize( ref _routeInfo, _routeInfoSize + route.Length );
		route.CopyTo( _routeInfo.AsSpan( offset ) );

		_routeInfoSize += route.Length;
		totalCompressedSize += compressedSize;

		return offset;
	}

	public void ComputeStaticRoutingTables()
	{
		var routes = new short[NodeCount * NodeCount];
		var path = new int[Math.Max( NodeCount, 2 )];
		var bestNextNodes = new ushort[NodeCount];
		var route = new sbyte[NodeCount * 2];
		var totalCompressedSize = 0;

		for ( var hull = 0; hull < MaxNodeHulls; hull++ )
		{
			for ( var cap = 0; cap < 2; cap++ )
			{
				var capMask = cap == 0 ? 0 : BaseMonster.CapabilityOpenDoors | BaseMonster.CapabilityAutoDoors | BaseMonster.CapabilityUse;

				Array.Fill( routes, (short)-1 );

				for ( var from = 0; from < NodeCount; from++ )
				{
					for ( var to = NodeCount - 1; to >= 0; to-- )
					{
						if ( routes[(from * NodeCount) + to] != -1 )
							continue;

						var pathSize = FindShortestPath( path, from, to, hull, capMask );

						if ( pathSize > 1 )
						{
							for ( var node = 0; node < pathSize - 1; node++ )
							{
								var start = path[node];
								var next = path[node + 1];

								for ( var node1 = node + 1; node1 < pathSize; node1++ )
									routes[(start * NodeCount) + path[node1]] = (short)next;
							}
						}
						else
						{
							routes[(from * NodeCount) + to] = (short)from;
							routes[(to * NodeCount) + from] = (short)to;
						}
					}
				}

				for ( var from = 0; from < NodeCount; from++ )
				{
					for ( var to = 0; to < NodeCount; to++ )
						bestNextNodes[to] = (ushort)routes[(from * NodeCount) + to];

					var length = CompressRoute( bestNextNodes, route, from, out var compressedSize );

					Nodes[from].NextBestNode[hull, cap] = StoreRoute( route.AsSpan( 0, length ), compressedSize, ref totalCompressedSize );
				}
			}
		}

		Util.AiAlert( $"Size of Routes = {totalCompressedSize}" );

		RoutingComplete = true;
	}
}
