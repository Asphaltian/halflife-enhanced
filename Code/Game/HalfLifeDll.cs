using System;
using GoldSrc;

namespace HalfLife;

public sealed partial class HalfLifeDll : GameDll
{
	public static GlobalState GlobalState { get; } = new();
	public static SkillData SkillData { get; } = new();
	public static GameRules GameRules { get; set; }
	public static int SkillLevel { get; set; }
	public static bool Teamplay { get; set; }

	public const int LanguageEnglish = 0;
	public const int LanguageGerman = 1;
	public const int LanguageFrench = 2;
	public const int LanguageBritish = 3;

	public static int Language { get; set; }
	public static bool DisplayTitle { get; set; }
	public static bool GameOver { get; set; }

	public static VoiceGameMgr VoiceGameMgr { get; private set; } = new();

	public static float IntermissionStartTime { get; set; }
	public static BaseEntity LastInflictor { get; set; }
	public static BaseEntity LastSpawn { get; set; }
	public static bool InitHud { get; set; } = true;
	public static bool EvilImpulse101 { get; set; }
	public static bool PrecacheGrunt { get; set; }
	public static int ModelIndexPlayer { get; set; }

	private static Edict _bodyQueueHead;
	private static bool _serverActive;

	public static Edict BodyQueueHead => _bodyQueueHead;
	public static Graph WorldGraph { get; } = new();
	public static bool TouchDisabled { get; set; }

	public static void ClientPrecache( Server server )
	{
		server.PrecacheSound( "player/sprayer.wav" );
		server.PrecacheSound( "player/pl_fallpain2.wav" );
		server.PrecacheSound( "player/pl_fallpain3.wav" );
		server.PrecacheSound( "player/pl_step1.wav" );
		server.PrecacheSound( "player/pl_step2.wav" );
		server.PrecacheSound( "player/pl_step3.wav" );
		server.PrecacheSound( "player/pl_step4.wav" );
		server.PrecacheSound( "common/npc_step1.wav" );
		server.PrecacheSound( "common/npc_step2.wav" );
		server.PrecacheSound( "common/npc_step3.wav" );
		server.PrecacheSound( "common/npc_step4.wav" );
		server.PrecacheSound( "player/pl_metal1.wav" );
		server.PrecacheSound( "player/pl_metal2.wav" );
		server.PrecacheSound( "player/pl_metal3.wav" );
		server.PrecacheSound( "player/pl_metal4.wav" );
		server.PrecacheSound( "player/pl_dirt1.wav" );
		server.PrecacheSound( "player/pl_dirt2.wav" );
		server.PrecacheSound( "player/pl_dirt3.wav" );
		server.PrecacheSound( "player/pl_dirt4.wav" );
		server.PrecacheSound( "player/pl_duct1.wav" );
		server.PrecacheSound( "player/pl_duct2.wav" );
		server.PrecacheSound( "player/pl_duct3.wav" );
		server.PrecacheSound( "player/pl_duct4.wav" );
		server.PrecacheSound( "player/pl_grate1.wav" );
		server.PrecacheSound( "player/pl_grate2.wav" );
		server.PrecacheSound( "player/pl_grate3.wav" );
		server.PrecacheSound( "player/pl_grate4.wav" );
		server.PrecacheSound( "player/pl_slosh1.wav" );
		server.PrecacheSound( "player/pl_slosh2.wav" );
		server.PrecacheSound( "player/pl_slosh3.wav" );
		server.PrecacheSound( "player/pl_slosh4.wav" );
		server.PrecacheSound( "player/pl_tile1.wav" );
		server.PrecacheSound( "player/pl_tile2.wav" );
		server.PrecacheSound( "player/pl_tile3.wav" );
		server.PrecacheSound( "player/pl_tile4.wav" );
		server.PrecacheSound( "player/pl_tile5.wav" );
		server.PrecacheSound( "player/pl_swim1.wav" );
		server.PrecacheSound( "player/pl_swim2.wav" );
		server.PrecacheSound( "player/pl_swim3.wav" );
		server.PrecacheSound( "player/pl_swim4.wav" );
		server.PrecacheSound( "player/pl_ladder1.wav" );
		server.PrecacheSound( "player/pl_ladder2.wav" );
		server.PrecacheSound( "player/pl_ladder3.wav" );
		server.PrecacheSound( "player/pl_ladder4.wav" );
		server.PrecacheSound( "player/pl_wade1.wav" );
		server.PrecacheSound( "player/pl_wade2.wav" );
		server.PrecacheSound( "player/pl_wade3.wav" );
		server.PrecacheSound( "player/pl_wade4.wav" );
		server.PrecacheSound( "debris/wood1.wav" );
		server.PrecacheSound( "debris/wood2.wav" );
		server.PrecacheSound( "debris/wood3.wav" );
		server.PrecacheSound( "plats/train_use1.wav" );
		server.PrecacheSound( "plats/vehicle_ignition.wav" );
		server.PrecacheSound( "buttons/spark5.wav" );
		server.PrecacheSound( "buttons/spark6.wav" );
		server.PrecacheSound( "debris/glass1.wav" );
		server.PrecacheSound( "debris/glass2.wav" );
		server.PrecacheSound( "debris/glass3.wav" );
		server.PrecacheSound( Player.SoundFlashlightOn );
		server.PrecacheSound( Player.SoundFlashlightOff );
		server.PrecacheSound( "common/bodysplat.wav" );
		server.PrecacheSound( "player/pl_pain2.wav" );
		server.PrecacheSound( "player/pl_pain4.wav" );
		server.PrecacheSound( "player/pl_pain5.wav" );
		server.PrecacheSound( "player/pl_pain6.wav" );
		server.PrecacheSound( "player/pl_pain7.wav" );
		server.PrecacheModel( "models/player.mdl" );
		server.PrecacheSound( "common/wpn_hudoff.wav" );
		server.PrecacheSound( "common/wpn_hudon.wav" );
		server.PrecacheSound( "common/wpn_moveselect.wav" );
		server.PrecacheSound( "common/wpn_select.wav" );
		server.PrecacheSound( "common/wpn_denyselect.wav" );
		server.PrecacheSound( "player/geiger6.wav" );
		server.PrecacheSound( "player/geiger5.wav" );
		server.PrecacheSound( "player/geiger4.wav" );
		server.PrecacheSound( "player/geiger3.wav" );
		server.PrecacheSound( "player/geiger2.wav" );
		server.PrecacheSound( "player/geiger1.wav" );

		if ( PrecacheGrunt )
			Util.PrecacheOther( "monster_human_grunt" );
	}

