using System;
using GoldSrc;

namespace HalfLife;

[Flags]
public enum ItemFlags
{
	SelectOnEmpty = 1,
	NoAutoReload = 2,
	NoAutoSwitchEmpty = 4,
	LimitInWorld = 8,
	Exhaustible = 16,
	NoAutoSwitchTo = 32
}

public struct ItemInfo
{
	public int Slot;
	public int Position;
	public string Ammo1;
	public int MaxAmmo1;
	public string Ammo2;
	public int MaxAmmo2;
	public string Name;
	public int MaxClip;
	public int Id;
	public ItemFlags Flags;
	public int Weight;
}

public struct AmmoInfo
{
	public string Name;
	public int Id;
}

public class BasePlayerItem : BaseAnimating
{
	public const int MaxWeapons = 32;
	public const int MaxAmmoSlots = 32;
	public const int MaxItemTypes = 6;
	public const int NoClip = -1;
	public const int SpawnFlagNoRespawn = 1 << 30;
	public const int WeaponIsOnTarget = 0x40;

	public static ItemInfo[] ItemInfoArray { get; } = new ItemInfo[MaxWeapons];
	public static AmmoInfo[] AmmoInfoArray { get; } = new AmmoInfo[MaxAmmoSlots];

	private static int _ammoIndex;

	[Save( FieldType.ClassPtr )]
	public Player Player;
	[Save( FieldType.ClassPtr )]
	public BasePlayerItem Next;
	[Save( FieldType.Integer )]
	public new int Id;

	public virtual int ItemSlot => 0;

	private ItemInfo Info => ClientSide ? default : ItemInfoArray[Id];

	public int ItemPosition => Info.Position;
	public string Ammo1 => Info.Ammo1;
	public int MaxAmmo1 => Info.MaxAmmo1;
	public string Ammo2 => Info.Ammo2;
	public int MaxAmmo2 => Info.MaxAmmo2;
	public string ItemName => Info.Name;
	public int MaxClip => Info.MaxClip;
	public int Weight => Info.Weight;
	public new ItemFlags Flags => Info.Flags;

	public static int MaxAmmoCarry( string name )
	{
		for ( var i = 0; i < MaxWeapons; i++ )
		{
			if ( ItemInfoArray[i].Ammo1 is not null && name == ItemInfoArray[i].Ammo1 )
				return ItemInfoArray[i].MaxAmmo1;

			if ( ItemInfoArray[i].Ammo2 is not null && name == ItemInfoArray[i].Ammo2 )
				return ItemInfoArray[i].MaxAmmo2;
		}

		Util.Alert( AlertType.Console, $"MaxAmmoCarry() doesn't recognize '{name}'!" );
		return -1;
	}

	public static void AddAmmoNameToAmmoRegistry( string name )
	{
		for ( var i = 0; i < MaxAmmoSlots; i++ )
		{
			if ( AmmoInfoArray[i].Name is null )
				continue;

			if ( AmmoInfoArray[i].Name.Equals( name, StringComparison.OrdinalIgnoreCase ) )
				return;
		}

		_ammoIndex++;

		if ( _ammoIndex >= MaxAmmoSlots )
			_ammoIndex = 0;

		AmmoInfoArray[_ammoIndex].Name = name;
		AmmoInfoArray[_ammoIndex].Id = _ammoIndex;
	}

	public static void PrecacheOtherWeapon( Server server, string className )
	{
		if ( server.CreateNamedEntity( className ) is not BaseEntity entity )
		{
			Util.Alert( AlertType.Console, "NULL Ent in UTIL_PrecacheOtherWeapon" );
			return;
		}

		entity.Precache();

		if ( entity is BasePlayerItem item && item.GetItemInfo( out var info ) )
		{
			ItemInfoArray[info.Id] = info;

			if ( !string.IsNullOrEmpty( info.Ammo1 ) )
				AddAmmoNameToAmmoRegistry( info.Ammo1 );

			if ( !string.IsNullOrEmpty( info.Ammo2 ) )
				AddAmmoNameToAmmoRegistry( info.Ammo2 );
		}

		server.FreeEdict( entity );
	}

