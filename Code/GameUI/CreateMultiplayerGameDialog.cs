using System;
using System.Globalization;
using System.Text;

namespace GoldSrc.GameUI;

public sealed class ServerDescription : Description
{
	public ServerDescription()
	{
		Hint =
			"// NOTE:  THIS FILE IS AUTOMATICALLY REGENERATED, \r\n" +
			"//DO NOT EDIT THIS HEADER, YOUR COMMENTS WILL BE LOST IF YOU DO\r\n" +
			"// Multiplayer options script\r\n" +
			"//\r\n" +
			"// Format:\r\n" +
			"//  Version [float]\r\n" +
			"//  Options description followed by \r\n" +
			"//  Options defaults\r\n" +
			"//\r\n" +
			"// Option description syntax:\r\n" +
			"//\r\n" +
			"//  \"cvar\" { \"Prompt\" { type [ type info ] } { default } }\r\n" +
			"//\r\n" +
			"//  type = \r\n" +
			"//   BOOL   (a yes/no toggle)\r\n" +
			"//   STRING\r\n" +
			"//   NUMBER\r\n" +
			"//   LIST\r\n" +
			"//\r\n" +
			"// type info:\r\n" +
			"// BOOL                 no type info\r\n" +
			"// NUMBER       min max range, use -1 -1 for no limits\r\n" +
			"// STRING       no type info\r\n" +
			"// LIST          delimited list of options value pairs\r\n" +
			"//\r\n" +
			"//\r\n" +
			"// default depends on type\r\n" +
			"// BOOL is \"0\" or \"1\"\r\n" +
			"// NUMBER is \"value\"\r\n" +
			"// STRING is \"value\"\r\n" +
			"// LIST is \"index\", where index \"0\" is the first element of the list\r\n" +
			"\r\n" +
			"\r\n";

		DescriptionName = "SERVER_OPTIONS";
	}

	protected override void WriteScriptHeader( StringBuilder file )
	{
		var time = DateTime.Now;
		var hour = time.Hour;
		var period = "AM";

		if ( hour > 12 )
		{
			hour -= 12;
			period = "PM";
		}

		if ( hour == 0 )
			hour = 12;

		var stamp = string.Format( CultureInfo.InvariantCulture, "{0:ddd} {0:MMM}{1,3} {2:00}:{0:mm}:{0:ss}", time, time.Day, hour );

		file.Append( Hint );
		file.Append( "// Half-Life Server Configuration Layout Script (stores last settings chosen, too)\r\n" );
		file.Append( $"// File generated:  {stamp} {period}\r\n" );
		file.Append( "//\r\n//\r\n// Cvar\t-\tSetting\r\n\r\n" );
		file.Append( "VERSION 1.0\r\n\r\n" );
		file.Append( "DESCRIPTION SERVER_OPTIONS\r\n{\r\n" );
	}
}

public sealed class CreateMultiplayerGameGameplayPage : PropertyPage
{
	private const string ScriptFileName = "settings.scr";
	private const int ValueLength = 128;
	private const int NameLength = 128;

	private readonly ServerDescription _description;
	private readonly PanelListPanel _optionsList;
	private readonly List<MultiplayerControl> _list = [];

	public CreateMultiplayerGameGameplayPage( Vgui vgui, Panel parent, string name ) : base( vgui, parent, name )
	{
		_optionsList = new PanelListPanel( vgui, this, "GameOptions" );

		_description = new ServerDescription();
		_description.InitFromFile( ScriptFileName );

		LoadControlSettings( "Resource/CreateMultiplayerGameGameplayPage.res" );

		LoadGameOptionsList();
	}

	public int GetMaxPlayers() => Common.Atoi( GetValue( "maxplayers", "32" ) );

	public string GetPassword() => GetValue( "sv_password", "" );

	public string GetHostName() => GetValue( "hostname", "Half-Life" );

	private string GetValue( string cvarName, string defaultValue )
	{
		foreach ( var control in _list )
		{
			if ( control.Control is null || !string.Equals( control.Name, cvarName, StringComparison.OrdinalIgnoreCase ) )
				continue;

			var value = control.Control switch
			{
				TextEntry entry => entry.Text,
				Label label => label.Text,
				_ => defaultValue
			};

			return value.Length > ValueLength - 1 ? value[..(ValueLength - 1)] : value;
		}

		return defaultValue;
	}

	protected override void OnApplyChanges()
	{
		GatherCurrentValues();

		_description.WriteToConfig();

		var file = new StringBuilder();

		_description.WriteToScriptFile( file );

		Server.WriteFile( ScriptFileName, Encoding.Latin1.GetBytes( file.ToString() ) );
	}

