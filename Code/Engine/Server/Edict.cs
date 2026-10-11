using System;

namespace GoldSrc;

public partial class Edict : Component
{
	public int Index { get; internal set; }

	public virtual bool HasPrivateData => false;

	public const int MaxLeafs = 48;

	internal LinkedListNode<Edict> AreaLink { get; set; }
	internal int HeadNode { get; set; }
	internal int NumLeafs { get; set; }
	internal short[] LeafNums { get; } = new short[MaxLeafs];

	public Server Server => Server.Get( Scene );

	public bool ClientSide { get; set; }

	private Vector3 _previousOrigin;
	private Vector3 _previousAngles;
	private Vector3 _renderAngles;
	private Vector3 _writtenPosition;
	private Rotation _writtenRotation;
	private bool _ownTransform;
	private bool _hasPrevious;

	internal bool SkipInterpolation { get; set; }

	internal void StoreInterpolation()
	{
		_previousOrigin = Origin;
		_previousAngles = Angles;
		_hasPrevious = true;

		SkipInterpolation = false;
	}

	internal void Interpolate( float fraction )
	{
		if ( !_hasPrevious || SkipInterpolation )
			fraction = 1f;

		_renderAngles = LerpAngles( _previousAngles, Angles, fraction );

		_ownTransform = true;
		WorldPosition = Vector3.Lerp( _previousOrigin, Origin, fraction );
		WorldRotation = ToRotation( _renderAngles );
		_ownTransform = false;
	}

	protected override void OnEnabled()
	{
		Transform.OnTransformChanged += TransformChanged;
	}

	protected override void OnDisabled()
	{
		Transform.OnTransformChanged -= TransformChanged;
	}

	private void TransformChanged()
	{
		var position = WorldPosition;
		var rotation = WorldRotation;

		if ( _ownTransform || Index == 0 || Server is not { } server )
		{
			_writtenPosition = position;
			_writtenRotation = rotation;
			return;
		}

		var delta = position - _writtenPosition;
		var rotated = rotation != _writtenRotation;

		_writtenPosition = position;
		_writtenRotation = rotation;

		if ( delta != Vector3.Zero )
		{
			_previousOrigin += delta;
			server.SetOrigin( this, Origin + delta );
		}

		if ( rotated && server.GetSpriteModel( ModelIndex ) is null )
		{
			var angles = FromRotation( rotation );

			_previousAngles = angles;
			Angles = angles;
		}
	}

	private static Vector3 FromRotation( Rotation rotation )
	{
		var forward = rotation * Vector3.Forward;
		var yaw = MathF.Atan2( forward.y, forward.x ) * (180f / MathF.PI);
		var pitch = -MathF.Asin( Math.Clamp( forward.z, -1f, 1f ) ) * (180f / MathF.PI);
		var up = (Rotation.FromAxis( Vector3.Up, yaw ) * Rotation.FromAxis( Vector3.Left, pitch )).Inverse * rotation * Vector3.Up;

		return new Vector3( pitch, yaw, MathF.Atan2( -up.y, up.z ) * (180f / MathF.PI) );
	}

	internal static Vector3 LerpAngles( Vector3 from, Vector3 to, float fraction )
	{
		var delta = to - from;

		for ( var i = 0; i < 3; i++ )
		{
			if ( delta[i] < -180f )
				delta[i] += 360f;
			else if ( delta[i] > 180f )
				delta[i] -= 360f;
		}

		return from + (delta * fraction);
	}

	internal static Rotation ToRotation( Vector3 angles )
	{
		return Rotation.FromAxis( Vector3.Up, angles.y )
			* Rotation.FromAxis( Vector3.Left, angles.x )
			* Rotation.FromAxis( Vector3.Forward, angles.z );
	}

	public void CopyEntVars( Edict source )
	{
		foreach ( var property in Game.TypeLibrary.GetType<Edict>().Properties )
		{
			if ( property.CanWrite && property.IsPublic && property.IsSetMethodPublic && property.Name != nameof( Enabled ) && property.TypeDescription?.TargetType == typeof( Edict ) )
				property.SetValue( this, property.GetValue( source ) );
		}

		source.Controller.CopyTo( Controller, 0 );
		source.Blending.CopyTo( Blending, 0 );
	}

	public virtual int DispatchSpawn() => 0;

	public virtual bool DispatchKeyValue( string className, string key, string value ) => false;

	public virtual void DispatchSave( SaveRestoreData saveData )
	{
	}

	public virtual int DispatchRestore( SaveRestoreData saveData, bool globalEntity ) => 0;

	public virtual void SetAbsBox()
	{
	}

	public virtual void DispatchThink()
	{
	}

	public virtual void DispatchTouch( Edict other )
	{
	}

	public virtual void DispatchBlocked( Edict other )
	{
	}

	public virtual void PlayerPreThink()
	{
	}

	public virtual void PlayerPostThink()
	{
	}
}
