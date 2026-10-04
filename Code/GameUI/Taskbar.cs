using System;

namespace GoldSrc.GameUI;

public sealed class BackgroundMenuButton( Vgui vgui, Panel parent, string name ) : Button( vgui, parent, name, "" )
{
	private static readonly Color32 White = new( 255, 255, 255, 255 );
	private static readonly Color32 Yellow = new( 255, 255, 0, 255 );

	private Image _image;
	private Image _mouseOverImage;

	public void SetImages( Image image, Image mouseOverImage )
	{
		_image = image;
		_mouseOverImage = mouseOverImage;

		SetArmed( IsArmed );
	}

	public override void SetArmed( bool state )
	{
		SetImageAtIndex( 0, state ? _mouseOverImage : _image, 0 );

		base.SetArmed( state );
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		SetFgColor( White );
		SetBgColor( default );
		SetDefaultColor( White, default );
		SetArmedColor( Yellow, default );
		SetDepressedColor( Yellow, default );
		SetContentAlignment( Alignment.West );
		SetBorder( null );
		SetDefaultBorder( null );
		SetDepressedBorder( null );
		SetKeyFocusBorder( null );
		SetTextInset( 0, 0 );
		SetArmedSound( "sound/UI/buttonrollover.wav" );
		SetDepressedSound( "sound/UI/buttonclick.wav" );
		SetReleasedSound( "sound/UI/buttonclickrelease.wav" );
	}
}

public sealed class Taskbar : EditablePanel
{
	private const int NarrowScreenWidth = 1024;
	private const float NarrowInsetScale = 0.7f;
	private const int TaskbarHeight = 44;
	private const int LogoBackgroundCount = 4;
	private const float StateChangeTime = 0.25f;
	private const float LogoRetargetMinTime = 0.4f;
	private const float LogoRetargetMaxTime = 1.95f;
	private const float BlipMinTime = 0.5f;
	private const float BlipMaxTime = 1.4f;

	private readonly GameUi _gameUi;
	private readonly BackgroundMenuButton _gameMenuButton;
	private readonly ImagePanel[] _logoBackground = new ImagePanel[LogoBackgroundCount];
	private readonly ImagePanel[] _logoBackgroundBlip = new ImagePanel[LogoBackgroundCount];
	private readonly int[] _logoBackgroundOffset = new int[LogoBackgroundCount];
	private readonly int[] _logoBackgroundBlipOffset = new int[LogoBackgroundCount];

	private GameMenu _gameMenu;
	private NewGameDialog _newGameDialog;
	private CreateMultiplayerGameDialog _createMultiplayerGameDialog;
	public List<string> OpenDialogCommands()
	{
		var commands = new List<string>();

		void Add( Frame dialog, string command )
		{
			if ( dialog is { Deleted: false, Visible: true } )
				commands.Add( command );
		}

		Add( _newGameDialog, "OpenNewGameDialog" );
		Add( _loadGameDialog, "OpenLoadGameDialog" );
		Add( _saveGameDialog, "OpenSaveGameDialog" );
		Add( _optionsDialog, "OpenOptionsDialog" );
		Add( _playerListDialog, "OpenPlayerListDialog" );
		Add( _serverBrowserDialog, "OpenServerBrowser" );
		Add( _createMultiplayerGameDialog, "OpenCreateMultiplayerGameDialog" );

		return commands;
	}

	private PlayerListDialog _playerListDialog;
	private ServerBrowserDialog _serverBrowserDialog;
	private LoadGameDialog _loadGameDialog;
	private SaveGameDialog _saveGameDialog;
	private OptionsDialog _optionsDialog;
	private ImagePanel _logo;
	private ImagePanel _logoBlip;
	private ImagePanel _logoBlip2;

	private int _gameMenuInset;
	private bool _animatedTitle;
	private bool _inGame;
	private float _stateChangeTime = -1f;

	private int _logoMinX;
	private int _logoMaxX;
	private int _logoTargetX;
	private float _logoX;
	private int _logoY;
	private float _logoSpeed;
	private float _logoMinSpeed;
	private float _logoMaxSpeed;
	private float _logoRetargetTime;
	private float _logoLastTime;
	private float _blipTime;
	private int _blipMode;
	private int _logoBackgroundX;
	private int _logoBackgroundY;

