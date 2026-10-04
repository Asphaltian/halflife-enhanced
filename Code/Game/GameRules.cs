using GoldSrc;

namespace HalfLife;

public enum PlayerRelationship
{
	NotTeammate,
	Teammate,
	Enemy,
	Ally,
	Neutral
}

public enum RuleCode
{
	None,
	WeaponRespawnYes,
	WeaponRespawnNo,
	AmmoRespawnYes,
	AmmoRespawnNo,
	ItemRespawnYes,
	ItemRespawnNo,
	PlayerDropGunAll,
	PlayerDropGunActive,
	PlayerDropGunNo,
	PlayerDropAmmoAll,
	PlayerDropAmmoActive,
	PlayerDropAmmoNo
}

public abstract class GameRules
{
	protected Server Server { get; }

	protected GameRules( Server server )
	{
		Server = server;
	}

	public static GameRules Install( Server server )
	{
		server.ServerCommand( "exec game.cfg\n" );
		server.ServerExecute();

		if ( !server.Deathmatch )
		{
			HalfLifeDll.Teamplay = false;

			return new HalfLifeRules( server );
		}

		if ( Cvar.GetFloat( "mp_teamplay" ) > 0f )
		{
			HalfLifeDll.Teamplay = true;

			return new HalfLifeTeamplay( server );
		}

		HalfLifeDll.Teamplay = false;

		if ( Cvar.GetFloat( "sv_busters" ) == 1f )
			return new MultiplayBusters( server );

		return new HalfLifeMultiplay( server );
	}

	public virtual void RefreshSkillData()
	{
		var skill = (int)Cvar.GetFloat( "skill" );

		HalfLifeDll.SkillLevel = skill;

		if ( skill < 1 )
			skill = 1;
		else if ( skill > 3 )
			skill = 3;

		HalfLifeDll.SkillData.SkillLevel = skill;

		Log.Info( $"\nGAME SKILL LEVEL:{skill}\n" );

		HalfLifeDll.SkillData.Refresh();
	}

	public abstract void Think();
	public abstract bool ClientConnected( Edict entity, string name, string address, ref string rejectReason );
	public abstract void ClientDisconnected( Edict client );
	public abstract bool IsAllowedToSpawn( BaseEntity entity );
	public abstract bool AllowFlashlight();
	public abstract bool IsMultiplayer();
	public abstract bool IsDeathmatch();
	public virtual bool IsTeamplay() => false;
	public abstract bool IsCoOp();
	public virtual string GetGameDescription() => "Half-Life";
	public abstract void InitHud( Player player );
	public virtual void UpdateGameMode( Player player ) { }
	public abstract float PlayerFallDamage( Player player );
	public virtual bool PlayerCanTakeDamage( Player player, BaseEntity attacker ) => true;
	public virtual bool ShouldAutoAim( Player player, Edict target ) => true;
	public abstract void PlayerSpawn( Player player );
	public abstract void PlayerThink( Player player );
	public abstract bool PlayerCanRespawn( Player player );
	public abstract float PlayerSpawnTime( Player player );
	public virtual bool AllowAutoTargetCrosshair() => true;
	public virtual bool ClientCommand( Player player, string command ) => false;

