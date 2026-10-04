using System;

namespace GoldSrc.GameUI;

public sealed class PlayerListDialog : Frame
{
	private readonly ListPanel _playerList;
	private readonly Button _muteButton;

	private static GameClientExports GameClientExports => ClientDll.Loaded?.GameClientExports;

	public PlayerListDialog( Vgui vgui, Panel parent ) : base( vgui, parent, "PlayerListDialog" )
	{
		SetMinimumSize( Scaled( 300 ), Scaled( 240 ) );
		SetSize( Scaled( 320 ), Scaled( 240 ) );

		if ( GameClientExports is { } exports )
			SetTitle( vgui.Localize( "#GameUI_PlayerListDialogTitle" ).Replace( "%s1", exports.GetServerHostName() ) );
		else
			SetTitle( "#GameUI_CurrentPlayers" );

		_muteButton = new Button( vgui, this, "MuteButton", "" );

		_playerList = new ListPanel( vgui, this, "PlayerList" );
		_playerList.AddColumnHeader( 0, "Name", "#GameUI_PlayerName", Scaled( 128 ), 0 );
		_playerList.AddColumnHeader( 1, "Properties", "#GameUI_Properties", Scaled( 80 ), 0 );
		_playerList.SetEmptyListText( "#GameUI_NoOtherPlayersInGame" );

		LoadControlSettings( "Resource/PlayerListDialog.res" );
	}

	public override void Activate()
	{
		base.Activate();

		_playerList.DeleteAllItems();

		var maxClients = Client.Current?.MaxClients ?? 0;

		for ( var i = 1; i <= maxClients; i++ )
		{
			var data = new KeyValues( i.ToString() );
			var playerName = Client.Current.PlayerInfoValueForKey( i, "name" );

			if ( !string.IsNullOrEmpty( playerName ) )
			{
				data.SetString( "Name", playerName );
				data.SetInt( "index", i );

				_playerList.AddItem( data, false, false );
			}
		}

		RefreshPlayerProperties();

		_playerList.SetSingleSelectedItem( _playerList.GetItemIDFromRow( 0 ) );

		OnItemSelected();
	}

	private void RefreshPlayerProperties()
	{
		for ( var i = 0; i <= _playerList.GetItemCount(); i++ )
		{
			if ( _playerList.GetItem( i ) is not { } data )
				continue;

			var playerIndex = data.GetInt( "index" );
			var playerName = Client.Current?.PlayerInfoValueForKey( playerIndex, "name" );

			if ( playerName is null )
			{
				data.SetString( "properties", "Disconnected" );
				continue;
			}

			data.SetString( "name", playerName );

			var muted = GameClientExports is { } exports && exports.IsPlayerGameVoiceMuted( playerIndex );
			var bot = Client.Current?.PlayerInfoValueForKey( playerIndex, "*bot" );

			if ( bot is not null && bot.Equals( "1", StringComparison.OrdinalIgnoreCase ) )
				data.SetString( "properties", "CPU Player" );
			else
				data.SetString( "properties", muted ? "Muted" : "" );
		}

		_playerList.RereadAllItems();
	}

	private void OnItemSelected()
	{
		RefreshPlayerProperties();

		if ( _playerList.GetSelectedItemsCount() < 1 )
		{
			_muteButton.SetEnabled( false );
			_muteButton.SetText( "#GameUI_MuteIngameVoice" );

			return;
		}

		var data = _playerList.GetItem( _playerList.GetSelectedItem( 0 ) );
		var playerIndex = data.GetInt( "index" );
		var bot = Client.Current?.PlayerInfoValueForKey( playerIndex, "*bot" );
		var isBot = bot is not null && bot.Equals( "1", StringComparison.OrdinalIgnoreCase );
		var text = "#GameUI_MuteIngameVoice";

		if ( Client.Current?.PlayerInfoValueForKey( playerIndex, "name" ) is null )
			isBot = true;
		else if ( !isBot && GameClientExports is { } exports && exports.IsPlayerGameVoiceMuted( playerIndex ) )
			text = "#GameUI_UnmuteIngameVoice";

		_muteButton.SetText( text );
		_muteButton.SetEnabled( GameClientExports is not null && !isBot );
	}

	public override void OnMessage( Message message, Panel from )
	{
		if ( message.Name == "ItemSelected" )
		{
			OnItemSelected();
			return;
		}

		base.OnMessage( message, from );
	}

	public override void OnCommand( string command )
	{
		if ( !command.Equals( "Mute", StringComparison.OrdinalIgnoreCase ) )
		{
			base.OnCommand( command );
			return;
		}

		if ( GameClientExports is not { } exports )
			return;

		if ( _playerList.GetItem( _playerList.GetSelectedItem( 0 ) ) is not { } data )
			return;

		var playerIndex = data.GetInt( "index" );

		if ( exports.IsPlayerGameVoiceMuted( playerIndex ) )
			exports.UnmutePlayerGameVoice( playerIndex );
		else
			exports.MutePlayerGameVoice( playerIndex );

		RefreshPlayerProperties();
		OnItemSelected();
	}
}
