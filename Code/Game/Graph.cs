using System;
using GoldSrc;

namespace HalfLife;

public sealed class GraphNode
{
	public const int Land = 1 << 0;
	public const int Air = 1 << 1;
	public const int Water = 1 << 2;
	public const int GroupRealm = Land | Air | Water;

	public Vector3 Origin;
	public Vector3 OriginPeek;
	public readonly byte[] Region = new byte[3];
	public int NodeInfo;
	public int NumLinks;
	public int FirstLink;
	public readonly int[,] NextBestNode = new int[Graph.MaxNodeHulls, 2];
	public float ClosestSoFar;
	public int PreviousNode;
	public short HintType;
	public short HintActivity;
	public float HintYaw;
}

public struct GraphLink
{
	public const int SmallHull = 1 << 0;
	public const int HumanHull = 1 << 1;
	public const int LargeHull = 1 << 2;
	public const int FlyHull = 1 << 3;
	public const int Disabled = 1 << 4;

	public int SourceNode;
	public int DestinationNode;
	public BaseEntity LinkEntity;
	public string LinkEntityModelName;
	public int LinkInfo;
	public float Weight;
}

public static class Hint
{
	public const short None = 0;
	public const short WorldDoor = 1;
	public const short WorldWindow = 2;
	public const short WorldButton = 3;
	public const short WorldMachinery = 4;
	public const short WorldLedge = 5;
	public const short WorldLightSource = 6;
	public const short WorldHeatSource = 7;
	public const short WorldBlinkingLight = 8;
	public const short WorldBrightColors = 9;
	public const short WorldHumanBlood = 10;
	public const short WorldAlienBlood = 11;
	public const short TacticalExit = 100;
	public const short TacticalVantage = 101;
	public const short TacticalAmbush = 102;
	public const short StukaPerch = 300;
	public const short StukaLanding = 301;
}

public sealed partial class Graph
{
	public const int NoNode = -1;
	public const int MaxNodeHulls = 4;
	public const int MaxPathSize = 10;
	public const int Version = 16;

	public const int NodeSmallHull = 0;
	public const int NodeHumanHull = 1;
	public const int NodeLargeHull = 2;
	public const int NodeFlyHull = 3;

	public const int MaxNodes = 1024;
	public const int MaxNodeInitialLinks = 128;
	public const int HullStepSize = 16;
	private const int CacheSize = 128;
	private const int NumRanges = 256;
	public const int NodeHeight = 8;

	public enum QueryType
	{
		Dynamic,
		Static
	}

	private struct CacheEntry
	{
		public Vector3 Origin;
		public short Node;
	}

	public bool GraphPresent;
	public bool GraphPointersSet;
	public bool RoutingComplete;

	public GraphNode[] Nodes;
	public GraphLink[] LinkPool;
	public int NodeCount;
	public int LinkCount;
	public int LastActiveIdleSearch;
	public int LastCoverSearch;

	private sbyte[] _routeInfo;
	private int _routeInfoSize;
	private int[,] _sortedBy;
	private int[] _checkedEvent;
	private bool[] _linkEntitySaved;
	private readonly int[,] _rangeStart = new int[3, NumRanges];
	private readonly int[,] _rangeEnd = new int[3, NumRanges];
	private float _shortest;
	private int _nearest;
	private readonly int[] _min = new int[3];
	private readonly int[] _max = new int[3];
	private readonly int[] _minBox = new int[3];
	private readonly int[] _maxBox = new int[3];
	private int _checkedCounter;
	private readonly float[] _regionMin = new float[3];
	private readonly float[] _regionMax = new float[3];
	private readonly CacheEntry[] _cache = new CacheEntry[CacheSize];
	private readonly int[] _hashPrimes = new int[16];
	private short[] _hashLinks;
	private int _hashLinkCount;

	public void InitGraph()
	{
		GraphPresent = false;
		GraphPointersSet = false;
		RoutingComplete = false;

		LinkPool = null;
		Nodes = null;
		_sortedBy = null;
		_checkedEvent = null;
		_linkEntitySaved = null;
		_routeInfo = null;
		_hashLinks = null;

		NodeCount = 0;
		LinkCount = 0;
		_routeInfoSize = 0;

		LastActiveIdleSearch = 0;
		LastCoverSearch = 0;
	}

	public void AllocNodes()
	{
		Nodes = new GraphNode[MaxNodes];

		for ( var i = 0; i < MaxNodes; i++ )
			Nodes[i] = new GraphNode();
	}

	public static int CapIndex( int capabilities )
	{
		return (capabilities & (BaseMonster.CapabilityOpenDoors | BaseMonster.CapabilityAutoDoors | BaseMonster.CapabilityUse)) != 0 ? 1 : 0;
	}

