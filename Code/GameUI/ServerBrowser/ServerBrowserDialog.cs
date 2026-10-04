using System;

namespace GoldSrc.GameUI;

public sealed class ServerContextMenu : Menu
{
	public ServerContextMenu( Vgui vgui, Panel parent ) : base( vgui, parent, "ServerContextMenu" )
	{
	}

	public void ShowMenu( Panel target, int serverID, bool showConnect, bool showViewGameInfo, bool showRefresh, bool showAddToFavorites )
	{
		if ( showConnect )
			AddMenuItem( "ConnectToServer", "#ServerBrowser_ConnectToServer", new Message( "ConnectToServer" ) { Int = serverID }, target );

		if ( showViewGameInfo )
			AddMenuItem( "ViewGameInfo", "#ServerBrowser_ViewServerInfo", new Message( "ViewGameInfo" ) { Int = serverID }, target );

		if ( showRefresh )
			AddMenuItem( "RefreshServer", "#ServerBrowser_RefreshServer", new Message( "RefreshServer" ) { Int = serverID }, target );

		if ( showAddToFavorites )
			AddMenuItem( "AddToFavorites", "#ServerBrowser_AddServerToFavorites", new Message( "AddToFavorites" ) { Int = serverID }, target );

		var (x, y) = Vgui.Input.CursorPos;

		SetPos( x, y );
		SetVisible( true );
	}
}

public sealed class ServerBrowserDialog : Frame
{
	private const string UserDataFile = "config/ServerBrowser.vdf";

	public static ServerBrowserDialog Instance;

	private readonly InternetGames _internetGames;
	private readonly FavoriteGames _favorites;
	private readonly HistoryGames _history;
	private readonly FriendsGames _friendsGames;
	private readonly PropertySheet _tabPanel;
	private readonly Label _statusLabel;

	private BaseGamesPage _gameList;
	private ServerContextMenu _contextMenu;
	private KeyValues _savedData;
	private KeyValues _filterData;
	private bool _currentlyConnected;
	private readonly List<DialogGameInfo> _gameInfoDialogs = [];

	public string ActiveGameName { get; private set; }

	public string ActiveModName { get; private set; }

	public GameServerItem CurrentConnectedServer { get; private set; }

	public ServerBrowserDialog( Vgui vgui, Panel parent, string gameName, string modName ) : base( vgui, parent, "CServerBrowserDialog" )
	{
		SetHdProportional( true );

		Instance = this;

		ActiveGameName = string.IsNullOrEmpty( gameName ) ? null : gameName;
		ActiveModName = string.IsNullOrEmpty( modName ) ? null : modName;

		LoadUserData();

		_internetGames = new InternetGames( vgui, this );
		_favorites = new FavoriteGames( vgui, this );
		_history = new HistoryGames( vgui, this );
		_friendsGames = new FriendsGames( vgui, this );

		SetCloseSound( "sound/UI/launch_dnmenu1.wav" );
		SetMinimumSize( Scaled( 640 ), Scaled( 384 ) );
		SetSize( Scaled( 640 ), Scaled( 384 ) );

		_gameList = _internetGames;
		_contextMenu = new ServerContextMenu( vgui, this );

		_tabPanel = new PropertySheet( vgui, this, "GameTabs" );
		_tabPanel.SetTabWidth( Scaled( 72 ) );
		_tabPanel.AddPage( _internetGames, "#ServerBrowser_InternetTab" );
		_tabPanel.AddPage( _favorites, "#ServerBrowser_FavoritesTab" );
		_tabPanel.AddPage( _history, "#ServerBrowser_HistoryTab" );
		_tabPanel.AddPage( _friendsGames, "#ServerBrowser_FriendsTab" );
		_tabPanel.AddActionSignalTarget( this );

		_statusLabel = new Label( vgui, this, "StatusLabel", "" );

		LoadControlSettings( "Servers/DialogServerBrowser.res" );

		var surface = vgui.Surface;

		SetSize( Math.Min( Wide, surface.ScreenWide ), Math.Min( Tall, surface.ScreenTall ) );

		_statusLabel.SetText( "" );

		var gameList = _savedData.GetString( "GameList" );

		if ( gameList.Equals( "favorites", StringComparison.OrdinalIgnoreCase ) )
			_tabPanel.SetActivePage( _favorites );
		else if ( gameList.Equals( "history", StringComparison.OrdinalIgnoreCase ) )
			_tabPanel.SetActivePage( _history );
		else if ( gameList.Equals( "friends", StringComparison.OrdinalIgnoreCase ) )
			_tabPanel.SetActivePage( _friendsGames );
		else
			_tabPanel.SetActivePage( _internetGames );

		SetTitle( "#ServerBrowser_Servers" );
		SetVisible( false );
	}

