using System;
using GoldSrc;

namespace HalfLife;

public enum LocalMove
{
	Invalid,
	InvalidDontTriangulate,
	Valid
}

partial class BaseMonster
{
	public const int RouteSize = 8;

	private const float CutCornerDistance = 8f;
	private const float LocalStepSize = 16f;
	private const float DistanceToCheck = 200f;

	public readonly WayPoint[] Route = new WayPoint[RouteSize];

	[Save( FieldType.Integer )]
	public int MovementGoal;
	[Save( FieldType.Integer )]
	public int RouteIndex;
	[Save( FieldType.Float )]
	public float MoveWaitTime;

	public override bool IsMoving => MovementGoal != MoveGoalType.None;

	public virtual float CoverRadius => 784f;

	public virtual Activity GetStoppedActivity() => Activity.Idle;

	public virtual void Stop() => IdealActivity = GetStoppedActivity();

	public virtual bool ValidateCover( Vector3 coverLocation ) => true;

	public void RouteClear()
	{
		RouteNew();

		MovementGoal = MoveGoalType.None;
		MovementActivity = Activity.Idle;

		Forget( MemoryMoveFailed );
	}

	public void RouteNew()
	{
		Route[0].Type = 0;
		RouteIndex = 0;
	}

	public bool RouteIsClear() => Route[RouteIndex].Type == 0 || MovementGoal == MoveGoalType.None;

	public bool RefreshRoute()
	{
		RouteNew();

		switch ( MovementGoal )
		{
			case MoveGoalType.PathCorner:
				{
					var pathCorner = GoalEntity;

					for ( var i = 0; pathCorner is not null && i < RouteSize; i++ )
					{
						Route[i].Type = MoveFlag.ToPathCorner;
						Route[i].Location = pathCorner.Origin;

						pathCorner = pathCorner.GetNextTarget();

						if ( pathCorner is null )
							Route[i].Type |= MoveFlag.IsGoal;
					}

					return true;
				}

			case MoveGoalType.Enemy:
				return BuildRoute( EnemyLastKnownPosition, MoveFlag.ToEnemy, EnemyEntity );

			case MoveGoalType.Location:
				return BuildRoute( MoveGoal, MoveFlag.ToLocation, null );

			case MoveGoalType.TargetEntity:
				return TargetEntity is { } target && BuildRoute( target.Origin, MoveFlag.ToTargetEntity, target );

			case MoveGoalType.Node:
				return GetNodeRoute( MoveGoal );
		}

		return false;
	}

	public bool MoveToEnemy( Activity movementActivity, float waitTime )
	{
		MovementActivity = movementActivity;
		MoveWaitTime = waitTime;
		MovementGoal = MoveGoalType.Enemy;

		return RefreshRoute();
	}

	public bool MoveToLocation( Activity movementActivity, float waitTime, Vector3 goal )
	{
		MovementActivity = movementActivity;
		MoveWaitTime = waitTime;
		MovementGoal = MoveGoalType.Location;
		MoveGoal = goal;

		return RefreshRoute();
	}

	public bool MoveToTarget( Activity movementActivity, float waitTime )
	{
		MovementActivity = movementActivity;
		MoveWaitTime = waitTime;
		MovementGoal = MoveGoalType.TargetEntity;

		return RefreshRoute();
	}

	public bool MoveToNode( Activity movementActivity, float waitTime, Vector3 goal )
	{
		MovementActivity = movementActivity;
		MoveWaitTime = waitTime;
		MovementGoal = MoveGoalType.Node;
		MoveGoal = goal;

		return RefreshRoute();
	}

	private static bool ShouldSimplify( int routeType )
	{
		routeType &= ~MoveFlag.IsGoal;

		return routeType != MoveFlag.ToPathCorner && (routeType & MoveFlag.DontSimplify) == 0;
	}

