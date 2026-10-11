using System;
using GoldSrc;

namespace HalfLife;

public enum UseType
{
	Off,
	On,
	Set,
	Toggle
}

[Flags]
public enum ObjectCaps : uint
{
	CustomSave = 0x00000001,
	AcrossTransition = 0x00000002,
	MustSpawn = 0x00000004,
	ImpulseUse = 0x00000008,
	ContinuousUse = 0x00000010,
	OnOffUse = 0x00000020,
	DirectionalUse = 0x00000040,
	Master = 0x00000080,
	ForceTransition = 0x00000080,
	DontSave = 0x80000000
}

public partial class BaseEntity : Edict
{
	public override bool HasPrivateData => true;

	public delegate void UseFunction( BaseEntity activator, BaseEntity caller, UseType useType, float value );

	public virtual ObjectCaps ObjectCaps => ObjectCaps.AcrossTransition;

	[Save( FieldType.ClassPtr )]
	public BaseEntity GoalEntity;

	public BaseEntity NextLink;

	[Save( FieldType.Function )]
	private Action _think;
	[Save( FieldType.Function )]
	private Action<BaseEntity> _touch;
	[Save( FieldType.Function )]
	private UseFunction _use;
	[Save( FieldType.Function )]
	private Action<BaseEntity> _blocked;

	protected static HalfLifeClientDll ClientWeapons => (HalfLifeClientDll)ClientDll.Loaded;

	public GlobalVars Globals => ClientSide ? ClientWeapons.Globals : Server.Globals;

	protected bool IsMultiplayer => ClientSide ? ClientWeapons.IsMultiplayer : HalfLifeDll.GameRules.IsMultiplayer();

	protected int MaxClients => ClientSide ? 0 : Server.MaxClients;

	protected int PrecacheModel( string name ) => ClientSide ? 0 : Server.PrecacheModel( name );

	protected int PrecacheSound( string name ) => ClientSide ? 0 : Server.PrecacheSound( name );

	protected int PrecacheEvent( int type, string name ) => ClientSide ? ClientWeapons.Engine.PrecacheEvent( type, name ) : Server.PrecacheEvent( type, name );

	protected void MakeVectors( Vector3 angles )
	{
		if ( !ClientSide )
			Server.Globals.MakeVectors( angles );
	}

	protected Trace TraceLine( Vector3 start, Vector3 end, int ignoreMonsters, Edict ignore )
	{
		return ClientSide ? new Trace { Fraction = 1f } : Util.TraceLine( start, end, ignoreMonsters, ignore );
	}

	protected void PlaybackEvent( EventFlags flags, Edict invoker, int eventIndex, float delay, Vector3 origin, Vector3 angles, float fparam1, float fparam2, int iparam1, int iparam2, int bparam1, int bparam2 )
	{
		if ( ClientSide )
			ClientWeapons.PlaybackEvent( flags, eventIndex, delay, fparam1, fparam2, iparam1, iparam2, bparam1, bparam2 );
		else
			Server.PlaybackEvent( flags, invoker, eventIndex, delay, origin, angles, fparam1, fparam2, iparam1, iparam2, bparam1, bparam2 );
	}

	public virtual void Spawn()
	{
	}

	public virtual void Precache()
	{
	}

	public virtual bool KeyValue( string key, string value ) => false;

	public virtual void Think() => _think?.Invoke();

	public virtual void Touch( BaseEntity other ) => _touch?.Invoke( other );

