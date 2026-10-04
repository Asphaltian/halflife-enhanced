using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "weapon_mp5" )]
[LinkEntityToClass( "weapon_9mmAR" )]
public class Mp5 : BasePlayerWeapon
{
	public const int WeaponId = 4;
	public const int WeaponWeight = 15;
	public const int MaxClipSize = 50;
	public const int DefaultGive = 25;
	public const int MaxCarryM203Grenade = 10;

	private const int AnimLongIdle = 0;
	private const int AnimIdle1 = 1;
	private const int AnimReload = 3;
	private const int AnimDeploy = 4;

	private int _shell;
	private int _fireMp5;
	private int _fireMp52;

	public override int ItemSlot => 3;

	public override bool UseDecrement => true;

	public override int SecondaryAmmoIndex => SecondaryAmmoType;

	public override void Spawn()
	{
		ClassName = "weapon_9mmAR";

		Precache();

		SetModel( "models/w_9mmAR.mdl" );
		Id = WeaponId;

		DefaultAmmo = MaxClients > 1 ? MaxClipSize : DefaultGive;

		FallInit();
	}

	public override void Precache()
	{
		PrecacheModel( "models/v_9mmAR.mdl" );
		PrecacheModel( "models/w_9mmAR.mdl" );
		PrecacheModel( "models/p_9mmAR.mdl" );

		_shell = PrecacheModel( "models/shell.mdl" );

		PrecacheModel( "models/grenade.mdl" );

		PrecacheModel( "models/w_9mmARclip.mdl" );
		PrecacheSound( "items/9mmclip1.wav" );

		PrecacheSound( "items/clipinsert1.wav" );
		PrecacheSound( "items/cliprelease1.wav" );

		PrecacheSound( "weapons/hks1.wav" );
		PrecacheSound( "weapons/hks2.wav" );
		PrecacheSound( "weapons/hks3.wav" );

		PrecacheSound( "weapons/glauncher.wav" );
		PrecacheSound( "weapons/glauncher2.wav" );

		PrecacheSound( "weapons/357_cock1.wav" );

		_fireMp5 = PrecacheEvent( 1, "events/mp5.sc" );
		_fireMp52 = PrecacheEvent( 1, "events/mp52.sc" );
	}

	public override bool GetItemInfo( out ItemInfo info )
	{
		Id = WeaponId;

		info = new ItemInfo
		{
			Name = ClassName,
			Ammo1 = "9mm",
			MaxAmmo1 = Glock.MaxCarry9mm,
			Ammo2 = "ARgrenades",
			MaxAmmo2 = MaxCarryM203Grenade,
			MaxClip = MaxClipSize,
			Slot = 2,
			Position = 0,
			Flags = 0,
			Id = WeaponId,
			Weight = WeaponWeight
		};

		return true;
	}

	public override bool AddToPlayer( Player player )
	{
		if ( !base.AddToPlayer( player ) )
			return false;

		SendWeaponPickup( player );

		return true;
	}

	public override bool Deploy()
	{
		return DefaultDeploy( "models/v_9mmAR.mdl", "models/p_9mmAR.mdl", AnimDeploy, "mp5", 0 );
	}

	public override void PrimaryAttack()
	{
		if ( Player.WaterLevel == 3 || Clip <= 0 )
		{
			PlayEmptySound();
			NextPrimaryAttack = 0.15f;
			return;
		}

		Player.WeaponVolume = NormalGunVolume;
		Player.WeaponFlash = NormalGunFlash;

		Clip--;

		Player.Effects |= Effects.MuzzleFlash;

		Player.SetAnimation( PlayerAnim.Attack1 );

		var source = Player.GetGunPosition();
		var aiming = Player.GetAutoaimVector( Player.Autoaim5Degrees );
		var spread = IsMultiplayer ? Cone6Degrees : Cone3Degrees;

		var direction = Player.FireBulletsPlayer( 1, source, aiming, spread, 8192f, Bullet.PlayerMp5, 2, 0, Player, Player.RandomSeed );

		PlaybackEvent( EventFlags.NotHost, Player, _fireMp5, 0f, Vector3.Zero, Vector3.Zero, direction.x, direction.y, 0, 0, 0, 0 );

		if ( Clip == 0 && Player.AmmoSlot( PrimaryAmmoType ) <= 0 )
			Player.SetSuitUpdate( "!HEV_AMO0", false, 0 );

		NextPrimaryAttack = GetNextAttackDelay( 0.1f );

		if ( NextPrimaryAttack < WeaponTimeBase )
			NextPrimaryAttack = (float)(WeaponTimeBase + 0.1);

		TimeWeaponIdle = WeaponTimeBase + Util.SharedRandomFloat( (uint)Player.RandomSeed, 10f, 15f );
	}

