using System;
using System.Threading.Tasks;
using Sandbox.Network;

namespace GoldSrc.GameUI;

public sealed class GameServerItem
{
	public ulong SteamId;
	public ulong OwnerId;
	public int Ping;
	public bool HadSuccessfulResponse;
	public bool DoNotRefresh;
	public string GameDir = "";
	public string Map = "";
	public string GameDescription = "";
	public int Players;
	public int MaxPlayers;
	public int BotPlayers;
	public bool Password;
	public bool Secure;
	public uint TimeLastPlayed;
	public string ServerName = "";
	public string GameTags = "";
	public string PlayerData = "";
}

public interface IMatchmakingPlayersResponse
{
	void AddPlayerToList( string name, int score, float timePlayed );

	void PlayersFailedToRespond();

	void PlayersRefreshComplete();
}

public enum MatchMakingType
{
	InternetServer,
	LanServer,
	FriendsServer,
	FavoritesServer,
	HistoryServer,
	SpectatorServer
}

public enum MatchMakingServerResponse
{
	ServerResponded,
	ServerFailedToRespond,
	NoServersListedOnMasterServer
}

public interface IMatchmakingServerListResponse
{
	void ServerResponded( ServerListRequest request, int server );

	void ServerFailedToRespond( ServerListRequest request, int server );

	void RefreshComplete( ServerListRequest request, MatchMakingServerResponse response );
}

public interface IMatchmakingPingResponse
{
	void ServerResponded( GameServerItem server );

	void ServerFailedToRespond();
}

public sealed class ServerQuery
{
	public bool Cancelled;
}

public sealed class ServerListRequest( MatchMakingType type, IMatchmakingServerListResponse response )
{
	public readonly MatchMakingType Type = type;
	public readonly IMatchmakingServerListResponse Response = response;
	public readonly List<GameServerItem> Servers = [];

	public bool Refreshing;
	public bool Released;
	public int Query;
}

public static class MatchmakingServers
{
	public const string GameDirKey = "gamedir";
	public const string DescriptionKey = "desc";
	public const string PasswordKey = "password";
	public const string BotsKey = "bots";
	public const string PlayersKey = "players";
	public const char PlayerSeparator = '\n';
	public const char PlayerFieldSeparator = '\t';

	public static void PlayerDetails( GameServerItem server, IMatchmakingPlayersResponse response )
	{
		if ( !server.HadSuccessfulResponse )
		{
			response.PlayersFailedToRespond();
			return;
		}

		var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		foreach ( var player in server.PlayerData.Split( PlayerSeparator, StringSplitOptions.RemoveEmptyEntries ) )
		{
			var fields = player.Split( PlayerFieldSeparator );

			if ( fields.Length != 3 )
				continue;

			response.AddPlayerToList( fields[0], Common.Atoi( fields[1] ), long.TryParse( fields[2], out var joined ) ? Math.Max( now - joined, 0 ) : 0 );
		}

		response.PlayersRefreshComplete();
	}

	public static ServerListRequest RequestServerList( MatchMakingType type, IMatchmakingServerListResponse response )
	{
		var request = new ServerListRequest( type, response );

		_ = RunQuery( request, -1 );

		return request;
	}

	public static void ReleaseRequest( ServerListRequest request )
	{
		request.Released = true;
		request.Refreshing = false;
	}

	public static GameServerItem GetServerDetails( ServerListRequest request, int server )
	{
		return request is not null && server >= 0 && server < request.Servers.Count ? request.Servers[server] : null;
	}

	public static void CancelQuery( ServerListRequest request )
	{
		if ( request is null || !request.Refreshing )
			return;

		request.Query++;
		request.Refreshing = false;

		request.Response.RefreshComplete( request, MatchMakingServerResponse.ServerResponded );
	}

	public static void RefreshQuery( ServerListRequest request )
	{
		if ( request is not null )
			_ = RunQuery( request, -1 );
	}

