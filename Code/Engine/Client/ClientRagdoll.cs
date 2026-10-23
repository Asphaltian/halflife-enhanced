using System;

namespace GoldSrc;

public sealed class ClientRagdoll : Component
{
	public const string Tag = "ragdoll";

	private const float OpaqueAlpha = 255f;
	private const float ImpactStrength = 4000f;
	private const float ExplosionStrength = 500f;
	private const float SleepTolerance = 1f;
	private const double TickInterval = 0.015;

	private readonly Vector3[] _attachments = new Vector3[4];
	private readonly List<(int Bone, Transform Local)> _rootBones = [];

	private SkinnedModelRenderer _renderer;
	private ModelPhysics _physics;
	private Studio.File _header;
	private Effects _effects;
	private int _lightMultiplier;
	private float _alpha = OpaqueAlpha;
	private bool _fadingOut;
	private Vector3 _lastOrigin;
	private double _lastOriginChangeTime;
	private double _nextSettleTime;

	internal BBox Bounds => _renderer.Bounds;

	internal static bool Supports( Sandbox.Model model ) => model?.Physics is { Parts: [{ BoneName.Length: > 0 }, ..] };

	internal bool Asleep => _physics.Bodies[0].Component.Sleeping;

	internal double CreateTime { get; private set; }

	internal bool Player { get; private set; }

	internal Vector3 Origin => _physics.Bodies[0].Component.WorldPosition;

	internal void Init( SkinnedModelRenderer renderer, Studio.File header, Effects effects, int lightMultiplier, Transform[] previous, Transform[] current, float boneTime, Vector3 force, int forceBone, bool player )
	{
		Player = player;

		_renderer = renderer;
		_header = header;
		_effects = effects;
		_lightMultiplier = lightMultiplier;

		CreateTime = Client.Current.ClientTime;

		_lastOriginChangeTime = CreateTime;

		GameObject.Tags.Add( Tag );

		_physics = AddComponent<ModelPhysics>();
		_physics.Model = renderer.Model;
		_physics.Renderer = renderer;

		var rootBind = renderer.Model.GetBoneTransform( _physics.Bodies[0].Bone );

		for ( var bone = 0; bone < header.Bones.Length; bone++ )
		{
			if ( FindBody( bone ) is null )
				_rootBones.Add( (bone, rootBind.ToLocal( renderer.Model.GetBoneTransform( bone ) )) );
		}

		var totalMass = 0f;

		for ( var i = 0; i < _physics.Bodies.Count; i++ )
		{
			var body = _physics.Bodies[i];
			var from = previous[body.Bone];
			var to = current[body.Bone];
			var rotation = to.Rotation * from.Rotation.Inverse;
			var axis = new Vector3( rotation.x, rotation.y, rotation.z );
			var angle = 2f * MathF.Atan2( axis.Length, rotation.w );

			if ( angle > MathF.PI )
				angle -= 2f * MathF.PI;

			body.Component.WorldTransform = to;
			body.Component.Velocity = (to.Position - from.Position) / boneTime;
			body.Component.AngularVelocity = axis.Length > 0f ? axis.Normal * (angle / boneTime) : Vector3.Zero;

			totalMass += body.Component.Mass;
		}

		if ( force == Vector3.Zero )
			return;

		var forceBody = FindBody( forceBone ) ?? _physics.Bodies[0].Component;
		var forcePosition = forceBody.WorldPosition;

		totalMass = MathF.Max( totalMass, 1f );

		forceBody.ApplyImpulse( force );

		for ( var i = 0; i < _physics.Bodies.Count; i++ )
		{
			var body = _physics.Bodies[i];

			if ( body.Component != forceBody )
				body.Component.ApplyImpulseAt( forcePosition, force * (body.Component.Mass / totalMass) );
		}
	}

	private Rigidbody FindBody( int bone )
	{
		for ( ; bone >= 0 && bone < _header.Bones.Length; bone = _header.Bones[bone].Parent )
		{
			for ( var i = 0; i < _physics.Bodies.Count; i++ )
			{
				var body = _physics.Bodies[i];

				if ( body.Bone == bone )
					return body.Component;
			}
		}

		return null;
	}

