using System;
using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "world_items" )]
public class WorldItem : BaseEntity
{
	private int _type;

	public override bool KeyValue( string key, string value )
	{
		if ( key != "type" )
			return base.KeyValue( key, value );

		_type = Util.Atoi( value );
		return true;
	}

	public override void Spawn()
	{
		var entity = _type switch
		{
			44 => Create( "item_battery", Origin, Angles, null ),
			42 => Create( "item_antidote", Origin, Angles, null ),
			43 => Create( "item_security", Origin, Angles, null ),
			45 => Create( "item_suit", Origin, Angles, null ),
			_ => null
		};

		if ( entity is null )
		{
			Util.Alert( AlertType.Console, $"unable to create world_item {_type}" );
		}
		else
		{
			entity.Target = Target;
			entity.TargetName = TargetName;
			entity.SpawnFlags = SpawnFlags;
		}

		Server.FreeEdict( this );
	}
}

public class Item : BaseEntity
{
	public const int Healthkit = 1;
	public const int Antidote = 2;
	public const int Security = 3;
	public const int Battery = 4;

	public override void Spawn()
	{
		MoveType = MoveType.Toss;
		Solid = Solid.Trigger;

		SetOrigin( Origin );
		SetSize( new Vector3( -16f, -16f, 0f ), new Vector3( 16f, 16f, 16f ) );
		SetTouch( ItemTouch );

		if ( Server.DropToFloor( this ) == 0 )
		{
			Util.Alert( AlertType.Error, $"Item {ClassName} fell out of level at {Origin.x:F6},{Origin.y:F6},{Origin.z:F6}" );
			Remove();
		}
	}

	public override BaseEntity Respawn()
	{
		SetTouch( null );
		Effects |= Effects.NoDraw;

		SetOrigin( HalfLifeDll.GameRules.ItemRespawnSpot( this ) );

		SetThink( Materialize );
		NextThink = HalfLifeDll.GameRules.ItemRespawnTime( this );

		return this;
	}

	protected virtual bool MyTouch( Player player ) => false;

	private void ItemTouch( BaseEntity other )
	{
		if ( other is not Player player )
			return;

		if ( !HalfLifeDll.GameRules.CanHaveItem( player, this ) )
			return;

		if ( MyTouch( player ) )
		{
			UseTargets( other, UseType.Toggle, 0f );
			SetTouch( null );

			HalfLifeDll.GameRules.PlayerGotItem( player, this );

			if ( HalfLifeDll.GameRules.ItemShouldRespawn( this ) == RuleCode.ItemRespawnYes )
				Respawn();
			else
				Remove();
		}
		else if ( HalfLifeDll.EvilImpulse101 )
		{
			Remove();
		}
	}

	private void Materialize()
	{
		if ( (Effects & Effects.NoDraw) == Effects.NoDraw )
		{
			Sound.EmitDyn( this, SoundChannel.Weapon, "items/suitchargeok1.wav", 1f, Server.AttenuationNormal, 0, 150 );

			Effects &= ~Effects.NoDraw;
			Effects |= Effects.MuzzleFlash;
		}

		SetTouch( ItemTouch );
	}

	protected void SendItemPickup( Player player )
	{
		Server.MessageBegin( MessageDest.One, UserMessages.ItemPickup, default, player );
		Server.WriteString( ClassName );
		Server.MessageEnd();
	}
}

[LinkEntityToClass( "item_suit" )]
public class ItemSuit : Item
{
	private const int SpawnFlagShortLogon = 0x0001;

	public override void Spawn()
	{
		Precache();
		SetModel( "models/w_suit.mdl" );
		base.Spawn();
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/w_suit.mdl" );
	}

	protected override bool MyTouch( Player player )
	{
		if ( (player.Weapons & (1 << Player.WeaponSuit)) != 0 )
			return false;

		Sound.EmitSuit( player, (SpawnFlags & SpawnFlagShortLogon) != 0 ? "!HEV_A0" : "!HEV_AAx" );

		player.Weapons |= 1 << Player.WeaponSuit;

		return true;
	}
}

[LinkEntityToClass( "item_battery" )]
public class ItemBattery : Item
{
	public const float MaxNormalBattery = 100f;

	public override void Spawn()
	{
		Precache();
		SetModel( "models/w_battery.mdl" );
		base.Spawn();
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/w_battery.mdl" );
		Server.PrecacheSound( "items/gunpickup2.wav" );
	}

	protected override bool MyTouch( Player player )
	{
		if ( player.DeadFlag != DeadNo )
			return false;

		if ( player.ArmorValue >= MaxNormalBattery || (player.Weapons & (1 << Player.WeaponSuit)) == 0 )
			return false;

		player.ArmorValue += HalfLifeDll.SkillData.BatteryCapacity;
		player.ArmorValue = Math.Min( player.ArmorValue, MaxNormalBattery );

		Sound.Emit( player, SoundChannel.Item, "items/gunpickup2.wav", 1f, Server.AttenuationNormal );

		SendItemPickup( player );

		var percent = (int)(((float)(player.ArmorValue * 100.0) * (1.0 / MaxNormalBattery)) + 0.5);

		percent /= 5;

		if ( percent > 0 )
			percent--;

		player.SetSuitUpdate( $"!HEV_{percent}P", false, Player.SuitNextIn30Sec );

		return true;
	}
}

