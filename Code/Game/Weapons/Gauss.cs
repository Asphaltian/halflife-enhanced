using System;
using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "weapon_gauss" )]
public class Gauss : BasePlayerWeapon
{
	public const int WeaponId = 9;
	public const int WeaponWeight = 20;
	public const int DefaultGive = 20;
	public const int MaxCarryUranium = 100;
	public const int UraniumBoxGive = 20;

	private const int PrimaryChargeVolume = 256;
	private const int PrimaryFireVolume = 450;

	private const int AnimIdle = 0;
	private const int AnimSpinUp = 3;
	private const int AnimSpin = 4;
	private const int AnimHolster = 7;
	private const int AnimDraw = 8;

	private const int SoundChangePitch = 1 << 7;

	[Save( FieldType.Integer )]
	public override int InAttack { get; set; }

	[Save( FieldType.Boolean )]
	private bool _primaryFire;

	private int _soundState;
	private int _gaussFire;
	private int _gaussSpin;

	public override int ItemSlot => 4;

	public override bool UseDecrement => true;

	private float FullChargeTime => IsMultiplayer ? 1.5f : 4f;

	public override void Spawn()
	{
		Precache();

		Id = WeaponId;
		SetModel( "models/w_gauss.mdl" );

		DefaultAmmo = DefaultGive;

		FallInit();
	}

	public override void Precache()
	{
		PrecacheModel( "models/w_gauss.mdl" );
		PrecacheModel( "models/v_gauss.mdl" );
		PrecacheModel( "models/p_gauss.mdl" );

		PrecacheSound( "items/9mmclip1.wav" );

		PrecacheSound( "weapons/gauss2.wav" );
		PrecacheSound( "weapons/electro4.wav" );
		PrecacheSound( "weapons/electro5.wav" );
		PrecacheSound( "weapons/electro6.wav" );
		PrecacheSound( "ambience/pulsemachine.wav" );

		PrecacheModel( "sprites/hotglow.spr" );
		PrecacheModel( "sprites/smoke.spr" );

		_gaussFire = PrecacheEvent( 1, "events/gauss.sc" );
		_gaussSpin = PrecacheEvent( 1, "events/gaussspin.sc" );
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
			Ammo1 = "uranium",
			MaxAmmo1 = MaxCarryUranium,
			Ammo2 = null,
			MaxAmmo2 = -1,
			MaxClip = NoClip,
			Slot = 3,
			Position = 1,
			Id = WeaponId,
			Flags = 0,
			Weight = WeaponWeight
		};