	public static void Precache( Server server )
	{
		Array.Clear( ItemInfoArray );
		Array.Clear( AmmoInfoArray );
		_ammoIndex = 0;

		Util.PrecacheOther( "item_suit" );
		Util.PrecacheOther( "item_battery" );
		Util.PrecacheOther( "item_antidote" );
		Util.PrecacheOther( "item_security" );
		Util.PrecacheOther( "item_longjump" );

		PrecacheOtherWeapon( server, "weapon_shotgun" );
		Util.PrecacheOther( "ammo_buckshot" );

		PrecacheOtherWeapon( server, "weapon_crowbar" );

		PrecacheOtherWeapon( server, "weapon_9mmhandgun" );
		Util.PrecacheOther( "ammo_9mmclip" );

		PrecacheOtherWeapon( server, "weapon_9mmAR" );
		Util.PrecacheOther( "ammo_9mmAR" );
		Util.PrecacheOther( "ammo_ARgrenades" );

		PrecacheOtherWeapon( server, "weapon_357" );
		Util.PrecacheOther( "ammo_357" );

		PrecacheOtherWeapon( server, "weapon_gauss" );
		Util.PrecacheOther( "ammo_gaussclip" );

		PrecacheOtherWeapon( server, "weapon_rpg" );
		Util.PrecacheOther( "ammo_rpgclip" );

		PrecacheOtherWeapon( server, "weapon_crossbow" );
		Util.PrecacheOther( "ammo_crossbow" );

		PrecacheOtherWeapon( server, "weapon_egon" );
		PrecacheOtherWeapon( server, "weapon_tripmine" );
		PrecacheOtherWeapon( server, "weapon_satchel" );
		PrecacheOtherWeapon( server, "weapon_handgrenade" );
		PrecacheOtherWeapon( server, "weapon_snark" );
		PrecacheOtherWeapon( server, "weapon_hornetgun" );

		if ( HalfLifeDll.GameRules.IsDeathmatch() )
			Util.PrecacheOther( "weaponbox" );

		GameModels.Fireball = server.PrecacheModel( "sprites/zerogxplode.spr" );
		GameModels.WaterExplosion = server.PrecacheModel( "sprites/WXplo1.spr" );
		GameModels.Smoke = server.PrecacheModel( "sprites/steam1.spr" );
		GameModels.Bubbles = server.PrecacheModel( "sprites/bubble.spr" );
		GameModels.BloodSpray = server.PrecacheModel( "sprites/bloodspray.spr" );
		GameModels.BloodDrop = server.PrecacheModel( "sprites/blood.spr" );
		GameModels.Laser = server.PrecacheModel( GameModels.LaserName );
		GameModels.LaserDot = server.PrecacheModel( "sprites/laserdot.spr" );

		server.PrecacheModel( "models/grenade.mdl" );
		server.PrecacheModel( "sprites/explode1.spr" );

		server.PrecacheSound( "weapons/debris1.wav" );
		server.PrecacheSound( "weapons/debris2.wav" );
		server.PrecacheSound( "weapons/debris3.wav" );

		server.PrecacheSound( "weapons/grenade_hit1.wav" );
		server.PrecacheSound( "weapons/grenade_hit2.wav" );
		server.PrecacheSound( "weapons/grenade_hit3.wav" );

		server.PrecacheSound( "weapons/bullet_hit1.wav" );
		server.PrecacheSound( "weapons/bullet_hit2.wav" );

		server.PrecacheSound( "items/weapondrop1.wav" );
	}

	public override void SetObjectCollisionBox()
	{
		AbsMin = Origin + new Vector3( -24f, -24f, 0f );
		AbsMax = Origin + new Vector3( 24f, 24f, 16f );
	}

	public virtual bool AddToPlayer( Player player )
	{
		Player = player;
		return true;
	}

	public virtual bool AddDuplicate( BasePlayerItem original ) => false;

	public virtual bool GetItemInfo( out ItemInfo info )
	{
		info = default;
		return false;
	}

	public virtual bool CanDeploy() => true;

	public virtual bool Deploy() => true;

	public virtual bool CanHolster() => true;

	public virtual void Holster( int skipLocal = 0 )
	{
		if ( ClientSide )
			return;

		Player.ViewModel = null;
		Player.WeaponModel = null;
	}

	public virtual void UpdateItemInfo()
	{
	}

	public virtual void ItemPreFrame()
	{
	}

	public virtual void ItemPostFrame()
	{
	}

	public virtual void Drop()
	{
		SetTouch( null );
		SetThink( SubRemove );
		NextThink = (float)(Globals.Time + .1);
	}

	public virtual void Kill()
	{
		SetTouch( null );
		SetThink( SubRemove );
		NextThink = (float)(Globals.Time + .1);
	}

	public virtual void AttachToPlayer( Player player )
	{
		MoveType = MoveType.Follow;
		Solid = Solid.Not;
		AimEntity = player;
		Effects = Effects.NoDraw;
		ModelIndex = 0;
		ModelName = null;
		Owner = player;
		NextThink = (float)(Globals.Time + .1);

		SetTouch( null );
	}

	public virtual int PrimaryAmmoIndex => -1;

