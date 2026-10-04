using System;
using GoldSrc;

namespace HalfLife;

public sealed class MultiplayGameMgrHelper : IVoiceGameMgrHelper
{
	public bool CanPlayerHearPlayer( Player listener, Player talker )
	{
		return !HalfLifeDll.Teamplay || HalfLifeDll.GameRules.PlayerRelationship( listener, talker ) == PlayerRelationship.Teammate;
	}
}

public class HalfLifeMultiplay : GameRules
{
	private const float ItemRespawnDelay = 30f;
	private const float WeaponRespawnDelay = 20f;
	private const float AmmoRespawnDelay = 20f;
	private const int MaxIntermissionTime = 120;
	private const int EntityIntolerance = 100;
	private const int MaxRuleBuffer = 1024;
	private const int MaxMapNameLength = 31;
	private const int MaxTokenLength = 1500;
	private const int MaxMotdChunk = 60;
	private const int MaxMotdLength = 1536;
	private const int DirectorCommandEvent = 2;
	private const int DirectorFlagDramatic = 1 << 5;

	private sealed class MapCycleItem
	{
		public string MapName;
		public int MinPlayers;
		public int MaxPlayers;
		public string RuleBuffer = "";
	}

	private static MultiplayGameMgrHelper _gameMgrHelper = new();
	private static int _lastFrags;
	private static int _lastTime;
	private static string _previousMapCycleFile = "";
	private static List<MapCycleItem> _mapCycle = [];
	private static int _nextMapCycleItem;
	private static string _token = "";

	protected float IntermissionEndTime;
	protected bool EndIntermissionButtonHit;

	public HalfLifeMultiplay( Server server ) : base( server )
	{
		HalfLifeDll.VoiceGameMgr.Init( server, _gameMgrHelper, server.MaxClients );

		RefreshSkillData();

		IntermissionEndTime = 0f;
		HalfLifeDll.IntermissionStartTime = 0f;

		if ( Server.IsDedicatedServer )
			return;

		var configFile = Cvar.GetString( "lservercfgfile" );

		if ( string.IsNullOrEmpty( configFile ) )
			return;

		Log.Info( "Executing listen server config file\n" );

		server.ServerCommand( $"exec {configFile}\n" );
	}

	public override bool ClientCommand( Player player, string command )
	{
		return HalfLifeDll.VoiceGameMgr.ClientCommand( player, command ) || base.ClientCommand( player, command );
	}

	public override void RefreshSkillData()
	{
		base.RefreshSkillData();

		var skillData = HalfLifeDll.SkillData;

		skillData.SuitchargerCapacity = 30;
		skillData.PlrDmgCrowbar = 25;
		skillData.PlrDmg9MM = 12;
		skillData.PlrDmg357 = 50;
		skillData.PlrDmgMP5 = 12;
		skillData.PlrDmgM203Grenade = 100;
		skillData.PlrDmgBuckshot = 20;
		skillData.PlrDmgCrossbowClient = 20;
		skillData.PlrDmgRPG = 120;
		skillData.PlrDmgEgonWide = 20;
		skillData.PlrDmgEgonNarrow = 10;
		skillData.PlrDmgHandGrenade = 100;
		skillData.PlrDmgSatchel = 120;
		skillData.PlrDmgTripmine = 150;
		skillData.PlrDmgHornet = 10;
	}

	private static void ClampChatTime()
	{
		var time = (int)Cvar.GetFloat( "mp_chattime" );

		if ( time < 1 )
			Cvar.Set( "mp_chattime", "1" );
		else if ( time > MaxIntermissionTime )
			Cvar.Set( "mp_chattime", MaxIntermissionTime.ToString() );
	}