	private void DestroyControls()
	{
		foreach ( var control in _list )
		{
			control.Control?.MarkForDeletion();
			control.Prompt?.MarkForDeletion();
			control.MarkForDeletion();
		}

		_list.Clear();
	}

	private static string LocalPlayerName()
	{
		var name = new StringBuilder();

		foreach ( var character in Sandbox.Utility.Steam.PersonaName ?? "" )
		{
			if ( name.Length == NameLength - 1 )
				break;

			if ( character < 0x80 )
				name.Append( character );
		}

		if ( name.Length == 0 )
			name.Append( "Someone" );

		return name.ToString().TrimEnd( ' ' );
	}

	private void LoadGameOptionsList()
	{
		DestroyControls();

		var hostName = $"{LocalPlayerName()}'s Server";

		if ( hostName.Length > 254 )
			hostName = hostName[..254];

		foreach ( var scriptObject in _description.Objects )
		{
			var control = new MultiplayerControl( Vgui, _optionsList, scriptObject.CvarName ) { Type = scriptObject.Type };

			switch ( control.Type )
			{
				case ScriptObjectType.Bool:
					{
						var box = new CheckButton( Vgui, control, "DescCheckButton", scriptObject.Prompt );

						box.SetSelected( scriptObject.DefValueFloat != 0f );

						control.Control = box;
						break;
					}

				case ScriptObjectType.Number:
				case ScriptObjectType.String:
					{
						var edit = new TextEntry( Vgui, control, "DescEdit" );

						edit.InsertString( scriptObject.CvarName == "hostname" ? hostName : scriptObject.DefValue );

						control.Control = edit;
						break;
					}

				case ScriptObjectType.List:
					{
						var combo = new ComboBox( Vgui, control, "DescEdit", 5, false );

						foreach ( var item in scriptObject.ListItems )
							combo.AddItem( item.ItemText );

						combo.ActivateItemByRow( (int)scriptObject.DefValueFloat );

						control.Control = combo;
						break;
					}
			}

			if ( control.Type != ScriptObjectType.Bool )
			{
				control.Prompt = new Label( Vgui, control, "DescLabel", "" );
				control.Prompt.SetContentAlignment( Alignment.West );
				control.Prompt.SetTextInset( Scaled( 5 ), 0 );
				control.Prompt.SetText( scriptObject.Prompt );
			}

			control.ScriptObject = scriptObject;
			control.SetSize( Scaled( 100 ), Scaled( 28 ) );

			_optionsList.AddItem( control );
			_list.Add( control );
		}
	}

	private void GatherCurrentValues()
	{
		foreach ( var control in _list )
		{
			var scriptObject = control.ScriptObject;

			if ( control.Control is null )
			{
				scriptObject.SetCurValue( scriptObject.DefValue );
				continue;
			}

			string value;

			switch ( scriptObject.Type )
			{
				case ScriptObjectType.Bool:
					value = ((CheckButton)control.Control).IsSelected ? "1" : "0";
					break;

				case ScriptObjectType.Number:
				case ScriptObjectType.String:
					value = ((TextEntry)control.Control).Text;
					break;

				case ScriptObjectType.List:
					{
						var text = ((ComboBox)control.Control).Text;
						var item = scriptObject.ListItems.Find( x => string.Equals( Vgui.Localize( x.ItemText ), text, StringComparison.OrdinalIgnoreCase ) );

						value = item is not null ? item.Value : scriptObject.DefValue;
						break;
					}

				default:
					value = scriptObject.DefValue;
					break;
			}

			scriptObject.SetCurValue( ScriptObject.StripInvalidCharacters( value ) );
		}
	}
}

public sealed class CreateMultiplayerGameServerPage : PropertyPage
{
	public const string RandomMap = "< Random Map >";

	private const int DefaultMaxPlayers = 20;

	private readonly ComboBox _mapList;
	private readonly TextEntry _botQuotaCombo;
	private readonly CheckButton _enableBotsCheck;
	private readonly CheckButton _enableSteamNetworkingCheck;
	private readonly CvarToggleCheckButton _tutorCheck;
	private readonly List<string> _mapNames = [];

