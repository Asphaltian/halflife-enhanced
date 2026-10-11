using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "func_friction" )]
public class FrictionModifier : BaseEntity
{
	[Save( FieldType.Float )]
	public float FrictionFraction { get; set; }

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override bool KeyValue( string key, string value )
	{
		if ( key == "modifier" )
		{
			FrictionFraction = (float)(Util.Atof( value ) / 100.0);
			return true;
		}

		return base.KeyValue( key, value );
	}

	public override void Spawn()
	{
		Solid = Solid.Trigger;
		SetModel( ModelName );
		MoveType = MoveType.None;
		SetTouch( ChangeFriction );
	}

	private void ChangeFriction( BaseEntity other )
	{
		if ( other.MoveType != MoveType.BounceMissile && other.MoveType != MoveType.Bounce )
			other.Friction = FrictionFraction;
	}
}

[LinkEntityToClass( "trigger_auto" )]
public class AutoTrigger : BaseDelay
{
	private const int SpawnFlagFireOnce = 0x0001;

	[Save( FieldType.String )]
	public string GlobalStateName { get; set; }
	[Save( FieldType.Integer )]
	public UseType TriggerType { get; set; }

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "globalstate":
				GlobalStateName = Server.AllocString( value );
				return true;
			case "triggerstate":
				TriggerType = Triggers.TriggerStateToUseType( Util.Atoi( value ) );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Spawn()
	{
		Precache();
	}

	public override void Precache()
	{
		NextThink = (float)(Server.Globals.Time + 0.1);
	}

	public override void Think()
	{
		if ( GlobalStateName is not null && HalfLifeDll.GlobalState.EntityGetState( GlobalStateName ) != GlobalEntityState.On )
			return;

		UseTargets( this, TriggerType, 0f );

		if ( (SpawnFlags & SpawnFlagFireOnce) != 0 )
			Remove();
	}
}

[LinkEntityToClass( "trigger_relay" )]
public class TriggerRelay : BaseDelay
{
	private const int SpawnFlagFireOnce = 0x0001;

	[Save( FieldType.Integer )]
	public UseType TriggerType { get; set; }

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override bool KeyValue( string key, string value )
	{
		if ( key == "triggerstate" )
		{
			TriggerType = Triggers.TriggerStateToUseType( Util.Atoi( value ) );
			return true;
		}

		return base.KeyValue( key, value );
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		UseTargets( this, TriggerType, 0f );

		if ( (SpawnFlags & SpawnFlagFireOnce) != 0 )
			Remove();
	}
}

[LinkEntityToClass( "multi_manager" )]
public class MultiManager : BaseToggle
{
	private const int MaxTargets = 16;
	private const int MaxTokenLength = 127;
	private const int SpawnFlagClone = unchecked((int)0x80000000);
	private const int SpawnFlagThread = 0x00000001;

	[Save( FieldType.Integer )]
	private int _targetCount;

	[Save( FieldType.Integer )]
	private int _index;

	[Save( FieldType.Time )]
	private float _startTime;

	[Save( FieldType.String )]
	private readonly string[] _targetNames = new string[MaxTargets];

	[Save( FieldType.Float )]
	private readonly float[] _targetDelays = new float[MaxTargets];

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override bool KeyValue( string key, string value )
	{
		if ( key == "wait" )
		{
			Wait = Util.Atof( value );
			return true;
		}

		if ( _targetCount >= MaxTargets )
			return false;

		_targetNames[_targetCount] = Server.AllocString( Triggers.StripToken( key, MaxTokenLength ) );
		_targetDelays[_targetCount] = Util.Atof( value );
		_targetCount++;

		return true;
	}

