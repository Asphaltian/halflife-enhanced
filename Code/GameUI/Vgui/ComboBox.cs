using System;

namespace GoldSrc.GameUI;

public sealed class ComboBoxButton : Button
{
	private readonly BitmapImage _image;

	private Color32 _disabledBgColor;

	public ComboBoxButton( ComboBox comboBox ) : base( comboBox.Vgui, comboBox, null, "" )
	{
		SetButtonActivationType( ActivationType.OnPressed );

		_image = new BitmapImage( Vgui.Surface.LoadImage( "resource/icon_down" ) );
	}

	protected override Border GetBorder( bool depressed, bool armed, bool selected, bool keyFocus ) => null;

	protected override Color32 GetButtonBgColor() => Enabled ? base.GetButtonBgColor() : _disabledBgColor;

	public override void OnCursorExited() => CallParentFunction( new Message( "CursorExited" ) );

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		SetDefaultBorder( Vgui.Scheme.GetBorder( "ScrollBarButtonBorder" ) );

		var bgColor = GetSchemeColor( "MenuButton/ButtonBgColor" );
		var armedColor = GetSchemeColor( "MenuButton/ArmedArrowColor" );

		SetArmedColor( armedColor, bgColor );
		SetDepressedColor( armedColor, bgColor );
		SetDefaultColor( GetSchemeColor( "MenuButton/ButtonArrowColor" ), bgColor );

		_disabledBgColor = GetSchemeColor( "ControlBG" );

		AddImage( _image, 0 );
	}
}

public class ComboBox : TextEntry
{
	private readonly Menu _dropDown;
	private readonly ComboBoxButton _button;

	private bool _highlight;

	public ComboBox( Vgui vgui, Panel parent, string name, int numLines, bool allowEdit ) : base( vgui, parent, name )
	{
		SetEditable( allowEdit );
		SetHorizontalScrolling( false );

		_dropDown = new Menu( vgui, this, null );
		_dropDown.AddActionSignalTarget( this );

		_button = new ComboBoxButton( this );
		_button.SetCommand( "ButtonClicked" );
		_button.AddActionSignalTarget( this );

		_dropDown.SetNumberOfVisibleItems( numLines );
	}

	public int AddItem( string itemText ) => _dropDown.AddMenuItem( null, itemText, new Message( "SetText" ) { Text = itemText }, this );

	public int AddItem( string itemText, KeyValues userData )
	{
		return _dropDown.AddMenuItem( itemText, itemText, new Message( "SetText" ) { Text = itemText }, this, userData );
	}

	public int GetItemCount() => _dropDown.ItemCount;

	public bool IsItemIDValid( int itemID ) => _dropDown.IsValidMenuID( itemID );

	public void DeleteAllItems() => _dropDown.DeleteAllItems();

	public KeyValues GetItemUserData( int itemID ) => _dropDown.GetItemUserData( itemID );

	public KeyValues GetActiveItemUserData() => _dropDown.GetItemUserData( GetActiveItem() );

	public virtual void ActivateItem( int itemID ) => _dropDown.ActivateItem( itemID );

	public void ActivateItemByRow( int row ) => _dropDown.ActivateItem( _dropDown.GetMenuID( row ) );

	public int GetActiveItem() => _dropDown.ActiveItem;

	public override void OnMessage( Message message, Panel from )
	{
		switch ( message.Name )
		{
			case "ActivateItem":
				ActivateItem( message.Int );
				break;

			case "MenuClose":
				OnMenuClose();
				break;

			case "MenuItemSelected":
				_highlight = true;
				break;

			case "SetText":
				OnSetText( message.Text );
				break;

			default:
				base.OnMessage( message, from );
				break;
		}
	}

	private void OnSetText( string newText )
	{
		newText = Vgui.Localize( newText );

		if ( Text != newText )
		{
			SetText( newText );
			PostActionSignal( new Message( "TextChanged" ) { Text = newText } );
		}

		HideMenu();
	}

	private void OnMenuClose()
	{
		HideMenu();

		if ( HasFocus )
		{
			SelectAllText();
		}
		else if ( _highlight )
		{
			_highlight = false;

			RequestFocus();
		}
		else if ( IsCursorOver() )
		{
			SelectAllText();
			OnCursorEntered();
			RequestFocus();
		}
		else
		{
			_button.SetArmed( false );
		}
	}

