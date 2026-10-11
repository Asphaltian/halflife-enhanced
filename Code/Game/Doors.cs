using GoldSrc;

namespace HalfLife;

public sealed class LockSound
{
	public string LockedSound { get; set; }
	public string LockedSentence { get; set; }
	public string UnlockedSound { get; set; }
	public string UnlockedSentence { get; set; }
	public int LockedSentenceIndex { get; set; }
	public int UnlockedSentenceIndex { get; set; }
	public float WaitSound { get; set; }
	public float WaitSentence { get; set; }
	public bool EndOfLocked { get; set; }
	public bool EndOfUnlocked { get; set; }

	public void SetSentences( int locked, int unlocked )
	{
		LockedSentence = locked switch
		{
			1 => "NA",
			2 => "ND",
			3 => "NF",
			4 => "NFIRE",
			5 => "NCHEM",
			6 => "NRAD",
			7 => "NCON",
			8 => "NH",
			9 => "NG",
			_ => null
		};

		UnlockedSentence = unlocked switch
		{
			1 => "EA",
			2 => "ED",
			3 => "EF",
			4 => "EFIRE",
			5 => "ECHEM",
			6 => "ERAD",
			7 => "ECON",
			8 => "EH",
			_ => null
		};
	}
}

[LinkEntityToClass( "func_door" )]
[LinkEntityToClass( "func_water" )]
public class BaseDoor : BaseToggle
{
	protected const int SpawnFlagStartOpen = 1;
	protected const int SpawnFlagRotateBackwards = 2;
	protected const int SpawnFlagPassable = 8;
	protected const int SpawnFlagOneWay = 16;
	public const int SpawnFlagNoAutoReturn = 32;
	public const int SpawnFlagUseOnly = 256;
	public const int SpawnFlagNoMonsters = 512;
	protected const int SpawnFlagSilent = unchecked((int)0x80000000);

	[Save( FieldType.Character )]
	public byte HealthValue { get; set; }
	[Save( FieldType.Character )]
	public byte MoveSound { get; set; }
	[Save( FieldType.Character )]
	public byte StopSound { get; set; }
	[Save( FieldType.Character )]
	public byte LockedSound { get; set; }
	[Save( FieldType.Character )]
	public byte LockedSentence { get; set; }
	[Save( FieldType.Character )]
	public byte UnlockedSound { get; set; }
	[Save( FieldType.Character )]
	public byte UnlockedSentence { get; set; }

	protected LockSound LockSounds { get; } = new();

	public override ObjectCaps ObjectCaps
	{
		get
		{
			var caps = base.ObjectCaps & ~ObjectCaps.AcrossTransition;
			return (SpawnFlags & SpawnFlagUseOnly) != 0 ? caps | ObjectCaps.ImpulseUse : caps;
		}
	}

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "skin":
				Skin = (int)Util.Atof( value );
				return true;
			case "movesnd":
				MoveSound = (byte)Util.Atof( value );
				return true;
			case "stopsnd":
				StopSound = (byte)Util.Atof( value );
				return true;
			case "healthvalue":
				HealthValue = (byte)Util.Atof( value );
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
			case "WaveHeight":
				Scale = (float)(Util.Atof( value ) * (1.0 / 8.0));
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Spawn()
	{
		Precache();
		SetMoveDir();

		if ( Skin == 0 )
		{
			Solid = (SpawnFlags & SpawnFlagPassable) != 0 ? Solid.Not : Solid.Bsp;
		}
		else
		{
			Solid = Solid.Not;
			SpawnFlags |= SpawnFlagSilent;
		}

		MoveType = MoveType.Push;
		SetOrigin( Origin );
		SetModel( ModelName );

		if ( Speed == 0f )
			Speed = 100f;

		Position1 = Origin;
		Position2 = Position1 + (MoveDir * MoveDirExtent);

		if ( (SpawnFlags & SpawnFlagStartOpen) != 0 )
		{
			SetOrigin( Position2 );
			Position2 = Position1;
			Position1 = Origin;
		}

		ToggleState = ToggleState.AtBottom;

		SetTouch( (SpawnFlags & SpawnFlagUseOnly) != 0 ? null : DoorTouch );
	}