	public static bool IsRefreshing( ServerListRequest request ) => request is { Refreshing: true };

	public static int GetServerCount( ServerListRequest request ) => request?.Servers.Count ?? 0;

	public static void RefreshServer( ServerListRequest request, int server )
	{
		if ( GetServerDetails( request, server ) is not null )
			_ = RunQuery( request, server );
	}

	public static ServerQuery PingServer( ulong ownerId, IMatchmakingPingResponse response )
	{
		var query = new ServerQuery();

		_ = RunPing( query, ownerId, response );

		return query;
	}

	public static void CancelServerQuery( ServerQuery query )
	{
		if ( query is not null )
			query.Cancelled = true;
	}

	private static async Task RunPing( ServerQuery query, ulong ownerId, IMatchmakingPingResponse response )
	{
		List<LobbyInformation> lobbies;

		try
		{
			lobbies = await Networking.QueryLobbies();
		}
		catch ( Exception )
		{
			lobbies = null;
		}

		if ( query.Cancelled )
			return;

		if ( lobbies is not null )
		{
			foreach ( var lobby in lobbies )
			{
				if ( lobby.OwnerId != ownerId )
					continue;

				var item = new GameServerItem();

				Fill( item, lobby );

				response.ServerResponded( item );

				return;
			}
		}

		response.ServerFailedToRespond();
	}

	private static void Fill( GameServerItem item, LobbyInformation lobby )
	{
		item.SteamId = lobby.LobbyId;
		item.OwnerId = lobby.OwnerId;
		item.Ping = Math.Max( lobby.Ping, 0 );
		item.HadSuccessfulResponse = true;
		item.DoNotRefresh = false;
		item.GameDir = lobby.Get( GameDirKey );
		item.Map = lobby.Map ?? "";
		item.GameDescription = lobby.Get( DescriptionKey );
		item.Players = lobby.Members;
		item.MaxPlayers = lobby.MaxMembers;
		item.BotPlayers = Common.Atoi( lobby.Get( BotsKey ) );
		item.Password = lobby.Get( PasswordKey ) == "1";
		item.PlayerData = lobby.Get( PlayersKey );
		item.ServerName = lobby.Name ?? "";
	}

	private static bool Accepts( ServerListRequest request, LobbyInformation lobby )
	{
		return request.Type switch
		{
			MatchMakingType.InternetServer => true,
			MatchMakingType.FriendsServer => lobby.OwnerId != 0 && new Friend( lobby.OwnerId ).IsFriend,
			_ => false
		};
	}

	private static async Task RunQuery( ServerListRequest request, int onlyServer )
	{
		var query = ++request.Query;

		request.Refreshing = true;

		List<LobbyInformation> lobbies;

		try
		{
			lobbies = await Networking.QueryLobbies();
		}
		catch ( Exception )
		{
			lobbies = null;
		}

		if ( request.Released || request.Query != query )
			return;

		var servers = request.Servers;
		var responded = new bool[servers.Count];

		if ( request.Type is MatchMakingType.FavoritesServer or MatchMakingType.HistoryServer )
		{
			var flag = request.Type == MatchMakingType.FavoritesServer ? FavoriteGameList.FlagFavorite : FavoriteGameList.FlagHistory;

			foreach ( var favorite in FavoriteGameList.Games )
			{
				if ( (favorite.Flags & flag) == 0 || servers.Exists( x => x.OwnerId == favorite.OwnerId ) )
					continue;

				servers.Add( new GameServerItem { SteamId = favorite.OwnerId, OwnerId = favorite.OwnerId, ServerName = favorite.Name, TimeLastPlayed = favorite.TimeLastPlayed } );
			}

			Array.Resize( ref responded, servers.Count );
		}

		if ( lobbies is not null )
		{
			foreach ( var lobby in lobbies )
			{
				var index = request.Type is MatchMakingType.FavoritesServer or MatchMakingType.HistoryServer
					? servers.FindIndex( x => x.OwnerId == lobby.OwnerId )
					: servers.FindIndex( x => x.SteamId == lobby.LobbyId );

				if ( index < 0 )
				{
					if ( !Accepts( request, lobby ) )
						continue;

					index = servers.Count;
					servers.Add( new GameServerItem() );

					Array.Resize( ref responded, servers.Count );
				}

				Fill( servers[index], lobby );

				responded[index] = true;
			}
		}

		for ( var i = 0; i < servers.Count; i++ )
		{
			if ( onlyServer >= 0 && i != onlyServer )
				continue;

			if ( responded[i] )
			{
				request.Response.ServerResponded( request, i );
				continue;
			}

			servers[i].HadSuccessfulResponse = false;
			servers[i].DoNotRefresh = true;

			request.Response.ServerFailedToRespond( request, i );
		}

		request.Refreshing = false;

		if ( lobbies is null )
			request.Response.RefreshComplete( request, MatchMakingServerResponse.ServerFailedToRespond );
		else if ( servers.Count == 0 )
			request.Response.RefreshComplete( request, MatchMakingServerResponse.NoServersListedOnMasterServer );
		else
			request.Response.RefreshComplete( request, MatchMakingServerResponse.ServerResponded );
	}
}