	public override void OnCommand( string command )
	{
		if ( command.Equals( "ButtonClicked", StringComparison.OrdinalIgnoreCase ) )
			DoClick();

		base.OnCommand( command );
	}

	private void HideMenu() => _dropDown.SetVisible( false );

	private int FindRow( string text )
	{
		for ( var row = 0; row < _dropDown.ItemCount; row++ )
		{
			if ( _dropDown.GetMenuItem( _dropDown.GetMenuID( row ) ).Text == text )
				return row;
		}

		return -1;
	}

	private void DoClick()
	{
		if ( _dropDown.Visible )
		{
			HideMenu();
			return;
		}

		if ( !_dropDown.Enabled )
			return;

		_dropDown.PerformLayout();

		var row = FindRow( Text );

		if ( row >= 0 )
			_dropDown.SetCurrentlyHighlightedItem( row );

		DoMenuLayout();
		MoveToFront();

		_dropDown.SetVisible( true );
		_dropDown.RequestFocus();

		SelectNoText();

		_button.SetArmed( true );
	}

	private void DoMenuLayout()
	{
		var (x, y) = LocalToScreen( 0, Tall );

		_dropDown.SetPos( x, y + Scaled( 1 ) );
		_dropDown.SetFixedWidth( Wide );
	}

	private void SelectRow( int row )
	{
		OnSetText( _dropDown.GetMenuItem( _dropDown.GetMenuID( row ) ).Text );
		SelectAllText();

		_dropDown.ActivateItem( row );
	}

	private void MoveAlongMenuItemList( int direction )
	{
		var row = FindRow( Text );

		if ( row >= 0 )
			SelectRow( Math.Clamp( row + direction, 0, _dropDown.ItemCount - 1 ) );
	}

	public override void OnSizeChanged( int wide, int tall )
	{
		base.OnSizeChanged( wide, tall );

		PerformLayout();
		SetDrawWidth( wide - _button.Wide );
	}

	public override void OnSetFocus()
	{
		base.OnSetFocus();

		GotoTextEnd();
		SelectAllText();
	}

	public override void OnKillFocus() => SelectNoText();

	public override void OnCursorEntered() => _button.OnCursorEntered();

	public override void OnCursorExited()
	{
		if ( !_dropDown.Visible )
			_button.SetArmed( false );
	}

	public override void OnMousePressed( MouseCode code )
	{
		if ( !Enabled )
			return;

		if ( !IsCursorOver() )
		{
			HideMenu();
			return;
		}

		if ( IsEditable )
		{
			HideMenu();
			return;
		}

		RequestFocus();
		DoClick();
	}

	public override void OnMouseDoublePressed( MouseCode code )
	{
		if ( !IsEditable )
			OnMousePressed( code );
	}

	public override void OnKeyCodeTyped( KeyCode code )
	{
		switch ( code )
		{
			case KeyCode.Enter:
			case KeyCode.Space:
				DoClick();
				return;

			case KeyCode.Up when _dropDown.Visible:
				MoveAlongMenuItemList( -1 );
				return;

			case KeyCode.Down when _dropDown.Visible:
				MoveAlongMenuItemList( 1 );
				return;
		}

		base.OnKeyCodeTyped( code );
	}

	public override void OnKeyTyped( char character )
	{
		if ( IsEditable )
		{
			base.OnKeyTyped( character );
			return;
		}

		var count = _dropDown.ItemCount;
		var current = Math.Max( _dropDown.ActiveItem, 0 );
		var row = current + 1 < count ? current + 1 : 0;

		while ( row != current )
		{
			var text = _dropDown.GetMenuItem( _dropDown.GetMenuID( row ) ).Text;

			if ( text.Length > 0 && char.ToLowerInvariant( text[0] ) == char.ToLowerInvariant( character ) )
				break;

			row = row + 1 < count ? row + 1 : 0;
		}

		if ( row >= 0 && row < count )
			SelectRow( row );
		else
			base.OnKeyTyped( character );
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		SetBorder( Vgui.Scheme.GetBorder( "ComboBoxBorder" ) );
	}

	public override void PerformLayout()
	{
		base.PerformLayout();

		SetCursor( IsEditable ? CursorCode.IBeam : CursorCode.Arrow );

		var (wide, tall) = PaintSize;

		_button.SetBounds( wide - Scaled( 20 ), Scaled( 2 ), Scaled( 16 ), tall - Scaled( 2 ) );
		_button.SetEnabled( Enabled );

		DoMenuLayout();
	}
}
