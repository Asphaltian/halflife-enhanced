using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "env_glow" )]
public class Glow : PointEntity
{
	[Save( FieldType.Time )]
	private float _lastTime;
	[Save( FieldType.Float )]
	private float _maxFrame;

	public override void Spawn()
	{
		Solid = Solid.Not;
		MoveType = MoveType.None;
		Effects = 0;
		Frame = 0f;

		Server.PrecacheModel( ModelName );
		SetModel( ModelName );

		_maxFrame = Server.ModelFrames( ModelIndex ) - 1;

		if ( _maxFrame > 1.0 && FrameRate != 0f )
			NextThink = (float)(Server.Globals.Time + 0.1);

		_lastTime = Server.Globals.Time;
	}

	public override void Think()
	{
		Animate( FrameRate * (Server.Globals.Time - _lastTime) );

		NextThink = (float)(Server.Globals.Time + 0.1);
		_lastTime = Server.Globals.Time;
	}

	private void Animate( float frames )
	{
		if ( _maxFrame > 0f )
			Frame = (Frame + frames) % _maxFrame;
	}
}

[LinkEntityToClass( "env_sprite" )]
public class Sprite : PointEntity
{
	public const int SpawnFlagStartOn = 0x0001;
	public const int SpawnFlagOnce = 0x0002;
	public const int SpawnFlagTemporary = 0x8000;

	public override ObjectCaps ObjectCaps
	{
		get
		{
			var flags = (SpawnFlags & SpawnFlagTemporary) != 0 ? ObjectCaps.DontSave : 0;

			return (base.ObjectCaps & ~ObjectCaps.AcrossTransition) | flags;
		}
	}

	public float Frames => _maxFrame;

	[Save( FieldType.Time )]
	private float _lastTime;
	[Save( FieldType.Float )]
	private float _maxFrame;

	public override void Spawn()
	{
		Solid = Solid.Not;
		MoveType = MoveType.None;
		Effects = 0;
		Frame = 0f;

		Precache();
		SetModel( ModelName );

		_maxFrame = Server.ModelFrames( ModelIndex ) - 1;

		if ( !string.IsNullOrEmpty( TargetName ) && (SpawnFlags & SpawnFlagStartOn) == 0 )
			TurnOff();
		else
			TurnOn();

		if ( Angles.y != 0f && Angles.z == 0f )
			Angles = new Vector3( Angles.x, 0f, Angles.y );
	}

	public override void Precache()
	{
		Server.PrecacheModel( ModelName );

		if ( AimEntity is not null )
		{
			SetAttachment( AimEntity, Body );
		}
		else
		{
			Skin = 0;
			Body = 0;
		}
	}

	public void SetAttachment( Edict entity, int attachment )
	{
		if ( entity is null )
			return;

		Skin = entity.Index;
		Body = attachment;
		AimEntity = entity;
		MoveType = MoveType.Follow;
	}

	public void SetTransparency( RenderMode renderMode, int r, int g, int b, int a, RenderFx fx )
	{
		RenderMode = renderMode;
		RenderColor = new Vector3( r, g, b );
		RenderAmt = a;
		RenderFx = fx;
	}

	public void SetBrightness( int brightness ) => RenderAmt = brightness;

	public void SetScale( float scale ) => Scale = scale;

	public void SpriteInit( string spriteName, Vector3 origin )
	{
		ModelName = spriteName;
		Origin = origin;
		Spawn();
	}

	public static Sprite SpriteCreate( Server server, string spriteName, Vector3 origin, bool animate )
	{
		var sprite = (Sprite)server.CreateNamedEntity( "env_sprite" );

		sprite.SpriteInit( spriteName, origin );
		sprite.Solid = Solid.Not;
		sprite.MoveType = MoveType.NoClip;

		if ( animate )
			sprite.TurnOn();

		return sprite;
	}

	public void AnimateAndDie( float frameRate )
	{
		SetThink( AnimateUntilDead );
		FrameRate = frameRate;
		DamageTime = Server.Globals.Time + (_maxFrame / frameRate);
		NextThink = Server.Globals.Time;
	}

	public void Expand( float scaleSpeed, float fadeSpeed )
	{
		Speed = scaleSpeed;
		Health = fadeSpeed;
		SetThink( ExpandThink );
		NextThink = Server.Globals.Time;
		_lastTime = Server.Globals.Time;
	}

	public void TurnOff()
	{
		Effects = Effects.NoDraw;
		NextThink = 0f;
	}

	public void TurnOn()
	{
		Effects = 0;

		if ( (FrameRate != 0f && _maxFrame > 1.0) || (SpawnFlags & SpawnFlagOnce) != 0 )
		{
			SetThink( AnimateThink );
			NextThink = Server.Globals.Time;
			_lastTime = Server.Globals.Time;
		}

		Frame = 0f;
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		var on = Effects != Effects.NoDraw;

		if ( !ShouldToggle( useType, on ) )
			return;

		if ( on )
			TurnOff();
		else
			TurnOn();
	}

	private void AnimateThink()
	{
		Animate( FrameRate * (Server.Globals.Time - _lastTime) );

		NextThink = (float)(Server.Globals.Time + 0.1);
		_lastTime = Server.Globals.Time;
	}