	public void RouteSimplify( BaseEntity target )
	{
		var outRoute = new WayPoint[RouteSize * 2];
		var count = 0;

		for ( var index = RouteIndex; index < RouteSize; index++ )
		{
			if ( Route[index].Type == 0 )
				break;

			count++;

			if ( (Route[index].Type & MoveFlag.IsGoal) != 0 )
				break;
		}

		if ( count < 2 )
			return;

		var outCount = 0;
		var start = Origin;
		int i;

		for ( i = 0; i < count - 1; i++ )
		{
			var current = Route[RouteIndex + i];
			var next = Route[RouteIndex + i + 1];

			if ( !ShouldSimplify( current.Type ) )
			{
				outRoute[outCount] = current;
				outCount++;
			}
			else if ( CheckLocalMove( start, next.Location, target, out _ ) == LocalMove.Valid )
			{
				continue;
			}
			else
			{
				var test = (next.Location + current.Location) * 0.5f;
				var split = (current.Location + start) * 0.5f;
				var type = (current.Type | MoveFlag.ToDetour) & ~MoveFlag.NotToMask;

				if ( CheckLocalMove( start, test, target, out _ ) == LocalMove.Valid )
				{
					outRoute[outCount].Type = type;
					outRoute[outCount].Location = test;
				}
				else if ( CheckLocalMove( split, test, target, out _ ) == LocalMove.Valid )
				{
					outRoute[outCount].Type = type;
					outRoute[outCount].Location = split;
					outRoute[outCount + 1].Type = type;
					outRoute[outCount + 1].Location = test;
					outCount++;
				}
				else
				{
					outRoute[outCount] = current;
				}
			}

			start = outRoute[outCount].Location;
			outCount++;
		}

		outRoute[outCount] = Route[RouteIndex + i];
		outCount++;

		outRoute[outCount].Type = 0;

		RouteIndex = 0;

		for ( i = 0; i < RouteSize && i < outCount; i++ )
			Route[i] = outRoute[i];

		if ( i < RouteSize )
			Route[i].Type = 0;
	}