	public void LoadUserData()
	{
		_savedData = (FileSystem.Data.FileExists( UserDataFile ) ? KeyValues.LoadFromText( FileSystem.Data.ReadAllText( UserDataFile ) ) : KeyValues.LoadFromFile( UserDataFile )) ?? new KeyValues( "Filters" );

		if ( _savedData.FindKey( "Filters" ) is { } filters )
		{
			_filterData = filters.MakeCopy();

			_savedData.RemoveSubKey( filters );
		}
		else
		{
			_filterData = new KeyValues( "Filters" );
		}

		if ( _history is not null )
		{
			_history.LoadHistoryList();

			if ( Visible && _history.Visible )
				_history.StartRefresh();
		}

		if ( _favorites is not null )
		{
			_favorites.LoadFavoritesList();

			_gameList?.StartRefresh();

			if ( Visible && _favorites.Visible )
				_favorites.StartRefresh();
		}

		InvalidateLayout();
	}

	private void SaveUserData()
	{
		_savedData = new KeyValues( "Filters" );

		if ( _gameList == _favorites )
			_savedData.SetString( "GameList", "favorites" );
		else if ( _gameList == _friendsGames )
			_savedData.SetString( "GameList", "friends" );
		else if ( _gameList == _history )
			_savedData.SetString( "GameList", "history" );
		else
			_savedData.SetString( "GameList", "internet" );

		_savedData.AddSubKey( _filterData.MakeCopy() );

		FileSystem.Data.CreateDirectory( "config" );
		FileSystem.Data.WriteAllText( UserDataFile, _savedData.SaveToText() );
	}

	public KeyValues GetFilterSaveData( string filterSet ) => _filterData.FindKey( filterSet, true );

	public void UpdateStatusText( string text ) => _statusLabel?.SetText( text ?? "" );

	public ServerContextMenu GetContextMenu( Panel contextParent )
	{
		_contextMenu?.Delete();

		_contextMenu = new ServerContextMenu( Vgui, this );
		_contextMenu.SetParent( contextParent ?? this );
		_contextMenu.SetVisible( false );

		return _contextMenu;
	}

	public void AddServerToFavorites( GameServerItem server )
	{
		FavoriteGameList.AddFavoriteGame( server, FavoriteGameList.FlagFavorite, (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds() );
	}

	public void OnFavoritesMsg( ulong ownerId, bool add )
	{
		_favorites.OnFavoritesMsg( ownerId, add );
		_history.OnFavoritesMsg( ownerId, add );
	}

	public DialogGameInfo JoinGame( BaseGamesPage gameList, int serverIndex )
	{
		var gameDialog = OpenGameInfoDialog( gameList, serverIndex );

		gameDialog?.Connect();

		return gameDialog;
	}

	public DialogGameInfo OpenGameInfoDialog( BaseGamesPage gameList, int serverIndex )
	{
		if ( gameList.GetServer( serverIndex ) is not { } server )
			return null;

		var gameDialog = new DialogGameInfo( Vgui, null, server );

		gameDialog.SetParent( Parent );
		gameDialog.AddActionSignalTarget( this );
		gameDialog.Run( "" );

		_gameInfoDialogs.Add( gameDialog );

		return gameDialog;
	}

	public void CloseAllGameInfoDialogs()
	{
		foreach ( var dialog in _gameInfoDialogs )
		{
			if ( !dialog.Deleted )
				PostMessage( dialog, new Message( "Close" ) );
		}
	}

	public void OnConnectToGame( GameServerItem server )
	{
		CurrentConnectedServer = server;

		FavoriteGameList.AddFavoriteGame( server, FavoriteGameList.FlagHistory, (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds() );

		_history.SetRefreshOnListReload();

		foreach ( var dialog in _gameInfoDialogs )
		{
			if ( !dialog.Deleted )
				dialog.OnConnectToGame( server.OwnerId );
		}

		_favorites.OnConnectToGame();

		_currentlyConnected = true;

		Close();
	}

	public void OnDisconnectFromGame()
	{
		_currentlyConnected = false;

		CurrentConnectedServer = null;

		_favorites.OnDisconnectFromGame();
	}

	public bool IsConnected => _currentlyConnected;

	private void OnGameListChanged()
	{
		_gameList = _tabPanel.ActivePage as BaseGamesPage;

		UpdateStatusText( "" );

		InvalidateLayout();
	}

	public override void OnMessage( Message message, Panel from )
	{
		if ( message.Name == "PageChanged" )
		{
			OnGameListChanged();
			return;
		}

		base.OnMessage( message, from );
	}

	public override void Activate()
	{
		base.Activate();

		_tabPanel.RequestFocus();
	}

	public override void Close()
	{
		_internetGames.StopRefresh();
		_favorites.StopRefresh();
		_history.StopRefresh();
		_friendsGames.StopRefresh();

		SaveUserData();

		base.Close();
	}
}
