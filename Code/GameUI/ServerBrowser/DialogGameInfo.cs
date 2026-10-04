using System;
using System.Globalization;

namespace GoldSrc.GameUI;

public sealed class DialogServerPassword : Frame
{
	private const int MaxPasswordLength = 63;

	private readonly Label _gameLabel;
	private readonly TextEntry _passwordEntry;
	private readonly Button _connectButton;

	private int _serverID = -1;

	public DialogServerPassword( Vgui vgui, Panel parent ) : base( vgui, parent, "DialogServerPassword" )
	{
		SetSize( 320, 240 );
		SetDeleteSelfOnClose( true );
		SetSizeable( false );

		_ = new Label( vgui, this, "InfoLabel", "#ServerBrowser_ServerRequiresPassword" );

		_gameLabel = new Label( vgui, this, "GameLabel", "<game label>" );
		_passwordEntry = new TextEntry( vgui, this, "PasswordEntry" );
		_connectButton = new Button( vgui, this, "ConnectButton", "#ServerBrowser_Connect" );

		_passwordEntry.SetTextHidden( true );

		LoadControlSettings( "Servers/DialogServerPassword.res" );

		SetTitle( "#ServerBrowser_ServerRequiresPasswordTitle" );

		MoveToCenterOfScreen();
	}

	public void Activate( string serverName, int serverID )
	{
		_gameLabel.SetText( serverName );

		_serverID = serverID;

		_connectButton.SetAsDefaultButton( true );
		_passwordEntry.RequestFocus();

		Activate();
	}

	public override void OnCommand( string command )
	{
		if ( command.Equals( "Connect", StringComparison.OrdinalIgnoreCase ) )
		{
			var password = _passwordEntry.Text;

			if ( password.Length > MaxPasswordLength )
				password = password[..MaxPasswordLength];

			PostActionSignal( new Message( "JoinServerWithPassword" ) { Text = password, Int = _serverID } );
		}
		else if ( !command.Equals( "Close", StringComparison.OrdinalIgnoreCase ) )
		{
			base.OnCommand( command );
			return;
		}

		PostMessage( this, new Message( "Close" ) );
	}
}

public sealed class DialogGameInfo : Frame, IMatchmakingPingResponse, IMatchmakingPlayersResponse
{
	private const int RetryTime = 10000;
	private const int FirstRowY = 42;
	private const int RowPitch = 24;
	private const int RemovedRows = 2;

	private readonly Button _connectButton;
	private readonly Button _refreshButton;
	private readonly Label _infoLabel;
	private readonly ToggleButton _autoRetry;
	private readonly RadioButton _autoRetryAlert;
	private readonly RadioButton _autoRetryJoin;
	private readonly ListPanel _playerList;

	private GameServerItem _server;
	private ServerQuery _pingQuery;
	private int _requestRetry;
	private bool _connecting;
	private bool _serverNotResponding;
	private bool _serverFull;
	private bool _showAutoRetryToggle;
	private bool _playerListUpdatePending;
	private string _password = "";

	public ulong OwnerId => _server.OwnerId;

	public DialogGameInfo( Vgui vgui, Panel parent, GameServerItem server ) : base( vgui, parent, "DialogGameInfo" )
	{
		SetHdProportional( true );
		SetBounds( 0, 0, Scaled( 512 ), Scaled( 512 ) );
		SetMinimumSize( Scaled( 416 ), Scaled( 340 ) );
		SetDeleteSelfOnClose( true );

		_connectButton = new Button( vgui, this, "Connect", "#ServerBrowser_JoinGame" );

		var closeButton = new Button( vgui, this, "Close", "#ServerBrowser_Close" );

		_refreshButton = new Button( vgui, this, "Refresh", "#ServerBrowser_Refresh" );
		_infoLabel = new Label( vgui, this, "InfoLabel", "" );

		_autoRetry = new ToggleButton( vgui, this, "AutoRetry", "#ServerBrowser_AutoRetry" );
		_autoRetry.AddActionSignalTarget( this );

		_autoRetryAlert = new RadioButton( vgui, this, "AutoRetryAlert", "#ServerBrowser_AlertMeWhenSlotOpens" );
		_autoRetryJoin = new RadioButton( vgui, this, "AutoRetryJoin", "#ServerBrowser_JoinWhenSlotOpens" );

		_playerList = new ListPanel( vgui, this, "PlayerList" );
		_playerList.AddColumnHeader( 0, "PlayerName", "#ServerBrowser_PlayerName", Scaled( 156 ), 0 );
		_playerList.AddColumnHeader( 1, "Score", "#ServerBrowser_Score", Scaled( 64 ), 0 );
		_playerList.AddColumnHeader( 2, "Time", "#ServerBrowser_Time", Scaled( 64 ), 0 );
		_playerList.SetSortFunc( 2, PlayerTimeColumnSortFunc );

		PostMessage( _playerList, new Message( "SetSortColumn" ) { Int = 2 } );
		PostMessage( _playerList, new Message( "SetSortColumn" ) { Int = 1 } );
		PostMessage( _playerList, new Message( "SetSortColumn" ) { Int = 1 } );

		_autoRetryAlert.SetSelected( true );

		_connectButton.SetCommand( new Message( "Connect" ) );
		closeButton.SetCommand( new Message( "Close" ) );
		_refreshButton.SetCommand( new Message( "Refresh" ) );

		_server = new GameServerItem { SteamId = server.SteamId, OwnerId = server.OwnerId };

		RequestInfo();

		vgui.AddTickSignal( this );

		LoadLayout( "Servers/DialogGameInfo.res" );

		MoveToCenterOfScreen();
	}

