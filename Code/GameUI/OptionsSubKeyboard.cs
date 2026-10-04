using System;

namespace GoldSrc.GameUI;

public sealed class InlineEditPanel : Panel
{
	private static readonly Color32 FillColor = new( 149, 136, 49, 255 );
	private static readonly Color32 DarkEdgeColor = new( 45, 49, 40, 255 );
	private static readonly Color32 LightEdgeColor = new( 150, 159, 142, 255 );

	public InlineEditPanel( Vgui vgui ) : base( vgui, null, "InlineEditPanel" )
	{
	}

	public override void Paint()
	{
		var surface = Vgui.Surface;

		surface.DrawSetColor( FillColor );
		surface.DrawFilledRect( 0, 0, Wide, Tall );

		surface.DrawSetColor( DarkEdgeColor );
		surface.DrawFilledRect( -3, -3, Wide + 2, -2 );
		surface.DrawFilledRect( -2, -2, -3, Tall + 3 );

		surface.DrawSetColor( LightEdgeColor );
		surface.DrawFilledRect( -2, Tall + 3, Wide + 3, Tall + 2 );
		surface.DrawFilledRect( Wide + 3, -3, Wide + 2, Tall + 2 );
	}

	public override void OnKeyCodeTyped( KeyCode code ) => Parent?.OnKeyCodeTyped( code );

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		SetBorder( Vgui.Scheme.GetBorder( "DepressedButtonBorder" ) );
	}

	public override void OnMousePressed( MouseCode code ) => Parent?.OnMousePressed( code );
}

public sealed class ControlsListPanel : SectionedListPanel
{
	private readonly InlineEditPanel _inlineEditPanel;

	private bool _captureMode;
	private int _clickRow;

	public ControlsListPanel( Vgui vgui, Panel parent, string name ) : base( vgui, parent, name )
	{
		_inlineEditPanel = new InlineEditPanel( vgui );
	}

	private (int X, int Y) _captureCursor;

	public bool IsCapturing => _captureMode;

	public void SetItemOfInterest( int itemID ) => _clickRow = itemID;

	public int GetItemOfInterest() => _clickRow;

	public void StartCaptureMode( CursorCode cursor )
	{
		_captureMode = true;

		EnterEditMode( _clickRow, 1, _inlineEditPanel );

		var input = Vgui.Input;

		if ( cursor == CursorCode.None )
			_captureCursor = input.CursorPos;

		input.SetMouseFocus( _inlineEditPanel );
		input.SetMouseCapture( _inlineEditPanel );

		if ( cursor != CursorCode.User )
			_inlineEditPanel.SetCursor( cursor );
	}

	public void EndCaptureMode( CursorCode cursor )
	{
		_captureMode = false;

		Vgui.Input.SetMouseCapture( null );

		LeaveEditMode();
		RequestFocus();

		Vgui.Input.SetMouseFocus( this );

		if ( cursor != CursorCode.None )
			Vgui.Input.SetCursorPos( _captureCursor.X, _captureCursor.Y );

		if ( cursor == CursorCode.User )
			return;

		_inlineEditPanel.SetCursor( cursor );

		Vgui.Surface.SetCursor( cursor );
	}

	public override void OnMousePressed( MouseCode code )
	{
		if ( IsCapturing )
			Parent?.OnMousePressed( code );
		else
			base.OnMousePressed( code );
	}

	public override void OnMouseDoublePressed( MouseCode code )
	{
		if ( IsItemIDValid( GetSelectedItem() ) )
			OnKeyCodePressed( KeyCode.Enter );
		else
			base.OnMouseDoublePressed( code );
	}
}

public sealed class OptionsSubKeyboard : PropertyPage
{
	private const int ActionColumnWidth = 226;
	private const int KeyColumnWidth = 128;
	private const int EscapeKey = 27;
	private const int MouseWheelDown = 239;
	private const int MouseWheelUp = 240;
	private const int FirstMouseButton = 241;
	private const int LastMouseButton = 245;

	private readonly ControlsListPanel _keyBindList;
	private readonly Button _setBindingButton;
	private readonly Button _clearBindingButton;

	public OptionsSubKeyboard( Vgui vgui, Panel parent ) : base( vgui, parent, null )
	{
		_keyBindList = new ControlsListPanel( vgui, this, "listpanel_keybindlist" );

		ParseActionDescriptions();

		_setBindingButton = new Button( vgui, this, "ChangeKeyButton", "" );
		_clearBindingButton = new Button( vgui, this, "ClearKeyButton", "" );

		LoadControlSettings( "Resource/OptionsSubKeyboard.res" );

		_setBindingButton.SetEnabled( false );
		_clearBindingButton.SetEnabled( false );
	}