	public virtual void ClientUserInfoChanged( Player player, string info )
	{
	}
	public abstract int PointsForKill( Player attacker, Player killed );
	public abstract void PlayerKilled( Player victim, Edict killer, Edict inflictor );
	public abstract void DeathNotice( Player victim, Edict killer, Edict inflictor );
	public abstract float HealthChargerRechargeTime();
	public virtual float HevChargerRechargeTime() => 0f;
	public abstract string GetTeamId( BaseEntity entity );
	public abstract PlayerRelationship PlayerRelationship( BaseEntity player, BaseEntity target );
	public virtual bool PlayTextureSounds() => true;
	public virtual bool PlayFootstepSounds( Player player, float volume ) => true;
	public abstract bool AllowMonsters();
	public abstract bool ShouldSwitchWeapon( Player player, BasePlayerItem weapon );
	public abstract bool GetNextBestWeapon( Player player, BasePlayerItem currentWeapon );
	public abstract void PlayerGotWeapon( Player player, BasePlayerItem weapon );
	public abstract RuleCode WeaponShouldRespawn( BasePlayerItem weapon );
	public abstract float WeaponRespawnTime( BasePlayerItem weapon );
	public abstract float WeaponTryRespawn( BasePlayerItem weapon );
	public abstract Vector3 WeaponRespawnSpot( BasePlayerItem weapon );
	public abstract bool CanHaveItem( Player player, Item item );
	public abstract void PlayerGotItem( Player player, Item item );
	public abstract RuleCode ItemShouldRespawn( Item item );
	public abstract float ItemRespawnTime( Item item );
	public abstract Vector3 ItemRespawnSpot( Item item );
	public abstract void PlayerGotAmmo( Player player, string name, int count );
	public abstract RuleCode AmmoShouldRespawn( BasePlayerAmmo ammo );
	public abstract float AmmoRespawnTime( BasePlayerAmmo ammo );
	public abstract Vector3 AmmoRespawnSpot( BasePlayerAmmo ammo );
	public abstract RuleCode DeadPlayerWeapons( Player player );
	public abstract RuleCode DeadPlayerAmmo( Player player );
	public virtual int GetTeamIndex( string teamName ) => -1;
	public virtual string GetIndexedTeamName( int teamIndex ) => "";
	public virtual bool IsValidTeam( string teamName ) => true;
	public virtual void ChangePlayerTeam( Player player, string teamName, bool kill, bool gib ) { }
	public virtual string SetDefaultPlayerTeam( Player player ) => "";

	public virtual bool CanHaveAmmo( Player player, string ammoName, int maxCarry )
	{
		if ( ammoName is null )
			return false;

		var ammoIndex = Player.GetAmmoIndex( ammoName );

		return ammoIndex > -1 && player.AmmoInventory( ammoIndex ) < maxCarry;
	}

	public virtual bool CanHavePlayerItem( Player player, BasePlayerItem weapon )
	{
		if ( player.DeadFlag != BaseEntity.DeadNo )
			return false;

		if ( weapon.Ammo1 is not null )
		{
			if ( !CanHaveAmmo( player, weapon.Ammo1, weapon.MaxAmmo1 ) && player.HasPlayerItem( weapon ) )
				return false;
		}
		else if ( player.HasPlayerItem( weapon ) )
		{
			return false;
		}

		return true;
	}
	public virtual void EndMultiplayerGame() { }

	public virtual Edict GetPlayerSpawnSpot( Player player )
	{
		var spot = player.EntSelectSpawnPoint();

		player.Origin = spot.Origin + new Vector3( 0f, 0f, 1f );
		player.ViewAngle = Vector3.Zero;
		player.Velocity = Vector3.Zero;
		player.Angles = spot.Angles;
		player.PunchAngle = Vector3.Zero;
		player.FixAngle = 1;

		return spot;
	}
}

public class HalfLifeRules : GameRules
{
	public HalfLifeRules( Server server ) : base( server )
	{
		server.ServerCommand( "exec spserver.cfg\n" );
		RefreshSkillData();
	}

	public override void Think() { }
	public override bool ClientConnected( Edict entity, string name, string address, ref string rejectReason ) => true;
	public override void ClientDisconnected( Edict client ) { }
	public override bool IsAllowedToSpawn( BaseEntity entity ) => true;
	public override bool AllowFlashlight() => true;
	public override bool IsMultiplayer() => false;
	public override bool IsDeathmatch() => false;
	public override bool IsCoOp() => false;
	public override void InitHud( Player player ) { }

	public override float PlayerFallDamage( Player player )
	{
		player.LastFallVelocity -= Player.MaxSafeFallSpeed;
		return player.LastFallVelocity * Player.DamageForFallSpeed;
	}