	public override void Spawn()
	{
		Solid = Solid.Not;
		SetUse( ManagerUse );
		SetThink( ManagerThink );

		var swapped = true;

		while ( swapped )
		{
			swapped = false;

			for ( var i = 1; i < _targetCount; i++ )
			{
				if ( _targetDelays[i] >= _targetDelays[i - 1] )
					continue;

				(_targetNames[i], _targetNames[i - 1]) = (_targetNames[i - 1], _targetNames[i]);
				(_targetDelays[i], _targetDelays[i - 1]) = (_targetDelays[i - 1], _targetDelays[i]);
				swapped = true;
			}
		}
	}

	public override bool HasTarget( string targetName )
	{
		for ( var i = 0; i < _targetCount; i++ )
		{
			if ( _targetNames[i] == targetName )
				return true;
		}

		return false;
	}

	private bool IsClone => (SpawnFlags & SpawnFlagClone) != 0;

	private bool ShouldClone => !IsClone && (SpawnFlags & SpawnFlagThread) != 0;

	private void ManagerThink()
	{
		var time = Server.Globals.Time - _startTime;

		while ( _index < _targetCount && _targetDelays[_index] <= time )
		{
			FireTargets( _targetNames[_index], Activator, this, UseType.Toggle, 0f );
			_index++;
		}

		if ( _index < _targetCount )
		{
			NextThink = _startTime + _targetDelays[_index];
			return;
		}

		SetThink( null );

		if ( IsClone )
		{
			Remove();
			return;
		}

		SetUse( ManagerUse );
	}

	private MultiManager Clone()
	{
		var clone = Server.CreateEntity<MultiManager>();

		clone.CopyEntVars( this );
		clone.SpawnFlags |= SpawnFlagClone;
		clone._targetCount = _targetCount;
		_targetNames.CopyTo( clone._targetNames, 0 );
		_targetDelays.CopyTo( clone._targetDelays, 0 );

		return clone;
	}

	private void ManagerUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( ShouldClone )
		{
			Clone().ManagerUse( activator, caller, useType, value );
			return;
		}

		Activator = activator;
		_index = 0;
		_startTime = Server.Globals.Time;

		SetUse( null );
		SetThink( ManagerThink );
		NextThink = Server.Globals.Time;
	}
}

[LinkEntityToClass( "env_render" )]
public class RenderFxManager : BaseEntity
{
	private const int SpawnFlagMaskFx = 1 << 0;
	private const int SpawnFlagMaskAmt = 1 << 1;
	private const int SpawnFlagMaskMode = 1 << 2;
	private const int SpawnFlagMaskColor = 1 << 3;

	public override void Spawn()
	{
		Solid = Solid.Not;
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( string.IsNullOrEmpty( Target ) )
			return;

		Edict target = null;

		while ( (target = Server.FindEntityByString( target, "targetname", Target )) is not null )
		{
			if ( (SpawnFlags & SpawnFlagMaskFx) == 0 )
				target.RenderFx = RenderFx;

			if ( (SpawnFlags & SpawnFlagMaskAmt) == 0 )
				target.RenderAmt = RenderAmt;

			if ( (SpawnFlags & SpawnFlagMaskMode) == 0 )
				target.RenderMode = RenderMode;

			if ( (SpawnFlags & SpawnFlagMaskColor) == 0 )
				target.RenderColor = RenderColor;
		}
	}
}

[LinkEntityToClass( "trigger" )]
public class BaseTrigger : BaseToggle
{
	protected const int SpawnFlagAllowMonsters = 1;
	protected const int SpawnFlagNoClients = 2;
	protected const int SpawnFlagPushables = 4;
	protected const int SpawnFlagNoMessage = 1;

	protected const int SpawnFlagHurtTargetOnce = 1;
	protected const int SpawnFlagHurtStartOff = 2;
	protected const int SpawnFlagHurtNoClients = 8;
	protected const int SpawnFlagHurtClientOnlyFire = 16;
	protected const int SpawnFlagHurtClientOnlyTouch = 32;

