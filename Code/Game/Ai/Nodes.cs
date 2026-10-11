using System.Text;
using GoldSrc;
using GoldSrc.Bsp;

namespace HalfLife;

[LinkEntityToClass( "info_node" )]
[LinkEntityToClass( "info_node_air" )]
public class NodeEnt : BaseEntity
{
	private short _hintType;
	private short _hintActivity;

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override bool KeyValue( string key, string value )
	{
		if ( key == "hinttype" )
			_hintType = (short)Util.Atoi( value );

		if ( key == "activity" )
		{
			_hintActivity = (short)Util.Atoi( value );
			return true;
		}

		return base.KeyValue( key, value );
	}

	public override void Spawn()
	{
		MoveType = MoveType.None;
		Solid = Solid.Not;

		var graph = HalfLifeDll.WorldGraph;

		if ( graph.GraphPresent )
		{
			Server.FreeEdict( this );
			return;
		}

		if ( graph.NodeCount == 0 )
			Server.CreateEntity<TestHull>().Spawn( this );

		if ( graph.NodeCount >= Graph.MaxNodes )
		{
			Util.Alert( AlertType.AiConsole, "cNodes > MAX_NODES\n" );
			return;
		}

		var node = graph.Nodes[graph.NodeCount];

		node.Origin = Origin;
		node.OriginPeek = Origin;
		node.HintYaw = Angles.y;
		node.HintType = _hintType;
		node.HintActivity = _hintActivity;
		node.NodeInfo = ClassName == "info_node_air" ? GraphNode.Air : 0;

		graph.NodeCount++;

		Server.FreeEdict( this );
	}
}

[LinkEntityToClass( "testhull" )]
public class TestHull : BaseMonster
{
	private const string ReportRule = "-------------------------------------------------------------------------------";

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public void Spawn( BaseEntity masterNode )
	{
		SetModel( "models/player.mdl" );
		SetSize( HumanHullMin, HumanHullMax );

		Solid = Solid.SlideBox;
		MoveType = MoveType.Step;
		Effects = 0;
		Health = 50f;
		YawSpeed = 8f;

		if ( HalfLifeDll.WorldGraph.GraphPresent )
		{
			SetThink( SubRemove );
			NextThink = (float)Server.Globals.Time;
		}
		else
		{
			SetThink( DropDelay );
			NextThink = (float)(Server.Globals.Time + 1.0);
		}

		RenderMode = RenderMode.TransTexture;
		RenderAmt = 0f;
	}

	public void DropDelay()
	{
		SetOrigin( HalfLifeDll.WorldGraph.Nodes[0].Origin );
		SetThink( CallBuildNodeGraph );

		NextThink = (float)(Server.Globals.Time + 1.0);
	}

	public void ShowBadNode()
	{
		MoveType = MoveType.Fly;
		Angles = Angles with { y = Angles.y + 4f };

		Server.Globals.MakeVectors( Angles );

		var forward = Server.Globals.Forward * 64f;
		var right = Server.Globals.Right * 64f;

		Util.ParticleEffect( Origin, Vector3.Zero, 255, 25 );
		Util.ParticleEffect( Origin + forward, Vector3.Zero, 255, 25 );
		Util.ParticleEffect( Origin - forward, Vector3.Zero, 255, 25 );
		Util.ParticleEffect( Origin + right, Vector3.Zero, 255, 25 );
		Util.ParticleEffect( Origin - right, Vector3.Zero, 255, 25 );

		NextThink = (float)(Server.Globals.Time + 0.1);
	}

	public void CallBuildNodeGraph()
	{
		HalfLifeDll.TouchDisabled = true;
		BuildNodeGraph();
		HalfLifeDll.TouchDisabled = false;
	}

	private void BuildNodeGraph()
	{
		var report = new StringBuilder();

		SetThink( SubRemove );
		NextThink = (float)Server.Globals.Time;

		BuildNodeGraph( report );

		FileSystem.Data.CreateDirectory( "maps/graphs" );
		FileSystem.Data.WriteAllText( Graph.ReportFileName( Server.Globals.MapName ), report.ToString() );
	}

