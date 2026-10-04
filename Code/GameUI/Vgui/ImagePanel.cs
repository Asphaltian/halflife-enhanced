namespace GoldSrc.GameUI;

public sealed class ImagePanel( Vgui vgui, Panel parent, string name ) : Panel( vgui, parent, name )
{
	private static readonly Color32 White = new( 255, 255, 255, 255 );

	private Image _image;
	private bool _scaleImage;
	private string _imageName;

	public Image Image => _image;

	public override void ApplySettings( KeyValues settings )
	{
		_imageName = null;
		_scaleImage = settings.GetInt( "scaleImage" ) != 0;

		var imageName = settings.GetString( "image" );

		if ( imageName.Length != 0 )
		{
			_imageName = imageName;

			InvalidateLayout( false, true );
		}

		var border = settings.GetString( "border" );

		if ( border.Length != 0 )
			SetBorder( Vgui.Scheme.GetBorder( border ) );

		base.ApplySettings( settings );
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		if ( !string.IsNullOrEmpty( _imageName ) )
			SetImage( Vgui.Surface.LoadImage( _imageName ) is { } texture ? new BitmapImage( texture ) : null );
	}

	public void SetImage( Image image, bool scaleImage )
	{
		_scaleImage = scaleImage;

		SetImage( image );
	}

	public void SetImage( Image image )
	{
		_image = image;

		if ( image is null )
			return;

		if ( _scaleImage )
		{
			image.SetSize( Wide, Tall );
			return;
		}

		var (wide, tall) = image.GetSize();

		SetSize( wide, tall );
	}

	public override void OnSizeChanged( int wide, int tall )
	{
		if ( _scaleImage )
			_image?.SetSize( wide, tall );

		base.OnSizeChanged( wide, tall );
	}

	public override void PaintBackground()
	{
		if ( _image is null )
			return;

		Vgui.Surface.DrawSetColor( White );

		_image.SetPos( 0, 0 );
		_image.Paint( Vgui.Surface );
	}
}
