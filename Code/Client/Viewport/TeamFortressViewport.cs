using System;
using GoldSrc;
using GoldSrc.Vgui1;

namespace HalfLife;

public sealed class TeamFortressViewport : Panel
{
	private sealed class ViewPortInputHandler : DefaultInputSignal
	{
		public override void MousePressed( MouseCode code, Panel panel )
		{
			if ( code != MouseCode.Left )
				Client.ClientCmd( "ForceCloseCommandMenu\n" );
		}
	}

	public const int MenuDefault = 1;
	public const int MenuTeam = 2;
	public const int MenuClass = 3;
	public const int MenuMapBriefing = 4;
	public const int MenuIntro = 5;
	public const int MenuClassHelp = 6;
	public const int MenuClassHelp2 = 7;
	public const int MenuRepeatHelp = 8;
	public const int MenuSpecHelp = 9;

	public const int MaxMenus = 80;
	public const int MaxMotdLength = 1536;
	public const int MaxServerNameLength = 64;
	public const int MaxTeamNameSize = 32;
	public const int MaxMapName = 256;

	private const int KeyEnter = 13;
	private const int KeyEscape = 27;
	private const int KeySpace = 32;
	private const int KeyKpEnter = 169;

	private readonly Hud _hud;
	private bool _initialized;
	private readonly CommandMenu[] _commandMenus = new CommandMenu[MaxMenus];
	private CommandMenu _currentCommandMenu;
	private float _menuOpenTime;
	private float _scoreBoardLastUpdated;
	private float _spectatorPanelLastUpdated;
	private int _numMenus;
	private int _currentTeamNumber;
	private int _currentPlayerClass;
	private int _user1;
	private int _user2;
	private int _user3;
	private readonly SchemeManager _schemeManager;
	private int _gotAllMotd;
	private string _motd = "";
	private readonly CommandButton[] _teamButtons = new CommandButton[6];
	private readonly CommandButton[] _disguiseButtons = new CommandButton[5];
	private ServerBrowser _serverBrowser;
	private int _allowSpectators;
	private readonly int[] _validClasses = new int[5];
	private int _isFeigning;
	private int _isSettingDetpack;
	private int _numberOfTeams;
	private int _buildState;
	private int _randomPc;
	private readonly string[] _teamNames = ["", "", "", "", ""];
	private readonly string[] _detpackStrings = ["", "", ""];
	private string _mapName = "";

	public MenuPanel CurrentMenu;
	public TeamMenuPanel TeamMenu;
	public int StandardMenu;
	public int SpectatorOptionsMenu;
	public int SpectatorCameraMenu;
	public int PlayerMenu;
	public ClassMenuPanel ClassMenu;
	public ScorePanel ScoreBoard;
	public SpectatorPanel SpectatorPanel;
	public string ServerName = "";

	private HalfLifeClientDll Dll => _hud.Dll;

	private Client Engine => _hud.Engine;

	public TeamFortressViewport( Hud hud, int x, int y, int wide, int tall ) : base( x, y, wide, tall )
	{
		_hud = hud;
		_schemeManager = new SchemeManager( wide, tall );

		hud.ViewPort = this;

		_initialized = false;
		TeamMenu = null;
		ClassMenu = null;
		ScoreBoard = null;
		SpectatorPanel = null;
		CurrentMenu = null;
		_currentCommandMenu = null;

		Initialize();
		AddInputSignal( new ViewPortInputHandler() );

		var scheme = App.GetInstance().GetScheme();
		var primaryScheme = _schemeManager.GetSchemeHandle( "Primary Button Text" );

		scheme.SetFont( SchemeFont.Primary1, _schemeManager.GetFont( primaryScheme ) );

		_schemeManager.GetFgColor( primaryScheme, out var r, out var g, out var b, out var a );
		scheme.SetColor( SchemeColor.Primary1, r, g, b, a );

		_schemeManager.GetBgColor( primaryScheme, out r, out g, out b, out a );
		scheme.SetColor( SchemeColor.Primary3, r, g, b, a );

		_schemeManager.GetFgArmedColor( primaryScheme, out r, out g, out b, out a );
		scheme.SetColor( SchemeColor.Secondary2, r, g, b, a );

		_schemeManager.GetBgArmedColor( primaryScheme, out r, out g, out b, out a );
		scheme.SetColor( SchemeColor.Primary2, r, g, b, a );

		_schemeManager.GetBorderColor( primaryScheme, out _, out _, out _, out _ );
		scheme.SetColor( SchemeColor.Secondary1, 178, 118, 0, 0 );

		var scoreboardScheme = _schemeManager.GetSchemeHandle( "Scoreboard Text" );

		scheme.SetFont( SchemeFont.Primary2, _schemeManager.GetFont( scoreboardScheme ) );

		var commandMenuScheme = _schemeManager.GetSchemeHandle( "CommandMenu Text" );

		scheme.SetFont( SchemeFont.Primary3, _schemeManager.GetFont( commandMenuScheme ) );

		App.GetInstance().SetScheme( scheme );

		CreateTeamMenu();
		CreateClassMenu();
		CreateSpectatorMenu();
		CreateScoreBoard();

		_numMenus = 0;
		_currentTeamNumber = _user1 = _user2 = _user3 = 0;

		StandardMenu = CreateCommandMenu( "commandmenu.txt", 0, (int)VguiUtil.CMenuTop, false, VguiUtil.CMenuSizeX, VguiUtil.ButtonSizeY, 0 );
		SpectatorOptionsMenu = CreateCommandMenu( "spectatormenu.txt", 1, SpectatorPanel.PanelHeight, true, VguiUtil.CMenuSizeX, VguiUtil.ButtonSizeY / 2, 0 );
		SpectatorCameraMenu = CreateCommandMenu( "spectcammenu.txt", 1, SpectatorPanel.PanelHeight, true, VguiUtil.XRes( 200 ), VguiUtil.ButtonSizeY / 2, (int)(VguiUtil.ScreenWidth - (VguiUtil.XRes( 200 ) + 15)) );

		PlayerMenu = _numMenus;
		_numMenus++;

		_commandMenus[PlayerMenu] = new CommandMenu( null, 1, (int)VguiUtil.XRes( 15 + SpectatorPanel.OptionsButtonX + 15 + 31 ), SpectatorPanel.PanelHeight, (int)PlayerMenuLabelSize(), 300 );
		_commandMenus[PlayerMenu].SetParent( this );
		_commandMenus[PlayerMenu].SetVisible( false );
		_commandMenus[PlayerMenu].ButtonSizeY = VguiUtil.ButtonSizeY / 2;
		_commandMenus[PlayerMenu].SpectCmdMenu = 1;

		UpdatePlayerMenu( PlayerMenu );

		CreateServerBrowser();
	}

	private static float PlayerMenuLabelSize()
	{
		return VguiUtil.ScreenWidth - (VguiUtil.XRes( SpectatorPanel.CamOptionsButtonX ) + 15) - VguiUtil.XRes( 24 + 15 ) - VguiUtil.XRes( 15 + SpectatorPanel.OptionsButtonX + 15 + 38 );
	}