	private static void DropNodes( Graph graph )
	{
		for ( var i = 0; i < graph.NodeCount; i++ )
		{
			var node = graph.Nodes[i];

			if ( (node.NodeInfo & GraphNode.Air) != 0 )
				continue;

			if ( Util.PointContents( node.Origin ) == (int)Contents.Water )
			{
				node.NodeInfo |= GraphNode.Water;
				continue;
			}

			node.NodeInfo |= GraphNode.Land;

			var floor = node.Origin - new Vector3( 0f, 0f, 384f );
			var trace = Util.TraceLine( node.Origin, floor, IgnoreMonsters, HalfLifeDll.BodyQueueHead );
			var entityTrace = Util.TraceLine( node.Origin, floor, DontIgnoreMonsters, HalfLifeDll.BodyQueueHead );

			if ( entityTrace.Fraction < trace.Fraction && entityTrace.Ent is { } hit && (hit.EntityFlags & EntityFlags.WorldBrush) == EntityFlags.WorldBrush )
				trace.EndPos = entityTrace.EndPos;

			node.Origin.z = trace.EndPos.z + Graph.NodeHeight;
			node.OriginPeek.z = node.Origin.z;
		}
	}

	private bool WalkLink( GraphNode source, GraphNode destination, out int step )
	{
		var saveFlags = EntityFlags;
		var moveMode = WalkMoveMode.WorldOnly;

		if ( (source.NodeInfo & GraphNode.Water) != 0 )
		{
			EntityFlags |= EntityFlags.Swim;
			moveMode = WalkMoveMode.Normal;
		}

		var spot = destination.Origin;
		var yaw = MathLib.VecToYaw( destination.Origin - Origin );
		var distance = MathLib.Length2D( spot - Origin );
		var walked = true;

		for ( step = 0; step < distance; step += Graph.HullStepSize )
		{
			float stepSize = Graph.HullStepSize;

			if ( step + stepSize >= distance - 1f )
				stepSize = distance - step - 1f;

			if ( !Server.WalkMove( this, yaw, stepSize, moveMode ) )
			{
				walked = false;
				break;
			}
		}

		if ( walked && MathLib.Length( Origin - spot ) > 64f )
			walked = false;

		EntityFlags = saveFlags;

		return walked;
	}

