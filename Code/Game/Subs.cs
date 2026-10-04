using System;
using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "info_player_start" )]
[LinkEntityToClass( "info_landmark" )]
[LinkEntityToClass( "info_target" )]
[LinkEntityToClass( "info_teleport_destination" )]
public class PointEntity : BaseEntity
{
	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override void Spawn()
	{
		Solid = Solid.Not;
	}
}

[LinkEntityToClass( "info_player_deathmatch" )]
public class BaseDeathmatchStart : PointEntity
{
	public override bool KeyValue( string key, string value )
	{
		if ( key == "master" )
		{
			NetName = Server.AllocString( value );
			return true;
		}

		return base.KeyValue( key, value );
	}

	public override bool IsTriggered( BaseEntity entity )
	{
		return IsMasterTriggered( NetName, entity );
	}
}

[LinkEntityToClass( "info_null" )]
public class NullEntity : BaseEntity
{
	public override void Spawn()
	{
		Server.FreeEdict( this );
	}
}

[LinkEntityToClass( "DelayedUse" )]
public class BaseDelay : BaseEntity
{
	[Save( FieldType.Float )]
	public float Delay { get; set; }
	[Save( FieldType.String )]
	public string KillTarget { get; set; }

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "delay":
				Delay = Util.Atof( value );
				return true;
			case "killtarget":
				KillTarget = Server.AllocString( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public new void UseTargets( BaseEntity activator, UseType useType, float value )
	{
		if ( string.IsNullOrEmpty( Target ) && KillTarget is null )
			return;

		if ( Delay != 0f )
		{
			var delayed = Server.CreateEntity<BaseDelay>();

			delayed.ClassName = "DelayedUse";
			delayed.NextThink = Server.Globals.Time + Delay;
			delayed.SetThink( delayed.DelayThink );
			delayed.Button = (int)useType;
			delayed.KillTarget = KillTarget;
			delayed.Delay = 0f;
			delayed.Target = Target;
			delayed.Owner = activator is { IsPlayer: true } ? activator : null;

			return;
		}

		if ( KillTarget is not null )
		{
			Edict found = null;

			Util.AiAlert( $"KillTarget: {KillTarget}" );

			while ( (found = Server.FindEntityByString( found, "targetname", KillTarget )) is not null )
			{
				(found as BaseEntity)?.Remove();

				Util.AiAlert( $"killing {found.ClassName}" );
			}
		}

		if ( !string.IsNullOrEmpty( Target ) )
			FireTargets( Target, activator, this, useType, value );
	}

	private void DelayThink()
	{
		UseTargets( Owner as BaseEntity, (UseType)Button, 0f );
		Server.FreeEdict( this );
	}
}

public enum ToggleState
{
	AtTop,
	AtBottom,
	GoingUp,
	GoingDown
}

public class BaseToggle : BaseAnimating
{
	public const int SpawnFlagRotateZ = 64;
	public const int SpawnFlagRotateX = 128;

	private const float DoorSentenceWait = 6f;
	private const float DoorSoundWait = 3f;
	private const float ButtonSoundWait = 0.5f;

	[Save( FieldType.Integer )]
	public ToggleState ToggleState { get; set; }

	public override ToggleState GetToggleState() => ToggleState;

	public virtual bool IsAllowedToSpeak => false;

	public virtual void PlaySentence( string sentence, float duration, float volume, float attenuation )
	{
		if ( sentence is null || !IsAllowedToSpeak )
			return;

		if ( sentence.StartsWith( '!' ) )
			Sound.EmitDyn( this, SoundChannel.Voice, sentence, volume, attenuation, 0, Server.PitchNormal );
		else
			Sentences.PlayRandom( this, sentence, volume, attenuation, 0, Server.PitchNormal );
	}

	public virtual void PlayScriptedSentence( string sentence, float duration, float volume, float attenuation, bool concurrent, BaseEntity listener )
	{
		PlaySentence( sentence, duration, volume, attenuation );
	}

	public virtual void SentenceStop() => Sound.Emit( this, SoundChannel.Voice, "common/null.wav", 1f, Server.AttenuationIdle );
	[Save( FieldType.Float )]
	public float MoveDistance { get; set; }
	[Save( FieldType.Float )]
	public float Wait { get; set; }
	[Save( FieldType.Float )]
	public float Lip { get; set; }
	[Save( FieldType.PositionVector )]
	public Vector3 Position1 { get; set; }
	[Save( FieldType.PositionVector )]
	public Vector3 Position2 { get; set; }
	[Save( FieldType.Vector )]
	public Vector3 Angle1 { get; set; }
	[Save( FieldType.Vector )]
	public Vector3 Angle2 { get; set; }
	[Save( FieldType.Integer )]
	public int TriggersLeft { get; set; }
	[Save( FieldType.Float )]
	public float Height { get; set; }
	[Save( FieldType.EHandle )]
	public BaseEntity Activator { get; set; }
	[Save( FieldType.PositionVector )]
	public Vector3 FinalDest { get; set; }
	[Save( FieldType.Vector )]
	public Vector3 FinalAngle { get; set; }
	[Save( FieldType.Integer )]
	public int BitsDamageInflict { get; set; }
	[Save( FieldType.String )]
	public string Master { get; set; }
	[Save( FieldType.Time )]
	public float ActivateFinished { get; set; }
	[Save( FieldType.Float )]
	public float TWidth { get; set; }
	[Save( FieldType.Float )]
	public float TLength { get; set; }

