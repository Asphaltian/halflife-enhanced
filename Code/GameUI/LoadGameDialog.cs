using System;

namespace GoldSrc.GameUI;

public static class SavedGame
{
	private const int ElapsedTimeLength = 5;

	public static bool Parse( string shortName, KeyValues save )
	{
		var fileName = $"save/{shortName}";

		save.SetString( "ShortName", shortName );
		save.SetString( "FileName", fileName );

		if ( !Server.SaveReadNameAndComment( shortName, out var mapName, out var comment, out var fileTime ) )
			return false;

		save.SetString( "Map", mapName );

		var elapsedTime = "??";

		if ( comment.Length > ElapsedTimeLength )
		{
			elapsedTime = comment[^ElapsedTimeLength..];
			comment = comment[..^ElapsedTimeLength];

			var last = comment.Length - 1;

			while ( last > 0 && comment[last] == ' ' )
				last--;

			comment = comment[..(last + 1)];
		}

		string type = null;

		if ( fileName.Contains( "quick", StringComparison.Ordinal ) )
			type = "#GameUI_QuickSave";
		else if ( fileName.Contains( "autosave", StringComparison.Ordinal ) )
			type = "#GameUI_AutoSave";

		save.SetString( "Type", type );
		save.SetString( "Game", comment );
		save.SetString( "Elapsed Time", elapsedTime );

		var local = fileTime.ToLocalTime();

		save.SetString( "Time", $"{local:ddd MMM}{local.Day,3} {local:HH:mm:ss yyyy}" );
		save.SetInt( "timestamp", (int)new DateTimeOffset( fileTime ).ToUnixTimeSeconds() );

		return true;
	}

	public static int TimeStampSortFunc( ListPanel panel, ListPanelItem item1, ListPanelItem item2 )
	{
		return item1.Data.GetInt( "timestamp" ) < item2.Data.GetInt( "timestamp" ) ? 1 : -1;
	}

	public static void AddColumns( ListPanel gameList, Panel dialog )
	{
		gameList.AddColumnHeader( 0, "Game", "#GameUI_SavedGame", dialog.Scaled( 175 ), 0 );
		gameList.AddColumnHeader( 1, "Type", "#GameUI_Type", dialog.Scaled( 100 ), 0 );
		gameList.AddColumnHeader( 2, "Elapsed Time", "#GameUI_ElapsedTime", dialog.Scaled( 75 ), 0 );
		gameList.AddColumnHeader( 3, "Time", "#GameUI_TimeStamp", dialog.Scaled( 175 ), 0 );
		gameList.SetSortFunc( 3, TimeStampSortFunc );
		gameList.SetSortColumn( 3 );
	}

	public static void ScanSavedGames( ListPanel gameList )
	{
		foreach ( var file in Server.SaveGameFiles )
		{
			if ( file.StartsWith( "HLSave", StringComparison.OrdinalIgnoreCase ) )
				continue;

			var save = new KeyValues( "SavedGame" );

			if ( Parse( file, save ) )
				gameList.AddItem( save, false, false );
		}
	}
}

public sealed class LoadGameDialog : Frame
{
	private readonly ListPanel _gameList;

	public LoadGameDialog( Vgui vgui, Panel parent ) : base( vgui, parent, "LoadGameDialog" )
	{
		SetHdProportional( true );
		SetBounds( 0, 0, Scaled( 512 ), Scaled( 384 ) );
		SetMinimumSize( Scaled( 256 ), Scaled( 300 ) );
		SetSizeable( true );
		SetCloseSound( "sound/UI/launch_dnmenu1.wav" );

		BasePanel.PositionDialog( this );

		SetTitle( "#GameUI_LoadGame" );

		_ = new Label( vgui, this, "HelpText", "#GameUI_LoadGameHelp" );

		var load = new Button( vgui, this, "Load", "#GameUI_Load" );

		load.SetCommand( "Load" );

		var cancel = new Button( vgui, this, "Cancel", "#GameUI_Cancel" );

		cancel.SetCommand( "Close" );

		_gameList = new ListPanel( vgui, this, "listpanel_loadgame" );

		SavedGame.AddColumns( _gameList, this );
		SavedGame.ScanSavedGames( _gameList );

		_gameList.SortList();
		_gameList.SetSingleSelectedItem( _gameList.GetItemIDFromRow( 0 ) );

		LoadControlSettings( "Resource\\LoadGameDialog.res" );
	}

	public override void Close()
	{
		base.Close();

		MarkForDeletion();
	}

	public override void OnCommand( string command )
	{
		if ( !command.Equals( "Load", StringComparison.OrdinalIgnoreCase ) )
		{
			base.OnCommand( command );
			return;
		}

		if ( _gameList.GetItem( _gameList.GetSelectedItem( 0 ) )?.GetString( "ShortName" ) is not { Length: > 0 } shortName )
			return;

		Client.ClientCmd( $"load {shortName}" );

		Close();
	}
}

public sealed class SaveGameDialog : Frame
{
	private const int MaxNewSaveGames = 1000;

	private readonly ListPanel _gameList;

	public SaveGameDialog( Vgui vgui, Panel parent ) : base( vgui, parent, "SaveGameDialog" )
	{
		SetHdProportional( true );
		SetMinimumSize( Scaled( 300 ), Scaled( 300 ) );
		SetBounds( 0, 0, Scaled( 512 ), Scaled( 384 ) );
		SetSizeable( true );

		BasePanel.PositionDialog( this );

		SetTitle( "#GameUI_SaveGame" );

		_ = new Label( vgui, this, "HelpText", "#GameUI_SaveGameHelp" );

		var save = new Button( vgui, this, "Save", "#GameUI_Save" );

		save.SetCommand( "Save" );

		var cancel = new Button( vgui, this, "Cancel", "#GameUI_Cancel" );

		cancel.SetCommand( "Close" );

		_gameList = new ListPanel( vgui, this, "listpanel_loadgame" );

		SavedGame.AddColumns( _gameList, this );

		ScanSavedGames();

		LoadControlSettings( "Resource/SaveGameDialog.res" );
	}

	private void ScanSavedGames()
	{
		SavedGame.ScanSavedGames( _gameList );

		var newSave = new KeyValues( "SavedGame" );

		newSave.SetString( "Game", "#GameUI_SaveGame_NewSavedGame" );
		newSave.SetString( "Elapsed Time", "#GameUI_SaveGame_New" );
		newSave.SetString( "Time", "#GameUI_SaveGame_Current" );
		newSave.SetInt( "timestamp", int.MaxValue );

		var shortName = "error.sav";

		for ( var i = 0; i < MaxNewSaveGames; i++ )
		{
			var name = $"Half-Life-{i:000}.sav";

			if ( Server.SaveGameExists( name ) )
				continue;

			shortName = name;
			break;
		}

		newSave.SetString( "ShortName", shortName );

		_gameList.AddItem( newSave, false, false );
		_gameList.SortList();
		_gameList.SetSingleSelectedItem( _gameList.GetItemIDFromRow( 0 ) );
	}

	public override void Close()
	{
		base.Close();

		MarkForDeletion();
	}

	public override void OnCommand( string command )
	{
		if ( !command.Equals( "Save", StringComparison.OrdinalIgnoreCase ) )
		{
			base.OnCommand( command );
			return;
		}

		if ( _gameList.GetItem( _gameList.GetSelectedItem( 0 ) )?.GetString( "ShortName" ) is not { Length: > 0 } shortName )
			return;

		Client.ClientCmd( $"save {shortName}" );
		Client.Current?.HideGameUi();

		Close();
	}
}