[LinkEntityToClass( "item_antidote" )]
public class ItemAntidote : Item
{
	public override void Spawn()
	{
		Precache();
		SetModel( "models/w_antidote.mdl" );
		base.Spawn();
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/w_antidote.mdl" );
	}

	protected override bool MyTouch( Player player )
	{
		player.SetSuitUpdate( "!HEV_DET4", false, Player.SuitNextIn1Min );
		player.Items[Antidote] += 1;

		return true;
	}
}

[LinkEntityToClass( "item_security" )]
public class ItemSecurity : Item
{
	public override void Spawn()
	{
		Precache();
		SetModel( "models/w_security.mdl" );
		base.Spawn();
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/w_security.mdl" );
	}

	protected override bool MyTouch( Player player )
	{
		player.Items[Security] += 1;

		return true;
	}
}

[LinkEntityToClass( "item_longjump" )]
public class ItemLongJump : Item
{
	public override void Spawn()
	{
		Precache();
		SetModel( "models/w_longjump.mdl" );
		base.Spawn();
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/w_longjump.mdl" );
	}

	protected override bool MyTouch( Player player )
	{
		if ( player.LongJump )
			return false;

		if ( (player.Weapons & (1 << Player.WeaponSuit)) == 0 )
			return false;

		player.LongJump = true;
		Server.SetPhysicsKeyValue( player, "slj", "1" );

		SendItemPickup( player );

		Sound.EmitSuit( player, "!HEV_A1" );

		return true;
	}
}

[LinkEntityToClass( "item_healthkit" )]
public class HealthKit : Item
{
	public override void Spawn()
	{
		Precache();
		SetModel( "models/w_medkit.mdl" );
		base.Spawn();
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/w_medkit.mdl" );
		Server.PrecacheSound( "items/smallmedkit1.wav" );
	}

	protected override bool MyTouch( Player player )
	{
		if ( player.DeadFlag != DeadNo )
			return false;

		if ( !player.TakeHealth( HalfLifeDll.SkillData.HealthkitCapacity, DamageGeneric ) )
			return false;

		SendItemPickup( player );

		Sound.Emit( player, SoundChannel.Item, "items/smallmedkit1.wav", 1f, Server.AttenuationNormal );

		if ( HalfLifeDll.GameRules.ItemShouldRespawn( this ) != RuleCode.None )
			Respawn();
		else
			Remove();

		return true;
	}
}

[LinkEntityToClass( "func_healthcharger" )]
public class WallHealth : BaseToggle
{
	[Save( FieldType.Time )]
	private float _nextCharge;
	[Save( FieldType.Integer )]
	private int _reactivate;
	[Save( FieldType.Integer )]
	private int _juice;
	[Save( FieldType.Integer )]
	private int _on;
	[Save( FieldType.Time )]
	private float _soundTime;