	public void RestoreFrom( TeamFortressViewport previous )
	{
		_currentTeamNumber = previous._currentTeamNumber;
		_currentPlayerClass = previous._currentPlayerClass;
		_user1 = previous._user1;
		_user2 = previous._user2;
		_user3 = previous._user3;
		_gotAllMotd = previous._gotAllMotd;
		_motd = previous._motd;
		_allowSpectators = previous._allowSpectators;
		_isFeigning = previous._isFeigning;
		_isSettingDetpack = previous._isSettingDetpack;
		_numberOfTeams = previous._numberOfTeams;
		_buildState = previous._buildState;
		_randomPc = previous._randomPc;
		_mapName = previous._mapName;
		ServerName = previous.ServerName;

		Array.Copy( previous._validClasses, _validClasses, _validClasses.Length );
		Array.Copy( previous._teamNames, _teamNames, _teamNames.Length );
		Array.Copy( previous._detpackStrings, _detpackStrings, _detpackStrings.Length );

		if ( previous.IsScoreBoardVisible() )
			ShowScoreBoard();
	}

	public void Initialize()
	{
		TeamMenu?.Initialize();
		ClassMenu?.Initialize();

		if ( ScoreBoard is not null )
		{
			ScoreBoard.Initialize();
			HideScoreBoard();
		}

		SpectatorPanel?.SetVisible( false );

		HideVGUIMenu();
		HideCommandMenu();

		_gotAllMotd = 1;
		_randomPc = 0;
		_scoreBoardLastUpdated = 0f;
		_spectatorPanelLastUpdated = 0f;

		Dll.PlayerClass = 0;
		Dll.TeamNumber = 0;

		_mapName = "";
		ServerName = "";

		for ( var i = 0; i < 5; i++ )
		{
			_validClasses[i] = 0;
			_teamNames[i] = "";
		}

		App.GetInstance().SetCursorOveride( App.GetInstance().GetScheme().GetCursor( SchemeCursor.None ) );
	}

	public int CreateCommandMenu( string menuFile, int direction, int yOffset, bool flatDesign, float buttonSizeX, float buttonSizeY, int xOffset )
	{
		var newIndex = _numMenus;

		_commandMenus[newIndex] = new CommandMenu( null, direction, xOffset, yOffset, (int)buttonSizeX, 300 );
		_commandMenus[newIndex].SetParent( this );
		_commandMenus[newIndex].SetVisible( false );
		_commandMenus[newIndex].ButtonSizeY = buttonSizeY;
		_commandMenus[newIndex].SpectCmdMenu = direction;

		_numMenus++;

		var file = Server.LoadMountText( menuFile );

		if ( file is null )
		{
			Con.DPrintf( $"Unable to open {menuFile}\n" );
			SetCurrentCommandMenu( null );

			return newIndex;
		}

		_detpackStrings[0] = _hud.TextMessage.LocaliseTextString( "#DetpackSet_For5Seconds", CommandButton.MaxButtonSize );
		_detpackStrings[1] = _hud.TextMessage.LocaliseTextString( "#DetpackSet_For20Seconds", CommandButton.MaxButtonSize );
		_detpackStrings[2] = _hud.TextMessage.LocaliseTextString( "#DetpackSet_For50Seconds", CommandButton.MaxButtonSize );

		_currentCommandMenu = _commandMenus[newIndex];

		var lastButtonText = "file start";
		var position = 0;
		var token = Common.Parse( file, ref position ) ?? "";

		while ( token.Length > 0 && _numMenus < MaxMenus )
		{
			while ( token.Length > 0 && token[0] != '}' )
			{
				var mapName = "";
				var playerClass = 0;
				var custom = false;
				var teamOnly = -1;
				var toggle = false;
				var getExtraToken = true;
				CommandButton button;

				if ( _currentCommandMenu is null )
				{
					Con.Printf( $"Error in {menuFile} file after '{lastButtonText}'.\n" );

					_initialized = false;

					return newIndex;
				}

				var customName = VguiUtil.Truncate( token, 32 );

				if ( customName == "CUSTOM" )
				{
					custom = true;
					token = Common.Parse( file, ref position ) ?? "";
				}
				else if ( customName == "MAP" )
				{
					token = Common.Parse( file, ref position ) ?? "";
					mapName = VguiUtil.Truncate( token, MaxMapName );
					token = Common.Parse( file, ref position ) ?? "";
				}
				else if ( customName.StartsWith( "TEAM", StringComparison.Ordinal ) )
				{
					teamOnly = Common.Atoi( customName[4..] );
					token = Common.Parse( file, ref position ) ?? "";
				}
				else if ( customName.StartsWith( "TOGGLE", StringComparison.Ordinal ) )
				{
					toggle = true;
					token = Common.Parse( file, ref position ) ?? "";
				}
				else
				{
					for ( var i = 1; i <= TeamFortress.PcEngineer; i++ )
					{
						if ( token == TeamFortress.Classes[i] )
						{
							playerClass = i;
							token = Common.Parse( file, ref position ) ?? "";

							break;
						}
					}
				}

				var boundKey = token;

				token = Common.Parse( file, ref position ) ?? "";

				var text = _hud.TextMessage.LocaliseTextString( token, 32 );

				lastButtonText = text;

				token = Common.Parse( file, ref position ) ?? "";

				var command = VguiUtil.Truncate( token, 128 );
				var buttonY = (int)((VguiUtil.ButtonSizeY - 1) * _currentCommandMenu.GetNumButtons());

				if ( custom )
				{
					button = CreateCustomButton( text, command, buttonY );

					token = Common.Parse( file, ref position ) ?? "";

					if ( token.Length > 0 && token[0] == '{' )
						command = token;
					else
						getExtraToken = false;
				}
				else if ( mapName.Length != 0 )
				{
					button = new MapButton( mapName, text, xOffset, buttonY, (int)buttonSizeX, (int)buttonSizeY );
				}
				else if ( teamOnly != -1 )
				{
					button = new TeamOnlyCommandButton( teamOnly, text, xOffset, buttonY, (int)buttonSizeX, (int)buttonSizeY, flatDesign );
				}
				else if ( toggle && direction == 0 )
				{
					button = new ToggleCommandButton( command, text, xOffset, buttonY, (int)buttonSizeX, (int)buttonSizeY, flatDesign );
				}
				else if ( direction == 1 )
				{
					if ( toggle )
						button = new SpectToggleButton( command, text, xOffset, buttonY, (int)buttonSizeX, (int)buttonSizeY, flatDesign );
					else
						button = new SpectButton( playerClass, text, xOffset, buttonY, (int)buttonSizeX, (int)buttonSizeY );
				}
				else
				{
					button = new CommandButton( playerClass, text, xOffset, buttonY, (int)buttonSizeX, (int)buttonSizeY, flatDesign );
				}

				if ( button is not null )
				{
					_currentCommandMenu.AddButton( button );
					button.SetBoundKey( boundKey.Length > 0 ? boundKey[0] : '\0' );
					button.SetParentMenu( _currentCommandMenu );
					button.SetFont( SchemeFont.Primary3 );
				}

				if ( command.Length > 0 && command[0] == '{' )
				{
					if ( _numMenus >= MaxMenus )
					{
						Con.Printf( $"Too many menus in {menuFile} past '{lastButtonText}'\n" );
					}
					else
					{
						_commandMenus[_numMenus] = CreateSubMenu( button, _currentCommandMenu, buttonY );
						_currentCommandMenu = _commandMenus[_numMenus];
						_numMenus++;
					}
				}
				else if ( !custom )
				{
					if ( toggle )
						button.AddActionSignal( new MenuHandlerToggleCvar( command ) );
					else
						button.AddActionSignal( new MenuHandlerStringCommand( command ) );

					button.AddInputSignal( new MenuHandlerPopupSubMenuInput( button, _currentCommandMenu ) );
				}

				if ( getExtraToken )
					token = Common.Parse( file, ref position ) ?? "";
			}

			_currentCommandMenu = _currentCommandMenu.GetParentMenu();

			token = Common.Parse( file, ref position ) ?? "";
		}

		SetCurrentMenu( null );
		SetCurrentCommandMenu( null );

		_initialized = true;

		return newIndex;
	}

