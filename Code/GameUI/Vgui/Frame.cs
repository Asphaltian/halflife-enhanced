using System;

namespace GoldSrc.GameUI;

public class GripPanel : Panel
{
	private readonly int _directionX;
	private readonly int _directionY;

	private readonly BitmapImage _resizer;

	private bool _dragging;
	private int _dragStartX;
	private int _dragStartY;

	protected Frame Frame { get; }

	protected int FrameStartX { get; private set; }

	protected int FrameStartY { get; private set; }

	private int _frameStartWide;
	private int _frameStartTall;

	public GripPanel( Frame frame, int directionX, int directionY ) : base( frame.Vgui, frame, null )
	{
		Frame = frame;
		_directionX = directionX;
		_directionY = directionY;

		SetPaintEnabled( false );
		SetPaintBackgroundEnabled( false );
		SetPaintBorderEnabled( false );

		if ( directionX == 1 && directionY == 1 )
		{
			SetPaintEnabled( true );
			SetPaintBackgroundEnabled( true );
		}

		_resizer = new BitmapImage( Vgui.Surface.LoadImage( "resource/icon_resizer" ) );

		var (wide, tall) = _resizer.GetSize();

		_resizer.SetSize( Scaled( wide ), Scaled( tall ) );
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		SetFgColor( GetSchemeColor( "BorderBright" ) );
		SetBgColor( GetSchemeColor( "BorderSelection" ) );
	}

	public override void PaintBackground() => _resizer.Paint( Vgui.Surface );

	public override void Paint() => _resizer.Paint( Vgui.Surface );

	public override void OnMousePressed( MouseCode code )
	{
		if ( code != MouseCode.Left )
			return;

		_dragging = true;

		(_dragStartX, _dragStartY) = Vgui.Input.CursorPos;

		FrameStartX = Frame.X;
		FrameStartY = Frame.Y;
		_frameStartWide = Frame.Wide;
		_frameStartTall = Frame.Tall;

		Vgui.Input.SetMouseCapture( this );

		if ( Vgui.Input.Focus is not { } focus || !focus.HasParent( Frame ) )
			Frame.RequestFocus();
	}

	public override void OnCursorMoved( int x, int y )
	{
		if ( !_dragging )
			return;

		if ( !Vgui.Input.IsMouseDown( MouseCode.Left ) )
		{
			OnMouseReleased( MouseCode.Left );
			return;
		}

		var (cursorX, cursorY) = Vgui.Input.CursorPos;

		Moved( cursorX - _dragStartX, cursorY - _dragStartY );
	}

	public override void OnMouseReleased( MouseCode code )
	{
		_dragging = false;

		Vgui.Input.SetMouseCapture( null );
	}

	public override void OnMouseCaptureLost() => _dragging = false;

	protected virtual void Moved( int dx, int dy )
	{
		if ( !Frame.IsSizeable )
			return;

		var x = FrameStartX;
		var y = FrameStartY;
		var wide = _frameStartWide + (_directionX * dx);
		var tall = _frameStartTall + (_directionY * dy);
		var (minimumWide, minimumTall) = Frame.MinimumSize;

		if ( _directionX == -1 )
		{
			if ( wide < minimumWide )
				dx = _frameStartWide - minimumWide;

			x += dx;
		}

		if ( _directionY == -1 )
		{
			if ( tall < minimumTall )
				dy = _frameStartTall - minimumTall;

			y += dy;
		}

		Frame.SetPos( x, y );
		Frame.SetSize( wide, tall );
		Frame.InvalidateLayout();
	}
}

public sealed class CaptionGripPanel( Frame frame ) : GripPanel( frame, 0, 0 )
{
	private const int SnapRange = 10;

	protected override void Moved( int dx, int dy )
	{
		if ( !Frame.IsMoveable )
			return;

		var surface = Vgui.Surface;

		Frame.SetPos( Snap( FrameStartX + dx, Frame.Wide, surface.ScreenWide ), Snap( FrameStartY + dy, Frame.Tall, surface.ScreenTall ) );
	}

