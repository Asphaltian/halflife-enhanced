using System;
using GoldSrc;

namespace HalfLife;

public enum GlobalEntityState
{
	Off,
	On,
	Dead
}

public sealed class GlobalState
{
	private sealed class GlobalEntity
	{
		[Save( FieldType.Character )]
		public string Name;
		[Save( FieldType.Character )]
		public string LevelName;
		[Save( FieldType.Integer )]
		public GlobalEntityState State;
	}

	private readonly List<GlobalEntity> _entities = [];

	[Save( FieldType.Integer )]
	private int _listCount;

	public bool Contains( string name ) => Find( name ) is not null;

	public void DumpGlobals()
	{
		Util.Alert( AlertType.Console, "-- Globals --\n" );

		foreach ( var entity in _entities )
			Util.Alert( AlertType.Console, $"{entity.Name}: {entity.LevelName} ({entity.State})\n" );
	}

	public void EntityAdd( string name, string mapName, GlobalEntityState state )
	{
		_entities.Insert( 0, new GlobalEntity { Name = name, LevelName = mapName, State = state } );
		_listCount++;
	}

	public void EntitySetState( string name, GlobalEntityState state )
	{
		if ( Find( name ) is { } entity )
			entity.State = state;
	}

	public string EntityLevelName( string name ) => Find( name )?.LevelName;

	public void EntityUpdate( string name, string mapName )
	{
		if ( Find( name ) is { } entity )
			entity.LevelName = mapName;
	}

	public GlobalEntityState EntityGetState( string name )
	{
		return Find( name )?.State ?? GlobalEntityState.Off;
	}

	public void ClearStates()
	{
		_entities.Clear();
		_listCount = 0;
	}

	public bool Save( Save save )
	{
		if ( !save.WriteFields( "GLOBAL", this, typeof( GlobalState ) ) )
			return false;

		foreach ( var entity in _entities )
		{
			if ( !save.WriteFields( "GENT", entity, typeof( GlobalEntity ) ) )
				return false;
		}

		return true;
	}

	public bool Restore( Restore restore )
	{
		ClearStates();

		if ( !restore.ReadFields( "GLOBAL", this, typeof( GlobalState ) ) )
			return false;

		var listCount = _listCount;
		_listCount = 0;

		for ( var i = 0; i < listCount; i++ )
		{
			var entity = new GlobalEntity();

			if ( !restore.ReadFields( "GENT", entity, typeof( GlobalEntity ) ) )
				return false;

			EntityAdd( entity.Name, entity.LevelName, entity.State );
		}

		return true;
	}

	private GlobalEntity Find( string name )
	{
		return _entities.Find( x => x.Name == name );
	}
}

[LinkEntityToClass( "env_global" )]
public class EnvGlobal : PointEntity
{
	private const int SpawnFlagSet = 1;

	[Save( FieldType.String )]
	public string GlobalStateName { get; set; }
	[Save( FieldType.Integer )]
	public int TriggerMode { get; set; }
	[Save( FieldType.Integer )]
	public int InitialState { get; set; }

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "globalstate":
				GlobalStateName = Server.AllocString( value );
				return true;
			case "triggermode":
				TriggerMode = Util.Atoi( value );
				return true;
			case "initialstate":
				InitialState = Util.Atoi( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Spawn()
	{
		if ( GlobalStateName is null )
		{
			Server.FreeEdict( this );
			return;
		}

		if ( (SpawnFlags & SpawnFlagSet) != 0 && !HalfLifeDll.GlobalState.Contains( GlobalStateName ) )
			HalfLifeDll.GlobalState.EntityAdd( GlobalStateName, Server.Globals.MapName, (GlobalEntityState)InitialState );
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		var oldState = HalfLifeDll.GlobalState.EntityGetState( GlobalStateName );

		var newState = TriggerMode switch
		{
			0 => GlobalEntityState.Off,
			1 => GlobalEntityState.On,
			2 => GlobalEntityState.Dead,
			_ => oldState switch
			{
				GlobalEntityState.On => GlobalEntityState.Off,
				GlobalEntityState.Off => GlobalEntityState.On,
				_ => oldState
			}
		};

		if ( HalfLifeDll.GlobalState.Contains( GlobalStateName ) )
			HalfLifeDll.GlobalState.EntitySetState( GlobalStateName, newState );
		else
			HalfLifeDll.GlobalState.EntityAdd( GlobalStateName, Server.Globals.MapName, newState );
	}
}

[LinkEntityToClass( "multisource" )]
public class MultiSource : PointEntity
{
	private const int MaxTargets = 32;
	private const int SpawnFlagInit = 1;