	public CreateMultiplayerGameServerPage( Vgui vgui, Panel parent, string name ) : base( vgui, parent, name )
	{
		_mapList = new ComboBox( vgui, this, "MapList", 12, false );
		_botQuotaCombo = new TextEntry( vgui, this, "BotQuotaCombo" );
		_tutorCheck = new CvarToggleCheckButton( vgui, this, "CheckButtonTutor", "#CStrike_Tutor_Enabled", "tutor_enable" );
		_enableBotsCheck = new CheckButton( vgui, this, "EnableBotsCheck", "Enable bots" );
		_enableSteamNetworkingCheck = new CheckButton( vgui, this, "EnableSteamNetworkingCheck", "Enable Steam Networking" );

		LoadControlSettings( "Resource/CreateMultiplayerGameServerPage.res" );

		_enableSteamNetworkingCheck.SetVisible( false );

		LoadMapList();

		if ( !ModInfo.GameName.Equals( "Condition Zero", StringComparison.OrdinalIgnoreCase ) )
			_tutorCheck.SetVisible( false );

		var maxClients = Client.Current?.MaxClients ?? 0;

		if ( maxClients < 2 )
			maxClients = DefaultMaxPlayers;

		SetControlString( "ServerNameEdit", ModInfo.GameName );
		SetControlString( "PasswordEdit", Cvar.GetString( "sv_password" ) );

		var maxPlayers = maxClients.ToString( CultureInfo.InvariantCulture );

		SetControlString( "MaxPlayersEdit", maxPlayers.Length > 2 ? maxPlayers[..2] : maxPlayers );
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

	public void SetBotQuota( int quota ) => _botQuotaCombo.SetText( quota.ToString( CultureInfo.InvariantCulture ) );

	public void SetBotsEnabled( bool state ) => _enableBotsCheck.SetSelected( state );

	public void SetSteamNetworkingEnabled( bool state ) => _enableSteamNetworkingCheck.SetSelected( state );

	public int GetBotQuota() => Common.Atoi( _botQuotaCombo.Text );

	public bool GetBotsEnabled() => _enableBotsCheck.IsSelected;

	public bool GetSteamNetworkingEnabled() => _enableSteamNetworkingCheck.IsSelected;

	public bool IsRandomMapSelected()
	{
		return string.Equals( _mapList.GetActiveItemUserData()?.GetString( "mapname" ) ?? "", RandomMap, StringComparison.OrdinalIgnoreCase );
	}

	public string GetMapName()
	{
		var count = _mapList.GetItemCount();

		if ( count <= 1 )
			return null;

		var mapName = _mapList.GetActiveItemUserData()?.GetString( "mapname" ) ?? "";

		if ( mapName == RandomMap )
			mapName = _mapList.GetItemUserData( Game.Random.Next( 1, count ) )?.GetString( "mapname" ) ?? "";

		return mapName;
	}

	public void SetMap( string mapName )
	{
		for ( var i = 0; i < _mapList.GetItemCount(); i++ )
		{
			if ( !_mapList.IsItemIDValid( i ) )
				continue;

			if ( string.Equals( _mapList.GetItemUserData( i )?.GetString( "mapname" ) ?? "", mapName, StringComparison.OrdinalIgnoreCase ) )
			{
				_mapList.ActivateItem( i );
				break;
			}
		}
	}

	private void LoadMaps()
	{
		var hiddenMaps = ModInfo.HiddenMaps;
		var halfLife = ModInfo.GameName.Equals( "Half-Life", StringComparison.OrdinalIgnoreCase );
		var opposingForce = ModInfo.GameName.Equals( "Opposing Force", StringComparison.OrdinalIgnoreCase );

		foreach ( var file in Server.FindFiles( "maps", ".bsp" ) )
		{
			var mapName = file.Length > 255 ? file[..255] : file;
			var extension = mapName.IndexOf( ".bsp", StringComparison.Ordinal );

			if ( extension >= 0 )
				mapName = mapName[..extension];

			if ( halfLife )
			{
				if ( mapName.Length > 2 && (mapName[0] == 'c' || mapName[0] == 't') && mapName[2] == 'a' && mapName[1] >= '0' && mapName[1] <= '5' )
					continue;

				if ( mapName.StartsWith( "hldemo", StringComparison.Ordinal ) )
					continue;
			}

			if ( opposingForce && mapName.StartsWith( "of", StringComparison.Ordinal ) )
				continue;

			if ( hiddenMaps.Length != 0 && mapName.Contains( hiddenMaps, StringComparison.Ordinal ) )
				continue;

			if ( !_mapNames.Exists( x => string.Equals( x, mapName, StringComparison.OrdinalIgnoreCase ) ) )
				_mapNames.Add( mapName );
		}
	}

	private static int CompareNames( string a, string b ) => string.CompareOrdinal( a.ToLowerInvariant(), b.ToLowerInvariant() );

	private void LoadMapList()
	{
		_mapList.DeleteAllItems();
		_mapNames.Clear();

		var random = new KeyValues( "data" );

		random.SetString( "mapname", RandomMap );

		_mapList.AddItem( "#GameUI_RandomMap", random );

		LoadMaps();

		_mapNames.Sort( CompareNames );

		foreach ( var mapName in _mapNames )
		{
			var data = new KeyValues( "data" );

			data.SetString( "mapname", mapName );

			_mapList.AddItem( mapName, data );
		}

		_mapNames.Clear();

		_mapList.ActivateItem( 0 );
	}

	protected override void OnApplyChanges()
	{
		if ( ModInfo.GameName.Equals( "Condition Zero", StringComparison.OrdinalIgnoreCase ) )
			_tutorCheck.ApplyChanges();
	}

	private void OnCheckButtonChecked()
	{
		var state = _enableBotsCheck.IsSelected;

		FindChildByName( "SkillLevelExpert" )?.SetEnabled( state );
		FindChildByName( "SkillLevelHard" )?.SetEnabled( state );
		FindChildByName( "SkillLevelNormal" )?.SetEnabled( state );
		FindChildByName( "SkillLevelEasy" )?.SetEnabled( state );
		FindChildByName( "BotQuotaCombo" )?.SetEnabled( state );
		FindChildByName( "BotDifficultyLabel" )?.SetEnabled( state );
		FindChildByName( "BotLabel1" )?.SetEnabled( state );
		FindChildByName( "BotLabel2" )?.SetEnabled( state );
	}

	public override void OnMessage( Message message, Panel from )
	{
		if ( message.Name == "CheckButtonChecked" )
		{
			OnCheckButtonChecked();
			return;
		}

		base.OnMessage( message, from );
	}
}

public sealed class CreateMultiplayerGameDialog : PropertyDialog
{
	private const int MaxStringLength = 64;