	public Taskbar( GameUi gameUi, Panel parent ) : base( gameUi.Vgui, parent, "TaskBar" )
	{
		_gameUi = gameUi;

		SetHdProportional( true );
		SetProportional( true );

		_gameMenuInset = Scaled( 24 );

		_gameMenuButton = new BackgroundMenuButton( Vgui, this, "GameMenuButton" );
		_gameMenuButton.SetCommand( "OpenGameMenu" );
		_gameMenuButton.SetEnabled( false );

		CreateGameMenu();
	}

	private static float RandomFloat( float min, float max ) => (Game.Random.Next( 0, 0x8000 ) / (32767f / (max - min))) + min;

	private void CreateGameMenu()
	{
		if ( KeyValues.LoadFromFile( "resource/GameMenu.res" ) is not { } data )
			return;

		_gameMenu = new GameMenu( Vgui, this, data.Name );
		_gameMenu.SetItemWidePadding( 12 );

		foreach ( var key in data.Keys )
		{
			var label = key.GetString( "label", "<unknown>" );

			_gameMenu.AddMenuItem( key.GetString( "name", label ), label, key.GetString( "command", null ), this, key.GetString( "HelpText", null ), key );
		}
	}

	public void RunFrame() => InvalidateLayout();

	public void OnGameUIActivated() => _gameMenu?.Reopen();

	public void OnGameUIHidden()
	{
		if ( _gameMenu is null )
			return;

		_gameMenu.SetVisible( false );
		_gameMenu.ClearCurrentlyHighlightedItem();
	}

	private ImagePanel CreateLogoPanel( ImagePanel previous, string name, string imageName, float scale, out int wide )
	{
		previous?.Delete();

		var image = new BitmapImage( Vgui.Surface.LoadImage( imageName ) );
		var (imageWide, imageTall) = image.GetContentSize();
		var panel = new ImagePanel( Vgui, Parent, name );

		wide = (int)(imageWide * scale);

		panel.SetSize( wide, (int)(imageTall * scale) );
		panel.SetImage( image, true );
		panel.MouseInputEnabled = false;

		return panel;
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		SetBorder( null );

		var surface = Vgui.Surface;

		_gameMenuInset = Scaled( Common.Atoi( Vgui.Scheme.GetResourceString( "InGameDesktop/GameMenuInset" ) ) );

		if ( surface.ScreenWide <= NarrowScreenWidth )
			_gameMenuInset = (int)(_gameMenuInset * NarrowInsetScale);

		_animatedTitle = ModInfo.AnimatedTitle;

		if ( _animatedTitle )
		{
			var scale = surface.ScreenWide * (1f / 1024f);

			_logo = CreateLogoPanel( _logo, "BigGameLogo", "resource/logo", scale, out var logoWide );

			var x = (float)((surface.ScreenWide / 2) - (logoWide / 2));

			_logoMinX = (int)(x - (scale * 88f));
			_logoMaxX = (int)((scale * 32f) + x);
			_logoTargetX = _logoMaxX;
			_logoY = Scaled( surface.ScreenTall <= 600 ? 20 : 60 );

			_logoBackgroundX = (int)(scale * -320f);
			_logoMinSpeed = scale * 13f;
			_logoBackgroundY = (int)(_logoY - (scale * 54f));
			_logoMaxSpeed = scale * 65f;

			var now = RealTime.Now;

			_logoLastTime = now;
			_logoRetargetTime = RandomFloat( LogoRetargetMinTime, LogoRetargetMaxTime ) + now;
			_logoX = ((_logoMaxX - _logoMinX) / 2) + _logoMinX;
			_logoSpeed = RandomFloat( _logoMinSpeed, _logoMaxSpeed );
			_blipTime = RandomFloat( BlipMinTime, BlipMaxTime ) + now;

			_logo.SetPos( (int)_logoX, _logoY );

			_logoBlip = CreateLogoPanel( _logoBlip, "BigGameLogoBlip", "resource/logo_blip", scale, out _ );
			_logoBlip.SetPos( (int)_logoX, _logoY );
			_logoBlip.SetVisible( false );

			_logoBlip2 = CreateLogoPanel( _logoBlip2, "BigGameLogoBlip2", "resource/logo_blip2", scale, out _ );
			_logoBlip2.SetPos( (int)_logoX, _logoY );
			_logoBlip2.SetVisible( false );

			var backgroundX = (int)_logoX + _logoBackgroundX;
			var offset = 0;

			for ( var i = 0; i < LogoBackgroundCount; i++ )
			{
				_logoBackground[i] = CreateLogoPanel( _logoBackground[i], $"BigGameLogoBG_{i}", $"resource/logo_big_blurred_{i}", scale, out var wide );
				_logoBackground[i].SetPos( backgroundX + offset, _logoBackgroundY );
				_logoBackgroundOffset[i] = offset;

				offset += wide;
			}

			offset = 0;

			for ( var i = 0; i < LogoBackgroundCount; i++ )
			{
				_logoBackgroundBlip[i] = CreateLogoPanel( _logoBackgroundBlip[i], $"BigGameLogoBGBlip_{i}", $"resource/logo_big_blurred_blip_{i}", scale, out var wide );
				_logoBackgroundBlip[i].SetPos( backgroundX + offset, _logoBackgroundY );
				_logoBackgroundBlip[i].SetVisible( false );
				_logoBackgroundBlipOffset[i] = offset;

				offset += wide;
			}

			_gameMenuButton.SetVisible( false );
			_gameMenuButton.SetEnabled( false );
		}
		else
		{
			_gameMenuButton.SetImages( new BitmapImage( surface.LoadImage( "resource/game_menu" ) ), new BitmapImage( surface.LoadImage( "resource/game_menu_mouseover" ) ) );
		}

		SetBgColor( GetSchemeColor( "InGameDesktop/WidescreenBarColor" ) );
		InvalidateLayout();
	}

