using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "weapon_glock" )]
[LinkEntityToClass( "weapon_9mmhandgun" )]
public class Glock : BasePlayerWeapon
{
	public const int WeaponId = 2;
	public const int WeaponWeight = 10;
	public const int MaxClipSize = 17;
	public const int DefaultGive = 17;
	public const int MaxCarry9mm = 250;

	private const int AnimIdle1 = 0;
	private const int AnimIdle2 = 1;
	private const int AnimIdle3 = 2;
	private const int AnimShoot = 3;
	private const int AnimShootEmpty = 4;
	private const int AnimReload = 5;
	private const int AnimReloadNotEmpty = 6;
	private const int AnimDraw = 7;
	private const int AnimHolster = 8;
	private const int AnimAddSilencer = 9;

	private int _shell;
	private int _fireGlock1;
	private int _fireGlock2;

	public override int ItemSlot => 2;

	public override bool UseDecrement => true;

	public override void Spawn()
	{
		ClassName = "weapon_9mmhandgun";

		Precache();

		Id = WeaponId;
		SetModel( "models/w_9mmhandgun.mdl" );

		DefaultAmmo = DefaultGive;

		FallInit();
	}

	public override void Precache()
	{
		PrecacheModel( "models/v_9mmhandgun.mdl" );
		PrecacheModel( "models/w_9mmhandgun.mdl" );
		PrecacheModel( "models/p_9mmhandgun.mdl" );

		_shell = PrecacheModel( "models/shell.mdl" );

		PrecacheSound( "items/9mmclip1.wav" );
		PrecacheSound( "items/9mmclip2.wav" );

		PrecacheSound( "weapons/pl_gun1.wav" );
		PrecacheSound( "weapons/pl_gun2.wav" );
		PrecacheSound( "weapons/pl_gun3.wav" );

		_fireGlock1 = PrecacheEvent( 1, "events/glock1.sc" );
		_fireGlock2 = PrecacheEvent( 1, "events/glock2.sc" );
	}

	public override bool GetItemInfo( out ItemInfo info )
	{
		Id = WeaponId;

		info = new ItemInfo
		{
			Name = ClassName,
			Ammo1 = "9mm",
			MaxAmmo1 = MaxCarry9mm,
			Ammo2 = null,
			MaxAmmo2 = -1,
			MaxClip = MaxClipSize,
			Slot = 1,
			Position = 0,
			Flags = 0,
			Id = WeaponId,
			Weight = WeaponWeight
		};

		return true;
	}

	public override bool Deploy()
	{
		return DefaultDeploy( "models/v_9mmhandgun.mdl", "models/p_9mmhandgun.mdl", AnimDraw, "onehanded", 0 );
	}

	public override void SecondaryAttack()
	{
		GlockFire( 0.1f, 0.2f, false );
	}

	public override void PrimaryAttack()
	{
		GlockFire( 0.01f, 0.3f, true );
	}

	private void GlockFire( float spread, float cycleTime, bool useAutoAim )
	{
		if ( Clip <= 0 )
		{
			if ( FireOnEmpty )
			{
				PlayEmptySound();
				NextPrimaryAttack = GetNextAttackDelay( 0.2f );
			}

			return;
		}

		Clip--;

		Player.Effects |= Effects.MuzzleFlash;

		Player.SetAnimation( PlayerAnim.Attack1 );

		if ( Body == 1 )
		{
			Player.WeaponVolume = QuietGunVolume;
			Player.WeaponFlash = DimGunFlash;
		}
		else
		{
			Player.WeaponVolume = NormalGunVolume;
			Player.WeaponFlash = NormalGunFlash;
		}

		var source = Player.GetGunPosition();
		var aiming = useAutoAim ? Player.GetAutoaimVector( Player.Autoaim10Degrees ) : Globals.Forward;

		var direction = Player.FireBulletsPlayer( 1, source, aiming, new Vector3( spread, spread, spread ), 8192f, Bullet.Player9mm, 0, 0, Player, Player.RandomSeed );

		PlaybackEvent( EventFlags.NotHost, Player, useAutoAim ? _fireGlock1 : _fireGlock2, 0f, Vector3.Zero, Vector3.Zero, direction.x, direction.y, 0, 0, Clip == 0 ? 1 : 0, 0 );

		NextPrimaryAttack = NextSecondaryAttack = GetNextAttackDelay( cycleTime );

		if ( Clip == 0 && Player.AmmoSlot( PrimaryAmmoType ) <= 0 )
			Player.SetSuitUpdate( "!HEV_AMO0", false, 0 );

		TimeWeaponIdle = WeaponTimeBase + Util.SharedRandomFloat( (uint)Player.RandomSeed, 10f, 15f );
	}

	public override void Reload()
	{
		if ( Player.Ammo9mm <= 0 )
			return;

		if ( DefaultReload( MaxClipSize, Clip > 0 ? AnimReloadNotEmpty : AnimReload, 1.5f ) )
			TimeWeaponIdle = WeaponTimeBase + Util.SharedRandomFloat( (uint)Player.RandomSeed, 10f, 15f );
	}

	public override void WeaponIdle()
	{
		ResetEmptySound();

		Player.GetAutoaimVector( Player.Autoaim10Degrees );

		if ( TimeWeaponIdle > WeaponTimeBase )
			return;

		if ( Clip == 0 )
			return;

		int animation;
		var random = Util.SharedRandomFloat( (uint)Player.RandomSeed, 0f, 1f );

		if ( random <= 0.3 )
		{
			animation = AnimIdle3;
			TimeWeaponIdle = (float)(WeaponTimeBase + (49.0 / 16));
		}
		else if ( random <= 0.6 )
		{
			animation = AnimIdle1;
			TimeWeaponIdle = (float)(WeaponTimeBase + (60.0 / 16.0));
		}
		else
		{
			animation = AnimIdle2;
			TimeWeaponIdle = (float)(WeaponTimeBase + (40.0 / 16.0));
		}

		SendWeaponAnim( animation, 1 );
	}
}

[LinkEntityToClass( "ammo_glockclip" )]
[LinkEntityToClass( "ammo_9mmclip" )]
public class GlockAmmo : BasePlayerAmmo
{
	private const int ClipGive = Glock.MaxClipSize;

	public override void Spawn()
	{
		Precache();
		SetModel( "models/w_9mmclip.mdl" );
		base.Spawn();
	}

	public override void Precache()
	{
		PrecacheModel( "models/w_9mmclip.mdl" );
		PrecacheSound( "items/9mmclip1.wav" );
	}

	public override bool AddAmmo( BaseEntity other )
	{
		if ( other.GiveAmmo( ClipGive, "9mm", Glock.MaxCarry9mm ) == -1 )
			return false;

		Sound.Emit( this, SoundChannel.Item, "items/9mmclip1.wav", 1f, Server.AttenuationNormal );

		return true;
	}
}