	[Save( FieldType.String )]
	public string GlobalStateName { get; set; }

	[Save( FieldType.EHandle )]
	private readonly BaseEntity[] _entities = new BaseEntity[MaxTargets];

	[Save( FieldType.Integer )]
	private readonly int[] _triggered = new int[MaxTargets];

	[Save( FieldType.Integer )]
	private int _total;

	public override ObjectCaps ObjectCaps => base.ObjectCaps | ObjectCaps.Master;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "style":
			case "height":
			case "killtarget":
			case "value1":
			case "value2":
			case "value3":
				return true;
			case "globalstate":
				GlobalStateName = Server.AllocString( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Spawn()
	{
		Solid = Solid.Not;
		MoveType = MoveType.None;
		NextThink = (float)(Server.Globals.Time + 0.1);
		SpawnFlags |= SpawnFlagInit;
		SetThink( Register );
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		var i = 0;

		while ( i < _total )
		{
			if ( _entities[i++] == caller )
				break;
		}

		if ( i > _total )
		{
			Util.Alert( AlertType.Console, $"MultiSrc:Used by non member {caller.ClassName}.\n" );
			return;
		}

		if ( i > 0 )
			_triggered[i - 1] ^= 1;

		if ( IsTriggered( activator ) )
		{
			Util.Alert( AlertType.AiConsole, $"Multisource {TargetName} enabled ({_total} inputs)\n" );
			UseTargets( null, GlobalStateName is not null ? UseType.On : UseType.Toggle, 0f );
		}
	}

	public override bool IsTriggered( BaseEntity activator )
	{
		if ( (SpawnFlags & SpawnFlagInit) != 0 )
			return false;

		for ( var i = 0; i < _total; i++ )
		{
			if ( _triggered[i] == 0 )
				return false;
		}

		return GlobalStateName is null || HalfLifeDll.GlobalState.EntityGetState( GlobalStateName ) == GlobalEntityState.On;
	}

	private void Register()
	{
		_total = 0;
		Array.Clear( _entities );
		SetThink( null );

		Edict found = null;

		while ( _total < MaxTargets && (found = Server.FindEntityByString( found, "target", TargetName )) is not null )
		{
			if ( found is BaseEntity entity )
				_entities[_total++] = entity;
		}

		found = null;

		while ( _total < MaxTargets && (found = Server.FindEntityByString( found, "classname", "multi_manager" )) is not null )
		{
			if ( found is BaseEntity entity && entity.HasTarget( TargetName ) )
				_entities[_total++] = entity;
		}

		SpawnFlags &= ~SpawnFlagInit;
	}
}

[LinkEntityToClass( "func_button" )]
public class BaseButton : BaseToggle
{
	public override bool IsAllowedToSpeak => true;

	protected const int SpawnFlagDontMove = 1;
	protected const int SpawnFlagToggle = 32;
	protected const int SpawnFlagSparkIfOff = 64;
	protected const int SpawnFlagTouchOnly = 256;

	protected enum ButtonCode
	{
		Nothing,
		Activate,
		Return
	}

	[Save( FieldType.Boolean )]
	public bool StayPushed { get; set; }
	[Save( FieldType.Boolean )]
	public bool Rotating { get; set; }
	[Save( FieldType.String )]
	public string ChangeTarget { get; set; }
	[Save( FieldType.Character )]
	public byte LockedSound { get; set; }
	[Save( FieldType.Character )]
	public byte LockedSentence { get; set; }
	[Save( FieldType.Character )]
	public byte UnlockedSound { get; set; }
	[Save( FieldType.Character )]
	public byte UnlockedSentence { get; set; }
	[Save( FieldType.Integer )]
	public int Sounds { get; set; }

	protected LockSound LockSounds { get; } = new();