	public override void Think()
	{
		HalfLifeDll.VoiceGameMgr.Update( Server, Server.Globals.FrameTime );

		var fragsRemaining = 0;

		if ( HalfLifeDll.GameOver )
		{
			ClampChatTime();

			IntermissionEndTime = HalfLifeDll.IntermissionStartTime + Cvar.GetFloat( "mp_chattime" );

			if ( IntermissionEndTime < Server.Globals.Time )
			{
				if ( EndIntermissionButtonHit || HalfLifeDll.IntermissionStartTime + MaxIntermissionTime < Server.Globals.Time )
					ChangeLevel();
			}

			return;
		}

		var timeLimit = Cvar.GetFloat( "mp_timelimit" ) * 60;
		var fragLimit = Cvar.GetFloat( "mp_fraglimit" );
		var timeRemaining = (int)(timeLimit != 0f ? timeLimit - Server.Globals.Time : 0);

		if ( timeLimit != 0 && Server.Globals.Time >= timeLimit )
		{
			GoToIntermission();
			return;
		}

		if ( fragLimit != 0f )
		{
			var bestFrags = 9999;

			for ( var i = 1; i <= Server.MaxClients; i++ )
			{
				var player = Util.PlayerByIndex( i );

				if ( player is not null && player.Frags >= fragLimit )
				{
					GoToIntermission();
					return;
				}

				if ( player is null )
					continue;

				var remain = (int)(fragLimit - player.Frags);

				if ( remain < bestFrags )
					bestFrags = remain;
			}

			fragsRemaining = bestFrags;
		}

		if ( fragsRemaining != _lastFrags )
			Cvar.Set( "mp_fragsleft", fragsRemaining.ToString() );

		if ( Cvar.GetFloat( "mp_timeleft" ) != _lastTime )
			Cvar.Set( "mp_timeleft", timeRemaining.ToString() );

		_lastFrags = fragsRemaining;
		_lastTime = timeRemaining;
	}

	public override bool IsMultiplayer() => true;

	public override bool IsDeathmatch() => true;

	public override bool IsCoOp() => Server.Coop;

	public override bool ShouldSwitchWeapon( Player player, BasePlayerItem weapon )
	{
		if ( !weapon.CanDeploy() )
			return false;

		if ( player.ActiveItem is null )
			return true;

		if ( player.AutoWeaponSwitch == 0 )
			return false;

		if ( player.AutoWeaponSwitch == 2 && (player.ButtonLast & (int)(InButtons.Attack | InButtons.Attack2)) != 0 )
			return false;

		if ( !player.ActiveItem.CanHolster() )
			return false;

		return weapon.Weight > player.ActiveItem.Weight;
	}

	public override bool GetNextBestWeapon( Player player, BasePlayerItem currentWeapon )
	{
		return HalfLifeRules.NextBestWeapon( player, currentWeapon );
	}

	public override bool ClientConnected( Edict entity, string name, string address, ref string rejectReason )
	{
		HalfLifeDll.VoiceGameMgr.ClientConnected( entity.Index );

		return true;
	}

	public override void UpdateGameMode( Player player )
	{
		Server.MessageBegin( MessageDest.One, UserMessages.GameMode, default, player );
		Server.WriteByte( 0 );
		Server.MessageEnd();
	}

	protected static string LogIdentity( Server server, Edict player )
	{
		var team = HalfLifeDll.Teamplay ? Info.ValueForKey( server.GetInfoKeyBuffer( player ), "model" ) : server.GetPlayerUserId( player ).ToString();

		return $"\"{player.NetName}<{server.GetPlayerUserId( player )}><{server.GetPlayerAuthId( player )}><{team}>\"";
	}

	public override void InitHud( Player player )
	{
		Util.ClientPrintAll( UserMessages.HudPrintNotify, $"{(string.IsNullOrEmpty( player.NetName ) ? "unconnected" : player.NetName)} has joined the game\n" );

		Server.LogPrintf( $"{LogIdentity( Server, player )} entered the game\n" );

		UpdateGameMode( player );

		Server.MessageBegin( MessageDest.One, UserMessages.ScoreInfo, default, player );
		Server.WriteByte( player.Index );
		Server.WriteShort( 0 );
		Server.WriteShort( 0 );
		Server.WriteShort( 0 );
		Server.WriteShort( 0 );
		Server.MessageEnd();

		SendMotdToClient( player );

		for ( var i = 1; i <= Server.MaxClients; i++ )
		{
			if ( Util.PlayerByIndex( i ) is not Player other )
				continue;

			Server.MessageBegin( MessageDest.One, UserMessages.ScoreInfo, default, player );
			Server.WriteByte( i );
			Server.WriteShort( (int)other.Frags );
			Server.WriteShort( other.Deaths );
			Server.WriteShort( 0 );
			Server.WriteShort( GetTeamIndex( other.TeamName ) + 1 );
			Server.MessageEnd();
		}

		if ( !HalfLifeDll.GameOver )
			return;

		Server.MessageBegin( MessageDest.One, Server.IntermissionMessage, default, player );
		Server.MessageEnd();
	}