	private void TestLinkHulls( GraphNode source, ref GraphLink link, StringBuilder report )
	{
		var destination = HalfLifeDll.WorldGraph.Nodes[link.DestinationNode];
		var skipRemainingHulls = false;

		link.LinkInfo = GraphLink.SmallHull | GraphLink.HumanHull | GraphLink.LargeHull | GraphLink.FlyHull;

		for ( var hull = 0; hull < Graph.MaxNodeHulls; hull++ )
		{
			if ( skipRemainingHulls && hull is Graph.NodeHumanHull or Graph.NodeLargeHull )
				continue;

			switch ( hull )
			{
				case Graph.NodeSmallHull:
					SetSize( new Vector3( -12f, -12f, 0f ), new Vector3( 12f, 12f, 24f ) );
					break;
				case Graph.NodeHumanHull:
					SetSize( HumanHullMin, HumanHullMax );
					break;
				case Graph.NodeLargeHull:
				case Graph.NodeFlyHull:
					SetSize( new Vector3( -32f, -32f, 0f ), new Vector3( 32f, 32f, 64f ) );
					break;
			}

			SetOrigin( source.Origin );

			if ( (EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround )
				Util.Alert( AlertType.AiConsole, "OFFGROUND!\n" );

			if ( hull == Graph.NodeFlyHull )
			{
				var lift = new Vector3( 0f, 0f, 32f );
				var trace = Util.TraceHull( source.Origin + lift, destination.OriginPeek + lift, IgnoreMonsters, LargeHull, this );

				if ( trace.StartSolid || trace.Fraction < 1.0 )
					link.LinkInfo &= ~GraphLink.FlyHull;

				continue;
			}

			if ( WalkLink( source, destination, out var step ) )
				continue;

			switch ( hull )
			{
				case Graph.NodeSmallHull:
					report.Append( $"NODE_SMALL_HULL step {step:F6}\n" );
					link.LinkInfo &= ~(GraphLink.SmallHull | GraphLink.HumanHull | GraphLink.LargeHull);
					skipRemainingHulls = true;
					break;
				case Graph.NodeHumanHull:
					report.Append( $"NODE_HUMAN_HULL step {step:F6}\n" );
					link.LinkInfo &= ~(GraphLink.HumanHull | GraphLink.LargeHull);
					skipRemainingHulls = true;
					break;
				case Graph.NodeLargeHull:
					report.Append( $"NODE_LARGE_HULL step {step:F6}\n" );
					link.LinkInfo &= ~GraphLink.LargeHull;
					break;
			}
		}
	}

	private void BuildNodeGraph( StringBuilder report )
	{
		var graph = HalfLifeDll.WorldGraph;
		var tempPool = new GraphLink[graph.NodeCount * Graph.MaxNodeInitialLinks];

		report.Append( $"Node Graph Report for map:  {Server.Globals.MapName}.bsp\n" );
		report.Append( $"{graph.NodeCount} Total Nodes\n\n" );

		for ( var i = 0; i < graph.NodeCount; i++ )
		{
			var node = graph.Nodes[i];

			node.NumLinks = 0;
			node.FirstLink = 0;

			System.Array.Clear( node.NextBestNode );

			report.Append( $"Node#         {i,4}\n" );
			report.Append( $"Location      {(int)node.Origin.x,4},{(int)node.Origin.y,4},{(int)node.Origin.z,4}\n" );
			report.Append( $"HintType:     {node.HintType,4}\n" );
			report.Append( $"HintActivity: {node.HintActivity,4}\n" );
			report.Append( $"HintYaw:      {node.HintYaw:F6}\n" );
			report.Append( ReportRule ).Append( '\n' );
		}

		report.Append( "\n\n" );

		DropNodes( graph );

		var poolLinks = graph.LinkVisibleNodes( tempPool, report, out var badNode );

		if ( poolLinks == 0 )
		{
			Util.Alert( AlertType.AiConsole, "**ConnectVisibleNodes FAILED!\n" );

			SetThink( ShowBadNode );
			Origin = graph.Nodes[badNode].Origin;

			return;
		}

		report.Append( "----------------------------------------------------------------------------\n" );
		report.Append( "Walk Rejection:\n" );

		for ( var i = 0; i < graph.NodeCount; i++ )
		{
			var source = graph.Nodes[i];

			report.Append( ReportRule ).Append( '\n' );
			report.Append( $"Node {i,4}:\n\n" );

			for ( var j = 0; j < source.NumLinks; j++ )
			{
				ref var link = ref tempPool[source.FirstLink + j];

				TestLinkHulls( source, ref link, report );

				if ( link.LinkInfo != 0 )
					continue;

				report.Append( $"Rejected Node {link.DestinationNode,3} - Unreachable by Any Hull\n" );

				link = tempPool[source.FirstLink + (source.NumLinks - 1)];

				source.NumLinks--;
				poolLinks--;
				j--;
			}
		}

		report.Append( ReportRule ).Append( "\n\n\n" );

		poolLinks -= graph.RejectInlineLinks( tempPool, report );

		graph.LinkPool = new GraphLink[poolLinks];
		graph.LinkCount = poolLinks;

		var finalPoolIndex = 0;

		for ( var i = 0; i < graph.NodeCount; i++ )
		{
			var oldFirstLink = graph.Nodes[i].FirstLink;

			graph.Nodes[i].FirstLink = finalPoolIndex;

			for ( var j = 0; j < graph.Nodes[i].NumLinks; j++ )
				graph.LinkPool[finalPoolIndex++] = tempPool[oldFirstLink + j];
		}

		graph.SortNodes();
		graph.BuildLinkLookups();

		var pairsValid = true;

		report.Append( "\n\n" ).Append( ReportRule ).Append( '\n' );
		report.Append( "Link Pairings:\n" );

		for ( var i = 0; i < graph.NodeCount; i++ )
		{
			for ( var j = 0; j < graph.Nodes[i].NumLinks; j++ )
			{
				var destination = graph.LinkPool[graph.Nodes[i].FirstLink + j].DestinationNode;

				graph.HashSearch( destination, i, out var link );

				if ( link >= 0 )
					continue;

				pairsValid = false;
				report.Append( $"WARNING: Node {destination,3} does not connect back to Node {i,3}\n" );
			}
		}

		if ( pairsValid )
			report.Append( "\nAll Connections are Paired!\n" );

		report.Append( ReportRule ).Append( '\n' );
		report.Append( "\n\n" ).Append( ReportRule ).Append( '\n' );
		report.Append( $"Total Number of Connections in Pool: {poolLinks}\n" );
		report.Append( ReportRule ).Append( '\n' );
		report.Append( $"Connection Pool: {Graph.LinkSize * poolLinks} bytes\n" );
		report.Append( ReportRule ).Append( '\n' );

		Util.Alert( AlertType.AiConsole, $"{graph.NodeCount} Nodes, {poolLinks} Connections\n" );

		graph.BuildRegionTables();

		for ( var i = 0; i < graph.NodeCount; i++ )
		{
			if ( (graph.Nodes[i].NodeInfo & GraphNode.Land) != 0 )
				graph.Nodes[i].Origin.z -= Graph.NodeHeight;
		}

		graph.GraphPresent = true;
		graph.GraphPointersSet = true;
		graph.RoutingComplete = false;

		graph.ComputeStaticRoutingTables();
		graph.SaveGraph( Server.Globals.MapName );

		Util.Alert( AlertType.Console, "Done.\n" );
	}
}

[LinkEntityToClass( "node_viewer" )]
[LinkEntityToClass( "node_viewer_human" )]
[LinkEntityToClass( "node_viewer_fly" )]
[LinkEntityToClass( "node_viewer_large" )]
public class NodeViewer : BaseEntity
{
	private const int MaxVisited = 128;

