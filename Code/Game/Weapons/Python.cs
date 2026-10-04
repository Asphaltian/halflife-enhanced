using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "weapon_python" )]
[LinkEntityToClass( "weapon_357" )]
public class Python : BasePlayerWeapon
{
	public const int WeaponId = 3;
	public const int WeaponWeight = 15;
	public const int MaxClipSize = 6;
	public const int DefaultGive = 6;
	public const int MaxCarry357 = 36;

	private const int AnimIdle1 = 0;
	private const int AnimFidget = 1;
	private const int AnimReload = 3;
	private const int AnimHolster = 4;
	private const int AnimDraw = 5;
	private const int AnimIdle2 = 6;
	private const int AnimIdle3 = 7;

	private bool _inZoom;
	private int _firePython;

	public override int ItemSlot => 2;

	public override bool UseDecrement => true;

	public override bool GetItemInfo( out ItemInfo info )
	{
		Id = WeaponId;

		info = new ItemInfo
		{
			Name = ClassName,
			Ammo1 = "357",
			MaxAmmo1 = MaxCarry357,
			Ammo2 = null,
			MaxAmmo2 = -1,
			MaxClip = MaxClipSize,
			Flags = 0,
			Slot = 1,
			Position = 1,
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

	public override void Spawn()
	{
		ClassName = "weapon_357";

		Precache();

		Id = WeaponId;
		SetModel( "models/w_357.mdl" );

		DefaultAmmo = DefaultGive;

		FallInit();
	}

	public override void Precache()
	{
		PrecacheModel( "models/v_357.mdl" );
		PrecacheModel( "models/w_357.mdl" );
		PrecacheModel( "models/p_357.mdl" );

		PrecacheModel( "models/w_357ammobox.mdl" );
		PrecacheSound( "items/9mmclip1.wav" );

		PrecacheSound( "weapons/357_reload1.wav" );
		PrecacheSound( "weapons/357_cock1.wav" );
		PrecacheSound( "weapons/357_shot1.wav" );
		PrecacheSound( "weapons/357_shot2.wav" );

		_firePython = PrecacheEvent( 1, "events/python.sc" );
	}

	public override bool Deploy()
	{
		Body = IsMultiplayer ? 1 : 0;

		return DefaultDeploy( "models/v_357.mdl", "models/p_357.mdl", AnimDraw, "python", UseDecrement ? 1 : 0, Body );
	}

	public override void Holster( int skipLocal = 0 )
	{
		InReload = false;

		if ( _inZoom )
			SecondaryAttack();

		Player.NextAttack = (float)(WeaponTimeBase + 1.0);
		TimeWeaponIdle = Util.SharedRandomFloat( (uint)Player.RandomSeed, 10f, 15f );

		SendWeaponAnim( AnimHolster );
	}

	private void SetZoom( bool zoom )
	{
		_inZoom = zoom;
		Player.Fov = Player.FieldOfViewSetting = zoom ? 40 : 0;
	}

	public override void SecondaryAttack()
	{
		if ( !IsMultiplayer )
			return;

		if ( Player.Fov != 0f )
			SetZoom( false );
		else if ( Player.Fov != 40f )
			SetZoom( true );

		NextSecondaryAttack = 0.5f;
	}

	public override void PrimaryAttack()
	{
		if ( Player.WaterLevel == 3 )
		{
			PlayEmptySound();
			NextPrimaryAttack = 0.15f;
			return;
		}

		if ( Clip <= 0 )
		{
			if ( !FireOnEmpty )
			{
				Reload();
			}
			else
			{
				PlayEmptySound();
				NextPrimaryAttack = 0.15f;
			}

			return;
		}

		Player.WeaponVolume = LoudGunVolume;
		Player.WeaponFlash = BrightGunFlash;

		Clip--;

		Player.Effects |= Effects.MuzzleFlash;

		Player.SetAnimation( PlayerAnim.Attack1 );

		MakeVectors( Player.ViewAngle + Player.PunchAngle );

		var source = Player.GetGunPosition();
		var aiming = Player.GetAutoaimVector( Player.Autoaim10Degrees );

		var direction = Player.FireBulletsPlayer( 1, source, aiming, Cone1Degrees, 8192f, Bullet.Player357, 0, 0, Player, Player.RandomSeed );

		PlaybackEvent( EventFlags.NotHost, Player, _firePython, 0f, Vector3.Zero, Vector3.Zero, direction.x, direction.y, 0, 0, 0, 0 );

		if ( Clip == 0 && Player.AmmoSlot( PrimaryAmmoType ) <= 0 )
			Player.SetSuitUpdate( "!HEV_AMO0", false, 0 );

		NextPrimaryAttack = 0.75f;
		TimeWeaponIdle = Util.SharedRandomFloat( (uint)Player.RandomSeed, 10f, 15f );
	}

	public override void Reload()
	{
		if ( Player.Ammo357 <= 0 )
			return;

		if ( Player.Fov != 0f )
			SetZoom( false );

		DefaultReload( 6, AnimReload, 2f, IsMultiplayer ? 1 : 0 );
	}

	public override void WeaponIdle()
	{
		ResetEmptySound();

		Player.GetAutoaimVector( Player.Autoaim10Degrees );

		if ( TimeWeaponIdle > WeaponTimeBase )
			return;

		int animation;
		var random = Util.SharedRandomFloat( (uint)Player.RandomSeed, 0f, 1f );

		if ( random <= 0.5 )
		{
			animation = AnimIdle1;
			TimeWeaponIdle = (float)(70.0 / 30.0);
		}
		else if ( random <= 0.7 )
		{
			animation = AnimIdle2;
			TimeWeaponIdle = (float)(60.0 / 30.0);
		}
		else if ( random <= 0.9 )
		{
			animation = AnimIdle3;
			TimeWeaponIdle = (float)(88.0 / 30.0);
		}
		else
		{
			animation = AnimFidget;
			TimeWeaponIdle = (float)(170.0 / 30.0);
		}

		SendWeaponAnim( animation, UseDecrement ? 1 : 0, IsMultiplayer ? 1 : 0 );
	}
}

[LinkEntityToClass( "ammo_357" )]
public class PythonAmmo : BasePlayerAmmo
{
	public override void Spawn()
	{
		Precache();
		SetModel( "models/w_357ammobox.mdl" );
		base.Spawn();
	}

	public override void Precache()
	{
		PrecacheModel( "models/w_357ammobox.mdl" );
		PrecacheSound( "items/9mmclip1.wav" );
	}

	public override bool AddAmmo( BaseEntity other )
	{
		if ( other.GiveAmmo( Python.MaxClipSize, "357", Python.MaxCarry357 ) == -1 )
			return false;

		Sound.Emit( this, SoundChannel.Item, "items/9mmclip1.wav", 1f, Server.AttenuationNormal );

		return true;
	}
}