	public override void ClientDisconnected( Edict client )
	{
		if ( client is not Player player )
			return;

		player.FireTargets( "game_playerleave", player, player, UseType.Toggle, 0f );

		Server.LogPrintf( $"{LogIdentity( Server, player )} disconnected\n" );

		player.RemoveAllItems( true );
	}

	public override float PlayerFallDamage( Player player )
	{
		if ( (int)Cvar.GetFloat( "mp_falldamage" ) != 1 )
			return 10f;

		player.LastFallVelocity -= Player.MaxSafeFallSpeed;

		return player.LastFallVelocity * Player.DamageForFallSpeed;
	}

	public override bool PlayerCanTakeDamage( Player player, BaseEntity attacker ) => true;

	public override void PlayerThink( Player player )
	{
		if ( !HalfLifeDll.GameOver )
			return;

		if ( (player.ButtonPressed & (int)(InButtons.Duck | InButtons.Attack | InButtons.Attack2 | InButtons.Use | InButtons.Jump)) != 0 )
			EndIntermissionButtonHit = true;

		player.ButtonPressed = 0;
		player.Button = 0;
		player.ButtonReleased = 0;
	}

	public override void PlayerSpawn( Player player )
	{
		var autoWeaponSwitch = player.AutoWeaponSwitch;

		player.AutoWeaponSwitch = 1;
		player.Weapons |= 1 << Player.WeaponSuit;

		var addDefault = true;

		for ( var equip = Util.FindEntityByClassname( null, "game_player_equip" ); equip is not null; equip = Util.FindEntityByClassname( equip, "game_player_equip" ) )
		{
			equip.Touch( player );
			addDefault = false;
		}

		if ( addDefault )
		{
			player.GiveNamedItem( "weapon_crowbar" );
			player.GiveNamedItem( "weapon_9mmhandgun" );
			player.GiveAmmo( 68, "9mm", Glock.MaxCarry9mm );
		}

		player.AutoWeaponSwitch = autoWeaponSwitch;
	}

	public override bool PlayerCanRespawn( Player player ) => true;

	public override float PlayerSpawnTime( Player player ) => Server.Globals.Time;

	public override bool AllowAutoTargetCrosshair() => Cvar.GetFloat( "mp_autocrosshair" ) != 0f;

	public override int PointsForKill( Player attacker, Player killed ) => 1;

	public override void PlayerKilled( Player victim, Edict killer, Edict inflictor )
	{
		Player playerKiller = null;
		var killerEntity = killer as BaseEntity;

		if ( killerEntity is not null && killerEntity.Classify() == BaseEntity.ClassPlayer )
		{
			playerKiller = (Player)killerEntity;
		}
		else if ( killerEntity is not null && killerEntity.Classify() == BaseEntity.ClassVehicle && ((FuncVehicle)killerEntity).Driver is { } driver )
		{
			playerKiller = driver;
			killerEntity = driver;
			killer = driver;
		}

		DeathNotice( victim, killer, inflictor );

		victim.Deaths += 1;

		victim.FireTargets( "game_playerdie", victim, victim, UseType.Toggle, 0f );

		if ( victim == killer )
		{
			killer.Frags -= 1;
		}
		else if ( killerEntity is { IsPlayer: true } )
		{
			killer.Frags += PointsForKill( playerKiller, victim );

			killerEntity.FireTargets( "game_playerkill", killerEntity, killerEntity, UseType.Toggle, 0f );
		}
		else
		{
			killer.Frags -= 1;
		}

		Server.MessageBegin( MessageDest.All, UserMessages.ScoreInfo );
		Server.WriteByte( victim.Index );
		Server.WriteShort( (int)victim.Frags );
		Server.WriteShort( victim.Deaths );
		Server.WriteShort( 0 );
		Server.WriteShort( GetTeamIndex( victim.TeamName ) + 1 );
		Server.MessageEnd();

		if ( killer is BaseEntity scorer && scorer.Classify() == BaseEntity.ClassPlayer )
		{
			var killerPlayer = (Player)scorer;

			Server.MessageBegin( MessageDest.All, UserMessages.ScoreInfo );
			Server.WriteByte( killerPlayer.Index );
			Server.WriteShort( (int)killerPlayer.Frags );
			Server.WriteShort( killerPlayer.Deaths );
			Server.WriteShort( 0 );
			Server.WriteShort( GetTeamIndex( killerPlayer.TeamName ) + 1 );
			Server.MessageEnd();

			killerPlayer.NextDecalTime = Server.Globals.Time;
		}

		if ( victim.HasNamedPlayerItem( "weapon_satchel" ) )
			SatchelCharge.DeactivateSatchels( victim );
	}

