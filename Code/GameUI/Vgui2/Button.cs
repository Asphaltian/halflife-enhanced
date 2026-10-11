namespace GoldSrc.GameUI;

public enum ActivationType
{
	OnPressedAndReleased,
	OnPressed,
	OnReleased
}

public class Button : Label
{
	private bool _armed;
	private bool _depressed;
	private bool _selected;
	private bool _forceDepressed;
	private bool _buttonBorderEnabled = true;
	private bool _useCaptureMouse = true;
	private bool _buttonKeyDown;
	private bool _defaultButton;
	private int _mouseClickMask;
	private ActivationType _activationType;
	private Message _actionMessage;
	private Border _defaultBorder;
	private Border _depressedBorder;
	private Border _keyFocusBorder;
	private Color32 _defaultFgColor;
	private Color32 _defaultBgColor;
	private Color32 _armedFgColor;
	private Color32 _armedBgColor;
	private Color32 _depressedFgColor;
	private Color32 _depressedBgColor;
	private Color32 _keyboardFocusColor;
	private string _armedSound;
	private string _depressedSound;
	private string _releasedSound;

	public Button( Vgui vgui, Panel parent, string name, string text ) : base( vgui, parent, name, text )
	{
		SetTextInset( Scaled( 6 ), 0 );
		SetMouseClickEnabled( MouseCode.Left, true );
		SetTextColorState( ColorState.BrightBase );
		SetPaintBackgroundEnabled( true );
	}

	public bool IsArmed => _armed;

	public bool IsDepressed => _depressed;

	public virtual void SetArmed( bool state )
	{
		if ( _armed == state )
			return;

		_armed = state;

		RecalculateDepressedState();
		InvalidateLayout();

		if ( _armed && _armedSound is not null )
			Surface.PlaySound( _armedSound );
	}

	public void SetArmedSound( string fileName ) => _armedSound = fileName;

	public void SetDepressedSound( string fileName ) => _depressedSound = fileName;

	public void SetReleasedSound( string fileName ) => _releasedSound = fileName;

	public bool IsSelected => _selected;

	public virtual void SetSelected( bool state )
	{
		if ( _selected == state )
			return;

		_selected = state;

		RecalculateDepressedState();
		InvalidateLayout();
	}

	public void ForceDepressed( bool state )
	{
		_forceDepressed = state;

		RecalculateDepressedState();
	}

	private void RecalculateDepressedState()
	{
		_depressed = Enabled && (_forceDepressed || (_armed && _selected));
	}

	public bool IsUseCaptureMouseEnabled => _useCaptureMouse;

	public void SetUseCaptureMouse( bool state ) => _useCaptureMouse = state;

	public void SetMouseClickEnabled( MouseCode code, bool state )
	{
		if ( state )
			_mouseClickMask |= 1 << ((int)code + 1);
		else
			_mouseClickMask &= ~(1 << ((int)code + 1));
	}

	public bool IsMouseClickEnabled( MouseCode code ) => (_mouseClickMask & (1 << ((int)code + 1))) != 0;

	public void SetButtonActivationType( ActivationType type ) => _activationType = type;

	public void SetButtonBorderEnabled( bool state )
	{
		if ( state == _buttonBorderEnabled )
			return;

		_buttonBorderEnabled = state;

		InvalidateLayout();
	}

	public void SetCommand( string command ) => _actionMessage = new Message( "Command" ) { Text = command };

	public void SetCommand( Message message ) => _actionMessage = message;

	public override bool CanBeDefaultButton => true;

	public void SetAsDefaultButton( bool state ) => SetDefaultButtonFlag( state, "DefaultButtonSet" );

	public void SetAsCurrentDefaultButton( bool state ) => SetDefaultButtonFlag( state, "CurrentDefaultButtonSet" );

	private void SetDefaultButtonFlag( bool state, string notification )
	{
		if ( _defaultButton == state )
			return;

		_defaultButton = state;

		if ( state && Parent is not null )
			PostMessage( Parent, new Message( notification ) { Panel = this } );

		InvalidateLayout();
	}