	public override void Precache()
	{
		if ( MoveSound is >= 1 and <= 10 )
		{
			Noise1 = $"doors/doormove{MoveSound}.wav";
			Server.PrecacheSound( Noise1 );
		}
		else
		{
			Noise1 = "common/null.wav";
		}

		if ( StopSound is >= 1 and <= 8 )
		{
			Noise2 = $"doors/doorstop{StopSound}.wav";
			Server.PrecacheSound( Noise2 );
		}
		else
		{
			Noise2 = "common/null.wav";
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

	public virtual void SetToggleState( ToggleState state )
	{
		SetOrigin( state == ToggleState.AtTop ? Position2 : Position1 );
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		Activator = activator;

		if ( ToggleState == ToggleState.AtBottom || ((SpawnFlags & SpawnFlagNoAutoReturn) != 0 && ToggleState == ToggleState.AtTop) )
			DoorActivate();
	}

	public override void Blocked( BaseEntity other )
	{
		if ( Damage != 0f )
			other.TakeDamage( this, this, Damage, DamageCrush );

		if ( Wait >= 0f )
		{
			if ( ToggleState == ToggleState.GoingDown )
				DoorGoUp();
			else
				DoorGoDown();
		}

		if ( string.IsNullOrEmpty( TargetName ) )
			return;

		Edict found = null;

		while ( (found = Server.FindEntityByString( found, "targetname", TargetName )) is not null )
		{
			if ( found == this || found is not BaseDoor door || door.ClassName is not ("func_door" or "func_door_rotating") )
				continue;

			if ( door.Wait < 0f )
				continue;

			if ( SameVector( door.Velocity, Velocity ) && SameVector( door.AVelocity, Velocity ) )
			{
				if ( door.ClassName == "func_door" )
				{
					door.Origin = Origin;
					door.Velocity = Vector3.Zero;
				}
				else
				{
					door.Angles = Angles;
					door.AVelocity = Vector3.Zero;
				}
			}

			if ( (SpawnFlags & SpawnFlagSilent) == 0 )
				Server.StopSound( this, SoundChannel.Static, Noise1 );

			if ( door.ToggleState == ToggleState.GoingDown )
				door.DoorGoUp();
			else
				door.DoorGoDown();
		}
	}

	protected void DoorTouch( BaseEntity other )
	{
		if ( other.ClassName != "player" )
			return;

		if ( !string.IsNullOrEmpty( Master ) && !IsMasterTriggered( Master, other ) )
			PlayLockSounds( LockSounds, true, false );

		if ( !string.IsNullOrEmpty( TargetName ) )
		{
			PlayLockSounds( LockSounds, true, false );
			return;
		}

		Activator = other;

		if ( DoorActivate() )
			SetTouch( null );
	}

	private bool DoorActivate()
	{
		if ( !IsMasterTriggered( Master, Activator ) )
			return false;

		if ( (SpawnFlags & SpawnFlagNoAutoReturn) != 0 && ToggleState == ToggleState.AtTop )
		{
			DoorGoDown();
		}
		else
		{
			if ( Activator is { IsPlayer: true } )
				Activator.TakeHealth( HealthValue, DamageGeneric );

			PlayLockSounds( LockSounds, false, false );
			DoorGoUp();
		}

		return true;
	}

	private void DoorGoUp()
	{
		if ( (SpawnFlags & SpawnFlagSilent) == 0 && ToggleState is not (ToggleState.GoingUp or ToggleState.GoingDown) )
			Server.EmitSound( this, SoundChannel.Static, Noise1, 1f, Server.AttenuationNormal );

		ToggleState = ToggleState.GoingUp;

		SetMoveDone( DoorHitTop );

		if ( ClassName != "func_door_rotating" )
		{
			LinearMove( Position2, Speed );
			return;
		}

		var sign = 1f;

		if ( Activator is not null && (SpawnFlags & SpawnFlagOneWay) == 0 && MoveDir.y != 0f )
		{
			var toActivator = Activator.Origin - Origin;

			Server.Globals.MakeVectors( Activator.Angles );

			var next = Activator.Origin + (Server.Globals.Forward * 10f) - Origin;

			if ( (toActivator.x * next.y) - (toActivator.y * next.x) < 0f )
				sign = -1f;
		}

		AngularMove( Angle2 * sign, Speed );
	}

	private void DoorHitTop()
	{
		if ( (SpawnFlags & SpawnFlagSilent) == 0 )
		{
			Server.StopSound( this, SoundChannel.Static, Noise1 );
			Server.EmitSound( this, SoundChannel.Static, Noise2, 1f, Server.AttenuationNormal );
		}

		ToggleState = ToggleState.AtTop;

		if ( (SpawnFlags & SpawnFlagNoAutoReturn) != 0 )
		{
			if ( (SpawnFlags & SpawnFlagUseOnly) == 0 )
				SetTouch( DoorTouch );
		}
		else
		{
			NextThink = LTime + Wait;
			SetThink( DoorGoDown );

			if ( Wait == -1f )
				NextThink = -1f;
		}

		if ( NetName is not null && (SpawnFlags & SpawnFlagStartOpen) != 0 )
			FireTargets( NetName, Activator, this, UseType.Toggle, 0f );

		UseTargets( Activator, UseType.Toggle, 0f );
	}

	private void DoorGoDown()
	{
		if ( (SpawnFlags & SpawnFlagSilent) == 0 && ToggleState is not (ToggleState.GoingUp or ToggleState.GoingDown) )
			Server.EmitSound( this, SoundChannel.Static, Noise1, 1f, Server.AttenuationNormal );

		ToggleState = ToggleState.GoingDown;

		SetMoveDone( DoorHitBottom );

		if ( ClassName == "func_door_rotating" )
			AngularMove( Angle1, Speed );
		else
			LinearMove( Position1, Speed );
	}

	private void DoorHitBottom()
	{
		if ( (SpawnFlags & SpawnFlagSilent) == 0 )
		{
			Server.StopSound( this, SoundChannel.Static, Noise1 );
			Server.EmitSound( this, SoundChannel.Static, Noise2, 1f, Server.AttenuationNormal );
		}

		ToggleState = ToggleState.AtBottom;

		SetTouch( (SpawnFlags & SpawnFlagUseOnly) != 0 ? null : DoorTouch );

		UseTargets( Activator, UseType.Toggle, 0f );

		if ( NetName is not null && (SpawnFlags & SpawnFlagStartOpen) == 0 )
			FireTargets( NetName, Activator, this, UseType.Toggle, 0f );
	}

	private static bool SameVector( Vector3 a, Vector3 b )
	{
		return a.x == b.x && a.y == b.y && a.z == b.z;
	}
}

[LinkEntityToClass( "func_door_rotating" )]
public class RotDoor : BaseDoor
{
	public override void Spawn()
	{
		Precache();
		AxisDir( this );

		if ( (SpawnFlags & SpawnFlagRotateBackwards) != 0 )
			MoveDir *= -1f;

		Angle1 = Angles;
		Angle2 = Angles + (MoveDir * MoveDistance);

		Solid = (SpawnFlags & SpawnFlagPassable) != 0 ? Solid.Not : Solid.Bsp;
		MoveType = MoveType.Push;

		SetOrigin( Origin );
		SetModel( ModelName );

		if ( Speed == 0f )
			Speed = 100f;

		if ( (SpawnFlags & SpawnFlagStartOpen) != 0 )
		{
			Angles = Angle2;
			Angle2 = Angle1;
			MoveDir *= -1f;
		}

		ToggleState = ToggleState.AtBottom;

		SetTouch( (SpawnFlags & SpawnFlagUseOnly) != 0 ? null : DoorTouch );
	}

	public override void SetToggleState( ToggleState state )
	{
		Angles = state == ToggleState.AtTop ? Angle2 : Angle1;
		SetOrigin( Origin );
	}
}

[LinkEntityToClass( "momentary_door" )]
public class MomentaryDoor : BaseToggle
{
	private const int SpawnFlagStartOpen = 1;

	[Save( FieldType.Character )]
	public byte MoveSound { get; set; }

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "movesnd":
				MoveSound = (byte)Util.Atof( value );
				return true;
			case "stopsnd":
			case "healthvalue":
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Spawn()
	{
		SetMoveDir();

		Solid = Solid.Bsp;
		MoveType = MoveType.Push;

		SetOrigin( Origin );
		SetModel( ModelName );

		if ( Speed == 0f )
			Speed = 100f;

		if ( Damage == 0f )
			Damage = 2f;

		Position1 = Origin;
		Position2 = Position1 + (MoveDir * MoveDirExtent);

		if ( (SpawnFlags & SpawnFlagStartOpen) != 0 )
		{
			SetOrigin( Position2 );
			Position2 = Position1;
			Position1 = Origin;
		}

		SetTouch( null );

		Precache();
	}

	public override void Precache()
	{
		if ( MoveSound is >= 1 and <= 8 )
		{
			Noise1 = $"doors/doormove{MoveSound}.wav";
			Server.PrecacheSound( Noise1 );
		}
		else
		{
			Noise1 = "common/null.wav";
		}
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( useType != UseType.Set )
			return;

		if ( value > 1f )
			value = 1f;

		if ( value < 0f )
			value = 0f;

		var move = Position1 + (value * (Position2 - Position1));

		var delta = move - Origin;
		var speed = (float)(MathLib.Length( delta ) / 0.1);

		if ( speed == 0f )
			return;

		if ( NextThink < LTime || NextThink == 0f )
			Server.EmitSound( this, SoundChannel.Static, Noise1, 1f, Server.AttenuationNormal );
		else if ( move.x == FinalDest.x && move.y == FinalDest.y && move.z == FinalDest.z )
			return;

		SetMoveDone( DoorMoveDone );
		LinearMove( move, speed );
	}

	private void DoorMoveDone()
	{
		Server.StopSound( this, SoundChannel.Static, Noise1 );
		Server.EmitSound( this, SoundChannel.Static, Noise2, 1f, Server.AttenuationNormal );
	}
}

public static class Buttons
{
	public static void DoSpark( BaseEntity entity, Vector3 location )
	{
		Util.Sparks( location + (entity.Size * 0.5f) );

		var volume = (float)(Server.RandomFloat( 0.25f, 0.75f ) * 0.4);
		var spark = (int)(Server.RandomFloat( 0f, 1f ) * 6);

		if ( spark < 6 )
			entity.Server.EmitSound( entity, SoundChannel.Voice, $"buttons/spark{spark + 1}.wav", volume, Server.AttenuationNormal );
	}

	public static string ButtonSound( int sound )
	{
		return sound switch
		{
			0 => "common/null.wav",
			>= 1 and <= 11 => $"buttons/button{sound}.wav",
			12 => "buttons/latchlocked1.wav",
			13 => "buttons/latchunlocked1.wav",
			14 => "buttons/lightswitch2.wav",
			>= 21 and <= 25 => $"buttons/lever{sound - 20}.wav",
			_ => "buttons/button9.wav"
		};
	}
}
