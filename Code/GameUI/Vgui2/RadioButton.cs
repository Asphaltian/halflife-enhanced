namespace GoldSrc.GameUI;

public sealed class RadioImage : Image
{
	private readonly RadioButton _radioButton;
	private readonly BitmapImage _selected;
	private readonly BitmapImage _unselected;

	public RadioImage( RadioButton radioButton )
	{
		_radioButton = radioButton;

		var surface = radioButton.Vgui.Surface;

		_selected = surface.LoadImage( "resource/icon_radiosel" ) is { } selected ? new BitmapImage( selected ) : null;
		_unselected = surface.LoadImage( "resource/icon_radiounsel" ) is { } unselected ? new BitmapImage( unselected ) : null;

		SetSize( radioButton.Scaled( 20 ), radioButton.Scaled( 13 ) );
	}

	public override void Paint( Surface surface )
	{
		if ( (_radioButton.IsSelected ? _selected : _unselected) is not { } image )
			return;

		var (wide, tall) = image.GetContentSize();

		image.SetPos( X, Y );
		image.SetSize( _radioButton.Scaled( wide ), _radioButton.Scaled( tall ) );
		image.Paint( surface );
		image.SetSize( wide, tall );
	}
}

public sealed class RadioButton : ToggleButton
{
	private readonly RadioImage _radioBoxImage;

	private Color32 _selectedFgColor;
	private int _oldTabPosition;
	private int _subTabPosition;

	public RadioButton( Vgui vgui, Panel parent, string name, string text ) : base( vgui, parent, name, text )
	{
		SetContentAlignment( Alignment.West );
		SetScaleImages( false );

		_radioBoxImage = new RadioImage( this );

		SetTextImageIndex( 1 );
		SetImageAtIndex( 0, _radioBoxImage, 0 );
		SetButtonActivationType( ActivationType.OnPressed );
	}

	public int SubTabPosition => _subTabPosition;

	public int RadioTabPosition => _oldTabPosition;

	public void SetSubTabPosition( int position ) => _subTabPosition = position;

	protected override Border GetBorder( bool depressed, bool armed, bool selected, bool keyFocus ) => null;

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		SetFgColor( GetSchemeColor( "FgColor" ) );

		_selectedFgColor = GetSchemeColor( "BrightControlText", GetSchemeColor( "ControlText" ) );

		SetContentAlignment( Alignment.West );
		SetImageAtIndex( 0, _radioBoxImage, 0 );
		SetPaintBackgroundEnabled( false );
	}

	public override void ApplySettings( KeyValues settings )
	{
		base.ApplySettings( settings );

		SetTextColorState( ColorState.Normal );

		_subTabPosition = settings.GetInt( "SubTabPosition" );
		_oldTabPosition = TabPosition;

		SetImageAtIndex( 0, _radioBoxImage, 0 );
	}

	public override void PerformLayout()
	{
		SetFgColor( IsSelected ? _selectedFgColor : GetButtonFgColor() );

		LayoutLabel();
	}

	public override void Paint() => PaintLabel();

	public override void SetSelected( bool state )
	{
		if ( state )
		{
			if ( !Enabled )
				return;

			TabPosition = _oldTabPosition;

			if ( Parent is { } parent )
			{
				foreach ( var child in parent.Children )
				{
					if ( child != this )
						PostMessage( child, new Message( "RadioButtonChecked" ) { Int = _oldTabPosition, Panel = this } );
				}
			}

			RequestFocus();

			PostActionSignal( new Message( "RadioButtonChecked" ) { Int = _oldTabPosition, Panel = this } );
		}
		else
		{
			if ( TabPosition != 0 )
				_oldTabPosition = TabPosition;

			TabPosition = 0;
		}

		InvalidateLayout();

		base.SetSelected( state );
	}

	public override void DoClick() => SetSelected( true );

	public override void OnMessage( Message message, Panel from )
	{
		if ( message.Name == "RadioButtonChecked" )
		{
			if ( message.Int == _oldTabPosition )
				SetSelected( false );

			return;
		}

		base.OnMessage( message, from );
	}

	private RadioButton FindBestRadioButton( int direction )
	{
		RadioButton bestRadio = null;
		var highestRadio = 0;

		if ( Parent is not { } parent )
			return null;

		foreach ( var child in parent.Children )
		{
			if ( child is not RadioButton radio || radio._oldTabPosition != _oldTabPosition )
				continue;

			if ( radio._subTabPosition == _subTabPosition + direction )
			{
				bestRadio = radio;
				break;
			}

			if ( radio._subTabPosition == 0 && direction == 1 )
			{
				bestRadio = radio;
			}
			else if ( highestRadio < radio._subTabPosition && direction == -1 )
			{
				highestRadio = radio._subTabPosition;
				bestRadio = radio;
			}
			else
			{
				bestRadio ??= radio;
			}
		}

		bestRadio?.RequestFocus();

		InvalidateLayout();

		return bestRadio;
	}

	public override void OnKeyCodeTyped( KeyCode code )
	{
		switch ( code )
		{
			case KeyCode.Enter:
			case KeyCode.Space:
				if ( !IsSelected )
					SetSelected( true );
				else
					CallParentFunction( new Message( "KeyCodeTyped" ) { Int = (int)code } );

				break;

			case KeyCode.Tab:
				FindBestRadioButton( 1 );
				break;

			case KeyCode.Up:
			case KeyCode.Left:
				FindBestRadioButton( -1 )?.SetSelected( true );
				break;

			case KeyCode.Down:
			case KeyCode.Right:
				FindBestRadioButton( 1 )?.SetSelected( true );
				break;

			default:
				base.OnKeyCodeTyped( code );
				break;
		}
	}
}
