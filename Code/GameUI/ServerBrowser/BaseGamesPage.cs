using System;
using System.Globalization;

namespace GoldSrc.GameUI;

public enum GameListInterfaceItem
{
	Filters,
	GetNewList,
	AddServer,
	AddCurrentServer
}

public sealed class ServerDisplay
{
	public int ListEntryID = -1;
	public int ServerID = -1;
	public bool DoNotRefresh = true;
}

public abstract class BaseGamesPage : PropertyPage, IMatchmakingServerListResponse
{
	private const int InvalidServerListID = -1;
	private const int ImagePassword = 1;
	private const int ImageSecure = 3;
	private const int ColumnPassword = 0;
	private const int ColumnBots = 1;
	private const int ColumnSecure = 2;
	private const int ColumnPlayers = 3;
	private const int ColumnName = 4;
	private const int ColumnGame = 5;
	private const int ColumnMap = 6;
	private const int ColumnPing = 7;
	private const int ColumnAddress = 8;

	public static BaseGamesPage CurrentPage;

	protected readonly ServerBrowserDialog Dialog;
	protected readonly ListPanel GameList;
	protected readonly Button Connect;
	protected readonly Button RefreshAll;
	protected readonly Button RefreshQuick;
	protected readonly Button AddServer;
	protected readonly Button AddCurrentServer;
	protected readonly ToggleButton Filter;

	private readonly Button _addToFavoritesButton;
	private readonly Label _filterString;
	private readonly ComboBox _gameFilter;
	private readonly TextEntry _mapFilter;
	private readonly ComboBox _pingFilter;
	private readonly ComboBox _secureFilter;
	private readonly CheckButton _noEmptyServersFilterCheck;
	private readonly CheckButton _noFullServersFilterCheck;
	private readonly CheckButton _noPasswordFilterCheck;
	private readonly CheckButton _validSteamAccountFilterCheck;
	private readonly Dictionary<int, ServerDisplay> _servers = [];
	private readonly string _comboAllText;
	private readonly string _customResFilename;

	protected readonly MatchMakingType MatchMakingType;
	protected ServerListRequest Request;
	protected int ServerRefreshCount;

	private SchemeFont _font;
	private bool _filtersVisible;
	private bool _autoSelectFirstItemInGameList;
	private string _gameFilterText = "";
	private string _mapFilterText = "";
	private int _pingFilterValue;
	private bool _filterNoFullServers;
	private bool _filterNoEmptyServers;
	private bool _filterNoPasswordedServers;
	private bool _filterValidSteamAccount;
	private int _secureFilterValue;

