using System;

namespace GoldSrc;

public sealed class PlayerShadow : Component, IScenePhysicsEvents, Component.ICollisionListener
{
	private const float MetersPerInch = 0.0254f;
	private const float MaxDeltaPosition = 24f;
	private const float DampFactor = 1f;
	private const float MinSpeedSquared = 1e-6f / (MetersPerInch * MetersPerInch);
	private const float MinTargetChangeSquared = 1e-6f / (MetersPerInch * MetersPerInch);
	private const float GroundNormalZ = -0.7f;
	private const float PushNormalZ = -0.99f;
	private const float SameNormalDot = 0.99f;
	private const int MaxListNormals = 8;
	private const float ShadowMass = 85f;

	private readonly List<(Vector3 Normal, PhysicsBody Other, float Impulse)> _contacts = [];
	private readonly List<Vector3> _normals = [];

	private Rigidbody _body;
	private BoxCollider _collider;
	private Rigidbody _ground;
	private Vector3 _groundPosition;
	private float _secondsToArrival;
	private Vector3 _targetPosition;
	private Vector3 _maxSpeed;
	private Vector3 _currentSpeed;
	private Vector3 _lastImpulse;
	private bool _enable;
	private bool _updatedSinceLast;

	public float PushMassLimit { get; set; } = float.MaxValue;

	public float PushSpeedLimit { get; set; } = 1e4f;

	public Action ShadowUpdate { get; set; }

	public float Mass => ShadowMass;

	public Vector3 Position => WorldPosition;

	protected override void OnAwake()
	{
		_collider = AddComponent<BoxCollider>();

		_body = AddComponent<Rigidbody>();
		_body.MassOverride = ShadowMass;
		_body.LinearDamping = 0f;
		_body.RigidbodyFlags = RigidbodyFlags.DisableCollisionSounds;
		_body.Locking = new PhysicsLock { Pitch = true, Yaw = true, Roll = true };
	}

	public void SetHull( Vector3 mins, Vector3 maxs )
	{
		_collider.Center = (mins + maxs) * 0.5f;
		_collider.Scale = maxs - mins;
	}

	public void EnableCollisions( bool state ) => _collider.Enabled = state;

	public void SetPosition( Vector3 position ) => WorldPosition = position;

	public void SetVelocity( Vector3 velocity ) => _body.Velocity = velocity;

	public void StepUp( float height )
	{
		if ( height != 0f )
			WorldPosition += new Vector3( 0f, 0f, height );
	}

	public Vector3 GetShadowVelocity()
	{
		var speed = _body.Velocity;

		if ( _ground.IsValid() )
			speed -= _ground.GetVelocityAtPoint( _ground.WorldTransform.PointToWorld( _groundPosition ) );

		return speed;
	}

	public bool IsInContact()
	{
		foreach ( var contact in _contacts )
		{
			if ( IsMoveable( contact.Other ) )
				return true;
		}

		return false;
	}

	private static bool IsMoveable( PhysicsBody body ) => body.IsValid() && body.BodyType == PhysicsBodyType.Dynamic && body.MotionEnabled;

	public void UpdateTarget( Vector3 position, Vector3 velocity, float secondsToArrival, Rigidbody ground )
	{
		_updatedSinceLast = true;

		if ( velocity.DistanceSquared( _currentSpeed ) < MinSpeedSquared && position.DistanceSquared( _targetPosition ) < MinTargetChangeSquared )
			return;

		_targetPosition = position;
		_secondsToArrival = secondsToArrival < 0f ? 0f : secondsToArrival;
		_currentSpeed = velocity;

		_body.Sleeping = false;
		_enable = true;

		if ( velocity.LengthSquared <= 0.1f )
		{
			_enable = false;
			ground = null;
		}
		else
		{
			MaxSpeed( velocity );
		}

		_ground = ground;

		if ( _ground.IsValid() )
			_groundPosition = _ground.WorldTransform.PointToLocal( _targetPosition );
	}

	private void MaxSpeed( Vector3 velocity )
	{
		var available = velocity * MetersPerInch;
		var direction = available;
		var length = direction.Length;

		direction = length > 0f ? direction / length : Vector3.Zero;

		var dot = Vector3.Dot( direction, _body.Velocity * MetersPerInch );

		if ( dot > 0f )
			available -= direction * (dot * length);

		_maxSpeed = new Vector3( MathF.Abs( available.x ), MathF.Abs( available.y ), MathF.Abs( available.z ) ) / MetersPerInch;
	}

	void IScenePhysicsEvents.PrePhysicsStep()
	{
		Simulate( Time.Delta );

		_contacts.Clear();
	}

	void IScenePhysicsEvents.PostPhysicsStep() => ShadowUpdate?.Invoke();

	void ICollisionListener.OnCollisionStart( Collision collision ) => AddContact( collision );