	public static void InitBodyQue( Server server )
	{
		_bodyQueueHead = server.CreateNamedEntity( "bodyque" );

		var body = _bodyQueueHead;

		for ( var i = 0; i < 3; i++ )
		{
			body.Owner = server.CreateNamedEntity( "bodyque" );
			body = body.Owner;
		}

		body.Owner = _bodyQueueHead;
	}

	public static void CopyToBodyQue( Edict entity )
	{
		if ( (entity.Effects & Effects.NoDraw) == Effects.NoDraw || entity.ModelIndex == 0 )
			return;

		var head = _bodyQueueHead;

		head.Angles = entity.Angles;
		head.ModelName = entity.ModelName;
		head.ModelIndex = entity.ModelIndex;
		head.Frame = entity.Frame;
		head.Colormap = entity.Colormap;
		head.MoveType = MoveType.Toss;
		head.Velocity = entity.Velocity;
		head.EntityFlags = 0;
		head.DeadFlag = entity.DeadFlag;
		head.RenderFx = RenderFx.DeadPlayer;
		head.RenderAmt = entity.Index;
		head.Effects = entity.Effects | Effects.NoInterp;
		head.Sequence = entity.Sequence;
		head.AnimTime = entity.AnimTime;

		entity.Server.SetOrigin( head, entity.Origin );
		entity.Server.SetSize( head, entity.Mins, entity.Maxs );

		_bodyQueueHead = head.Owner;
	}