	private void AnimateUntilDead()
	{
		if ( Server.Globals.Time > DamageTime )
		{
			Remove();
			return;
		}

		AnimateThink();
		NextThink = Server.Globals.Time;
	}

	private void ExpandThink()
	{
		var frameTime = Server.Globals.Time - _lastTime;

		Scale += Speed * frameTime;
		RenderAmt -= Health * frameTime;

		if ( RenderAmt <= 0f )
		{
			RenderAmt = 0f;
			Remove();
			return;
		}

		NextThink = (float)(Server.Globals.Time + 0.1);
		_lastTime = Server.Globals.Time;
	}

	private void Animate( float frames )
	{
		Frame += frames;

		if ( Frame <= _maxFrame )
			return;

		if ( (SpawnFlags & SpawnFlagOnce) != 0 )
			TurnOff();
		else if ( _maxFrame > 0f )
			Frame %= _maxFrame;
	}
}

[LinkEntityToClass( "beam" )]
public class Beam : BaseEntity
{
	public const int SpawnFlagStartOn = 0x0001;
	public const int SpawnFlagToggle = 0x0002;
	public const int SpawnFlagRandom = 0x0004;
	public const int SpawnFlagRing = 0x0008;
	public const int SpawnFlagSparkStart = 0x0010;
	public const int SpawnFlagSparkEnd = 0x0020;
	public const int SpawnFlagDecals = 0x0040;
	public const int SpawnFlagShadeIn = 0x0080;
	public const int SpawnFlagShadeOut = 0x0100;
	public const int SpawnFlagTemporary = 0x8000;

	public override ObjectCaps ObjectCaps
	{
		get
		{
			var flags = (SpawnFlags & SpawnFlagTemporary) != 0 ? ObjectCaps.DontSave : 0;

			return (base.ObjectCaps & ~ObjectCaps.AcrossTransition) | flags;
		}
	}

	public override void Spawn()
	{
		Solid = Solid.Not;
		Precache();
	}

	public override void Precache()
	{
		if ( Owner is not null )
			SetStartEntity( Owner.Index );

		if ( AimEntity is not null )
			SetEndEntity( AimEntity.Index );
	}

	public void SetBeamType( int type ) => RenderMode = (RenderMode)(((int)RenderMode & 0xF0) | (type & 0x0F));

	public void SetBeamFlags( int flags ) => RenderMode = (RenderMode)(((int)RenderMode & 0x0F) | (flags & 0xF0));

	public void SetStartPos( Vector3 position ) => Origin = position;

	public void SetEndPos( Vector3 position ) => Angles = position;

	public void SetStartEntity( int entityIndex )
	{
		Sequence = (entityIndex & 0x0FFF) | ((Sequence & 0xF000) << 12);
		Owner = Server.EntityOfEntIndex( entityIndex );
	}

	public void SetEndEntity( int entityIndex )
	{
		Skin = (entityIndex & 0x0FFF) | ((Skin & 0xF000) << 12);
		AimEntity = Server.EntityOfEntIndex( entityIndex );
	}

	public void SetStartAttachment( int attachment ) => Sequence = (Sequence & 0x0FFF) | ((attachment & 0xF) << 12);

	public void SetEndAttachment( int attachment ) => Skin = (Skin & 0x0FFF) | ((attachment & 0xF) << 12);

	public void SetTexture( int spriteIndex ) => ModelIndex = spriteIndex;

	public void SetWidth( int width ) => Scale = width;

	public void SetNoise( int amplitude ) => Body = amplitude;

	public void SetColor( int r, int g, int b ) => RenderColor = new Vector3( r, g, b );

	public void SetBrightness( int brightness ) => RenderAmt = brightness;

	public int GetBrightness() => (int)RenderAmt;

	public void SetFrame( float frame ) => Frame = frame;

	public void SetScrollRate( int speed ) => AnimTime = speed;

	public int BeamType => (int)RenderMode & 0x0F;

	public int BeamFlags => (int)RenderMode & 0xF0;

	public int StartEntity => Sequence & 0xFFF;

	public int EndEntity => Skin & 0xFFF;

	public Vector3 StartPos => BeamType == Client.BeamEntsType ? Server.EntityOfEntIndex( StartEntity ).Origin : Origin;

	public Vector3 EndPos
	{
		get
		{
			if ( BeamType is Client.BeamPointsType or Client.BeamHoseType )
				return Angles;

			return Server.EntityOfEntIndex( EndEntity ) is { } entity ? entity.Origin : Angles;
		}
	}

	public override Vector3 Center => (StartPos + EndPos) * 0.5f;

	public void RelinkBeam()
	{
		var startPos = StartPos;
		var endPos = EndPos;

		SetSize( Vector3.Min( startPos, endPos ) - Origin, Vector3.Max( startPos, endPos ) - Origin );
		SetOrigin( Origin );
	}

	public void TriggerTouch( BaseEntity other )
	{
		if ( (other.EntityFlags & (EntityFlags.Client | EntityFlags.Monster)) == 0 )
			return;

		if ( Owner is BaseEntity owner )
			owner.Use( other, this, UseType.Toggle, 0f );

		Util.Alert( AlertType.Console, "Firing targets!!!\n" );
	}