	public override void PerformLayout()
	{
		var surface = Vgui.Surface;
		var tall = Scaled( TaskbarHeight );

		SetPos( 0, surface.ScreenTall - tall );
		SetSize( surface.ScreenWide, tall );

		_gameMenuButton.SetPos( _gameMenuInset, (tall - _gameMenuInset) / 2 );
		_gameMenuButton.SetSize( Scaled( 240 ), Scaled( 4 ) + _gameMenuInset );

		if ( _animatedTitle )
			LayoutLogo();
		else
			HideLogo();

		UpdateGameMenus();
	}

	private void HideLogo()
	{
		_logo?.SetVisible( false );
		_logoBlip?.SetVisible( false );

		for ( var i = 0; i < LogoBackgroundCount; i++ )
		{
			_logoBackground[i]?.SetVisible( false );
			_logoBackgroundBlip[i]?.SetVisible( false );
		}
	}

	private void LayoutLogo()
	{
		var now = RealTime.Now;
		var elapsed = Math.Max( Math.Min( now - _logoLastTime, 0.3f ), 0.0001f );
		float x;
		bool retarget;

		if ( _logoTargetX <= _logoX )
		{
			x = _logoX - (elapsed * _logoSpeed);
			retarget = x <= _logoTargetX || _logoRetargetTime <= now;

			if ( retarget )
				_logoTargetX = _logoMaxX;
		}
		else
		{
			x = (elapsed * _logoSpeed) + _logoX;
			retarget = _logoTargetX <= x || _logoRetargetTime <= now;

			if ( retarget )
				_logoTargetX = _logoMinX;
		}

		if ( retarget )
		{
			_logoRetargetTime = RandomFloat( LogoRetargetMinTime, LogoRetargetMaxTime ) + now;
			_logoSpeed = RandomFloat( _logoMinSpeed, _logoMaxSpeed );
		}

		var window = _blipMode switch
		{
			3 => 0.09f,
			4 => 0.07f,
			_ => 0.06f
		};

		if ( now > _blipTime )
		{
			_logo.SetVisible( true );
			_logoBlip.SetVisible( false );
			_logoBlip2.SetVisible( false );

			for ( var i = 0; i < LogoBackgroundCount; i++ )
			{
				_logoBackground[i].SetVisible( true );
				_logoBackgroundBlip[i].SetVisible( false );
			}

			_blipTime = RandomFloat( BlipMinTime, BlipMaxTime ) + now;

			var random = Game.Random.Next( 0, 0x8000 ) / 32767f;

			if ( random > 0.6f )
				_blipMode = 0;
			else if ( random > 0.4f )
				_blipMode = 3;
			else if ( random > 0.25f )
				_blipMode = 1;
			else
				_blipMode = random > 0.1f ? 4 : 2;
		}
		else if ( _blipTime < window + now )
		{
			var blip = true;
			var blip2 = false;
			var backgroundBlip = true;

			switch ( _blipMode )
			{
				case 1:
					backgroundBlip = false;
					break;

				case 2:
					blip = false;
					break;

				case 3:
					blip = (window * 0.5f) + now <= _blipTime;
					break;

				case 4:
					blip2 = true;
					break;
			}

			_logoBlip.SetVisible( blip );
			_logoBlip2.SetVisible( blip2 );

			for ( var i = 0; i < LogoBackgroundCount; i++ )
			{
				_logoBackground[i].SetVisible( !backgroundBlip );
				_logoBackgroundBlip[i].SetVisible( backgroundBlip );
			}
		}

		_logoX = x;

		_logo.SetPos( (int)x, _logoY );
		_logoBlip.SetPos( (int)x, _logoY );
		_logoBlip2.SetPos( (int)x, _logoY );

		var range = (float)(_logoMaxX - _logoMinX);
		var spread = range * 1.85f;
		var start = _logoBackgroundX - spread;
		var backgroundX = (int)((((_logoX - _logoMinX) / range) * ((_logoBackgroundX + spread) - start)) + start);

		for ( var i = 0; i < LogoBackgroundCount; i++ )
		{
			_logoBackground[i].SetPos( _logoBackgroundOffset[i] + backgroundX, _logoBackgroundY );
			_logoBackgroundBlip[i].SetPos( _logoBackgroundBlipOffset[i] + backgroundX, _logoBackgroundY );
		}

		_logoLastTime = now;
	}