	public CommandMenu CreateDisguiseSubmenu( CommandButton button, CommandMenu parentMenu, string commandText, int yOffset, int xOffset = 0 )
	{
		var menu = CreateSubMenu( button, parentMenu, yOffset, xOffset );

		_commandMenus[_numMenus] = menu;
		_numMenus++;

		for ( var i = TeamFortress.PcScout; i <= TeamFortress.PcEngineer; i++ )
		{
			var disguiseButton = new CommandButton( _hud.TextMessage.BufferedLocaliseTextString( TeamFortress.LocalisedClasses[i] ), 0, (int)VguiUtil.ButtonSizeY, (int)VguiUtil.CMenuSizeX, (int)VguiUtil.ButtonSizeY );

			disguiseButton.AddActionSignal( new MenuHandlerStringCommand( $"{commandText} {i}" ) );

			menu.AddButton( disguiseButton );
		}

		return menu;
	}

	public CommandButton CreateCustomButton( string buttonText, string buttonName, int yOffset )
	{
		CommandButton button = null;
		CommandMenu menu;

		var sizeX = (int)VguiUtil.CMenuSizeX;
		var sizeY = (int)VguiUtil.ButtonSizeY;
		var doubleY = (int)(VguiUtil.ButtonSizeY * 2);
		var rowY = (int)(VguiUtil.ButtonSizeY * _currentCommandMenu.GetNumButtons());

		if ( buttonName == "!CHANGETEAM" )
		{
			button = new CommandButton( buttonText, 0, doubleY, sizeX, sizeY );

			menu = CreateSubMenu( button, _currentCommandMenu, yOffset );
			_commandMenus[_numMenus] = menu;
			_numMenus++;

			for ( var i = 0; i < 4; i++ )
			{
				_teamButtons[i] = new TeamButton( i + 1, "teamname", 0, sizeY, sizeX, sizeY );
				_teamButtons[i].AddActionSignal( new MenuHandlerStringCommandWatch( $"jointeam {i + 1}" ) );

				menu.AddButton( _teamButtons[i] );
			}

			_teamButtons[4] = new TeamButton( 5, _hud.TextMessage.BufferedLocaliseTextString( "#Team_AutoAssign" ), 0, sizeY, sizeX, sizeY );
			_teamButtons[4].AddActionSignal( new MenuHandlerStringCommand( "jointeam 5" ) );
			menu.AddButton( _teamButtons[4] );

			_teamButtons[5] = new SpectateButton( _hud.TextMessage.BufferedLocaliseTextString( "#Menu_Spectate" ), 0, sizeY, sizeX, sizeY, false );
			_teamButtons[5].AddActionSignal( new MenuHandlerStringCommand( "spectate" ) );
			menu.AddButton( _teamButtons[5] );
		}
		else if ( buttonName == "!CHANGECLASS" )
		{
			button = new ClassButton( -1, buttonText, 0, sizeY, sizeX, sizeY, false );

			menu = CreateSubMenu( button, _currentCommandMenu, yOffset );
			_commandMenus[_numMenus] = menu;
			_numMenus++;

			for ( var i = TeamFortress.PcScout; i <= TeamFortress.PcRandom; i++ )
			{
				var classButton = new ClassButton( i, _hud.TextMessage.LocaliseTextString( TeamFortress.LocalisedClasses[i], 256 ), 0, sizeY, sizeX, sizeY, false );

				classButton.AddActionSignal( new MenuHandlerStringCommandClassSelect( TeamFortress.ClassSelection[i] ) );
				menu.AddButton( classButton );
			}
		}
		else if ( buttonName == "!MAPBRIEFING" )
		{
			button = new CommandButton( buttonText, 0, rowY, sizeX, sizeY );
			button.AddActionSignal( new MenuHandlerTextWindow( MenuMapBriefing ) );
			button.AddInputSignal( new MenuHandlerPopupSubMenuInput( button, _currentCommandMenu ) );
		}
		else if ( buttonName == "!CLASSDESC" )
		{
			button = new ClassButton( 0, buttonText, 0, rowY, sizeX, sizeY, false );
			button.AddActionSignal( new MenuHandlerTextWindow( MenuClassHelp ) );
			button.AddInputSignal( new MenuHandlerPopupSubMenuInput( button, _currentCommandMenu ) );
		}
		else if ( buttonName == "!SERVERINFO" )
		{
			button = new ClassButton( 0, buttonText, 0, rowY, sizeX, sizeY, false );
			button.AddActionSignal( new MenuHandlerTextWindow( MenuIntro ) );
			button.AddInputSignal( new MenuHandlerPopupSubMenuInput( button, _currentCommandMenu ) );
		}
		else if ( buttonName == "!SPY" )
		{
			button = new DisguiseButton( 0, buttonText, 0, sizeY, sizeX, sizeY );
		}
		else if ( buttonName == "!FEIGN" )
		{
			button = StringCommandButton( new FeignButton( 0, buttonText, 0, sizeY, sizeX, sizeY ), "feign" );
		}
		else if ( buttonName == "!FEIGNSILENT" )
		{
			button = StringCommandButton( new FeignButton( 0, buttonText, 0, sizeY, sizeX, sizeY ), "sfeign" );
		}
		else if ( buttonName == "!FEIGNSTOP" )
		{
			button = StringCommandButton( new FeignButton( 1, buttonText, 0, sizeY, sizeX, sizeY ), "feign" );
		}
		else if ( buttonName == "!DISGUISEENEMY" )
		{
			button = new DisguiseButton( DisguiseButton.DisguiseTeam2, buttonText, 0, sizeY, sizeX, sizeY );

			CreateDisguiseSubmenu( button, _currentCommandMenu, "disguise_enemy", yOffset );
		}
		else if ( buttonName == "!DISGUISEFRIENDLY" )
		{
			button = new DisguiseButton( DisguiseButton.DisguiseTeam1 | DisguiseButton.DisguiseTeam2, buttonText, 0, sizeY, sizeX, sizeY );

			CreateDisguiseSubmenu( button, _currentCommandMenu, "disguise_friendly", yOffset );
		}
		else if ( buttonName == "!DISGUISE" )
		{
			button = new DisguiseButton( DisguiseButton.DisguiseTeam3 | DisguiseButton.DisguiseTeam4, buttonText, 0, sizeY, sizeX, sizeY );

			var disguiseMenu = CreateSubMenu( button, _currentCommandMenu, yOffset );

			_commandMenus[_numMenus] = disguiseMenu;
			_numMenus++;

			for ( var i = 1; i <= 4; i++ )
			{
				_disguiseButtons[i] = new DisguiseButton( (i < 4 ? DisguiseButton.DisguiseTeam3 : 0) | DisguiseButton.DisguiseTeam4, "Disguise", 0, sizeY, sizeX, sizeY );

				disguiseMenu.AddButton( _disguiseButtons[i] );
				_disguiseButtons[i].SetParentMenu( disguiseMenu );

				CreateDisguiseSubmenu( _disguiseButtons[i], disguiseMenu, $"disguise {i}", yOffset, (int)(VguiUtil.CMenuSizeX - 1) );
			}
		}
		else if ( buttonName == "!DETPACKSTART" )
		{
			button = new DetpackButton( 2, buttonText, 0, doubleY, sizeX, sizeY );

			menu = CreateSubMenu( button, _currentCommandMenu, yOffset );
			_commandMenus[_numMenus] = menu;
			_numMenus++;

			var detButton = new CommandButton( _detpackStrings[0], 0, sizeY, sizeX, sizeY );

			detButton.AddActionSignal( new MenuHandlerStringCommand( "detstart 5" ) );
			menu.AddButton( detButton );

			detButton = new CommandButton( _detpackStrings[1], 0, sizeY, sizeX, sizeY );
			detButton.AddActionSignal( new MenuHandlerStringCommand( "detstart 20" ) );
			menu.AddButton( detButton );

			detButton = new CommandButton( _detpackStrings[2], 0, sizeY, sizeX, sizeY );
			detButton.AddActionSignal( new MenuHandlerStringCommand( "detstart 50" ) );
			menu.AddButton( detButton );
		}
		else if ( buttonName == "!DETPACKSTOP" )
		{
			button = StringCommandButton( new DetpackButton( 1, buttonText, 0, sizeY, sizeX, sizeY ), "detstop" );
		}
		else if ( buttonName == "!BUILD" )
		{
			button = new BuildButton( BuildButton.BuildStateBase, 0, buttonText, 0, doubleY, sizeX, sizeY );
		}
		else if ( buttonName == "!BUILDSENTRY" )
		{
			button = StringCommandButton( new BuildButton( BuildButton.BuildStateCanBuild, BuildButton.SentryGun, buttonText, 0, doubleY, sizeX, sizeY ), "build 2" );
		}
		else if ( buttonName == "!BUILDDISPENSER" )
		{
			button = StringCommandButton( new BuildButton( BuildButton.BuildStateCanBuild, BuildButton.Dispenser, buttonText, 0, doubleY, sizeX, sizeY ), "build 1" );
		}
		else if ( buttonName == "!ROTATESENTRY180" )
		{
			button = StringCommandButton( new BuildButton( BuildButton.BuildStateHasBuilding, BuildButton.SentryGun, buttonText, 0, doubleY, sizeX, sizeY ), "rotatesentry180" );
		}
		else if ( buttonName == "!ROTATESENTRY" )
		{
			button = StringCommandButton( new BuildButton( BuildButton.BuildStateHasBuilding, BuildButton.SentryGun, buttonText, 0, doubleY, sizeX, sizeY ), "rotatesentry" );
		}
		else if ( buttonName == "!DISMANTLEDISPENSER" )
		{
			button = StringCommandButton( new BuildButton( BuildButton.BuildStateHasBuilding, BuildButton.Dispenser, buttonText, 0, doubleY, sizeX, sizeY ), "dismantle 1" );
		}
		else if ( buttonName == "!DISMANTLESENTRY" )
		{
			button = StringCommandButton( new BuildButton( BuildButton.BuildStateHasBuilding, BuildButton.SentryGun, buttonText, 0, doubleY, sizeX, sizeY ), "dismantle 2" );
		}
		else if ( buttonName == "!DETONATEDISPENSER" )
		{
			button = StringCommandButton( new BuildButton( BuildButton.BuildStateHasBuilding, BuildButton.Dispenser, buttonText, 0, doubleY, sizeX, sizeY ), "detdispenser" );
		}
		else if ( buttonName == "!DETONATESENTRY" )
		{
			button = StringCommandButton( new BuildButton( BuildButton.BuildStateHasBuilding, BuildButton.SentryGun, buttonText, 0, doubleY, sizeX, sizeY ), "detsentry" );
		}
		else if ( buttonName == "!BUILDENTRYTELEPORTER" )
		{
			button = StringCommandButton( new BuildButton( BuildButton.BuildStateCanBuild, BuildButton.EntryTeleporter, buttonText, 0, doubleY, sizeX, sizeY ), "build 4" );
		}
		else if ( buttonName == "!DISMANTLEENTRYTELEPORTER" )
		{
			button = StringCommandButton( new BuildButton( BuildButton.BuildStateHasBuilding, BuildButton.EntryTeleporter, buttonText, 0, doubleY, sizeX, sizeY ), "dismantle 4" );
		}
		else if ( buttonName == "!DETONATEENTRYTELEPORTER" )
		{
			button = StringCommandButton( new BuildButton( BuildButton.BuildStateHasBuilding, BuildButton.EntryTeleporter, buttonText, 0, doubleY, sizeX, sizeY ), "detentryteleporter" );
		}
		else if ( buttonName == "!BUILDEXITTELEPORTER" )
		{
			button = StringCommandButton( new BuildButton( BuildButton.BuildStateCanBuild, BuildButton.ExitTeleporter, buttonText, 0, doubleY, sizeX, sizeY ), "build 5" );
		}
		else if ( buttonName == "!DISMANTLEEXITTELEPORTER" )
		{
			button = StringCommandButton( new BuildButton( BuildButton.BuildStateHasBuilding, BuildButton.ExitTeleporter, buttonText, 0, doubleY, sizeX, sizeY ), "dismantle 5" );
		}
		else if ( buttonName == "!DETONATEEXITTELEPORTER" )
		{
			button = StringCommandButton( new BuildButton( BuildButton.BuildStateHasBuilding, BuildButton.ExitTeleporter, buttonText, 0, doubleY, sizeX, sizeY ), "detexitteleporter" );
		}
		else if ( buttonName == "!BUILDSTOP" )
		{
			button = StringCommandButton( new BuildButton( BuildButton.BuildStateBuilding, 0, buttonText, 0, doubleY, sizeX, sizeY ), "build" );
		}

		return button;
	}