	private void ParseActionDescriptions()
	{
		if ( Server.LoadMountText( "gfx/shell/kb_act.lst" ) is not { } data )
			return;

		var sectionIndex = 0;
		var position = 0;

		while ( Common.Parse( data, ref position ) is { Length: > 0 } binding )
		{
			if ( Common.Parse( data, ref position ) is not { Length: > 0 } description )
				break;

			if ( description[0] == '=' )
				continue;

			if ( binding.Equals( "blank", StringComparison.OrdinalIgnoreCase ) )
			{
				sectionIndex++;

				_keyBindList.AddSection( sectionIndex, description );
				_keyBindList.AddColumnToSection( sectionIndex, "Action", description, SectionedListColumn.ColumnBright, Scaled( ActionColumnWidth ) );
				_keyBindList.AddColumnToSection( sectionIndex, "Code", "#GameUI_KeyButton", SectionedListColumn.ColumnBright, Scaled( KeyColumnWidth ) );
				_keyBindList.AddColumnToSection( sectionIndex, "AltCode", "#GameUI_Alternate", SectionedListColumn.ColumnBright, Scaled( KeyColumnWidth ) );
				continue;
			}

			var item = new KeyValues( "Item" );

			item.SetString( "Action", Vgui.Localize( description ) );
			item.SetString( "Binding", binding );
			item.SetString( "Key", "" );
			item.SetString( "AltKey", "" );
			item.SetString( "Code", "" );
			item.SetString( "AltCode", "" );

			_keyBindList.AddItem( sectionIndex, item );
		}
	}

	private KeyValues GetItemForBinding( string binding )
	{
		for ( var i = 0; i < _keyBindList.ItemCount; i++ )
		{
			if ( _keyBindList.GetItemData( _keyBindList.GetItemIDFromRow( i ) ) is { } item && item.GetString( "Binding" ).Equals( binding, StringComparison.OrdinalIgnoreCase ) )
				return item;
		}

		return null;
	}

	private void ClearBindItems()
	{
		for ( var i = 0; i < _keyBindList.ItemCount; i++ )
		{
			var itemID = _keyBindList.GetItemIDFromRow( i );

			if ( _keyBindList.GetItemData( itemID ) is not { } item )
				continue;

			item.SetString( "Key", "" );
			item.SetString( "AltKey", "" );
			item.SetString( "Code", "" );
			item.SetString( "AltCode", "" );

			_keyBindList.InvalidateItem( itemID );
		}

		_keyBindList.InvalidateLayout();
	}

	private void RemoveKeyFromBindItems( string key )
	{
		if ( string.IsNullOrEmpty( key ) )
			return;

		for ( var i = 0; i < _keyBindList.ItemCount; i++ )
		{
			var itemID = _keyBindList.GetItemIDFromRow( i );

			if ( _keyBindList.GetItemData( itemID ) is not { } item )
				continue;

			if ( key.Equals( item.GetString( "AltKey" ), StringComparison.OrdinalIgnoreCase ) )
			{
				item.SetString( "AltKey", "" );
				item.SetString( "AltCode", "" );

				_keyBindList.InvalidateItem( itemID );
			}

			if ( !key.Equals( item.GetString( "Key" ), StringComparison.OrdinalIgnoreCase ) )
				continue;

			item.SetString( "Key", "" );
			item.SetString( "Code", "" );

			_keyBindList.InvalidateItem( itemID );

			if ( item.GetString( "AltKey" ) is not { Length: > 0 } alternate )
				continue;

			item.SetString( "Key", alternate );
			item.SetString( "Code", item.GetString( "AltCode" ) );
			item.SetString( "AltKey", "" );
			item.SetString( "AltCode", "" );
		}

		_keyBindList.InvalidateLayout();
	}

	private void AddBinding( KeyValues item, string key, string keyName )
	{
		if ( item.GetString( "Key" ).Equals( key, StringComparison.OrdinalIgnoreCase ) )
			return;

		RemoveKeyFromBindItems( key );

		item.SetString( "AltKey", item.GetString( "Key" ) );
		item.SetString( "Key", key );
		item.SetString( "AltCode", item.GetString( "Code" ) );
		item.SetString( "Code", keyName );
	}

	private void FillInCurrentBindings()
	{
		ClearBindItems();

		foreach ( var (action, binding) in Keys.ActionBindings )
		{
			if ( GetItemForBinding( binding ) is not { } item )
				continue;

			var key = Keys.GetBind( action ) ?? "";
			var alternate = Keys.GetAlternateBind( action ) ?? "";

			if ( Keys.KeyNumber( alternate ) < Keys.KeyNumber( key ) )
				(key, alternate) = (alternate, key);

			if ( key.Length != 0 )
				AddBinding( item, key, Keys.DisplayName( key ) );

			if ( alternate.Length != 0 )
				AddBinding( item, alternate, Keys.DisplayName( alternate ) );
		}
	}

	private static string DefaultKeyName( string key )
	{
		return Keys.KeyNumber( key ) switch
		{
			>= FirstMouseButton and <= LastMouseButton => key.ToUpperInvariant(),
			MouseWheelDown or MouseWheelUp => "",
			_ => Keys.DisplayName( key )
		};
	}

	private void FillInDefaultBindings()
	{
		ClearBindItems();

		foreach ( var (key, binding) in Keys.DefaultBindings() )
		{
			if ( GetItemForBinding( binding ) is { } item )
				AddBinding( item, key, DefaultKeyName( key ) );
		}

		PostActionSignal( new Message( "ApplyButtonEnable" ) );
	}