	public virtual int SecondaryAmmoIndex => -1;

	public virtual bool UpdateClientData( Player player ) => false;

	public virtual BasePlayerWeapon GetWeaponPtr() => null;

	public void FallInit()
	{
		if ( ClientSide )
			return;

		MoveType = MoveType.Toss;
		Solid = Solid.BBox;

		SetOrigin( Origin );
		SetSize( Vector3.Zero, Vector3.Zero );

		SetTouch( DefaultTouch );
		SetThink( FallThink );

		NextThink = (float)(Globals.Time + 0.1);
	}

	public void CheckRespawn()
	{
		if ( HalfLifeDll.GameRules.WeaponShouldRespawn( this ) == RuleCode.WeaponRespawnYes )
			Respawn();
	}

	public override BaseEntity Respawn()
	{
		var newWeapon = Create( ClassName, HalfLifeDll.GameRules.WeaponRespawnSpot( this ), Angles, Owner );

		if ( newWeapon is BasePlayerItem item )
		{
			item.Effects |= Effects.NoDraw;
			item.SetTouch( null );
			item.SetThink( item.AttemptToMaterialize );

			Server.DropToFloor( this );

			item.NextThink = HalfLifeDll.GameRules.WeaponRespawnTime( this );
		}
		else
		{
			Util.Alert( AlertType.Console, $"Respawn failed to create {ClassName}!" );
		}

		return newWeapon;
	}

	public void DestroyItem()
	{
		Player?.RemovePlayerItem( this );

		Kill();
	}

	private void FallThink()
	{
		NextThink = (float)(Globals.Time + 0.1);

		if ( (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround )
		{
			if ( !Util.IsNullEnt( Owner ) )
			{
				var pitch = 95 + Server.RandomLong( 0, 29 );

				Sound.EmitDyn( this, SoundChannel.Voice, "items/weapondrop1.wav", 1f, Server.AttenuationNormal, 0, pitch );
			}

			Angles = new Vector3( 0f, Angles.y, 0f );

			Materialize();
		}
		else if ( Player is not null )
		{
			SetThink( null );
		}

		if ( MultiplayBusters.IsBustingGame() && Util.IsNullEnt( Owner ) && ClassName == "weapon_egon" )
			Remove();
	}

	private void Materialize()
	{
		if ( (Effects & Effects.NoDraw) == Effects.NoDraw )
		{
			Sound.EmitDyn( this, SoundChannel.Weapon, "items/suitchargeok1.wav", 1f, Server.AttenuationNormal, 0, 150 );

			Effects &= ~Effects.NoDraw;
			Effects |= Effects.MuzzleFlash;
		}

		Solid = Solid.Trigger;

		SetOrigin( Origin );
		SetTouch( DefaultTouch );
		SetThink( null );
	}

	private void AttemptToMaterialize()
	{
		var time = HalfLifeDll.GameRules.WeaponTryRespawn( this );

		if ( time == 0f )
		{
			Materialize();
			return;
		}

		NextThink = Globals.Time + time;
	}

	protected void DefaultTouch( BaseEntity other )
	{
		if ( other is not Player player )
			return;

		if ( !HalfLifeDll.GameRules.CanHavePlayerItem( player, this ) )
		{
			if ( HalfLifeDll.EvilImpulse101 )
				Remove();

			return;
		}

		if ( player.AddPlayerItem( this ) )
		{
			AttachToPlayer( player );
			Sound.Emit( player, SoundChannel.Item, "items/gunpickup2.wav", 1f, Server.AttenuationNormal );
		}

		UseTargets( other, UseType.Toggle, 0f );
	}
}

public class BasePlayerWeapon : BasePlayerItem
{
	public const int LoudGunVolume = 1000;
	public const int NormalGunVolume = 600;
	public const int QuietGunVolume = 200;
	public const int BrightGunFlash = 512;
	public const int NormalGunFlash = 256;
	public const int DimGunFlash = 128;
	public const int BigExplosionVolume = 2048;
	public const int NormalExplosionVolume = 1024;
	public const int SmallExplosionVolume = 512;
	public const int WeaponActivityVolume = 64;

