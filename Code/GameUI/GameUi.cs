namespace GoldSrc.GameUI;

public sealed class GameUi
{
	private readonly Panel _root;
	private readonly BasePanel _staticPanel;

	private int _screenWide;
	private int _screenTall;

	private const int MaxConnectCommandLength = 128;

	private GameServerItem _connectedServer;

	public Vgui Vgui { get; } = new();

	public Taskbar Taskbar { get; }

	public bool LevelLoaded { get; private set; }

	public bool Multiplayer { get; private set; }

	public IReadOnlyList<HudDraw> Draws => Vgui.Surface.Draws;

	public CursorCode Cursor => Vgui.Surface.Cursor;

	public GameUi( int screenWide, int screenTall )
	{
		_screenWide = screenWide;
		_screenTall = screenTall;

		Vgui.SetScreenSize( screenWide, screenTall );

		_root = new Panel( Vgui, null, null );
		_root.SetBounds( 0, 0, screenWide, screenTall );
		_root.SetPaintBackgroundEnabled( false );

		Vgui.Surface.EmbeddedPanel = _root;

		_staticPanel = new BasePanel( this );
		_staticPanel.SetBounds( 0, 0, 400, 300 );
		_staticPanel.SetPaintBorderEnabled( false );
		_staticPanel.SetPaintBackgroundEnabled( true );
		_staticPanel.SetPaintEnabled( false );
		_staticPanel.SetVisible( true );
		_staticPanel.MouseInputEnabled = false;
		_staticPanel.SetKeyBoardInputEnabled( false );
		_staticPanel.SetParent( _root );

		Taskbar = new Taskbar( this, _staticPanel );
		Taskbar.SetVisible( false );

		_staticPanel.SetBackgroundRenderState( BackgroundState.Menu );
	}

	public void ActivateGameUI()
	{
		_staticPanel.SetVisible( true );

		Taskbar.SetVisible( true );
		Taskbar.OnGameUIActivated();
	}

	public void HideGameUI()
	{
		_staticPanel.SetVisible( false );

		Taskbar.OnGameUIHidden();
	}

	public void ConnectToServer( string game, GameServerItem server )
	{
		if ( !game.Equals( "valve", System.StringComparison.OrdinalIgnoreCase ) )
			Client.ClientCmd( "mp3 stop" );

		var command = Cvar.GetString( "ui_cmd_on_connect" );

		if ( command.Length > MaxConnectCommandLength )
			command = command[..MaxConnectCommandLength];

		if ( command.Length > 2 )
		{
			Client.ClientCmd( command );
			Client.ClientCmd( "ui_cmd_on_connect 0" );
		}

		Client.Current?.HideGameUi();

		_connectedServer = server;

		if ( server is not null )
			ServerBrowserDialog.Instance?.OnConnectToGame( server );
	}

	public void DisconnectFromServer()
	{
		Client.Current?.ActivateGameUi();

		_connectedServer = null;

		ServerBrowserDialog.Instance?.OnDisconnectFromGame();
	}

	public GameServerItem ConnectedServer => _connectedServer;

	public int ScreenWide => _screenWide;

	public int ScreenTall => _screenTall;

	public void RestoreFrom( GameUi previous )
	{
		_connectedServer = previous._connectedServer;

		foreach ( var command in previous.Taskbar.OpenDialogCommands() )
			Taskbar.OnCommand( command );
	}

	public void LoadingStarted( string resourceType )
	{
		_staticPanel.SetBackgroundRenderState( resourceType.Equals( "transition", System.StringComparison.OrdinalIgnoreCase ) ? BackgroundState.Transition : BackgroundState.Loading );
	}

	public void LoadingFinished()
	{
		_staticPanel.SetBackgroundRenderState( BackgroundState.Menu );
	}

	public void RunFrame( int screenWide, int screenTall, bool levelLoaded, bool multiplayer )
	{
		LevelLoaded = levelLoaded;
		Multiplayer = multiplayer;

		var surface = Vgui.Surface;

		surface.ClearDraws();

		_staticPanel.SetSize( screenWide, screenTall );

		Taskbar.RunFrame();

		if ( surface.RestrictedPanel is not null )
			surface.PaintTraverse( _staticPanel );

		Vgui.RunFrame();
	}

	public void Frame( int screenWide, int screenTall, bool levelLoaded, bool multiplayer, Vector2 cursor )
	{
		Vgui.Input.InternalCursorMoved( (int)cursor.x, (int)cursor.y );

		RunFrame( screenWide, screenTall, levelLoaded, multiplayer );

		Vgui.Surface.PaintTraverse( _root );
	}

	public void MousePressed( MouseCode code ) => Vgui.Input.InternalMousePressed( code );

	public void MouseDoublePressed( MouseCode code ) => Vgui.Input.InternalMouseDoublePressed( code );

	public void MouseReleased( MouseCode code ) => Vgui.Input.InternalMouseReleased( code );

	public void MouseWheeled( int delta ) => Vgui.Input.InternalMouseWheeled( delta );

	public void KeyCodePressed( KeyCode code ) => Vgui.Input.InternalKeyCodePressed( code );

	public void KeyCodeTyped( KeyCode code ) => Vgui.Input.InternalKeyCodeTyped( code );

	public void KeyTyped( char character ) => Vgui.Input.InternalKeyTyped( character );

	public void KeyCodeReleased( KeyCode code ) => Vgui.Input.InternalKeyCodeReleased( code );
}
