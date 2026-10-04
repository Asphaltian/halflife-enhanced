using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "weapon_shotgun" )]
public class Shotgun : BasePlayerWeapon
{
	public const int WeaponId = 7;
	public const int WeaponWeight = 15;
	public const int MaxClipSize = 8;
	public const int DefaultGive = 12;
	public const int MaxCarryBuckshot = 125;

	private const int AnimIdle = 0;
	private const int AnimReload = 3;
	private const int AnimPump = 4;
	private const int AnimStartReload = 5;
	private const int AnimDraw = 6;
	private const int AnimIdle4 = 8;
	private const int AnimIdleDeep = 9;

	private static readonly Vector3 ConeDmShotgun = new( 0.08716f, 0.04362f, 0.00f );
	private static readonly Vector3 ConeDmDoubleShotgun = new( 0.17365f, 0.04362f, 0.00f );

	[Save( FieldType.Time )]
	private float _nextReload;

	private int _shell;
	private int _singleFire;
	private int _doubleFire;

	public override int ItemSlot => 3;

	public override bool UseDecrement => true;

	public override void Spawn()
	{
		Precache();

		Id = WeaponId;
		SetModel( "models/w_shotgun.mdl" );

		DefaultAmmo = DefaultGive;

		FallInit();
	}

	public override void Precache()
	{
		PrecacheModel( "models/v_shotgun.mdl" );
		PrecacheModel( "models/w_shotgun.mdl" );
		PrecacheModel( "models/p_shotgun.mdl" );

		_shell = PrecacheModel( "models/shotgunshell.mdl" );

		PrecacheSound( "items/9mmclip1.wav" );

		PrecacheSound( "weapons/dbarrel1.wav" );
		PrecacheSound( "weapons/sbarrel1.wav" );

		PrecacheSound( "weapons/reload1.wav" );
		PrecacheSound( "weapons/reload3.wav" );

		PrecacheSound( "weapons/357_cock1.wav" );
		PrecacheSound( "weapons/scock1.wav" );

		_singleFire = PrecacheEvent( 1, "events/shotgun1.sc" );
		_doubleFire = PrecacheEvent( 1, "events/shotgun2.sc" );
	}

	public override bool AddToPlayer( Player player )
	{
		if ( !base.AddToPlayer( player ) )
			return false;

		SendWeaponPickup( player );

		return true;
	}

	public override bool GetItemInfo( out ItemInfo info )
	{
		Id = WeaponId;

		info = new ItemInfo
		{
			Name = ClassName,
			Ammo1 = "buckshot",
			MaxAmmo1 = MaxCarryBuckshot,
			Ammo2 = null,
			MaxAmmo2 = -1,
			MaxClip = MaxClipSize,
			Slot = 2,
			Position = 1,
			Flags = 0,
			Id = WeaponId,
			Weight = WeaponWeight
		};

		return true;
	}

	public override bool Deploy()
	{
		return DefaultDeploy( "models/v_shotgun.mdl", "models/p_shotgun.mdl", AnimDraw, "shotgun", 0 );
	}

	public override void PrimaryAttack()
	{
		if ( Player.WaterLevel == 3 )
		{
			PlayEmptySound();
			NextPrimaryAttack = GetNextAttackDelay( 0.15f );
			return;
		}

		if ( Clip <= 0 )
		{
			Reload();

			if ( Clip == 0 )
				PlayEmptySound();

			return;
		}

		Player.WeaponVolume = LoudGunVolume;
		Player.WeaponFlash = NormalGunFlash;

		Clip--;

		Player.Effects |= Effects.MuzzleFlash;

		var source = Player.GetGunPosition();
		var aiming = Player.GetAutoaimVector( Player.Autoaim5Degrees );

		var direction = IsMultiplayer
			? Player.FireBulletsPlayer( 4, source, aiming, ConeDmShotgun, 2048f, Bullet.PlayerBuckshot, 0, 0, Player, Player.RandomSeed )
			: Player.FireBulletsPlayer( 6, source, aiming, Cone10Degrees, 2048f, Bullet.PlayerBuckshot, 0, 0, Player, Player.RandomSeed );

		PlaybackEvent( EventFlags.NotHost, Player, _singleFire, 0f, Vector3.Zero, Vector3.Zero, direction.x, direction.y, 0, 0, 0, 0 );

		if ( Clip == 0 && Player.AmmoSlot( PrimaryAmmoType ) <= 0 )
			Player.SetSuitUpdate( "!HEV_AMO0", false, 0 );

		PumpTime = (float)(Globals.Time + 0.5);

		NextPrimaryAttack = GetNextAttackDelay( 0.75f );
		NextSecondaryAttack = (float)(WeaponTimeBase + 0.75);

		TimeWeaponIdle = Clip != 0 ? (float)(WeaponTimeBase + 5.0) : (float)(WeaponTimeBase + 0.75);

		InSpecialReload = 0;
	}