	public static readonly Vector3 Cone1Degrees = new( 0.00873f, 0.00873f, 0.00873f );
	public static readonly Vector3 Cone2Degrees = new( 0.01745f, 0.01745f, 0.01745f );
	public static readonly Vector3 Cone3Degrees = new( 0.02618f, 0.02618f, 0.02618f );
	public static readonly Vector3 Cone4Degrees = new( 0.03490f, 0.03490f, 0.03490f );
	public static readonly Vector3 Cone5Degrees = new( 0.04362f, 0.04362f, 0.04362f );
	public static readonly Vector3 Cone6Degrees = new( 0.05234f, 0.05234f, 0.05234f );
	public static readonly Vector3 Cone7Degrees = new( 0.06105f, 0.06105f, 0.06105f );
	public static readonly Vector3 Cone8Degrees = new( 0.06976f, 0.06976f, 0.06976f );
	public static readonly Vector3 Cone9Degrees = new( 0.07846f, 0.07846f, 0.07846f );
	public static readonly Vector3 Cone10Degrees = new( 0.08716f, 0.08716f, 0.08716f );
	public static readonly Vector3 Cone15Degrees = new( 0.13053f, 0.13053f, 0.13053f );
	public static readonly Vector3 Cone20Degrees = new( 0.17365f, 0.17365f, 0.17365f );

	public bool PlayEmptySoundPending;
	public bool FireOnEmpty;
	[Save( FieldType.Time )]
	public float PumpTime;
	[Save( FieldType.Integer )]
	public int InSpecialReload;
	public float StartThrow;
	public float ReleaseThrow;

	public virtual int ChargeReady { get; set; }

	public virtual int InAttack { get; set; }

	public virtual int FireState { get; set; }


	[Save( FieldType.Float )]
	public float NextPrimaryAttack;
	[Save( FieldType.Float )]
	public float NextSecondaryAttack;
	[Save( FieldType.Float )]
	public float TimeWeaponIdle;
	[Save( FieldType.Integer )]
	public int PrimaryAmmoType;
	[Save( FieldType.Integer )]
	public int SecondaryAmmoType;
	[Save( FieldType.Integer )]
	public int Clip;

	public int ClientClip;
	public int ClientWeaponState;
	public bool InReload;

	[Save( FieldType.Integer )]
	public int DefaultAmmo;

	public float PrevPrimaryAttack;
	public float LastFireTime;

	public static float WeaponTimeBase => 0f;

	public override int PrimaryAmmoIndex => ClientSide ? -1 : PrimaryAmmoType;

	public override int SecondaryAmmoIndex => -1;

	public override BasePlayerWeapon GetWeaponPtr() => this;

	public virtual bool UseDecrement => false;

	public virtual bool ShouldWeaponIdle => false;

	public virtual void PrimaryAttack()
	{
	}

	public virtual void SecondaryAttack()
	{
	}

	public virtual void Reload()
	{
	}

	public virtual void WeaponIdle()
	{
	}

	public virtual bool AddWeapon()
	{
		ExtractAmmo( this );
		return true;
	}

	public override bool AddDuplicate( BasePlayerItem original )
	{
		return DefaultAmmo != 0 ? ExtractAmmo( (BasePlayerWeapon)original ) : ExtractClipAmmo( (BasePlayerWeapon)original );
	}

	protected bool ItemAddToPlayer( Player player ) => base.AddToPlayer( player );

	public override bool AddToPlayer( Player player )
	{
		var result = base.AddToPlayer( player );

		player.Weapons |= 1 << Id;

		if ( PrimaryAmmoType == 0 )
		{
			PrimaryAmmoType = Player.GetAmmoIndex( Ammo1 );
			SecondaryAmmoType = Player.GetAmmoIndex( Ammo2 );
		}

		return result && AddWeapon();
	}

	protected void SendWeaponPickup( Player player )
	{
		Server.MessageBegin( MessageDest.One, UserMessages.WeapPickup, default, player );
		Server.WriteByte( Id );
		Server.MessageEnd();
	}

	public override bool UpdateClientData( Player player )
	{
		var send = false;
		var state = 0;

		if ( player.ActiveItem == this )
			state = player.OnTarget ? WeaponIsOnTarget : 1;

		if ( !player.WeaponSent )
			send = true;

		if ( (this == player.ActiveItem || this == player.ClientActiveItem) && player.ActiveItem != player.ClientActiveItem )
			send = true;

		if ( Clip != ClientClip || state != ClientWeaponState || player.FieldOfViewSetting != player.ClientFieldOfView )
			send = true;

		if ( send )
		{
			Server.MessageBegin( MessageDest.One, UserMessages.CurWeapon, default, player );
			Server.WriteByte( state );
			Server.WriteByte( Id );
			Server.WriteByte( Clip );
			Server.MessageEnd();

			ClientClip = Clip;
			ClientWeaponState = state;
			player.WeaponSent = true;
		}

		Next?.UpdateClientData( player );

		return true;
	}

