using System;

namespace GoldSrc.GameUI;

public class InternetGames : BaseGamesPage
{
	private const float MinimumSortTime = 1.5f;
	private const float MaximumSortTime = 1f;

	private float _lastSort;
	private bool _dirty;
	private bool _requireUpdate = true;
	private bool _anyServersRespondedToQuery;

	public InternetGames( Vgui vgui, ServerBrowserDialog parent, string name = "InternetGames", MatchMakingType type = MatchMakingType.InternetServer ) : base( vgui, parent, name, type )
	{
		LoadFilterSettings();

		vgui.AddTickSignal( this, 1000 );
	}

	public override bool SupportsItem( GameListInterfaceItem item ) => item is GameListInterfaceItem.Filters or GameListInterfaceItem.GetNewList;

	protected override void OnPageShow()
	{
		CurrentPage = this;

		if ( GameList.GetItemCount() == 0 && Dialog.Visible )
			base.OnPageShow();
	}

	public override void OnTick()
	{
		if ( !_dirty )
			return;

		var time = RealTime.Now;

		if ( time - _lastSort < MinimumSortTime )
			return;

		var input = Vgui.Input;

		if ( !input.IsMouseDown( MouseCode.Left ) && !input.IsMouseDown( MouseCode.Right ) )
		{
			_dirty = false;
			_lastSort = time;

			GameList.SortList();

			return;
		}

		_lastSort = time - MinimumSortTime + MaximumSortTime;
	}

	public override void PerformLayout()
	{
		if ( _requireUpdate && Dialog.Visible )
		{
			Vgui.PostMessage( this, new Message( "GetNewServerList" ), this, 0.1f );

			_requireUpdate = false;
		}

		base.PerformLayout();
	}

	public override void GetNewServerList()
	{
		base.GetNewServerList();

		UpdateStatus();

		_requireUpdate = false;
		_anyServersRespondedToQuery = false;

		GameList.DeleteAllItems();
	}

	public override void ServerResponded( ServerListRequest request, int server )
	{
		_dirty = true;

		base.ServerResponded( request, server );

		_anyServersRespondedToQuery = true;
	}

	public override void ServerFailedToRespond( ServerListRequest request, int server )
	{
		_dirty = true;

		if ( MatchmakingServers.GetServerDetails( Request, server ) is not { } item )
			return;

		if ( item.HadSuccessfulResponse )
		{
			ServerResponded( request, server );
			return;
		}

		if ( HasServer( server ) )
			RemoveServer( server );

		ServerRefreshCount++;
	}

	public override void RefreshComplete( ServerListRequest request, MatchMakingServerResponse response )
	{
		if ( request != Request )
			return;

		SetRefreshing( false );
		UpdateFilterSettings();

		if ( response == MatchMakingServerResponse.ServerFailedToRespond )
			GameList.SetEmptyListText( "#ServerBrowser_MasterServerNotResponsive" );
		else if ( _anyServersRespondedToQuery )
			GameList.SetEmptyListText( "#ServerBrowser_NoInternetGames" );
		else if ( response == MatchMakingServerResponse.NoServersListedOnMasterServer )
			GameList.SetEmptyListText( "#ServerBrowser_MasterServerHasNoServersListed" );
		else
			GameList.SetEmptyListText( "#ServerBrowser_NoInternetGamesResponded" );

		_dirty = false;
		_lastSort = RealTime.Now;

		if ( Visible )
			GameList.SortList();

		UpdateStatus();
	}

	protected override void OnRefreshServer( int serverID )
	{
		base.OnRefreshServer( serverID );

		Dialog.UpdateStatusText( "#ServerBrowser_GettingNewServerList" );
	}

	protected override void OnOpenContextMenu( int itemID )
	{
		if ( GameList.GetSelectedItemsCount() == 0 )
			return;

		var serverID = GameList.GetItemUserData( GameList.GetSelectedItem( 0 ) );

		Dialog.GetContextMenu( GameList ).ShowMenu( this, serverID, true, true, true, true );
	}

	public override void OnMessage( Message message, Panel from )
	{
		if ( message.Name == "GetNewServerList" )
		{
			GetNewServerList();
			return;
		}

		base.OnMessage( message, from );
	}
}

public sealed class FavoriteGames : BaseGamesPage
{
	private bool _refreshOnListReload;

	public FavoriteGames( Vgui vgui, ServerBrowserDialog parent ) : base( vgui, parent, "FavoriteGames", MatchMakingType.FavoritesServer )
	{
	}

	public override bool SupportsItem( GameListInterfaceItem item ) => item is GameListInterfaceItem.Filters or GameListInterfaceItem.AddServer or GameListInterfaceItem.AddCurrentServer;

	private string EmptyListText => FavoriteGameList.Count == 0 ? "#ServerBrowser_NoFavoriteServers" : "#ServerBrowser_NoInternetGamesResponded";

	public void LoadFavoritesList()
	{
		GameList.SetEmptyListText( EmptyListText );

		if ( _refreshOnListReload )
		{
			_refreshOnListReload = false;

			StartRefresh();
		}
	}

	public void OnConnectToGame() => AddCurrentServer.SetEnabled( true );

	public void OnDisconnectFromGame() => AddCurrentServer.SetEnabled( false );

	public override void RefreshComplete( ServerListRequest request, MatchMakingServerResponse response )
	{
		SetRefreshing( false );

		GameList.SetEmptyListText( EmptyListText );
		GameList.SortList();
	}