		return true;
	}

	public override bool Deploy()
	{
		Player.PlayAftershock = 0f;

		return DefaultDeploy( "models/v_gauss.mdl", "models/p_gauss.mdl", AnimDraw, "gauss" );
	}

	public override void Holster( int skipLocal = 0 )
	{
		PlaybackEvent( EventFlags.Reliable | EventFlags.Global, Player, _gaussFire, 0.01f, Player.Origin, Player.Angles, 0f, 0f, 0, 0, 0, 1 );

		Player.NextAttack = (float)(WeaponTimeBase + 0.5);

		SendWeaponAnim( AnimHolster );
		InAttack = 0;
	}

	public override void PrimaryAttack()
	{
		if ( Player.WaterLevel == 3 )
		{
			PlayEmptySound();
			NextSecondaryAttack = NextPrimaryAttack = GetNextAttackDelay( 0.15f );
			return;
		}

		if ( Player.AmmoSlot( PrimaryAmmoType ) < 2 )
		{
			PlayEmptySound();
			Player.NextAttack = (float)(WeaponTimeBase + 0.5);
			return;
		}

		Player.WeaponVolume = PrimaryFireVolume;
		_primaryFire = true;

		Player.SetAmmoSlot( PrimaryAmmoType, Player.AmmoSlot( PrimaryAmmoType ) - 2 );

		StartFire();
		InAttack = 0;
		TimeWeaponIdle = (float)(WeaponTimeBase + 1.0);
		Player.NextAttack = (float)(WeaponTimeBase + 0.2);
	}

	public override void SecondaryAttack()
	{
		Player.StartCharge = Math.Min( Player.StartCharge, Globals.Time );

		if ( Player.WaterLevel == 3 )
		{
			if ( InAttack != 0 )
			{
				Sound.EmitDyn( Player, SoundChannel.Weapon, "weapons/electro4.wav", 1f, Server.AttenuationNormal, 0, 80 + Server.RandomLong( 0, 0x3f ) );
				SendWeaponAnim( AnimIdle );
				InAttack = 0;
			}
			else
			{
				PlayEmptySound();
			}

			NextSecondaryAttack = NextPrimaryAttack = GetNextAttackDelay( 0.5f );
			return;
		}

		if ( InAttack == 0 )
		{
			if ( Player.AmmoSlot( PrimaryAmmoType ) <= 0 )
			{
				Sound.Emit( Player, SoundChannel.Weapon, "weapons/357_cock1.wav", 0.8f, Server.AttenuationNormal );
				Player.NextAttack = (float)(WeaponTimeBase + 0.5);
				return;
			}

			_primaryFire = false;

			Player.SetAmmoSlot( PrimaryAmmoType, Player.AmmoSlot( PrimaryAmmoType ) - 1 );
			Player.NextAmmoBurn = WeaponTimeBase;

			Player.WeaponVolume = PrimaryChargeVolume;

			SendWeaponAnim( AnimSpinUp );
			InAttack = 1;
			TimeWeaponIdle = (float)(WeaponTimeBase + 0.5);
			Player.StartCharge = Globals.Time;
			Player.AmmoStartCharge = WeaponTimeBase + FullChargeTime;

			PlaybackEvent( EventFlags.NotHost, Player, _gaussSpin, 0f, Vector3.Zero, Vector3.Zero, 0f, 0f, 110, 0, 0, 0 );

			_soundState = SoundChangePitch;
		}
		else if ( InAttack == 1 )
		{
			if ( TimeWeaponIdle < WeaponTimeBase )
			{
				SendWeaponAnim( AnimSpin );
				InAttack = 2;
			}
		}
		else
		{
			if ( WeaponTimeBase >= Player.NextAmmoBurn && Player.NextAmmoBurn != 1000f )
			{
				Player.SetAmmoSlot( PrimaryAmmoType, Player.AmmoSlot( PrimaryAmmoType ) - 1 );
				Player.NextAmmoBurn = (float)(WeaponTimeBase + (IsMultiplayer ? 0.1 : 0.3));
			}

			if ( Player.AmmoSlot( PrimaryAmmoType ) <= 0 )
			{
				StartFire();
				InAttack = 0;
				TimeWeaponIdle = (float)(WeaponTimeBase + 1.0);
				Player.NextAttack = WeaponTimeBase + 1f;
				return;
			}

			if ( WeaponTimeBase >= Player.AmmoStartCharge )
				Player.NextAmmoBurn = 1000f;

			var pitch = (int)(((Globals.Time - Player.StartCharge) * (150 / FullChargeTime)) + 100);

			if ( pitch > 250 )
				pitch = 250;

			if ( _soundState == 0 )
				Log.Info( $"sound state {_soundState}" );

			PlaybackEvent( EventFlags.NotHost, Player, _gaussSpin, 0f, Vector3.Zero, Vector3.Zero, 0f, 0f, pitch, 0, _soundState == SoundChangePitch ? 1 : 0, 0 );

			_soundState = SoundChangePitch;

			Player.WeaponVolume = PrimaryChargeVolume;

			if ( Player.StartCharge < Globals.Time - 10 )
			{
				Sound.EmitDyn( Player, SoundChannel.Weapon, "weapons/electro4.wav", 1f, Server.AttenuationNormal, 0, 80 + Server.RandomLong( 0, 0x3f ) );
				Sound.EmitDyn( Player, SoundChannel.Item, "weapons/electro6.wav", 1f, Server.AttenuationNormal, 0, 75 + Server.RandomLong( 0, 0x3f ) );

				InAttack = 0;
				TimeWeaponIdle = (float)(WeaponTimeBase + 1.0);
				Player.NextAttack = (float)(WeaponTimeBase + 1.0);

				if ( !ClientSide )
				{
					Player.TakeDamage( World, World, 50f, DamageShock );
					Util.ScreenFade( Player, new Vector3( 255f, 128f, 0f ), 2f, 0.5f, 128, Util.FadeIn );
				}

				SendWeaponAnim( AnimIdle );
			}
		}
	}

	public void StartFire()
	{
		Player.StartCharge = Math.Min( Player.StartCharge, Globals.Time );

		MakeVectors( Player.ViewAngle + Player.PunchAngle );

		var aiming = Globals.Forward;
		var source = Player.GetGunPosition();
		float damage;

		if ( Globals.Time - Player.StartCharge > FullChargeTime )
			damage = 200f;
		else
			damage = 200 * ((Globals.Time - Player.StartCharge) / FullChargeTime);

		if ( _primaryFire )
			damage = ClientSide ? 20f : HalfLifeDll.SkillData.PlrDmgGauss;

		if ( InAttack != 3 )
		{
			if ( !ClientSide )
			{
				var verticalVelocity = Player.Velocity.z;

				if ( !_primaryFire )
					Player.Velocity -= Globals.Forward * damage * 5f;

				if ( !HalfLifeDll.GameRules.IsMultiplayer() )
					Player.Velocity = Player.Velocity with { z = verticalVelocity };
			}

			Player.SetAnimation( PlayerAnim.Attack1 );
		}

		Player.PlayAftershock = Globals.Time + Util.SharedRandomFloat( (uint)Player.RandomSeed, 0.3f, 0.8f );

		Fire( source, aiming, damage );
	}

	public void Fire( Vector3 originalSource, Vector3 direction, float damage )
	{
		Player.WeaponVolume = PrimaryFireVolume;

		var source = originalSource;
		var destination = source + (direction * 8192f);
		var hasPunched = false;
		var firstBeam = true;
		var maxHits = 10;

		BaseEntity ignore = Player;

		if ( ClientSide && !_primaryFire )
			ClientWeapons.RunningGaussPrediction = true;

		PlaybackEvent( EventFlags.NotHost, Player, _gaussFire, 0f, Player.Origin, Player.Angles, damage, 0f, 0, 0, _primaryFire ? 1 : 0, 0 );
		PlaybackEvent( EventFlags.NotHost | EventFlags.Reliable, Player, _gaussFire, 0.01f, Player.Origin, Player.Angles, 0f, 0f, 0, 0, 0, 1 );

		if ( ClientSide )
			return;

		while ( damage > 10f && maxHits > 0 )
		{
			maxHits--;

			var trace = TraceLine( source, destination, DontIgnoreMonsters, ignore );

			if ( trace.AllSolid )
				break;

			if ( trace.Ent is not BaseEntity entity )
				break;

			if ( firstBeam )
			{
				Player.Effects |= Effects.MuzzleFlash;
				firstBeam = false;
			}

			ScenePhysicsDamage.MeleeImpact( trace, damage, direction );

			if ( entity.DamageMode != DamageNo )
			{
				Combat.ClearMultiDamage();

				if ( entity == Player )
					trace.HitGroup = 0;

				entity.TraceAttack( Player, damage, direction, trace, DamageBullet );
				Combat.ApplyMultiDamage( Player, Player );
			}

			if ( !entity.ReflectGauss )
			{
				source = trace.EndPos + direction;
				ignore = entity;
				continue;
			}

			ignore = null;

			var n = -Vector3.Dot( trace.PlaneNormal, direction );

			if ( n < 0.5 )
			{
				direction = (2f * trace.PlaneNormal * n) + direction;
				source = trace.EndPos + (direction * 8f);
				destination = source + (direction * 8192f);

				Player.RadiusDamage( trace.EndPos, this, Player, damage * n, ClassNone, DamageBlast );

				if ( n == 0f )
					n = 0.1f;

				damage *= 1f - n;
				continue;
			}

			if ( hasPunched )
				break;

			hasPunched = true;

			if ( _primaryFire )
			{
				damage = 0f;
				continue;
			}

			var beamTrace = TraceLine( trace.EndPos + (direction * 8f), destination, DontIgnoreMonsters, ignore );

			if ( beamTrace.AllSolid )
			{
				damage = 0f;
				continue;
			}

			beamTrace = TraceLine( beamTrace.EndPos, trace.EndPos, DontIgnoreMonsters, ignore );

			var thickness = MathLib.Length( beamTrace.EndPos - trace.EndPos );

			if ( thickness >= damage )
				continue;

			if ( thickness == 0f )
				thickness = 1f;

			damage -= thickness;

			var radius = HalfLifeDll.GameRules.IsMultiplayer() ? damage * 1.75f : damage * 2.5f;

			Combat.RadiusDamage( beamTrace.EndPos + (direction * 8f), this, Player, damage, radius, ClassNone, DamageBlast );

			SoundEnt.InsertSound( WorldSound.Combat, Origin, NormalExplosionVolume, 3f );

			source = beamTrace.EndPos + direction;
		}
	}

	public override void WeaponIdle()
	{
		ResetEmptySound();

		if ( Player.PlayAftershock != 0f && Player.PlayAftershock < Globals.Time )
		{
			switch ( Server.RandomLong( 0, 3 ) )
			{
				case 0:
					Sound.Emit( Player, SoundChannel.Weapon, "weapons/electro4.wav", Server.RandomFloat( 0.7f, 0.8f ), Server.AttenuationNormal );
					break;
				case 1:
					Sound.Emit( Player, SoundChannel.Weapon, "weapons/electro5.wav", Server.RandomFloat( 0.7f, 0.8f ), Server.AttenuationNormal );
					break;
				case 2:
					Sound.Emit( Player, SoundChannel.Weapon, "weapons/electro6.wav", Server.RandomFloat( 0.7f, 0.8f ), Server.AttenuationNormal );
					break;
			}

			Player.PlayAftershock = 0f;
		}

		if ( TimeWeaponIdle > WeaponTimeBase )
			return;

		if ( InAttack != 0 )
		{
			StartFire();
			InAttack = 0;
			TimeWeaponIdle = (float)(WeaponTimeBase + 2.0);
			return;
		}

		var random = Server.RandomFloat( 0f, 1f );

		if ( random <= 0.75 )
			TimeWeaponIdle = WeaponTimeBase + Util.SharedRandomFloat( (uint)Player.RandomSeed, 10f, 15f );
		else
			TimeWeaponIdle = WeaponTimeBase + 3f;
	}
}

[LinkEntityToClass( "ammo_gaussclip" )]
public class GaussAmmo : BasePlayerAmmo
{
	public override void Spawn()
	{
		Precache();
		SetModel( "models/w_gaussammo.mdl" );
		base.Spawn();
	}

	public override void Precache()
	{
		PrecacheModel( "models/w_gaussammo.mdl" );
		PrecacheSound( "items/9mmclip1.wav" );
	}

	public override bool AddAmmo( BaseEntity other )
	{
		if ( other.GiveAmmo( Gauss.UraniumBoxGive, "uranium", Gauss.MaxCarryUranium ) == -1 )
			return false;

		Sound.Emit( this, SoundChannel.Item, "items/9mmclip1.wav", 1f, Server.AttenuationNormal );

		return true;
	}
}