	[ConVar( "showtriggers" )]
	public static float ShowTriggers { get; set; }

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "damage":
				Damage = Util.Atof( value );
				return true;
			case "count":
				TriggersLeft = (int)Util.Atof( value );
				return true;
			case "damagetype":
				BitsDamageInflict = Util.Atoi( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	protected void InitTrigger()
	{
		if ( Angles.x != 0f || Angles.y != 0f || Angles.z != 0f )
			SetMoveDir();

		Solid = Solid.Trigger;
		MoveType = MoveType.None;
		SetModel( ModelName );

		if ( ShowTriggers == 0f )
			Effects |= Effects.NoDraw;
	}

	protected void ToggleUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( Solid == Solid.Not )
		{
			Solid = Solid.Trigger;
			Server.Globals.ForceRetouch++;
		}
		else
		{
			Solid = Solid.Not;
		}

		SetOrigin( Origin );
	}

	protected void HurtTouch( BaseEntity other )
	{
		if ( other.DamageMode == DamageNo )
			return;

		if ( (SpawnFlags & SpawnFlagHurtClientOnlyTouch) != 0 && !other.IsPlayer )
			return;

		if ( (SpawnFlags & SpawnFlagHurtNoClients) != 0 && other.IsPlayer )
			return;

		if ( DamageTime > Server.Globals.Time && Server.Globals.Time != PainFinished )
			return;

		var damage = (float)(Damage * 0.5);

		if ( damage < 0f )
			other.TakeHealth( -damage, BitsDamageInflict );
		else
			other.TakeDamage( this, this, damage, BitsDamageInflict );

		PainFinished = Server.Globals.Time;
		DamageTime = (float)(Server.Globals.Time + 0.5);

		if ( Target is null )
			return;

		if ( (SpawnFlags & SpawnFlagHurtClientOnlyFire) != 0 && !other.IsPlayer )
			return;

		UseTargets( other, UseType.Toggle, 0f );

		if ( (SpawnFlags & SpawnFlagHurtTargetOnce) != 0 )
			Target = null;
	}

	protected void MultiTouch( BaseEntity other )
	{
		if ( ((other.EntityFlags & EntityFlags.Client) == EntityFlags.Client && (SpawnFlags & SpawnFlagNoClients) == 0)
			|| ((other.EntityFlags & EntityFlags.Monster) == EntityFlags.Monster && (SpawnFlags & SpawnFlagAllowMonsters) != 0)
			|| ((SpawnFlags & SpawnFlagPushables) != 0 && other.ClassName == "func_pushable") )
		{
			ActivateMultiTrigger( other );
		}
	}

	protected void ActivateMultiTrigger( BaseEntity activator )
	{
		if ( NextThink > Server.Globals.Time )
			return;

		if ( !IsMasterTriggered( Master, activator ) )
			return;

		if ( ClassName == "trigger_secret" )
		{
			if ( Enemy is null || Enemy.ClassName != "player" )
				return;

			Server.Globals.FoundSecrets++;
		}

		if ( !string.IsNullOrEmpty( Noise ) )
			Server.EmitSound( this, SoundChannel.Voice, Noise, 1f, Server.AttenuationNormal );

		Activator = activator;
		UseTargets( Activator, UseType.Toggle, 0f );

		if ( Wait > 0f )
		{
			SetThink( MultiWaitOver );
			NextThink = Server.Globals.Time + Wait;
		}
		else
		{
			SetTouch( null );
			NextThink = (float)(Server.Globals.Time + 0.1);
			SetThink( SubRemove );
		}
	}

	protected void CounterUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		TriggersLeft--;
		Activator = activator;

		if ( TriggersLeft < 0 )
			return;

		var tellActivator = Activator is { ClassName: "player" } && (SpawnFlags & SpawnFlagNoMessage) == 0;