	public virtual LocalMove CheckLocalMove( Vector3 start, Vector3 end, BaseEntity target, out float distanceMoved )
	{
		var startPosition = Origin;
		var yaw = MathLib.VecToYaw( end - start );
		var distance = MathLib.Length2D( end - start );
		var result = LocalMove.Valid;

		distanceMoved = 0f;

		SetOrigin( start );

		if ( (EntityFlags & (EntityFlags.Fly | EntityFlags.Swim)) == 0 )
			Server.DropToFloor( this );

		for ( var step = 0f; step < distance; step += LocalStepSize )
		{
			var stepSize = LocalStepSize;

			if ( step + LocalStepSize >= distance - 1f )
				stepSize = distance - step - 1f;

			if ( Server.WalkMove( this, yaw, stepSize, WalkMoveMode.CheckOnly ) )
				continue;

			distanceMoved = step;
			result = target is not null && target == Server.Globals.TraceEnt ? LocalMove.Valid : LocalMove.Invalid;
			break;
		}

		if ( result == LocalMove.Valid && (EntityFlags & (EntityFlags.Fly | EntityFlags.Swim)) == 0 && (target is null || (target.EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround) )
		{
			if ( MathF.Abs( end.z - Origin.z ) > 64f )
				result = LocalMove.InvalidDontTriangulate;
		}

		SetOrigin( startPosition );

		return result;
	}

	public float OpenDoorAndWait( BaseEntity door )
	{
		var travelTime = 0f;

		if ( door is not null && !door.IsLockedByMaster )
		{
			door.Use( this, this, UseType.On, 0f );

			travelTime = door.NextThink - door.LTime;

			if ( !string.IsNullOrEmpty( door.TargetName ) )
			{
				BaseEntity other = null;

				while ( (other = Util.FindEntityByTargetname( other, door.TargetName )) is not null )
				{
					if ( other != door && other.ClassName == door.ClassName )
						other.Use( this, this, UseType.On, 0f );
				}
			}
		}

		return Server.Globals.Time + travelTime;
	}

	public void AdvanceRoute( float distance )
	{
		if ( RouteIndex == RouteSize - 1 )
		{
			if ( !RefreshRoute() )
				Util.Alert( AlertType.AiConsole, "Can't Refresh Route!!" );

			return;
		}

		if ( (Route[RouteIndex].Type & MoveFlag.IsGoal) != 0 )
		{
			if ( distance < GroundSpeed * 0.2 )
				MovementComplete();

			return;
		}

		if ( (Route[RouteIndex].Type & ~MoveFlag.NotToMask) == MoveFlag.ToPathCorner )
			GoalEntity = GoalEntity.GetNextTarget();

		if ( (Route[RouteIndex].Type & MoveFlag.ToNode) == MoveFlag.ToNode && (Route[RouteIndex + 1].Type & MoveFlag.ToNode) == MoveFlag.ToNode )
		{
			var graph = HalfLifeDll.WorldGraph;
			var sourceNode = graph.FindNearestNode( Route[RouteIndex].Location, this );
			var destinationNode = graph.FindNearestNode( Route[RouteIndex + 1].Location, this );

			graph.HashSearch( sourceNode, destinationNode, out var link );

			if ( link >= 0 && graph.LinkPool[link].LinkEntity is { } linkEntity && graph.HandleLinkEnt( sourceNode, linkEntity, Capability, Graph.QueryType.Dynamic ) )
				MoveWaitFinished = OpenDoorAndWait( linkEntity );
		}

		RouteIndex++;
	}

	public static int RouteClassify( int moveFlag )
	{
		if ( (moveFlag & MoveFlag.ToTargetEntity) != 0 )
			return MoveGoalType.TargetEntity;

		if ( (moveFlag & MoveFlag.ToEnemy) != 0 )
			return MoveGoalType.Enemy;

		if ( (moveFlag & MoveFlag.ToPathCorner) != 0 )
			return MoveGoalType.PathCorner;

		if ( (moveFlag & MoveFlag.ToNode) != 0 )
			return MoveGoalType.Node;

		return (moveFlag & MoveFlag.ToLocation) != 0 ? MoveGoalType.Location : MoveGoalType.None;
	}

	public bool BuildRoute( Vector3 goal, int moveFlag, BaseEntity target )
	{
		RouteNew();
		MovementGoal = RouteClassify( moveFlag );

		Route[0].Location = goal;
		Route[0].Type = moveFlag | MoveFlag.IsGoal;

		var localMove = CheckLocalMove( Origin, goal, target, out var distance );

		if ( localMove == LocalMove.Valid )
			return true;

		if ( localMove != LocalMove.InvalidDontTriangulate && Triangulate( Origin, goal, distance, target, out var apex ) )
		{
			Route[0].Location = apex;
			Route[0].Type = moveFlag | MoveFlag.ToDetour;

			Route[1].Location = goal;
			Route[1].Type = moveFlag | MoveFlag.IsGoal;

			RouteSimplify( target );
			return true;
		}

		if ( GetNodeRoute( goal ) )
		{
			MoveGoal = goal;
			RouteSimplify( target );
			return true;
		}

		return false;
	}

	public void InsertWaypoint( Vector3 location, int moveFlags )
	{
		var type = moveFlags | (Route[RouteIndex].Type & ~MoveFlag.NotToMask);

		for ( var i = RouteSize - 1; i > 0; i-- )
			Route[i] = Route[i - 1];

		Route[RouteIndex].Location = location;
		Route[RouteIndex].Type = type;
	}

	private bool TriangulatePoint( Vector3 point, Vector3 farSide, BaseEntity target )
	{
		return CheckLocalMove( Origin, point, target, out _ ) == LocalMove.Valid && CheckLocalMove( point, farSide, target, out _ ) == LocalMove.Valid;
	}

	public virtual bool Triangulate( Vector3 start, Vector3 end, float distance, BaseEntity target, out Vector3 apex )
	{
		var sizeX = Math.Clamp( Size.x, 24f, 48f );
		var sizeZ = Size.z;
		var flying = MoveType == MoveType.Fly;

		var forward = Util.Normalize( end - start );

		var directionUp = new Vector3( 0f, 0f, 1f );
		var direction = MathLib.CrossProduct( forward, directionUp );

		var left = Origin + (forward * (distance + sizeX)) - (direction * (sizeX * 3f));
		var right = Origin + (forward * (distance + sizeX)) + (direction * (sizeX * 3f));
		var top = Vector3.Zero;
		var bottom = Vector3.Zero;

		if ( flying )
		{
			top = Origin + (forward * distance) + (directionUp * sizeZ * 3f);
			bottom = Origin + (forward * distance) - (directionUp * sizeZ * 3f);
		}

		var farSide = Route[RouteIndex].Location;

		direction = direction * sizeX * 2f;

		if ( flying )
			directionUp = directionUp * sizeZ * 2f;

		for ( var i = 0; i < 8; i++ )
		{
			if ( TriangulatePoint( right, farSide, target ) )
			{
				apex = right;
				return true;
			}

			if ( TriangulatePoint( left, farSide, target ) )
			{
				apex = left;
				return true;
			}

			if ( flying )
			{
				if ( TriangulatePoint( top, farSide, target ) )
				{
					apex = top;
					return true;
				}

				if ( TriangulatePoint( bottom, farSide, target ) )
				{
					apex = bottom;
					return true;
				}
			}

			right += direction;
			left -= direction;

			if ( flying )
			{
				top += directionUp;
				bottom -= directionUp;
			}
		}

		apex = Vector3.Zero;

		return false;
	}

	public virtual void Move( float interval = 0.1f )
	{
		if ( RouteIsClear() && (MovementGoal == MoveGoalType.None || !RefreshRoute()) )
		{
			Util.Alert( AlertType.AiConsole, "Tried to move with no route!" );
			TaskFail();
			return;
		}

		if ( MoveWaitFinished > Server.Globals.Time )
			return;

		var waypoint = Route[RouteIndex];
		var direction = Util.Normalize( waypoint.Location - Origin );
		var waypointDistance = MathLib.Length2D( waypoint.Location - Origin );

		MakeIdealYaw( waypoint.Location );
		ChangeYaw( (int)YawSpeed );

		var checkDistance = MathF.Min( waypointDistance, DistanceToCheck );

		var target = (waypoint.Type & ~MoveFlag.NotToMask) switch
		{
			MoveFlag.ToEnemy => EnemyEntity,
			MoveFlag.ToTargetEntity => TargetEntity,
			_ => null
		};

		if ( CheckLocalMove( Origin, Origin + (direction * checkDistance), target, out var distance ) != LocalMove.Valid )
		{
			Stop();

			var blocker = (Server.Globals.TraceEnt ?? Server.GetEdict( 0 )) as BaseEntity;

			if ( blocker is not null )
				DispatchBlocked( blocker );

			if ( blocker is not null && MoveWaitTime > 0f && blocker.IsMoving && !blocker.IsPlayer && Server.Globals.Time - MoveWaitFinished > 3.0 )
			{
				if ( distance < GroundSpeed )
				{
					MoveWaitFinished = Server.Globals.Time + MoveWaitTime;
					return;
				}
			}
			else if ( Triangulate( Origin, Route[RouteIndex].Location, distance, target, out var apex ) )
			{
				InsertWaypoint( apex, MoveFlag.ToDetour );
				RouteSimplify( target );
			}
			else
			{
				Stop();

				if ( MoveWaitTime > 0f && !HasMemory( MemoryMoveFailed ) )
				{
					RefreshRoute();

					if ( RouteIsClear() )
					{
						TaskFail();
					}
					else
					{
						if ( Server.Globals.Time - MoveWaitFinished < 0.2 )
							Remember( MemoryMoveFailed );

						MoveWaitFinished = (float)(Server.Globals.Time + 0.1);
					}
				}
				else
				{
					TaskFail();
					Util.Alert( AlertType.AiConsole, $"{ClassName} Failed to move ({(HasMemory( MemoryMoveFailed ) ? 1 : 0)})!" );
				}

				return;
			}
		}

		if ( ShouldAdvanceRoute( waypointDistance ) )
			AdvanceRoute( waypointDistance );

		if ( MoveWaitFinished > Server.Globals.Time )
		{
			Stop();
			return;
		}

		if ( checkDistance < GroundSpeed * interval )
			interval = checkDistance / GroundSpeed;

		MoveExecute( target, direction, interval );

		if ( MovementIsComplete )
		{
			Stop();
			RouteClear();
		}
	}

	public virtual bool ShouldAdvanceRoute( float waypointDistance ) => waypointDistance <= CutCornerDistance;

	public virtual void MoveExecute( BaseEntity target, Vector3 direction, float interval )
	{
		if ( IdealActivity != MovementActivity )
			IdealActivity = MovementActivity;

		var total = GroundSpeed * FrameRate * interval;

		while ( total > 0.001 )
		{
			var step = MathF.Min( 16f, total );

			Server.MoveToOrigin( this, Route[RouteIndex].Location, step, 0 );

			total -= step;
		}
	}

	public void MovementComplete()
	{
		switch ( TaskStatus )
		{
			case TaskState.New:
			case TaskState.Running:
				TaskStatus = TaskState.RunningTask;
				break;

			case TaskState.RunningMovement:
				TaskComplete();
				break;

			case TaskState.RunningTask:
				Util.Alert( AlertType.Error, "Movement completed twice!" );
				break;
		}

		MovementGoal = MoveGoalType.None;
	}

	private static float CoverMaxDistance( ref float minDistance, float maxDistance )
	{
		if ( maxDistance == 0f )
			maxDistance = 784f;

		if ( minDistance > 0.5 * maxDistance )
			minDistance = (float)(0.5 * maxDistance);

		return maxDistance;
	}

	public virtual bool FindCover( Vector3 threat, Vector3 viewOffset, float minDistance, float maxDistance )
	{
		var graph = HalfLifeDll.WorldGraph;

		maxDistance = CoverMaxDistance( ref minDistance, maxDistance );

		if ( !graph.GraphPresent || !graph.GraphPointersSet )
		{
			Util.Alert( AlertType.AiConsole, "Graph not ready for findcover!" );
			return false;
		}

		var myNode = graph.FindNearestNode( Origin, this );
		var threatNode = graph.FindNearestNode( threat, this );
		var myHullIndex = graph.HullIndex( this );

		if ( myNode == Graph.NoNode )
		{
			Util.Alert( AlertType.AiConsole, $"FindCover() - {ClassName} has no nearest node!" );
			return false;
		}

		if ( threatNode == Graph.NoNode )
			threatNode = myNode;

		var lookersOffset = threat + viewOffset;

		for ( var i = 0; i < graph.NodeCount; i++ )
		{
			var nodeNumber = (i + graph.LastCoverSearch) % graph.NodeCount;
			var node = graph.Nodes[nodeNumber];

			graph.LastCoverSearch = nodeNumber + 1;

			var distance = MathLib.Length( Origin - node.Origin );

			if ( distance < minDistance || distance >= maxDistance )
				continue;

			var trace = Util.TraceLine( node.Origin + viewOffset, lookersOffset, IgnoreMonsters | IgnoreGlass, this );

			if ( trace.Fraction == 1f )
				continue;

			if ( myNode != threatNode && graph.PathLength( myNode, nodeNumber, myHullIndex, Capability ) > graph.PathLength( threatNode, nodeNumber, myHullIndex, Capability ) )
				continue;

			if ( ValidateCover( node.Origin ) && MoveToLocation( Activity.Run, 0f, node.Origin ) )
				return true;
		}

		return false;
	}

	public virtual bool BuildNearestRoute( Vector3 threat, Vector3 viewOffset, float minDistance, float maxDistance )
	{
		var graph = HalfLifeDll.WorldGraph;

		maxDistance = CoverMaxDistance( ref minDistance, maxDistance );

		if ( !graph.GraphPresent || !graph.GraphPointersSet )
		{
			Util.Alert( AlertType.AiConsole, "Graph not ready for BuildNearestRoute!" );
			return false;
		}

		var myNode = graph.FindNearestNode( Origin, this );
		var myHullIndex = graph.HullIndex( this );

		if ( myNode == Graph.NoNode )
		{
			Util.Alert( AlertType.AiConsole, $"BuildNearestRoute() - {ClassName} has no nearest node!" );
			return false;
		}

		var lookersOffset = threat + viewOffset;

		for ( var i = 0; i < graph.NodeCount; i++ )
		{
			var nodeNumber = (i + graph.LastCoverSearch) % graph.NodeCount;
			var node = graph.Nodes[nodeNumber];

			graph.LastCoverSearch = nodeNumber + 1;

			if ( graph.NextNodeInRoute( myNode, nodeNumber, myHullIndex, 0 ) == myNode )
				continue;

			var distance = MathLib.Length( threat - node.Origin );

			if ( distance <= minDistance || distance >= maxDistance )
				continue;

			var trace = Util.TraceLine( node.Origin + ViewOffset, lookersOffset, IgnoreMonsters, this );

			if ( trace.Fraction == 1f && BuildRoute( node.Origin, MoveFlag.ToLocation, null ) )
			{
				MoveGoal = node.Origin;
				return true;
			}
		}

		return false;
	}
}