	protected BaseEntity RandomTargetname( string name )
	{
		var total = 0;
		BaseEntity entity = null;
		BaseEntity next = null;

		while ( (next = Util.FindEntityByTargetname( next, name )) is not null )
		{
			total++;

			if ( Server.RandomLong( 0, total - 1 ) < 1 )
				entity = next;
		}

		return entity;
	}

	protected void DoSparks( Vector3 start, Vector3 end )
	{
		if ( (SpawnFlags & SpawnFlagSparkStart) != 0 )
			Util.Sparks( start );

		if ( (SpawnFlags & SpawnFlagSparkEnd) != 0 )
			Util.Sparks( end );
	}

	public void BeamDamage( in Trace trace )
	{
		RelinkBeam();

		if ( trace.Fraction != 1f && trace.Ent is BaseEntity hit )
		{
			var direction = trace.EndPos - Origin;

			MathLib.VectorNormalize( ref direction );

			Combat.ClearMultiDamage();
			hit.TraceAttack( this, Damage * (Server.Globals.Time - DamageTime), direction, trace, DamageEnergyBeam );
			Combat.ApplyMultiDamage( this, this );

			if ( (SpawnFlags & SpawnFlagDecals) != 0 && hit.IsBspModel )
				Util.DecalTrace( trace, Decals.BigShot1 + Server.RandomLong( 0, 4 ) );
		}

		DamageTime = Server.Globals.Time;
	}

	public void BeamDamageInstant( in Trace trace, float damage )
	{
		Damage = damage;
		DamageTime = Server.Globals.Time - 1f;
		BeamDamage( trace );
	}

	public void BeamInit( string spriteName, int width )
	{
		EntityFlags |= EntityFlags.CustomEntity;
		SetColor( 255, 255, 255 );
		SetBrightness( 255 );
		SetNoise( 0 );
		SetFrame( 0f );
		SetScrollRate( 0 );
		ModelName = spriteName;
		SetTexture( Server.PrecacheModel( spriteName ) );
		SetWidth( width );
		Skin = 0;
		Sequence = 0;
		RenderMode = 0;
	}

	public void PointsInit( Vector3 start, Vector3 end )
	{
		SetBeamType( Client.BeamPointsType );
		SetStartPos( start );
		SetEndPos( end );
		SetStartAttachment( 0 );
		SetEndAttachment( 0 );
		RelinkBeam();
	}

	public void HoseInit( Vector3 start, Vector3 direction )
	{
		SetBeamType( Client.BeamHoseType );
		SetStartPos( start );
		SetEndPos( direction );
		SetStartAttachment( 0 );
		SetEndAttachment( 0 );
		RelinkBeam();
	}

	public void PointEntInit( Vector3 start, int endIndex )
	{
		SetBeamType( Client.BeamEntPointType );
		SetStartPos( start );
		SetEndEntity( endIndex );
		SetStartAttachment( 0 );
		SetEndAttachment( 0 );
		RelinkBeam();
	}

	public void EntsInit( int startIndex, int endIndex )
	{
		SetBeamType( Client.BeamEntsType );
		SetStartEntity( startIndex );
		SetEndEntity( endIndex );
		SetStartAttachment( 0 );
		SetEndAttachment( 0 );
		RelinkBeam();
	}

	public static Beam BeamCreate( Server server, string spriteName, int width )
	{
		var beam = (Beam)server.CreateNamedEntity( "beam" );

		beam.BeamInit( spriteName, width );

		return beam;
	}

	public void LiveForTime( float time )
	{
		SetThink( SubRemove );
		NextThink = Server.Globals.Time + time;
	}
}

[LinkEntityToClass( "env_lightning" )]
[LinkEntityToClass( "env_beam" )]
public class Lightning : Beam
{
	[Save( FieldType.Integer )]
	private int _active;
	[Save( FieldType.String )]
	private string _startEntity;
	[Save( FieldType.String )]
	private string _endEntity;
	[Save( FieldType.Float )]
	private float _life;
	[Save( FieldType.Integer )]
	private int _boltWidth;
	[Save( FieldType.Integer )]
	private int _noiseAmplitude;
	[Save( FieldType.Integer )]
	private int _speed;
	[Save( FieldType.Float )]
	private float _restrike;
	[Save( FieldType.Integer )]
	private int _spriteTexture;
	[Save( FieldType.String )]
	private string _spriteName;
	[Save( FieldType.Integer )]
	private int _frameStart;
	[Save( FieldType.Float )]
	private float _radius;

	private bool ServerSide => _life == 0f && (SpawnFlags & SpawnFlagRing) == 0;

	public override void Spawn()
	{
		if ( string.IsNullOrEmpty( _spriteName ) )
		{
			SetThink( SubRemove );
			return;
		}

		Solid = Solid.Not;
		Precache();

		DamageTime = Server.Globals.Time;

		if ( ServerSide )
		{
			SetThink( null );

			if ( Damage > 0f )
			{
				SetThink( DamageThink );
				NextThink = (float)(Server.Globals.Time + 0.1);
			}

			if ( !string.IsNullOrEmpty( TargetName ) )
			{
				if ( (SpawnFlags & SpawnFlagStartOn) == 0 )
				{
					Effects = Effects.NoDraw;
					_active = 0;
					NextThink = 0f;
				}
				else
				{
					_active = 1;
				}

				SetUse( ToggleUse );
			}
		}
		else
		{
			_active = 0;

			if ( !string.IsNullOrEmpty( TargetName ) )
				SetUse( StrikeUse );

			if ( string.IsNullOrEmpty( TargetName ) || (SpawnFlags & SpawnFlagStartOn) != 0 )
			{
				SetThink( StrikeThink );
				NextThink = (float)(Server.Globals.Time + 1.0);
			}
		}
	}