		if ( TriggersLeft != 0 )
		{
			if ( tellActivator )
			{
				Util.Alert( AlertType.Console, TriggersLeft switch
				{
					1 => "Only 1 more to go...",
					2 => "Only 2 more to go...",
					3 => "Only 3 more to go...",
					_ => "There are more to go..."
				} );
			}

			return;
		}

		if ( tellActivator )
			Util.Alert( AlertType.Console, "Sequence completed!" );

		ActivateMultiTrigger( Activator );
	}

	protected void TeleportTouch( BaseEntity other )
	{
		if ( (other.EntityFlags & (EntityFlags.Client | EntityFlags.Monster)) == 0 )
			return;

		if ( !IsMasterTriggered( Master, other ) )
			return;

		if ( (SpawnFlags & SpawnFlagAllowMonsters) == 0 && (other.EntityFlags & EntityFlags.Monster) == EntityFlags.Monster )
			return;

		if ( (SpawnFlags & SpawnFlagNoClients) != 0 && other.IsPlayer )
			return;

		var target = Server.FindEntityByString( null, "targetname", Target );
		if ( target is null )
			return;

		var destination = target.Origin;

		if ( other.IsPlayer )
			destination.z -= other.Mins.z;

		destination.z++;

		other.EntityFlags &= ~EntityFlags.OnGround;

		Server.SetOrigin( other, destination );

		other.Angles = target.Angles;

		if ( other.IsPlayer )
			other.ViewAngle = target.Angles;

		other.FixAngle = 1;
		other.Velocity = Vector3.Zero;
		other.BaseVelocity = Vector3.Zero;
	}

	private void MultiWaitOver()
	{
		SetThink( null );
	}
}

[LinkEntityToClass( "trigger_hurt" )]
public class TriggerHurt : BaseTrigger
{
	public override void Spawn()
	{
		InitTrigger();
		SetTouch( HurtTouch );
		SetUse( string.IsNullOrEmpty( TargetName ) ? null : ToggleUse );

		if ( (BitsDamageInflict & DamageRadiation) != 0 )
		{
			SetThink( RadiationThink );
			NextThink = Server.Globals.Time + Server.RandomFloat( 0f, 0.5f );
		}

		if ( (SpawnFlags & SpawnFlagHurtStartOff) != 0 )
			Solid = Solid.Not;

		SetOrigin( Origin );
	}

	public void RadiationThink()
	{
		var origin = Origin;
		var viewOffset = ViewOffset;

		Origin = (AbsMin + AbsMax) * 0.5f;
		ViewOffset *= 0f;

		var client = Server.FindClientInPvs( this );

		Origin = origin;
		ViewOffset = viewOffset;

		if ( !Util.IsNullEnt( client ) && client is Player player )
		{
			var spot1 = (AbsMin + AbsMax) * 0.5f;
			var spot2 = (player.AbsMin + player.AbsMax) * 0.5f;
			var range = MathLib.Length( spot1 - spot2 );

			if ( player.GeigerRange >= range )
				player.GeigerRange = range;
		}

		NextThink = (float)(Server.Globals.Time + 0.25);
	}
}

[LinkEntityToClass( "trigger_monsterjump" )]
public class TriggerMonsterJump : BaseTrigger
{
	public override void Spawn()
	{
		SetMoveDir();
		InitTrigger();

		NextThink = 0f;
		Speed = 200f;
		Height = 150f;

		if ( string.IsNullOrEmpty( TargetName ) )
			return;

		Solid = Solid.Not;
		SetOrigin( Origin );
		SetUse( ToggleUse );
	}

	public override void Think()
	{
		Solid = Solid.Not;
		SetOrigin( Origin );
		SetThink( null );
	}

	public override void Touch( BaseEntity other )
	{
		if ( (other.EntityFlags & EntityFlags.Monster) != EntityFlags.Monster )
			return;

		other.Origin += new Vector3( 0f, 0f, 1f );
		other.EntityFlags &= ~EntityFlags.OnGround;

		var velocity = MoveDir * Speed;
		velocity.z += Height;
		other.Velocity = velocity;

		NextThink = Server.Globals.Time;
	}
}

