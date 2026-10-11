namespace GoldSrc;

partial class Server
{
	private const float ShadowPushMassLimit = 350f;
	private const float ShadowPushSpeedLimit = 50f;
	private const float ShadowMaxDistance = 2f;
	private const float ShadowMaxVelocity = 10f;
	private const float ShadowMaxFrameTime = 0.1f;
	private const float ShadowTeleportStepHeight = 4f;
	private const float ShadowMinStepHeight = 0.1f;

	private enum ShadowCollisionState
	{
		Walk,
		Crouch,
		NoClip
	}

	private PlayerShadow _shadow;
	private Edict _shadowPlayer;
	private ShadowCollisionState _shadowCollisionState;
	private bool _touchedPhysObject;
	private Vector3 _shadowOldOrigin;
	private Vector3 _newShadowPosition;
	private Vector3 _newShadowVelocity;
	private float _shadowArrivalTime;

	private void InitPlayerShadow( Edict player )
	{
		if ( _shadow.IsValid() && _shadowPlayer == player )
			return;

		if ( _shadow.IsValid() )
			_shadow.GameObject.Destroy();

		var gameObject = new GameObject( true, "player shadow" );

		gameObject.NetworkMode = NetworkMode.Never;
		gameObject.Tags.Add( ScenePhysics.Tag );
		gameObject.Tags.Add( ScenePhysics.ShadowTag );
		gameObject.Tags.Add( ScenePhysics.BoxTag );
		gameObject.WorldPosition = player.Origin;

		_shadow = gameObject.AddComponent<PlayerShadow>();
		_shadow.PushMassLimit = ShadowPushMassLimit;
		_shadow.PushSpeedLimit = ShadowPushSpeedLimit;
		_shadow.ShadowUpdate = PlayerShadowUpdate;

		_shadowPlayer = player;
		_shadowOldOrigin = player.Origin;

		UpdateShadowPosition( player.Origin, Vector3.Zero, Globals.FrameTime );

		SetShadowCollisionState( player, (player.EntityFlags & EntityFlags.Ducking) == EntityFlags.Ducking ? ShadowCollisionState.Crouch : ShadowCollisionState.Walk );
	}

	private bool IsRideablePhysics( Rigidbody body ) => body.IsValid() && body.Mass > _shadow.Mass * 2f;

	private Trace TracePlayer( Edict player, Vector3 start, Vector3 end ) => Move( start, player.Mins, player.Maxs, end, 0, player, false );

	private void SetShadowCollisionState( Edict player, ShadowCollisionState state )
	{
		_shadowCollisionState = state;

		if ( state == ShadowCollisionState.NoClip )
		{
			_shadow.EnableCollisions( false );
			return;
		}

		var hull = state == ShadowCollisionState.Crouch ? 1 : 0;

		_shadow.SetHull( PlayerMove.PlayerMins[hull], PlayerMove.PlayerMaxs[hull] );
		_shadow.SetPosition( player.Origin );
		_shadow.SetVelocity( player.Velocity );
		_shadow.EnableCollisions( true );
	}

