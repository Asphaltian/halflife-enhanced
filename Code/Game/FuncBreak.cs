using System;
using GoldSrc;

namespace HalfLife;

public enum Explosions
{
	Random,
	Directed
}

public enum Materials
{
	Glass,
	Wood,
	Metal,
	Flesh,
	CinderBlock,
	CeilingTile,
	Computer,
	UnbreakableGlass,
	Rocks,
	None,
	LastMaterial
}

[LinkEntityToClass( "func_breakable" )]
[SaveRestoreBase( typeof( BaseEntity ) )]
public class Breakable : BaseDelay
{
	protected const int SpawnFlagTriggerOnly = 1;
	protected const int SpawnFlagTouch = 2;
	protected const int SpawnFlagPressure = 4;
	protected const int SpawnFlagCrowbar = 256;

	private static readonly string[] SpawnObjects =
	[
		null,
		"item_battery",
		"item_healthkit",
		"weapon_9mmhandgun",
		"ammo_9mmclip",
		"weapon_9mmAR",
		"ammo_9mmAR",
		"ammo_ARgrenades",
		"weapon_shotgun",
		"ammo_buckshot",
		"weapon_crossbow",
		"ammo_crossbow",
		"weapon_357",
		"ammo_357",
		"weapon_rpg",
		"ammo_rpgclip",
		"ammo_gaussclip",
		"weapon_handgrenade",
		"weapon_tripmine",
		"weapon_satchel",
		"weapon_snark",
		"weapon_hornetgun"
	];

	[Save( FieldType.Integer )]
	public Materials Material { get; set; }

	private const int BreakGlass = 0x01;
	public const int BreakMetal = 0x02;
	public const int BreakFlesh = 0x04;
	private const int BreakWood = 0x08;
	private const int BreakConcrete = 0x40;

	public int Shard { get; private set; }
	[Save( FieldType.Integer )]
	public Explosions Explosion { get; set; }
	[Save( FieldType.String )]
	public string GibModel { get; set; }
	[Save( FieldType.String )]
	public string SpawnObject { get; set; }

	[Save( FieldType.Float )]
	private float _angle;

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public bool IsBreakable => Material != Materials.UnbreakableGlass;

	public bool Explodable => ExplosionMagnitude > 0;

	public int ExplosionMagnitude => Impulse;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "explosion":
				Explosion = string.Equals( value, "directed", StringComparison.OrdinalIgnoreCase ) ? Explosions.Directed : Explosions.Random;
				return true;

			case "material":
				var material = Util.Atoi( value );
				Material = material < 0 || material >= (int)Materials.LastMaterial ? Materials.Wood : (Materials)material;
				return true;

			case "deadmodel":
			case "shards":
			case "lip":
				return true;

			case "gibmodel":
				GibModel = Server.AllocString( value );
				return true;

			case "spawnobject":
				var spawnObject = Util.Atoi( value );

				if ( spawnObject > 0 && spawnObject < SpawnObjects.Length )
					SpawnObject = SpawnObjects[spawnObject];

				return true;

			case "explodemagnitude":
				Impulse = Util.Atoi( value );
				return true;

			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Spawn()
	{
		Precache();

		DamageMode = (SpawnFlags & SpawnFlagTriggerOnly) != 0 ? DamageNo : DamageYes;

		Solid = Solid.Bsp;
		MoveType = MoveType.Push;

		_angle = Angles.y;
		Angles = new Vector3( Angles.x, 0f, Angles.z );

		if ( Material == Materials.Glass )
			PlayerClass = 1;

		SetModel( ModelName );

		SetTouch( BreakTouch );

		if ( (SpawnFlags & SpawnFlagTriggerOnly) != 0 )
			SetTouch( null );

		if ( !IsBreakable && RenderMode != RenderMode.Normal )
			EntityFlags |= EntityFlags.WorldBrush;
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		BreakableUse();
	}