	public override ObjectCaps ObjectCaps => (base.ObjectCaps & ~ObjectCaps.AcrossTransition) | (DamageMode != 0f ? 0 : ObjectCaps.ImpulseUse);

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "changetarget":
				ChangeTarget = Server.AllocString( value );
				return true;
			case "locked_sound":
				LockedSound = (byte)Util.Atof( value );
				return true;
			case "locked_sentence":
				LockedSentence = (byte)Util.Atof( value );
				return true;
			case "unlocked_sound":
				UnlockedSound = (byte)Util.Atof( value );
				return true;
			case "unlocked_sentence":
				UnlockedSentence = (byte)Util.Atof( value );
				return true;
			case "sounds":
				Sounds = Util.Atoi( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Precache()
	{
		if ( (SpawnFlags & SpawnFlagSparkIfOff) != 0 )
		{
			Server.PrecacheSound( "buttons/spark1.wav" );
			Server.PrecacheSound( "buttons/spark2.wav" );
			Server.PrecacheSound( "buttons/spark3.wav" );
			Server.PrecacheSound( "buttons/spark4.wav" );
			Server.PrecacheSound( "buttons/spark5.wav" );
			Server.PrecacheSound( "buttons/spark6.wav" );
		}

		if ( LockedSound != 0 )
		{
			LockSounds.LockedSound = Buttons.ButtonSound( LockedSound );
			Server.PrecacheSound( LockSounds.LockedSound );
		}

		if ( UnlockedSound != 0 )
		{
			LockSounds.UnlockedSound = Buttons.ButtonSound( UnlockedSound );
			Server.PrecacheSound( LockSounds.UnlockedSound );
		}

		LockSounds.SetSentences( LockedSentence, UnlockedSentence );
	}

	public override void Spawn()
	{
		Noise = Buttons.ButtonSound( Sounds );
		Server.PrecacheSound( Noise );

		Precache();

		if ( (SpawnFlags & SpawnFlagSparkIfOff) != 0 )
		{
			SetThink( ButtonSpark );
			NextThink = (float)(Server.Globals.Time + 0.5);
		}

		SetMoveDir();

		MoveType = MoveType.Push;
		Solid = Solid.Bsp;
		SetModel( ModelName );

		if ( Speed == 0f )
			Speed = 40f;

		if ( Health > 0f )
			DamageMode = DamageYes;

		if ( Wait == 0f )
			Wait = 1f;

		if ( Lip == 0f )
			Lip = 4f;

		ToggleState = ToggleState.AtBottom;

		Position1 = Origin;
		Position2 = Position1 + (MoveDir * MoveDirExtent);

		if ( MathLib.Length( Position2 - Position1 ) < 1f || (SpawnFlags & SpawnFlagDontMove) != 0 )
			Position2 = Position1;

		StayPushed = Wait == -1f;
		Rotating = false;

		if ( (SpawnFlags & SpawnFlagTouchOnly) != 0 )
		{
			SetTouch( ButtonTouch );
		}
		else
		{
			SetTouch( null );
			SetUse( ButtonUse );
		}
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		var code = ButtonResponseToTouch();
		if ( code == ButtonCode.Nothing )
			return false;

		SetTouch( null );

		Activator = attacker;
		if ( Activator is null )
			return false;

		if ( code == ButtonCode.Return )
		{
			Server.EmitSound( this, SoundChannel.Voice, Noise, 1f, Server.AttenuationNormal );

			if ( (SpawnFlags & SpawnFlagToggle) == 0 )
				UseTargets( Activator, UseType.Toggle, 0f );

			ButtonReturn();
		}
		else
		{
			ButtonActivate();
		}

		return false;
	}

	protected void ButtonSpark()
	{
		SetThink( ButtonSpark );
		NextThink = (float)(Server.Globals.Time + (0.1 + Server.RandomFloat( 0f, 1.5f )));

		Buttons.DoSpark( this, Mins );
	}

	protected void ButtonUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( ToggleState is ToggleState.GoingUp or ToggleState.GoingDown )
			return;

		Activator = activator;

		if ( ToggleState == ToggleState.AtTop )
		{
			if ( !StayPushed && (SpawnFlags & SpawnFlagToggle) != 0 )
			{
				Server.EmitSound( this, SoundChannel.Voice, Noise, 1f, Server.AttenuationNormal );
				ButtonReturn();
			}
		}
		else
		{
			ButtonActivate();
		}
	}

