using System;
using GoldSrc;
using GoldSrc.Bsp;

namespace HalfLife;

[LinkEntityToClass( "crossbow_bolt" )]
public class CrossbowBolt : BaseEntity
{
	public const float AirVelocity = 2000f;
	public const float WaterVelocity = 1000f;

	public static CrossbowBolt BoltCreate( Server server )
	{
		var bolt = server.CreateEntity<CrossbowBolt>();

		bolt.ClassName = "bolt";
		bolt.Spawn();

		return bolt;
	}

	public override void Spawn()
	{
		Precache();

		MoveType = MoveType.Fly;
		Solid = Solid.BBox;

		Gravity = 0.5f;

		SetModel( "models/crossbow_bolt.mdl" );

		SetOrigin( Origin );
		SetSize( Vector3.Zero, Vector3.Zero );

		SetTouch( BoltTouch );
		SetThink( BubbleThink );
		NextThink = (float)(Globals.Time + 0.2);
	}

	public override void Precache()
	{
		PrecacheModel( "models/crossbow_bolt.mdl" );
		PrecacheSound( "weapons/xbow_hitbod1.wav" );
		PrecacheSound( "weapons/xbow_hitbod2.wav" );
		PrecacheSound( "weapons/xbow_fly1.wav" );
		PrecacheSound( "weapons/xbow_hit1.wav" );
		PrecacheSound( "fvox/beep.wav" );
		PrecacheModel( "sprites/streak.spr" );
	}

	public override int Classify() => ClassNone;

	private void BoltImpact( Vector3 origin, Vector3 direction )
	{
		Server.MessageBegin( MessageDest.Pvs, UserMessages.BoltImpact, origin );
		Server.WriteCoord( origin.x );
		Server.WriteCoord( origin.y );
		Server.WriteCoord( origin.z );
		Server.WriteLong( BitConverter.SingleToInt32Bits( direction.x ) );
		Server.WriteLong( BitConverter.SingleToInt32Bits( direction.y ) );
		Server.WriteLong( BitConverter.SingleToInt32Bits( direction.z ) );
		Server.MessageEnd();
	}

	public void BoltTouch( BaseEntity other )
	{
		SetTouch( null );
		SetThink( null );

		Combat.ClearMultiDamage();
		ScenePhysicsDamage.MeleeImpact( Globals.Trace, HalfLifeDll.SkillData.PlrDmgCrossbowMonster, Velocity );

		if ( other.DamageMode != DamageNo )
		{
			var trace = Globals.Trace;
			var owner = Owner as BaseEntity;
			var direction = Util.Normalize( Velocity );

			if ( other.IsPlayer )
				other.TraceAttack( owner, HalfLifeDll.SkillData.PlrDmgCrossbowClient, direction, trace, DamageNeverGib );
			else
				other.TraceAttack( owner, HalfLifeDll.SkillData.PlrDmgCrossbowMonster, direction, trace, DamageBullet | DamageNeverGib );

			Combat.ApplyMultiDamage( this, owner );

			Velocity = Vector3.Zero;

			var sample = Server.RandomLong( 0, 1 ) == 0 ? "weapons/xbow_hitbod1.wav" : "weapons/xbow_hitbod2.wav";

			Sound.Emit( this, SoundChannel.Body, sample, 1f, Server.AttenuationNormal );

			if ( GameCvars.Ragdolls != 0f )
			{
				var wall = Util.TraceLine( Origin, Origin + (direction * 128f), IgnoreMonsters, other );

				if ( wall.Fraction != 1f )
					BoltImpact( wall.EndPos, direction );
			}

			if ( !HalfLifeDll.GameRules.IsMultiplayer() )
				Killed( this, GibNever );
		}
		else
		{
			Sound.EmitDyn( this, SoundChannel.Body, "weapons/xbow_hit1.wav", Server.RandomFloat( 0.95f, 1f ), Server.AttenuationNormal, 0, 98 + Server.RandomLong( 0, 7 ) );

			SetThink( SubRemove );
			NextThink = Globals.Time;

			var sticky = GameCvars.Ragdolls != 0f && !HalfLifeDll.GameRules.IsMultiplayer();

			if ( sticky )
				BoltImpact( Origin, Util.Normalize( Velocity ) );

			if ( !sticky && other.ClassName == "worldspawn" )
			{
				var direction = Util.Normalize( Velocity );

				SetOrigin( Origin - (direction * 12f) );
				Angles = MathLib.VectorAngles( direction );
				Solid = Solid.Not;
				MoveType = MoveType.Fly;
				Velocity = Vector3.Zero;
				AVelocity = AVelocity with { z = 0f };
				Angles = Angles with { z = Server.RandomLong( 0, 360 ) };
				NextThink = (float)(Globals.Time + 10.0);
			}

			if ( Util.PointContents( Origin ) != (int)Contents.Water )
				Util.Sparks( Origin );
		}

		if ( HalfLifeDll.GameRules.IsMultiplayer() )
		{
			SetThink( ExplodeThink );
			NextThink = (float)(Globals.Time + 0.1);
		}
	}