public sealed class FavoriteGame
{
	public ulong OwnerId;
	public string Name = "";
	public int Flags;
	public uint TimeLastPlayed;
}

public static class FavoriteGameList
{
	public const int FlagFavorite = 1;
	public const int FlagHistory = 2;

	private const string FileName = "config/serverbrowser_hist.vdf";

	private static List<FavoriteGame> _games;

	public static List<FavoriteGame> Games
	{
		get
		{
			if ( _games is null )
				Load();

			return _games;
		}
	}

	public static int Count => Games.Count;

	private static void Load()
	{
		_games = [];

		if ( !FileSystem.Data.FileExists( FileName ) || KeyValues.LoadFromText( FileSystem.Data.ReadAllText( FileName ) ) is not { } file )
			return;

		foreach ( var key in file.Keys )
		{
			if ( !ulong.TryParse( key.Name, out var ownerId ) )
				continue;

			_games.Add( new FavoriteGame
			{
				OwnerId = ownerId,
				Name = key.GetString( "name" ),
				Flags = key.GetInt( "flags" ),
				TimeLastPlayed = uint.TryParse( key.GetString( "lastplayed" ), out var time ) ? time : 0
			} );
		}
	}

	private static void Save()
	{
		var file = new KeyValues( "Filters" );

		foreach ( var game in _games )
		{
			var key = file.FindKey( game.OwnerId.ToString(), true );

			key.SetString( "name", game.Name );
			key.SetInt( "flags", game.Flags );
			key.SetString( "lastplayed", game.TimeLastPlayed.ToString() );
		}

		FileSystem.Data.CreateDirectory( "config" );
		FileSystem.Data.WriteAllText( FileName, file.SaveToText() );
	}

	public static void AddFavoriteGame( GameServerItem server, int flags, uint timeLastPlayed )
	{
		var game = Games.Find( x => x.OwnerId == server.OwnerId );

		if ( game is null )
		{
			game = new FavoriteGame { OwnerId = server.OwnerId };

			_games.Add( game );
		}

		game.Name = server.ServerName;
		game.Flags |= flags;
		game.TimeLastPlayed = timeLastPlayed;

		Save();

		ServerBrowserDialog.Instance?.OnFavoritesMsg( server.OwnerId, true );
	}

	public static bool RemoveFavoriteGame( ulong ownerId, int flags )
	{
		if ( Games.Find( x => x.OwnerId == ownerId ) is not { } game || (game.Flags & flags) == 0 )
			return false;

		game.Flags &= ~flags;

		if ( game.Flags == 0 )
			_games.Remove( game );

		Save();

		ServerBrowserDialog.Instance?.OnFavoritesMsg( ownerId, false );

		return true;
	}
}