	protected ButtonCode ButtonResponseToTouch()
	{
		var toggle = (SpawnFlags & SpawnFlagToggle) != 0;

		if ( ToggleState is ToggleState.GoingUp or ToggleState.GoingDown || (ToggleState == ToggleState.AtTop && !StayPushed && !toggle) )
			return ButtonCode.Nothing;

		if ( ToggleState != ToggleState.AtTop )
			return ButtonCode.Activate;

		return toggle && !StayPushed ? ButtonCode.Return : ButtonCode.Nothing;
	}

	protected void ButtonTouch( BaseEntity other )
	{
		if ( other.ClassName != "player" )
			return;

		Activator = other;

		var code = ButtonResponseToTouch();
		if ( code == ButtonCode.Nothing )
			return;

		if ( !IsMasterTriggered( Master, other ) )
		{
			PlayLockSounds( LockSounds, true, true );
			return;
		}

		SetTouch( null );

		if ( code == ButtonCode.Return )
		{
			Server.EmitSound( this, SoundChannel.Voice, Noise, 1f, Server.AttenuationNormal );
			UseTargets( Activator, UseType.Toggle, 0f );
			ButtonReturn();
		}
		else
		{
			ButtonActivate();
		}
	}

	protected void ButtonActivate()
	{
		Server.EmitSound( this, SoundChannel.Voice, Noise, 1f, Server.AttenuationNormal );

		if ( !IsMasterTriggered( Master, Activator ) )
		{
			PlayLockSounds( LockSounds, true, true );
			return;
		}

		PlayLockSounds( LockSounds, false, true );

		ToggleState = ToggleState.GoingUp;

		SetMoveDone( TriggerAndWait );

		if ( !Rotating )
			LinearMove( Position2, Speed );
		else
			AngularMove( Angle2, Speed );
	}

	protected void TriggerAndWait()
	{
		if ( !IsMasterTriggered( Master, Activator ) )
			return;

		ToggleState = ToggleState.AtTop;

		if ( StayPushed || (SpawnFlags & SpawnFlagToggle) != 0 )
		{
			SetTouch( (SpawnFlags & SpawnFlagTouchOnly) == 0 ? null : ButtonTouch );
		}
		else
		{
			NextThink = LTime + Wait;
			SetThink( ButtonReturn );
		}

		Frame = 1f;

		UseTargets( Activator, UseType.Toggle, 0f );
	}

	protected void ButtonReturn()
	{
		ToggleState = ToggleState.GoingDown;

		SetMoveDone( ButtonBackHome );

		if ( !Rotating )
			LinearMove( Position1, Speed );
		else
			AngularMove( Angle1, Speed );

		Frame = 0f;
	}

	protected void ButtonBackHome()
	{
		ToggleState = ToggleState.AtBottom;

		if ( (SpawnFlags & SpawnFlagToggle) != 0 )
			UseTargets( Activator, UseType.Toggle, 0f );

		if ( !string.IsNullOrEmpty( Target ) )
		{
			Edict found = null;

			while ( (found = Server.FindEntityByString( found, "targetname", Target )) is not null )
			{
				if ( found.ClassName == "multisource" && found is BaseEntity target )
					target.Use( Activator, this, UseType.Toggle, 0f );
			}
		}

		SetTouch( (SpawnFlags & SpawnFlagTouchOnly) == 0 ? null : ButtonTouch );

		if ( (SpawnFlags & SpawnFlagSparkIfOff) != 0 )
		{
			SetThink( ButtonSpark );
			NextThink = (float)(Server.Globals.Time + 0.5);
		}
	}
}

[LinkEntityToClass( "func_rot_button" )]
public class RotButton : BaseButton
{
	private const int SpawnFlagNotSolid = 1;
	private const int SpawnFlagRotateBackwards = 2;