	private CommandButton StringCommandButton( CommandButton button, string command )
	{
		button.AddActionSignal( new MenuHandlerStringCommand( command ) );
		button.AddInputSignal( new MenuHandlerPopupSubMenuInput( button, _currentCommandMenu ) );

		return button;
	}

	public CommandMenu CreateSubMenu( CommandButton button, CommandMenu parentMenu, int yOffset, int xOffset = 0 )
	{
		var xPos = 0;
		var yPos = 0;
		var wide = (int)VguiUtil.CMenuSizeX;
		var direction = 0;

		if ( parentMenu is not null )
		{
			xPos = (int)(_currentCommandMenu.GetXOffset() + (VguiUtil.CMenuSizeX - 1) + xOffset);
			yPos = _currentCommandMenu.GetYOffset() + yOffset;
			direction = parentMenu.GetDirection();
		}

		var menu = new CommandMenu( parentMenu, direction, xPos, yPos, wide, 0 );

		menu.SetParent( this );
		button.AddSubMenu( menu );
		button.SetFont( SchemeFont.Primary3 );
		menu.ButtonSizeY = _currentCommandMenu.ButtonSizeY;

		var signal = new MenuHandlerPopupSubMenuInput( button, menu );

		button.AddInputSignal( signal );

		var label = new ImageLabel( "arrowright", (int)VguiUtil.XRes( VguiUtil.CMenuSizeX - (VguiUtil.CMenuSizeX / 8) ), (int)VguiUtil.YRes( VguiUtil.ButtonSizeY / 6 ) );

		label.SetParent( button );
		label.AddInputSignal( signal );
		label.GetPos( out _, out _ );
		label.SetPos( button.GetWide() - label.GetImageWide() - 4, -4 );

		if ( !button.NoHighlight )
			label.AddInputSignal( new HandlerCommandButtonHighlight( button ) );

		return menu;
	}