	public static void Respawn( Player player, bool copyCorpse )
	{
		var server = player.Server;

		if ( server.Coop || server.Deathmatch )
		{
			if ( copyCorpse )
				CopyToBodyQue( player );

			player.Spawn();
		}
		else
		{
			server.ServerCommand( "reload\n" );
		}
	}

	private readonly PlayerMovement _movement = new();

	public override void Init()
	{
		Cmd.AddText( "exec skill.cfg\n" );
	}

	public override void RegisterEncoders() => Encoders.Register();

	public override bool GetWeaponData( Edict player, WeaponData[] info )
	{
		Array.Clear( info, 0, BasePlayerItem.MaxWeapons );

		if ( player is not Player pl )
			return true;

		for ( var i = 0; i < BasePlayerItem.MaxItemTypes; i++ )
		{
			for ( var playerItem = pl.PlayerItems[i]; playerItem is not null; playerItem = playerItem.Next )
			{
				var gun = playerItem.GetWeaponPtr();

				if ( gun is null || !gun.UseDecrement )
					continue;

				gun.GetItemInfo( out var itemInfo );

				if ( itemInfo.Id < 0 || itemInfo.Id >= BasePlayerItem.MaxWeapons )
					continue;

				ref var item = ref info[itemInfo.Id];

				item.Id = itemInfo.Id;
				item.Clip = gun.Clip;

				item.TimeWeaponIdle = Math.Max( gun.TimeWeaponIdle, -0.001f );
				item.NextPrimaryAttack = Math.Max( gun.NextPrimaryAttack, -0.001f );
				item.NextSecondaryAttack = Math.Max( gun.NextSecondaryAttack, -0.001f );
				item.InReload = gun.InReload ? 1 : 0;
				item.InSpecialReload = gun.InSpecialReload;
				item.FUser1 = Math.Max( gun.FUser1, -0.001f );
				item.FUser2 = gun.StartThrow;
				item.FUser3 = gun.ReleaseThrow;
				item.IUser1 = gun.ChargeReady;
				item.IUser2 = gun.InAttack;
				item.IUser3 = gun.FireState;
			}
		}

		return true;
	}

	public override void UpdateClientData( Edict entity, bool sendWeapons, ref LocalClientData data )
	{
		if ( entity is not BaseEntity player )
			return;

		var pl = player as Player;
		Player original = null;

		if ( pl is not null && pl.IUser1 == Player.ObserverInEye && pl.ObserverTarget is { } target )
		{
			original = pl;
			player = target;
			pl = target as Player;
		}

		data.Flags = (int)player.EntityFlags;
		data.Health = player.Health;

		data.ViewModel = string.IsNullOrEmpty( player.ViewModel ) ? 0 : player.Server.ModelIndex( player.ViewModel );

		data.WaterLevel = player.WaterLevel;
		data.WaterType = (int)player.WaterType;
		data.Weapons = player.Weapons;

		data.Origin = player.Origin;
		data.Velocity = player.Velocity;
		data.ViewOffset = player.ViewOffset;
		data.PunchAngle = player.PunchAngle;

		data.InDuck = player.InDuck ? 1 : 0;
		data.TimeStepSound = player.TimeStepSound;
		data.DuckTime = player.DuckTime;
		data.SwimTime = player.SwimTime;
		data.WaterJumpTime = (int)player.TeleportTime;

		data.PhysInfo = player.Server.GetPhysicsInfoString( entity );

		data.MaxSpeed = player.MaxSpeed;
		data.Fov = player.Fov;
		data.WeaponAnim = player.WeaponAnim;

		data.PushMsec = player.PushMsec;

		if ( original is not null )
		{
			data.IUser1 = original.IUser1;
			data.IUser2 = original.IUser2;
		}
		else
		{
			data.IUser1 = player.IUser1;
			data.IUser2 = player.IUser2;
		}

		if ( !sendWeapons || pl is null )
			return;

		data.NextAttack = pl.NextAttack;
		data.FUser2 = pl.NextAmmoBurn;
		data.FUser3 = pl.AmmoStartCharge;
		data.VUser1 = new Vector3( pl.Ammo9mm, pl.Ammo357, pl.AmmoArGrenades );
		data.AmmoNails = pl.AmmoBolts;
		data.AmmoShells = pl.AmmoBuckshot;
		data.AmmoRockets = pl.AmmoRockets;
		data.AmmoCells = pl.AmmoUranium;
		data.VUser2.x = pl.AmmoHornets;

		if ( pl.ActiveItem?.GetWeaponPtr() is not { UseDecrement: true } gun )
			return;

		gun.GetItemInfo( out var itemInfo );

		data.Id = itemInfo.Id;

		data.VUser3.z = gun.SecondaryAmmoType;
		data.VUser4 = new Vector3( gun.PrimaryAmmoType, pl.AmmoSlot( gun.PrimaryAmmoType ), pl.AmmoSlot( gun.SecondaryAmmoType ) );

		if ( pl.ActiveItem.Id == Rpg.WeaponId )
		{
			var rpg = (Rpg)pl.ActiveItem;

			data.VUser2.y = rpg.SpotActive;
			data.VUser2.z = rpg.ActiveRockets;
		}
	}

