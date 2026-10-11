using System;

namespace GoldSrc.GameUI;

public class PropertyPage : EditablePanel
{
	private PageTab _pageTab;
	private bool _paintBorder;

	public PropertyPage( Vgui vgui, Panel parent, string name, bool paintBorder = true ) : base( vgui, parent, name )
	{
		_paintBorder = paintBorder;
	}

	public void SetPaintBorder( bool state ) => _paintBorder = state;

	protected virtual void OnResetData()
	{
	}

	protected virtual void OnApplyChanges()
	{
	}

	protected virtual void OnPageShow()
	{
	}

	protected virtual void OnPageHide()
	{
	}

	private static PageTab Alive( PageTab tab ) => tab is { Deleted: false } ? tab : null;

	public override void OnMessage( Message message, Panel from )
	{
		switch ( message.Name )
		{
			case "ResetData":
				OnResetData();
				break;

			case "ApplyChanges":
				OnApplyChanges();
				break;

			case "PageShow":
				OnPageShow();
				break;

			case "PageHide":
				OnPageHide();
				break;

			case "PageTabActivated":
				_pageTab = message.Panel as PageTab;
				break;

			default:
				base.OnMessage( message, from );
				break;
		}
	}

	public override void SetVisible( bool state )
	{
		if ( Visible && !state && NavGroup.CurrentDefaultButton is not null )
			NavGroup.SetCurrentDefaultButton( null, true );

		base.SetVisible( state );
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		if ( _paintBorder )
			SetBorder( Vgui.Scheme.GetBorder( "ButtonBorder" ) );
	}

	public override void OnKeyCodeTyped( KeyCode code )
	{
		if ( code is KeyCode.Left or KeyCode.Right )
		{
			if ( Alive( _pageTab ) is { HasFocus: true } )
				base.OnKeyCodeTyped( code );

			return;
		}

		base.OnKeyCodeTyped( code );
	}

	public override void PaintBorder()
	{
		if ( _paintBorder && Border is { } border && Alive( _pageTab ) is { } tab )
		{
			border.Paint( Vgui.Surface, 0, 0, Wide, Tall, 1, tab.X + 1, tab.Wide - 1 + tab.X );
			return;
		}

		base.PaintBorder();
	}
}

public sealed class PageTab : Button
{
	private readonly int _maxTabWidth;

	private bool _active;
	private Color32 _textColor;
	private Color32 _dimTextColor;

	public PageTab( Vgui vgui, Panel parent, string text, int maxTabWidth ) : base( vgui, parent, "tab", text )
	{
		_maxTabWidth = maxTabWidth;

		SetCommand( new Message( "TabPressed" ) );
	}

	public bool ShiftedLeft { get; set; }

	public bool ShiftedRight { get; set; }

	public override bool CanBeDefaultButton => false;

	public void SetActive( bool state )
	{
		_active = state;

		InvalidateLayout();
	}

	protected override Color32 GetButtonFgColor() => _active ? _textColor : _dimTextColor;

	protected override Border GetBorder( bool depressed, bool armed, bool selected, bool keyFocus )
	{
		return Vgui.Scheme.GetBorder( _active ? "TabActiveBorder" : "TabBorder" );
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		_textColor = GetSchemeColor( "BrightControlText", GetFgColor() );
		_dimTextColor = GetSchemeColor( "FgColorDim", GetFgColor() );

		SetSize( Math.Max( _maxTabWidth, Scaled( 10 ) + GetContentSize().Wide ), Tall );
	}

	public override void OnKeyCodeTyped( KeyCode code )
	{
		if ( code == KeyCode.Left )
			ShiftedLeft = true;
		else if ( code == KeyCode.Right )
			ShiftedRight = true;

		base.OnKeyCodeTyped( code );
	}