	protected BaseGamesPage( Vgui vgui, ServerBrowserDialog parent, string name, MatchMakingType type, string customResFilename = null ) : base( vgui, parent, name )
	{
		Dialog = parent;
		MatchMakingType = type;
		_customResFilename = customResFilename;

		SetSize( Scaled( 624 ), Scaled( 278 ) );

		_comboAllText = vgui.Localize( "#ServerBrowser_All" );

		Connect = new Button( vgui, this, "ConnectButton", "#ServerBrowser_Connect" );
		Connect.SetEnabled( false );

		RefreshAll = new Button( vgui, this, "RefreshButton", "#ServerBrowser_Refresh" );
		RefreshQuick = new Button( vgui, this, "RefreshQuickButton", "#ServerBrowser_RefreshQuick" );
		AddServer = new Button( vgui, this, "AddServerButton", "#ServerBrowser_AddServer" );
		AddCurrentServer = new Button( vgui, this, "AddCurrentServerButton", "#ServerBrowser_AddCurrentServer" );
		GameList = new ListPanel( vgui, this, "gamelist" );

		_addToFavoritesButton = new Button( vgui, this, "AddToFavoritesButton", "" );
		_addToFavoritesButton.SetEnabled( false );
		_addToFavoritesButton.SetVisible( false );

		GameList.AddColumnHeader( ColumnPassword, "Password", "#ServerBrowser_Password", Scaled( 16 ), ListPanelColumnFlags.FixedWidth | ListPanelColumnFlags.Image );
		GameList.AddColumnHeader( ColumnBots, "Bots", "#ServerBrowser_Bots", Scaled( 20 ), 0 );
		GameList.AddColumnHeader( ColumnSecure, "Secure", "#ServerBrowser_Secure", Scaled( 16 ), ListPanelColumnFlags.FixedWidth | ListPanelColumnFlags.Image );
		GameList.AddColumnHeader( ColumnPlayers, "Players", "#ServerBrowser_Players", Scaled( 55 ), 0 );
		GameList.AddColumnHeader( ColumnName, "Name", "#ServerBrowser_Servers", Scaled( 50 ), ListPanelColumnFlags.ResizeWithWindow | ListPanelColumnFlags.Unhidable );
		GameList.AddColumnHeader( ColumnGame, "GameDesc", "#ServerBrowser_Game", Scaled( 112 ), 0 );
		GameList.AddColumnHeader( ColumnMap, "Map", "#ServerBrowser_Map", Scaled( 90 ), 0 );
		GameList.AddColumnHeader( ColumnPing, "Ping", "#ServerBrowser_Latency", Scaled( 55 ), 0 );
		GameList.AddColumnHeader( ColumnAddress, "IPAddr", "#ServerBrowser_IPAddress", Scaled( 64 ), ListPanelColumnFlags.Hidden );

		GameList.SetSortFunc( ColumnPassword, PasswordCompare );
		GameList.SetSortFunc( ColumnBots, BotsCompare );
		GameList.SetSortFunc( ColumnSecure, SecureCompare );
		GameList.SetSortFunc( ColumnPlayers, PlayersCompare );
		GameList.SetSortFunc( ColumnName, ServerNameCompare );
		GameList.SetSortFunc( ColumnGame, GameCompare );
		GameList.SetSortFunc( ColumnMap, MapCompare );
		GameList.SetSortFunc( ColumnPing, PingCompare );
		GameList.SetSortFunc( ColumnAddress, AddressCompare );
		GameList.SetSortColumn( ColumnAddress );

		Filter = new ToggleButton( vgui, this, "Filter", "#ServerBrowser_Filter" );
		_filterString = new Label( vgui, this, "FilterString", "" );
		_gameFilter = new ComboBox( vgui, this, "GameFilter", 6, false );
		_mapFilter = new TextEntry( vgui, this, "MapFilter" );

		_pingFilter = new ComboBox( vgui, this, "PingFilter", 6, false );
		_pingFilter.AddItem( "#ServerBrowser_All" );
		_pingFilter.AddItem( "#ServerBrowser_LessThan50" );
		_pingFilter.AddItem( "#ServerBrowser_LessThan100" );
		_pingFilter.AddItem( "#ServerBrowser_LessThan150" );
		_pingFilter.AddItem( "#ServerBrowser_LessThan250" );
		_pingFilter.AddItem( "#ServerBrowser_LessThan350" );
		_pingFilter.AddItem( "#ServerBrowser_LessThan600" );

		_secureFilter = new ComboBox( vgui, this, "SecureFilter", 3, false );
		_secureFilter.AddItem( "#ServerBrowser_All" );
		_secureFilter.AddItem( "#ServerBrowser_SecureOnly" );
		_secureFilter.AddItem( "#ServerBrowser_InsecureOnly" );

		_noEmptyServersFilterCheck = new CheckButton( vgui, this, "ServerEmptyFilterCheck", "" );
		_noFullServersFilterCheck = new CheckButton( vgui, this, "ServerFullFilterCheck", "" );
		_noPasswordFilterCheck = new CheckButton( vgui, this, "NoPasswordFilterCheck", "" );
		_validSteamAccountFilterCheck = new CheckButton( vgui, this, "ValidSteamAccountFilterCheck", "" );

		var all = new KeyValues( "mod" );

		all.SetString( "gamedir", "" );
		all.SetString( "appid", "" );

		_gameFilter.AddItem( "#ServerBrowser_All", all );

		LoadFilterLayout( Filter.IsSelected );

		LoadFilterSettings();

		GameList.SetColumnVisible( ColumnBots, true );
		GameList.SetColumnVisible( ColumnAddress, false );
		GameList.SetColumnVisible( ColumnSecure, false );

		_autoSelectFirstItemInGameList = false;
	}

	private void LoadFilterLayout( bool filtersVisible )
	{
		var wide = Wide;
		var tall = Tall;

		SetSize( Scaled( 624 ), Scaled( 278 ) );

		_filtersVisible = _customResFilename is null && filtersVisible;

		if ( _customResFilename is not null )
			LoadControlSettings( _customResFilename );
		else if ( filtersVisible )
			LoadControlSettings( "servers/InternetGamesPage_Filters.res" );
		else
			LoadControlSettings( "servers/InternetGamesPage.res" );

		Filter.SetSelected( _filtersVisible );

		UpdateGameFilter();

		if ( _font is not null )
		{
			GameList.MakeReadyForUse();
			GameList.SetFont( _font );
		}

		HideUnsupportedControls();

		SetSize( wide, tall );
		InvalidateLayout();
	}