	private static int Snap( int position, int size, int workspaceSize )
	{
		if ( Math.Abs( position ) < SnapRange )
			return 0;

		return Math.Abs( size - workspaceSize + position ) < SnapRange ? workspaceSize - size : position;
	}
}

public sealed class FrameButton : Button
{
	private const int DefaultSize = 18;

	private readonly BitmapImage _image;

	private bool _disabledLook = true;
	private Border _brightBorder;
	private Border _depressedBorder;
	private Border _disabledBorder;
	private Color32 _enabledFgColor;
	private Color32 _enabledBgColor;
	private Color32 _disabledFgColor;
	private Color32 _disabledBgColor;

	public FrameButton( Frame frame, string imageName ) : base( frame.Vgui, frame, null, "" )
	{
		SetSize( DefaultSize, DefaultSize );
		SetContentAlignment( Alignment.NorthWest );
		SetTextInset( 2, 1 );

		_image = new BitmapImage( Vgui.Surface.LoadImage( imageName ) );
	}

	public void SetDisabledLook( bool state )
	{
		_disabledLook = state;

		var fgColor = state ? _disabledFgColor : _enabledFgColor;
		var bgColor = state ? _disabledBgColor : _enabledBgColor;

		SetDefaultColor( fgColor, bgColor );
		SetArmedColor( fgColor, bgColor );
		SetDepressedColor( fgColor, bgColor );
	}

	protected override Border GetBorder( bool depressed, bool armed, bool selected, bool keyFocus )
	{
		if ( _disabledLook )
			return _disabledBorder;

		return depressed ? _depressedBorder : _brightBorder;
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		_enabledBgColor = GetSchemeColor( "TitleButtonBgColor" );
		_enabledFgColor = GetSchemeColor( "TitleButtonFgColor" );
		_disabledBgColor = GetSchemeColor( "TitleButtonDisabledBgColor" );
		_disabledFgColor = GetSchemeColor( "TitleButtonDisabledFgColor" );

		_brightBorder = Vgui.Scheme.GetBorder( "TitleButtonBorder" );
		_depressedBorder = Vgui.Scheme.GetBorder( "TitleButtonDepressedBorder" );
		_disabledBorder = Vgui.Scheme.GetBorder( "TitleButtonDisabledBorder" );

		SetDisabledLook( _disabledLook );
		AddImage( _image, 0 );
	}

	public override void OnMousePressed( MouseCode code )
	{
		if ( !Enabled || !IsMouseClickEnabled( code ) || !IsUseCaptureMouseEnabled )
			return;

		SetSelected( true );

		Vgui.Input.SetMouseCapture( this );
	}
}

public class MenuButton : Button
{
	private Menu _menu;

	public MenuButton( Vgui vgui, Panel parent, string name, string text ) : base( vgui, parent, name, text )
	{
		SetUseCaptureMouse( false );
		SetButtonActivationType( ActivationType.OnPressed );
	}

	public override bool CanBeDefaultButton => false;

	public void SetMenu( Menu menu )
	{
		_menu = menu;

		menu.SetVisible( false );
		menu.AddActionSignalTarget( this );
		menu.SetParent( this );
	}

	private void HideMenu()
	{
		if ( _menu is null )
			return;

		_menu.SetVisible( false );

		ForceDepressed( false );
	}

	public override void OnMessage( Message message, Panel from )
	{
		if ( message.Name != "MenuClose" )
		{
			base.OnMessage( message, from );
			return;
		}

		HideMenu();
		PostActionSignal( new Message( "MenuClose" ) );
	}

	public override void OnKeyCodeTyped( KeyCode code )
	{
		var input = Vgui.Input;

		if ( !input.IsShiftDown && !input.IsControlDown && !input.IsAltDown && code == KeyCode.Enter )
			DoClick();

		base.OnKeyCodeTyped( code );
	}