	public void SetDefaultColor( Color32 fgColor, Color32 bgColor )
	{
		if ( _defaultFgColor.Equals( fgColor ) && _defaultBgColor.Equals( bgColor ) )
			return;

		_defaultFgColor = fgColor;
		_defaultBgColor = bgColor;

		InvalidateLayout();
	}

	public void SetArmedColor( Color32 fgColor, Color32 bgColor )
	{
		if ( _armedFgColor.Equals( fgColor ) && _armedBgColor.Equals( bgColor ) )
			return;

		_armedFgColor = fgColor;
		_armedBgColor = bgColor;

		InvalidateLayout();
	}

	public void SetDepressedColor( Color32 fgColor, Color32 bgColor )
	{
		if ( _depressedFgColor.Equals( fgColor ) && _depressedBgColor.Equals( bgColor ) )
			return;

		_depressedFgColor = fgColor;
		_depressedBgColor = bgColor;

		InvalidateLayout();
	}

	public void SetDefaultBorder( Border border )
	{
		_defaultBorder = border;

		InvalidateLayout();
	}

	public void SetDepressedBorder( Border border )
	{
		_depressedBorder = border;

		InvalidateLayout();
	}

	public void SetKeyFocusBorder( Border border )
	{
		_keyFocusBorder = border;

		InvalidateLayout();
	}

	protected virtual Color32 GetButtonFgColor()
	{
		if ( _depressed )
			return _depressedFgColor;

		return _armed ? _armedFgColor : _defaultFgColor;
	}

	protected virtual Color32 GetButtonBgColor()
	{
		if ( _depressed )
			return _depressedBgColor;

		return _armed ? _armedBgColor : _defaultBgColor;
	}

	protected virtual Border GetBorder( bool depressed, bool armed, bool selected, bool keyFocus )
	{
		if ( depressed )
			return _depressedBorder;

		if ( _buttonBorderEnabled && (keyFocus || (Enabled && _defaultButton)) )
			return _keyFocusBorder;

		return _defaultBorder;
	}

	public virtual void DoClick()
	{
		if ( (_depressedSound ?? _releasedSound) is { } sound )
			Surface.PlaySound( sound );

		SetSelected( true );
		FireActionSignal();
		SetSelected( false );
	}

	protected virtual void FireActionSignal()
	{
		if ( _actionMessage is not null )
			PostActionSignal( new Message( _actionMessage.Name ) { Text = _actionMessage.Text, Int = _actionMessage.Int } );
	}

	public override void OnMessage( Message message, Panel from )
	{
		switch ( message.Name )
		{
			case "SetAsDefaultButton":
				SetAsDefaultButton( message.Int != 0 );
				break;

			case "SetAsCurrentDefaultButton":
				SetAsCurrentDefaultButton( message.Int != 0 );
				break;

			default:
				base.OnMessage( message, from );
				break;
		}
	}

	protected override void OnHotkeyPressed() => DoClick();

	public override void OnSetFocus() => InvalidateLayout();

	public override void OnKillFocus() => InvalidateLayout();

	public override void OnCursorEntered()
	{
		if ( Enabled )
			SetArmed( true );
	}

	public override void OnCursorExited()
	{
		if ( !_buttonKeyDown )
			SetArmed( false );
	}

	public override void OnMousePressed( MouseCode code )
	{
		if ( !Enabled || !IsMouseClickEnabled( code ) )
			return;

		if ( _activationType == ActivationType.OnPressed )
		{
			DoClick();
			return;
		}

		if ( !_useCaptureMouse || _activationType != ActivationType.OnPressedAndReleased )
			return;

		RequestFocus();
		SetSelected( true );

		Vgui.Input.SetMouseCapture( this );
	}

	public override void OnMouseDoublePressed( MouseCode code ) => OnMousePressed( code );

	public override void OnMouseReleased( MouseCode code )
	{
		if ( _useCaptureMouse )
			Vgui.Input.SetMouseCapture( null );

		if ( _activationType == ActivationType.OnPressed || !IsMouseClickEnabled( code ) )
			return;

		if ( !_selected && _activationType == ActivationType.OnPressedAndReleased )
			return;

		if ( Enabled && (Vgui.Input.MouseOver == this || _buttonKeyDown) )
		{
			DoClick();
			return;
		}

		SetSelected( false );
	}