	private void MoveRow( string labelName, string textName, int row )
	{
		var y = Scaled( FirstRowY + (row * RowPitch) );

		if ( FindChildByName( labelName ) is { } label )
			label.SetPos( label.X, y );

		if ( FindChildByName( textName ) is { } text )
			text.SetPos( text.X, y );
	}

	private void LoadLayout( string resourceName )
	{
		LoadControlSettings( resourceName );

		FindChildByName( "ServerIPLabel" )?.SetVisible( false );
		FindChildByName( "ServerIPText" )?.SetVisible( false );
		FindChildByName( "Label1" )?.SetVisible( false );
		FindChildByName( "SecureText" )?.SetVisible( false );

		MoveRow( "GameLabel", "GameText", 1 );
		MoveRow( "MapLabel", "MapText", 2 );
		MoveRow( "PlayersLabel", "PlayersText", 3 );
		MoveRow( "PingLabel", "PingText", 4 );

		var removed = Scaled( RemovedRows * RowPitch );

		_playerList.SetBounds( _playerList.X, _playerList.Y - removed, _playerList.Wide, _playerList.Tall + removed );
	}

	private void RequestPlayerInfo()
	{
		_playerListUpdatePending = true;

		MatchmakingServers.PlayerDetails( _server, this );
	}

	public void AddPlayerToList( string name, int score, float timePlayed )
	{
		if ( _playerListUpdatePending )
		{
			_playerListUpdatePending = false;

			_playerList.DeleteAllItems();
		}

		if ( Math.Max( _server.Players - _server.BotPlayers, 0 ) == 0 )
			return;

		var player = new KeyValues( "player" );
		var seconds = (int)timePlayed;
		var minutes = seconds / 60;
		var hours = minutes / 60;

		minutes %= 60;

		player.SetString( "PlayerName", name );
		player.SetInt( "Score", score );
		player.SetInt( "TimeSec", seconds );

		if ( hours != 0 )
			player.SetString( "Time", $"{hours}h {minutes}m {seconds % 60}s" );
		else if ( minutes != 0 )
			player.SetString( "Time", $"{minutes}m {seconds % 60}s" );
		else
			player.SetString( "Time", $"{seconds % 60}s" );

		_playerList.AddItem( player, 0, false, true );
	}

	public void PlayersFailedToRespond()
	{
	}

	public void PlayersRefreshComplete()
	{
	}

	private static int PlayerTimeColumnSortFunc( ListPanel panel, ListPanelItem item1, ListPanelItem item2 )
	{
		var time1 = item1.Data.GetInt( "TimeSec" );
		var time2 = item2.Data.GetInt( "TimeSec" );

		if ( time2 < time1 )
			return -1;

		return time1 < time2 ? 1 : 0;
	}

	public void Run( string titleName )
	{
		SetTitle( Vgui.Localize( "#ServerBrowser_GameInfoWithNameTitle" ).Replace( "%game%", titleName ?? "" ) );

		RequestInfo();
		Activate();
	}

	private void SendPing()
	{
		_requestRetry = Vgui.TimeMillis + RetryTime;

		MatchmakingServers.CancelServerQuery( _pingQuery );

		_pingQuery = MatchmakingServers.PingServer( _server.OwnerId, this );
	}

	private void RequestInfo()
	{
		if ( _requestRetry == 0 )
			SendPing();
	}

	public void Connect()
	{
		_connecting = true;
		_serverNotResponding = false;
		_serverFull = false;

		InvalidateLayout();

		_requestRetry = 0;

		SendPing();
	}

	private void OnRefresh()
	{
		_requestRetry = 0;

		SendPing();
	}

	private void OnButtonToggled( Panel panel )
	{
		if ( panel == _autoRetry )
		{
			var selected = _autoRetry.IsSelected;
			var growSize = Scaled( 60 );

			if ( !selected )
				growSize = -growSize;

			var x = X;
			var y = Y;
			var wide = Wide;
			var tall = Tall;

			SetMinimumSize( Scaled( 416 ), Scaled( 340 ) );

			LoadLayout( selected ? "Servers/DialogGameInfo_AutoRetry.res" : "Servers/DialogGameInfo.res" );

			SetBounds( x, y, wide, tall + growSize );
			PerformLayout();

			_autoRetryAlert.SetSelected( true );

			InvalidateLayout();
		}

		InvalidateLayout();
	}