	public override void CmdStart( Edict player, UserCmd cmd, int randomSeed )
	{
		if ( player is Player entity )
			entity.RandomSeed = randomSeed;
	}

	public override bool AllowLagCompensation() => true;

	public override string GetGameDescription() => GameRules is not null ? GameRules.GetGameDescription() : "Half-Life";

	[ConCmd( "say" )]
	private static void SayCommand( params string[] arguments ) => Cmd.ForwardToServer( "say", arguments );

	[ConCmd( "say_team" )]
	private static void SayTeamCommand( params string[] arguments ) => Cmd.ForwardToServer( "say_team", arguments );

	[ConCmd( "fullupdate" )]
	private static void FullUpdateCommand( params string[] arguments ) => Cmd.ForwardToServer( "fullupdate", arguments );

	[ConCmd( "give" )]
	private static void GiveCommand( params string[] arguments ) => Cmd.ForwardToServer( "give", arguments );

	[ConCmd( "drop" )]
	private static void DropCommand( params string[] arguments ) => Cmd.ForwardToServer( "drop", arguments );

	[ConCmd( "fov" )]
	private static void FovCommand( params string[] arguments ) => Cmd.ForwardToServer( "fov", arguments );

	[ConCmd( "use" )]
	private static void UseCommand( params string[] arguments ) => Cmd.ForwardToServer( "use", arguments );

	[ConCmd( "lastinv" )]
	private static void LastInventoryCommand( params string[] arguments ) => Cmd.ForwardToServer( "lastinv", arguments );

	[ConCmd( "spectate" )]
	private static void SpectateCommand( params string[] arguments ) => Cmd.ForwardToServer( "spectate", arguments );

	[ConCmd( "specmode" )]
	private static void SpectatorModeCommand( params string[] arguments ) => Cmd.ForwardToServer( "specmode", arguments );

	[ConCmd( "closemenus" )]
	private static void CloseMenusCommand( params string[] arguments ) => Cmd.ForwardToServer( "closemenus", arguments );

	[ConCmd( "follownext" )]
	private static void FollowNextCommand( params string[] arguments ) => Cmd.ForwardToServer( "follownext", arguments );