	private GraphLink NodeLink( int node, int link ) => LinkPool[Nodes[node].FirstLink + link];

	private static bool IsDoor( BaseEntity entity ) => entity.ClassName is "func_door" or "func_door_rotating";

	public BaseEntity LinkEntForLink( GraphLink link, GraphNode node )
	{
		if ( link.LinkEntity is not { } linkEntity )
			return null;

		if ( !IsDoor( linkEntity ) )
		{
			Util.Alert( AlertType.AiConsole, $"Unsupported PathEnt:\n'{linkEntity.ClassName}'" );
			return null;
		}

		if ( (linkEntity.SpawnFlags & BaseDoor.SpawnFlagUseOnly) != 0 )
			return linkEntity;

		var server = linkEntity.Server;
		Edict search = null;

		while ( true )
		{
			var trigger = server.FindEntityByString( search, "target", linkEntity.TargetName );

			if ( Util.IsNullEnt( trigger ) )
				return linkEntity;

			search = trigger;

			if ( trigger.ClassName is not ("func_button" or "func_rot_button") )
				continue;

			var trace = Util.TraceLine( node.Origin, ((BaseEntity)trigger).BModelOrigin, BaseEntity.IgnoreMonsters, HalfLifeDll.BodyQueueHead );

			if ( trace.Ent == trigger )
				return (BaseEntity)trace.Ent;
		}
	}

	public bool HandleLinkEnt( int node, BaseEntity linkEntity, int capabilities, QueryType queryType )
	{
		if ( !GraphPresent || !GraphPointersSet )
		{
			Util.Alert( AlertType.AiConsole, "Graph not ready!" );
			return false;
		}

		if ( Util.IsNullEnt( linkEntity ) )
		{
			Util.Alert( AlertType.AiConsole, "dead path ent!" );
			return true;
		}

		if ( IsDoor( linkEntity ) )
		{
			var stayOpen = linkEntity.GetToggleState() == ToggleState.AtTop && (linkEntity.SpawnFlags & BaseDoor.SpawnFlagNoAutoReturn) != 0;
			var canOpen = (capabilities & BaseMonster.CapabilityOpenDoors) != 0;

			if ( (linkEntity.SpawnFlags & BaseDoor.SpawnFlagUseOnly) != 0 )
				return canOpen || stayOpen;

			if ( stayOpen )
				return true;

			return canOpen && ((linkEntity.SpawnFlags & BaseDoor.SpawnFlagNoMonsters) == 0 || queryType == QueryType.Static);
		}

		if ( linkEntity.ClassName == "func_breakable" && queryType == QueryType.Static )
			return true;

		Util.Alert( AlertType.AiConsole, $"Unhandled Ent in Path {linkEntity.ClassName}" );

		return false;
	}

	public int HullIndex( BaseEntity entity )
	{
		if ( entity.MoveType == MoveType.Fly )
			return NodeFlyHull;

		if ( entity.Mins == new Vector3( -12f, -12f, 0f ) )
			return NodeSmallHull;

		if ( entity.Mins == BaseEntity.HumanHullMin )
			return NodeHumanHull;

		return entity.Mins == new Vector3( -32f, -32f, 0f ) ? NodeLargeHull : NodeHumanHull;
	}

	public int NodeType( BaseEntity entity )
	{
		if ( entity.MoveType != MoveType.Fly )
			return GraphNode.Land;

		return entity.WaterLevel != 0 ? GraphNode.Water : GraphNode.Air;
	}

	public float PathLength( int start, int destination, int hull, int capabilities )
	{
		var distance = 0f;
		var maxLoop = NodeCount;
		var current = start;
		var cap = CapIndex( capabilities );

		while ( current != destination )
		{
			if ( maxLoop-- <= 0 )
			{
				Util.Alert( AlertType.Console, "Route Failure" );
				return 0f;
			}

			var next = NextNodeInRoute( current, destination, hull, cap );

			if ( current == next )
				return 0f;

			HashSearch( current, next, out var link );

			if ( link < 0 )
			{
				Util.Alert( AlertType.Console, $"HashLinks is broken from {current} to {destination}." );
				return 0f;
			}

			distance += LinkPool[link].Weight;
			current = next;
		}

		return distance;
	}