	public virtual void SendWeaponAnim( int animation, int skipLocal = 1, int body = 0 )
	{
		Player.WeaponAnim = animation;

		if ( ClientSide )
		{
			ClientWeapons.SendWeaponAnim( animation, body, false );
			return;
		}

		if ( UseDecrement && Server.CanSkipPlayer( Player ) )
			return;

		Server.MessageBegin( MessageDest.One, Server.WeaponAnimMessage, default, Player );
		Server.WriteByte( animation );
		Server.WriteByte( Body );
		Server.MessageEnd();
	}

	public bool AddPrimaryAmmo( int count, string name, int maxClip, int maxCarry )
	{
		int ammoId;

		if ( maxClip < 1 )
		{
			Clip = -1;
			ammoId = Player.GiveAmmo( count, name, maxCarry );
		}
		else if ( Clip == 0 )
		{
			var added = Math.Min( Clip + count, maxClip ) - Clip;

			Clip += added;
			ammoId = Player.GiveAmmo( count - added, name, maxCarry );
		}
		else
		{
			ammoId = Player.GiveAmmo( count, name, maxCarry );
		}

		if ( ammoId > 0 )
		{
			PrimaryAmmoType = ammoId;

			if ( Player.HasPlayerItem( this ) )
				Sound.Emit( this, SoundChannel.Item, "items/9mmclip1.wav", 1f, Server.AttenuationNormal );
		}

		return ammoId > 0;
	}

	public bool AddSecondaryAmmo( int count, string name, int max )
	{
		var ammoId = Player.GiveAmmo( count, name, max );

		if ( ammoId > 0 )
		{
			SecondaryAmmoType = ammoId;
			Sound.Emit( this, SoundChannel.Item, "items/9mmclip1.wav", 1f, Server.AttenuationNormal );
		}

		return ammoId > 0;
	}

	public virtual bool IsUseable()
	{
		if ( ClientSide )
			return true;

		if ( Clip <= 0 && Player.AmmoSlot( PrimaryAmmoIndex ) <= 0 && MaxAmmo1 != -1 )
			return CanDeploy();

		return true;
	}

	public override bool CanDeploy()
	{
		if ( Ammo1 is null )
			return true;

		var hasAmmo = Player.AmmoSlot( PrimaryAmmoType ) != 0;

		if ( Ammo2 is not null )
			hasAmmo |= Player.AmmoSlot( SecondaryAmmoType ) != 0;

		if ( Clip > 0 )
			hasAmmo = true;

		return hasAmmo;
	}

	public bool DefaultDeploy( string viewModel, string weaponModel, int animation, string animationExtension, int skipLocal = 0, int body = 0 )
	{
		if ( !CanDeploy() )
			return false;

		if ( ClientSide )
		{
			ClientWeapons.LoadViewModel( viewModel, Player );

			SendWeaponAnim( animation, skipLocal, body );

			ClientWeapons.RunningGaussPrediction = false;
			Player.NextAttack = 0.5f;
			TimeWeaponIdle = 1f;

			return true;
		}

		Player.TabulateAmmo();
		Player.ViewModel = viewModel;
		Player.WeaponModel = weaponModel;
		Player.AnimExtension = animationExtension;

		SendWeaponAnim( animation, skipLocal, body );

		Player.NextAttack = (float)(WeaponTimeBase + 0.5);
		TimeWeaponIdle = (float)(WeaponTimeBase + 1.0);
		LastFireTime = 0f;

		return true;
	}

	public bool DefaultReload( int clipSize, int animation, float delay, int body = 0 )
	{
		if ( Player.AmmoSlot( PrimaryAmmoType ) <= 0 )
			return false;

		var count = Math.Min( clipSize - Clip, Player.AmmoSlot( PrimaryAmmoType ) );

		if ( count == 0 )
			return false;

		Player.NextAttack = WeaponTimeBase + delay;

		if ( ClientSide )
			SendWeaponAnim( animation, UseDecrement ? 1 : 0, body );
		else
			SendWeaponAnim( animation, UseDecrement ? 1 : 0 );

		InReload = true;
		TimeWeaponIdle = WeaponTimeBase + 3f;

		return true;
	}

	public virtual bool PlayEmptySound()
	{
		if ( PlayEmptySoundPending )
		{
			if ( ClientSide )
				ClientWeapons.PlaySound( "weapons/357_cock1.wav", 0.8f );
			else
				Sound.Emit( Player, SoundChannel.Weapon, "weapons/357_cock1.wav", 0.8f, Server.AttenuationNormal );

			PlayEmptySoundPending = false;
		}

		return false;
	}

	public virtual void ResetEmptySound()
	{
		PlayEmptySoundPending = true;
	}