	public override void ClientCommand( Edict client )
	{
		if ( client is not Player player )
			return;

		var command = Cmd.Argv( 0 );

		switch ( command )
		{
			case "say":
				HostSay( player, false );
				return;

			case "say_team":
				HostSay( player, true );
				return;

			case "fullupdate":
				player.ForceClientDllUpdate();
				return;

			case "give":
				if ( Sandbox.Game.CheatsEnabled )
					player.GiveNamedItem( Server.AllocString( Cmd.Argv( 1 ) ) );

				return;

			case "drop":
				player.DropPlayerItem( Cmd.Argv( 1 ) );
				return;

			case "fov":
				if ( Sandbox.Game.CheatsEnabled && Cmd.Argc > 1 )
					player.FieldOfViewSetting = Util.Atoi( Cmd.Argv( 1 ) );
				else
					player.Server.ClientPrintf( player, PrintType.Console, $"\"fov\" is \"{player.FieldOfViewSetting}\"\n" );

				return;

			case "use":
				player.SelectItem( Cmd.Argv( 1 ) );
				return;

			case "lastinv":
				player.SelectLastItem();
				return;

			case "spectate":
				if ( (player.EntityFlags & EntityFlags.Proxy) == EntityFlags.Proxy || Cvar.GetFloat( "allow_spectators" ) != 0f )
				{
					var spot = GameRules.GetPlayerSpawnSpot( player );

					player.StartObserver( player.Origin, spot.Angles );

					Util.ClientPrintAll( UserMessages.HudPrintNotify, $"{(string.IsNullOrEmpty( player.NetName ) ? "unconnected" : player.NetName)} switched to spectator mode\n" );
				}
				else
				{
					Util.ClientPrint( player, UserMessages.HudPrintConsole, "Spectator mode is disabled.\n" );
				}

				return;

			case "specmode":
				if ( player.IsObserver )
					player.ObserverSetMode( Util.Atoi( Cmd.Argv( 1 ) ) );

				return;

			case "closemenus":
				return;

			case "follownext":
				if ( player.IsObserver )
					player.ObserverFindNextPlayer( Util.Atoi( Cmd.Argv( 1 ) ) != 0 );

				return;
		}

		if ( command.StartsWith( "weapon_", System.StringComparison.Ordinal ) )
		{
			player.SelectItem( command );
			return;
		}

		if ( GameRules.ClientCommand( player, command ) )
			return;

		if ( command.Length > 127 )
			command = command[..127];

		Util.ClientPrint( player, UserMessages.HudPrintConsole, $"Unknown command: {command.Replace( '%', ' ' )}\n" );
	}

	public override bool ClientConnect( Edict entity, string name, string address, ref string rejectReason )
	{
		return GameRules.ClientConnected( entity, name, address, ref rejectReason );
	}

	public override void ClientDisconnect( Edict entity )
	{
		if ( GameOver )
			return;

		var text = entity.NetName is not null ? $"- {entity.NetName} has left the game\n" : "";

		if ( text.Length > 255 )
			text = text[..255];

		var server = entity.Server;

		server.MessageBegin( MessageDest.All, UserMessages.SayText );
		server.WriteByte( entity.Index );
		server.WriteString( text );
		server.MessageEnd();

		SoundEnt.SoundPointerForIndex( SoundEnt.ClientSoundIndex( entity ) )?.Reset();

		entity.DamageMode = BaseEntity.DamageNo;
		entity.Solid = Solid.Not;

		server.SetOrigin( entity, entity.Origin );

		GameRules.ClientDisconnected( entity );
	}

	public override void ClientKill( Edict client )
	{
		if ( client is not Player player )
			return;

		var time = player.Server.Globals.Time;

		if ( player.NextSuicideTime > time )
			return;

		player.NextSuicideTime = time + 1f;
		player.Health = 0f;
		player.Killed( player, BaseEntity.GibNever );
	}

	public override void ClientPutInServer( Edict client )
	{
		if ( client is not Player player )
			return;

		player.SetCustomDecalFrames( -1 );
		player.Spawn();

		player.Effects |= Effects.NoInterp;
		player.IUser1 = 0;
		player.IUser2 = 0;
	}

	public override void ClientUserInfoChanged( Edict client, ref string info )
	{
		if ( client is not Player player )
			return;

		if ( !string.IsNullOrEmpty( player.NetName ) && player.NetName != Info.ValueForKey( info, "name" ) )
		{
			var name = Info.ValueForKey( info, "name" ).Replace( '%', ' ' );

			player.Server.SetClientKeyValue( player.Index, ref info, "name", name );

			if ( player.Server.MaxClients > 1 )
				Util.SayTextAll( $"* {player.NetName} changed name to {Info.ValueForKey( info, "name" )}\n", player );
		}

		GameRules.ClientUserInfoChanged( player, info );
	}