	public override void TraceAttack( BaseEntity attacker, float damage, Vector3 direction, in Trace trace, int damageType )
	{
		if ( Server.RandomLong( 0, 1 ) != 0 )
		{
			switch ( Material )
			{
				case Materials.Computer:
					Util.Sparks( trace.EndPos );

					var volume = Server.RandomFloat( 0.7f, 1f );

					switch ( Server.RandomLong( 0, 1 ) )
					{
						case 0:
							Server.EmitSound( this, SoundChannel.Voice, "buttons/spark5.wav", volume, Server.AttenuationNormal );
							break;
						case 1:
							Server.EmitSound( this, SoundChannel.Voice, "buttons/spark6.wav", volume, Server.AttenuationNormal );
							break;
					}

					break;

				case Materials.UnbreakableGlass:
					Util.Ricochet( trace.EndPos, Server.RandomFloat( 0.5f, 1.5f ) );
					break;
			}
		}

		base.TraceAttack( attacker, damage, direction, trace, damageType );
	}

	public override int DamageDecal( int damageType )
	{
		if ( Material == Materials.Glass )
			return Decals.GlassBreak1 + Server.RandomLong( 0, 2 );

		if ( Material == Materials.UnbreakableGlass )
			return Decals.BulletProof1;

		return base.DamageDecal( damageType );
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		return BreakableTakeDamage( inflictor, attacker, damage, damageType );
	}

	public static void MaterialSoundRandom( BaseEntity entity, Materials material, float volume )
	{
		var sounds = MaterialSoundList( material );

		if ( sounds.Length != 0 )
			entity.Server.EmitSound( entity, SoundChannel.Body, sounds[Server.RandomLong( 0, sounds.Length - 1 )], volume, 1f );
	}

	protected void BreakableUse()
	{
		if ( !IsBreakable )
			return;

		Angles = new Vector3( Angles.x, _angle, Angles.z );
		Server.Globals.MakeVectors( Angles );
		AttackDir = Server.Globals.Forward;

		Die();
	}

	protected bool BreakableTakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		var direction = inflictor.Origin - (AbsMin + (Size * 0.5f));

		if ( attacker == inflictor
			&& (attacker.EntityFlags & EntityFlags.Client) == EntityFlags.Client
			&& (SpawnFlags & SpawnFlagCrowbar) != 0
			&& (damageType & DamageClub) != 0 )
		{
			damage = Health;
		}

		if ( !IsBreakable )
			return false;

		if ( (damageType & DamageClub) != 0 )
			damage *= 2f;

		if ( (damageType & DamagePoison) != 0 )
			damage = (float)(damage * 0.1);

		AttackDir = Util.Normalize( direction );

		Health -= damage;

		if ( Health <= 0f )
		{
			Killed( attacker, GibNormal );
			Die();
			return false;
		}

		DamageSound();