	private void PostThinkShadow( Edict player )
	{
		var newPosition = player.Origin;
		var frameTime = Globals.FrameTime;

		if ( frameTime <= 0f || frameTime > ShadowMaxFrameTime )
			frameTime = ShadowMaxFrameTime;

		var onGround = (player.EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround;

		if ( !_groundBody.IsValid() && _touchedPhysObject && _pmove.OutStepHeight <= 0f && onGround )
		{
			newPosition = _shadowOldOrigin + (frameTime * _pmove.OutWishVel);
			newPosition = (player.Origin * 0.5f) + (newPosition * 0.5f);
		}

		var collisionState = ShadowCollisionState.Walk;

		if ( player.MoveType == MoveType.NoClip )
			collisionState = ShadowCollisionState.NoClip;
		else if ( (player.EntityFlags & EntityFlags.Ducking) == EntityFlags.Ducking )
			collisionState = ShadowCollisionState.Crouch;

		if ( collisionState != _shadowCollisionState )
			SetShadowCollisionState( player, collisionState );

		if ( !(_touchedPhysObject || _groundBody.IsValid()) )
		{
			var maxSpeed = player.MaxSpeed > 0f ? player.MaxSpeed : SvMaxSpeed;

			_pmove.OutWishVel = new Vector3( maxSpeed, maxSpeed, maxSpeed );
		}

		if ( _pmove.OutStepHeight > ShadowMinStepHeight )
		{
			if ( _pmove.OutStepHeight > ShadowTeleportStepHeight )
			{
				_shadow.SetPosition( player.Origin );
			}
			else
			{
				var position = _shadow.Position;
				var end = position + new Vector3( 0f, 0f, _pmove.OutStepHeight );
				var trace = TracePlayer( player, position, end );

				if ( trace.Fraction != 1f || trace.StartSolid )
					_pmove.OutStepHeight = trace.EndPos.z - position.z;

				_shadow.StepUp( _pmove.OutStepHeight );
			}
		}

		_pmove.OutStepHeight = 0f;

		_newShadowPosition = newPosition;
		_newShadowVelocity = _pmove.OutWishVel;

		_shadowOldOrigin = player.Origin;
	}

	private void UpdateShadowPosition( Vector3 position, Vector3 velocity, float secondsToArrival )
	{
		_shadow.UpdateTarget( position, velocity, secondsToArrival, IsRideablePhysics( _groundBody ) ? _groundBody : null );
	}

	private void PlayerShadowUpdate()
	{
		_shadowArrivalTime = 0f;

		if ( !Active || _shadowPlayer is not { } player || player != Player )
			return;

		if ( _shadow.IsInContact() )
			_touchedPhysObject = true;

		if ( player.MoveType == MoveType.NoClip || player.DeadFlag != 0 )
		{
			_shadowOldOrigin = player.Origin;
			return;
		}

		var ground = _groundBody.IsValid() ? _groundBody : null;
		var newPosition = _shadow.Position;
		var newVelocity = _shadow.GetShadowVelocity();
		var lastValidPosition = newPosition;
		var difference = player.Origin - newPosition;

		if ( !_touchedPhysObject && (player.EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround )
			difference.z *= 0.5f;

		var distance = difference.LengthSquared;
		var deltaVelocity = (newVelocity - player.Velocity).LengthSquared;
		var maxDistanceError = ShadowMaxDistance * ShadowMaxDistance;
		var maxVelocityError = ShadowMaxVelocity * ShadowMaxVelocity;

		if ( IsRideablePhysics( ground ) )
		{
			maxDistanceError *= 0.25f;
			maxVelocityError *= 0.25f;
		}

		var checkStuck = false;

		if ( distance >= maxDistanceError || deltaVelocity >= maxVelocityError || (ground is not null && !_touchedPhysObject) )
		{
			if ( _touchedPhysObject || ground is not null )
			{
				if ( deltaVelocity >= maxVelocityError )
				{
					var direction = player.Velocity;
					var length = MathLib.VectorNormalize( ref direction );
					var dot = Vector3.Dot( newVelocity, direction );

					if ( dot > length )
						dot = length;
					else if ( dot < -length )
						dot = -length;

					newVelocity += direction * -dot;

					if ( !IsRideablePhysics( ground ) )
						player.Velocity += newVelocity;
				}

				var trace = TracePlayer( player, newPosition, newPosition );

				if ( !trace.AllSolid && !trace.StartSolid )
					SetPlayerOrigin( player, newPosition );
			}
			else
			{
				checkStuck = true;
			}
		}
		else if ( _touchedPhysObject )
		{
			var trace = TracePlayer( player, player.Origin, player.Origin );

			if ( trace.AllSolid || trace.StartSolid )
			{
				checkStuck = true;
				lastValidPosition = _shadowOldOrigin;

				SetPlayerOrigin( player, newPosition );
			}
		}

		if ( checkStuck )
		{
			var trace = TracePlayer( player, player.Origin, player.Origin );

			if ( trace.AllSolid || trace.StartSolid )
				SetPlayerOrigin( player, lastValidPosition );
		}

		_shadowOldOrigin = player.Origin;
	}

	private void SetPlayerOrigin( Edict player, Vector3 origin )
	{
		player.Origin = origin;

		LinkEdict( player, false );
	}
}