	public override void Precache()
	{
		_spriteTexture = Server.PrecacheModel( _spriteName );
		base.Precache();
	}

	public override void Activate()
	{
		if ( ServerSide )
			BeamUpdateVars();
	}

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "LightningStart":
				_startEntity = Server.AllocString( value );
				return true;
			case "LightningEnd":
				_endEntity = Server.AllocString( value );
				return true;
			case "life":
				_life = Util.Atof( value );
				return true;
			case "BoltWidth":
				_boltWidth = Util.Atoi( value );
				return true;
			case "NoiseAmplitude":
				_noiseAmplitude = Util.Atoi( value );
				return true;
			case "TextureScroll":
				_speed = Util.Atoi( value );
				return true;
			case "StrikeTime":
				_restrike = Util.Atof( value );
				return true;
			case "texture":
				_spriteName = Server.AllocString( value );
				return true;
			case "framestart":
				_frameStart = Util.Atoi( value );
				return true;
			case "Radius":
				_radius = Util.Atof( value );
				return true;
			case "damage":
				Damage = Util.Atof( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	private void ToggleUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( !ShouldToggle( useType, _active != 0 ) )
			return;

		if ( _active != 0 )
		{
			_active = 0;
			Effects |= Effects.NoDraw;
			NextThink = 0f;
		}
		else
		{
			_active = 1;
			Effects &= ~Effects.NoDraw;
			DoSparks( StartPos, EndPos );

			if ( Damage > 0f )
			{
				NextThink = Server.Globals.Time;
				DamageTime = Server.Globals.Time;
			}
		}
	}

	private void StrikeUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( !ShouldToggle( useType, _active != 0 ) )
			return;

		if ( _active != 0 )
		{
			_active = 0;
			SetThink( null );
		}
		else
		{
			SetThink( StrikeThink );
			NextThink = (float)(Server.Globals.Time + 0.1);
		}