	public override void Move( PlayerMove pmove, bool server )
	{
		_movement.Move( pmove, server );
	}

	public override void ParmsChangeLevel( Server server )
	{
		if ( server.Globals.SaveData is { } saveData )
			ChangeLevel.ChangeList( server, saveData.LevelList, SaveRestoreData.MaxLevelConnections );
	}

	public override void SaveGlobalState( List<SaveBlock> blocks )
	{
		GlobalState.Save( new Save( null, blocks ) );
	}

	public override void RestoreGlobalState( List<SaveBlock> blocks )
	{
		GlobalState.Restore( new Restore( null, blocks ) );
	}

	public override void ResetGlobalState()
	{
		GlobalState.ClearStates();
	}

	public override void StartFrame( Server server )
	{
		GameRules?.Think();

		server.Globals.Teamplay = Cvar.GetFloat( "mp_teamplay" );
	}

	public override byte[] SetupVisibility( Edict viewEntity, Edict client )
	{
		var view = viewEntity ?? client;

		if ( (client.EntityFlags & EntityFlags.Proxy) == EntityFlags.Proxy )
			return null;

		var origin = view.Origin + view.ViewOffset;

		if ( (view.EntityFlags & EntityFlags.Ducking) == EntityFlags.Ducking )
			origin += Player.HullMin - Player.DuckHullMin;

		return client.Server.SetFatPvs( origin );
	}

	public override bool AddToFullPack( ref EntityState state, int index, Edict entity, Edict host, int hostFlags, bool player, byte[] set )
	{
		if ( (entity.Effects & Effects.NoDraw) == Effects.NoDraw && entity != host )
			return false;

		if ( entity.ModelIndex == 0 )
			return false;

		if ( (entity.EntityFlags & EntityFlags.Spectator) == EntityFlags.Spectator && entity != host )
			return false;

		if ( entity != host && entity.Server.CheckVisibility( entity, set ) == 0 )
			return false;

		if ( (entity.EntityFlags & EntityFlags.SkipLocalHost) == EntityFlags.SkipLocalHost && (hostFlags & 1) != 0 && entity.Owner == host )
			return false;

		state = default;

		state.Number = index;
		state.EntityType = (entity.EntityFlags & EntityFlags.CustomEntity) == EntityFlags.CustomEntity ? EntityState.EntityBeam : EntityState.EntityNormal;

		state.AnimTime = (float)((int)(1000.0 * entity.AnimTime) / 1000.0);

		state.Origin = entity.Origin;
		state.Angles = entity.Angles;
		state.Mins = entity.Mins;
		state.Maxs = entity.Maxs;

		state.StartPos = entity.ParametricStartPos;
		state.EndPos = entity.ParametricEndPos;

		state.ImpactTime = entity.ImpactTime;
		state.StartTime = entity.ParametricStartTime;

		state.ModelIndex = entity.ModelIndex;

		state.Frame = entity.Frame;

		state.Skin = (short)entity.Skin;
		state.Effects = (int)entity.Effects;

		if ( !player && entity.AnimTime != 0f && entity.Velocity.x == 0f && entity.Velocity.y == 0f && entity.Velocity.z == 0f )
			state.EFlags |= EntityState.EFlagSlerp;

		state.Scale = entity.Scale;
		state.Solid = (short)entity.Solid;
		state.Colormap = entity.Colormap;

		state.MoveType = (int)entity.MoveType;
		state.Sequence = entity.Sequence;
		state.FrameRate = entity.FrameRate;
		state.Body = entity.Body;

		state.Controller0 = entity.Controller[0];
		state.Controller1 = entity.Controller[1];
		state.Controller2 = entity.Controller[2];
		state.Controller3 = entity.Controller[3];

		state.Blending0 = entity.Blending[0];
		state.Blending1 = entity.Blending[1];

		state.RenderMode = (int)entity.RenderMode;
		state.RenderAmt = (int)entity.RenderAmt;
		state.RenderFx = (int)entity.RenderFx;
		state.RenderColorR = (byte)(int)entity.RenderColor.x;
		state.RenderColorG = (byte)(int)entity.RenderColor.y;
		state.RenderColorB = (byte)(int)entity.RenderColor.z;

		state.AimEnt = entity.AimEntity?.Index ?? 0;

		state.Owner = 0;

		if ( entity.Owner is { } owner && owner.Index >= 1 && owner.Index <= entity.Server.MaxClients )
			state.Owner = owner.Index;

		if ( !player )
			state.PlayerClass = entity.PlayerClass;

		if ( player )
		{
			state.BaseVelocity = entity.BaseVelocity;

			state.WeaponModel = entity.Server.ModelIndex( entity.WeaponModel );
			state.GaitSequence = entity.GaitSequence;
			state.Spectator = (int)(entity.EntityFlags & EntityFlags.Spectator);
			state.Friction = entity.Friction;

			state.Gravity = entity.Gravity;

			state.UseHull = (entity.EntityFlags & EntityFlags.Ducking) == EntityFlags.Ducking ? 1 : 0;
			state.Health = (int)entity.Health;
		}

		if ( entity is BaseEntity baseEntity && baseEntity.Classify() != BaseEntity.ClassNone && baseEntity.Classify() != BaseEntity.ClassMachine )
			state.EFlags |= EntityState.EFlagFleshSound;
		else
			state.EFlags &= unchecked((byte)~EntityState.EFlagFleshSound);

		return true;
	}