	[Save( FieldType.Function )]
	private Action _moveDone;

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
			case "master":
				Master = Server.AllocString( value );
				return true;
			case "distance":
				MoveDistance = Util.Atof( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override float GetDelay() => Wait;

	protected float MoveDirExtent => (float)(Math.Abs( MoveDir.x * (Size.x - 2f) ) + Math.Abs( MoveDir.y * (Size.y - 2f) ) + Math.Abs( MoveDir.z * (Size.z - 2f) ) - Lip);

	public override bool IsLockedByMaster => !string.IsNullOrEmpty( Master ) && !IsMasterTriggered( Master, Activator );

	protected void SetMoveDone( Action moveDone ) => _moveDone = moveDone;

	protected void PlayLockSounds( LockSound lockSound, bool locked, bool button )
	{
		var soundWait = button ? ButtonSoundWait : DoorSoundWait;
		var time = Server.Globals.Time;

		if ( locked )
		{
			var playSound = !string.IsNullOrEmpty( lockSound.LockedSound ) && time > lockSound.WaitSound;
			var playSentence = lockSound.LockedSentence is not null && !lockSound.EndOfLocked && time > lockSound.WaitSentence;
			var volume = playSound && playSentence ? 0.25f : 1f;

			if ( playSound )
			{
				Server.EmitSound( this, SoundChannel.Item, lockSound.LockedSound, volume, Server.AttenuationNormal );
				lockSound.WaitSound = time + soundWait;
			}

			if ( playSentence )
			{
				var previous = lockSound.LockedSentenceIndex;

				lockSound.LockedSentenceIndex = Sentences.PlaySequential( this, lockSound.LockedSentence, 0.85f, Server.AttenuationNormal, 0, 100, lockSound.LockedSentenceIndex, false );
				lockSound.UnlockedSentenceIndex = 0;

				lockSound.EndOfLocked = previous == lockSound.LockedSentenceIndex;

				lockSound.WaitSentence = time + DoorSentenceWait;
			}
		}
		else
		{
			var playSound = !string.IsNullOrEmpty( lockSound.UnlockedSound ) && time > lockSound.WaitSound;
			var playSentence = lockSound.UnlockedSentence is not null && !lockSound.EndOfUnlocked && time > lockSound.WaitSentence;
			var volume = playSound && playSentence ? 0.25f : 1f;

			if ( playSound )
			{
				Server.EmitSound( this, SoundChannel.Item, lockSound.UnlockedSound, volume, Server.AttenuationNormal );
				lockSound.WaitSound = time + soundWait;
			}

			if ( playSentence )
			{
				var previous = lockSound.UnlockedSentenceIndex;

				lockSound.UnlockedSentenceIndex = Sentences.PlaySequential( this, lockSound.UnlockedSentence, 0.85f, Server.AttenuationNormal, 0, 100, lockSound.UnlockedSentenceIndex, false );
				lockSound.LockedSentenceIndex = 0;

				lockSound.EndOfUnlocked = previous == lockSound.UnlockedSentenceIndex;
				lockSound.WaitSentence = time + DoorSentenceWait;
			}
		}
	}

	protected void LinearMove( Vector3 dest, float speed )
	{
		FinalDest = dest;

		if ( dest.x == Origin.x && dest.y == Origin.y && dest.z == Origin.z )
		{
			LinearMoveDone();
			return;
		}

		var delta = dest - Origin;
		var travelTime = MathLib.Length( delta ) / speed;

		NextThink = LTime + travelTime;
		SetThink( LinearMoveDone );

		Velocity = delta / travelTime;
	}

	protected void AngularMove( Vector3 destAngle, float speed )
	{
		FinalAngle = destAngle;

		if ( destAngle.x == Angles.x && destAngle.y == Angles.y && destAngle.z == Angles.z )
		{
			AngularMoveDone();
			return;
		}

		var delta = destAngle - Angles;
		var travelTime = MathLib.Length( delta ) / speed;

		NextThink = LTime + travelTime;
		SetThink( AngularMoveDone );

		AVelocity = delta / travelTime;
	}

	public static float AxisValue( int flags, Vector3 angles )
	{
		if ( (flags & SpawnFlagRotateZ) != 0 )
			return angles.z;

		if ( (flags & SpawnFlagRotateX) != 0 )
			return angles.x;

		return angles.y;
	}

	public static void AxisDir( BaseEntity entity )
	{
		if ( (entity.SpawnFlags & SpawnFlagRotateZ) != 0 )
			entity.MoveDir = new Vector3( 0f, 0f, 1f );
		else if ( (entity.SpawnFlags & SpawnFlagRotateX) != 0 )
			entity.MoveDir = new Vector3( 1f, 0f, 0f );
		else
			entity.MoveDir = new Vector3( 0f, 1f, 0f );
	}

	public static float AxisDelta( int flags, Vector3 angle1, Vector3 angle2 )
	{
		if ( (flags & SpawnFlagRotateZ) != 0 )
			return angle1.z - angle2.z;

		if ( (flags & SpawnFlagRotateX) != 0 )
			return angle1.x - angle2.x;

		return angle1.y - angle2.y;
	}

	private void LinearMoveDone()
	{
		var delta = FinalDest - Origin;

		if ( MathLib.Length( delta ) > 0.03125f )
		{
			LinearMove( FinalDest, 100f );
			return;
		}

		SetOrigin( FinalDest );
		Velocity = Vector3.Zero;
		NextThink = -1f;

		_moveDone?.Invoke();
	}

	private void AngularMoveDone()
	{
		Angles = FinalAngle;
		AVelocity = Vector3.Zero;
		NextThink = -1f;

		_moveDone?.Invoke();
	}
}