		if ( (SpawnFlags & SpawnFlagToggle) == 0 )
			SetUse( null );
	}

	private static bool IsPointEntity( BaseEntity entity )
	{
		return entity.ModelIndex == 0 || entity.ClassName is "info_target" or "info_landmark" or "path_corner";
	}

	private void WriteBeamParameters()
	{
		Server.WriteShort( _spriteTexture );
		Server.WriteByte( _frameStart );
		Server.WriteByte( (int)FrameRate );
		Server.WriteByte( (int)(_life * 10.0) );
		Server.WriteByte( _boltWidth );
		Server.WriteByte( _noiseAmplitude );
		Server.WriteByte( (int)RenderColor.x );
		Server.WriteByte( (int)RenderColor.y );
		Server.WriteByte( (int)RenderColor.z );
		Server.WriteByte( (int)RenderAmt );
		Server.WriteByte( _speed );
	}

	private void StrikeThink()
	{
		if ( _life != 0f )
		{
			if ( (SpawnFlags & SpawnFlagRandom) != 0 )
				NextThink = Server.Globals.Time + _life + Server.RandomFloat( 0f, _restrike );
			else
				NextThink = Server.Globals.Time + _life + _restrike;
		}

		_active = 1;

		if ( string.IsNullOrEmpty( _endEntity ) )
		{
			if ( string.IsNullOrEmpty( _startEntity ) )
				RandomArea();
			else if ( RandomTargetname( _startEntity ) is { } source )
				RandomPoint( source.Origin );
			else
				Util.Alert( AlertType.Console, $"env_beam: unknown entity \"{_startEntity}\"\n" );

			return;
		}

		var start = RandomTargetname( _startEntity );
		var end = RandomTargetname( _endEntity );

		if ( start is null || end is null )
			return;

		var point = IsPointEntity( start ) || IsPointEntity( end );

		if ( point && (SpawnFlags & SpawnFlagRing) != 0 )
			return;

		Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );

		if ( point )
		{
			if ( !IsPointEntity( end ) )
				(start, end) = (end, start);

			if ( !IsPointEntity( start ) )
			{
				Server.WriteByte( TempEntity.BeamEntPoint );
				Server.WriteShort( start.Index );
				Server.WriteCoord( end.Origin.x );
				Server.WriteCoord( end.Origin.y );
				Server.WriteCoord( end.Origin.z );
			}
			else
			{
				Server.WriteByte( TempEntity.BeamPoints );
				Server.WriteCoord( start.Origin.x );
				Server.WriteCoord( start.Origin.y );
				Server.WriteCoord( start.Origin.z );
				Server.WriteCoord( end.Origin.x );
				Server.WriteCoord( end.Origin.y );
				Server.WriteCoord( end.Origin.z );
			}
		}
		else
		{
			Server.WriteByte( (SpawnFlags & SpawnFlagRing) != 0 ? TempEntity.BeamRing : TempEntity.BeamEnts );
			Server.WriteShort( start.Index );
			Server.WriteShort( end.Index );
		}

		WriteBeamParameters();
		Server.MessageEnd();

		DoSparks( start.Origin, end.Origin );

		if ( Damage > 0f )
			BeamDamageInstant( Util.TraceLine( start.Origin, end.Origin, DontIgnoreMonsters, null ), Damage );
	}

	private void DamageThink()
	{
		NextThink = (float)(Server.Globals.Time + 0.1);
		BeamDamage( Util.TraceLine( StartPos, EndPos, DontIgnoreMonsters, null ) );
	}

	private void Zap( Vector3 source, Vector3 destination )
	{
		Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
		Server.WriteByte( TempEntity.BeamPoints );
		Server.WriteCoord( source.x );
		Server.WriteCoord( source.y );
		Server.WriteCoord( source.z );
		Server.WriteCoord( destination.x );
		Server.WriteCoord( destination.y );
		Server.WriteCoord( destination.z );
		WriteBeamParameters();
		Server.MessageEnd();

		DoSparks( source, destination );
	}

	private static Vector3 RandomDirection()
	{
		var x = Server.RandomFloat( -1f, 1f );
		var y = Server.RandomFloat( -1f, 1f );
		var z = Server.RandomFloat( -1f, 1f );

		return new Vector3( x, y, z );
	}

	private void RandomArea()
	{
		for ( var loops = 0; loops < 10; loops++ )
		{
			var source = Origin;
			var direction1 = RandomDirection();

			MathLib.VectorNormalize( ref direction1 );

			var trace1 = Util.TraceLine( source, source + (direction1 * _radius), IgnoreMonsters, this );

			if ( trace1.Fraction == 1f )
				continue;

			Vector3 direction2;

			do
			{
				direction2 = RandomDirection();
			}
			while ( Vector3.Dot( direction1, direction2 ) > 0f );

			MathLib.VectorNormalize( ref direction2 );

			var trace2 = Util.TraceLine( source, source + (direction2 * _radius), IgnoreMonsters, this );

			if ( trace2.Fraction == 1f )
				continue;

			if ( MathLib.Length( trace1.EndPos - trace2.EndPos ) < _radius * 0.1 )
				continue;

			trace2 = Util.TraceLine( trace1.EndPos, trace2.EndPos, IgnoreMonsters, this );

			if ( trace2.Fraction != 1f )
				continue;

			Zap( trace1.EndPos, trace2.EndPos );

			break;
		}
	}

	private void RandomPoint( Vector3 source )
	{
		for ( var loops = 0; loops < 10; loops++ )
		{
			var direction = RandomDirection();

			MathLib.VectorNormalize( ref direction );

			var trace = Util.TraceLine( source, source + (direction * _radius), IgnoreMonsters, this );

			if ( MathLib.Length( trace.EndPos - source ) < _radius * 0.1 )
				continue;

			if ( trace.Fraction == 1f )
				continue;

			Zap( source, trace.EndPos );
			break;
		}
	}

	private void BeamUpdateVars()
	{
		var start = Server.FindEntityByString( null, "targetname", _startEntity ) as BaseEntity ?? World;
		var end = Server.FindEntityByString( null, "targetname", _endEntity ) as BaseEntity ?? World;
		var pointStart = IsPointEntity( start );
		var pointEnd = IsPointEntity( end );

		Skin = 0;
		Sequence = 0;
		RenderMode = 0;
		EntityFlags |= EntityFlags.CustomEntity;
		ModelName = _spriteName;
		SetTexture( _spriteTexture );

		var beamType = Client.BeamEntsType;

		if ( pointStart || pointEnd )
		{
			if ( !pointStart )
			{
				(start, end) = (end, start);
				(pointStart, pointEnd) = (pointEnd, pointStart);
			}

			beamType = pointEnd ? Client.BeamPointsType : Client.BeamEntPointType;
		}

		SetBeamType( beamType );

		if ( beamType is Client.BeamPointsType or Client.BeamEntPointType or Client.BeamHoseType )
		{
			SetStartPos( start.Origin );

			if ( beamType is Client.BeamPointsType or Client.BeamHoseType )
				SetEndPos( end.Origin );
			else
				SetEndEntity( end.Index );
		}
		else
		{
			SetStartEntity( start.Index );
			SetEndEntity( end.Index );
		}

		RelinkBeam();

		SetWidth( _boltWidth );
		SetNoise( _noiseAmplitude );
		SetFrame( _frameStart );
		SetScrollRate( _speed );

		if ( (SpawnFlags & SpawnFlagShadeIn) != 0 )
			SetBeamFlags( (int)GoldSrc.BeamFlags.ShadeIn );
		else if ( (SpawnFlags & SpawnFlagShadeOut) != 0 )
			SetBeamFlags( (int)GoldSrc.BeamFlags.ShadeOut );
	}
}

[LinkEntityToClass( "env_laser" )]
public class Laser : Beam
{
	[Save( FieldType.ClassPtr )]
	private Sprite _sprite;
	[Save( FieldType.String )]
	private string _spriteName;
	[Save( FieldType.PositionVector )]
	private Vector3 _firePosition;

	private bool IsOn => (Effects & Effects.NoDraw) != Effects.NoDraw;

