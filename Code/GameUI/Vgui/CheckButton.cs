namespace GoldSrc.GameUI;

public class ToggleButton : Button
{
	private Color32 _selectedColor;

	public ToggleButton( Vgui vgui, Panel parent, string name, string text ) : base( vgui, parent, name, text )
	{
		SetButtonActivationType( ActivationType.OnPressed );
	}

	public override bool CanBeDefaultButton => false;

	protected override Color32 GetButtonFgColor() => IsSelected ? _selectedColor : base.GetButtonFgColor();

	public override void DoClick()
	{
		if ( IsSelected )
			ForceDepressed( false );
		else
			ForceDepressed( true );

		SetSelected( !IsSelected );
		FireActionSignal();

		PostActionSignal( new Message( "ButtonToggled" ) { Int = IsSelected ? 1 : 0 } );
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		_selectedColor = GetSchemeColor( "BrightControlText" );
	}

	public override void OnKeyCodePressed( KeyCode code )
	{
		if ( code == KeyCode.Space )
		{
			DoClick();
			return;
		}

		base.OnKeyCodePressed( code );
	}

	public override void OnKeyCodeTyped( KeyCode code )
	{
		if ( code is KeyCode.Enter or KeyCode.Space )
			return;

		base.OnKeyCodeTyped( code );
	}
}

public class CheckButton : ToggleButton
{
	private readonly Image _uncheckedImage;
	private readonly Image _checkedImage;

	private Color32 _selectedFgColor = new( 196, 181, 80, 255 );
	private bool _checkButtonCheckable = true;

	public CheckButton( Vgui vgui, Panel parent, string name, string text ) : base( vgui, parent, name, text )
	{
		SetContentAlignment( Alignment.West );

		_checkedImage = LoadImage( "resource/icon_checked" );
		_uncheckedImage = LoadImage( "resource/icon_emptybox" );
	}

	private Image LoadImage( string name ) => Vgui.Surface.LoadImage( name ) is { } texture ? new BitmapImage( texture ) : null;

	public void SetCheckButtonCheckable( bool state ) => _checkButtonCheckable = state;

	protected override Color32 GetButtonFgColor() => IsSelected ? _selectedFgColor : base.GetButtonFgColor();

	protected override Border GetBorder( bool depressed, bool armed, bool selected, bool keyFocus ) => null;

	public override void SetSelected( bool state )
	{
		if ( !_checkButtonCheckable )
			return;

		PostActionSignal( new Message( "CheckButtonChecked" ) { Int = state ? 1 : 0 } );

		base.SetSelected( state );

		if ( _uncheckedImage is not null && !state )
			SetImageAtIndex( 0, _uncheckedImage, 0 );

		if ( _checkedImage is not null && state )
			SetImageAtIndex( 0, _checkedImage, 0 );
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		SetDefaultColor( GetSchemeColor( "FgColor" ), GetBgColor() );

		_selectedFgColor = GetSchemeColor( "BrightControlText", GetSchemeColor( "ControlText" ) );

		SetPaintBackgroundEnabled( false );
		SetTextImageIndex( 1 );

		if ( (IsSelected ? _checkedImage : null) is { } image )
			SetImageAtIndex( 0, image, 0 );
		else if ( _uncheckedImage is not null )
			SetImageAtIndex( 0, _uncheckedImage, 0 );
	}
}