	private void HideUnsupportedControls()
	{
		_secureFilter.SetVisible( false );

		FindChildByName( "SecureFilterLabel" )?.SetVisible( false );
	}

	public abstract bool SupportsItem( GameListInterfaceItem item );

	public virtual void StartRefresh()
	{
		ClearServerList();

		Request = MatchmakingServers.RequestServerList( MatchMakingType, this );

		SetRefreshing( true );

		ServerRefreshCount = 0;
	}

	private void ClearServerList()
	{
		if ( Request is not null )
		{
			MatchmakingServers.ReleaseRequest( Request );

			Request = null;
		}

		_servers.Clear();

		GameList.DeleteAllItems();
	}

	public virtual void GetNewServerList() => StartRefresh();

	public void StopRefresh()
	{
		ServerRefreshCount = 0;

		MatchmakingServers.CancelQuery( Request );
	}

	public bool IsRefreshing => MatchmakingServers.IsRefreshing( Request );

	public GameServerItem GetServer( int serverID ) => serverID >= 0 ? MatchmakingServers.GetServerDetails( Request, serverID ) : null;

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		var imageList = new ImageList();
		var surface = Vgui.Surface;

		imageList.AddImage( new BitmapImage( surface.LoadImage( "servers/icon_password" ) ) );
		imageList.AddImage( new BitmapImage( surface.LoadImage( "servers/icon_bots" ) ) );
		imageList.AddImage( new BitmapImage( surface.LoadImage( "servers/icon_robotron" ) ) );
		imageList.AddImage( new BitmapImage( surface.LoadImage( "servers/icon_secure_deny" ) ) );

		var passwordColumnImage = imageList.AddImage( new BitmapImage( surface.LoadImage( "servers/icon_password_column" ) ) );
		var botColumnImage = imageList.AddImage( new BitmapImage( surface.LoadImage( "servers/icon_bots_column" ) ) );
		var secureColumnImage = imageList.AddImage( new BitmapImage( surface.LoadImage( "servers/icon_robotron_column" ) ) );

		GameList.SetImageList( imageList );

		_font = GetFont( "ListSmall" ) ?? GetFont( "DefaultSmall" );

		GameList.SetFont( _font );