	public override void PlayerSpawn( Player player ) { }
	public override bool AllowAutoTargetCrosshair() => HalfLifeDll.SkillLevel == SkillData.Easy;
	public override void PlayerThink( Player player ) { }
	public override bool PlayerCanRespawn( Player player ) => true;
	public override float PlayerSpawnTime( Player player ) => Server.Globals.Time;
	public override int PointsForKill( Player attacker, Player killed ) => 1;
	public override void PlayerKilled( Player victim, Edict killer, Edict inflictor ) { }
	public override void DeathNotice( Player victim, Edict killer, Edict inflictor ) { }
	public override float HealthChargerRechargeTime() => 0f;
	public override string GetTeamId( BaseEntity entity ) => "";
	public override PlayerRelationship PlayerRelationship( BaseEntity player, BaseEntity target ) => HalfLife.PlayerRelationship.NotTeammate;
	public override bool AllowMonsters() => true;

	public override bool ShouldSwitchWeapon( Player player, BasePlayerItem weapon )
	{
		return player.ActiveItem is null || player.ActiveItem.CanHolster();
	}

	public override bool GetNextBestWeapon( Player player, BasePlayerItem currentWeapon )
	{
		return currentWeapon is not null && (currentWeapon.Flags & ItemFlags.Exhaustible) == ItemFlags.Exhaustible && NextBestWeapon( player, currentWeapon );
	}

	public static bool NextBestWeapon( Player player, BasePlayerItem currentWeapon )
	{
		BasePlayerItem best = null;
		var bestWeight = -1;

		if ( !currentWeapon.CanHolster() )
			return false;

		for ( var i = 0; i < BasePlayerItem.MaxItemTypes; i++ )
		{
			for ( var check = player.PlayerItems[i]; check is not null; check = check.Next )
			{
				if ( (check.Flags & ItemFlags.NoAutoSwitchTo) == ItemFlags.NoAutoSwitchTo )
					continue;

				if ( check.Weight > -1 && check.Weight == currentWeapon.Weight && check != currentWeapon )
				{
					if ( check.CanDeploy() && player.SwitchWeapon( check ) )
						return true;
				}
				else if ( check.Weight > bestWeight && check != currentWeapon && check.CanDeploy() )
				{
					bestWeight = check.Weight;
					best = check;
				}
			}
		}

		if ( best is null )
			return false;

		player.SwitchWeapon( best );

		return true;
	}

	public override void PlayerGotWeapon( Player player, BasePlayerItem weapon ) { }
	public override RuleCode WeaponShouldRespawn( BasePlayerItem weapon ) => RuleCode.WeaponRespawnNo;
	public override float WeaponRespawnTime( BasePlayerItem weapon ) => -1f;
	public override float WeaponTryRespawn( BasePlayerItem weapon ) => 0f;
	public override Vector3 WeaponRespawnSpot( BasePlayerItem weapon ) => weapon.Origin;
	public override bool CanHaveItem( Player player, Item item ) => true;
	public override void PlayerGotItem( Player player, Item item ) { }
	public override RuleCode ItemShouldRespawn( Item item ) => RuleCode.ItemRespawnNo;
	public override float ItemRespawnTime( Item item ) => -1f;
	public override Vector3 ItemRespawnSpot( Item item ) => item.Origin;
	public override void PlayerGotAmmo( Player player, string name, int count ) { }
	public override RuleCode AmmoShouldRespawn( BasePlayerAmmo ammo ) => RuleCode.AmmoRespawnNo;
	public override float AmmoRespawnTime( BasePlayerAmmo ammo ) => -1f;
	public override Vector3 AmmoRespawnSpot( BasePlayerAmmo ammo ) => ammo.Origin;
	public override RuleCode DeadPlayerWeapons( Player player ) => RuleCode.PlayerDropGunNo;
	public override RuleCode DeadPlayerAmmo( Player player ) => RuleCode.PlayerDropAmmoNo;
}