	public override void OnKeyCodePressed( KeyCode code )
	{
		if ( code is KeyCode.Space or KeyCode.Enter )
		{
			SetArmed( true );

			_buttonKeyDown = true;

			OnMousePressed( MouseCode.Left );

			if ( _useCaptureMouse )
				Vgui.Input.SetMouseCapture( null );

			return;
		}

		_buttonKeyDown = false;

		base.OnKeyCodePressed( code );
	}

	public override void OnKeyCodeReleased( KeyCode code )
	{
		if ( _buttonKeyDown && code is KeyCode.Space or KeyCode.Enter )
		{
			SetArmed( true );
			OnMouseReleased( MouseCode.Left );
		}
		else
		{
			base.OnKeyCodeReleased( code );
		}

		_buttonKeyDown = false;

		SetArmed( false );
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		var scheme = Vgui.Scheme;

		_defaultBorder = scheme.GetBorder( "ButtonBorder" );
		_depressedBorder = scheme.GetBorder( "ButtonDepressedBorder" );
		_keyFocusBorder = scheme.GetBorder( "ButtonKeyFocusBorder" );

		_defaultFgColor = GetSchemeColor( "ButtonFgColor", GetFgColor() );
		_defaultBgColor = GetSchemeColor( "ButtonBgColor", GetBgColor() );
		_armedFgColor = GetSchemeColor( "ButtonArmedFgColor", _defaultFgColor );
		_armedBgColor = GetSchemeColor( "ButtonArmedBgColor", _defaultBgColor );
		_depressedFgColor = GetSchemeColor( "ButtonDepressedFgColor", _defaultFgColor );
		_depressedBgColor = GetSchemeColor( "ButtonDepressedBgColor", _defaultBgColor );
		_keyboardFocusColor = GetSchemeColor( "ButtonFocusBorder", new Color32( 0, 0, 0, 255 ) );

		InvalidateLayout();
	}

	public override void ApplySettings( KeyValues settings )
	{
		base.ApplySettings( settings );

		SetTextColorState( ColorState.BrightBase );

		if ( settings.GetString( "command" ) is { Length: > 0 } command )
			SetCommand( command );

		if ( settings.GetInt( "default" ) != 0 && CanBeDefaultButton )
			SetAsDefaultButton( true );
	}

	public override void PerformLayout()
	{
		SetBorder( GetBorder( _depressed, _armed, _selected, HasFocus ) );
		SetFgColor( GetButtonFgColor() );
		SetBgColor( GetButtonBgColor() );

		base.PerformLayout();
	}

	public override void Paint()
	{
		base.Paint();

		if ( HasFocus && Enabled )
			DrawFocusBorder( 3, 3, Wide - 4, Tall - 2 );
	}

	protected virtual void DrawFocusBorder( int x0, int y0, int x1, int y1 )
	{
		var size = Scaled( 1 );

		Vgui.Surface.DrawSetColor( _keyboardFocusColor );

		DrawDashedLine( x0, y0, x1, y0 + size, size, size );
		DrawDashedLine( x0, y0, x0 + size, y1, size, size );
		DrawDashedLine( x0, y1 - size, x1, y1, size, size );
		DrawDashedLine( x1 - size, y0, x1, y1, size, size );
	}

	private void DrawDashedLine( int x0, int y0, int x1, int y1, int dashLength, int dashGap )
	{
		var surface = Vgui.Surface;

		if ( x1 - x0 <= y1 - y0 )
		{
			do
			{
				var end = dashLength + y0;

				surface.DrawFilledRect( x0, y0, x1, System.Math.Min( end, y1 ) );

				y0 = dashGap + end;
			}
			while ( y0 <= y1 );

			return;
		}

		do
		{
			var end = dashLength + x0;

			surface.DrawFilledRect( x0, y0, System.Math.Min( end, x1 ), y1 );

			x0 = dashGap + end;
		}
		while ( x0 <= x1 );
	}
}