		return true;
	}

	public static void MaterialSoundPrecache( Server server, Materials material )
	{
		foreach ( var sound in MaterialSoundList( material ) )
			server.PrecacheSound( sound );
	}

	public override void Precache()
	{
		string gibName = null;

		switch ( Material )
		{
			case Materials.Wood:
				gibName = "models/woodgibs.mdl";

				Server.PrecacheSound( "debris/bustcrate1.wav" );
				Server.PrecacheSound( "debris/bustcrate2.wav" );
				break;
			case Materials.Flesh:
				gibName = "models/fleshgibs.mdl";

				Server.PrecacheSound( "debris/bustflesh1.wav" );
				Server.PrecacheSound( "debris/bustflesh2.wav" );
				break;
			case Materials.Computer:
				Server.PrecacheSound( "buttons/spark5.wav" );
				Server.PrecacheSound( "buttons/spark6.wav" );
				gibName = "models/computergibs.mdl";

				Server.PrecacheSound( "debris/bustmetal1.wav" );
				Server.PrecacheSound( "debris/bustmetal2.wav" );
				break;
			case Materials.UnbreakableGlass:
			case Materials.Glass:
				gibName = "models/glassgibs.mdl";

				Server.PrecacheSound( "debris/bustglass1.wav" );
				Server.PrecacheSound( "debris/bustglass2.wav" );
				break;
			case Materials.Metal:
				gibName = "models/metalplategibs.mdl";

				Server.PrecacheSound( "debris/bustmetal1.wav" );
				Server.PrecacheSound( "debris/bustmetal2.wav" );
				break;
			case Materials.CinderBlock:
				gibName = "models/cindergibs.mdl";

				Server.PrecacheSound( "debris/bustconcrete1.wav" );
				Server.PrecacheSound( "debris/bustconcrete2.wav" );
				break;
			case Materials.Rocks:
				gibName = "models/rockgibs.mdl";

				Server.PrecacheSound( "debris/bustconcrete1.wav" );
				Server.PrecacheSound( "debris/bustconcrete2.wav" );
				break;
			case Materials.CeilingTile:
				gibName = "models/ceilinggibs.mdl";

				Server.PrecacheSound( "debris/bustceiling.wav" );
				break;
		}

		MaterialSoundPrecache( Server, Material );

		if ( !string.IsNullOrEmpty( GibModel ) )
			gibName = GibModel;

		Shard = Server.PrecacheModel( gibName );

		if ( SpawnObject is not null )
			Util.PrecacheOther( SpawnObject );
	}

	private static string[] MaterialSoundList( Materials material )
	{
		return material switch
		{
			Materials.Wood => ["debris/wood1.wav", "debris/wood2.wav", "debris/wood3.wav"],
			Materials.Flesh => ["debris/flesh1.wav", "debris/flesh2.wav", "debris/flesh3.wav", "debris/flesh5.wav", "debris/flesh6.wav", "debris/flesh7.wav"],
			Materials.Computer or Materials.UnbreakableGlass or Materials.Glass => ["debris/glass1.wav", "debris/glass2.wav", "debris/glass3.wav"],
			Materials.Metal => ["debris/metal1.wav", "debris/metal2.wav", "debris/metal3.wav"],
			Materials.CinderBlock or Materials.Rocks => ["debris/concrete1.wav", "debris/concrete2.wav", "debris/concrete3.wav"],
			_ => []
		};
	}

	private void DamageSound()
	{
		var material = Material;

		var pitch = Server.RandomLong( 0, 2 ) != 0 ? Server.PitchNormal : 95 + Server.RandomLong( 0, 34 );
		var volume = Server.RandomFloat( 0.75f, 1f );

		if ( material == Materials.Computer && Server.RandomLong( 0, 1 ) != 0 )
			material = Materials.Metal;

		string[] sounds = material switch
		{
			Materials.Computer or Materials.Glass or Materials.UnbreakableGlass => ["debris/glass1.wav", "debris/glass2.wav", "debris/glass3.wav"],
			Materials.Wood => ["debris/wood1.wav", "debris/wood2.wav", "debris/wood3.wav"],
			Materials.Metal => ["debris/metal1.wav", "debris/metal3.wav"],
			Materials.Flesh => ["debris/flesh1.wav", "debris/flesh2.wav", "debris/flesh3.wav", "debris/flesh5.wav", "debris/flesh6.wav", "debris/flesh7.wav"],
			Materials.Rocks or Materials.CinderBlock => ["debris/concrete1.wav", "debris/concrete2.wav", "debris/concrete3.wav"],
			_ => []
		};

		if ( sounds.Length != 0 )
			Server.EmitSound( this, SoundChannel.Voice, sounds[Server.RandomLong( 0, sounds.Length - 1 )], volume, Server.AttenuationNormal, 0, pitch );
	}

	private void BreakTouch( BaseEntity other )
	{
		if ( !other.IsPlayer || !IsBreakable )
			return;

		if ( (SpawnFlags & SpawnFlagTouch) != 0 )
		{
			var damage = (float)(MathLib.Length( other.Velocity ) * 0.01);

			if ( damage >= Health )
			{
				SetTouch( null );
				TakeDamage( other, other, damage, DamageCrush );

				other.TakeDamage( this, this, damage / 4f, DamageSlash );
			}
		}

		if ( (SpawnFlags & SpawnFlagPressure) != 0 && other.AbsMin.z >= Maxs.z - 2f )
		{
			DamageSound();

			SetThink( Die );
			SetTouch( null );

			if ( Delay == 0f )
				Delay = 0.1f;

			NextThink = LTime + Delay;
		}
	}

	private void Die()
	{
		var pitch = 95 + Server.RandomLong( 0, 29 );

		if ( pitch is > 97 and < 103 )
			pitch = 100;

		var volume = (float)(Server.RandomFloat( 0.85f, 1f ) + (MathF.Abs( Health ) / 100.0));

		if ( volume > 1f )
			volume = 1f;

		var bust = Material switch
		{
			Materials.Glass => "debris/bustglass",
			Materials.Wood => "debris/bustcrate",
			Materials.Computer or Materials.Metal => "debris/bustmetal",
			Materials.Flesh => "debris/bustflesh",
			Materials.Rocks or Materials.CinderBlock => "debris/bustconcrete",
			_ => null
		};

		if ( bust is not null )
			Server.EmitSound( this, SoundChannel.Voice, $"{bust}{Server.RandomLong( 0, 1 ) + 1}.wav", volume, Server.AttenuationNormal, 0, pitch );
		else if ( Material == Materials.CeilingTile )
			Server.EmitSound( this, SoundChannel.Voice, "debris/bustceiling.wav", volume, Server.AttenuationNormal, 0, pitch );

		var flag = Material switch
		{
			Materials.Glass => BreakGlass,
			Materials.Wood => BreakWood,
			Materials.Computer or Materials.Metal => BreakMetal,
			Materials.Flesh => BreakFlesh,
			Materials.Rocks or Materials.CinderBlock => BreakConcrete,
			_ => 0
		};

		var velocity = Explosion == Explosions.Directed ? AttackDir * 200f : Vector3.Zero;
		var spot = Origin + ((Mins + Maxs) * 0.5f);

		Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, spot );
		Server.WriteByte( TempEntity.BreakModel );
		Server.WriteCoord( spot.x );
		Server.WriteCoord( spot.y );
		Server.WriteCoord( spot.z );
		Server.WriteCoord( Size.x );
		Server.WriteCoord( Size.y );
		Server.WriteCoord( Size.z );
		Server.WriteCoord( velocity.x );
		Server.WriteCoord( velocity.y );
		Server.WriteCoord( velocity.z );
		Server.WriteByte( 10 );
		Server.WriteShort( Shard );
		Server.WriteByte( 0 );
		Server.WriteByte( 25 );
		Server.WriteByte( flag );
		Server.MessageEnd();

		var mins = AbsMin;
		var maxs = AbsMax;

		mins.z = AbsMax.z;
		maxs.z += 8f;

		for ( var i = 1; i < Server.NumEdicts; i++ )
		{
			var edict = Server.GetEdict( i );

			if ( edict is null || (edict.EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround )
				continue;

			if ( mins.x > edict.AbsMax.x || mins.y > edict.AbsMax.y || mins.z > edict.AbsMax.z
				|| maxs.x < edict.AbsMin.x || maxs.y < edict.AbsMin.y || maxs.z < edict.AbsMin.z )
				continue;

			edict.EntityFlags &= ~EntityFlags.OnGround;
			edict.GroundEntity = null;
		}

		TargetName = null;

		Solid = Solid.Not;

		UseTargets( null, UseType.Toggle, 0f );

		SetThink( SubRemove );
		NextThink = (float)(LTime + 0.1);

		if ( SpawnObject is not null )
			Create( SpawnObject, BModelOrigin, Angles, this );

		if ( Explodable )
			ExplosionCreate( Center, Angles, this, ExplosionMagnitude, true );
	}
}

