namespace GoldSrc.GameUI;

public sealed class GameMenuItemHelpText : Label
{
	private const int NarrowScreenWidth = 1024;

	public GameMenuItemHelpText( Vgui vgui, Panel parent ) : base( vgui, parent, "HelpText", "GameMenuHelpText" )
	{
		SetHdProportional( true );
		SetPaintEnabled( true );

		MouseInputEnabled = false;
	}

	public override void ApplySchemeSettings()
	{
		SetProportional( true );

		base.ApplySchemeSettings();

		SetFgColor( GetSchemeColor( "InGameDesktop/MenuHintColor" ) );
		SetBgColor( default );
		SetContentAlignment( Alignment.West );
		SetBorder( null );
		SetFont( GetFont( Vgui.Surface.ScreenWide < NarrowScreenWidth ? "MenuHintSmall" : "MenuHintLarge" ) );
		SetTextInset( Scaled( 4 ), 0 );
		SetContentAlignment( Alignment.West );
		SetSize( Scaled( 600 ), Scaled( 30 ) );
	}
}

public sealed class GameMenuItemHighlight : Label
{
	private const float FadeTime = 0.8f;

	private Color32 _color;
	private bool _fading;
	private float _fadeStart;

	public GameMenuItemHighlight( Vgui vgui, Panel parent, string name ) : base( vgui, parent, name, "GameMenuItemHighlight" )
	{
		SetPaintEnabled( true );

		MouseInputEnabled = false;
	}

	public void Show()
	{
		InvalidateLayout();
		SetVisible( true );
		SetFgColor( _color );

		_fading = false;
	}

	public void FadeOut()
	{
		InvalidateLayout();

		_fading = true;
		_fadeStart = RealTime.Now;
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		_color = GetSchemeColor( "InGameDesktop/BlurMenuColor" );

		SetProportional( true );
		SetFgColor( _color );
		SetBgColor( default );
		SetContentAlignment( Alignment.West );
		SetBorder( null );
		SetFont( GetFont( "MenuBlurLarge" ) );
		SetTextInset( Scaled( 4 ), 0 );

		_fadeStart = -999f;
		_fading = true;
	}

	public override void PerformLayout()
	{
		base.PerformLayout();

		if ( !_fading )
			return;

		var elapsed = RealTime.Now - _fadeStart;

		if ( FadeTime < elapsed )
		{
			SetVisible( false );
			return;
		}

		var fraction = 1f - (elapsed / FadeTime);

		SetFgColor( new Color32( (byte)(int)(_color.r * fraction), (byte)(int)(_color.g * fraction), (byte)(int)(_color.b * fraction), (byte)(int)(fraction * 255f) ) );
		InvalidateLayout();
	}
}

public sealed class GameMenuItem : MenuItem
{
	private const int NarrowScreenWidth = 1024;

	private readonly GameMenuItemHighlight _highlight;

	private GameMenuItemHelpText _helpText;

	public GameMenuItem( Menu menu, string name ) : base( menu, name, "GameMenuItem" )
	{
		SetPaintEnabled( false );

		_highlight = new GameMenuItemHighlight( Vgui, Parent, name );
		_highlight.MakeReadyForUse();
		_highlight.SetVisible( false );
	}

	public GameMenuItemHelpText HelpText => _helpText;

	public void SetHelpText( string text )
	{
		if ( text is null || _helpText is not null )
			return;

		var parent = Parent;

		if ( parent.Parent is not null )
			parent = parent.Parent;

		if ( parent.Parent is not null )
			parent = parent.Parent;

		_helpText = new GameMenuItemHelpText( Vgui, parent );
		_helpText.SetText( text );
	}

	public override void SetText( string text )
	{
		base.SetText( text );

		_highlight.SetText( text );
		_highlight.InvalidateLayout( true );
	}

	protected override void OnArmed()
	{
		_highlight.SetSize( Wide + Scaled( 18 ), Tall + Scaled( 16 ) );
		_highlight.SetPos( X, Y - Scaled( 8 ) );
		_highlight.Show();
	}

	protected override void OnDisarmed() => _highlight.FadeOut();