	public void ToggleServerBrowser()
	{
		if ( !_initialized )
			return;

		if ( _serverBrowser is null )
			return;

		_serverBrowser.SetVisible( !_serverBrowser.IsVisible() );

		UpdateCursorState();
	}

	public void ShowCommandMenu( int menuIndex )
	{
		if ( !_initialized )
			return;

		if ( CurrentMenu is not null )
			return;

		if ( _currentCommandMenu == _commandMenus[menuIndex] )
		{
			HideCommandMenu();
			return;
		}

		if ( _hud.IntermissionActive )
			return;

		UpdateCommandMenu( menuIndex );
		HideVGUIMenu();

		SetCurrentCommandMenu( _commandMenus[menuIndex] );

		_menuOpenTime = _hud.Time;

		UpdateCursorState();

		for ( var i = 2; i < Cmd.Argc; i++ )
		{
			var param = Cmd.Argv( i - 1 );

			if ( _currentCommandMenu.KeyInput( param.Length > 0 ? param[0] : 0 ) )
				HideCommandMenu();
		}
	}

	public void InputSignalHideCommandMenu()
	{
		if ( !_initialized )
			return;

		if ( _menuOpenTime + 0.3 > _hud.Time )
			return;

		HideCommandMenu();
	}

	public void HideCommandMenu()
	{
		if ( !_initialized )
			return;

		_commandMenus[StandardMenu]?.ClearButtonsOfArmedState();
		_commandMenus[SpectatorOptionsMenu]?.ClearButtonsOfArmedState();
		_commandMenus[SpectatorCameraMenu]?.ClearButtonsOfArmedState();
		_commandMenus[PlayerMenu]?.ClearButtonsOfArmedState();

		_menuOpenTime = 0f;

		SetCurrentCommandMenu( null );
		UpdateCursorState();
	}

	public void ShowScoreBoard()
	{
		if ( ScoreBoard is null )
			return;

		if ( Engine.MaxClients > 1 )
		{
			ScoreBoard.Open();
			UpdateCursorState();
		}
	}

	public bool IsScoreBoardVisible() => ScoreBoard is not null && ScoreBoard.IsVisible();

	public void HideScoreBoard()
	{
		if ( _hud.IntermissionActive )
			return;

		if ( ScoreBoard is null )
			return;

		ScoreBoard.SetVisible( false );

		_hud.Voice.StopSquelchMode();

		UpdateCursorState();
	}

	public void InputPlayerSpecial()
	{
		if ( !_initialized )
			return;

		if ( Dll.PlayerClass == TeamFortress.PcEngineer || Dll.PlayerClass == TeamFortress.PcSpy )
		{
			ShowCommandMenu( StandardMenu );

			_currentCommandMenu?.KeyInput( '7' );
		}
		else
		{
			Client.ClientCmd( "_special" );
		}
	}

	public void SetCurrentCommandMenu( CommandMenu newMenu )
	{
		for ( var i = 0; i < _numMenus; i++ )
			_commandMenus[i].SetVisible( false );

		_currentCommandMenu = newMenu;

		_currentCommandMenu?.MakeVisible( null );
	}

	public void UpdateCommandMenu( int menuIndex )
	{
		if ( menuIndex == PlayerMenu )
		{
			_commandMenus[PlayerMenu].RemoveAllButtons();

			UpdatePlayerMenu( PlayerMenu );
		}

		_commandMenus[menuIndex].RecalculateVisibles( 0, false );
		_commandMenus[menuIndex].RecalculatePositions( 0 );
	}

	private void UpdatePlayerMenu( int menuIndex )
	{
		var labelSize = PlayerMenuLabelSize();

		GetAllPlayersInfo();

		for ( var i = 1; i < Hud.MaxPlayers; i++ )
		{
			var entity = Engine.GetEntityByIndex( i );

			if ( !_hud.Spectator.IsActivePlayer( entity ) )
				continue;

			var name = _hud.PlayerInfoList[entity.Index].Name;
			var button = new SpectButton( 1, name, (int)VguiUtil.XRes( 15 + SpectatorPanel.OptionsButtonX + 15 + 31 ), (int)(SpectatorPanel.PanelHeight + ((i - 1) * VguiUtil.CMenuSizeX)), (int)labelSize, (int)(VguiUtil.ButtonSizeY / 2) );

			button.SetBoundKey( (char)255 );
			button.SetContentAlignment( Label.Alignment.Center );

			_commandMenus[menuIndex].AddButton( button );
			button.SetParentMenu( _commandMenus[menuIndex] );
			button.SetFont( SchemeFont.Primary3 );

			button.AddActionSignal( new MenuHandlerSpectateFollow( name ) );
			button.AddInputSignal( new MenuHandlerPopupSubMenuInput( button, _commandMenus[menuIndex] ) );
		}
	}