	public void BubbleThink()
	{
		NextThink = (float)(Globals.Time + 0.1);

		if ( WaterLevel == 0 )
			return;

		Util.BubbleTrail( Origin - (Velocity * 0.1f), Origin, 1 );
	}

	public void ExplodeThink()
	{
		var contents = Util.PointContents( Origin );

		Damage = 40f;

		Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, Origin );
		Server.WriteByte( TempEntity.Explosion );
		Server.WriteCoord( Origin.x );
		Server.WriteCoord( Origin.y );
		Server.WriteCoord( Origin.z );
		Server.WriteShort( contents != (int)Contents.Water ? GameModels.Fireball : GameModels.WaterExplosion );
		Server.WriteByte( 10 );
		Server.WriteByte( 15 );
		Server.WriteByte( TempEntity.ExplosionNone );
		Server.MessageEnd();

		var owner = Owner as BaseEntity;

		Owner = null;

		Combat.RadiusDamage( Origin, this, owner, Damage, 128f, ClassNone, DamageBlast | DamageAlwaysGib );

		Remove();
	}
}

[LinkEntityToClass( "weapon_crossbow" )]
public class Crossbow : BasePlayerWeapon
{
	public const int WeaponId = 6;
	public const int WeaponWeight = 10;
	public const int MaxClipSize = 5;
	public const int DefaultGive = 5;
	public const int MaxCarryBolt = 50;

	private const int AnimIdle1 = 0;
	private const int AnimIdle2 = 1;
	private const int AnimFidget1 = 2;
	private const int AnimFidget2 = 3;
	private const int AnimReload = 7;
	private const int AnimDraw1 = 8;
	private const int AnimDraw2 = 9;
	private const int AnimHolster1 = 10;
	private const int AnimHolster2 = 11;

	private bool _inZoom;
	private int _crossbow;
	private int _crossbow2;

	public override int ItemSlot => 3;

	public override bool UseDecrement => true;

	public override void Spawn()
	{
		Precache();

		Id = WeaponId;
		SetModel( "models/w_crossbow.mdl" );

		DefaultAmmo = DefaultGive;

		FallInit();
	}

	public override bool AddToPlayer( Player player )
	{
		if ( !base.AddToPlayer( player ) )
			return false;

		SendWeaponPickup( player );

		return true;
	}

	public override void Precache()
	{
		PrecacheModel( "models/w_crossbow.mdl" );
		PrecacheModel( "models/v_crossbow.mdl" );
		PrecacheModel( "models/p_crossbow.mdl" );

		PrecacheSound( "weapons/xbow_fire1.wav" );
		PrecacheSound( "weapons/xbow_reload1.wav" );

		if ( !ClientSide )
			Util.PrecacheOther( "crossbow_bolt" );

		_crossbow = PrecacheEvent( 1, "events/crossbow1.sc" );
		_crossbow2 = PrecacheEvent( 1, "events/crossbow2.sc" );
	}

	public override bool GetItemInfo( out ItemInfo info )
	{
		info = new ItemInfo
		{
			Name = ClassName,
			Ammo1 = "bolts",
			MaxAmmo1 = MaxCarryBolt,
			Ammo2 = null,
			MaxAmmo2 = -1,
			MaxClip = MaxClipSize,
			Slot = 2,
			Position = 2,
			Id = WeaponId,
			Flags = 0,
			Weight = WeaponWeight
		};

		return true;
	}

	public override bool Deploy()
	{
		return DefaultDeploy( "models/v_crossbow.mdl", "models/p_crossbow.mdl", Clip != 0 ? AnimDraw1 : AnimDraw2, "bow" );
	}

	public override void Holster( int skipLocal = 0 )
	{
		InReload = false;

		if ( _inZoom )
			SecondaryAttack();

		Player.NextAttack = (float)(WeaponTimeBase + 0.5);

		SendWeaponAnim( Clip != 0 ? AnimHolster1 : AnimHolster2 );
	}

	public override void PrimaryAttack()
	{
		if ( _inZoom && IsMultiplayer )
		{
			FireSniperBolt();
			return;
		}

		FireBolt();
	}

	public void FireSniperBolt()
	{
		NextPrimaryAttack = GetNextAttackDelay( 0.75f );

		if ( Clip == 0 )
		{
			PlayEmptySound();
			return;
		}

		Player.WeaponVolume = QuietGunVolume;
		Clip--;

		PlaybackEvent( EventFlags.NotHost, Player, _crossbow2, 0f, Vector3.Zero, Vector3.Zero, 0f, 0f, Clip, Player.AmmoSlot( PrimaryAmmoType ), 0, 0 );

		Player.SetAnimation( PlayerAnim.Attack1 );

		MakeVectors( Player.ViewAngle + Player.PunchAngle );

		var source = Player.GetGunPosition() - (Globals.Up * 2f);
		var direction = Globals.Forward;

		var trace = TraceLine( source, source + (direction * 8192f), DontIgnoreMonsters, Player );

		if ( !ClientSide )
		{
			Combat.ClearMultiDamage();
			ScenePhysicsDamage.MeleeImpact( trace, 120f, direction );

			if ( trace.Ent is BaseEntity hit && hit.DamageMode != DamageNo )
			{
				hit.TraceAttack( Player, 120f, direction, trace, DamageBullet | DamageNeverGib );
				Combat.ApplyMultiDamage( this, Player );
			}
		}
	}