[LinkEntityToClass( "trigger_cdaudio" )]
public class TriggerCdAudio : BaseTrigger
{
	public override void Spawn()
	{
		InitTrigger();
	}

	public override void Touch( BaseEntity other )
	{
		if ( other.IsPlayer )
			PlayTrack();
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		PlayTrack();
	}

	private void PlayTrack()
	{
		Triggers.PlayCdTrack( Server, (int)Health );

		SetTouch( null );
		Remove();
	}
}

[LinkEntityToClass( "target_cdaudio" )]
public class TargetCdAudio : PointEntity
{
	public override bool KeyValue( string key, string value )
	{
		if ( key == "radius" )
		{
			Scale = Util.Atof( value );
			return true;
		}

		return base.KeyValue( key, value );
	}

	public override void Spawn()
	{
		Solid = Solid.Not;
		MoveType = MoveType.None;

		if ( Scale > 0f )
			NextThink = (float)(Server.Globals.Time + 1.0);
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		Play();
	}

	public override void Think()
	{
		var client = Server.EntityOfEntIndex( 1 );
		if ( client is null )
			return;

		NextThink = (float)(Server.Globals.Time + 0.5);

		if ( MathLib.Length( client.Origin - Origin ) <= Scale )
			Play();
	}

	private void Play()
	{
		Triggers.PlayCdTrack( Server, (int)Health );
		Remove();
	}
}

[LinkEntityToClass( "trigger_multiple" )]
public class TriggerMultiple : BaseTrigger
{
	public override void Spawn()
	{
		if ( Wait == 0f )
			Wait = 0.2f;

		InitTrigger();
		SetTouch( MultiTouch );
	}
}

[LinkEntityToClass( "trigger_once" )]
public class TriggerOnce : TriggerMultiple
{
	public override void Spawn()
	{
		Wait = -1f;
		base.Spawn();
	}
}

[LinkEntityToClass( "trigger_counter" )]
public class TriggerCounter : BaseTrigger
{
	public override void Spawn()
	{
		Wait = -1f;

		if ( TriggersLeft == 0 )
			TriggersLeft = 2;

		SetUse( CounterUse );
	}
}

[LinkEntityToClass( "trigger_transition" )]
public class TriggerVolume : PointEntity
{
	public override void Spawn()
	{
		Solid = Solid.Not;
		MoveType = MoveType.None;
		SetModel( ModelName );
		ModelName = null;
		ModelIndex = 0;
	}
}

[LinkEntityToClass( "fireanddie" )]
public class FireAndDie : BaseDelay
{
	public override ObjectCaps ObjectCaps => base.ObjectCaps | ObjectCaps.ForceTransition;

	public override void Spawn()
	{
		ClassName = "fireanddie";
	}

	public override void Precache()
	{
		NextThink = Server.Globals.Time + Delay;
	}

	public override void Think()
	{
		UseTargets( this, UseType.Toggle, 0f );
		Remove();
	}
}

[LinkEntityToClass( "trigger_changelevel" )]
public class ChangeLevel : BaseTrigger
{
	private const int SpawnFlagUseOnly = 0x0002;
	private const int MapNameMost = 32;

	[Save( FieldType.String )]
	public string MapName { get; set; } = string.Empty;
	[Save( FieldType.String )]
	public string LandmarkName { get; set; } = string.Empty;
	[Save( FieldType.String )]
	public string ChangeTarget { get; set; }
	[Save( FieldType.Float )]
	public float ChangeTargetDelay { get; set; }

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "map":
				if ( value.Length >= MapNameMost )
					Util.Alert( AlertType.Error, $"Map name '{value}' too long (32 chars)\n" );