	public override void Spawn()
	{
		Noise = Buttons.ButtonSound( Sounds );
		Server.PrecacheSound( Noise );

		AxisDir( this );

		if ( (SpawnFlags & SpawnFlagRotateBackwards) != 0 )
			MoveDir *= -1f;

		MoveType = MoveType.Push;
		Solid = (SpawnFlags & SpawnFlagNotSolid) != 0 ? Solid.Not : Solid.Bsp;

		SetModel( ModelName );

		if ( Speed == 0f )
			Speed = 40f;

		if ( Wait == 0f )
			Wait = 1f;

		if ( Health > 0f )
			DamageMode = DamageYes;

		ToggleState = ToggleState.AtBottom;
		Angle1 = Angles;
		Angle2 = Angles + (MoveDir * MoveDistance);

		StayPushed = Wait == -1f;
		Rotating = true;

		if ( (SpawnFlags & SpawnFlagTouchOnly) == 0 )
		{
			SetTouch( null );
			SetUse( ButtonUse );
		}
		else
		{
			SetTouch( ButtonTouch );
		}
	}
}

[LinkEntityToClass( "momentary_rot_button" )]
public class MomentaryRotButton : BaseToggle
{
	private const int SpawnFlagDoor = 0x0001;
	private const int SpawnFlagAutoReturn = 16;

	[Save( FieldType.Float )]
	public float ReturnSpeed { get; set; }
	[Save( FieldType.Integer )]
	public int Sounds { get; set; }

	[Save( FieldType.Integer )]
	private bool _lastUsed;
	[Save( FieldType.Integer )]
	private int _direction;
	[Save( FieldType.Vector )]
	private Vector3 _start;
	[Save( FieldType.Vector )]
	private Vector3 _end;

	public override ObjectCaps ObjectCaps
	{
		get
		{
			var caps = base.ObjectCaps & ~ObjectCaps.AcrossTransition;
			return (SpawnFlags & SpawnFlagDoor) != 0 ? caps : caps | ObjectCaps.ContinuousUse;
		}
	}

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "returnspeed":
				ReturnSpeed = Util.Atof( value );
				return true;
			case "sounds":
				Sounds = Util.Atoi( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Spawn()
	{
		AxisDir( this );

		if ( Speed == 0f )
			Speed = 100f;

		if ( MoveDistance < 0f )
		{
			_start = Angles + (MoveDir * MoveDistance);
			_end = Angles;
			_direction = 1;
			MoveDistance = -MoveDistance;
		}
		else
		{
			_start = Angles;
			_end = Angles + (MoveDir * MoveDistance);
			_direction = -1;
		}

		Solid = (SpawnFlags & SpawnFlagDoor) != 0 ? Solid.Bsp : Solid.Not;
		MoveType = MoveType.Push;

		SetOrigin( Origin );
		SetModel( ModelName );

		Noise = Buttons.ButtonSound( Sounds );
		Server.PrecacheSound( Noise );
		_lastUsed = false;
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		IdealYaw = AxisDelta( SpawnFlags, Angles, _start ) / MoveDistance;

		UpdateAllButtons( IdealYaw, true );

		var dest = Angles + (AVelocity * (NextThink - LTime));
		var predicted = AxisDelta( SpawnFlags, dest, _start ) / MoveDistance;

		UpdateTarget( predicted );
	}

	private void UpdateAllButtons( float value, bool start )
	{
		Edict found = null;

		while ( (found = Server.FindEntityByString( found, "target", Target )) is not null )
		{
			if ( found.ClassName != "momentary_rot_button" || found is not MomentaryRotButton button )
				continue;

			if ( start )
				button.UpdateSelf( value );
			else
				button.UpdateSelfReturn( value );
		}
	}

	private void UpdateSelf( float value )
	{
		var playSound = false;

		if ( !_lastUsed )
		{
			playSound = true;
			_direction = -_direction;
		}

		_lastUsed = true;

		NextThink = (float)(LTime + 0.1);

		if ( _direction > 0 && value >= 1.0 )
		{
			AVelocity = Vector3.Zero;
			Angles = _end;
			return;
		}

		if ( _direction < 0 && value <= 0f )
		{
			AVelocity = Vector3.Zero;
			Angles = _start;
			return;
		}

		if ( playSound )
			Server.EmitSound( this, SoundChannel.Voice, Noise, 1f, Server.AttenuationNormal );

		if ( NextThink < LTime )
			NextThink = (float)(LTime + 0.1);
		else
			NextThink = (float)(NextThink + 0.1);

		AVelocity = (_direction * Speed) * MoveDir;
		SetThink( Off );
	}

	private void UpdateTarget( float value )
	{
		if ( string.IsNullOrEmpty( Target ) )
			return;

		Edict found = null;

		while ( (found = Server.FindEntityByString( found, "targetname", Target )) is not null )
			(found as BaseEntity)?.Use( this, this, UseType.Set, value );
	}

	private void Off()
	{
		AVelocity = Vector3.Zero;
		_lastUsed = false;

		if ( (SpawnFlags & SpawnFlagAutoReturn) != 0 && ReturnSpeed > 0f )
		{
			SetThink( Return );
			NextThink = (float)(LTime + 0.1);
			_direction = -1;
		}
		else
		{
			SetThink( null );
		}
	}

	private void Return()
	{
		var value = AxisDelta( SpawnFlags, Angles, _start ) / MoveDistance;

		UpdateAllButtons( value, false );

		if ( value > 0f )
			UpdateTarget( value );
	}

	private void UpdateSelfReturn( float value )
	{
		if ( value <= 0f )
		{
			AVelocity = Vector3.Zero;
			Angles = _start;
			NextThink = -1f;
			SetThink( null );
		}
		else
		{
			AVelocity = -ReturnSpeed * MoveDir;
			NextThink = (float)(LTime + 0.1);
		}
	}
}

[LinkEntityToClass( "env_spark" )]
[LinkEntityToClass( "env_debris" )]
public class EnvSpark : BaseEntity
{
	private const int SpawnFlagToggle = 32;
	private const int SpawnFlagStartOn = 64;