	public override void DeathNotice( Player victim, Edict killer, Edict inflictor )
	{
		var killerWeaponName = "world";
		var killerIndex = 0;

		if ( (killer.EntityFlags & EntityFlags.Client) == EntityFlags.Client )
		{
			killerIndex = killer.Index;

			if ( inflictor is not null )
			{
				if ( inflictor == killer )
				{
					if ( ((Player)killer).ActiveItem is { } activeItem )
						killerWeaponName = activeItem.ItemName;
				}
				else
				{
					killerWeaponName = inflictor.ClassName;
				}
			}
		}
		else
		{
			killerWeaponName = inflictor.ClassName;
		}

		if ( killerWeaponName.StartsWith( "weapon_", StringComparison.Ordinal ) )
			killerWeaponName = killerWeaponName[7..];
		else if ( killerWeaponName.StartsWith( "monster_", StringComparison.Ordinal ) )
			killerWeaponName = killerWeaponName[8..];
		else if ( killerWeaponName.StartsWith( "func_", StringComparison.Ordinal ) )
			killerWeaponName = killerWeaponName[5..];

		Server.MessageBegin( MessageDest.All, UserMessages.DeathMsg );
		Server.WriteByte( killerIndex );
		Server.WriteByte( victim.Index );
		Server.WriteString( killerWeaponName );
		Server.MessageEnd();

		if ( killerWeaponName == "egon" )
			killerWeaponName = "gluon gun";
		else if ( killerWeaponName == "gauss" )
			killerWeaponName = "tau_cannon";

		if ( victim == killer )
			Server.LogPrintf( $"{LogIdentity( Server, victim )} committed suicide with \"{killerWeaponName}\"\n" );
		else if ( (killer.EntityFlags & EntityFlags.Client) == EntityFlags.Client )
			Server.LogPrintf( $"{LogIdentity( Server, killer )} killed {LogIdentity( Server, victim )} with \"{killerWeaponName}\"\n" );
		else
			Server.LogPrintf( $"{LogIdentity( Server, victim )} committed suicide with \"{killerWeaponName}\" (world)\n" );

		Server.MessageBegin( MessageDest.Spectator, Server.DirectorMessage );
		Server.WriteByte( 9 );
		Server.WriteByte( DirectorCommandEvent );
		Server.WriteShort( victim.Index );
		Server.WriteShort( inflictor is not null ? inflictor.Index : killer.Index );
		Server.WriteLong( 7 | DirectorFlagDramatic );
		Server.MessageEnd();
	}

	public override void PlayerGotWeapon( Player player, BasePlayerItem weapon ) { }

	public override float WeaponRespawnTime( BasePlayerItem weapon )
	{
		if ( Cvar.GetFloat( "mp_weaponstay" ) > 0f && (weapon.Flags & ItemFlags.LimitInWorld) != ItemFlags.LimitInWorld )
			return Server.Globals.Time + 0f;

		return Server.Globals.Time + WeaponRespawnDelay;
	}

	public override float WeaponTryRespawn( BasePlayerItem weapon )
	{
		if ( weapon is not null && weapon.Id != 0 && (weapon.Flags & ItemFlags.LimitInWorld) == ItemFlags.LimitInWorld )
		{
			if ( Server.NumberOfEntities() < Server.MaxEdicts - EntityIntolerance )
				return 0f;

			return WeaponRespawnTime( weapon );
		}

		return 0f;
	}