	public override void OnMousePressed( MouseCode code )
	{
		if ( code == MouseCode.Right )
		{
			PostActionSignal( new Message( "OpenContextMenu" ) { Int = -1 } );
			return;
		}

		if ( !Enabled || !IsMouseClickEnabled( code ) || !IsUseCaptureMouseEnabled )
			return;

		RequestFocus();
		FireActionSignal();
		SetSelected( true );

		Vgui.Input.SetMouseCapture( this );
	}

	public override void OnMouseReleased( MouseCode code )
	{
		if ( IsUseCaptureMouseEnabled )
			Vgui.Input.SetMouseCapture( null );

		SetSelected( false );
	}
}

public sealed class PropertySheet : Panel
{
	private readonly List<PropertyPage> _pages = [];
	private readonly List<PageTab> _pageTabs = [];

	private PropertyPage _activePage;
	private PageTab _activeTab;
	private readonly int _tabHeight;
	private int _tabWidth;
	private int _activeTabIndex;
	private bool _tabFocus;

	public PropertySheet( Vgui vgui, Panel parent, string name ) : base( vgui, parent, name )
	{
		_tabWidth = Scaled( 64 );
		_tabHeight = Scaled( 28 );
	}

	public PropertyPage ActivePage => _activePage;

	public int ActivePageNum => _pages.IndexOf( _activePage );

	public int NumPages => _pages.Count;

	public PropertyPage GetPage( int index ) => index >= 0 && index < _pages.Count ? _pages[index] : null;

	public void AddPage( PropertyPage page, string title )
	{
		if ( page is null || _pages.Contains( page ) )
			return;

		var tab = new PageTab( Vgui, this, title, _tabWidth );

		tab.AddActionSignalTarget( this );

		_pageTabs.Add( tab );
		_pages.Add( page );

		page.SetParent( this );
		page.AddActionSignalTarget( this );

		PostMessage( page, new Message( "ResetData" ) );

		page.SetVisible( false );
	}

	public void SetActivePage( PropertyPage page )
	{
		var index = _pages.IndexOf( page );

		if ( index >= 0 )
			ChangeActiveTab( index );
	}

	public void SetTabWidth( int pixels )
	{
		_tabWidth = pixels;

		InvalidateLayout();
	}

	public void ResetAllData()
	{
		foreach ( var page in _pages )
			PostMessage( page, new Message( "ResetData" ) );
	}

	public void ApplyChanges()
	{
		foreach ( var page in _pages )
			PostMessage( page, new Message( "ApplyChanges" ) );
	}

	public void ChangeActiveTab( int index )
	{
		if ( GetPage( index ) is not { } page )
			return;

		if ( page == _activePage )
		{
			_activeTab.RequestFocus();
			_tabFocus = true;
			return;
		}

		if ( _activePage is not null )
		{
			PostMessage( _activePage, new Message( "PageHide" ) );
			PostMessage( _activePage, new Message( "PageTabActivated" ) );

			_activePage.SetVisible( false );
		}

		if ( _activeTab is not null )
		{
			_activeTab.SetActive( false );

			_tabFocus = _activeTab.HasFocus;
		}
		else
		{
			_tabFocus = false;
		}

		_activePage = page;
		_activeTab = _pageTabs[index];
		_activeTabIndex = index;

		_activePage.SetVisible( true );
		_activePage.MoveToFront();

		_activeTab.SetVisible( true );
		_activeTab.MoveToFront();
		_activeTab.SetActive( true );

		if ( _tabFocus )
			_activeTab.RequestFocus();
		else
			_activePage.RequestFocus();

		PostMessage( _activePage, new Message( "PageShow" ) );
		PostMessage( _activePage, new Message( "PageTabActivated" ) { Panel = _activeTab } );

		PostActionSignal( new Message( "PageChanged" ) );

		InvalidateLayout();
	}

