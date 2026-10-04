using System;
using GoldSrc;

namespace HalfLife;

public class BasePlatTrain : BaseToggle
{
	private const int SpawnFlagToggle = 0x0001;

	private static readonly string[] MoveSounds =
	[
		"plats/bigmove1.wav",
		"plats/bigmove2.wav",
		"plats/elevmove1.wav",
		"plats/elevmove2.wav",
		"plats/elevmove3.wav",
		"plats/freightmove1.wav",
		"plats/freightmove2.wav",
		"plats/heavymove1.wav",
		"plats/rackmove1.wav",
		"plats/railmove1.wav",
		"plats/squeekmove1.wav",
		"plats/talkmove1.wav",
		"plats/talkmove2.wav"
	];

	private static readonly string[] StopSounds =
	[
		"plats/bigstop1.wav",
		"plats/bigstop2.wav",
		"plats/freightstop1.wav",
		"plats/heavystop2.wav",
		"plats/rackstop1.wav",
		"plats/railstop1.wav",
		"plats/squeekstop1.wav",
		"plats/talkstop1.wav"
	];

	[Save( FieldType.Character )]
	public byte MoveSound { get; set; }
	[Save( FieldType.Character )]
	public byte StopSound { get; set; }
	[Save( FieldType.Float )]
	public float Volume { get; set; }

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public virtual bool IsTogglePlat => (SpawnFlags & SpawnFlagToggle) != 0;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "lip":
				Lip = Util.Atof( value );
				return true;
			case "wait":
				Wait = Util.Atof( value );
				return true;
			case "height":
				Height = Util.Atof( value );
				return true;
			case "rotation":
				FinalAngle = new Vector3( Util.Atof( value ), FinalAngle.y, FinalAngle.z );
				return true;
			case "movesnd":
				MoveSound = (byte)Util.Atof( value );
				return true;
			case "stopsnd":
				StopSound = (byte)Util.Atof( value );
				return true;
			case "volume":
				Volume = Util.Atof( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Precache()
	{
		if ( MoveSound >= 1 && MoveSound <= MoveSounds.Length )
		{
			Noise = MoveSounds[MoveSound - 1];
			Server.PrecacheSound( Noise );
		}
		else
		{
			Noise = "common/null.wav";
		}

		if ( StopSound >= 1 && StopSound <= StopSounds.Length )
		{
			Noise1 = StopSounds[StopSound - 1];
			Server.PrecacheSound( Noise1 );
		}
		else
		{
			Noise1 = "common/null.wav";
		}
	}
}

[LinkEntityToClass( "func_plat" )]
public class FuncPlat : BasePlatTrain
{
	public override void Spawn()
	{
		Setup();

		Precache();

		if ( !string.IsNullOrEmpty( TargetName ) )
		{
			SetOrigin( Position1 );
			ToggleState = ToggleState.AtTop;
			SetUse( PlatUse );
		}
		else
		{
			SetOrigin( Position2 );
			ToggleState = ToggleState.AtBottom;
		}
	}

	public override void Precache()
	{
		base.Precache();

		if ( !IsTogglePlat )
			Server.CreateEntity<PlatTrigger>().SpawnInsideTrigger( this );
	}

	public override void Blocked( BaseEntity other )
	{
		Util.AiAlert( $"{ClassName} Blocked by {other.ClassName}" );

		other.TakeDamage( this, this, 1f, DamageCrush );

		if ( Noise is not null )
			Server.StopSound( this, SoundChannel.Static, Noise );

		if ( ToggleState == ToggleState.GoingUp )
			GoDown();
		else if ( ToggleState == ToggleState.GoingDown )
			GoUp();
	}

	public virtual void GoUp() => PlatGoUp();

	public virtual void GoDown() => PlatGoDown();

	public virtual void HitTop()
	{
		if ( Noise is not null )
			Server.StopSound( this, SoundChannel.Static, Noise );

		if ( Noise1 is not null )
			Server.EmitSound( this, SoundChannel.Weapon, Noise1, Volume, Server.AttenuationNormal );

		ToggleState = ToggleState.AtTop;

		if ( !IsTogglePlat )
		{
			SetThink( CallGoDown );
			NextThink = LTime + 3f;
		}
	}

	public virtual void HitBottom()
	{
		if ( Noise is not null )
			Server.StopSound( this, SoundChannel.Static, Noise );

		if ( Noise1 is not null )
			Server.EmitSound( this, SoundChannel.Weapon, Noise1, Volume, Server.AttenuationNormal );

		ToggleState = ToggleState.AtBottom;
	}

	protected void Setup()
	{
		if ( TLength == 0f )
			TLength = 80f;

		if ( TWidth == 0f )
			TWidth = 10f;

		Angles = Vector3.Zero;

		Solid = Solid.Bsp;
		MoveType = MoveType.Push;

		SetOrigin( Origin );
		SetSize( Mins, Maxs );
		SetModel( ModelName );

		Position1 = Origin;

		if ( Height != 0f )
			Position2 = new Vector3( Origin.x, Origin.y, Origin.z - Height );
		else
			Position2 = new Vector3( Origin.x, Origin.y, Origin.z - Size.z + 8f );

		if ( Speed == 0f )
			Speed = 150f;

		if ( Volume == 0f )
			Volume = 0.85f;
	}