	public override Vector3 WeaponRespawnSpot( BasePlayerItem weapon ) => weapon.Origin;

	public override RuleCode WeaponShouldRespawn( BasePlayerItem weapon )
	{
		return (weapon.SpawnFlags & BasePlayerItem.SpawnFlagNoRespawn) != 0 ? RuleCode.WeaponRespawnNo : RuleCode.WeaponRespawnYes;
	}

	public override bool CanHavePlayerItem( Player player, BasePlayerItem weapon )
	{
		if ( Cvar.GetFloat( "mp_weaponstay" ) > 0f )
		{
			if ( (weapon.Flags & ItemFlags.LimitInWorld) == ItemFlags.LimitInWorld )
				return base.CanHavePlayerItem( player, weapon );

			for ( var i = 0; i < BasePlayerItem.MaxItemTypes; i++ )
			{
				for ( var item = player.PlayerItems[i]; item is not null; item = item.Next )
				{
					if ( item.Id == weapon.Id )
						return false;
				}
			}
		}

		return base.CanHavePlayerItem( player, weapon );
	}

	public override bool CanHaveItem( Player player, Item item ) => true;

	public override void PlayerGotItem( Player player, Item item ) { }

	public override RuleCode ItemShouldRespawn( Item item )
	{
		return (item.SpawnFlags & BasePlayerItem.SpawnFlagNoRespawn) != 0 ? RuleCode.ItemRespawnNo : RuleCode.ItemRespawnYes;
	}

	public override float ItemRespawnTime( Item item ) => Server.Globals.Time + ItemRespawnDelay;

	public override Vector3 ItemRespawnSpot( Item item ) => item.Origin;

	public override void PlayerGotAmmo( Player player, string name, int count ) { }

	public override bool IsAllowedToSpawn( BaseEntity entity ) => true;

	public override RuleCode AmmoShouldRespawn( BasePlayerAmmo ammo )
	{
		return (ammo.SpawnFlags & BasePlayerItem.SpawnFlagNoRespawn) != 0 ? RuleCode.AmmoRespawnNo : RuleCode.AmmoRespawnYes;
	}

	public override float AmmoRespawnTime( BasePlayerAmmo ammo ) => Server.Globals.Time + AmmoRespawnDelay;

	public override Vector3 AmmoRespawnSpot( BasePlayerAmmo ammo ) => ammo.Origin;

	public override float HealthChargerRechargeTime() => 60f;

	public override float HevChargerRechargeTime() => 30f;

	public override RuleCode DeadPlayerWeapons( Player player ) => RuleCode.PlayerDropGunActive;

	public override RuleCode DeadPlayerAmmo( Player player ) => RuleCode.PlayerDropAmmoActive;

	public override Edict GetPlayerSpawnSpot( Player player )
	{
		var spot = base.GetPlayerSpawnSpot( player );

		if ( IsMultiplayer() && !string.IsNullOrEmpty( spot.Target ) )
			player.FireTargets( spot.Target, player, player, UseType.Toggle, 0f );

		return spot;
	}

	public override string GetTeamId( BaseEntity entity ) => "";

	public override PlayerRelationship PlayerRelationship( BaseEntity player, BaseEntity target ) => HalfLife.PlayerRelationship.NotTeammate;

	public override bool PlayFootstepSounds( Player player, float volume )
	{
		if ( Cvar.GetFloat( "mp_footsteps" ) == 0f )
			return false;

		return player.IsOnLadder || MathLib.Length2D( player.Velocity ) > 220f;
	}

	public override bool AllowFlashlight() => Cvar.GetFloat( "mp_flashlight" ) != 0f;

	public override bool AllowMonsters() => Cvar.GetFloat( "mp_allowmonsters" ) != 0f;

	public override void EndMultiplayerGame() => GoToIntermission();