	public override void SecondaryAttack()
	{
		if ( Player.WaterLevel == 3 )
		{
			PlayEmptySound();
			NextPrimaryAttack = 0.15f;
			return;
		}

		if ( Player.AmmoSlot( SecondaryAmmoType ) == 0 )
		{
			PlayEmptySound();
			return;
		}

		Player.WeaponVolume = NormalGunVolume;
		Player.WeaponFlash = BrightGunFlash;

		Player.ExtraSoundTypes = WorldSound.Danger;
		Player.StopExtraSoundTime = (float)(WeaponTimeBase + 0.2);

		Player.SetAmmoSlot( SecondaryAmmoType, Player.AmmoSlot( SecondaryAmmoType ) - 1 );

		Player.SetAnimation( PlayerAnim.Attack1 );

		MakeVectors( Player.ViewAngle + Player.PunchAngle );

		Grenade.ShootContact( Player, Player.Origin + Player.ViewOffset + (Globals.Forward * 16f), Globals.Forward * 800f );

		PlaybackEvent( EventFlags.NotHost, Player, _fireMp52, 0f, Vector3.Zero, Vector3.Zero, 0f, 0f, 0, 0, 0, 0 );

		NextPrimaryAttack = GetNextAttackDelay( 1f );
		NextSecondaryAttack = WeaponTimeBase + 1f;
		TimeWeaponIdle = WeaponTimeBase + 5f;

		if ( Player.AmmoSlot( SecondaryAmmoType ) == 0 )
			Player.SetSuitUpdate( "!HEV_AMO0", false, 0 );
	}

	public override void Reload()
	{
		if ( Player.Ammo9mm <= 0 )
			return;

		DefaultReload( MaxClipSize, AnimReload, 1.5f );
	}

	public override void WeaponIdle()
	{
		ResetEmptySound();

		Player.GetAutoaimVector( Player.Autoaim5Degrees );

		if ( TimeWeaponIdle > WeaponTimeBase )
			return;

		SendWeaponAnim( Server.RandomLong( 0, 1 ) == 0 ? AnimLongIdle : AnimIdle1 );

		TimeWeaponIdle = Util.SharedRandomFloat( (uint)Player.RandomSeed, 10f, 15f );
	}
}

[LinkEntityToClass( "ammo_mp5clip" )]
[LinkEntityToClass( "ammo_9mmAR" )]
public class Mp5AmmoClip : BasePlayerAmmo
{
	public override void Spawn()
	{
		Precache();
		SetModel( "models/w_9mmARclip.mdl" );
		base.Spawn();
	}

	public override void Precache()
	{
		PrecacheModel( "models/w_9mmARclip.mdl" );
		PrecacheSound( "items/9mmclip1.wav" );
	}

	public override bool AddAmmo( BaseEntity other )
	{
		if ( other.GiveAmmo( Mp5.MaxClipSize, "9mm", Glock.MaxCarry9mm ) == -1 )
			return false;

		Sound.Emit( this, SoundChannel.Item, "items/9mmclip1.wav", 1f, Server.AttenuationNormal );

		return true;
	}
}

[LinkEntityToClass( "ammo_9mmbox" )]
public class Mp5ChainAmmo : BasePlayerAmmo
{
	private const int BoxGive = 200;

	public override void Spawn()
	{
		Precache();
		SetModel( "models/w_chainammo.mdl" );
		base.Spawn();
	}

	public override void Precache()
	{
		PrecacheModel( "models/w_chainammo.mdl" );
		PrecacheSound( "items/9mmclip1.wav" );
	}

	public override bool AddAmmo( BaseEntity other )
	{
		if ( other.GiveAmmo( BoxGive, "9mm", Glock.MaxCarry9mm ) == -1 )
			return false;

		Sound.Emit( this, SoundChannel.Item, "items/9mmclip1.wav", 1f, Server.AttenuationNormal );

		return true;
	}
}

[LinkEntityToClass( "ammo_mp5grenades" )]
[LinkEntityToClass( "ammo_ARgrenades" )]
public class Mp5AmmoGrenade : BasePlayerAmmo
{
	private const int BoxGive = 2;

	public override void Spawn()
	{
		Precache();
		SetModel( "models/w_ARgrenade.mdl" );
		base.Spawn();
	}

	public override void Precache()
	{
		PrecacheModel( "models/w_ARgrenade.mdl" );
		PrecacheSound( "items/9mmclip1.wav" );
	}

	public override bool AddAmmo( BaseEntity other )
	{
		if ( other.GiveAmmo( BoxGive, "ARgrenades", Mp5.MaxCarryM203Grenade ) == -1 )
			return false;

		Sound.Emit( this, SoundChannel.Item, "items/9mmclip1.wav", 1f, Server.AttenuationNormal );

		return true;
	}
}