	private void UpdateGameMenus()
	{
		var inGame = _gameUi.LevelLoaded;
		var multiplayer = inGame && _gameUi.Multiplayer;
		var now = RealTime.Now;

		if ( inGame != _inGame )
		{
			_inGame = inGame;
			_stateChangeTime = now;
		}

		var changing = now - _stateChangeTime < StateChangeTime;

		_animatedTitle = !changing && !inGame && ModInfo.AnimatedTitle;

		if ( _gameMenu is null )
			return;

		_gameMenu.UpdateMenuItemState( inGame, multiplayer, changing );
		_gameMenu.SetPos( _gameMenuInset, Vgui.Surface.ScreenTall - _gameMenu.Tall - Scaled( TaskbarHeight ) );
		_gameMenu.SetVisible( true );
	}

	public override void OnCommand( string command )
	{
		if ( command.Equals( "OpenGameMenu", StringComparison.OrdinalIgnoreCase ) )
		{
			if ( _gameMenu is not null )
				PostMessage( _gameMenu, new Message( "Command" ) { Text = "Open" } );
		}
		else if ( command.Equals( "OpenNewGameDialog", StringComparison.OrdinalIgnoreCase ) )
		{
			if ( _newGameDialog is not { Deleted: false } )
			{
				_newGameDialog = new NewGameDialog( Vgui, this );

				BasePanel.PositionDialog( _newGameDialog );
			}

			_newGameDialog.Activate();
		}
		else if ( command.Equals( "OpenLoadGameDialog", StringComparison.OrdinalIgnoreCase ) )
		{
			if ( _loadGameDialog is not { Deleted: false } )
			{
				_loadGameDialog = new LoadGameDialog( Vgui, this );

				BasePanel.PositionDialog( _loadGameDialog );
			}

			_loadGameDialog.Activate();
		}
		else if ( command.Equals( "OpenSaveGameDialog", StringComparison.OrdinalIgnoreCase ) )
		{
			if ( _saveGameDialog is not { Deleted: false } )
			{
				_saveGameDialog = new SaveGameDialog( Vgui, this );

				BasePanel.PositionDialog( _saveGameDialog );
			}

			_saveGameDialog.Activate();
		}
		else if ( command.Equals( "OpenOptionsDialog", StringComparison.OrdinalIgnoreCase ) )
		{
			if ( _optionsDialog is not { Deleted: false } )
			{
				_optionsDialog = new OptionsDialog( Vgui, this );

				BasePanel.PositionDialog( _optionsDialog );
			}

			_optionsDialog.Activate();
		}
		else if ( command.Equals( "OpenPlayerListDialog", StringComparison.OrdinalIgnoreCase ) )
		{
			if ( _playerListDialog is not { Deleted: false } )
			{
				_playerListDialog = new PlayerListDialog( Vgui, this );

				BasePanel.PositionDialog( _playerListDialog );
			}

			_playerListDialog.Activate();
		}
		else if ( command.Equals( "OpenServerBrowser", StringComparison.OrdinalIgnoreCase ) )
		{
			if ( _serverBrowserDialog is not { Deleted: false } )
			{
				Vgui.AddLocalizationFile( "servers/serverbrowser_english.txt" );

				_serverBrowserDialog = new ServerBrowserDialog( Vgui, this, ModInfo.GameName, Server.GameDirectory );

				BasePanel.PositionDialog( _serverBrowserDialog );

				_serverBrowserDialog.LoadUserData();

				if ( _gameUi.ConnectedServer is { } server )
					_serverBrowserDialog.OnConnectToGame( server );
			}

			_serverBrowserDialog.Activate();
		}
		else if ( command.Equals( "OpenCreateMultiplayerGameDialog", StringComparison.OrdinalIgnoreCase ) )
		{
			if ( _createMultiplayerGameDialog is not { Deleted: false } )
			{
				_createMultiplayerGameDialog = new CreateMultiplayerGameDialog( Vgui, this );

				BasePanel.PositionDialog( _createMultiplayerGameDialog );
			}

			_createMultiplayerGameDialog.Activate();
		}
		else if ( command.Equals( "Quit", StringComparison.OrdinalIgnoreCase ) )
		{
			var box = new QueryBox( Vgui, "#GameUI_QuitConfirmationTitle", "#GameUI_QuitConfirmationText", this );

			box.SetOKButtonText( "#GameUI_Quit" );
			box.SetOKCommand( new Message( "Command" ) { Text = "QuitNoConfirm" } );
			box.SetCancelCommand( new Message( "Command" ) { Text = "ReleaseModalWindow" } );
			box.AddActionSignalTarget( this );
			box.DoModal( null );

			Vgui.Surface.RestrictPaintToSinglePanel( box );
		}
		else if ( command.Equals( "QuitNoConfirm", StringComparison.OrdinalIgnoreCase ) )
		{
			Client.ClientCmd( "quit" );

			SetVisible( false );

			Vgui.Surface.RestrictPaintToSinglePanel( this );
		}
		else if ( command.Equals( "ResumeGame", StringComparison.OrdinalIgnoreCase ) )
		{
			Client.Current?.HideGameUi();
		}
		else if ( command.Equals( "Disconnect", StringComparison.OrdinalIgnoreCase ) )
		{
			Client.ClientCmd( "disconnect" );
		}
		else if ( command.Equals( "ReleaseModalWindow", StringComparison.OrdinalIgnoreCase ) )
		{
			Vgui.Surface.RestrictPaintToSinglePanel( null );
		}
		else if ( command.IndexOf( "engine ", StringComparison.Ordinal ) is >= 0 and var index )
		{
			if ( command.Length > index + 7 )
				Client.ClientCmd( command[(index + 7)..] );

			Client.Current?.HideGameUi();
		}
	}
}
