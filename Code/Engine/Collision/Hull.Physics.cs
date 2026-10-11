using System;

namespace GoldSrc;

partial class Hull
{
	private const float AxialNormal = 0.99999f;
	private const float SolidStep = 1f;
	private const float PointProbe = 0.001f;
	private const float FrontSide = 1f / 1024f;

	public BBox? PhysicsBounds { get; init; }

	private PhysicsWorld _world;
	private bool _worldBuilt;

	private PhysicsWorld World
	{
		get
		{
			if ( _worldBuilt )
				return _world;

			_worldBuilt = true;

			if ( PhysicsBounds is not { } bounds )
				return null;

			var cells = SolidCells( bounds );

			if ( cells.Count == 0 )
				return null;

			var builder = Sandbox.Model.Builder;

			foreach ( var cell in cells )
				builder.AddCollisionHull( cell );

			if ( builder.Create() is not { Physics: { } physics } )
				return null;

			PhysicsBody body = null;

			foreach ( var part in physics.Parts )
			{
				foreach ( var hull in part.Hulls )
				{
					_world ??= new PhysicsWorld();
					body ??= new PhysicsBody( _world ) { BodyType = PhysicsBodyType.Static };

					body.AddShape( hull, part.Transform );
				}
			}

			return _world;
		}
	}

	public void Dispose()
	{
		_world?.Delete();
		_world = null;
		_worldBuilt = false;
	}

	private static Vector3 Front( Vector3 point ) => new( point.x + FrontSide, point.y + FrontSide, point.z + FrontSide );

	public bool PhysicsPointSolid( Vector3 point )
	{
		if ( World is not { } world )
			return false;

		point = Front( point );

		return world.Trace.Ray( point, point + new Vector3( 0f, 0f, PointProbe ) ).Run().StartedSolid;
	}

	public void PhysicsCheck( Vector3 start, Vector3 end, ref Trace trace )
	{
		PhysicsCheck( start, end, ref trace.StartSolid, ref trace.AllSolid, ref trace.InOpen, ref trace.Fraction, ref trace.EndPos, ref trace.PlaneNormal, ref trace.PlaneDist );
	}

	public void PhysicsCheck( Vector3 start, Vector3 end, ref PmTrace trace )
	{
		PhysicsCheck( start, end, ref trace.StartSolid, ref trace.AllSolid, ref trace.InOpen, ref trace.Fraction, ref trace.EndPos, ref trace.PlaneNormal, ref trace.PlaneDist );
	}

	private void PhysicsCheck( Vector3 start, Vector3 end, ref bool startSolid, ref bool allSolid, ref bool inOpen, ref float fraction, ref Vector3 endPos, ref Vector3 planeNormal, ref float planeDist )
	{
		if ( World is not { } world )
		{
			allSolid = false;
			inOpen = true;
			return;
		}

		var delta = end - start;
		var length = delta.Length;

		if ( length == 0f )
		{
			if ( PhysicsPointSolid( start ) )
			{
				startSolid = true;
				return;
			}

			allSolid = false;
			inOpen = true;
			return;
		}

		var direction = delta / length;
		var front = Front( start );
		var result = world.Trace.Ray( front, front + delta ).Run();
		var travelled = 0f;

		if ( result.StartedSolid )
		{
			startSolid = true;

			var cell = result.Shape;
			var exit = 0f;

			while ( true )
			{
				exit = MathF.Min( exit + SolidStep, length );

				var sample = world.Trace.Ray( front + (direction * exit), front + (direction * exit) + new Vector3( 0f, 0f, PointProbe ) ).Run();

				if ( !sample.StartedSolid )
					break;

				if ( sample.Shape != cell || exit >= length )
					return;
			}

			allSolid = false;
			inOpen = true;
			travelled = exit;
			result = world.Trace.Ray( front + (direction * exit), front + delta ).Run();

			if ( result.StartedSolid )
				return;
		}
		else
		{
			allSolid = false;
			inOpen = true;
		}

		if ( !result.Hit )
			return;

		var normal = result.Normal;

		for ( var i = 0; i < 3; i++ )
		{
			if ( MathF.Abs( normal[i] ) <= AxialNormal )
				continue;

			var axial = Vector3.Zero;

			axial[i] = normal[i] > 0f ? 1f : -1f;
			normal = axial;
			break;
		}

		var approach = Vector3.Dot( direction, normal );
		var facing = MathF.Abs( approach );
		var distance = travelled + (result.Fraction * (length - travelled)) - (facing > 0f ? (DistEpsilon / facing) - (Vector3.Dot( normal, front - start ) / approach) : 0f);
		var hit = Math.Clamp( distance / length, 0f, 1f );

		fraction = hit;
		endPos = start + (delta * hit);
		planeNormal = normal;
		planeDist = Vector3.Dot( normal, result.HitPosition );
	}
}