	void ICollisionListener.OnCollisionUpdate( Collision collision ) => AddContact( collision );

	private void AddContact( Collision collision )
	{
		var normal = collision.Contact.Normal;

		if ( Vector3.Dot( normal, collision.Contact.Point - (WorldPosition + _collider.Center) ) < 0f )
			normal = -normal;

		_contacts.Add( (normal, collision.Other.Body, collision.Contact.Impulse) );
	}

	private void Simulate( float deltaTime )
	{
		if ( !_enable || deltaTime <= 0f )
			return;

		var speed = _body.Velocity;
		var baseVelocity = Vector3.Zero;

		if ( _ground.IsValid() )
		{
			_targetPosition = _ground.WorldTransform.PointToWorld( _groundPosition );
			baseVelocity = _ground.GetVelocityAtPoint( _targetPosition );
			speed -= baseVelocity;
		}

		var deltaPosition = _targetPosition - WorldPosition;

		if ( deltaPosition.LengthSquared > MaxDeltaPosition * MaxDeltaPosition )
		{
			WorldPosition = _targetPosition;
			return;
		}

		var gravitySpeed = Scene.PhysicsWorld.Gravity * deltaTime;
		var fraction = 1f;

		if ( _secondsToArrival > 0f )
		{
			fraction = deltaTime / _secondsToArrival;

			if ( fraction > 1f )
				fraction = 1f;
		}

		if ( !_updatedSinceLast )
			ComputeController( ref speed, deltaPosition, new Vector3( _lastImpulse.Length ), fraction / deltaTime, out _ );
		else
			ComputeController( ref speed, deltaPosition, _maxSpeed, fraction / deltaTime, out _lastImpulse );

		speed += baseVelocity;

		_updatedSinceLast = false;

		var ground = false;
		var inverseMass = 1f / ShadowMass;
		var limitVelocity = PushSpeedLimit;

		_normals.Clear();

		foreach ( var contact in _contacts )
		{
			if ( contact.Normal.z < GroundNormalZ )
				ground = true;

			if ( contact.Normal.z <= PushNormalZ )
				continue;

			if ( !IsMoveable( contact.Other ) || contact.Other.Mass > PushMassLimit )
				limitVelocity = 0f;

			var pushTotal = Vector3.Dot( speed, contact.Normal ) + (contact.Impulse * inverseMass);

			if ( pushTotal > limitVelocity )
				AddNormal( contact.Normal );
		}

		var limit = ClampVector( speed, limitVelocity ) - speed;

		speed += limit;
		_lastImpulse += limit;

		if ( ground )
		{
			var gravityDelta = gravitySpeed.Length;

			if ( -_lastImpulse.z >= 0f )
			{
				var delta = gravityDelta + _lastImpulse.z;

				speed.z -= delta;
				_lastImpulse.z -= delta;
			}
		}

		_body.Velocity = speed;

		_secondsToArrival -= deltaTime;

		if ( _secondsToArrival < 0f )
			_secondsToArrival = 0f;
	}

	private static void ComputeController( ref Vector3 currentSpeed, Vector3 delta, Vector3 maxSpeed, float scaleDelta, out Vector3 impulse )
	{
		var acceleration = delta * scaleDelta;

		if ( currentSpeed.LengthSquared < MinSpeedSquared )
			currentSpeed = Vector3.Zero;

		acceleration += currentSpeed * -DampFactor;

		acceleration.x = ClipForce( acceleration.x, maxSpeed.x );
		acceleration.y = ClipForce( acceleration.y, maxSpeed.y );
		acceleration.z = ClipForce( acceleration.z, maxSpeed.z );

		currentSpeed += acceleration;
		impulse = acceleration;
	}

	private static float ClipForce( float value, float maximum )
	{
		if ( MathF.Abs( value ) < maximum )
			return value;

		return value < 0f ? -maximum : maximum;
	}

	private void AddNormal( Vector3 normal )
	{
		if ( _normals.Count == MaxListNormals )
			return;

		foreach ( var existing in _normals )
		{
			if ( Vector3.Dot( existing, normal ) > SameNormalDot )
				return;
		}

		_normals.Add( normal );
	}

	private Vector3 ClampVector( Vector3 vector, float limitVelocity )
	{
		if ( _normals.Count > 2 )
		{
			foreach ( var normal in _normals )
			{
				if ( Vector3.Dot( vector, normal ) > 0f )
					return Vector3.Zero;
			}
		}
		else if ( _normals.Count == 2 )
		{
			var crease = Vector3.Cross( _normals[0], _normals[1] );

			return crease * Vector3.Dot( vector, crease );
		}
		else if ( _normals.Count == 1 )
		{
			var dot = Vector3.Dot( vector, _normals[0] );

			if ( dot > limitVelocity )
				return vector + (_normals[0] * (limitVelocity - dot));
		}

		return vector;
	}
}