				MapName = value;
				return true;
			case "landmark":
				if ( value.Length >= MapNameMost )
					Util.Alert( AlertType.Error, $"Landmark name '{value}' too long (32 chars)\n" );

				LandmarkName = value;
				return true;
			case "changetarget":
				ChangeTarget = Server.AllocString( value );
				return true;
			case "changedelay":
				ChangeTargetDelay = Util.Atof( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Spawn()
	{
		if ( MapName.Length == 0 )
			Util.Alert( AlertType.Console, "a trigger_changelevel doesn't have a map" );

		if ( LandmarkName.Length == 0 )
			Util.Alert( AlertType.Console, $"trigger_changelevel to {MapName} doesn't have a landmark" );

		if ( !string.IsNullOrEmpty( TargetName ) )
			SetUse( UseChangeLevel );

		InitTrigger();

		if ( (SpawnFlags & SpawnFlagUseOnly) == 0 )
			SetTouch( TouchChangeLevel );
	}

	private static Edict FindLandmark( Server server, string landmarkName )
	{
		Edict landmark = null;

		while ( (landmark = server.FindEntityByString( landmark, "targetname", landmarkName )) is not null )
		{
			if ( landmark.ClassName == "info_landmark" )
				return landmark;
		}

		Util.Alert( AlertType.Error, $"Can't find landmark {landmarkName}\n" );
		return null;
	}

	private void UseChangeLevel( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		ChangeLevelNow( activator );
	}

	private void TouchChangeLevel( BaseEntity other )
	{
		if ( other.ClassName != "player" )
			return;

		ChangeLevelNow( other );
	}

	private void ChangeLevelNow( BaseEntity activator )
	{
		if ( Server.Deathmatch )
			return;

		if ( Server.Globals.Time == DamageTime )
			return;

		DamageTime = Server.Globals.Time;

		var player = (BaseEntity)Server.EntityOfEntIndex( 1 );

		if ( !InTransitionVolume( Server, player, LandmarkName ) )
		{
			Util.Alert( AlertType.AiConsole, $"Player isn't in the transition volume {LandmarkName}, aborting\n" );
			return;
		}

		if ( ChangeTarget is not null )
		{
			var fireAndDie = Server.CreateEntity<FireAndDie>();

			fireAndDie.Target = ChangeTarget;
			fireAndDie.Delay = ChangeTargetDelay;
			fireAndDie.Origin = player.Origin;
			fireAndDie.DispatchSpawn();
		}

		Activator = activator;
		UseTargets( activator, UseType.Toggle, 0f );

		var nextSpot = string.Empty;

		if ( FindLandmark( Server, LandmarkName ) is { } landmark )
		{
			nextSpot = LandmarkName;
			Server.Globals.LandmarkOffset = landmark.Origin;
		}

		Util.Alert( AlertType.Console, $"CHANGE LEVEL: {MapName} {nextSpot}\n" );
		Server.ChangeLevel( MapName, nextSpot );
	}

	public static int ChangeList( Server server, List<LevelList> levelList, int maxList )
	{
		levelList.Clear();

		Edict found = null;

		while ( (found = server.FindEntityByString( found, "classname", "trigger_changelevel" )) is not null )
		{
			if ( found is not ChangeLevel trigger || FindLandmark( server, trigger.LandmarkName ) is not { } landmark )
				continue;

			if ( levelList.Exists( x => x.Landmark == landmark && x.MapName == trigger.MapName ) )
				continue;

			levelList.Add( new LevelList
			{
				MapName = trigger.MapName,
				LandmarkName = trigger.LandmarkName,
				Landmark = landmark,
				LandmarkOrigin = landmark.Origin
			} );

			if ( levelList.Count >= maxList )
				break;
		}

		if ( server.Globals.SaveData is not { Table.Length: > 0 } saveData )
			return levelList.Count;

		var saveHelper = new Save( saveData );

		for ( var i = 0; i < levelList.Count; i++ )
		{
			var entities = new List<(BaseEntity Entity, int Flags)>();

			foreach ( var edict in server.EntitiesInPvs( levelList[i].Landmark ) )
			{
				if ( edict is not BaseEntity entity )
					continue;

				var caps = entity.ObjectCaps;

				if ( (caps & ObjectCaps.DontSave) == ObjectCaps.DontSave )
					continue;

				var flags = 0;

				if ( (caps & ObjectCaps.AcrossTransition) == ObjectCaps.AcrossTransition )
					flags |= EntityTable.Moveable;

				if ( entity.GlobalName is not null && (entity.EntityFlags & EntityFlags.Dormant) != EntityFlags.Dormant )
					flags |= EntityTable.Global;

				if ( flags == 0 )
					continue;

				entities.Add( (entity, flags) );

				if ( entities.Count > MaxEntity )
					Util.Alert( AlertType.Error, "Too many entities across a transition!" );
			}

			foreach ( var (entity, flags) in entities )
			{
				if ( InTransitionVolume( server, entity, levelList[i].LandmarkName ) )
					saveHelper.EntityFlagsSet( saveHelper.EntityIndex( entity ), flags | (1 << i) );
			}
		}

		return levelList.Count;
	}

	private const int MaxEntity = 512;

	private static bool InTransitionVolume( Server server, BaseEntity entity, string volumeName )
	{
		if ( (entity.ObjectCaps & ObjectCaps.ForceTransition) == ObjectCaps.ForceTransition )
			return true;

		if ( entity.MoveType == MoveType.Follow && entity.AimEntity is BaseEntity aim )
			entity = aim;

		var inVolume = true;
		Edict volume = null;

		while ( (volume = server.FindEntityByString( volume, "targetname", volumeName )) is not null )
		{
			if ( volume is not BaseEntity { ClassName: "trigger_transition" } transition )
				continue;

			if ( transition.Intersects( entity ) )
				return true;

			inVolume = false;
		}

		return inVolume;
	}
}

[LinkEntityToClass( "func_ladder" )]
public class Ladder : BaseTrigger
{
	private const int ContentsLadder = -16;