	public virtual void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		_use?.Invoke( activator, caller, useType, value );
	}

	public virtual void Blocked( BaseEntity other ) => _blocked?.Invoke( other );

	public virtual void SetObjectCollisionBox()
	{
		if ( Solid == Solid.Bsp && (Angles.x != 0f || Angles.y != 0f || Angles.z != 0f) )
		{
			var max = 0f;

			for ( var i = 0; i < 3; i++ )
			{
				max = MathF.Max( max, MathF.Abs( Mins[i] ) );
				max = MathF.Max( max, MathF.Abs( Maxs[i] ) );
			}

			AbsMin = Origin - max;
			AbsMax = Origin + max;
		}
		else
		{
			AbsMin = Origin + Mins;
			AbsMax = Origin + Maxs;
		}

		AbsMin -= 1f;
		AbsMax += 1f;
	}

	public virtual bool IsPlayer => false;

	public virtual bool IsAlive => DeadFlag == 0 && Health > 0f;

	public virtual float GetDelay() => 0f;

	public virtual bool OnControls( BaseEntity entity ) => false;

	public virtual void Activate()
	{
	}

	public virtual void OverrideReset()
	{
	}

	public virtual bool Save( Save save )
	{
		if ( !save.WriteFields( "ENTVARS", this, typeof( Edict ) ) )
			return false;

		foreach ( var type in SaveClasses() )
		{
			if ( !save.WriteFields( type.Name, this, type ) )
				return false;
		}

		return true;
	}

	public virtual bool Restore( Restore restore )
	{
		var status = restore.ReadFields( "ENTVARS", this, typeof( Edict ) );

		foreach ( var type in SaveClasses() )
		{
			if ( !status )
				break;

			status = restore.ReadFields( type.Name, this, type );
		}

		if ( ModelIndex != 0 && !string.IsNullOrEmpty( ModelName ) )
		{
			var mins = Mins;
			var maxs = Maxs;

			Server.PrecacheModel( ModelName );
			SetModel( ModelName );
			SetSize( mins, maxs );
		}

		return status;
	}

	private List<Type> SaveClasses()
	{
		var classes = new List<Type>();

		for ( var type = Game.TypeLibrary.GetType( GetType() ); type.TargetType != typeof( BaseEntity ); )
		{
			if ( Array.Exists( type.DeclaredMembers, x => x.HasAttribute<SaveAttribute>() ) )
				classes.Insert( 0, type.TargetType );

			type = type.GetAttribute<SaveRestoreBaseAttribute>() is { } attribute ? Game.TypeLibrary.GetType( attribute.BaseClass ) : type.BaseType;
		}

		classes.Insert( 0, typeof( BaseEntity ) );

		return classes;
	}

	public virtual BaseEntity GetNextTarget()
	{
		if ( string.IsNullOrEmpty( Target ) )
			return null;

		return Server.FindEntityByString( null, "targetname", Target ) as BaseEntity;
	}

	public virtual bool HasTarget( string targetName ) => targetName == TargetName;

	public virtual bool IsTriggered( BaseEntity activator ) => true;

	public virtual bool TakeHealth( float health, int damageType )
	{
		if ( DamageMode == DamageNo )
			return false;

		if ( Health >= MaxHealth )
			return false;

		Health += health;

		if ( Health > MaxHealth )
			Health = MaxHealth;

		return true;
	}

	public virtual bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType ) => EntityTakeDamage( inflictor, attacker, damage, damageType );

	protected bool EntityTakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		if ( DamageMode == DamageNo )
			return false;

		AttackDir = Util.Normalize( inflictor.Origin - BModelOrigin );

		if ( inflictor.Index != 0 && MoveType is MoveType.Walk or MoveType.Step && attacker.Solid != Solid.Trigger )
		{
			var push = Util.Normalize( Origin - ((inflictor.AbsMin + inflictor.AbsMax) * 0.5f) );

			var force = (float)(damage * (32 * 32 * 72.0 / (Size.x * Size.y * Size.z)) * 5);
			if ( force > 1000f )
				force = 1000f;

			Velocity += push * force;
		}

		Health -= damage;

		if ( Health <= 0f )
		{
			Killed( attacker, GibNormal );
			return false;
		}

		return true;
	}

	public virtual void Killed( BaseEntity attacker, int gib )
	{
		if ( ClientSide )
		{
			Effects |= Effects.NoDraw;
			return;
		}

		DamageMode = DamageNo;
		DeadFlag = DeadDead;
		Remove();
	}

	public virtual void UpdateOnRemove()
	{
		if ( (EntityFlags & EntityFlags.Graphed) == EntityFlags.Graphed )
		{
			var graph = HalfLifeDll.WorldGraph;

			for ( var i = 0; i < graph.LinkCount; i++ )
			{
				if ( graph.LinkPool[i].LinkEntity == this )
					graph.LinkPool[i].LinkEntity = null;
			}
		}

		if ( GlobalName is not null )
			HalfLifeDll.GlobalState.EntitySetState( GlobalName, GlobalEntityState.Dead );
	}

	public void MakeDormant()
	{
		EntityFlags |= EntityFlags.Dormant;
		Solid = Solid.Not;
		MoveType = MoveType.None;
		Effects |= Effects.NoDraw;
		NextThink = 0f;
		SetOrigin( Origin );
	}

	public bool Intersects( BaseEntity other )
	{
		return other.AbsMin.x <= AbsMax.x && other.AbsMin.y <= AbsMax.y && other.AbsMin.z <= AbsMax.z
			&& other.AbsMax.x >= AbsMin.x && other.AbsMax.y >= AbsMin.y && other.AbsMax.z >= AbsMin.z;
	}

	protected BaseEntity Create( string className, Vector3 origin, Vector3 angles, Edict owner )
	{
		if ( Server.CreateNamedEntity( className ) is not BaseEntity entity )
		{
			Util.Alert( AlertType.Console, "NULL Ent in Create!\n" );
			return null;
		}

		entity.Owner = owner;
		entity.Origin = origin;
		entity.Angles = angles;
		entity.DispatchSpawn();

		return entity;
	}

	public void CallUseToggle() => Use( this, this, UseType.Toggle, 0f );

	protected BaseEntity World => Server.GetEdict( 0 ) as BaseEntity;

	public void SubDoNothing()
	{
	}

	public void SubRemove()
	{
		UpdateOnRemove();

		if ( Health > 0f )
		{
			Health = 0f;
			Util.Alert( AlertType.AiConsole, "SUB_Remove called on entity with health > 0\n" );
		}

		Server.FreeEdict( this );
	}

	public void Remove()
	{
		UpdateOnRemove();

		EntityFlags |= EntityFlags.KillMe;
		TargetName = null;
	}

	public void UseTargets( BaseEntity activator, UseType useType, float value )
	{
		if ( !string.IsNullOrEmpty( Target ) )
			FireTargets( Target, activator, this, useType, value );
	}

	public void FireTargets( string targetName, BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( targetName is null )
			return;

		Util.Alert( AlertType.AiConsole, $"Firing: ({targetName})\n" );

		Edict found = null;

		while ( (found = Server.FindEntityByString( found, "targetname", targetName )) is not null )
		{
			if ( found is BaseEntity target && (target.EntityFlags & EntityFlags.KillMe) != EntityFlags.KillMe )
			{
				Util.Alert( AlertType.AiConsole, $"Found: {target.ClassName}, firing ({targetName})\n" );
				target.Use( activator, caller, useType, value );
			}
		}
	}

	public bool IsMasterTriggered( string master, BaseEntity activator )
	{
		if ( string.IsNullOrEmpty( master ) )
			return true;

		if ( Server.FindEntityByString( null, "targetname", master ) is BaseEntity entity && (entity.ObjectCaps & ObjectCaps.Master) == ObjectCaps.Master )
			return entity.IsTriggered( activator );

		Util.Alert( AlertType.Console, "Master was null or not a master!\n" );
		return true;
	}

	public Vector3 BModelOrigin => AbsMin + (Size * 0.5f);

	public bool ShouldToggle( UseType useType, bool currentState )
	{
		if ( useType is not (UseType.Toggle or UseType.Set) )
		{
			if ( (currentState && useType == UseType.On) || (!currentState && useType == UseType.Off) )
				return false;
		}

		return true;
	}

	public sealed override int DispatchSpawn()
	{
		AbsMin = Origin - 1f;
		AbsMax = Origin + 1f;

		Spawn();

		if ( Server.GetEdict( Index ) != this )
			return 0;

		if ( (EntityFlags & EntityFlags.KillMe) == EntityFlags.KillMe )
			return -1;

		if ( GlobalName is null )
			return 0;

		var globalState = HalfLifeDll.GlobalState;

		if ( !globalState.Contains( GlobalName ) )
			globalState.EntityAdd( GlobalName, Server.Globals.MapName, GlobalEntityState.On );
		else if ( globalState.EntityGetState( GlobalName ) == GlobalEntityState.Dead )
			return -1;
		else if ( Server.Globals.MapName != globalState.EntityLevelName( GlobalName ) )
			MakeDormant();

		return 0;
	}

	public sealed override bool DispatchKeyValue( string className, string key, string value )
	{
		return EntVarsKeyValue( key, value ) || KeyValue( key, value );
	}

	public sealed override void SetAbsBox() => SetObjectCollisionBox();

	public sealed override void DispatchSave( SaveRestoreData saveData )
	{
		var table = saveData.Current;

		if ( table.Edict != this )
			Util.Alert( AlertType.Error, "ENTITY TABLE OR INDEX IS WRONG!!!!\n" );

		if ( (ObjectCaps & ObjectCaps.DontSave) == ObjectCaps.DontSave )
			return;

		if ( MoveType == MoveType.Push )
		{
			var delta = NextThink - LTime;

			LTime = Server.Globals.Time;
			NextThink = LTime + delta;
		}

		table.ClassName = ClassName;

		Save( new Save( saveData ) );
	}

	public sealed override int DispatchRestore( SaveRestoreData saveData, bool globalEntity )
	{
		var entity = this;
		var restore = new Restore( saveData );
		var oldOffset = Vector3.Zero;
		var globalState = HalfLifeDll.GlobalState;

		if ( globalEntity )
		{
			var entVars = saveData.Current.Blocks[0].Fields;

			var globalName = entVars.GetValueOrDefault( nameof( GlobalName ) ) as string;
			var className = entVars.GetValueOrDefault( nameof( ClassName ) ) as string;
			var mins = entVars.GetValueOrDefault( nameof( Mins ) ) is Vector3 saved ? saved : Vector3.Zero;

			if ( saveData.CurrentMapName != globalState.EntityLevelName( globalName ) )
				return 0;

			oldOffset = saveData.LandmarkOffset;

			if ( FindGlobalEntity( className, globalName ) is not { } newEntity )
				return 0;

			restore.Global = true;
			saveData.LandmarkOffset = saveData.LandmarkOffset - newEntity.Mins + mins;
			entity = newEntity;

			globalState.EntityUpdate( entity.GlobalName, Server.Globals.MapName );
		}

		entity.Restore( restore );

		if ( (entity.ObjectCaps & ObjectCaps.MustSpawn) == ObjectCaps.MustSpawn )
			entity.Spawn();
		else
			entity.Precache();

		if ( Server.GetEdict( entity.Index ) != entity )
			entity = null;

		if ( globalEntity )
		{
			saveData.LandmarkOffset = oldOffset;

			if ( entity is not null )
			{
				entity.SetOrigin( entity.Origin );
				entity.OverrideReset();
			}
		}
		else if ( entity?.GlobalName is not null )
		{
			if ( !globalState.Contains( entity.GlobalName ) )
			{
				Util.Alert( AlertType.Error, $"Global Entity {entity.GlobalName} ({entity.ClassName}) not in table!!!\n" );
				globalState.EntityAdd( entity.GlobalName, Server.Globals.MapName, GlobalEntityState.On );
			}
			else if ( globalState.EntityGetState( entity.GlobalName ) == GlobalEntityState.Dead )
			{
				return -1;
			}
			else if ( Server.Globals.MapName != globalState.EntityLevelName( entity.GlobalName ) )
			{
				entity.MakeDormant();
			}
		}

		return 0;
	}

	private BaseEntity FindGlobalEntity( string className, string globalName )
	{
		if ( Server.FindEntityByString( null, "globalname", globalName ) is not BaseEntity entity )
			return null;

		if ( entity.ClassName != className )
		{
			Util.Alert( AlertType.Console, $"Global entity found {globalName}, wrong class {entity.ClassName}\n" );
			return null;
		}

		return entity;
	}

	public sealed override void DispatchThink()
	{
		if ( (EntityFlags & EntityFlags.Dormant) == EntityFlags.Dormant )
			Util.Alert( AlertType.Error, $"Dormant entity {ClassName} is thinking!!\n" );

		Think();
	}

	public sealed override void DispatchTouch( Edict other )
	{
		if ( HalfLifeDll.TouchDisabled )
			return;

		if ( other is BaseEntity entity && ((EntityFlags | entity.EntityFlags) & EntityFlags.KillMe) != EntityFlags.KillMe )
			Touch( entity );
	}

	public sealed override void DispatchBlocked( Edict other )
	{
		Blocked( other as BaseEntity );
	}

	protected void SetMoveDir()
	{
		if ( Angles.x == 0f && Angles.y == -1f && Angles.z == 0f )
		{
			MoveDir = new Vector3( 0f, 0f, 1f );
		}
		else if ( Angles.x == 0f && Angles.y == -2f && Angles.z == 0f )
		{
			MoveDir = new Vector3( 0f, 0f, -1f );
		}
		else
		{
			Server.Globals.MakeVectors( Angles );
			MoveDir = Server.Globals.Forward;
		}

		Angles = Vector3.Zero;
	}

	public void SetThink( Action think ) => _think = think;

	public void SetTouch( Action<BaseEntity> touch ) => _touch = touch;

	public void SetUse( UseFunction use ) => _use = use;

	public void SetBlocked( Action<BaseEntity> blocked ) => _blocked = blocked;

	protected void PrecacheSounds( string[] sounds )
	{
		foreach ( var sound in sounds )
			Server.PrecacheSound( sound );
	}

	protected static string RandomSound( string[] sounds ) => sounds[Server.RandomLong( 0, sounds.Length - 1 )];

	protected void SetModel( string name )
	{
		if ( !ClientSide )
			Server.SetModel( this, name );
	}

	protected void SetOrigin( Vector3 origin )
	{
		if ( !ClientSide )
			Server.SetOrigin( this, origin );
	}

	protected void SetSize( Vector3 mins, Vector3 maxs )
	{
		if ( !ClientSide )
			Server.SetSize( this, mins, maxs );
	}
}
