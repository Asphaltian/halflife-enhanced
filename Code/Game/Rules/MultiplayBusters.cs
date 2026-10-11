using System;
using GoldSrc;

namespace HalfLife;

public sealed class MultiplayBusters : HalfLifeMultiplay
{
	private const float EgonBustingTime = 10f;

	private float _egonBustingCheckTime = -1f;

	public MultiplayBusters( Server server ) : base( server )
	{
		_egonBustingCheckTime = -1f;
	}

	public static bool IsBustingGame() => Cvar.GetFloat( "sv_busters" ) == 1f;

	public static bool IsPlayerBusting( BaseEntity player )
	{
		if ( player is null || !player.IsPlayer || !IsBustingGame() )
			return false;

		return ((Player)player).HasPlayerItemFromId( Egon.WeaponId );
	}

	private static bool BustingCanHaveItem( Player player, BaseEntity item )
	{
		var weaponOrAmmo = item.ClassName.Contains( "weapon_", StringComparison.Ordinal ) || item.ClassName.Contains( "ammo_", StringComparison.Ordinal );

		return !(IsPlayerBusting( player ) && weaponOrAmmo);
	}

	public override void Think()
	{
		CheckForEgons();

		base.Think();
	}

	public override int PointsForKill( Player attacker, Player killed )
	{
		if ( IsPlayerBusting( attacker ) )
			return 1;

		if ( IsPlayerBusting( killed ) )
			return 2;

		return 0;
	}

	public override void PlayerKilled( Player victim, Edict killer, Edict inflictor )
	{
		if ( IsPlayerBusting( victim ) )
		{
			Util.ClientPrintAll( UserMessages.HudPrintCenter, "The Buster is dead!!" );

			_egonBustingCheckTime = -1f;

			Player playerKiller = null;
			var killerEntity = killer as BaseEntity;

			if ( killerEntity is not null && killerEntity.Classify() == BaseEntity.ClassPlayer )
			{
				playerKiller = (Player)killerEntity;
			}
			else if ( killerEntity is not null && killerEntity.Classify() == BaseEntity.ClassVehicle && ((FuncVehicle)killerEntity).Driver is { } driver )
			{
				playerKiller = driver;
				killer = driver;
			}

			if ( playerKiller is not null )
				Util.ClientPrintAll( UserMessages.HudPrintTalk, $"{playerKiller.NetName} has has killed the Buster!\n" );

			victim.RenderFx = RenderFx.None;
			victim.RenderColor = Vector3.Zero;
		}

		base.PlayerKilled( victim, killer, inflictor );
	}

	public override void DeathNotice( Player victim, Edict killer, Edict inflictor )
	{
		if ( !IsPlayerBusting( victim ) && !IsPlayerBusting( killer as BaseEntity ) )
			return;

		base.DeathNotice( victim, killer, inflictor );
	}

	public override RuleCode WeaponShouldRespawn( BasePlayerItem weapon )
	{
		return weapon.Id == Egon.WeaponId ? RuleCode.WeaponRespawnNo : base.WeaponShouldRespawn( weapon );
	}

	private void CheckForEgons()
	{
		if ( _egonBustingCheckTime <= 0f )
		{
			_egonBustingCheckTime = Server.Globals.Time + EgonBustingTime;
			return;
		}

		if ( _egonBustingCheckTime > Server.Globals.Time )
			return;

		_egonBustingCheckTime = -1f;

		for ( var i = 1; i <= Server.MaxClients; i++ )
		{
			if ( IsPlayerBusting( Util.PlayerByIndex( i ) ) )
				return;
		}

		var bestFrags = 9999;
		Player bestPlayer = null;

		for ( var i = 1; i <= Server.MaxClients; i++ )
		{
			if ( Util.PlayerByIndex( i ) is not Player player || player.Frags > bestFrags )
				continue;

			bestFrags = (int)player.Frags;
			bestPlayer = player;
		}

		if ( bestPlayer is null )
			return;

		bestPlayer.GiveNamedItem( "weapon_egon" );

		for ( var entity = Util.FindEntityByClassname( null, "weaponbox" ); entity is not null; entity = Util.FindEntityByClassname( entity, "weaponbox" ) )
		{
			var weaponBox = (WeaponBox)entity;

			for ( var i = 0; i < BasePlayerItem.MaxItemTypes; i++ )
			{
				for ( var weapon = weaponBox.PlayerItems[i]; weapon is not null; weapon = weapon.Next )
				{
					if ( weapon.Id != Egon.WeaponId )
						continue;

					weaponBox.Kill();
					break;
				}
			}
		}
	}

	public override bool CanHavePlayerItem( Player player, BasePlayerItem weapon )
	{
		return BustingCanHaveItem( player, weapon ) && base.CanHavePlayerItem( player, weapon );
	}

	public override bool CanHaveItem( Player player, Item item )
	{
		return BustingCanHaveItem( player, item ) && base.CanHaveItem( player, item );
	}

	public override void PlayerGotWeapon( Player player, BasePlayerItem weapon )
	{
		if ( weapon.Id != Egon.WeaponId )
			return;

		player.RemoveAllItems( false );

		Util.ClientPrintAll( UserMessages.HudPrintCenter, "Long live the new Buster!" );
		Util.ClientPrintAll( UserMessages.HudPrintTalk, $"{player.NetName} is busting!\n" );

		SetPlayerModel( player );

		player.Health = player.MaxHealth;
		player.ArmorValue = 100f;
		player.RenderFx = RenderFx.GlowShell;
		player.RenderAmt = 25f;
		player.RenderColor = new Vector3( 0f, 75f, 250f );

		var egon = (BasePlayerWeapon)weapon;

		egon.DefaultAmmo = 100;
		player.Ammo[egon.PrimaryAmmoType] = egon.DefaultAmmo;

		Server.SetClientKeyValue( player.Index, ref Server.GetClient( player ).UserInfo, "model", "ivan" );
	}

	public override void ClientUserInfoChanged( Player player, string info )
	{
		SetPlayerModel( player );

		player.SetPrefsFromUserinfo( Info.ValueForKey( info, "cl_autowepswitch" ) );
	}

	public override void PlayerSpawn( Player player )
	{
		base.PlayerSpawn( player );

		SetPlayerModel( player );
	}

	private void SetPlayerModel( Player player )
	{
		Server.SetClientKeyValue( player.Index, ref Server.GetClient( player ).UserInfo, "model", IsPlayerBusting( player ) ? "ivan" : "skeleton" );
	}
}
