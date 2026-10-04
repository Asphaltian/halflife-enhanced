using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "trigger_camera" )]
public class TriggerCamera : BaseDelay
{
	private const int SpawnFlagPlayerPosition = 1;
	private const int SpawnFlagPlayerTarget = 2;
	private const int SpawnFlagPlayerTakeControl = 4;

	[Save( FieldType.EHandle )]
	private BaseEntity _player;
	[Save( FieldType.EHandle )]
	private BaseEntity _target;
	[Save( FieldType.ClassPtr )]
	private BaseEntity _path;
	[Save( FieldType.String )]
	private string _pathName;
	[Save( FieldType.Float )]
	private float _wait;
	[Save( FieldType.Time )]
	private float _returnTime;
	[Save( FieldType.Time )]
	private float _stopTime;
	[Save( FieldType.Float )]
	private float _moveDistance;
	[Save( FieldType.Float )]
	private float _targetSpeed;
	[Save( FieldType.Float )]
	private float _initialSpeed;
	[Save( FieldType.Float )]
	private float _acceleration;
	[Save( FieldType.Float )]
	private float _deceleration;
	[Save( FieldType.Integer )]
	private int _state;

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override void Spawn()
	{
		MoveType = MoveType.NoClip;
		Solid = Solid.Not;
		RenderAmt = 0f;
		RenderMode = RenderMode.TransTexture;

		_initialSpeed = Speed;

		if ( _acceleration == 0f )
			_acceleration = 500f;

		if ( _deceleration == 0f )
			_deceleration = 500f;
	}

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "wait":
				_wait = Util.Atof( value );
				return true;
			case "moveto":
				_pathName = Server.AllocString( value );
				return true;
			case "acceleration":
				_acceleration = Util.Atof( value );
				return true;
			case "deceleration":
				_deceleration = Util.Atof( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( !ShouldToggle( useType, _state != 0 ) )
			return;

		_state = _state == 0 ? 1 : 0;

		if ( _state == 0 )
		{
			_returnTime = Server.Globals.Time;
			return;
		}

		if ( activator is null || !activator.IsPlayer )
			activator = Server.EntityOfEntIndex( 1 ) as BaseEntity;

		_player = activator;
		_returnTime = Server.Globals.Time + _wait;
		Speed = _initialSpeed;
		_targetSpeed = _initialSpeed;

		_target = (SpawnFlags & SpawnFlagPlayerTarget) != 0 ? _player : GetNextTarget();

		if ( !_target.IsValid() )
			return;

		if ( (SpawnFlags & SpawnFlagPlayerTakeControl) != 0 )
			((Player)activator).EnableControl( false );

		_path = string.IsNullOrEmpty( _pathName ) ? null : Util.FindEntityByTargetname( null, _pathName );

		_stopTime = Server.Globals.Time;

		if ( _path is not null )
		{
			if ( _path.Speed != 0f )
				_targetSpeed = _path.Speed;

			_stopTime += _path.GetDelay();
		}

		if ( (SpawnFlags & SpawnFlagPlayerPosition) != 0 )
		{
			SetOrigin( activator.Origin + activator.ViewOffset );
			Angles = new Vector3( -activator.Angles.x, activator.Angles.y, 0f );
			Velocity = activator.Velocity;
		}
		else
		{
			Velocity = Vector3.Zero;
		}

		Server.SetView( activator, this );

		SetModel( activator.ModelName );

		SetThink( FollowTarget );
		NextThink = Server.Globals.Time;

		_moveDistance = 0f;
		Move();
	}

	public void FollowTarget()
	{
		if ( !_player.IsValid() )
			return;

		if ( !_target.IsValid() || _returnTime < Server.Globals.Time )
		{
			if ( _player.IsAlive )
			{
				Server.SetView( _player, _player );
				((Player)_player).EnableControl( true );
			}

			UseTargets( this, UseType.Toggle, 0f );
			AVelocity = Vector3.Zero;
			_state = 0;
			return;
		}

		var goal = MathLib.VectorAngles( _target.Origin - Origin );

		goal.x = -goal.x;

		if ( Angles.y > 360f )
			Angles = Angles with { y = Angles.y - 360f };

		if ( Angles.y < 0f )
			Angles = Angles with { y = Angles.y + 360f };

		var dx = goal.x - Angles.x;
		var dy = goal.y - Angles.y;

		if ( dx < -180f )
			dx += 360f;

		if ( dx > 180f )
			dx -= 360f;

		if ( dy < -180f )
			dy += 360f;

		if ( dy > 180f )
			dy -= 360f;

		AVelocity = AVelocity with { x = dx * 40f * Server.Globals.FrameTime, y = dy * 40f * Server.Globals.FrameTime };

		if ( (SpawnFlags & SpawnFlagPlayerTakeControl) == 0 )
		{
			Velocity *= 0.8f;

			if ( MathLib.Length( Velocity ) < 10.0 )
				Velocity = Vector3.Zero;
		}

		NextThink = Server.Globals.Time;

		Move();
	}

	public void Move()
	{
		if ( _path is null )
			return;

		_moveDistance -= Speed * Server.Globals.FrameTime;

		if ( _moveDistance <= 0f )
		{
			if ( !string.IsNullOrEmpty( _path.Message ) )
			{
				FireTargets( _path.Message, this, this, UseType.Toggle, 0f );

				if ( (_path.SpawnFlags & PathCorner.SpawnFlagFireOnce) != 0 )
					_path.Message = null;
			}

			_path = _path.GetNextTarget();

			if ( _path is null )
			{
				Velocity = Vector3.Zero;
			}
			else
			{
				if ( _path.Speed != 0f )
					_targetSpeed = _path.Speed;

				var delta = _path.Origin - Origin;

				_moveDistance = MathLib.Length( delta );
				MoveDir = Util.Normalize( delta );
				_stopTime = Server.Globals.Time + _path.GetDelay();
			}
		}

		if ( _stopTime > Server.Globals.Time )
			Speed = Util.Approach( 0f, Speed, _deceleration * Server.Globals.FrameTime );
		else
			Speed = Util.Approach( _targetSpeed, Speed, _acceleration * Server.Globals.FrameTime );

		var fraction = 2f * Server.Globals.FrameTime;

		Velocity = (MoveDir * Speed * fraction) + (Velocity * (1f - fraction));
	}
}