	internal void Impact( Vector3 position, Vector3 direction )
	{
		_physics.Bodies[0].Component.ApplyImpulseAt( position, direction.Normal * ImpactStrength );

		_lastOriginChangeTime = Client.Current.ClientTime;
	}

	internal void Explode( Vector3 blast )
	{
		_physics.Bodies[0].Component.ApplyImpulse( blast * (Player ? ImpactStrength : ExplosionStrength) );

		_lastOriginChangeTime = Client.Current.ClientTime;
	}

	internal void Pin( Rigidbody body, Vector3 position, Vector3 anchor )
	{
		body.MassOverride = body.Mass * 2f;

		var joint = new GameObject( body.GameObject, true, "bolt" ) { WorldPosition = position }.AddComponent<BallJoint>();

		joint.Friction = 0f;
		joint.Body = new GameObject( GameObject, true, "bolt anchor" ) { WorldPosition = anchor };
	}

	internal void FadeOut() => _fadingOut = true;

	private void CheckSettle()
	{
		var time = Client.Current.ClientTime;

		if ( time < _nextSettleTime || Asleep )
			return;

		_nextSettleTime = time + TickInterval;

		var delta = Origin - _lastOrigin;

		_lastOrigin = Origin;

		if ( MathF.Abs( delta.x ) > SleepTolerance || MathF.Abs( delta.y ) > SleepTolerance || MathF.Abs( delta.z ) > SleepTolerance )
		{
			_lastOriginChangeTime = time;
			return;
		}

		if ( time - _lastOriginChangeTime < Client.RagdollSleepAfterTime )
			return;

		for ( var i = 0; i < _physics.Bodies.Count; i++ )
		{
			var body = _physics.Bodies[i];

			if ( body.Component.Sleeping )
				continue;

			body.Component.Velocity = Vector3.Zero;
			body.Component.AngularVelocity = Vector3.Zero;
			body.Component.Sleeping = true;
		}
	}

	protected override void OnFixedUpdate()
	{
		var client = Client.Current;
		var gravity = client.Gravity / Scene.PhysicsWorld.Gravity.Length;

		for ( var i = 0; i < _physics.Bodies.Count; i++ )
		{
			var body = _physics.Bodies[i];
			var rigidbody = body.Component;

			rigidbody.GravityScale = gravity;

			if ( rigidbody.Sleeping )
				continue;

			var bounds = rigidbody.PhysicsBody.GetBounds();

			if ( client.WaterSurface( bounds ) is { } level )
				rigidbody.ApplyBuoyancy( new Plane( bounds.Center.WithZ( level ), Vector3.Up ), Time.Delta );
		}
	}

	protected override void OnUpdate()
	{
		CheckSettle();

		if ( !_fadingOut )
			return;

		_alpha = (int)MathF.Max( _alpha - ((int)Client.RagdollFadeSpeed * Time.Delta), 0f );

		if ( _alpha == 0f )
			GameObject.Destroy();
	}

	protected override void OnPreRender()
	{
		var client = Client.Current;
		var origin = Origin;
		var light = client.StudioDynamicLight( _effects, RenderFx.None, _lightMultiplier, origin, _header.Header.Flags );
		var attributes = _renderer.Attributes;

		attributes.SetCombo( "D_RENDER_MODE", (int)(_fadingOut ? RenderMode.TransTexture : RenderMode.Normal) );
		attributes.Set( "Blend", _alpha / OpaqueAlpha );
		light.Apply( attributes );

		Array.Fill( _attachments, origin );

		client.StudioEntityLight( 0, origin, _attachments, attributes );

		if ( _renderer.SceneModel is not { } sceneModel )
			return;

		sceneModel.Flags.IsTranslucent = _fadingOut;
		sceneModel.Flags.IsOpaque = !_fadingOut;
		sceneModel.Flags.WantsPrePass = !_fadingOut;

		if ( _rootBones.Count == 0 )
			return;

		var root = WorldTransform.ToLocal( _physics.Bodies[0].Component.WorldTransform );

		foreach ( var (bone, local) in _rootBones )
			sceneModel.SetBoneOverride( bone, root.ToWorld( local ) );
	}
}