	public override void Holster( int skipLocal = 0 )
	{
		InReload = false;

		if ( ClientSide )
		{
			ClientWeapons.RunningGaussPrediction = false;
			Player.ViewModelIndex = 0;
			return;
		}

		Player.ViewModel = null;
		Player.WeaponModel = null;
	}

	public virtual bool ExtractAmmo( BasePlayerWeapon weapon )
	{
		var result = false;

		if ( Ammo1 is not null )
		{
			result = weapon.AddPrimaryAmmo( DefaultAmmo, Ammo1, MaxClip, MaxAmmo1 );
			DefaultAmmo = 0;
		}

		if ( Ammo2 is not null )
			result = weapon.AddSecondaryAmmo( 0, Ammo2, MaxAmmo2 );

		return result;
	}

	public virtual bool ExtractClipAmmo( BasePlayerWeapon weapon )
	{
		var ammo = Clip == NoClip ? 0 : Clip;

		return weapon.Player.GiveAmmo( ammo, Ammo1, MaxAmmo1 ) != 0;
	}

	public virtual void RetireWeapon()
	{
		if ( ClientSide )
			return;

		Player.ViewModel = null;
		Player.WeaponModel = null;

		HalfLifeDll.GameRules.GetNextBestWeapon( Player, this );
	}

	public float GetNextAttackDelay( float delay )
	{
		if ( ClientSide )
			return delay;

		if ( LastFireTime == 0f || NextPrimaryAttack == -1f )
		{
			LastFireTime = Globals.Time;
			PrevPrimaryAttack = delay;
		}

		var timeBetweenFires = Globals.Time - LastFireTime;
		var creep = 0f;

		if ( timeBetweenFires > 0f )
			creep = timeBetweenFires - PrevPrimaryAttack;

		LastFireTime = Globals.Time;

		var nextAttack = WeaponTimeBase + delay - creep;

		PrevPrimaryAttack = nextAttack - WeaponTimeBase;

		return nextAttack;
	}

	private void ClientItemPostFrame()
	{
		if ( InReload && Player.NextAttack <= 0.0 )
		{
			Clip += 10;
			InReload = false;
		}

		if ( (Player.Button & (int)InButtons.Attack2) != 0 && NextSecondaryAttack <= 0.0 )
		{
			if ( Ammo2 is not null && Player.AmmoSlot( SecondaryAmmoIndex ) == 0 )
				FireOnEmpty = true;

			SecondaryAttack();
			Player.Button &= ~(int)InButtons.Attack2;
		}
		else if ( (Player.Button & (int)InButtons.Attack) != 0 && NextPrimaryAttack <= 0.0 )
		{
			if ( (Clip == 0 && Ammo1 is not null) || (MaxClip == -1 && Player.AmmoSlot( PrimaryAmmoIndex ) == 0) )
				FireOnEmpty = true;

			PrimaryAttack();
		}
		else if ( (Player.Button & (int)InButtons.Reload) != 0 && MaxClip != NoClip && !InReload )
		{
			Reload();
		}
		else if ( (Player.Button & (int)(InButtons.Attack | InButtons.Attack2)) == 0 )
		{
			FireOnEmpty = false;

			if ( Clip == 0 && (Flags & ItemFlags.NoAutoReload) != ItemFlags.NoAutoReload && NextPrimaryAttack < 0.0 )
			{
				Reload();
				return;
			}

			WeaponIdle();
			return;
		}

		if ( ShouldWeaponIdle )
			WeaponIdle();
	}