	protected override void OnOpenContextMenu( int itemID )
	{
		var menu = Dialog.GetContextMenu( GameList );

		if ( GameList.GetSelectedItemsCount() == 0 )
		{
			menu.ShowMenu( this, -1, false, false, false, false );
			return;
		}

		var serverID = GameList.GetItemUserData( GameList.GetSelectedItem( 0 ) );

		menu.ShowMenu( this, serverID, true, true, true, false );
		menu.AddMenuItem( "RemoveServer", "#ServerBrowser_RemoveServerFromFavorites", new Message( "RemoveFromFavorites" ), this );
	}

	private void OnRemoveFromFavorites()
	{
		for ( var i = 0; i < GameList.GetSelectedItemsCount(); i++ )
		{
			var serverID = GameList.GetItemUserData( GameList.GetSelectedItem( i ) );

			if ( MatchmakingServers.GetServerDetails( Request, serverID ) is { } server )
				FavoriteGameList.RemoveFavoriteGame( server.OwnerId, FavoriteGameList.FlagFavorite );
		}

		UpdateStatus();
		InvalidateLayout();
	}

	public override void OnCommand( string command )
	{
		if ( command.Equals( "AddCurrentServer", StringComparison.OrdinalIgnoreCase ) )
		{
			if ( Dialog.CurrentConnectedServer is { } server )
			{
				FavoriteGameList.AddFavoriteGame( server, FavoriteGameList.FlagFavorite, (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds() );

				_refreshOnListReload = true;
			}

			return;
		}

		base.OnCommand( command );
	}

	public override void OnMessage( Message message, Panel from )
	{
		if ( message.Name == "RemoveFromFavorites" )
		{
			OnRemoveFromFavorites();
			return;
		}

		base.OnMessage( message, from );
	}

	public void SetRefreshOnListReload() => _refreshOnListReload = true;
}

public sealed class HistoryGames : BaseGamesPage
{
	private const int ColumnLastPlayed = 9;

	private bool _refreshOnListReload;

	public HistoryGames( Vgui vgui, ServerBrowserDialog parent ) : base( vgui, parent, "HistoryGames", MatchMakingType.HistoryServer )
	{
		GameList.AddColumnHeader( ColumnLastPlayed, "LastPlayed", "#ServerBrowser_LastPlayed", 100, 0 );
		GameList.SetSortFunc( ColumnLastPlayed, LastPlayedCompare );
		GameList.SetSortColumn( ColumnLastPlayed );
	}

	private static int LastPlayedCompare( ListPanel panel, ListPanelItem item1, ListPanelItem item2 )
	{
		if ( CurrentPage is null )
			return 0;

		var server1 = CurrentPage.GetServer( item1.UserData );
		var server2 = CurrentPage.GetServer( item2.UserData );

		if ( server1 is null )
			return server2 is null ? 0 : -1;

		if ( server2 is null )
			return 1;

		if ( server2.TimeLastPlayed < server1.TimeLastPlayed )
			return -1;

		return server2.TimeLastPlayed > server1.TimeLastPlayed ? 1 : 0;
	}

	public override bool SupportsItem( GameListInterfaceItem item ) => item == GameListInterfaceItem.Filters;

	public void LoadHistoryList()
	{
		GameList.SetEmptyListText( "#ServerBrowser_NoServersPlayed" );

		if ( _refreshOnListReload )
		{
			_refreshOnListReload = false;

			StartRefresh();
		}
	}

	public void SetRefreshOnListReload() => _refreshOnListReload = true;

	public override void RefreshComplete( ServerListRequest request, MatchMakingServerResponse response )
	{
		SetRefreshing( false );

		GameList.SetEmptyListText( "#ServerBrowser_NoServersPlayed" );
		GameList.SortList();
	}

	protected override void OnOpenContextMenu( int itemID )
	{
		var menu = Dialog.GetContextMenu( GameList );

		if ( GameList.GetSelectedItemsCount() == 0 )
		{
			menu.ShowMenu( this, -1, false, false, false, false );
			return;
		}

		var serverID = GameList.GetItemUserData( GameList.GetSelectedItem( 0 ) );

		menu.ShowMenu( this, serverID, true, true, true, true );
		menu.AddMenuItem( "RemoveServer", "#ServerBrowser_RemoveServerFromHistory", new Message( "RemoveFromHistory" ), this );
	}

	private void OnRemoveFromHistory()
	{
		for ( var i = GameList.GetSelectedItemsCount() - 1; i >= 0; i-- )
		{
			var serverID = GameList.GetItemData( GameList.GetSelectedItem( i ) ).UserData;

			if ( MatchmakingServers.GetServerDetails( Request, serverID ) is { } server )
				FavoriteGameList.RemoveFavoriteGame( server.OwnerId, FavoriteGameList.FlagHistory );
		}

		UpdateStatus();
		InvalidateLayout();
	}

	public override void OnMessage( Message message, Panel from )
	{
		if ( message.Name == "RemoveFromHistory" )
		{
			OnRemoveFromHistory();
			return;
		}

		base.OnMessage( message, from );
	}
}

public sealed class FriendsGames : BaseGamesPage
{
	public FriendsGames( Vgui vgui, ServerBrowserDialog parent ) : base( vgui, parent, "FriendsGames", MatchMakingType.FriendsServer )
	{
	}

	public override bool SupportsItem( GameListInterfaceItem item ) => item == GameListInterfaceItem.Filters;

	public override void RefreshComplete( ServerListRequest request, MatchMakingServerResponse response )
	{
		SetRefreshing( false );

		GameList.SortList();
		GameList.SetEmptyListText( "#ServerBrowser_NoFriendsServers" );
	}

	protected override void OnOpenContextMenu( int itemID )
	{
		if ( GameList.GetSelectedItemsCount() == 0 )
			return;

		var serverID = GameList.GetItemData( GameList.GetSelectedItem( 0 ) ).UserData;

		Dialog.GetContextMenu( GameList ).ShowMenu( this, serverID, true, true, true, true );
	}
}