[LinkEntityToClass( "func_pushable" )]
public class Pushable : Breakable
{
	private const int SpawnFlagBreakable = 128;

	private static readonly string[] SoundNames = ["debris/pushbox1.wav", "debris/pushbox2.wav", "debris/pushbox3.wav"];

	private int _lastSound;
	[Save( FieldType.Float )]
	private float _maxSpeed;
	[Save( FieldType.Time )]
	private float _soundTime;

	public override ObjectCaps ObjectCaps => (base.ObjectCaps & ~ObjectCaps.AcrossTransition) | ObjectCaps.ContinuousUse;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "size":
				switch ( Util.Atoi( value ) )
				{
					case 0:
						SetSize( new Vector3( -8f, -8f, -8f ), new Vector3( 8f, 8f, 8f ) );
						break;
					case 2:
						SetSize( new Vector3( -16f, -16f, -18f ) * 2f, new Vector3( 16f, 16f, 18f ) * 2f );
						break;
					case 3:
						SetSize( new Vector3( -16f, -16f, -18f ), new Vector3( 16f, 16f, 18f ) );
						break;
					default:
						SetSize( new Vector3( -16f, -16f, -36f ), new Vector3( 16f, 16f, 36f ) );
						break;
				}

				return true;

			case "buoyancy":
				Skin = (int)Util.Atof( value );
				return true;

			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Precache()
	{
		foreach ( var sound in SoundNames )
			Server.PrecacheSound( sound );

		if ( (SpawnFlags & SpawnFlagBreakable) != 0 )
			base.Precache();
	}