	protected void GoToIntermission()
	{
		if ( HalfLifeDll.GameOver )
			return;

		Server.MessageBegin( MessageDest.All, Server.IntermissionMessage );
		Server.MessageEnd();

		ClampChatTime();

		IntermissionEndTime = Server.Globals.Time + (int)Cvar.GetFloat( "mp_chattime" );
		HalfLifeDll.IntermissionStartTime = Server.Globals.Time;

		HalfLifeDll.GameOver = true;
		EndIntermissionButtonHit = false;
	}

	private static int Parse( string data, int position )
	{
		_token = "";

		if ( position < 0 )
			return -1;

		while ( true )
		{
			while ( position >= data.Length || data[position] <= ' ' )
			{
				if ( position >= data.Length || data[position] == 0 )
					return -1;

				position++;
			}

			if ( data[position] != '/' || position + 1 >= data.Length || data[position + 1] != '/' )
				break;

			while ( position < data.Length && data[position] != '\n' )
				position++;
		}

		var c = data[position];

		if ( c == '"' )
		{
			position++;

			var start = position;

			while ( position < data.Length && data[position] != '"' )
				position++;

			_token = data[start..position];

			return position + 1;
		}

		if ( c is '{' or '}' or ')' or '(' or '\'' or ',' )
		{
			_token = c.ToString();

			return position + 1;
		}

		var begin = position;

		do
		{
			position++;

			if ( position >= data.Length )
				break;

			c = data[position];
		}
		while ( c is not ('{' or '}' or ')' or '(' or '\'' or ',') && c > 32 );

		_token = data[begin..position];

		return position;
	}

	private static bool TokenWaiting( string buffer, int position )
	{
		for ( ; position >= 0 && position < buffer.Length && buffer[position] != '\n'; position++ )
		{
			if ( !char.IsWhiteSpace( buffer[position] ) || char.IsLetterOrDigit( buffer[position] ) )
				return true;
		}

		return false;
	}

	private bool ReloadMapCycleFile( string fileName )
	{
		var fileList = Server.LoadMountText( fileName );

		if ( !string.IsNullOrEmpty( fileList ) )
		{
			var position = 0;

			while ( true )
			{
				var hasBuffer = false;
				var buffer = "";

				position = Parse( fileList, position );

				if ( _token.Length <= 0 )
					break;

				var map = _token.Length > MaxMapNameLength ? _token[..MaxMapNameLength] : _token;

				if ( TokenWaiting( fileList, position ) )
				{
					position = Parse( fileList, position );

					if ( _token.Length > 0 )
					{
						hasBuffer = true;
						buffer = _token.Length > MaxRuleBuffer - 1 ? _token[..(MaxRuleBuffer - 1)] : _token;
					}
				}

				if ( !Server.IsMapValid( map ) )
				{
					Log.Info( $"Skipping {map} from mapcycle, not a valid map\n" );
					continue;
				}

				var item = new MapCycleItem { MapName = map };

				if ( hasBuffer )
				{
					var value = Info.ValueForKey( buffer, "minplayers" );

					if ( value.Length != 0 )
						item.MinPlayers = Math.Min( Math.Max( Util.Atoi( value ), 0 ), Server.MaxClients );

					value = Info.ValueForKey( buffer, "maxplayers" );

					if ( value.Length != 0 )
						item.MaxPlayers = Math.Min( Math.Max( Util.Atoi( value ), 0 ), Server.MaxClients );

					Info.RemoveKey( ref buffer, "minplayers" );
					Info.RemoveKey( ref buffer, "maxplayers" );

					item.RuleBuffer = buffer;
				}

				_mapCycle.Add( item );
			}
		}

		_nextMapCycleItem = 0;

		return _mapCycle.Count != 0;
	}

	private int CountPlayers()
	{
		var count = 0;

		for ( var i = 1; i <= Server.MaxClients; i++ )
		{
			if ( Util.PlayerByIndex( i ) is not null )
				count++;
		}

		return count;
	}

	private static string ExtractCommandString( string rules )
	{
		var commands = "";
		var position = rules.StartsWith( '\\' ) ? 1 : 0;

		while ( true )
		{
			var start = position;

			while ( position < rules.Length && rules[position] != '\\' )
				position++;

			if ( position >= rules.Length )
				return commands;

			var key = rules[start..position];

			position++;
			start = position;

			while ( position < rules.Length && rules[position] != '\\' )
				position++;

			var value = rules[start..position];

			commands += key;

			if ( value.Length > 0 )
				commands += $" {value}";

			commands += "\n";

			if ( position >= rules.Length )
				return commands;

			position++;
		}
	}