	protected void PlatGoUp()
	{
		if ( Noise is not null )
			Server.EmitSound( this, SoundChannel.Static, Noise, Volume, Server.AttenuationNormal );

		ToggleState = ToggleState.GoingUp;
		SetMoveDone( CallHitTop );
		LinearMove( Position1, Speed );
	}

	protected void PlatGoDown()
	{
		if ( Noise is not null )
			Server.EmitSound( this, SoundChannel.Static, Noise, Volume, Server.AttenuationNormal );

		ToggleState = ToggleState.GoingDown;
		SetMoveDone( CallHitBottom );
		LinearMove( Position2, Speed );
	}

	protected void CallGoDown() => GoDown();

	protected void CallHitTop() => HitTop();

	protected void CallHitBottom() => HitBottom();

	private void PlatUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( IsTogglePlat )
		{
			if ( !ShouldToggle( useType, ToggleState == ToggleState.AtBottom ) )
				return;

			if ( ToggleState == ToggleState.AtTop )
				GoDown();
			else if ( ToggleState == ToggleState.AtBottom )
				GoUp();
		}
		else
		{
			SetUse( null );

			if ( ToggleState == ToggleState.AtTop )
				GoDown();
		}
	}
}

public class PlatTrigger : BaseEntity
{
	private FuncPlat _platform;

	public override ObjectCaps ObjectCaps => (base.ObjectCaps & ~ObjectCaps.AcrossTransition) | ObjectCaps.DontSave;

	public void SpawnInsideTrigger( FuncPlat platform )
	{
		_platform = platform;

		Solid = Solid.Trigger;
		MoveType = MoveType.None;
		Origin = platform.Origin;

		var min = platform.Mins + new Vector3( 25f, 25f, 0f );
		var max = platform.Maxs + new Vector3( 25f, 25f, 8f );

		min.z = max.z - (platform.Position1.z - platform.Position2.z + 8f);

		if ( platform.Size.x <= 50f )
		{
			min.x = (platform.Mins.x + platform.Maxs.x) / 2f;
			max.x = min.x + 1f;
		}

		if ( platform.Size.y <= 50f )
		{
			min.y = (platform.Mins.y + platform.Maxs.y) / 2f;
			max.y = min.y + 1f;
		}

		SetSize( min, max );
	}

	public override void Touch( BaseEntity other )
	{
		if ( other.ClassName != "player" )
			return;

		if ( !other.IsAlive || _platform is null || !_platform.IsValid() )
			return;

		if ( _platform.ToggleState == ToggleState.AtBottom )
			_platform.GoUp();
		else if ( _platform.ToggleState == ToggleState.AtTop )
			_platform.NextThink = _platform.LTime + 1f;
	}
}

[LinkEntityToClass( "func_platrot" )]
public class FuncPlatRot : FuncPlat
{
	[Save( FieldType.Vector )]
	protected Vector3 End;
	[Save( FieldType.Vector )]
	protected Vector3 Start;

	public override void Spawn()
	{
		base.Spawn();
		SetupRotation();
	}

	public override void GoDown()
	{
		base.GoDown();
		RotMove( Start, NextThink - LTime );
	}

	public override void HitBottom()
	{
		base.HitBottom();
		AVelocity = Vector3.Zero;
		Angles = Start;
	}

	public override void GoUp()
	{
		base.GoUp();
		RotMove( End, NextThink - LTime );
	}

	public override void HitTop()
	{
		base.HitTop();
		AVelocity = Vector3.Zero;
		Angles = End;
	}

	protected void SetupRotation()
	{
		if ( FinalAngle.x != 0f )
		{
			AxisDir( this );
			Start = Angles;
			End = Angles + (MoveDir * FinalAngle.x);
		}
		else
		{
			Start = Vector3.Zero;
			End = Vector3.Zero;
		}

		if ( !string.IsNullOrEmpty( TargetName ) )
			Angles = End;
	}

	protected void RotMove( Vector3 destAngle, float time )
	{
		var delta = destAngle - Angles;

		if ( time >= 0.1 )
		{
			AVelocity = delta / time;
		}
		else
		{
			AVelocity = delta;
			NextThink = LTime + 1f;
		}
	}
}

[LinkEntityToClass( "func_train" )]
public class FuncTrain : BasePlatTrain
{
	private const int SpawnFlagWaitRetrigger = 1;
	private const int SpawnFlagPassable = 0x0008;

	[Save( FieldType.Integer )]
	public int Sounds { get; set; }

	[Save( FieldType.EntVars )]
	private BaseEntity _currentTarget;
	[Save( FieldType.Boolean )]
	private bool _activated;

	public override bool KeyValue( string key, string value )
	{
		if ( key == "sounds" )
		{
			Sounds = Util.Atoi( value );
			return true;
		}

		return base.KeyValue( key, value );
	}