	public override void Precache()
	{
		Solid = Solid.Not;
		Skin = ContentsLadder;

		if ( ShowTriggers == 0f )
		{
			RenderMode = RenderMode.TransTexture;
			RenderAmt = 0f;
		}

		Effects &= ~Effects.NoDraw;
	}

	public override void Spawn()
	{
		Precache();

		SetModel( ModelName );
		MoveType = MoveType.Push;
	}
}

[LinkEntityToClass( "trigger_push" )]
public class TriggerPush : BaseTrigger
{
	private const int SpawnFlagPushOnce = 1;
	private const int SpawnFlagPushStartOff = 2;

	public override void Spawn()
	{
		if ( Angles.x == 0f && Angles.y == 0f && Angles.z == 0f )
			Angles = new Vector3( 0f, 360f, 0f );

		InitTrigger();

		if ( Speed == 0f )
			Speed = 100f;

		if ( (SpawnFlags & SpawnFlagPushStartOff) != 0 )
			Solid = Solid.Not;

		SetUse( ToggleUse );
		SetOrigin( Origin );
	}

	public override void Touch( BaseEntity other )
	{
		if ( other.MoveType is MoveType.None or MoveType.Push or MoveType.NoClip or MoveType.Follow )
			return;

		if ( other.Solid is Solid.Not or Solid.Bsp )
			return;

		if ( (SpawnFlags & SpawnFlagPushOnce) != 0 )
		{
			other.Velocity += Speed * MoveDir;

			if ( other.Velocity.z > 0f )
				other.EntityFlags &= ~EntityFlags.OnGround;

			Remove();
			return;
		}

		var push = Speed * MoveDir;

		if ( (other.EntityFlags & EntityFlags.BaseVelocity) == EntityFlags.BaseVelocity )
			push += other.BaseVelocity;

		other.BaseVelocity = push;
		other.EntityFlags |= EntityFlags.BaseVelocity;
	}
}

[LinkEntityToClass( "trigger_teleport" )]
public class TriggerTeleport : BaseTrigger
{
	public override void Spawn()
	{
		InitTrigger();
		SetTouch( TeleportTouch );
	}
}

[LinkEntityToClass( "trigger_autosave" )]
public class TriggerSave : BaseTrigger
{
	public override void Spawn()
	{
		if ( Server.Deathmatch )
		{
			Server.FreeEdict( this );
			return;
		}

		InitTrigger();
		SetTouch( SaveTouch );
	}