	private void OnJoinServerWithPassword( string password )
	{
		_password = password ?? "";

		Connect();
	}

	public void OnConnectToGame( ulong ownerId )
	{
		if ( _server.OwnerId == ownerId )
			Close();
	}

	public override void OnTick()
	{
		if ( _requestRetry != 0 && _requestRetry < Vgui.TimeMillis )
		{
			_requestRetry = 0;

			RequestInfo();
		}
	}

	private void SetControlString( string controlName, string text )
	{
		switch ( FindChildByName( controlName ) )
		{
			case TextEntry entry:
				entry.SetText( text );
				break;

			case Label label:
				label.SetText( text );
				break;
		}
	}

	public override void PerformLayout()
	{
		base.PerformLayout();

		SetControlString( "ServerText", _server.ServerName );
		SetControlString( "GameText", _server.GameDescription );
		SetControlString( "MapText", _server.Map );
		SetControlString( "GameTags", _server.GameTags );

		var players = Math.Max( _server.Players - _server.BotPlayers, 0 );

		SetControlString( "PlayersText", _server.MaxPlayers < 1 ? "" : $"{players} / {_server.MaxPlayers}" );

		if ( _server.SteamId == 0 )
		{
			_connectButton.SetEnabled( false );
		}
		else
		{
			_connectButton.SetEnabled( true );

			_autoRetryAlert.SetVisible( _autoRetry.IsSelected );
			_autoRetryJoin.SetVisible( _autoRetry.IsSelected );
		}

		SetControlString( "PingText", _server.HadSuccessfulResponse ? _server.Ping.ToString( CultureInfo.InvariantCulture ) : "" );

		if ( !_autoRetry.IsSelected )
		{
			if ( _serverFull )
				_infoLabel.SetText( "#ServerBrowser_CouldNotConnectServerFull" );
			else
				_infoLabel.SetText( _serverNotResponding ? "#ServerBrowser_ServerNotResponding" : "" );
		}
		else if ( _server.Players < _server.MaxPlayers )
		{
			_infoLabel.SetText( "#ServerBrowser_PressJoinToConnect" );
		}
		else
		{
			_infoLabel.SetText( _autoRetryJoin.IsSelected ? "#ServerBrowser_JoinWhenSlotIsFree" : "#ServerBrowser_AlertWhenSlotIsFree" );
		}

		_playerList.SetEmptyListText( !_server.HadSuccessfulResponse || players != 0 ? "#ServerBrowser_ServerNotResponding" : "#ServerBrowser_ServerHasNoPlayers" );

		_autoRetry.SetVisible( _showAutoRetryToggle );
	}

	public void ServerResponded( GameServerItem server )
	{
		_pingQuery = null;
		_server = server;

		if ( !_connecting )
		{
			if ( !_autoRetry.IsSelected || server.MaxPlayers <= server.Players )
			{
				RequestPlayerInfo();
			}
			else
			{
				Surface.PlaySound( "Servers/game_ready.wav" );

				if ( _autoRetryJoin.IsSelected )
					ConnectToServer();
			}
		}
		else
		{
			ConnectToServer();
		}

		_serverNotResponding = false;

		InvalidateLayout();
	}

	public void ServerFailedToRespond()
	{
		if ( !_server.HadSuccessfulResponse )
			_serverNotResponding = true;

		InvalidateLayout();
	}

	private void ConnectToServer()
	{
		_connecting = false;

		if ( _server.Password && _password.Length == 0 )
		{
			var box = new DialogServerPassword( Vgui, this );

			box.AddActionSignalTarget( this );
			box.Activate( _server.ServerName, 0 );

			return;
		}

		if ( _server.MaxPlayers <= _server.Players )
		{
			_serverFull = true;
			_showAutoRetryToggle = true;

			InvalidateLayout();

			return;
		}

		if ( _password.Length != 0 )
			Client.ClientCmd( $"password \"{_password}\"" );

		Client.ClientCmd( $"connect {_server.SteamId}" );

		PostMessage( this, new Message( "Close" ) );
	}

	public override void OnMessage( Message message, Panel from )
	{
		switch ( message.Name )
		{
			case "Connect":
				Connect();
				break;

			case "Refresh":
				OnRefresh();
				break;

			case "ButtonToggled":
			case "RadioButtonChecked":
				OnButtonToggled( from );
				break;

			case "JoinServerWithPassword":
				OnJoinServerWithPassword( message.Text );
				break;

			default:
				base.OnMessage( message, from );
				break;
		}
	}
}