	public override void Spawn()
	{
		if ( (SpawnFlags & SpawnFlagBreakable) != 0 )
			base.Spawn();
		else
			Precache();

		MoveType = MoveType.PushStep;
		Solid = Solid.BBox;
		SetModel( ModelName );

		if ( Friction > 399f )
			Friction = 399f;

		_maxSpeed = 400f - Friction;
		EntityFlags |= EntityFlags.Float;
		Friction = 0f;

		Origin += new Vector3( 0f, 0f, 1f );
		SetOrigin( Origin );

		Skin = (int)(Skin * (Maxs.x - Mins.x) * (Maxs.y - Mins.y) * 0.0005);
		_soundTime = 0f;
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( activator is null || !activator.IsPlayer )
		{
			if ( (SpawnFlags & SpawnFlagBreakable) != 0 )
				BreakableUse();

			return;
		}

		if ( activator.Velocity.x != 0f || activator.Velocity.y != 0f || activator.Velocity.z != 0f )
			Move( activator, false );
	}

	public override void Touch( BaseEntity other )
	{
		if ( other.ClassName == "worldspawn" )
			return;

		Move( other, true );
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		if ( (SpawnFlags & SpawnFlagBreakable) != 0 )
			return BreakableTakeDamage( inflictor, attacker, damage, damageType );

		return true;
	}

	private void Move( BaseEntity other, bool push )
	{
		var playerTouch = false;

		if ( (other.EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround && other.GroundEntity == this )
		{
			if ( WaterLevel > 0 )
				Velocity += new Vector3( 0f, 0f, (float)(other.Velocity.z * 0.1) );

			return;
		}

		if ( other.IsPlayer )
		{
			if ( push && (other.Button & (int)InButtons.Use) != 0 )
				return;

			playerTouch = true;
		}

		float factor;

		if ( playerTouch )
		{
			if ( (other.EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround )
			{
				if ( WaterLevel < 1 )
					return;

				factor = 0.1f;
			}
			else
			{
				factor = 1f;
			}
		}
		else
		{
			factor = 0.25f;
		}

		var fixedTickFudge = Cvar.GetFloat( "sv_pushable_fixed_tick_fudge" );

		if ( !push && fixedTickFudge >= 0f )
			factor *= Server.Globals.FrameTime * fixedTickFudge;

		var velocity = Velocity;

		if ( push || MathF.Abs( velocity.x ) < MathF.Abs( other.Velocity.x - (other.Velocity.x * factor) ) )
			velocity.x += other.Velocity.x * factor;

		if ( push || MathF.Abs( velocity.y ) < MathF.Abs( other.Velocity.y - (other.Velocity.y * factor) ) )
			velocity.y += other.Velocity.y * factor;

		var length = MathF.Sqrt( (velocity.x * velocity.x) + (velocity.y * velocity.y) );

		if ( length > _maxSpeed )
		{
			velocity.x = velocity.x * _maxSpeed / length;
			velocity.y = velocity.y * _maxSpeed / length;
		}

		Velocity = velocity;

		if ( !playerTouch )
			return;

		if ( push )
			other.Velocity = new Vector3( velocity.x, velocity.y, other.Velocity.z );

		if ( Server.Globals.Time - _soundTime <= 0.7 )
			return;

		_soundTime = Server.Globals.Time;

		if ( length > 0f && (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround )
		{
			_lastSound = Server.RandomLong( 0, 2 );
			Server.EmitSound( this, SoundChannel.Weapon, SoundNames[_lastSound], 0.5f, Server.AttenuationNormal );
		}
		else
		{
			Server.StopSound( this, SoundChannel.Weapon, SoundNames[_lastSound] );
		}
	}
}