	public void UpdateSpectatorPanel()
	{
		_user1 = Dll.User1;
		_user2 = Dll.User2;
		_user3 = Dll.User3;

		if ( SpectatorPanel is null )
			return;

		if ( Dll.User1 != 0 && Hud.DrawHud != 0f && !_hud.IntermissionActive )
		{
			var player = 0;

			_hud.Spectator.CheckSettings();

			if ( !SpectatorPanel.IsVisible() )
			{
				SpectatorPanel.SetVisible( true );
				SpectatorPanel.ShowMenu( false );

				_hud.TextMessage.TextMsg( HudTextMessage.HudPrintCenter, VguiUtil.Truncate( _hud.TextMessage.BufferedLocaliseTextString( "#Spec_Duck" ), 127 ) );
			}

			var bottomText = $"#Spec_Mode{Dll.User1}";
			var helpString2 = $"#Spec_Mode{Dll.User1}";

			if ( Engine.IsSpectateOnly )
				helpString2 += " - HLTV";

			if ( Dll.User2 > 0 && Dll.User2 <= Engine.MaxClients && Dll.User1 != HudSpectator.ObsRoaming )
				player = Dll.User2;

			string name;

			if ( Dll.User1 == HudSpectator.ObsMapFree && HudSpectator.Pip == 0f )
				name = null;
			else
				name = _hud.PlayerInfoList[player].Name;

			if ( player != 0 && name is not null )
				bottomText = VguiUtil.Truncate( name, 128 );
			else
				bottomText = _hud.TextMessage.BufferedLocaliseTextString( bottomText );

			if ( Dll.User1 == HudSpectator.ObsInEye && player != 0 )
			{
				var color = _hud.GetClientColor( player );
				var r = (int)(color[0] * 255);
				var g = (int)(color[1] * 255);
				var b = (int)(color[2] * 255);

				SpectatorPanel.BottomMainLabel.SetFgColor( r, g, b, 0 );
				SpectatorPanel.BottomMainButton.SetFgColor( r, g, b, 0 );
			}
			else
			{
				SpectatorPanel.BottomMainLabel.SetFgColor( 143, 143, 54, 0 );
				SpectatorPanel.BottomMainButton.SetFgColor( 143, 143, 54, 0 );
			}

			SpectatorPanel.BottomMainLabel.SetText( bottomText );
			SpectatorPanel.BottomMainButton.SetText( bottomText );

			string text;

			if ( Engine.IsSpectateOnly )
				text = $"{_hud.TextMessage.BufferedLocaliseTextString( "#Spectators" )}: {_hud.Spectator.SpectatorNumber}";
			else
				text = $"{_hud.TextMessage.BufferedLocaliseTextString( "#Spec_Map" )}: {Common.FileBase( Engine.LevelName )}";

			SpectatorPanel.ExtraInfo.SetText( VguiUtil.Truncate( text, 64 ) );

			SpectatorPanel.Update();
		}
		else
		{
			if ( SpectatorPanel.IsVisible() )
			{
				SpectatorPanel.SetVisible( false );
				SpectatorPanel.ShowMenu( false );
			}
		}

		_spectatorPanelLastUpdated = (float)(_hud.Time + 1.0);
	}

	public void CreateScoreBoard()
	{
		var xdent = (int)VguiUtil.XRes( 104 );
		var ydent = (int)VguiUtil.YRes( 40 );

		if ( VguiUtil.ScreenWidth == 512 )
		{
			xdent = 30;
			ydent = 30;
		}
		else if ( VguiUtil.ScreenWidth == 400 )
		{
			xdent = 0;
			ydent = 20;
		}

		ScoreBoard = new ScorePanel( _hud, xdent, ydent, VguiUtil.ScreenWidth - (xdent * 2), VguiUtil.ScreenHeight - (ydent * 2) );
		ScoreBoard.SetParent( this );
		ScoreBoard.SetVisible( false );
	}

	public void CreateServerBrowser()
	{
		_serverBrowser = new ServerBrowser( _hud, 0, 0, VguiUtil.ScreenWidth, VguiUtil.ScreenHeight );
		_serverBrowser.SetParent( this );
		_serverBrowser.SetVisible( false );
	}

	public void SetCurrentMenu( MenuPanel menu )
	{
		CurrentMenu = menu;

		if ( CurrentMenu is not null )
		{
			if ( Engine.DemoPlayback )
				return;

			CurrentMenu.Open();
		}
		else
		{
			Client.ClientCmd( "closemenus;" );
		}
	}

	public MenuPanel CreateTextWindow( int textToShow )
	{
		const int maxTitleLength = 64;

		string text = null;
		var title = "";

		if ( textToShow == MenuHandlerTextWindow.ShowMotd )
		{
			title = ServerName.Length == 0 ? "Half-Life" : VguiUtil.Truncate( ServerName, maxTitleLength );
			text = _motd;
		}
		else if ( textToShow == MenuHandlerTextWindow.ShowMapBriefing )
		{
			string path;

			if ( _mapName.Length != 0 )
			{
				path = $"maps/{_mapName}.txt";
			}
			else
			{
				var level = Engine.LevelName;

				if ( level is null )
					return null;

				path = $"{level[..level.IndexOf( '.' )]}.txt";

				_mapName = level;

				var dot = _mapName.IndexOf( '.' );

				if ( dot >= 0 )
					_mapName = _mapName[..dot];

				var slash = _mapName.IndexOf( '/' );

				if ( slash >= 0 )
					_mapName = _mapName[(slash + 1)..];
			}

			text = Server.LoadMountText( path );

			if ( text is null )
				return null;

			title = VguiUtil.Truncate( _mapName, maxTitleLength );
		}
		else if ( textToShow == MenuHandlerTextWindow.ShowClassDesc )
		{
			var className = Dll.PlayerClass switch
			{
				TeamFortress.PcScout => "scout",
				TeamFortress.PcSniper => "sniper",
				TeamFortress.PcSoldier => "soldier",
				TeamFortress.PcDemoman => "demoman",
				TeamFortress.PcMedic => "medic",
				TeamFortress.PcHvyWeap => "hwguy",
				TeamFortress.PcPyro => "pyro",
				TeamFortress.PcSpy => "spy",
				TeamFortress.PcEngineer => "engineer",
				TeamFortress.PcCivilian => "civilian",
				_ => null
			};

			if ( className is null )
				return null;

			text = _hud.TextMessage.BufferedLocaliseTextString( $"#Help_{className}" );
			title = _hud.TextMessage.LocaliseTextString( $"#Title_{className}", maxTitleLength );

			var path = Dll.PlayerClass == TeamFortress.PcCivilian ? "classes/long_civilian.txt" : $"classes/long_{TeamFortress.ClassSelection[Dll.PlayerClass]}.txt";

			if ( Server.LoadMountText( path ) is { } file )
				text = file;
		}
		else if ( textToShow == MenuHandlerTextWindow.ShowSpecHelp )
		{
			title = _hud.TextMessage.LocaliseTextString( "#Spec_Help_Title", maxTitleLength );
			text = _hud.TextMessage.BufferedLocaliseTextString( "#Spec_Help_Text" );
		}

		var panel = MessageWindowPanel.Create( _hud, text, title, Dll.PlayerClass == TeamFortress.PcUndefined, 0, 0, 0, VguiUtil.ScreenWidth, VguiUtil.ScreenHeight );

		panel.SetParent( this );

		return panel;
	}