	protected void ChangeLevel()
	{
		var nextMap = "";
		var firstMapInList = "hldm1";
		var commands = "";
		var rules = "";
		var minPlayers = 0;
		var maxPlayers = 0;
		var doCycle = true;
		var mapCycleFile = Cvar.GetString( "mapcyclefile" );
		var currentPlayers = CountPlayers();

		if ( !mapCycleFile.Equals( _previousMapCycleFile, StringComparison.OrdinalIgnoreCase ) )
		{
			_previousMapCycleFile = mapCycleFile;

			_mapCycle.Clear();

			if ( !ReloadMapCycleFile( mapCycleFile ) )
			{
				Log.Info( $"Unable to load map cycle file {mapCycleFile}\n" );
				doCycle = false;
			}
		}

		if ( doCycle && _mapCycle.Count != 0 )
		{
			var found = false;

			nextMap = Server.Globals.MapName;
			firstMapInList = Server.Globals.MapName;

			var index = _nextMapCycleItem;

			for ( ; (index + 1) % _mapCycle.Count != _nextMapCycleItem; index = (index + 1) % _mapCycle.Count )
			{
				var keepLooking = false;
				var candidate = _mapCycle[index];

				if ( candidate.MinPlayers != 0 )
				{
					if ( currentPlayers >= candidate.MinPlayers )
					{
						found = true;
						minPlayers = candidate.MinPlayers;
					}
					else
					{
						keepLooking = true;
					}
				}

				if ( candidate.MaxPlayers != 0 )
				{
					if ( currentPlayers <= candidate.MaxPlayers )
					{
						found = true;
						maxPlayers = candidate.MaxPlayers;
					}
					else
					{
						keepLooking = true;
					}
				}

				if ( keepLooking )
					continue;

				found = true;
				break;
			}

			if ( !found )
				index = _nextMapCycleItem;

			var item = _mapCycle[index];

			_nextMapCycleItem = (index + 1) % _mapCycle.Count;

			nextMap = item.MapName;
			commands = ExtractCommandString( item.RuleBuffer );
			rules = item.RuleBuffer;
		}

		if ( !Server.IsMapValid( nextMap ) )
			nextMap = firstMapInList;

		HalfLifeDll.GameOver = true;

		Log.Info( $"CHANGE LEVEL: {nextMap}\n" );

		if ( minPlayers != 0 || maxPlayers != 0 )
			Log.Info( $"PLAYER COUNT:  min {minPlayers} max {maxPlayers} current {currentPlayers}\n" );

		if ( rules.Length > 0 )
			Log.Info( $"RULES:  {rules}\n" );

		Server.ChangeLevel( nextMap, null );

		if ( commands.Length > 0 )
			Server.ServerCommand( commands );
	}

	private void SendMotdToClient( Edict client )
	{
		var fileList = Server.LoadMountText( Cvar.GetString( "motdfile" ) );
		var charCount = 0;

		Server.MessageBegin( MessageDest.One, UserMessages.ServerName, default, client );
		Server.WriteString( Server.HostName );
		Server.MessageEnd();

		var position = 0;

		while ( fileList is not null && position < fileList.Length && charCount < MaxMotdLength )
		{
			var chunk = fileList.Length - position < MaxMotdChunk ? fileList[position..] : fileList.Substring( position, MaxMotdChunk );

			charCount += chunk.Length;

			if ( charCount < MaxMotdLength )
				position = charCount;
			else
				position = fileList.Length;

			Server.MessageBegin( MessageDest.One, UserMessages.Motd, default, client );
			Server.WriteByte( position < fileList.Length ? 0 : 1 );
			Server.WriteString( chunk );
			Server.MessageEnd();
		}
	}

	public override void ClientUserInfoChanged( Player player, string info )
	{
		player.SetPrefsFromUserinfo( Info.ValueForKey( info, "cl_autowepswitch" ) );
	}
}