	public override void Spawn()
	{
		if ( string.IsNullOrEmpty( ModelName ) )
		{
			SetThink( SubRemove );
			return;
		}

		Solid = Solid.Not;
		Precache();

		SetThink( StrikeThink );
		EntityFlags |= EntityFlags.CustomEntity;

		PointsInit( Origin, Origin );

		if ( _sprite is null && !string.IsNullOrEmpty( _spriteName ) )
			_sprite = Sprite.SpriteCreate( Server, _spriteName, Origin, true );
		else
			_sprite = null;

		_sprite?.SetTransparency( RenderMode.Glow, (int)RenderColor.x, (int)RenderColor.y, (int)RenderColor.z, (int)RenderAmt, RenderFx );

		if ( !string.IsNullOrEmpty( TargetName ) && (SpawnFlags & SpawnFlagStartOn) == 0 )
			TurnOff();
		else
			TurnOn();
	}

	public override void Precache()
	{
		ModelIndex = Server.PrecacheModel( ModelName );

		if ( !string.IsNullOrEmpty( _spriteName ) )
			Server.PrecacheModel( _spriteName );
	}

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "LaserTarget":
				Message = Server.AllocString( value );
				return true;
			case "width":
				SetWidth( (int)Util.Atof( value ) );
				return true;
			case "NoiseAmplitude":
				SetNoise( Util.Atoi( value ) );
				return true;
			case "TextureScroll":
				SetScrollRate( Util.Atoi( value ) );
				return true;
			case "texture":
				ModelName = Server.AllocString( value );
				return true;
			case "EndSprite":
				_spriteName = Server.AllocString( value );
				return true;
			case "framestart":
				Frame = Util.Atoi( value );
				return true;
			case "damage":
				Damage = Util.Atof( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public void TurnOff()
	{
		Effects |= Effects.NoDraw;
		NextThink = 0f;
		_sprite?.TurnOff();
	}

	public void TurnOn()
	{
		Effects &= ~Effects.NoDraw;
		_sprite?.TurnOn();
		DamageTime = Server.Globals.Time;
		NextThink = Server.Globals.Time;
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		var active = IsOn;

		if ( !ShouldToggle( useType, active ) )
			return;

		if ( active )
			TurnOff();
		else
			TurnOn();
	}

	public void FireAtPoint( in Trace trace )
	{
		SetEndPos( trace.EndPos );
		if ( _sprite is not null )
			Server.SetOrigin( _sprite, trace.EndPos );

		BeamDamage( trace );
		DoSparks( StartPos, trace.EndPos );
	}

	private void StrikeThink()
	{
		if ( RandomTargetname( Message ) is { } end )
			_firePosition = end.Origin;

		FireAtPoint( Util.TraceLine( Origin, _firePosition, DontIgnoreMonsters, null ) );
		NextThink = (float)(Server.Globals.Time + 0.1);
	}
}

[LinkEntityToClass( "env_shake" )]
public class Shake : PointEntity
{
	private const int SpawnFlagEveryone = 0x0001;

	public override void Spawn()
	{
		Solid = Solid.Not;
		MoveType = MoveType.None;
		Effects = 0;
		Frame = 0f;

		if ( (SpawnFlags & SpawnFlagEveryone) != 0 )
			Damage = 0f;
	}

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "amplitude":
				Scale = Util.Atof( value );
				return true;
			case "frequency":
				DamageSave = Util.Atof( value );
				return true;
			case "duration":
				DamageTake = Util.Atof( value );
				return true;
			case "radius":
				Damage = Util.Atof( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		Util.ScreenShake( Origin, Scale, DamageSave, DamageTake, Damage );
	}
}

[LinkEntityToClass( "env_fade" )]
public class Fade : PointEntity
{
	private const int SpawnFlagIn = 0x0001;
	private const int SpawnFlagModulate = 0x0002;
	private const int SpawnFlagOnlyOne = 0x0004;

	public override void Spawn()
	{
		Solid = Solid.Not;
		MoveType = MoveType.None;
		Effects = 0;
		Frame = 0f;
	}

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "duration":
				DamageTake = Util.Atof( value );
				return true;
			case "holdtime":
				DamageSave = Util.Atof( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		var fadeFlags = 0;

		if ( (SpawnFlags & SpawnFlagIn) == 0 )
			fadeFlags |= Util.FadeOut;

		if ( (SpawnFlags & SpawnFlagModulate) != 0 )
			fadeFlags |= Util.FadeModulate;

		if ( (SpawnFlags & SpawnFlagOnlyOne) != 0 )
		{
			if ( activator.IsNetClient )
				Util.ScreenFade( activator, RenderColor, DamageTake, DamageSave, (int)RenderAmt, fadeFlags );
		}
		else
		{
			Util.ScreenFadeAll( RenderColor, DamageTake, DamageSave, (int)RenderAmt, fadeFlags );
		}

		UseTargets( this, UseType.Toggle, 0f );
	}
}

[LinkEntityToClass( "env_message" )]
public class EnvMessage : PointEntity
{
	public const int SpawnFlagOnce = 0x0001;
	public const int SpawnFlagAll = 0x0002;