	public void ShowVGUIMenu( int menu )
	{
		MenuPanel newMenu = null;

		if ( Engine.DemoPlayback )
			return;

		if ( _hud.IntermissionActive && menu != MenuIntro )
			return;

		for ( var existing = CurrentMenu; existing is not null; existing = existing.GetNextMenu() )
		{
			if ( existing.GetMenuID() == menu )
				return;
		}

		switch ( menu )
		{
			case MenuTeam:
				newMenu = ShowTeamMenu();
				break;

			case MenuMapBriefing:
				newMenu = CreateTextWindow( MenuHandlerTextWindow.ShowMapBriefing );
				break;

			case MenuIntro:
				newMenu = CreateTextWindow( MenuHandlerTextWindow.ShowMotd );
				break;

			case MenuClassHelp:
				newMenu = CreateTextWindow( MenuHandlerTextWindow.ShowClassDesc );
				break;

			case MenuSpecHelp:
				newMenu = CreateTextWindow( MenuHandlerTextWindow.ShowSpecHelp );
				break;

			case MenuClass:
				newMenu = ShowClassMenu();
				break;
		}

		if ( newMenu is null )
			return;

		HideCommandMenu();

		newMenu.SetMenuID( menu );
		newMenu.SetActive( true );
		newMenu.SetParent( this );

		if ( CurrentMenu is not null )
		{
			if ( CurrentMenu.GetMenuID() == MenuClass && menu == MenuTeam )
			{
				var temp = CurrentMenu;

				CurrentMenu.Close();
				CurrentMenu = newMenu;
				CurrentMenu.SetNextMenu( temp );
				CurrentMenu.Open();

				UpdateCursorState();
			}
			else
			{
				CurrentMenu.SetNextMenu( newMenu );
			}
		}
		else
		{
			CurrentMenu = newMenu;
			CurrentMenu.Open();

			UpdateCursorState();
		}
	}

	public void HideVGUIMenu()
	{
		while ( CurrentMenu is not null )
			HideTopMenu();
	}

	public void HideTopMenu()
	{
		if ( CurrentMenu is not null )
		{
			CurrentMenu.Close();

			SetCurrentMenu( CurrentMenu.GetNextMenu() );
		}

		UpdateCursorState();
	}

	public bool AllowedToPrintText()
	{
		if ( CurrentMenu is not null && Dll.PlayerClass == 0 )
		{
			var id = CurrentMenu.GetMenuID();

			if ( id == MenuTeam || id == MenuClass || id == MenuIntro || id == MenuClassHelp )
				return false;
		}

		return true;
	}

	private MenuPanel ShowTeamMenu()
	{
		if ( Engine.DemoPlayback )
			return null;

		TeamMenu.Reset();

		return TeamMenu;
	}

	private void CreateTeamMenu()
	{
		TeamMenu = new TeamMenuPanel( _hud, 100, 0, 0, 0, VguiUtil.ScreenWidth, VguiUtil.ScreenHeight );
		TeamMenu.SetParent( this );
		TeamMenu.SetVisible( false );
	}

	private MenuPanel ShowClassMenu()
	{
		if ( Engine.DemoPlayback )
			return null;

		ClassMenu.Reset();

		return ClassMenu;
	}

	private void CreateClassMenu()
	{
		ClassMenu = new ClassMenuPanel( _hud, 100, 0, 0, 0, VguiUtil.ScreenWidth, VguiUtil.ScreenHeight );
		ClassMenu.SetParent( this );
		ClassMenu.SetVisible( false );
	}

	private void CreateSpectatorMenu()
	{
		SpectatorPanel = new SpectatorPanel( _hud, 0, 0, VguiUtil.ScreenWidth, VguiUtil.ScreenHeight );
		SpectatorPanel.SetParent( this );
		SpectatorPanel.SetVisible( false );
		SpectatorPanel.Initialize();
	}

	public void UpdateOnPlayerInfo()
	{
		TeamMenu?.Update();
		ClassMenu?.Update();
		ScoreBoard?.Update();
	}

	public void UpdateCursorState()
	{
		if ( SpectatorPanel.MenuVisible || CurrentMenu is not null || TeamMenu.IsVisible() || _serverBrowser.IsVisible() || _hud.Voice.IsInSquelchMode() )
		{
			Dll.VisibleMouse = true;

			App.GetInstance().SetCursorOveride( App.GetInstance().GetScheme().GetCursor( SchemeCursor.Arrow ) );

			return;
		}

		if ( _currentCommandMenu is not null && Hud.CaptureMouse != 0f )
		{
			Dll.VisibleMouse = true;

			App.GetInstance().SetCursorOveride( App.GetInstance().GetScheme().GetCursor( SchemeCursor.Arrow ) );

			return;
		}

		Dll.VisibleMouse = false;

		App.GetInstance().SetCursorOveride( App.GetInstance().GetScheme().GetCursor( SchemeCursor.None ) );
	}

	public void UpdateHighlights() => _currentCommandMenu?.MakeVisible( null );

	public void GetAllPlayersInfo()
	{
		for ( var i = 1; i < Hud.MaxPlayers; i++ )
		{
			Engine.GetPlayerInfo( i, _hud.PlayerInfoList[i] );

			if ( _hud.PlayerInfoList[i].ThisPlayer )
				ScoreBoard.PlayerNum = i;
		}
	}

	public override void PaintBackground()
	{
		GetParent().GetSize( out var wide, out var tall );
		SetSize( wide, tall );

		if ( ScoreBoard is not null )
		{
			GetApp().GetCursorPos( out var x, out var y );

			ScoreBoard.CursorMoved( x, y, ScoreBoard );
		}

		if ( Dll.TeamNumber != _currentTeamNumber )
		{
			UpdateCommandMenu( StandardMenu );

			ClassMenu?.Update();

			_currentTeamNumber = Dll.TeamNumber;
		}

		if ( Dll.PlayerClass != _currentPlayerClass )
		{
			UpdateCommandMenu( StandardMenu );

			_currentPlayerClass = Dll.PlayerClass;
		}

		if ( Dll.User1 != _user1 || Dll.User2 != _user2 || _spectatorPanelLastUpdated < _hud.Time )
			UpdateSpectatorPanel();

		if ( ScoreBoard.IsVisible() && _scoreBoardLastUpdated < _hud.Time )
		{
			ScoreBoard.Update();

			_scoreBoardLastUpdated = (float)(_hud.Time + 0.5);
		}
	}

	public bool SlotInput( int slot ) => CurrentMenu is not null && CurrentMenu.SlotInput( slot );

	public int KeyInput( bool down, int keyNum, string currentBinding )
	{
		if ( _user1 != 0 && Engine.ConIsVisible == 0 )
		{
			if ( down && (keyNum == KeyEnter || keyNum == KeyKpEnter) )
				ShowVGUIMenu( MenuTeam );
		}

		if ( CurrentMenu is not null && Engine.ConIsVisible == 0 )
		{
			var menuId = CurrentMenu.GetMenuID();

			if ( menuId == MenuTeam || menuId == MenuClass )
			{
				if ( keyNum == KeyEscape )
				{
					if ( (menuId == MenuTeam && Dll.TeamNumber != 0) || (menuId == MenuClass && Dll.PlayerClass != 0) )
					{
						HideTopMenu();

						return 0;
					}
				}

				for ( var i = '0'; i <= '9'; i++ )
				{
					if ( down && keyNum == i )
					{
						SlotInput( i - '0' );

						return 0;
					}
				}
			}

			if ( down && (keyNum == KeyEnter || keyNum == KeyKpEnter || keyNum == KeySpace || keyNum == KeyEscape) )
			{
				if ( menuId == MenuMapBriefing || menuId == MenuIntro || menuId == MenuClassHelp )
				{
					HideTopMenu();

					return 0;
				}
			}

			if ( currentBinding is not null && down && currentBinding == "+jump" )
			{
				if ( menuId == MenuTeam )
				{
					TeamMenu.SlotInput( 5 );

					return 0;
				}
			}
		}

		if ( down && _currentCommandMenu is not null )
		{
			if ( keyNum == KeyEscape )
			{
				HideCommandMenu();

				return 0;
			}

			if ( keyNum >= '0' && keyNum <= '9' )
			{
				if ( _currentCommandMenu.KeyInput( keyNum ) )
					HideCommandMenu();

				return 0;
			}
		}

		return 1;
	}