	public override void CreateBaseline( bool player, int index, ref EntityState baseline, Edict entity, int playerModelIndex, Vector3 playerMins, Vector3 playerMaxs )
	{
		baseline.Origin = entity.Origin;
		baseline.Angles = entity.Angles;
		baseline.Frame = entity.Frame;
		baseline.Skin = (short)entity.Skin;

		baseline.RenderMode = (byte)(int)entity.RenderMode;
		baseline.RenderAmt = (byte)(int)entity.RenderAmt;
		baseline.RenderColorR = (byte)(int)entity.RenderColor.x;
		baseline.RenderColorG = (byte)(int)entity.RenderColor.y;
		baseline.RenderColorB = (byte)(int)entity.RenderColor.z;
		baseline.RenderFx = (byte)(int)entity.RenderFx;

		if ( player )
		{
			baseline.Mins = playerMins;
			baseline.Maxs = playerMaxs;

			baseline.Colormap = index;
			baseline.ModelIndex = playerModelIndex;
			baseline.Friction = 1f;
			baseline.MoveType = (int)MoveType.Walk;

			baseline.Scale = entity.Scale;
			baseline.Solid = (short)Solid.SlideBox;
			baseline.FrameRate = 1f;
			baseline.Gravity = 1f;
		}
		else
		{
			baseline.Mins = entity.Mins;
			baseline.Maxs = entity.Maxs;

			baseline.Colormap = 0;
			baseline.ModelIndex = entity.ModelIndex;
			baseline.MoveType = (int)entity.MoveType;

			baseline.Scale = entity.Scale;
			baseline.Solid = (short)entity.Solid;
			baseline.FrameRate = entity.FrameRate;
			baseline.Gravity = entity.Gravity;
		}
	}

	public override void CreateInstancedBaselines()
	{
	}

	public override void ServerDeactivate()
	{
		if ( !_serverActive )
			return;

		_serverActive = false;
	}

	public override void ServerActivate( Server server )
	{
		_serverActive = true;

		for ( var i = 0; i < server.NumEdicts; i++ )
		{
			var edict = server.GetEdict( i );

			if ( edict is null || i < server.MaxClients || !edict.HasPrivateData )
				continue;

			if ( edict is BaseEntity entity && (entity.EntityFlags & EntityFlags.Dormant) != EntityFlags.Dormant )
				entity.Activate();
			else
				Util.Alert( AlertType.Console, $"Can't instance {edict.ClassName}" );
		}

		UserMessages.Link();
	}
}