	public override void OnKillFocus()
	{
		if ( _menu is not null && !_menu.HasFocus )
			HideMenu();

		base.OnKillFocus();
	}

	public override void DoClick()
	{
		if ( _menu is null )
			return;

		if ( _menu.Visible )
		{
			HideMenu();
			return;
		}

		if ( !_menu.Enabled )
			return;

		_menu.PerformLayout();

		var (x, y) = LocalToScreen( 0, Tall );

		_menu.SetPos( x, y + 1 );

		var surface = Vgui.Surface;

		_menu.SetPos( Math.Min( Math.Max( _menu.X, 0 ), surface.ScreenWide - _menu.Wide ), _menu.Y );

		MoveToFront();
		ForceDepressed( true );

		_menu.SetVisible( true );
		_menu.RequestFocus();
	}
}

public sealed class FrameSystemButton : MenuButton
{
	private BitmapImage _enabledImage;
	private BitmapImage _disabledImage;
	private Color32 _enabledColor;
	private Color32 _disabledColor;

	public bool Responsive { get; set; } = true;

	public FrameSystemButton( Frame frame ) : base( frame.Vgui, frame, null, "" )
	{
		SetEnabled( false );
		SetMouseClickEnabled( MouseCode.Right, true );
	}

	public override void SetEnabled( bool state )
	{
		base.SetEnabled( state );

		var image = Enabled ? _enabledImage : _disabledImage;
		var color = Enabled ? _enabledColor : _disabledColor;

		if ( image is not null )
			SetImageAtIndex( 0, image, 0 );

		SetBgColor( color );
		SetDefaultColor( color, color );
		SetArmedColor( color, color );
		SetDepressedColor( color, color );
	}

	protected override Border GetBorder( bool depressed, bool armed, bool selected, bool keyFocus ) => null;

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		_enabledColor = GetSchemeColor( "TitleBarBgColor" );
		_disabledColor = GetSchemeColor( "TitleBarDisabledBgColor" );

		_enabledImage = new BitmapImage( Vgui.Surface.LoadImage( Vgui.Scheme.GetResourceString( "TitleBarIcon" ) ) );
		_disabledImage = new BitmapImage( Vgui.Surface.LoadImage( Vgui.Scheme.GetResourceString( "TitleBarDisabledIcon" ) ) );

		SetTextInset( 0, 0 );
		SetEnabled( Enabled );
	}

	public override void OnMousePressed( MouseCode code )
	{
		if ( Responsive )
			base.OnMousePressed( code );
	}

	public override void OnMouseDoublePressed( MouseCode code )
	{
		if ( Responsive && code == MouseCode.Left && Parent is not null )
			PostMessage( Parent, new Message( "CloseFrameButtonPressed" ) );
	}
}

public class Frame : EditablePanel
{
	private const float TitleButtonSpacing = 20f;
	private const float TitleButtonSize = 18f;
	private const float TitleButtonY = 8f;
	private const float TitleBarInset = 5f;
	private const float TitleBarBottom = 28f;
	private const float TitleTextY = 9f;
	private const float TitleTextTrim = 72f;

	private readonly TextImage _title;
	private readonly GripPanel[] _grips = new GripPanel[8];
	private readonly CaptionGripPanel _captionGrip;
	private readonly FrameButton _closeButton;
	private readonly FrameSystemButton _menuButton;

	private Menu _sysMenu;
	private Color32 _titleBarBgColor;
	private Color32 _titleBarDisabledBgColor;
	private Color32 _titleBarFgColor;
	private Color32 _titleBarDisabledFgColor;
	private bool _sizeable = true;
	private bool _deleteSelfOnClose;
	private string _closeSound;
	private bool _moveable = true;
	private bool _hasFocus;
	private bool _titleBarVisible = true;