	public override void Spawn()
	{
		Precache();

		Solid = Solid.Not;
		MoveType = MoveType.None;

		Speed = Impulse switch
		{
			1 => Server.AttenuationStatic,
			2 => Server.AttenuationNormal,
			3 => Server.AttenuationNone,
			_ => Server.AttenuationIdle
		};

		Impulse = 0;

		if ( Scale <= 0f )
			Scale = 1f;
	}

	public override void Precache()
	{
		if ( !string.IsNullOrEmpty( Noise ) )
			Server.PrecacheSound( Noise );
	}

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "messagesound":
				Noise = Server.AllocString( value );
				return true;
			case "messagevolume":
				Scale = (float)(Util.Atof( value ) * 0.1);
				return true;
			case "messageattenuation":
				Impulse = Util.Atoi( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( (SpawnFlags & SpawnFlagAll) != 0 )
		{
			Util.ShowMessageAll( Message );
		}
		else
		{
			var player = activator is { IsPlayer: true } ? activator : Server.EntityOfEntIndex( 1 ) as BaseEntity;

			if ( player is not null )
				Util.ShowMessage( Message, player );
		}

		if ( !string.IsNullOrEmpty( Noise ) )
			Server.EmitSound( this, SoundChannel.Body, Noise, Scale, Speed );

		if ( (SpawnFlags & SpawnFlagOnce) != 0 )
			Remove();

		UseTargets( this, UseType.Toggle, 0f );
	}
}

[LinkEntityToClass( "test_effect" )]
public class TestEffect : BaseDelay
{
	private const int MaxBeams = 24;

	private int _beamCount;
	private readonly Beam[] _beams = new Beam[MaxBeams];
	private readonly float[] _beamTime = new float[MaxBeams];
	private float _startTime;

	public override void Spawn() => Precache();

	public override void Precache() => Server.PrecacheModel( "sprites/lgtning.spr" );

	public void TestThink()
	{
		var time = Server.Globals.Time - _startTime;

		if ( _beamCount < MaxBeams )
		{
			var beam = Beam.BeamCreate( Server, "sprites/lgtning.spr", 100 );
			var source = Origin;
			var direction = new Vector3( Server.RandomFloat( -1f, 1f ), Server.RandomFloat( -1f, 1f ), Server.RandomFloat( -1f, 1f ) );

			direction = Util.Normalize( direction );

			var trace = Util.TraceLine( source, source + (direction * 128f), IgnoreMonsters, this );

			beam.PointsInit( source, trace.EndPos );
			beam.SetColor( 255, 180, 100 );
			beam.SetWidth( 100 );
			beam.SetScrollRate( 12 );

			_beamTime[_beamCount] = Server.Globals.Time;
			_beams[_beamCount] = beam;
			_beamCount++;
		}

		if ( time < 3.0 )
		{
			for ( var i = 0; i < _beamCount; i++ )
			{
				time = (Server.Globals.Time - _beamTime[i]) / (3 + _startTime - _beamTime[i]);
				_beams[i].SetBrightness( (int)(255 * time) );
			}

			NextThink = (float)(Server.Globals.Time + 0.1);
		}
		else
		{
			for ( var i = 0; i < _beamCount; i++ )
				_beams[i].Remove();

			_startTime = Server.Globals.Time;
			_beamCount = 0;

			SetThink( null );
		}
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		SetThink( TestThink );
		NextThink = (float)(Server.Globals.Time + 0.1);
		_startTime = Server.Globals.Time;
	}
}

[LinkEntityToClass( "env_bubbles" )]
public class Bubbling : BaseEntity
{
	private const int SpawnFlagStartOff = 0x0001;

	[Save( FieldType.Integer )]
	private int _density;
	[Save( FieldType.Integer )]
	private int _frequency;
	[Save( FieldType.Integer )]
	private int _state;

	private int _bubbleModel;

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override void Spawn()
	{
		Precache();
		SetModel( ModelName );

		Solid = Solid.Not;
		RenderAmt = 0f;
		RenderMode = RenderMode.TransTexture;

		var speed = (int)(Speed > 0f ? Speed : -Speed);

		RenderColor = new Vector3( speed >> 8, speed & 255, Speed < 0f ? 1 : 0 );

		if ( (SpawnFlags & SpawnFlagStartOff) == 0 )
		{
			SetThink( FizzThink );
			NextThink = (float)(Server.Globals.Time + 2.0);
			_state = 1;
		}
		else
		{
			_state = 0;
		}
	}

	public override void Precache()
	{
		_bubbleModel = Server.PrecacheModel( "sprites/bubble.spr" );
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( ShouldToggle( useType, _state != 0 ) )
			_state = _state == 0 ? 1 : 0;

		if ( _state != 0 )
		{
			SetThink( FizzThink );
			NextThink = (float)(Server.Globals.Time + 0.1);
		}
		else
		{
			SetThink( null );
			NextThink = 0f;
		}
	}

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "density":
				_density = Util.Atoi( value );
				return true;
			case "frequency":
				_frequency = Util.Atoi( value );
				return true;
			case "current":
				Speed = Util.Atoi( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public void FizzThink()
	{
		Server.MessageBegin( MessageDest.Pas, Server.TempEntityMessage, BModelOrigin );
		Server.WriteByte( TempEntity.Fizz );
		Server.WriteShort( (short)Index );
		Server.WriteShort( (short)_bubbleModel );
		Server.WriteByte( _density );
		Server.MessageEnd();

		if ( _frequency > 19 )
			NextThink = (float)(Server.Globals.Time + 0.5);
		else
			NextThink = (float)(Server.Globals.Time + 2.5 - (0.1 * _frequency));
	}
}

[LinkEntityToClass( "env_blood" )]
public class Blood : PointEntity
{
	private const int SpawnFlagRandom = 0x0001;
	private const int SpawnFlagStream = 0x0002;
	private const int SpawnFlagPlayer = 0x0004;
	private const int SpawnFlagDecal = 0x0008;