	public override void Spawn()
	{
		Precache();

		if ( Speed == 0f )
			Speed = 100f;

		if ( string.IsNullOrEmpty( Target ) )
			Log.Info( "FuncTrain with no target" );

		if ( Damage == 0f )
			Damage = 2f;

		MoveType = MoveType.Push;
		Solid = (SpawnFlags & SpawnFlagPassable) != 0 ? Solid.Not : Solid.Bsp;

		SetModel( ModelName );
		SetSize( Mins, Maxs );
		SetOrigin( Origin );

		_activated = false;

		if ( Volume == 0f )
			Volume = 0.85f;
	}

	public override void Activate()
	{
		if ( _activated )
			return;

		_activated = true;

		var target = (Server.FindEntityByString( null, "targetname", Target ) ?? Server.GetEdict( 0 )) as BaseEntity;

		Target = target.Target;
		_currentTarget = target;

		SetOrigin( target.Origin - ((Mins + Maxs) * 0.5f) );

		if ( string.IsNullOrEmpty( TargetName ) )
		{
			NextThink = (float)(LTime + 0.1);
			SetThink( Next );
		}
		else
		{
			SpawnFlags |= SpawnFlagWaitRetrigger;
		}
	}

	public override void OverrideReset()
	{
		if ( (Velocity.x == 0f && Velocity.y == 0f && Velocity.z == 0f) || NextThink == 0f )
			return;

		Target = Message;

		if ( GetNextTarget() is null )
		{
			NextThink = 0f;
			Velocity = Vector3.Zero;
		}
		else
		{
			SetThink( Next );
			NextThink = (float)(LTime + 0.1);
		}
	}

	public override void Blocked( BaseEntity other )
	{
		if ( Server.Globals.Time < ActivateFinished )
			return;

		ActivateFinished = (float)(Server.Globals.Time + 0.5);

		other.TakeDamage( this, this, Damage, DamageCrush );
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( (SpawnFlags & SpawnFlagWaitRetrigger) != 0 )
		{
			SpawnFlags &= ~SpawnFlagWaitRetrigger;
			Next();
			return;
		}

		SpawnFlags |= SpawnFlagWaitRetrigger;

		if ( Enemy is not null )
			Target = Enemy.TargetName;

		NextThink = 0f;
		Velocity = Vector3.Zero;

		if ( Noise1 is not null )
			Server.EmitSound( this, SoundChannel.Voice, Noise1, Volume, Server.AttenuationNormal );
	}

	private void TrainWait()
	{
		if ( _currentTarget.Message is not null )
		{
			FireTargets( _currentTarget.Message, this, this, UseType.Toggle, 0f );

			if ( (_currentTarget.SpawnFlags & PathCorner.SpawnFlagFireOnce) != 0 )
				_currentTarget.Message = null;
		}

		if ( (_currentTarget.SpawnFlags & SpawnFlagWaitRetrigger) != 0 || (SpawnFlags & SpawnFlagWaitRetrigger) != 0 )
		{
			SpawnFlags |= SpawnFlagWaitRetrigger;

			StopMoving();
			NextThink = 0f;
			return;
		}

		if ( Wait != 0f )
		{
			NextThink = LTime + Wait;

			StopMoving();
			SetThink( Next );
		}
		else
		{
			Next();
		}
	}

	private void Next()
	{
		var target = GetNextTarget();

		if ( target is null )
		{
			StopMoving();
			return;
		}

		Message = Target;

		Target = target.Target;
		Wait = target.GetDelay();

		if ( _currentTarget is not null && _currentTarget.Speed != 0f )
		{
			Speed = _currentTarget.Speed;
			Util.AiAlert( $"Train {TargetName} speed to {Speed,4:F2}" );
		}

		_currentTarget = target;

		Enemy = target;

		if ( (_currentTarget.SpawnFlags & PathCorner.SpawnFlagTeleport) != 0 )
		{
			Effects |= Effects.NoInterp;
			SetOrigin( target.Origin - ((Mins + Maxs) * 0.5f) );
			TrainWait();
		}
		else
		{
			if ( Noise is not null )
			{
				Server.StopSound( this, SoundChannel.Static, Noise );
				Server.EmitSound( this, SoundChannel.Static, Noise, Volume, Server.AttenuationNormal );
			}

			Effects &= ~Effects.NoInterp;
			SetMoveDone( TrainWait );
			LinearMove( target.Origin - ((Mins + Maxs) * 0.5f), Speed );
		}
	}

	private void StopMoving()
	{
		if ( Noise is not null )
			Server.StopSound( this, SoundChannel.Static, Noise );

		if ( Noise1 is not null )
			Server.EmitSound( this, SoundChannel.Voice, Noise1, Volume, Server.AttenuationNormal );
	}
}

[LinkEntityToClass( "func_tracktrain" )]
public class FuncTrackTrain : BaseEntity
{
	public const int SpawnFlagNoPitch = 0x0001;
	public const int SpawnFlagNoControl = 0x0002;
	public const int SpawnFlagForwardOnly = 0x0004;
	public const int SpawnFlagPassable = 0x0008;