	public void FireBolt()
	{
		if ( Clip == 0 )
		{
			PlayEmptySound();
			return;
		}

		Player.WeaponVolume = QuietGunVolume;

		Clip--;

		PlaybackEvent( EventFlags.NotHost, Player, _crossbow, 0f, Vector3.Zero, Vector3.Zero, 0f, 0f, Clip, Player.AmmoSlot( PrimaryAmmoType ), 0, 0 );

		Player.SetAnimation( PlayerAnim.Attack1 );

		var aimAngles = Player.ViewAngle + Player.PunchAngle;

		MakeVectors( aimAngles );

		aimAngles.x = -aimAngles.x;

		var source = Player.GetGunPosition() - (Globals.Up * 2f);
		var direction = Globals.Forward;

		if ( !ClientSide )
		{
			var bolt = CrossbowBolt.BoltCreate( Server );

			bolt.Origin = source;
			bolt.Angles = aimAngles;
			bolt.Owner = Player;

			if ( Player.WaterLevel == 3 )
			{
				bolt.Velocity = direction * CrossbowBolt.WaterVelocity;
				bolt.Speed = CrossbowBolt.WaterVelocity;
			}
			else
			{
				bolt.Velocity = direction * CrossbowBolt.AirVelocity;
				bolt.Speed = CrossbowBolt.AirVelocity;
			}

			bolt.AVelocity = bolt.AVelocity with { z = 10f };
		}

		if ( Clip == 0 && Player.AmmoSlot( PrimaryAmmoType ) <= 0 )
			Player.SetSuitUpdate( "!HEV_AMO0", false, 0 );

		NextPrimaryAttack = GetNextAttackDelay( 0.75f );

		NextSecondaryAttack = (float)(WeaponTimeBase + 0.75);

		TimeWeaponIdle = Clip != 0 ? (float)(WeaponTimeBase + 5.0) : (float)(WeaponTimeBase + 0.75);
	}

	public override void SecondaryAttack()
	{
		if ( Player.Fov != 0f )
		{
			Player.Fov = Player.FieldOfViewSetting = 0;
			_inZoom = false;
		}
		else if ( Player.Fov != 20f )
		{
			Player.Fov = Player.FieldOfViewSetting = 20;
			_inZoom = true;
		}

		NextThink = (float)(WeaponTimeBase + 0.1);
		NextSecondaryAttack = (float)(WeaponTimeBase + 1.0);
	}

	public override void Reload()
	{
		if ( Player.AmmoBolts <= 0 )
			return;

		if ( Player.Fov != 0f )
			SecondaryAttack();

		if ( DefaultReload( 5, AnimReload, 4.5f ) )
			Sound.EmitDyn( Player, SoundChannel.Item, "weapons/xbow_reload1.wav", Server.RandomFloat( 0.95f, 1f ), Server.AttenuationNormal, 0, 93 + Server.RandomLong( 0, 0xF ) );
	}

	public override void WeaponIdle()
	{
		Player.GetAutoaimVector( Player.Autoaim2Degrees );

		ResetEmptySound();

		if ( TimeWeaponIdle >= WeaponTimeBase )
			return;

		var random = Util.SharedRandomFloat( (uint)Player.RandomSeed, 0f, 1f );

		if ( random <= 0.75 )
		{
			SendWeaponAnim( Clip != 0 ? AnimIdle1 : AnimIdle2 );
			TimeWeaponIdle = WeaponTimeBase + Util.SharedRandomFloat( (uint)Player.RandomSeed, 10f, 15f );
		}
		else if ( Clip != 0 )
		{
			SendWeaponAnim( AnimFidget1 );
			TimeWeaponIdle = (float)(WeaponTimeBase + (90.0 / 30.0));
		}
		else
		{
			SendWeaponAnim( AnimFidget2 );
			TimeWeaponIdle = (float)(WeaponTimeBase + (80.0 / 30.0));
		}
	}
}

[LinkEntityToClass( "ammo_crossbow" )]
public class CrossbowAmmo : BasePlayerAmmo
{
	public override void Spawn()
	{
		Precache();
		SetModel( "models/w_crossbow_clip.mdl" );
		base.Spawn();
	}

	public override void Precache()
	{
		PrecacheModel( "models/w_crossbow_clip.mdl" );
		PrecacheSound( "items/9mmclip1.wav" );
	}

	public override bool AddAmmo( BaseEntity other )
	{
		if ( other.GiveAmmo( Crossbow.MaxClipSize, "bolts", Crossbow.MaxCarryBolt ) == -1 )
			return false;

		Sound.Emit( this, SoundChannel.Item, "items/9mmclip1.wav", 1f, Server.AttenuationNormal );

		return true;
	}
}