	public override ObjectCaps ObjectCaps => (base.ObjectCaps | ObjectCaps.ContinuousUse) & ~ObjectCaps.AcrossTransition;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "style":
			case "height":
			case "value1":
			case "value2":
			case "value3":
				return true;
			case "dmdelay":
				_reactivate = Util.Atoi( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Spawn()
	{
		Precache();

		Solid = Solid.Bsp;
		MoveType = MoveType.Push;

		SetOrigin( Origin );
		SetSize( Mins, Maxs );
		SetModel( ModelName );

		_juice = (int)HalfLifeDll.SkillData.HealthchargerCapacity;
		Frame = 0f;
	}

	public override void Precache()
	{
		Server.PrecacheSound( "items/medshot4.wav" );
		Server.PrecacheSound( "items/medshotno1.wav" );
		Server.PrecacheSound( "items/medcharge4.wav" );
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( activator is null )
			return;

		if ( !activator.IsPlayer )
			return;

		if ( _juice <= 0 )
		{
			Frame = 1f;
			Off();
		}

		if ( _juice <= 0 || (activator.Weapons & (1 << Player.WeaponSuit)) == 0 )
		{
			if ( _soundTime <= Server.Globals.Time )
			{
				_soundTime = (float)(Server.Globals.Time + 0.62);
				Sound.Emit( this, SoundChannel.Item, "items/medshotno1.wav", 1f, Server.AttenuationNormal );
			}

			return;
		}

		NextThink = (float)(LTime + 0.25);
		SetThink( Off );

		if ( _nextCharge >= Server.Globals.Time )
			return;

		if ( _on == 0 )
		{
			_on++;
			Sound.Emit( this, SoundChannel.Item, "items/medshot4.wav", 1f, Server.AttenuationNormal );
			_soundTime = (float)(0.56 + Server.Globals.Time);
		}

		if ( _on == 1 && _soundTime <= Server.Globals.Time )
		{
			_on++;
			Sound.Emit( this, SoundChannel.Static, "items/medcharge4.wav", 1f, Server.AttenuationNormal );
		}

		if ( activator.TakeHealth( 1f, DamageGeneric ) )
			_juice--;

		_nextCharge = (float)(Server.Globals.Time + 0.1);
	}

	private void Recharge()
	{
		Sound.Emit( this, SoundChannel.Item, "items/medshot4.wav", 1f, Server.AttenuationNormal );

		_juice = (int)HalfLifeDll.SkillData.HealthchargerCapacity;
		Frame = 0f;

		SetThink( SubDoNothing );
	}

	private void Off()
	{
		if ( _on > 1 )
			Sound.Stop( this, SoundChannel.Static, "items/medcharge4.wav" );

		_on = 0;

		if ( _juice == 0 && (_reactivate = (int)HalfLifeDll.GameRules.HealthChargerRechargeTime()) > 0 )
		{
			NextThink = LTime + _reactivate;
			SetThink( Recharge );
		}
		else
		{
			SetThink( SubDoNothing );
		}
	}
}

[LinkEntityToClass( "func_recharge" )]
public class Recharge : BaseToggle
{
	[Save( FieldType.Time )]
	private float _nextCharge;
	[Save( FieldType.Integer )]
	private int _reactivate;
	[Save( FieldType.Integer )]
	private int _juice;
	[Save( FieldType.Integer )]
	private int _on;
	[Save( FieldType.Time )]
	private float _soundTime;

	public override ObjectCaps ObjectCaps => (base.ObjectCaps | ObjectCaps.ContinuousUse) & ~ObjectCaps.AcrossTransition;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "style":
			case "height":
			case "value1":
			case "value2":
			case "value3":
				return true;
			case "dmdelay":
				_reactivate = Util.Atoi( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Spawn()
	{
		Precache();

		Solid = Solid.Bsp;
		MoveType = MoveType.Push;

		SetOrigin( Origin );
		SetSize( Mins, Maxs );
		SetModel( ModelName );

		_juice = (int)HalfLifeDll.SkillData.SuitchargerCapacity;
		Frame = 0f;
	}

	public override void Precache()
	{
		Server.PrecacheSound( "items/suitcharge1.wav" );
		Server.PrecacheSound( "items/suitchargeno1.wav" );
		Server.PrecacheSound( "items/suitchargeok1.wav" );
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( activator.ClassName != "player" )
			return;

		if ( _juice <= 0 )
		{
			Frame = 1f;
			Off();
		}

		if ( _juice <= 0 || (activator.Weapons & (1 << Player.WeaponSuit)) == 0 )
		{
			if ( _soundTime <= Server.Globals.Time )
			{
				_soundTime = (float)(Server.Globals.Time + 0.62);
				Sound.Emit( this, SoundChannel.Item, "items/suitchargeno1.wav", 0.85f, Server.AttenuationNormal );
			}

			return;
		}

		NextThink = (float)(LTime + 0.25);
		SetThink( Off );

		if ( _nextCharge >= Server.Globals.Time )
			return;

		Activator = activator;

		if ( !activator.IsPlayer )
			return;

		if ( _on == 0 )
		{
			_on++;
			Sound.Emit( this, SoundChannel.Item, "items/suitchargeok1.wav", 0.85f, Server.AttenuationNormal );
			_soundTime = (float)(0.56 + Server.Globals.Time);
		}

		if ( _on == 1 && _soundTime <= Server.Globals.Time )
		{
			_on++;
			Sound.Emit( this, SoundChannel.Static, "items/suitcharge1.wav", 0.85f, Server.AttenuationNormal );
		}

		if ( activator.ArmorValue < 100f )
		{
			_juice--;
			activator.ArmorValue += 1f;

			if ( activator.ArmorValue > 100f )
				activator.ArmorValue = 100f;
		}

		_nextCharge = (float)(Server.Globals.Time + 0.1);
	}

	private void Refill()
	{
		_juice = (int)HalfLifeDll.SkillData.SuitchargerCapacity;
		Frame = 0f;
		SetThink( SubDoNothing );
	}

	private void Off()
	{
		if ( _on > 1 )
			Sound.Stop( this, SoundChannel.Static, "items/suitcharge1.wav" );

		_on = 0;

		if ( _juice == 0 && (_reactivate = (int)HalfLifeDll.GameRules.HevChargerRechargeTime()) > 0 )
		{
			NextThink = LTime + _reactivate;
			SetThink( Refill );
		}
		else
		{
			SetThink( SubDoNothing );
		}
	}
}