	private void ApplyAllBindings()
	{
		foreach ( var (action, binding) in Keys.ActionBindings )
		{
			if ( GetItemForBinding( binding ) is not { } item )
				continue;

			Keys.SetBinds( action, item.GetString( "Key" ), item.GetString( "AltKey" ) );
		}

		Keys.SaveBinds();

		Cmd.AddText( "exec userconfig.cfg\n" );
	}

	protected override void OnResetData()
	{
		FillInCurrentBindings();

		_keyBindList.SetSelectedItem( 0 );
	}

	protected override void OnApplyChanges() => ApplyAllBindings();

	private void ItemSelected( int itemID )
	{
		_keyBindList.SetItemOfInterest( itemID );

		if ( !_keyBindList.IsItemIDValid( itemID ) )
		{
			_setBindingButton.SetEnabled( false );
			_clearBindingButton.SetEnabled( false );
			return;
		}

		_setBindingButton.SetEnabled( true );

		if ( _keyBindList.GetItemData( itemID ) is not { } item )
			return;

		_clearBindingButton.SetEnabled( item.GetString( "Key" ).Length != 0 );

		if ( item.GetInt( "Header" ) != 0 )
			_setBindingButton.SetEnabled( false );
	}

	private void Finish( string key, string keyName )
	{
		var itemID = _keyBindList.GetItemOfInterest();

		_keyBindList.EndCaptureMode( CursorCode.Arrow );

		if ( _keyBindList.GetItemData( itemID ) is { } item )
		{
			if ( !string.IsNullOrEmpty( key ) )
			{
				AddBinding( item, key, keyName );
				PostActionSignal( new Message( "ApplyButtonEnable" ) );
			}

			_keyBindList.InvalidateItem( itemID );
		}

		_setBindingButton.SetEnabled( true );
		_clearBindingButton.SetEnabled( true );
	}

	public override void OnKeyCodeTyped( KeyCode code )
	{
		if ( !_keyBindList.IsCapturing )
		{
			base.OnKeyCodeTyped( code );
			return;
		}

		var key = Keys.NameForKey( (int)code );

		if ( key is null || Keys.KeyNumber( key ) is 0 or EscapeKey )
			key = null;

		Finish( key, key is null ? null : Keys.DisplayName( key ) );
	}

	public override void OnMousePressed( MouseCode code )
	{
		if ( !_keyBindList.IsCapturing )
		{
			base.OnMousePressed( code );
			return;
		}

		var key = Keys.NameForMouse( (int)code );

		Finish( key, key.ToUpperInvariant() );
	}

	public override void OnMouseDoublePressed( MouseCode code ) => OnMousePressed( code );

	public override void OnMouseWheeled( int delta )
	{
		if ( !_keyBindList.IsCapturing )
		{
			base.OnMouseWheeled( delta );
			return;
		}

		var key = delta > 0 ? "mwheelup" : "mwheeldown";

		Finish( key, key.ToUpperInvariant() );
	}

	public override void OnKeyCodePressed( KeyCode code )
	{
		if ( !_keyBindList.IsCapturing && code == KeyCode.Delete )
		{
			var itemID = _keyBindList.GetItemOfInterest();

			if ( _keyBindList.GetCellBounds( itemID, 1, out _, out _, out _, out _ ) )
			{
				var item = _keyBindList.GetItemData( itemID );

				RemoveKeyFromBindItems( item.GetString( "AltKey" ) );
				RemoveKeyFromBindItems( item.GetString( "Key" ) );

				_clearBindingButton.SetEnabled( false );
				_keyBindList.InvalidateItem( itemID );
				return;
			}
		}

		base.OnKeyCodePressed( code );
	}

	public override void OnMessage( Message message, Panel from )
	{
		if ( message.Name == "ItemSelected" )
		{
			ItemSelected( message.Int );
			return;
		}

		base.OnMessage( message, from );
	}

	public override void OnCommand( string command )
	{
		if ( command.Equals( "Defaults", StringComparison.OrdinalIgnoreCase ) )
		{
			var box = new QueryBox( Vgui, "#GameUI_KeyboardSettings", "#GameUI_KeyboardSettingsText", null );

			box.AddActionSignalTarget( this );
			box.SetOKCommand( new Message( "Command" ) { Text = "DefaultsOK" } );
			box.DoModal( null );
		}
		else if ( command.Equals( "DefaultsOK", StringComparison.OrdinalIgnoreCase ) )
		{
			FillInDefaultBindings();

			_keyBindList.RequestFocus();
		}
		else if ( !_keyBindList.IsCapturing && command.Equals( "ChangeKey", StringComparison.OrdinalIgnoreCase ) )
		{
			_keyBindList.StartCaptureMode( CursorCode.None );
		}
		else if ( !_keyBindList.IsCapturing && command.Equals( "ClearKey", StringComparison.OrdinalIgnoreCase ) )
		{
			OnKeyCodePressed( KeyCode.Delete );

			_keyBindList.RequestFocus();
		}
		else
		{
			base.OnCommand( command );
		}
	}
}