	public override void OnMessage( Message message, Panel from )
	{
		switch ( message.Name )
		{
			case "TabPressed":
				ChangeActiveTab( _pageTabs.IndexOf( message.Panel as PageTab ) );
				break;

			case "OpenContextMenu":
				PostActionSignal( new Message( "OpenContextMenu" ) );
				break;

			case "ApplyButtonEnable":
				PostActionSignal( new Message( "ApplyButtonEnable" ) );
				break;

			case "DefaultButtonSet":
			case "CurrentDefaultButtonSet":
				if ( Parent is not null )
					PostMessage( Parent, new Message( message.Name ) { Panel = message.Panel } );

				break;

			case "FindDefaultButton":
				if ( Parent is not null )
					PostMessage( Parent, new Message( "FindDefaultButton" ) );

				break;

			default:
				base.OnMessage( message, from );
				break;
		}
	}

	public override void RequestFocus( int direction = 0 )
	{
		if ( direction != -1 && direction != 0 && _activeTab is not null )
		{
			_activeTab.RequestFocus( direction );
			_tabFocus = true;
			return;
		}

		if ( _activePage is not null )
		{
			_activePage.RequestFocus( direction );
			_tabFocus = false;
		}
	}

	public override bool RequestFocusPrev( Panel panel = null )
	{
		if ( _activeTab is not null )
		{
			if ( !_tabFocus )
			{
				if ( Parent is not null )
					PostMessage( Parent, new Message( "FindDefaultButton" ) );

				_activeTab.RequestFocus( -1 );
				_tabFocus = true;

				return true;
			}

			if ( _activeTab.ShiftedLeft )
			{
				_activeTab.ShiftedLeft = false;

				ChangeActiveTab( _activeTabIndex - 1 );

				return true;
			}
		}

		_tabFocus = false;

		return base.RequestFocusPrev();
	}

	public override bool RequestFocusNext( Panel panel = null )
	{
		if ( !_tabFocus || _activePage is null || _activeTab is null )
			return base.RequestFocusNext();

		if ( _activeTab.ShiftedRight )
		{
			_activeTab.ShiftedRight = false;

			ChangeActiveTab( _activeTabIndex + 1 );

			return true;
		}

		_activePage.RequestFocus( 1 );
		_tabFocus = false;

		return true;
	}

	public override Panel HasHotkey( char key )
	{
		if ( _activePage is null )
			return null;

		foreach ( var child in _activePage.Children )
		{
			if ( child.HasHotkey( key ) is { } hotkey )
				return hotkey;
		}

		return null;
	}

	public override void OnCommand( string command )
	{
		if ( command.Equals( "Close", StringComparison.OrdinalIgnoreCase ) )
			CallParentFunction( new Message( "Command" ) { Text = command } );
	}

	public override void OnKeyCodeTyped( KeyCode code )
	{
		var control = Vgui.Input.IsControlDown;

		if ( code == KeyCode.LBracket )
		{
			if ( control )
				ChangeActiveTab( _activeTabIndex - 1 );

			return;
		}

		if ( code == KeyCode.RBracket )
		{
			if ( control )
				ChangeActiveTab( _activeTabIndex + 1 );

			return;
		}

		base.OnKeyCodeTyped( code );
	}

	public override void PerformLayout()
	{
		if ( _activePage is null )
		{
			ChangeActiveTab( 0 );

			_activePage?.RequestFocus();
		}

		if ( _activePage is not null )
		{
			_activePage.SetBounds( 0, _tabHeight, Wide, Tall - _tabHeight );
			_activePage.InvalidateLayout();
		}

		var x = 0;

		foreach ( var tab in _pageTabs )
		{
			if ( tab == _activeTab )
				tab.SetBounds( x, Scaled( 2 ), tab.Wide, Scaled( 27 ) );
			else
				tab.SetBounds( x, Scaled( 4 ), tab.Wide, Scaled( 25 ) );

			tab.SetVisible( true );

			x += tab.Wide + 1;
		}

		_activePage?.MoveToFront();
		_activeTab?.MoveToFront();
	}
}