	private const int TrainStartPitch = 60;
	private const int TrainMaxPitch = 200;
	private const int TrainMaxSpeed = 1000;

	[Save( FieldType.ClassPtr )]
	public PathTrack Path { get; set; }
	[Save( FieldType.Float )]
	public float Length { get; set; }
	[Save( FieldType.Float )]
	public float Height { get; set; }
	[Save( FieldType.Float )]
	public float StartSpeed { get; set; }
	[Save( FieldType.Integer )]
	public int Sounds { get; set; }
	[Save( FieldType.Float )]
	public float Volume { get; set; }
	[Save( FieldType.Float )]
	public float Bank { get; set; }

	[Save( FieldType.Float )]
	private float _speed;
	[Save( FieldType.Float )]
	private float _dir;
	[Save( FieldType.Float )]
	private float _oldSpeed;
	[Save( FieldType.Vector )]
	private Vector3 _controlMins;
	[Save( FieldType.Vector )]
	private Vector3 _controlMaxs;
	private bool _soundPlaying;
	private int _adjustPitch;

	public override ObjectCaps ObjectCaps => (base.ObjectCaps & ~ObjectCaps.AcrossTransition) | ObjectCaps.DirectionalUse;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "wheels":
				Length = Util.Atof( value );
				return true;
			case "height":
				Height = Util.Atof( value );
				return true;
			case "startspeed":
				StartSpeed = Util.Atof( value );
				return true;
			case "sounds":
				Sounds = Util.Atoi( value );
				return true;
			case "volume":
				Volume = Util.Atoi( value );
				Volume = (float)(Volume * 0.1);
				return true;
			case "bank":
				Bank = Util.Atof( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Spawn()
	{
		_speed = Speed == 0f ? 100f : Speed;

		Speed = 0f;
		Velocity = Vector3.Zero;
		AVelocity = Vector3.Zero;
		Impulse = (int)_speed;

		_dir = 1f;

		if ( string.IsNullOrEmpty( Target ) )
			Log.Info( "FuncTrain with no target" );

		Solid = (SpawnFlags & SpawnFlagPassable) != 0 ? Solid.Not : Solid.Bsp;
		MoveType = MoveType.Push;

		SetModel( ModelName );

		SetSize( Mins, Maxs );
		SetOrigin( Origin );

		OldOrigin = Origin;

		_controlMins = Mins;
		_controlMaxs = Maxs;
		_controlMaxs.z += 72f;

		SetNextThink( (float)(LTime + 0.1), false );
		SetThink( Find );
		Precache();
	}

	public override void Precache()
	{
		if ( Volume == 0f )
			Volume = 1f;

		Noise = Sounds switch
		{
			>= 1 and <= 4 => $"plats/ttrain{Sounds}.wav",
			5 => "plats/ttrain6.wav",
			6 => "plats/ttrain7.wav",
			_ => null
		};

		if ( Noise is not null )
			Server.PrecacheSound( Noise );

		Server.PrecacheSound( "plats/ttrain_brake1.wav" );
		Server.PrecacheSound( "plats/ttrain_start1.wav" );

		_adjustPitch = Server.PrecacheEvent( 1, "events/train.sc" );
	}

	public override void Blocked( BaseEntity other )
	{
		if ( (other.EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround && other.GroundEntity == this )
		{
			var deltaSpeed = MathF.Abs( Speed );
			if ( deltaSpeed > 50f )
				deltaSpeed = 50f;

			if ( other.Velocity.z == 0f )
				other.Velocity += new Vector3( 0f, 0f, deltaSpeed );

			return;
		}

		other.Velocity = Util.Normalize( other.Origin - Origin ) * Damage;

		if ( Damage <= 0f )
			return;

		other.TakeDamage( this, this, Damage, DamageCrush );
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( useType != UseType.Set )
		{
			if ( !ShouldToggle( useType, Speed != 0f ) )
				return;

			if ( Speed == 0f )
			{
				Speed = _speed * _dir;
				Next();
			}
			else
			{
				Speed = 0f;
				Velocity = Vector3.Zero;
				AVelocity = Vector3.Zero;
				StopSound();
				SetThink( null );
			}

			return;
		}

		var delta = (float)(((int)(Speed * 4f) / (int)_speed * 0.25) + (0.25 * value));

		if ( delta > 1f )
			delta = 1f;
		else if ( delta < -1f )
			delta = -1f;

		if ( (SpawnFlags & SpawnFlagForwardOnly) != 0 && delta < 0f )
			delta = 0f;

		Speed = _speed * delta;
		Next();
		Util.AiAlert( $"TRAIN({TargetName}), speed to {Speed:F2}" );
	}

	public override void OverrideReset()
	{
		SetNextThink( (float)(LTime + 0.1), false );
		SetThink( NearestPath );
	}

	public override bool OnControls( BaseEntity entity )
	{
		var offset = entity.Origin - Origin;

		if ( (SpawnFlags & SpawnFlagNoControl) != 0 )
			return false;

		Server.Globals.MakeVectors( Angles );

		var local = new Vector3(
			Vector3.Dot( offset, Server.Globals.Forward ),
			-Vector3.Dot( offset, Server.Globals.Right ),
			Vector3.Dot( offset, Server.Globals.Up ) );

		return local.x >= _controlMins.x && local.y >= _controlMins.y && local.z >= _controlMins.z
			&& local.x <= _controlMaxs.x && local.y <= _controlMaxs.y && local.z <= _controlMaxs.z;
	}

	public void SetNextThink( float thinkTime, bool alwaysThink )
	{
		if ( alwaysThink )
			EntityFlags |= EntityFlags.AlwaysThink;
		else
			EntityFlags &= ~EntityFlags.AlwaysThink;

		NextThink = thinkTime;
	}

	public void SetTrack( PathTrack track )
	{
		Path = track.Nearest( Origin );
	}

	public void SetControls( BaseEntity controls )
	{
		var offset = controls.Origin - OldOrigin;

		_controlMins = controls.Mins + offset;
		_controlMaxs = controls.Maxs + offset;
	}

	public static FuncTrackTrain Instance( Edict edict )
	{
		return edict is FuncTrackTrain train && train.ClassName == "func_tracktrain" ? train : null;
	}

	private void StopSound()
	{
		if ( _soundPlaying && Noise is not null )
		{
			var encoded = (ushort)(((ushort)Sounds & 0x0007) << 12);

			Server.PlaybackEvent( EventFlags.Reliable | EventFlags.Update, this, _adjustPitch, 0f, Vector3.Zero, Vector3.Zero, 0f, 0f, encoded, 0, 1, 0 );
			Server.EmitSound( this, SoundChannel.Item, "plats/ttrain_brake1.wav", Volume, Server.AttenuationNormal, 0, 100 );
		}

		_soundPlaying = false;
	}

	private void UpdateSound()
	{
		if ( Noise is null )
			return;

		var pitch = TrainStartPitch + (MathF.Abs( Speed ) * (TrainMaxPitch - TrainStartPitch) / TrainMaxSpeed);

		if ( !_soundPlaying )
		{
			Server.EmitSound( this, SoundChannel.Item, "plats/ttrain_start1.wav", Volume, Server.AttenuationNormal, 0, 100 );
			Server.EmitSound( this, SoundChannel.Static, Noise, Volume, Server.AttenuationNormal, 0, (int)pitch );
			_soundPlaying = true;
			return;
		}

		var encodedSound = ((ushort)Sounds & 0x0007) << 12;
		var encodedPitch = ((ushort)(pitch / 10.0) & 0x003f) << 6;
		var encodedVolume = (ushort)(Volume * 40.0) & 0x003f;

		Server.PlaybackEvent( EventFlags.Reliable | EventFlags.Update, this, _adjustPitch, 0f, Vector3.Zero, Vector3.Zero, 0f, 0f, (ushort)(encodedSound | encodedPitch | encodedVolume), 0, 0, 0 );
	}

	private void Next()
	{
		var time = 0.5f;

		if ( Speed == 0f )
		{
			Util.AiAlert( $"TRAIN({TargetName}): Speed is 0" );
			StopSound();
			return;
		}

		if ( Path is null )
		{
			Util.AiAlert( $"TRAIN({TargetName}): Lost path" );
			StopSound();
			return;
		}

		UpdateSound();

		var nextPos = Origin;

		nextPos.z -= Height;
		var next = Path.LookAhead( ref nextPos, (float)(Speed * 0.1), true );
		nextPos.z += Height;

		Velocity = (nextPos - Origin) * 10f;

		var nextFront = Origin;

		nextFront.z -= Height;
		Path.LookAhead( ref nextFront, Length > 0f ? Length : 100f, false );
		nextFront.z += Height;

		var delta = nextFront - Origin;
		var angles = MathLib.VectorAngles( delta );

		angles.y += 180f;

		angles = FixupAngles( angles );
		Angles = FixupAngles( Angles );

		if ( next is null || (delta.x == 0f && delta.y == 0f) )
			angles = Angles;

		var vx = (SpawnFlags & SpawnFlagNoPitch) == 0 ? Util.AngleDistance( angles.x, Angles.x ) : 0f;
		var vy = Util.AngleDistance( angles.y, Angles.y );

		var angularVelocity = new Vector3( vx * 10f, vy * 10f, AVelocity.z );

		if ( Bank != 0f )
		{
			if ( angularVelocity.y < -5f )
				angularVelocity.z = Util.AngleDistance( Util.ApproachAngle( -Bank, Angles.z, Bank * 2f ), Angles.z );
			else if ( angularVelocity.y > 5f )
				angularVelocity.z = Util.AngleDistance( Util.ApproachAngle( Bank, Angles.z, Bank * 2f ), Angles.z );
			else
				angularVelocity.z = Util.AngleDistance( Util.ApproachAngle( 0f, Angles.z, Bank * 4f ), Angles.z ) * 4f;
		}

		AVelocity = angularVelocity;

		if ( next is not null )
		{
			if ( next != Path )
			{
				var fire = Speed >= 0f ? next : Path;

				Path = next;

				if ( fire.Message is not null )
				{
					FireTargets( fire.Message, this, this, UseType.Toggle, 0f );

					if ( (fire.SpawnFlags & PathTrack.SpawnFlagFireOnce) != 0 )
						fire.Message = null;
				}

				if ( (fire.SpawnFlags & PathTrack.SpawnFlagDisableTrain) != 0 )
					SpawnFlags |= SpawnFlagNoControl;

				if ( (SpawnFlags & SpawnFlagNoControl) != 0 && fire.Speed != 0f )
				{
					Speed = fire.Speed;
					Util.AiAlert( $"TrackTrain {TargetName} speed to {Speed,4:F2}" );
				}
			}

			SetThink( Next );
			SetNextThink( LTime + time, true );
			return;
		}

		StopSound();

		Velocity = nextPos - Origin;
		AVelocity = Vector3.Zero;

		var distance = MathLib.Length( Velocity );
		_oldSpeed = Speed;

		Speed = 0f;

		if ( distance > 0f )
		{
			time = distance / _oldSpeed;
			Velocity *= _oldSpeed / distance;
			SetThink( DeadEnd );
			SetNextThink( LTime + time, false );
		}
		else
		{
			DeadEnd();
		}
	}

	private void NearestPath()
	{
		BaseEntity nearest = null;
		var closest = 1024f;

		Edict found = null;

		while ( (found = Server.FindEntityInSphere( found, Origin, 1024f )) is not null )
		{
			if ( (found.EntityFlags & (EntityFlags.Client | EntityFlags.Monster)) != 0 || found.ClassName != "path_track" )
				continue;

			var dist = MathLib.Length( Origin - found.Origin );

			if ( dist < closest )
			{
				closest = dist;
				nearest = found as BaseEntity;
			}
		}

		if ( nearest is null )
		{
			Log.Info( "Can't find a nearby track !!!" );
			SetThink( null );
			return;
		}

		if ( ((PathTrack)nearest).GetNext() is { } next
			&& MathLib.Length( Origin - next.Origin ) < MathLib.Length( Origin - nearest.Origin ) )
		{
			nearest = next;
		}

		Path = (PathTrack)nearest;

		if ( Speed != 0f )
		{
			SetNextThink( (float)(LTime + 0.1), false );
			SetThink( Next );
		}
	}

	private void DeadEnd()
	{
		var track = Path;

		Util.AiAlert( $"TRAIN({TargetName}): Dead end " );

		if ( track is not null )
		{
			PathTrack next;

			do
			{
				next = PathTrack.ValidPath( _oldSpeed < 0f ? track.GetPrevious() : track.GetNext(), true );

				if ( next is not null )
					track = next;
			}
			while ( next is not null );
		}

		Velocity = Vector3.Zero;
		AVelocity = Vector3.Zero;

		if ( track is not null )
		{
			Util.AiAlert( $"at {track.TargetName}" );

			if ( track.NetName is not null )
				FireTargets( track.NetName, this, this, UseType.Toggle, 0f );
		}
		else
		{
			Util.AiAlert( "" );
		}
	}

	private void Find()
	{
		Path = PathTrack.Instance( Server.FindEntityByString( null, "targetname", Target ) );

		if ( Path is null )
			return;

		var nextPos = Path.Origin;
		nextPos.z += Height;

		var look = nextPos;

		look.z -= Height;
		Path.LookAhead( ref look, Length, false );
		look.z += Height;

		var angles = MathLib.VectorAngles( look - nextPos );
		angles.y += 180f;

		if ( (SpawnFlags & SpawnFlagNoPitch) != 0 )
			angles.x = 0f;

		Angles = angles;

		SetOrigin( nextPos );
		SetNextThink( (float)(LTime + 0.1), false );
		SetThink( Next );
		Speed = StartSpeed;

		UpdateSound();
	}

	private static Vector3 FixupAngles( Vector3 angles )
	{
		return new Vector3( Fix( angles.x ), Fix( angles.y ), Fix( angles.z ) );
	}

	private static float Fix( float angle )
	{
		while ( angle < 0f )
			angle += 360f;

		while ( angle > 360f )
			angle -= 360f;

		return angle;
	}
}

[LinkEntityToClass( "func_traincontrols" )]
public class FuncTrainControls : BaseEntity
{
	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override void Spawn()
	{
		Solid = Solid.Not;
		MoveType = MoveType.None;
		SetModel( ModelName );

		SetSize( Mins, Maxs );
		SetOrigin( Origin );

		SetThink( Find );
		NextThink = Server.Globals.Time;
	}

	private void Find()
	{
		Edict target = null;

		do
		{
			target = Server.FindEntityByString( target, "targetname", Target );
		}
		while ( target is not null && target.ClassName != "func_tracktrain" );

		if ( target is null )
		{
			Log.Info( $"No train {Target}" );
			return;
		}

		FuncTrackTrain.Instance( target ).SetControls( this );
		Remove();
	}
}

[LinkEntityToClass( "func_trackchange" )]
public class FuncTrackChange : FuncPlatRot
{
	protected enum TrainCode
	{
		Safe,
		Blocking,
		Following
	}

	private const int SpawnFlagStartBottom = 0x00000008;
	private const int SpawnFlagDontMove = 0x00000010;

	[Save( FieldType.String, Global = true )]
	public string TrainName { get; set; }
	[Save( FieldType.String, Global = true )]
	public string TrackTopName { get; set; }
	[Save( FieldType.String, Global = true )]
	public string TrackBottomName { get; set; }

	[Save( FieldType.ClassPtr, Global = true )]
	protected PathTrack TrackTop;
	[Save( FieldType.ClassPtr, Global = true )]
	protected PathTrack TrackBottom;
	[Save( FieldType.ClassPtr, Global = true )]
	protected FuncTrackTrain Train;
	[Save( FieldType.Integer )]
	protected TrainCode Code;
	[Save( FieldType.Integer )]
	protected ToggleState TargetState;
	[Save( FieldType.Integer )]
	protected bool UseEnabled;

	public override bool IsTogglePlat => true;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "train":
				TrainName = Server.AllocString( value );
				return true;
			case "toptrack":
				TrackTopName = Server.AllocString( value );
				return true;
			case "bottomtrack":
				TrackBottomName = Server.AllocString( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Spawn()
	{
		Setup();

		if ( (SpawnFlags & SpawnFlagDontMove) != 0 )
			Position2 = new Vector3( Position2.x, Position2.y, Origin.z );

		SetupRotation();

		if ( (SpawnFlags & SpawnFlagStartBottom) != 0 )
		{
			SetOrigin( Position2 );
			ToggleState = ToggleState.AtBottom;
			Angles = Start;
			TargetState = ToggleState.AtTop;
		}
		else
		{
			SetOrigin( Position1 );
			ToggleState = ToggleState.AtTop;
			Angles = End;
			TargetState = ToggleState.AtBottom;
		}

		UseEnabled = true;
		NextThink = LTime + 2f;
		SetThink( Find );
		Precache();
	}

	public override void Precache()
	{
		Server.PrecacheSound( "buttons/button11.wav" );

		base.Precache();
	}

	public override void Touch( BaseEntity other )
	{
	}

	public override void OverrideReset()
	{
		NextThink = (float)(LTime + 1.0);
		SetThink( Find );
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( ToggleState is not (ToggleState.AtTop or ToggleState.AtBottom) )
			return;

		Code = EvaluateTrain( ToggleState == ToggleState.AtTop ? TrackTop : TrackBottom );

		if ( Code == TrainCode.Blocking )
		{
			Server.EmitSound( this, SoundChannel.Voice, "buttons/button11.wav", 1f, Server.AttenuationNormal );
			return;
		}

		UseEnabled = false;

		if ( ToggleState == ToggleState.AtTop )
			GoDown();
		else
			GoUp();
	}

	public override void GoDown()
	{
		if ( Code == TrainCode.Blocking )
			return;

		UpdateAutoTargets( ToggleState.GoingDown );

		if ( (SpawnFlags & SpawnFlagDontMove) != 0 )
		{
			SetMoveDone( CallHitBottom );
			ToggleState = ToggleState.GoingDown;
			AngularMove( Start, Speed );
		}
		else
		{
			PlatGoDown();
			SetMoveDone( CallHitBottom );
			RotMove( Start, NextThink - LTime );
		}

		if ( Code == TrainCode.Following )
		{
			UpdateTrain( Start );
			Train.Path = null;
		}
	}

	public override void GoUp()
	{
		if ( Code == TrainCode.Blocking )
			return;

		UpdateAutoTargets( ToggleState.GoingUp );

		if ( (SpawnFlags & SpawnFlagDontMove) != 0 )
		{
			ToggleState = ToggleState.GoingUp;
			SetMoveDone( CallHitTop );
			AngularMove( End, Speed );
		}
		else
		{
			PlatGoUp();
			SetMoveDone( CallHitTop );
			RotMove( End, NextThink - LTime );
		}

		if ( Code == TrainCode.Following )
		{
			UpdateTrain( End );
			Train.Path = null;
		}
	}

	public override void HitBottom()
	{
		base.HitBottom();

		if ( Code == TrainCode.Following )
			Train.SetTrack( TrackBottom );

		SetThink( null );
		NextThink = -1f;

		UpdateAutoTargets( ToggleState );

		UseEnabled = true;
	}

	public override void HitTop()
	{
		base.HitTop();

		if ( Code == TrainCode.Following )
			Train.SetTrack( TrackTop );

		SetThink( null );
		NextThink = -1f;

		UpdateAutoTargets( ToggleState );

		UseEnabled = true;
	}

	protected virtual void UpdateAutoTargets( ToggleState toggleState )
	{
		if ( TrackTop is null || TrackBottom is null )
			return;

		if ( toggleState == ToggleState.AtTop )
			TrackTop.SpawnFlags &= ~PathTrack.SpawnFlagDisabled;
		else
			TrackTop.SpawnFlags |= PathTrack.SpawnFlagDisabled;

		if ( toggleState == ToggleState.AtBottom )
			TrackBottom.SpawnFlags &= ~PathTrack.SpawnFlagDisabled;
		else
			TrackBottom.SpawnFlags |= PathTrack.SpawnFlagDisabled;
	}

	protected TrainCode EvaluateTrain( PathTrack current )
	{
		if ( current is null || Train is null )
			return TrainCode.Safe;

		if ( Train.Path == current
			|| (current.PreviousTrack is not null && Train.Path == current.PreviousTrack)
			|| (current.NextTrack is not null && Train.Path == current.NextTrack) )
		{
			if ( Train.Speed != 0f )
				return TrainCode.Blocking;

			var dist = Origin - Train.Origin;
			var length = MathF.Sqrt( (dist.x * dist.x) + (dist.y * dist.y) );

			if ( length < Train.Length )
				return TrainCode.Following;

			if ( length > 150f + Train.Length )
				return TrainCode.Safe;

			return TrainCode.Blocking;
		}

		return TrainCode.Safe;
	}

	private void Find()
	{
		if ( Server.FindEntityByString( null, "targetname", TrackTopName ) is not { } top )
		{
			Log.Error( $"Can't find top track for track change! {TrackTopName}" );
			return;
		}

		TrackTop = PathTrack.Instance( top );

		if ( Server.FindEntityByString( null, "targetname", TrackBottomName ) is not { } bottom )
		{
			Log.Error( $"Can't find bottom track for track change! {TrackBottomName}" );
			return;
		}

		TrackBottom = PathTrack.Instance( bottom );

		Train = Server.FindEntityByString( null, "targetname", TrainName ) is { } train ? FuncTrackTrain.Instance( train ) : null;

		if ( Train is null )
		{
			Log.Error( $"Can't find train for track change! {TrainName}" );
			return;
		}

		var center = (AbsMin + AbsMax) * 0.5f;

		TrackBottom = TrackBottom.Nearest( center );
		TrackTop = TrackTop.Nearest( center );

		UpdateAutoTargets( ToggleState );
		SetThink( null );
	}

	private void UpdateTrain( Vector3 dest )
	{
		var time = NextThink - LTime;

		Train.Velocity = Velocity;
		Train.AVelocity = AVelocity;
		Train.SetNextThink( Train.LTime + time, false );

		if ( time <= 0f )
			return;

		var offset = Train.Origin - Origin;
		var delta = dest - Angles;

		Util.MakeInvVectors( delta, Server.Globals );

		var local = new Vector3(
			Vector3.Dot( offset, Server.Globals.Forward ),
			Vector3.Dot( offset, Server.Globals.Right ),
			Vector3.Dot( offset, Server.Globals.Up ) );

		local -= offset;

		Train.Velocity = Velocity + (local * (float)(1.0 / time));
	}
}

[LinkEntityToClass( "func_trackautochange" )]
public class FuncTrackAuto : FuncTrackChange
{
	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( !UseEnabled )
			return;

		var target = ToggleState switch
		{
			ToggleState.AtTop => TrackTop,
			ToggleState.AtBottom => TrackBottom,
			_ => null
		};

		if ( activator.ClassName == "func_tracktrain" )
		{
			Code = EvaluateTrain( target );

			if ( Code == TrainCode.Following && ToggleState != TargetState )
			{
				UseEnabled = false;

				if ( ToggleState == ToggleState.AtTop )
					GoDown();
				else
					GoUp();
			}

			return;
		}

		target = target?.GetNext();

		if ( target is not null && Train.Path != target && ShouldToggle( useType, TargetState != ToggleState.AtTop ) )
			TargetState = TargetState == ToggleState.AtTop ? ToggleState.AtBottom : ToggleState.AtTop;

		UpdateAutoTargets( TargetState );
	}

	protected override void UpdateAutoTargets( ToggleState toggleState )
	{
		if ( TrackTop is null || TrackBottom is null )
			return;

		PathTrack target;
		PathTrack nextTarget;

		if ( TargetState == ToggleState.AtTop )
		{
			target = TrackTop.GetNext();
			nextTarget = TrackBottom.GetNext();
		}
		else
		{
			target = TrackBottom.GetNext();
			nextTarget = TrackTop.GetNext();
		}

		if ( target is not null )
		{
			target.SpawnFlags &= ~PathTrack.SpawnFlagDisabled;

			if ( Code == TrainCode.Following && Train is not null && Train.Speed == 0f )
				Train.Use( this, this, UseType.On, 0f );
		}

		if ( nextTarget is not null )
			nextTarget.SpawnFlags |= PathTrack.SpawnFlagDisabled;
	}
}