	private int Color => Impulse;

	private float BloodAmount => Damage;

	public override void Spawn()
	{
		Solid = Solid.Not;
		MoveType = MoveType.None;
		Effects = 0;
		Frame = 0f;
		SetMoveDir();
	}

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "color":
				Impulse = Util.Atoi( value ) == 1 ? BloodColorYellow : BloodColorRed;
				return true;
			case "amount":
				Damage = Util.Atof( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	private Vector3 Direction() => (SpawnFlags & SpawnFlagRandom) != 0 ? Util.RandomBloodVector() : MoveDir;

	private Vector3 BloodPosition( BaseEntity activator )
	{
		if ( (SpawnFlags & SpawnFlagPlayer) == 0 )
			return Origin;

		var player = activator is not null && activator.IsPlayer ? activator : Server.EntityOfEntIndex( 1 );

		if ( player is null )
			return Origin;

		return player.Origin + player.ViewOffset + new Vector3( Server.RandomFloat( -10f, 10f ), Server.RandomFloat( -10f, 10f ), Server.RandomFloat( -10f, 10f ) );
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( (SpawnFlags & SpawnFlagStream) != 0 )
			Util.BloodStream( BloodPosition( activator ), Direction(), Color == BloodColorRed ? 70 : Color, (int)BloodAmount );
		else
			Util.BloodDrips( BloodPosition( activator ), Direction(), Color, (int)BloodAmount );

		if ( (SpawnFlags & SpawnFlagDecal) == 0 )
			return;

		var forward = Direction();
		var start = BloodPosition( activator );
		var trace = Util.TraceLine( start, start + (forward * BloodAmount * 2f), IgnoreMonsters, null );

		if ( trace.Fraction != 1.0 )
			Util.BloodDecalTrace( trace, Color );
	}
}

[LinkEntityToClass( "env_funnel" )]
public class EnvFunnel : BaseDelay
{
	private const int SpawnFlagReverse = 1;

	private int _sprite;

	public override void Precache()
	{
		_sprite = Server.PrecacheModel( "sprites/flare6.spr" );
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
		Server.WriteByte( TempEntity.LargeFunnel );
		Server.WriteCoord( Origin.x );
		Server.WriteCoord( Origin.y );
		Server.WriteCoord( Origin.z );
		Server.WriteShort( _sprite );
		Server.WriteShort( (SpawnFlags & SpawnFlagReverse) != 0 ? 1 : 0 );
		Server.MessageEnd();

		SetThink( SubRemove );
		NextThink = Server.Globals.Time;
	}

	public override void Spawn()
	{
		Precache();
		Solid = Solid.Not;
		Effects = Effects.NoDraw;
	}
}

[LinkEntityToClass( "env_beverage" )]
public class EnvBeverage : BaseDelay
{
	public override void Precache()
	{
		Server.PrecacheModel( "models/can.mdl" );
		Server.PrecacheSound( "weapons/g_bounce3.wav" );
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( Frags != 0f || Health <= 0f )
			return;

		var can = Create( "item_sodacan", Origin, Angles, this );

		can.Skin = Skin == 6 ? Server.RandomLong( 0, 5 ) : Skin;

		Frags = 1f;
		Health--;
	}

	public override void Spawn()
	{
		Precache();
		Solid = Solid.Not;
		Effects = Effects.NoDraw;
		Frags = 0f;

		if ( Health == 0f )
			Health = 10f;
	}
}

[LinkEntityToClass( "item_sodacan" )]
public class ItemSoda : BaseEntity
{
	public override void Spawn()
	{
		Precache();
		Solid = Solid.Not;
		MoveType = MoveType.Toss;

		SetModel( "models/can.mdl" );
		SetSize( Vector3.Zero, Vector3.Zero );

		SetThink( CanThink );
		NextThink = (float)(Server.Globals.Time + 0.5);
	}

	public void CanThink()
	{
		Sound.Emit( this, SoundChannel.Weapon, "weapons/g_bounce3.wav", 1f, Server.AttenuationNormal );

		Solid = Solid.Trigger;
		SetSize( new Vector3( -8f, -8f, 0f ), new Vector3( 8f, 8f, 8f ) );
		SetThink( null );
		SetTouch( CanTouch );
	}

	public void CanTouch( BaseEntity other )
	{
		if ( !other.IsPlayer )
			return;

		other.TakeHealth( 1f, DamageGeneric );

		if ( !Util.IsNullEnt( Owner ) )
			Owner.Frags = 0f;

		Solid = Solid.Not;
		MoveType = MoveType.None;
		Effects = Effects.NoDraw;
		SetTouch( null );
		SetThink( SubRemove );
		NextThink = Server.Globals.Time;
	}
}