	[Save( FieldType.Float )]
	public float MaxDelay { get; set; }

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "MaxDelay":
				MaxDelay = Util.Atof( value );
				return true;
			case "style":
			case "height":
			case "killtarget":
			case "value1":
			case "value2":
			case "value3":
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Spawn()
	{
		SetThink( null );
		SetUse( null );

		if ( (SpawnFlags & SpawnFlagToggle) != 0 )
		{
			if ( (SpawnFlags & SpawnFlagStartOn) != 0 )
			{
				SetThink( SparkThink );
				SetUse( SparkStop );
			}
			else
			{
				SetUse( SparkStart );
			}
		}
		else
		{
			SetThink( SparkThink );
		}

		NextThink = (float)(Server.Globals.Time + (0.1 + Server.RandomFloat( 0f, 1.5f )));

		if ( MaxDelay <= 0f )
			MaxDelay = 1.5f;

		Precache();
	}

	public override void Precache()
	{
		Server.PrecacheSound( "buttons/spark1.wav" );
		Server.PrecacheSound( "buttons/spark2.wav" );
		Server.PrecacheSound( "buttons/spark3.wav" );
		Server.PrecacheSound( "buttons/spark4.wav" );
		Server.PrecacheSound( "buttons/spark5.wav" );
		Server.PrecacheSound( "buttons/spark6.wav" );
	}

	private void SparkThink()
	{
		NextThink = (float)(Server.Globals.Time + 0.1 + Server.RandomFloat( 0f, MaxDelay ));
		Buttons.DoSpark( this, Origin );
	}

	private void SparkStart( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		SetUse( SparkStop );
		SetThink( SparkThink );
		NextThink = (float)(Server.Globals.Time + (0.1 + Server.RandomFloat( 0f, MaxDelay )));
	}

	private void SparkStop( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		SetUse( SparkStart );
		SetThink( null );
	}
}

[LinkEntityToClass( "button_target" )]
public class ButtonTarget : BaseEntity
{
	private const int SpawnFlagUse = 0x0001;
	private const int SpawnFlagOn = 0x0002;

	public override ObjectCaps ObjectCaps
	{
		get
		{
			var caps = base.ObjectCaps & ~ObjectCaps.AcrossTransition;
			return (SpawnFlags & SpawnFlagUse) != 0 ? caps | ObjectCaps.ImpulseUse : caps;
		}
	}

	public override void Spawn()
	{
		MoveType = MoveType.Push;
		Solid = Solid.Bsp;
		SetModel( ModelName );
		DamageMode = DamageYes;

		if ( (SpawnFlags & SpawnFlagOn) != 0 )
			Frame = 1f;
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( !ShouldToggle( useType, (int)Frame != 0 ) )
			return;

		Frame = 1f - Frame;

		UseTargets( activator, Frame != 0f ? UseType.On : UseType.Off, 0f );
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		Use( attacker, this, UseType.Toggle, 0f );
		return true;
	}
}