	public Frame( Vgui vgui, Panel parent, string name ) : base( vgui, parent, name )
	{
		SetVisible( false );
		MakePopup();

		_title = new TextImage( vgui, "Untitled" );

		BuildGroup = OwnBuildGroup;
		OwnBuildGroup.PanelAdded( this );

		SetMinimumSize( Scaled( 128 ), Scaled( 66 ) );

		NavGroup.SetFocusTopLevel( true );

		_grips[0] = new GripPanel( this, 0, -1 );
		_grips[1] = new GripPanel( this, 0, 1 );
		_grips[2] = new GripPanel( this, -1, 0 );
		_grips[3] = new GripPanel( this, 1, 0 );
		_grips[4] = new GripPanel( this, -1, -1 );
		_grips[5] = new GripPanel( this, 1, -1 );
		_grips[6] = new GripPanel( this, -1, 1 );
		_grips[7] = new GripPanel( this, 1, 1 );
		_captionGrip = new CaptionGripPanel( this );

		SetupResizeCursors();

		_closeButton = new FrameButton( this, "resource/icon_close" );
		_closeButton.AddActionSignalTarget( this );
		_closeButton.SetCommand( new Message( "CloseFrameButtonPressed" ) );

		vgui.Surface.EscapeKeySupported = true;

		_menuButton = new FrameSystemButton( this );
		_menuButton.SetMenu( GetSysMenu() );
	}

	public bool IsSizeable => _sizeable;

	public bool IsMoveable => _moveable;

	public void SetSizeable( bool state )
	{
		_sizeable = state;

		SetupResizeCursors();
	}

	public void SetCloseSound( string fileName ) => _closeSound = fileName;

	private void SetupResizeCursors()
	{
		_grips[0].SetCursor( _sizeable ? CursorCode.SizeNs : CursorCode.Arrow );
		_grips[1].SetCursor( _sizeable ? CursorCode.SizeNs : CursorCode.Arrow );
		_grips[2].SetCursor( _sizeable ? CursorCode.SizeWe : CursorCode.Arrow );
		_grips[3].SetCursor( _sizeable ? CursorCode.SizeWe : CursorCode.Arrow );
		_grips[4].SetCursor( _sizeable ? CursorCode.SizeNwse : CursorCode.Arrow );
		_grips[5].SetCursor( _sizeable ? CursorCode.SizeNesw : CursorCode.Arrow );
		_grips[6].SetCursor( _sizeable ? CursorCode.SizeNesw : CursorCode.Arrow );
		_grips[7].SetCursor( _sizeable ? CursorCode.SizeNwse : CursorCode.Arrow );
		_grips[7].SetPaintEnabled( _sizeable );
		_grips[7].SetPaintBackgroundEnabled( _sizeable );
	}

	public void SetCloseButtonVisible( bool state ) => _closeButton.SetVisible( state );

	protected void SetMenuButtonResponsive( bool state ) => _menuButton.Responsive = state;

	public void SetTitle( string title ) => _title.SetText( title );

	private Menu GetSysMenu()
	{
		if ( _sysMenu is not null )
			return _sysMenu;

		_sysMenu = new Menu( Vgui, this, null );
		_sysMenu.SetVisible( false );
		_sysMenu.AddActionSignalTarget( this );

		_sysMenu.GetMenuItem( _sysMenu.AddMenuItem( "Minimize", "#SysMenu_Minimize", "Minimize", this ) ).SetEnabled( false );
		_sysMenu.GetMenuItem( _sysMenu.AddMenuItem( "Maximize", "#SysMenu_Maximize", "Maximize", this ) ).SetEnabled( false );
		_sysMenu.GetMenuItem( _sysMenu.AddMenuItem( "Close", "#SysMenu_Close", "Close", this ) ).SetEnabled( _closeButton.Visible );

		return _sysMenu;
	}

	public virtual void Activate()
	{
		MoveToFront();
		RequestFocus();
		SetVisible( true );
		SetEnabled( true );
	}