		GameList.SetColumnHeaderImage( ColumnPassword, passwordColumnImage );
		GameList.SetColumnHeaderImage( ColumnBots, botColumnImage );
		GameList.SetColumnHeaderImage( ColumnSecure, secureColumnImage );
	}

	public override void PerformLayout()
	{
		base.PerformLayout();

		Connect.SetEnabled( GameList.GetSelectedItemsCount() > 0 );

		if ( SupportsItem( GameListInterfaceItem.GetNewList ) )
		{
			RefreshQuick.SetVisible( true );
			RefreshAll.SetText( "#ServerBrowser_RefreshAll" );
		}
		else
		{
			RefreshQuick.SetVisible( false );
			RefreshAll.SetText( "#ServerBrowser_Refresh" );
		}

		AddServer.SetVisible( false );
		AddCurrentServer.SetVisible( SupportsItem( GameListInterfaceItem.AddCurrentServer ) );

		if ( IsRefreshing )
			RefreshAll.SetText( "#ServerBrowser_StopRefreshingList" );

		RefreshQuick.SetEnabled( GameList.GetItemCount() > 0 );
	}

	private static GameServerItem SortServer( ListPanelItem item ) => CurrentPage?.GetServer( item.UserData );

	private static bool SortServers( ListPanelItem item1, ListPanelItem item2, out GameServerItem server1, out GameServerItem server2, out int result )
	{
		server1 = null;
		server2 = null;
		result = 0;

		if ( CurrentPage is null )
			return false;

		server1 = SortServer( item1 );
		server2 = SortServer( item2 );

		if ( server1 is null )
		{
			result = server2 is null ? 0 : -1;
			return false;
		}

		if ( server2 is null )
		{
			result = 1;
			return false;
		}

		return true;
	}

	private static int PasswordCompare( ListPanel panel, ListPanelItem item1, ListPanelItem item2 )
	{
		if ( !SortServers( item1, item2, out var server1, out var server2, out var result ) )
			return result;

		if ( server1.Password == server2.Password )
			return 0;

		return server1.Password ? -1 : 1;
	}

	private static int BotsCompare( ListPanel panel, ListPanelItem item1, ListPanelItem item2 )
	{
		if ( !SortServers( item1, item2, out var server1, out var server2, out var result ) )
			return result;

		if ( server1.BotPlayers < server2.BotPlayers )
			return 1;

		return server1.BotPlayers > server2.BotPlayers ? -1 : 0;
	}

	private static int SecureCompare( ListPanel panel, ListPanelItem item1, ListPanelItem item2 )
	{
		if ( !SortServers( item1, item2, out var server1, out var server2, out var result ) )
			return result;

		if ( server1.Secure == server2.Secure )
			return 0;

		return server1.Secure ? -1 : 1;
	}

	private static int PlayersCompare( ListPanel panel, ListPanelItem item1, ListPanelItem item2 )
	{
		if ( !SortServers( item1, item2, out var server1, out var server2, out var result ) )
			return result;

		var players1 = Math.Max( server1.Players - server1.BotPlayers, 0 );
		var max1 = Math.Max( server1.MaxPlayers - server1.BotPlayers, 0 );
		var players2 = Math.Max( server2.Players - server2.BotPlayers, 0 );
		var max2 = Math.Max( server2.MaxPlayers - server2.BotPlayers, 0 );

		if ( players2 < players1 )
			return -1;

		if ( players2 > players1 )
			return 1;

		if ( max2 < max1 )
			return -1;

		return max2 > max1 ? 1 : 0;
	}

	private static int ServerNameCompare( ListPanel panel, ListPanelItem item1, ListPanelItem item2 )
	{
		if ( !SortServers( item1, item2, out var server1, out var server2, out var result ) )
			return result;

		return string.Compare( server1.ServerName, server2.ServerName, StringComparison.OrdinalIgnoreCase );
	}

	private static int GameCompare( ListPanel panel, ListPanelItem item1, ListPanelItem item2 )
	{
		if ( !SortServers( item1, item2, out var server1, out var server2, out var result ) )
			return result;

		return string.Compare( server1.GameDescription, server2.GameDescription, StringComparison.OrdinalIgnoreCase );
	}

	private static int MapCompare( ListPanel panel, ListPanelItem item1, ListPanelItem item2 )
	{
		if ( !SortServers( item1, item2, out var server1, out var server2, out var result ) )
			return result;

		return string.Compare( server1.Map, server2.Map, StringComparison.OrdinalIgnoreCase );
	}

	private static int PingCompare( ListPanel panel, ListPanelItem item1, ListPanelItem item2 )
	{
		if ( !SortServers( item1, item2, out var server1, out var server2, out var result ) )
			return result;

		if ( server1.Ping < server2.Ping )
			return -1;

		return server1.Ping > server2.Ping ? 1 : 0;
	}

	private static int AddressCompare( ListPanel panel, ListPanelItem item1, ListPanelItem item2 )
	{
		if ( !SortServers( item1, item2, out var server1, out var server2, out var result ) )
			return result;

		if ( server1.SteamId < server2.SteamId )
			return -1;

		return server1.SteamId > server2.SteamId ? 1 : 0;
	}

	private void SetServerRow( KeyValues row, GameServerItem server, bool pendingDescription )
	{
		row.SetString( "name", server.ServerName );
		row.SetString( "map", server.Map );
		row.SetString( "GameDir", server.GameDir );
		row.SetString( "GameTags", server.GameTags );

		if ( pendingDescription && server.GameDescription.Length == 0 )
			row.SetString( "GameDesc", Vgui.Localize( "#ServerBrowser_PendingPing" ) );
		else
			row.SetString( "GameDesc", server.GameDescription );

		row.SetString( "Players", $"{Math.Max( server.Players - server.BotPlayers, 0 )} / {server.MaxPlayers}" );
		row.SetInt( "Ping", server.Ping );
		row.SetInt( "password", server.Password ? ImagePassword : 0 );

		if ( server.BotPlayers > 0 )
			row.SetInt( "bots", server.BotPlayers );
		else
			row.SetString( "bots", "" );

		row.SetInt( "secure", server.Secure ? ImageSecure : 0 );
		row.SetString( "IPAddr", server.SteamId.ToString( CultureInfo.InvariantCulture ) );

		if ( server.TimeLastPlayed != 0 )
		{
			var time = DateTimeOffset.FromUnixTimeSeconds( server.TimeLastPlayed ).LocalDateTime;
			var text = time.ToString( "ddd dd MMM hh:mmtt", CultureInfo.InvariantCulture );

			row.SetString( "LastPlayed", text.Length > 4 ? text[..^4] + text[^4..].ToLowerInvariant() : text );
		}
	}

	private void SetNotResponding( KeyValues row )
	{
		row.SetString( "Ping", "" );
		row.SetString( "GameDesc", Vgui.Localize( "#ServerBrowser_NotResponding" ) );
		row.SetString( "Players", "" );
		row.SetString( "map", "" );
	}

	public virtual void ServerResponded( ServerListRequest request, int server )
	{
		if ( request != Request || MatchmakingServers.GetServerDetails( request, server ) is not { } item )
			return;

		ServerResponded( server, item );
	}

	protected void ServerResponded( int serverID, GameServerItem server )
	{
		if ( !_servers.TryGetValue( serverID, out var display ) )
		{
			display = new ServerDisplay { DoNotRefresh = false };

			_servers[serverID] = display;
		}

		display.ServerID = serverID;

		if ( !CheckPrimaryFilters( server ) )
		{
			display.DoNotRefresh = true;
		}
		else if ( CheckSecondaryFilters( server ) )
		{
			KeyValues row;

			if ( GameList.IsValidItemID( display.ListEntryID ) )
			{
				row = GameList.GetItem( display.ListEntryID );

				GameList.SetUserData( display.ListEntryID, display.ServerID );
			}
			else
			{
				row = new KeyValues( "Server" );
			}

			SetServerRow( row, server, false );

			if ( display.DoNotRefresh )
				SetNotResponding( row );

			if ( !GameList.IsValidItemID( display.ListEntryID ) )
			{
				display.ListEntryID = GameList.AddItem( row, display.ServerID, false, false );

				if ( _autoSelectFirstItemInGameList && GameList.GetItemCount() == 1 )
					GameList.AddSelectedItem( display.ListEntryID );
			}
			else
			{
				GameList.ApplyItemChanges( display.ListEntryID );
				GameList.SetItemVisible( display.ListEntryID, true );
			}

			UpdateStatus();

			ServerRefreshCount++;

			return;
		}

		if ( GameList.IsValidItemID( display.ListEntryID ) )
		{
			GameList.RemoveItem( display.ListEntryID );

			display.ListEntryID = InvalidServerListID;
		}
	}

	public virtual void ServerFailedToRespond( ServerListRequest request, int server ) => ServerResponded( request, server );

	public abstract void RefreshComplete( ServerListRequest request, MatchMakingServerResponse response );

	protected void RemoveServer( int serverID )
	{
		if ( !_servers.TryGetValue( serverID, out var display ) )
			return;

		if ( GameList.IsValidItemID( display.ListEntryID ) )
			GameList.SetItemVisible( display.ListEntryID, false );

		UpdateStatus();
	}

	protected bool HasServer( int serverID ) => _servers.ContainsKey( serverID );

	protected void UpdateStatus()
	{
		var count = GameList.GetItemCount();

		if ( count > 1 )
			GameList.SetColumnHeaderText( ColumnName, Vgui.Localize( "#ServerBrowser_ServersCount" ).Replace( "%s1", count.ToString( CultureInfo.InvariantCulture ) ) );
		else
			GameList.SetColumnHeaderText( ColumnName, Vgui.Localize( "#ServerBrowser_Servers" ) );
	}

	protected virtual bool CheckPrimaryFilters( GameServerItem server )
	{
		if ( _gameFilterText.Length != 0 && (server.GameDir.Length != 0 || server.Ping != 0) && !_gameFilterText.Equals( server.GameDir, StringComparison.OrdinalIgnoreCase ) )
			return false;

		return true;
	}

	protected virtual bool CheckSecondaryFilters( GameServerItem server )
	{
		if ( _filterNoEmptyServers && server.Players - server.BotPlayers <= 0 )
			return false;

		if ( _filterNoFullServers && server.Players >= server.MaxPlayers )
			return false;

		if ( _pingFilterValue != 0 && server.Ping > _pingFilterValue )
			return false;

		if ( _filterNoPasswordedServers && server.Password )
			return false;

		if ( _filterValidSteamAccount && server.SteamId == 0 )
			return false;

		if ( _secureFilterValue == 1 && !server.Secure )
			return false;

		if ( _secureFilterValue == 2 && server.Secure )
			return false;

		if ( _mapFilterText.Length != 0 && !server.Map.StartsWith( _mapFilterText, StringComparison.OrdinalIgnoreCase ) )
			return false;

		return true;
	}

	protected virtual bool ShowServer( ServerDisplay server ) => server.DoNotRefresh;

	private void ApplyGameFilters()
	{
		foreach ( var display in _servers.Values )
		{
			if ( MatchmakingServers.GetServerDetails( Request, display.ServerID ) is not { } server )
				continue;

			if ( !CheckPrimaryFilters( server ) || !CheckSecondaryFilters( server ) )
			{
				display.DoNotRefresh = true;

				if ( GameList.IsValidItemID( display.ListEntryID ) )
					GameList.SetItemVisible( display.ListEntryID, false );
			}
			else if ( ShowServer( display ) )
			{
				display.DoNotRefresh = false;

				if ( !GameList.IsValidItemID( display.ListEntryID ) )
				{
					var row = new KeyValues( "Server" );

					SetServerRow( row, server, true );

					if ( server.DoNotRefresh )
						SetNotResponding( row );

					display.ListEntryID = GameList.AddItem( row, display.ServerID, false, false );
				}

				GameList.SetItemVisible( display.ListEntryID, true );
			}
		}

		UpdateStatus();

		GameList.SortList();

		InvalidateLayout();
	}

	private void UpdateGameFilter()
	{
		var count = _gameFilter.GetItemCount();
		int i;

		for ( i = 0; i < count; i++ )
		{
			var modDir = _gameFilter.GetItemUserData( i )?.GetString( "gamedir" );

			if ( _gameFilterText.Length == 0 || (!string.IsNullOrEmpty( modDir ) && _gameFilterText.StartsWith( modDir, StringComparison.Ordinal )) )
				break;
		}

		if ( i < count )
		{
			if ( i != _gameFilter.GetActiveItem() )
				_gameFilter.ActivateItem( i );
		}
		else if ( _gameFilter.GetActiveItem() != 0 && _gameFilter.IsItemIDValid( i ) )
		{
			_gameFilter.ActivateItem( i );
		}

		if ( Dialog.ActiveModName is not null )
		{
			_gameFilter.SetEnabled( false );
			_gameFilter.SetText( Dialog.ActiveGameName );
		}
	}

	protected void LoadFilterSettings()
	{
		var filter = Dialog.GetFilterSaveData( Name );

		_gameFilterText = Dialog.ActiveModName ?? filter.GetString( "game" );
		_mapFilterText = filter.GetString( "map" );
		_pingFilterValue = filter.GetInt( "ping" );
		_filterNoFullServers = filter.GetInt( "NoFull" ) != 0;
		_filterNoEmptyServers = filter.GetInt( "NoEmpty" ) != 0;
		_filterNoPasswordedServers = filter.GetInt( "NoPassword" ) != 0;
		_filterValidSteamAccount = filter.GetInt( "ValidSteamAccount" ) != 0;

		_secureFilter.ActivateItem( 0 );

		UpdateGameFilter();

		_mapFilter.SetText( _mapFilterText );

		if ( _pingFilterValue != 0 )
			_pingFilter.SetText( $"< {_pingFilterValue}" );

		_noFullServersFilterCheck.SetSelected( _filterNoFullServers );
		_noEmptyServersFilterCheck.SetSelected( _filterNoEmptyServers );
		_noPasswordFilterCheck.SetSelected( _filterNoPasswordedServers );
		_validSteamAccountFilterCheck.SetSelected( _filterValidSteamAccount );

		OnLoadFilter( filter );
		UpdateFilterSettings();
	}

	protected virtual void OnLoadFilter( KeyValues filter )
	{
	}

	protected virtual void OnSaveFilter( KeyValues filter )
	{
	}

	protected void UpdateFilterSettings()
	{
		if ( Dialog.ActiveModName is not null )
		{
			_gameFilterText = Dialog.ActiveModName;

			RecalculateFilterString();
			UpdateGameFilter();
		}
		else
		{
			var modDir = _gameFilter.GetActiveItemUserData()?.GetString( "gamedir" ) ?? "";

			_gameFilterText = modDir;

			_gameFilter.SetEnabled( true );
		}

		_gameFilterText = _gameFilterText.ToLowerInvariant();
		_mapFilterText = _mapFilter.Text.ToLowerInvariant();

		var ping = _pingFilter.Text;

		_pingFilterValue = ping.Length > 2 ? Common.Atoi( ping[2..] ) : 0;

		_filterNoFullServers = _noFullServersFilterCheck.IsSelected;
		_filterNoEmptyServers = _noEmptyServersFilterCheck.IsSelected;
		_filterNoPasswordedServers = _noPasswordFilterCheck.IsSelected;
		_filterValidSteamAccount = _validSteamAccountFilterCheck.IsSelected;
		_secureFilterValue = Math.Max( _secureFilter.GetActiveItem(), 0 );

		var filter = Dialog.GetFilterSaveData( Name );

		if ( Dialog.ActiveModName is null )
			filter.SetString( "game", _gameFilterText );

		filter.SetString( "map", _mapFilterText );
		filter.SetInt( "ping", _pingFilterValue );
		filter.SetInt( "NoFull", _filterNoFullServers ? 1 : 0 );
		filter.SetInt( "NoEmpty", _filterNoEmptyServers ? 1 : 0 );
		filter.SetInt( "NoPassword", _filterNoPasswordedServers ? 1 : 0 );
		filter.SetInt( "ValidSteamAccount", _filterValidSteamAccount ? 1 : 0 );
		filter.SetInt( "Secure", _secureFilterValue );

		OnSaveFilter( filter );

		RecalculateFilterString();
	}

	private void RecalculateFilterString()
	{
		const string spacer = "; ";

		var text = new System.Text.StringBuilder();

		if ( _gameFilterText.Length != 0 )
			text.Append( Dialog.ActiveGameName ?? _gameFilterText ).Append( spacer );

		if ( _secureFilterValue == 1 )
			text.Append( Vgui.Localize( "#ServerBrowser_FilterDescSecureOnly" ) ).Append( spacer );
		else if ( _secureFilterValue == 2 )
			text.Append( Vgui.Localize( "#ServerBrowser_FilterDescInsecureOnly" ) ).Append( spacer );

		if ( _pingFilterValue != 0 )
			text.Append( Vgui.Localize( "#ServerBrowser_FilterDescLatency" ) ).Append( " < " ).Append( _pingFilterValue ).Append( spacer );

		if ( _filterNoFullServers )
			text.Append( Vgui.Localize( "#ServerBrowser_FilterDescNotFull" ) ).Append( spacer );

		if ( _filterNoEmptyServers )
			text.Append( Vgui.Localize( "#ServerBrowser_FilterDescNotEmpty" ) ).Append( spacer );

		if ( _filterNoPasswordedServers )
			text.Append( Vgui.Localize( "#ServerBrowser_FilterDescNoPassword" ) ).Append( spacer );

		if ( _filterValidSteamAccount )
			text.Append( Vgui.Localize( "#ServerBrowser_FilterDescValidSteamAccount" ) ).Append( spacer );

		if ( _mapFilterText.Length != 0 )
			text.Append( _mapFilterText );

		_filterString.SetText( text.ToString() );
	}

	public virtual void SetRefreshing( bool state )
	{
		if ( state )
		{
			Dialog.UpdateStatusText( "#ServerBrowser_RefreshingServerList" );

			GameList.SetEmptyListText( "" );

			RefreshAll.SetText( "#ServerBrowser_StopRefreshingList" );
			RefreshAll.SetCommand( "stoprefresh" );
			RefreshQuick.SetEnabled( false );

			return;
		}

		Dialog.UpdateStatusText( "" );

		RefreshAll.SetText( SupportsItem( GameListInterfaceItem.GetNewList ) ? "#ServerBrowser_RefreshAll" : "#ServerBrowser_Refresh" );
		RefreshAll.SetCommand( "GetNewList" );
		RefreshQuick.SetEnabled( GameList.GetItemCount() > 0 );
	}

	public override void OnCommand( string command )
	{
		if ( command.Equals( "Connect", StringComparison.OrdinalIgnoreCase ) )
		{
			OnBeginConnect();
		}
		else if ( command.Equals( "stoprefresh", StringComparison.OrdinalIgnoreCase ) )
		{
			StopRefresh();
		}
		else if ( command.Equals( "refresh", StringComparison.OrdinalIgnoreCase ) )
		{
			MatchmakingServers.RefreshQuery( Request );

			SetRefreshing( true );

			ServerRefreshCount = 0;
		}
		else if ( command.Equals( "GetNewList", StringComparison.OrdinalIgnoreCase ) )
		{
			GetNewServerList();
		}
	}

	protected virtual void OnBeginConnect()
	{
		if ( GameList.GetSelectedItemsCount() == 0 )
			return;

		var serverID = GameList.GetItemUserData( GameList.GetSelectedItem( 0 ) );

		StopRefresh();

		Dialog.JoinGame( this, serverID );
	}

	private void OnViewGameInfo()
	{
		if ( GameList.GetSelectedItemsCount() == 0 )
			return;

		var serverID = GameList.GetItemUserData( GameList.GetSelectedItem( 0 ) );

		StopRefresh();

		Dialog.OpenGameInfoDialog( this, serverID );
	}

	private void OnItemSelected() => Connect.SetEnabled( GameList.GetSelectedItemsCount() >= 1 );

	private void OnAddToFavorites()
	{
		for ( var i = 0; i < GameList.GetSelectedItemsCount(); i++ )
		{
			var serverID = GameList.GetItemUserData( GameList.GetSelectedItem( i ) );

			if ( MatchmakingServers.GetServerDetails( Request, serverID ) is { } server )
				Dialog.AddServerToFavorites( server );
		}
	}

	protected virtual void OnRefreshServer( int serverID )
	{
		for ( var i = 0; i < GameList.GetSelectedItemsCount(); i++ )
			MatchmakingServers.RefreshServer( Request, GameList.GetItemUserData( GameList.GetSelectedItem( i ) ) );

		SetRefreshing( IsRefreshing );
	}

	private void OnButtonToggled( Panel panel )
	{
		if ( panel == Filter )
		{
			LoadFilterLayout( Filter.IsSelected );
			return;
		}

		if ( panel == _noFullServersFilterCheck || panel == _noEmptyServersFilterCheck || panel == _noPasswordFilterCheck || panel == _validSteamAccountFilterCheck )
			OnTextChanged( panel, "" );
	}

	private void OnTextChanged( Panel panel, string text )
	{
		if ( text is not null && text.Equals( _comboAllText, StringComparison.OrdinalIgnoreCase ) && panel is ComboBox comboBox )
			comboBox.SetText( "" );

		UpdateFilterSettings();
		ApplyGameFilters();

		if ( _filtersVisible && panel == _gameFilter && Dialog.Visible )
		{
			StopRefresh();
			GetNewServerList();
		}
	}

	protected virtual void OnOpenContextMenu( int itemID )
	{
	}

	public void OnFavoritesMsg( ulong ownerId, bool add )
	{
		if ( MatchMakingType is not (MatchMakingType.FavoritesServer or MatchMakingType.HistoryServer) )
			return;

		if ( add )
		{
			MatchmakingServers.RefreshQuery( Request );
			return;
		}

		foreach ( var display in _servers.Values )
		{
			if ( MatchmakingServers.GetServerDetails( Request, display.ServerID ) is not { } server || server.OwnerId != ownerId )
				continue;

			if ( GameList.IsValidItemID( display.ListEntryID ) )
				GameList.SetItemVisible( display.ListEntryID, false );

			UpdateStatus();

			break;
		}
	}

	public override void OnMessage( Message message, Panel from )
	{
		switch ( message.Name )
		{
			case "ItemSelected":
				OnItemSelected();
				break;

			case "ConnectToServer":
				OnBeginConnect();
				break;

			case "ViewGameInfo":
				OnViewGameInfo();
				break;

			case "RefreshServer":
				OnRefreshServer( message.Int );
				break;

			case "AddToFavorites":
				OnAddToFavorites();
				break;

			case "ButtonToggled":
				OnButtonToggled( from );
				break;

			case "TextChanged":
				OnTextChanged( from, message.Text ?? (from as TextEntry)?.Text );
				break;

			case "OpenContextMenu":
				OnOpenContextMenu( message.Int );
				break;

			default:
				base.OnMessage( message, from );
				break;
		}
	}

	public override void OnKeyCodePressed( KeyCode code )
	{
		if ( code == KeyCode.F5 )
		{
			StartRefresh();
			return;
		}

		base.OnKeyCodePressed( code );
	}

	protected override void OnPageShow()
	{
		CurrentPage = this;

		StartRefresh();
	}

	protected override void OnPageHide() => StopRefresh();
}