	public int NextNodeInRoute( int current, int destination, int hull, int cap )
	{
		var next = current;
		var count = destination + 1;
		var route = Nodes[current].NextBestNode[hull, cap];

		while ( count > 0 )
		{
			var value = _routeInfo[route++];

			if ( value < 0 )
			{
				value = (sbyte)-value;

				if ( count <= value )
				{
					next = destination;
					count = 0;
				}
				else
				{
					count -= value;
				}
			}
			else
			{
				if ( count <= value + 1 )
				{
					next = current + _routeInfo[route];

					if ( next >= NodeCount )
						next -= NodeCount;
					else if ( next < 0 )
						next += NodeCount;

					count = 0;
				}
				else
				{
					count = count - value - 1;
				}

				route++;
			}
		}

		return next;
	}

	private static int HullMask( int hull ) => hull switch
	{
		NodeSmallHull => GraphLink.SmallHull,
		NodeHumanHull => GraphLink.HumanHull,
		NodeLargeHull => GraphLink.LargeHull,
		NodeFlyHull => GraphLink.FlyHull,
		_ => 0
	};

	public int FindShortestPath( int[] path, int start, int destination, int hull, int capabilities )
	{
		if ( !GraphPresent || !GraphPointersSet )
		{
			Util.Alert( AlertType.AiConsole, "Graph not ready!" );
			return 0;
		}

		if ( start < 0 || start > NodeCount )
		{
			Util.Alert( AlertType.AiConsole, $"Can't build a path, iStart is {start}!" );
			return 0;
		}

		if ( start == destination )
		{
			path[0] = start;
			path[1] = destination;
			return 2;
		}

		int pathNodes;
		int current;

		if ( RoutingComplete )
		{
			var cap = CapIndex( capabilities );

			pathNodes = 0;
			path[pathNodes++] = start;
			current = start;

			while ( current != destination )
			{
				var next = NextNodeInRoute( current, destination, hull, cap );

				if ( current == next )
					return 0;

				if ( pathNodes >= MaxPathSize )
					break;

				path[pathNodes++] = next;
				current = next;
			}

			return pathNodes;
		}

		var queue = new PriorityQueue();
		var hullMask = HullMask( hull );

		for ( var i = 0; i < NodeCount; i++ )
			Nodes[i].ClosestSoFar = -1f;

		Nodes[start].ClosestSoFar = 0f;
		Nodes[start].PreviousNode = start;

		queue.Insert( start, 0f );

		while ( !queue.Empty )
		{
			current = queue.Remove( out var currentDistance );

			if ( current == destination )
				break;

			var currentNode = Nodes[current];

			for ( var i = 0; i < currentNode.NumLinks; i++ )
			{
				var link = LinkPool[currentNode.FirstLink + i];
				var visit = link.DestinationNode;

				if ( (link.LinkInfo & hullMask) != hullMask )
					continue;

				if ( link.LinkEntity is not null && !HandleLinkEnt( current, link.LinkEntity, capabilities, QueryType.Static ) )
					continue;

				var ourDistance = currentDistance + link.Weight;

				if ( Nodes[visit].ClosestSoFar < -0.5 || ourDistance < Nodes[visit].ClosestSoFar - 0.001 )
				{
					Nodes[visit].ClosestSoFar = ourDistance;
					Nodes[visit].PreviousNode = current;

					queue.Insert( visit, ourDistance );
				}
			}
		}

		if ( Nodes[destination].ClosestSoFar < -0.5 )
			return 0;

		current = destination;
		pathNodes = 1;

		while ( current != start )
		{
			pathNodes++;
			current = Nodes[current].PreviousNode;
		}

		current = destination;

		for ( var i = pathNodes - 1; i >= 0; i-- )
		{
			path[i] = current;
			current = Nodes[current].PreviousNode;
		}

		return pathNodes;
	}

	private static void CalcBounds( out int lower, out int upper, int goal, int best )
	{
		var temp = (2 * goal) - best;

		if ( best > goal )
		{
			lower = Math.Max( 0, temp );
			upper = best;
		}
		else
		{
			upper = Math.Min( 255, temp );
			lower = best;
		}
	}

	private static int CalcRange( int x, int lower, int upper ) => NumRanges * (x - lower) / (upper - lower + 1);

	private int CalcRange( float value, int axis ) => CalcRange( (int)value, (int)_regionMin[axis], (int)_regionMax[axis] );

	private static void UpdateRange( ref int minValue, ref int maxValue, int goal, int best )
	{
		CalcBounds( out var lower, out var upper, goal, best );

		if ( upper < maxValue )
			maxValue = upper;

		if ( minValue < lower )
			minValue = lower;
	}