	public override void ItemPostFrame()
	{
		if ( ClientSide )
		{
			ClientItemPostFrame();
			return;
		}

		if ( InReload && Player.NextAttack <= WeaponTimeBase )
		{
			var count = Math.Min( MaxClip - Clip, Player.AmmoSlot( PrimaryAmmoType ) );

			Clip += count;
			Player.SetAmmoSlot( PrimaryAmmoType, Player.AmmoSlot( PrimaryAmmoType ) - count );

			Player.TabulateAmmo();

			InReload = false;
		}

		if ( (Player.Button & (int)InButtons.Attack) == 0 )
			LastFireTime = 0f;

		if ( (Player.Button & (int)InButtons.Attack2) != 0 && CanAttack( NextSecondaryAttack, Globals.Time, UseDecrement ) )
		{
			if ( Ammo2 is not null && Player.AmmoSlot( SecondaryAmmoIndex ) == 0 )
				FireOnEmpty = true;

			Player.TabulateAmmo();
			SecondaryAttack();
			Player.Button &= ~(int)InButtons.Attack2;
		}
		else if ( (Player.Button & (int)InButtons.Attack) != 0 && CanAttack( NextPrimaryAttack, Globals.Time, UseDecrement ) )
		{
			if ( (Clip == 0 && Ammo1 is not null) || (MaxClip == -1 && Player.AmmoSlot( PrimaryAmmoIndex ) == 0) )
				FireOnEmpty = true;

			Player.TabulateAmmo();
			PrimaryAttack();
		}
		else if ( (Player.Button & (int)InButtons.Reload) != 0 && MaxClip != NoClip && !InReload )
		{
			Reload();
		}
		else if ( (Player.Button & (int)(InButtons.Attack | InButtons.Attack2)) == 0 )
		{
			FireOnEmpty = false;

			var now = UseDecrement ? 0f : Globals.Time;

			if ( !IsUseable() && NextPrimaryAttack < now )
			{
				if ( (Flags & ItemFlags.NoAutoSwitchEmpty) != ItemFlags.NoAutoSwitchEmpty && HalfLifeDll.GameRules.GetNextBestWeapon( Player, this ) )
				{
					NextPrimaryAttack = (float)(now + 0.3);
					return;
				}
			}
			else if ( Clip == 0 && (Flags & ItemFlags.NoAutoReload) != ItemFlags.NoAutoReload && NextPrimaryAttack < now )
			{
				Reload();
				return;
			}

			WeaponIdle();
			return;
		}

		if ( ShouldWeaponIdle )
			WeaponIdle();
	}

	private static bool CanAttack( float attackTime, float currentTime, bool predicted )
	{
		return predicted ? attackTime <= 0.0 : attackTime <= currentTime;
	}
}

public class BasePlayerAmmo : BaseEntity
{
	public override void Spawn()
	{
		MoveType = MoveType.Toss;
		Solid = Solid.Trigger;

		SetSize( new Vector3( -16f, -16f, 0f ), new Vector3( 16f, 16f, 16f ) );
		SetOrigin( Origin );
		SetTouch( DefaultTouch );
	}

	public virtual bool AddAmmo( BaseEntity other ) => true;

	public override BaseEntity Respawn()
	{
		Effects |= Effects.NoDraw;
		SetTouch( null );

		SetOrigin( HalfLifeDll.GameRules.AmmoRespawnSpot( this ) );

		SetThink( Materialize );
		NextThink = HalfLifeDll.GameRules.AmmoRespawnTime( this );

		return this;
	}

	private void Materialize()
	{
		if ( (Effects & Effects.NoDraw) == Effects.NoDraw )
		{
			Sound.EmitDyn( this, SoundChannel.Weapon, "items/suitchargeok1.wav", 1f, Server.AttenuationNormal, 0, 150 );

			Effects &= ~Effects.NoDraw;
			Effects |= Effects.MuzzleFlash;
		}

		SetTouch( DefaultTouch );
	}

	private void DefaultTouch( BaseEntity other )
	{
		if ( !other.IsPlayer )
			return;

		if ( MultiplayBusters.IsPlayerBusting( other ) )
			return;

		if ( AddAmmo( other ) )
		{
			if ( HalfLifeDll.GameRules.AmmoShouldRespawn( this ) == RuleCode.AmmoRespawnYes )
			{
				Respawn();
			}
			else
			{
				SetTouch( null );
				SetThink( SubRemove );
				NextThink = (float)(Globals.Time + .1);
			}
		}
		else if ( HalfLifeDll.EvilImpulse101 )
		{
			SetTouch( null );
			SetThink( SubRemove );
			NextThink = (float)(Globals.Time + .1);
		}
	}
}

[LinkEntityToClass( "weaponbox" )]
public class WeaponBox : BaseEntity
{
	[Save( FieldType.Integer )]
	private int[] _ammo = new int[BasePlayerItem.MaxAmmoSlots];
	[Save( FieldType.String )]
	private string[] _ammoNames = new string[BasePlayerItem.MaxAmmoSlots];
	[Save( FieldType.ClassPtr )]
	private BasePlayerItem[] _playerItems = new BasePlayerItem[BasePlayerItem.MaxItemTypes];
	[Save( FieldType.Integer )]
	private int _ammoTypes;

	public BasePlayerItem[] PlayerItems => _playerItems;

	public override void Precache()
	{
		PrecacheModel( "models/w_weaponbox.mdl" );
	}

	public override bool KeyValue( string key, string value )
	{
		if ( _ammoTypes >= BasePlayerItem.MaxAmmoSlots )
		{
			Util.Alert( AlertType.Console, $"WeaponBox too full! only {BasePlayerItem.MaxAmmoSlots} ammotypes allowed" );
			return false;
		}

		PackAmmo( Server.AllocString( key ), Util.Atoi( value ) );
		_ammoTypes++;

		return true;
	}