	private void ReleaseModal()
	{
		Vgui.Input.ReleaseAppModalSurface();
	}

	public virtual void Close()
	{
		if ( Vgui.Input.AppModalSurface == this )
			ReleaseModal();

		SetVisible( false );

		if ( _deleteSelfOnClose )
			MarkForDeletion();
	}

	public void SetDeleteSelfOnClose( bool state ) => _deleteSelfOnClose = state;

	public void MoveToCenterOfScreen()
	{
		var surface = Vgui.Surface;

		SetPos( (surface.ScreenWide - Wide) / 2, (surface.ScreenTall - Tall) / 2 );
	}

	private void CloseModal()
	{
		ReleaseModal();

		PostMessage( this, new Message( "Close" ) );
	}

	public (int X, int Y, int Wide, int Tall) GetClientArea()
	{
		var x = Scaled( 5 );
		var y = 0;
		var wide = Wide;
		var tall = Tall;

		if ( _titleBarVisible )
		{
			y = Scaled( 5 ) + Surface.GetFontTall( _title.Font ) + Scaled( 8 );
			tall -= Scaled( 5 ) + y;
		}

		wide -= Scaled( 5 ) + x;

		return (x, y, wide, tall);
	}

	public override void LoadControlSettings( string resourceName )
	{
		base.LoadControlSettings( resourceName );

		NavGroup.GetDefaultPanel()?.RequestFocus();
	}

	public override void OnMessage( Message message, Panel from )
	{
		switch ( message.Name )
		{
			case "Close":
				Close();
				break;

			case "CloseModal":
				CloseModal();
				break;

			case "CloseFrameButtonPressed":
				if ( _closeSound is not null )
					Surface.PlaySound( _closeSound );

				OnCommand( "Close" );
				break;

			default:
				base.OnMessage( message, from );
				break;
		}
	}

	public override void OnCommand( string command )
	{
		if ( command.Equals( "Close", StringComparison.OrdinalIgnoreCase ) )
			Close();
		else if ( command.Equals( "CloseModal", StringComparison.OrdinalIgnoreCase ) )
			CloseModal();
		else
			base.OnCommand( command );
	}

	public override void OnMousePressed( MouseCode code )
	{
		if ( Vgui.Input.Focus is not { } focus || !focus.HasParent( this ) )
			RequestFocus();
	}

	public override void OnKeyCodePressed( KeyCode code )
	{
		if ( code != KeyCode.Escape || !Vgui.Surface.EscapeKeySupported )
			return;

		PostMessage( this, new Message( "CloseFrameButtonPressed" ) );
		PostMessage( this, new Message( "Command" ) { Text = "Cancel" } );
	}

	public override void OnKeyCodeTyped( KeyCode code )
	{
		var input = Vgui.Input;

		if ( input.IsControlDown && code == KeyCode.F4 )
		{
			PostMessage( this, new Message( "CloseFrameButtonPressed" ) );
			return;
		}

		if ( code == KeyCode.Enter )
		{
			if ( NavGroup.CurrentDefaultButton is { Visible: true, Enabled: true } button )
				PostMessage( button, new Message( "Hotkey" ) );
		}
		else if ( code == KeyCode.Escape && Vgui.Surface.EscapeKeySupported && input.AppModalSurface == this )
		{
			CloseModal();
		}
	}

	public override void OnKeyTyped( char character )
	{
		if ( NavGroup.FindPanelByHotkey( char.ToLowerInvariant( character ) ) is { } panel )
			PostMessage( panel, new Message( "Hotkey" ) );
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		_titleBarFgColor = GetSchemeColor( "TitleBarFgColor" );
		_titleBarBgColor = GetSchemeColor( "TitleBarBgColor" );
		_titleBarDisabledFgColor = GetSchemeColor( "TitleBarDisabledFgColor" );
		_titleBarDisabledBgColor = GetSchemeColor( "TitleBarDisabledBgColor" );

		_title.SetFont( GetFont( "Default" ) );
		_title.ResizeImageToContent();
	}