	private void CheckNode( Vector3 origin, int node )
	{
		if ( _checkedEvent[node] == _checkedCounter )
			return;

		_checkedEvent[node] = _checkedCounter;

		var distance = MathLib.Length( origin - Nodes[node].OriginPeek );

		if ( distance >= _shortest )
			return;

		var trace = Util.TraceLine( origin, Nodes[node].OriginPeek, BaseEntity.IgnoreMonsters, null );

		if ( trace.Fraction != 1f )
			return;

		_nearest = node;
		_shortest = distance;

		for ( var axis = 0; axis < 3; axis++ )
		{
			UpdateRange( ref _min[axis], ref _max[axis], CalcRange( origin[axis], axis ), Nodes[node].Region[axis] );

			_minBox[axis] = CalcRange( origin[axis] - distance, axis );
			_maxBox[axis] = CalcRange( origin[axis] + distance, axis );
		}
	}

	public int FindNearestNode( Vector3 origin, BaseEntity entity ) => FindNearestNode( origin, NodeType( entity ) );

	private void CheckRange( Vector3 origin, int nodeTypes, int axis, int range )
	{
		var second = (axis + 1) % 3;
		var third = (axis + 2) % 3;

		for ( var j = _rangeStart[axis, range]; j <= _rangeEnd[axis, range]; j++ )
		{
			var index = _sortedBy[j, axis];
			var node = Nodes[index];

			if ( (node.NodeInfo & nodeTypes) == 0 )
				continue;

			if ( node.Region[second] > _maxBox[second] )
				break;

			if ( node.Region[second] < _minBox[second] )
				continue;

			if ( node.Region[third] < _minBox[third] || node.Region[third] > _maxBox[third] )
				continue;

			CheckNode( origin, index );
		}
	}

	public int FindNearestNode( Vector3 origin, int nodeTypes )
	{
		if ( !GraphPresent || !GraphPointersSet )
		{
			Util.Alert( AlertType.AiConsole, "Graph not ready!" );
			return -1;
		}

		var hash = (int)((CacheSize - 1) & Crc32.Compute( origin ));

		if ( _cache[hash].Origin == origin )
			return _cache[hash].Node;

		_checkedCounter++;

		if ( _checkedCounter == 0 )
		{
			for ( var i = 0; i < NodeCount; i++ )
				_checkedEvent[i] = 0;

			_checkedCounter++;
		}

		_nearest = -1;
		_shortest = 999999f;

		for ( var axis = 0; axis < 3; axis++ )
		{
			_min[axis] = 0;
			_max[axis] = 255;
			_minBox[axis] = 0;
			_maxBox[axis] = 255;
		}

		var halfX = (_min[0] + _max[0]) / 2;
		var halfY = (_min[1] + _max[1]) / 2;
		var halfZ = (_min[2] + _max[2]) / 2;

		for ( var i = halfX; i >= _min[0]; i-- )
			CheckRange( origin, nodeTypes, 0, i );

		for ( var i = Math.Max( _min[1], halfY + 1 ); i <= _max[1]; i++ )
			CheckRange( origin, nodeTypes, 1, i );

		for ( var i = Math.Min( _max[2], halfZ ); i >= _min[2]; i-- )
			CheckRange( origin, nodeTypes, 2, i );

		for ( var i = Math.Max( _min[0], halfX + 1 ); i <= _max[0]; i++ )
			CheckRange( origin, nodeTypes, 0, i );

		for ( var i = Math.Min( _max[1], halfY ); i >= _min[1]; i-- )
			CheckRange( origin, nodeTypes, 1, i );

		for ( var i = Math.Max( _min[2], halfZ + 1 ); i <= _max[2]; i++ )
			CheckRange( origin, nodeTypes, 2, i );

		_cache[hash].Origin = origin;
		_cache[hash].Node = (short)_nearest;

		return _nearest;
	}

	public void ShowNodeConnections( int node )
	{
		if ( !GraphPresent || !GraphPointersSet )
		{
			Util.Alert( AlertType.AiConsole, "Graph not ready!" );
			return;
		}

		if ( node < 0 )
		{
			Util.Alert( AlertType.AiConsole, $"Can't show connections for node {node}" );
			return;
		}

		var source = Nodes[node];
		var server = Server.Current;

		Util.ParticleEffect( source.Origin, Vector3.Zero, 255, 20 );

		if ( source.NumLinks <= 0 )
			Util.Alert( AlertType.AiConsole, "**No Connections!" );

		for ( var i = 0; i < source.NumLinks; i++ )
		{
			var spot = Nodes[NodeLink( node, i ).DestinationNode].Origin;

			server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
			server.WriteByte( TempEntity.ShowLine );
			server.WriteCoord( source.Origin.x );
			server.WriteCoord( source.Origin.y );
			server.WriteCoord( source.Origin.z + NodeHeight );
			server.WriteCoord( spot.x );
			server.WriteCoord( spot.y );
			server.WriteCoord( spot.z + NodeHeight );
			server.MessageEnd();
		}
	}
}