	public override void SecondaryAttack()
	{
		if ( Player.WaterLevel == 3 )
		{
			PlayEmptySound();
			NextPrimaryAttack = GetNextAttackDelay( 0.15f );
			return;
		}

		if ( Clip <= 1 )
		{
			Reload();
			PlayEmptySound();
			return;
		}

		Player.WeaponVolume = LoudGunVolume;
		Player.WeaponFlash = NormalGunFlash;

		Clip -= 2;

		Player.Effects |= Effects.MuzzleFlash;

		Player.SetAnimation( PlayerAnim.Attack1 );

		var source = Player.GetGunPosition();
		var aiming = Player.GetAutoaimVector( Player.Autoaim5Degrees );

		var direction = IsMultiplayer
			? Player.FireBulletsPlayer( 8, source, aiming, ConeDmDoubleShotgun, 2048f, Bullet.PlayerBuckshot, 0, 0, Player, Player.RandomSeed )
			: Player.FireBulletsPlayer( 12, source, aiming, Cone10Degrees, 2048f, Bullet.PlayerBuckshot, 0, 0, Player, Player.RandomSeed );

		PlaybackEvent( EventFlags.NotHost, Player, _doubleFire, 0f, Vector3.Zero, Vector3.Zero, direction.x, direction.y, 0, 0, 0, 0 );

		if ( Clip == 0 && Player.AmmoSlot( PrimaryAmmoType ) <= 0 )
			Player.SetSuitUpdate( "!HEV_AMO0", false, 0 );

		PumpTime = (float)(Globals.Time + 0.95);

		NextPrimaryAttack = GetNextAttackDelay( 1.5f );
		NextSecondaryAttack = (float)(WeaponTimeBase + 1.5);

		TimeWeaponIdle = Clip != 0 ? (float)(WeaponTimeBase + 6.0) : 1.5f;

		InSpecialReload = 0;
	}

	public override void Reload()
	{
		if ( Player.AmmoSlot( PrimaryAmmoType ) <= 0 || Clip == MaxClipSize )
			return;

		if ( NextPrimaryAttack > WeaponTimeBase )
			return;

		if ( InSpecialReload == 0 )
		{
			SendWeaponAnim( AnimStartReload );
			InSpecialReload = 1;

			Player.NextAttack = (float)(WeaponTimeBase + 0.6);
			TimeWeaponIdle = (float)(WeaponTimeBase + 0.6);
			NextPrimaryAttack = GetNextAttackDelay( 1f );
			NextSecondaryAttack = (float)(WeaponTimeBase + 1.0);
		}
		else if ( InSpecialReload == 1 )
		{
			if ( TimeWeaponIdle > WeaponTimeBase )
				return;

			InSpecialReload = 2;

			var sample = Server.RandomLong( 0, 1 ) != 0 ? "weapons/reload1.wav" : "weapons/reload3.wav";

			Sound.EmitDyn( Player, SoundChannel.Item, sample, 1f, Server.AttenuationNormal, 0, 85 + Server.RandomLong( 0, 0x1f ) );

			SendWeaponAnim( AnimReload );

			_nextReload = (float)(WeaponTimeBase + 0.5);
			TimeWeaponIdle = (float)(WeaponTimeBase + 0.5);
		}
		else
		{
			Clip += 1;
			Player.SetAmmoSlot( PrimaryAmmoType, Player.AmmoSlot( PrimaryAmmoType ) - 1 );
			InSpecialReload = 1;
		}
	}

	public override void WeaponIdle()
	{
		ResetEmptySound();

		Player.GetAutoaimVector( Player.Autoaim5Degrees );

		if ( TimeWeaponIdle >= WeaponTimeBase )
			return;

		var hasAmmo = Player.AmmoSlot( PrimaryAmmoType ) != 0;

		if ( Clip == 0 && InSpecialReload == 0 && hasAmmo )
		{
			Reload();
		}
		else if ( InSpecialReload != 0 )
		{
			if ( Clip != 8 && hasAmmo )
			{
				Reload();
				return;
			}

			SendWeaponAnim( AnimPump );

			Sound.EmitDyn( Player, SoundChannel.Item, "weapons/scock1.wav", 1f, Server.AttenuationNormal, 0, 95 + Server.RandomLong( 0, 0x1f ) );

			InSpecialReload = 0;
			TimeWeaponIdle = (float)(WeaponTimeBase + 1.5);
		}
		else
		{
			int animation;
			var random = Util.SharedRandomFloat( (uint)Player.RandomSeed, 0f, 1f );

			if ( random <= 0.8 )
			{
				animation = AnimIdleDeep;
				TimeWeaponIdle = (float)(WeaponTimeBase + (60.0 / 12.0));
			}
			else if ( random <= 0.95 )
			{
				animation = AnimIdle;
				TimeWeaponIdle = (float)(WeaponTimeBase + (20.0 / 9.0));
			}
			else
			{
				animation = AnimIdle4;
				TimeWeaponIdle = (float)(WeaponTimeBase + (20.0 / 9.0));
			}

			SendWeaponAnim( animation );
		}
	}

	public override void ItemPostFrame()
	{
		if ( PumpTime != 0f && PumpTime < Globals.Time )
		{
			Sound.EmitDyn( Player, SoundChannel.Item, "weapons/scock1.wav", 1f, Server.AttenuationNormal, 0, 95 + Server.RandomLong( 0, 0x1f ) );

			PumpTime = 0f;
		}

		base.ItemPostFrame();
	}
}

[LinkEntityToClass( "ammo_buckshot" )]
public class ShotgunAmmo : BasePlayerAmmo
{
	private const int BoxGive = 12;

	public override void Spawn()
	{
		Precache();
		SetModel( "models/w_shotbox.mdl" );
		base.Spawn();
	}

	public override void Precache()
	{
		PrecacheModel( "models/w_shotbox.mdl" );
		PrecacheSound( "items/9mmclip1.wav" );
	}

	public override bool AddAmmo( BaseEntity other )
	{
		if ( other.GiveAmmo( BoxGive, "buckshot", Shotgun.MaxCarryBuckshot ) == -1 )
			return false;

		Sound.Emit( this, SoundChannel.Item, "items/9mmclip1.wav", 1f, Server.AttenuationNormal );

		return true;
	}
}