	public override void ApplySchemeSettings()
	{
		SetProportional( true );

		base.ApplySchemeSettings();

		var menuColor = GetSchemeColor( "InGameDesktop/MenuColor" );

		SetFgColor( menuColor );
		SetBgColor( default );
		SetDefaultColor( menuColor, default );
		SetArmedColor( GetSchemeColor( "InGameDesktop/ArmedMenuColor" ), default );
		SetDepressedColor( GetSchemeColor( "InGameDesktop/DepressedMenuColor" ), default );
		SetContentAlignment( Alignment.West );
		SetBorder( null );
		SetDefaultBorder( null );
		SetDepressedBorder( null );
		SetKeyFocusBorder( null );
		SetFont( GetFont( "MenuLarge" ) );
		SetTextInset( Scaled( 4 ), 0 );
		SetDepressedSound( "sound/UI/launch_selectmenu.wav" );
		SetButtonActivationType( ActivationType.OnPressed );
	}

	public override void PerformLayout()
	{
		base.PerformLayout();

		if ( _helpText is null )
			return;

		var narrow = Vgui.Surface.ScreenWide < NarrowScreenWidth;

		_helpText.SetPos( Scaled( narrow ? 190 : 270 ) + Parent.X + X, Scaled( narrow ? 12 : 10 ) + Parent.Y + Y );
	}
}

public sealed class GameMenu : Menu
{
	private const float MinVisibilityRate = 1e-7f;
	private const bool SteamLaunch = true;

	private bool _reopen;
	private bool _appearing;
	private float _appearStart;
	private float _visibilityRate;

	public GameMenu( Vgui vgui, Panel parent, string name ) : base( vgui, parent, name )
	{
	}

	public void Reopen() => _reopen = true;

	public int AddMenuItem( string itemName, string itemText, string command, Panel target, string helpText, KeyValues userData )
	{
		var item = new GameMenuItem( this, itemName );

		item.AddActionSignalTarget( target );
		item.SetCommand( command );
		item.SetText( itemText );
		item.UserData = userData;
		item.SetHelpText( helpText );

		return AddMenuItem( item );
	}

	public override void SetVisible( bool state )
	{
		base.SetVisible( true );

		if ( !state )
			MoveToBack();
	}

	protected override void LayoutMenuBorder()
	{
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		SetProportional( true );
		SetMenuItemHeight( Common.Atoi( Vgui.Scheme.GetResourceString( "InGameDesktop/MenuItemHeight" ) ) );
		SetBgColor( default );
		SetBorder( null );

		_visibilityRate = Common.Atof( Vgui.Scheme.GetResourceString( "InGameDesktop/MenuItemVisibilityRate" ) );

		if ( _visibilityRate < MinVisibilityRate )
			_visibilityRate = MinVisibilityRate;
	}

	public override void OnThink()
	{
		if ( _reopen )
		{
			ClearCurrentlyHighlightedItem();

			_appearing = true;
			_appearStart = RealTime.Now;
			_reopen = false;
		}

		if ( !_appearing )
			return;

		var count = Children.Count;
		var shown = (int)((RealTime.Now - _appearStart) / _visibilityRate);

		if ( count <= shown )
		{
			shown = count;
			_appearing = false;
		}

		for ( var i = 0; i < count; i++ )
			Children[i].SetPaintEnabled( count - shown <= i );

		if ( !_appearing )
		{
			foreach ( var child in Children )
			{
				if ( child is not GameMenuItem { Visible: true, Enabled: true } item )
					continue;

				SetCurrentlyHighlightedItem( GetMenuID( item ) );
				break;
			}
		}

		InvalidateLayout();
	}

	public override void OnCommand( string command )
	{
		if ( !command.Equals( "Open", System.StringComparison.OrdinalIgnoreCase ) )
		{
			base.OnCommand( command );
			return;
		}

		MoveToFront();
		RequestFocus();

		_reopen = true;
	}

	public override void OnKillFocus()
	{
		base.OnKillFocus();

		Vgui.Surface.MovePopupToBack( this );
	}

	public void UpdateMenuItemState( bool inGame, bool multiplayer, bool changing )
	{
		if ( changing )
			_reopen = true;

		foreach ( var child in Children )
		{
			if ( child is not GameMenuItem { UserData: { } data } item )
				continue;

			var visible = true;

			if ( changing )
				visible = false;
			else if ( inGame ? data.GetInt( "OnlyInCareerGame" ) != 0 : data.GetInt( "OnlyInGame" ) != 0 )
				visible = false;
			else if ( multiplayer && data.GetInt( "notmulti" ) != 0 )
				visible = false;
			else if ( inGame && !multiplayer && data.GetInt( "notsingle" ) != 0 )
				visible = false;
			else if ( SteamLaunch && data.GetInt( "notsteam" ) != 0 )
				visible = false;

			item.HelpText?.SetVisible( !inGame && visible );
			item.SetVisible( visible );
		}

		InvalidateLayout();
	}
}