	public int GetValidClasses( int team ) => _validClasses[team];

	public int GetNumberOfTeams() => _numberOfTeams;

	public int GetIsFeigning() => _isFeigning;

	public int GetIsSettingDetpack() => _isSettingDetpack;

	public int GetBuildState() => _buildState;

	public int IsRandomPC() => _randomPc;

	public string GetTeamName( int team ) => _teamNames[team];

	public int GetAllowSpectators() => _allowSpectators;

	public SchemeManager GetSchemeManager() => _schemeManager;

	public ScorePanel GetScoreBoard() => ScoreBoard;

	public bool MsgFuncValClass( string name, byte[] data )
	{
		var message = new MessageBuffer( data );

		for ( var i = 0; i < 5; i++ )
			_validClasses[i] = message.ReadShort();

		UpdateCommandMenu( StandardMenu );

		return true;
	}

	public bool MsgFuncTeamNames( string name, byte[] data )
	{
		var message = new MessageBuffer( data );

		_numberOfTeams = message.ReadByte();

		for ( var i = 0; i < _numberOfTeams; i++ )
		{
			var teamNum = i + 1;

			_teamNames[teamNum] = _hud.TextMessage.LocaliseTextString( message.ReadString(), MaxTeamNameSize );

			_teamButtons[i]?.SetText( _teamNames[teamNum] );

			if ( teamNum < 5 )
				_disguiseButtons[teamNum]?.SetText( _teamNames[teamNum] );
		}

		TeamMenu?.Update();

		return true;
	}

	public bool MsgFuncFeign( string name, byte[] data )
	{
		_isFeigning = new MessageBuffer( data ).ReadByte();

		UpdateCommandMenu( StandardMenu );

		return true;
	}

	public bool MsgFuncDetpack( string name, byte[] data )
	{
		_isSettingDetpack = new MessageBuffer( data ).ReadByte();

		UpdateCommandMenu( StandardMenu );

		return true;
	}

	public bool MsgFuncVGUIMenu( string name, byte[] data )
	{
		var message = new MessageBuffer( data );
		var menu = message.ReadByte();

		if ( menu == MenuMapBriefing )
			_mapName = VguiUtil.Truncate( message.ReadString(), 64 );

		ShowVGUIMenu( menu );

		return true;
	}

	public bool MsgFuncMOTD( string name, byte[] data )
	{
		if ( _gotAllMotd != 0 )
			_motd = "";

		var message = new MessageBuffer( data );

		_gotAllMotd = message.ReadByte();

		var roomInArray = MaxMotdLength - _motd.Length - 1;
		var text = message.ReadString();

		if ( roomInArray < 0 )
			roomInArray = 0;

		_motd += text.Length > roomInArray ? text[..roomInArray] : text;

		if ( _gotAllMotd != 0 && !Engine.IsSpectateOnly )
			ShowVGUIMenu( MenuIntro );

		return true;
	}

	public bool MsgFuncBuildSt( string name, byte[] data )
	{
		_buildState = new MessageBuffer( data ).ReadShort();

		UpdateCommandMenu( StandardMenu );

		return true;
	}

	public bool MsgFuncRandomPC( string name, byte[] data )
	{
		_randomPc = new MessageBuffer( data ).ReadByte();

		return true;
	}

	public bool MsgFuncServerName( string name, byte[] data )
	{
		ServerName = VguiUtil.Truncate( new MessageBuffer( data ).ReadString(), MaxServerNameLength );

		return true;
	}

	public bool MsgFuncScoreInfo( string name, byte[] data )
	{
		var message = new MessageBuffer( data );
		var client = (short)message.ReadByte();
		var frags = (short)message.ReadShort();
		var deaths = (short)message.ReadShort();
		var playerClass = (short)message.ReadShort();
		var teamNumber = (short)message.ReadShort();

		if ( client > 0 && client <= Hud.MaxPlayers )
		{
			var info = _hud.GetExtraPlayerInfo( client );

			info.Frags = frags;
			info.Deaths = deaths;
			info.PlayerClass = playerClass;
			info.TeamNumber = teamNumber;

			if ( info.TeamNumber < 0 )
				info.TeamNumber = 0;

			UpdateOnPlayerInfo();
		}

		return true;
	}

	public bool MsgFuncTeamScore( string name, byte[] data )
	{
		var message = new MessageBuffer( data );
		var teamName = message.ReadString();
		int i;

		for ( i = 1; i <= ScoreBoard.NumTeams; i++ )
		{
			if ( string.Equals( teamName, _hud.TeamInfo[i].Name, StringComparison.OrdinalIgnoreCase ) )
				break;
		}

		if ( i > ScoreBoard.NumTeams )
			return true;

		_hud.TeamInfo[i].ScoresOverriden = true;
		_hud.TeamInfo[i].Frags = (short)message.ReadShort();
		_hud.TeamInfo[i].Deaths = (short)message.ReadShort();

		return true;
	}

	public bool MsgFuncTeamInfo( string name, byte[] data )
	{
		if ( ScoreBoard is null )
			return true;

		var message = new MessageBuffer( data );
		var client = (short)message.ReadByte();

		if ( client > 0 && client <= Hud.MaxPlayers )
			_hud.GetExtraPlayerInfo( client ).TeamName = VguiUtil.Truncate( message.ReadString(), Hud.MaxTeamName + 1 );

		ScoreBoard.RebuildTeams();

		return true;
	}

	public void DeathMsg( int killer, int victim ) => ScoreBoard.DeathMsg( killer, victim );

	public bool MsgFuncSpectator( string name, byte[] data )
	{
		var message = new MessageBuffer( data );
		var client = (short)message.ReadByte();

		if ( client > 0 && client <= Hud.MaxPlayers )
			_hud.IsSpectator[client] = message.ReadByte();

		return true;
	}

	public bool MsgFuncAllowSpec( string name, byte[] data )
	{
		_allowSpectators = new MessageBuffer( data ).ReadByte();

		UpdateCommandMenu( StandardMenu );

		TeamMenu?.Update();

		return true;
	}

	public bool MsgFuncResetFade( string name, byte[] data ) => true;

	public bool MsgFuncSpecFade( string name, byte[] data ) => true;
}