	private void SaveTouch( BaseEntity other )
	{
		if ( !IsMasterTriggered( Master, other ) )
			return;

		if ( !other.IsPlayer )
			return;

		SetTouch( null );
		Remove();

		Server.ServerCommand( "autosave\n" );
	}
}

[LinkEntityToClass( "trigger_endsection" )]
public class TriggerEndSection : BaseTrigger
{
	private const int SpawnFlagUseOnly = 0x0001;

	public override bool KeyValue( string key, string value )
	{
		if ( key == "section" )
		{
			Message = Server.AllocString( value );
			return true;
		}

		return base.KeyValue( key, value );
	}

	public override void Spawn()
	{
		if ( HalfLifeDll.GameRules.IsDeathmatch() )
		{
			Server.FreeEdict( this );
			return;
		}

		InitTrigger();

		SetUse( EndSectionUse );

		if ( (SpawnFlags & SpawnFlagUseOnly) == 0 )
			SetTouch( EndSectionTouch );
	}

	private void EndSectionUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( activator is not null && !activator.IsNetClient )
			return;

		SetUse( null );

		if ( !string.IsNullOrEmpty( Message ) )
			Server.EndSection( Message );

		Remove();
	}

	private void EndSectionTouch( BaseEntity other )
	{
		if ( !other.IsNetClient )
			return;

		SetTouch( null );

		if ( !string.IsNullOrEmpty( Message ) )
			Server.EndSection( Message );

		Remove();
	}
}

[LinkEntityToClass( "trigger_gravity" )]
public class TriggerGravity : BaseTrigger
{
	public override void Spawn()
	{
		InitTrigger();
		SetTouch( GravityTouch );
	}

	private void GravityTouch( BaseEntity other )
	{
		if ( other.IsPlayer )
			other.Gravity = Gravity;
	}
}

[LinkEntityToClass( "trigger_changetarget" )]
public class TriggerChangeTarget : BaseDelay
{
	[Save( FieldType.String )]
	public string NewTarget { get; set; }

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override bool KeyValue( string key, string value )
	{
		if ( key == "m_iszNewTarget" )
		{
			NewTarget = Server.AllocString( value );
			return true;
		}

		return base.KeyValue( key, value );
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( Server.FindEntityByString( null, "targetname", Target ) is { } target )
			target.Target = NewTarget;
	}
}

public static class Triggers
{
	public static UseType TriggerStateToUseType( int state )
	{
		return state switch
		{
			0 => UseType.Off,
			2 => UseType.Toggle,
			_ => UseType.On
		};
	}

	public static string StripToken( string key, int maxLength )
	{
		var end = key.IndexOf( '#' );
		if ( end < 0 )
			end = key.Length;

		return key[..System.Math.Min( end, maxLength )];
	}

	public static void PlayCdTrack( Server server, int track )
	{
		var client = server.EntityOfEntIndex( 1 );
		if ( client is null )
			return;

		if ( track is < -1 or > 30 )
		{
			Util.Alert( AlertType.Console, "TriggerCDAudio - Track %d out of range\n" );
			return;
		}

		server.ClientCommand( client, track == -1 ? "cd stop\n" : $"cd play {track,3}\n" );
	}
}