	private readonly int[] _from = new int[MaxVisited];
	private readonly int[] _to = new int[MaxVisited];
	private int _baseNode;
	private int _draw;
	private int _visited;
	private int _hull;
	private int _nodeType;
	private Vector3 _color;

	public override void Spawn()
	{
		var graph = HalfLifeDll.WorldGraph;

		if ( !graph.GraphPresent || !graph.GraphPointersSet )
		{
			Util.Alert( AlertType.Console, "Graph not ready!\n" );
			Remove();
			return;
		}

		switch ( ClassName )
		{
			case "node_viewer_fly":
				_hull = Graph.NodeFlyHull;
				_nodeType = GraphNode.Air;
				_color = new Vector3( 160f, 100f, 255f );
				break;
			case "node_viewer_large":
				_hull = Graph.NodeLargeHull;
				_nodeType = GraphNode.Land | GraphNode.Water;
				_color = new Vector3( 100f, 255f, 160f );
				break;
			default:
				_hull = Graph.NodeHumanHull;
				_nodeType = GraphNode.Land | GraphNode.Water;
				_color = new Vector3( 255f, 160f, 100f );
				break;
		}

		_baseNode = graph.FindNearestNode( Origin, _nodeType );

		if ( _baseNode < 0 )
		{
			Util.Alert( AlertType.Console, "No nearby node\n" );
			return;
		}

		_visited = 0;

		Util.Alert( AlertType.AiConsole, $"basenode {_baseNode}\n" );

		if ( graph.NodeCount < MaxVisited )
		{
			for ( var i = 0; i < graph.NodeCount; i++ )
				AddNode( i, graph.NextNodeInRoute( i, _baseNode, _hull, 0 ) );
		}
		else
		{
			FindNodeConnections( _baseNode );

			var start = 0;
			int end;

			do
			{
				for ( end = _visited; start < end; start++ )
				{
					FindNodeConnections( _from[start] );
					FindNodeConnections( _to[start] );
				}
			}
			while ( end != _visited );
		}

		Util.Alert( AlertType.AiConsole, $"{_visited} nodes\n" );

		_draw = 0;

		SetThink( DrawThink );
		NextThink = (float)Server.Globals.Time;
	}

	private void FindNodeConnections( int node )
	{
		var graph = HalfLifeDll.WorldGraph;

		AddNode( node, graph.NextNodeInRoute( node, _baseNode, _hull, 0 ) );

		for ( var i = 0; i < graph.Nodes[node].NumLinks; i++ )
		{
			var destination = graph.LinkPool[graph.Nodes[node].FirstLink + i].DestinationNode;

			AddNode( destination, graph.NextNodeInRoute( destination, _baseNode, _hull, 0 ) );
		}
	}

	private void AddNode( int from, int to )
	{
		if ( _visited >= MaxVisited || from == to )
			return;

		for ( var i = 0; i < _visited; i++ )
		{
			if ( (_from[i] == from && _to[i] == to) || (_from[i] == to && _to[i] == from) )
				return;
		}

		_from[_visited] = from;
		_to[_visited] = to;
		_visited++;
	}

	public void DrawThink()
	{
		var graph = HalfLifeDll.WorldGraph;

		NextThink = (float)Server.Globals.Time;

		for ( var i = 0; i < 10; i++ )
		{
			if ( _draw == _visited )
			{
				Remove();
				return;
			}

			var from = graph.Nodes[_from[_draw]].Origin;
			var to = graph.Nodes[_to[_draw]].Origin;

			Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
			Server.WriteByte( TempEntity.BeamPoints );
			Server.WriteCoord( from.x );
			Server.WriteCoord( from.y );
			Server.WriteCoord( from.z + Graph.NodeHeight );
			Server.WriteCoord( to.x );
			Server.WriteCoord( to.y );
			Server.WriteCoord( to.z + Graph.NodeHeight );
			Server.WriteShort( GameModels.Laser );
			Server.WriteByte( 0 );
			Server.WriteByte( 0 );
			Server.WriteByte( 250 );
			Server.WriteByte( 40 );
			Server.WriteByte( 0 );
			Server.WriteByte( (int)_color.x );
			Server.WriteByte( (int)_color.y );
			Server.WriteByte( (int)_color.z );
			Server.WriteByte( 128 );
			Server.WriteByte( 0 );
			Server.MessageEnd();

			_draw++;
		}
	}
}