	private readonly CreateMultiplayerGameServerPage _serverPage;
	private readonly CreateMultiplayerGameGameplayPage _gameplayPage;

	public CreateMultiplayerGameDialog( Vgui vgui, Panel parent ) : base( vgui, parent, "CreateMultiplayerGameDialog" )
	{
		SetHdProportional( true );
		SetSize( Scaled( 448 ), Scaled( 460 ) );
		SetCloseSound( "sound/UI/launch_dnmenu1.wav" );

		BasePanel.PositionDialog( this );

		SetTitle( "#GameUI_CreateServer" );
		SetOKButtonText( "#GameUI_Start" );

		_serverPage = new CreateMultiplayerGameServerPage( vgui, this, "ServerPage" );

		AddPage( _serverPage, "#GameUI_Server" );

		_gameplayPage = new CreateMultiplayerGameGameplayPage( vgui, this, "GameplayPage" );

		AddPage( _gameplayPage, "#GameUI_Game" );

		_serverPage.SetBotQuota( Cvar.Exists( "bot_quota" ) ? (int)Cvar.GetFloat( "bot_quota" ) : 9 );
		_serverPage.SetBotsEnabled( false );

		if ( Cvar.Exists( "sv_use_steam_networking" ) )
			_serverPage.SetSteamNetworkingEnabled( (int)Cvar.GetFloat( "sv_use_steam_networking" ) != 0 );

		if ( _serverPage.FindChildByName( "SkillLevelEasy" ) is CheckButton skill )
			skill.SetSelected( true );
	}

	private static string Limit( string value ) => value.Length > MaxStringLength ? value[..MaxStringLength] : value;

	protected override void OnOK()
	{
		base.OnOK();

		var mapName = Limit( _serverPage.GetMapName() );
		var hostName = Limit( _gameplayPage.GetHostName() );
		var password = Limit( _gameplayPage.GetPassword() );
		var steamNetworking = _serverPage.GetSteamNetworkingEnabled();
		var maxPlayers = _gameplayPage.GetMaxPlayers();

		Client.ClientCmd( $"disconnect\nsv_lan {(steamNetworking ? 0 : 1)}\nsv_use_steam_networking {(steamNetworking ? 1 : 0)}\nsetmaster enable\nmaxplayers {maxPlayers}\nsv_password \"{password}\"\nhostname \"{hostName}\"\ncd fadeout\nmap {mapName}\n" );
		Client.ClientCmd( "wait\nwait\n" );
	}

	public override void Close()
	{
		MarkForDeletion();

		base.Close();
	}
}
