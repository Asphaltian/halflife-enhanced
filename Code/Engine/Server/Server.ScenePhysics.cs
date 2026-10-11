namespace GoldSrc;

partial class Server
{
	private const float GroundCheckDistance = 2f;

	private Rigidbody _groundBody;

	public IEnumerable<Rigidbody> FindSceneBodies( Vector3 origin, float radius )
	{
		foreach ( var gameObject in Scene.FindInPhysics( new Sphere( origin, radius ) ) )
		{
			if ( !gameObject.Tags.Has( ScenePhysics.Tag ) && gameObject.GetComponent<Rigidbody>() is { MotionEnabled: true } body )
				yield return body;
		}
	}

	private void PlayerSceneContact( SceneTraceResult hit, Vector3 velocity )
	{
		if ( hit.Collider?.Rigidbody is { MotionEnabled: true } body && body != _groundBody )
			_touchedPhysObject = true;
	}

	private void CheckSceneGround( Edict player )
	{
		_groundBody = null;

		if ( (player.EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround )
			return;

		var origin = player.Origin;

		if ( !ScenePhysics.Sweep( Scene, origin, origin - new Vector3( 0f, 0f, GroundCheckDistance ), player.Mins, player.Maxs, out _, out var hit ) || hit.Collider is not { } collider )
			return;

		if ( collider.Rigidbody is { MotionEnabled: true } body )
		{
			_groundBody = body;
			return;
		}

		var velocity = collider.GetVelocityAtPoint( hit.HitPosition );

		if ( velocity == Vector3.Zero )
			return;

		if ( (player.EntityFlags & EntityFlags.BaseVelocity) == EntityFlags.BaseVelocity )
			player.BaseVelocity += velocity;
		else
			player.BaseVelocity = velocity;

		player.EntityFlags |= EntityFlags.BaseVelocity;
	}
}