	public override void ApplySettings( KeyValues settings )
	{
		settings.SetInt( "visible", -1 );

		base.ApplySettings( settings );

		if ( settings.GetInt( "settitlebarvisible", 1 ) == 0 )
			_titleBarVisible = false;

		if ( settings.GetString( "title" ) is { Length: > 0 } title )
			SetTitle( title );
	}

	public override void PerformLayout()
	{
		var wide = Wide;
		var tall = Tall;
		var draggerSize = Scaled( 5 );
		var cornerSize = Scaled( 8 );
		var bottomRightSize = Scaled( 18 );

		_grips[0].SetBounds( cornerSize, 0, wide - (cornerSize * 2), draggerSize );
		_grips[2].SetBounds( 0, cornerSize, draggerSize, tall - (cornerSize * 2) );
		_grips[4].SetBounds( 0, 0, cornerSize, cornerSize );
		_grips[5].SetBounds( wide - cornerSize, 0, cornerSize, cornerSize );
		_grips[6].SetBounds( 0, tall - cornerSize, cornerSize, cornerSize );
		_grips[1].SetBounds( cornerSize, tall - draggerSize, wide - bottomRightSize - cornerSize, draggerSize );
		_grips[3].SetBounds( wide - draggerSize, cornerSize, draggerSize, tall - bottomRightSize - cornerSize );
		_grips[7].SetBounds( wide - bottomRightSize, tall - bottomRightSize, bottomRightSize, bottomRightSize );

		_captionGrip.SetSize( wide - Scaled( 10 ), Scaled( 23 ) );

		foreach ( var grip in _grips )
			grip.MoveToFront();

		_menuButton.MoveToFront();
		_menuButton.SetBounds( Scaled( 7 ), Scaled( 8 ), Scaled( 18 ), Scaled( 18 ) );

		if ( !_closeButton.Visible )
			return;

		var scale = ProportionalScale;
		var size = (int)(scale * TitleButtonSize);

		_closeButton.SetPos( wide - (int)(scale * TitleBarInset) - (int)(scale * TitleButtonSpacing), (int)(scale * TitleButtonY) );
		_closeButton.SetSize( size, size );
		_closeButton.SetTextInset( (int)Math.Ceiling( (double)(scale + scale) ), (int)Math.Ceiling( (double)scale ) );
	}

	public override void PaintBackground()
	{
		var surface = Vgui.Surface;
		var hasFocus = Vgui.Input.Focus is { } focus && focus.HasParent( this );

		base.PaintBackground();

		if ( _hasFocus != hasFocus )
		{
			_hasFocus = hasFocus;

			_closeButton.SetDisabledLook( !hasFocus );
			_menuButton.SetEnabled( hasFocus );

			_closeButton.InvalidateLayout();
			_menuButton.InvalidateLayout();
		}

		Vgui.Scheme.GetBorder( "FrameBorder" )?.Paint( surface, 0, 0, Wide, Tall );

		if ( !_titleBarVisible )
			return;

		var scale = ProportionalScale;
		var inset = (int)(scale * TitleBarInset);
		var bottom = scale * TitleBarBottom;

		surface.DrawSetColor( hasFocus ? _titleBarBgColor : _titleBarDisabledBgColor );
		surface.DrawFilledRect( inset, inset, (int)(Wide - (scale * TitleBarInset)), (int)bottom );

		_title.Color = hasFocus ? _titleBarFgColor : _titleBarDisabledFgColor;
		_title.SetPos( (int)bottom, (int)(scale * TitleTextY) );
		_title.SetSize( (int)(Wide - (scale * TitleTextTrim)), Surface.GetFontTall( _title.Font ) );
		_title.Paint( surface );
	}
}