	public override void Spawn()
	{
		Precache();

		MoveType = MoveType.Toss;
		Solid = Solid.Trigger;

		SetSize( Vector3.Zero, Vector3.Zero );
		SetModel( "models/w_weaponbox.mdl" );
	}

	public void Kill()
	{
		for ( var i = 0; i < BasePlayerItem.MaxItemTypes; i++ )
		{
			for ( var weapon = _playerItems[i]; weapon is not null; weapon = weapon.Next )
			{
				weapon.SetThink( weapon.SubRemove );
				weapon.NextThink = (float)(Globals.Time + 0.1);
			}
		}

		Remove();
	}

	public override void Touch( BaseEntity other )
	{
		if ( (EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround )
			return;

		if ( other is not Player player )
			return;

		if ( !other.IsAlive )
			return;

		for ( var i = 0; i < BasePlayerItem.MaxAmmoSlots; i++ )
		{
			if ( string.IsNullOrEmpty( _ammoNames[i] ) )
				continue;

			player.GiveAmmo( _ammo[i], _ammoNames[i], BasePlayerItem.MaxAmmoCarry( _ammoNames[i] ) );

			_ammoNames[i] = null;
			_ammo[i] = 0;
		}

		for ( var i = 0; i < BasePlayerItem.MaxItemTypes; i++ )
		{
			while ( _playerItems[i] is { } item )
			{
				_playerItems[i] = item.Next;

				if ( player.AddPlayerItem( item ) )
					item.AttachToPlayer( player );
			}
		}

		Sound.Emit( other, SoundChannel.Item, "items/gunpickup2.wav", 1f, Server.AttenuationNormal );

		SetTouch( null );
		Remove();
	}

	public bool PackWeapon( BasePlayerItem weapon )
	{
		if ( HasWeapon( weapon ) )
			return false;

		if ( weapon.Player is not null && !weapon.Player.RemovePlayerItem( weapon ) )
			return false;

		var slot = weapon.ItemSlot;

		weapon.Next = _playerItems[slot];
		_playerItems[slot] = weapon;

		weapon.SpawnFlags |= BasePlayerItem.SpawnFlagNoRespawn;
		weapon.MoveType = MoveType.None;
		weapon.Solid = Solid.Not;
		weapon.Effects = Effects.NoDraw;
		weapon.ModelIndex = 0;
		weapon.ModelName = null;
		weapon.Owner = this;
		weapon.SetThink( null );
		weapon.SetTouch( null );
		weapon.Player = null;

		return true;
	}

	public bool PackAmmo( string name, int count )
	{
		if ( string.IsNullOrEmpty( name ) )
		{
			Util.Alert( AlertType.Console, "NULL String in PackAmmo!" );
			return false;
		}

		var maxCarry = BasePlayerItem.MaxAmmoCarry( name );

		if ( maxCarry != -1 && count > 0 )
		{
			GiveAmmo( count, name, maxCarry );
			return true;
		}

		return false;
	}

	public override int GiveAmmo( int count, string name, int max )
	{
		int i;

		for ( i = 1; i < BasePlayerItem.MaxAmmoSlots && !string.IsNullOrEmpty( _ammoNames[i] ); i++ )
		{
			if ( !name.Equals( _ammoNames[i], StringComparison.OrdinalIgnoreCase ) )
				continue;

			var add = Math.Min( count, max - _ammo[i] );

			if ( count == 0 || add > 0 )
			{
				_ammo[i] += add;
				return i;
			}

			return -1;
		}

		if ( i < BasePlayerItem.MaxAmmoSlots )
		{
			_ammoNames[i] = name;
			_ammo[i] = count;
			return i;
		}

		Util.Alert( AlertType.Console, "out of named ammo slots" );
		return i;
	}

	public bool HasWeapon( BasePlayerItem checkItem )
	{
		for ( var item = _playerItems[checkItem.ItemSlot]; item is not null; item = item.Next )
		{
			if ( item.ClassName == checkItem.ClassName )
				return true;
		}

		return false;
	}

	public bool IsEmpty
	{
		get
		{
			for ( var i = 0; i < BasePlayerItem.MaxItemTypes; i++ )
			{
				if ( _playerItems[i] is not null )
					return false;
			}

			for ( var i = 0; i < BasePlayerItem.MaxAmmoSlots; i++ )
			{
				if ( !string.IsNullOrEmpty( _ammoNames[i] ) )
					return false;
			}

			return true;
		}
	}

	public override void SetObjectCollisionBox()
	{
		AbsMin = Origin + new Vector3( -16f, -16f, 0f );
		AbsMax = Origin + new Vector3( 16f, 16f, 16f );
	}
}
